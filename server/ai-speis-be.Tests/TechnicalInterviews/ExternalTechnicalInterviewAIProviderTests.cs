using System.Net;
using System.Text;
using System.Collections.Immutable;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class ExternalTechnicalInterviewAIProviderTests
{
    [Fact]
    public async Task EvaluateAnswerAsync_AcceptsLeadingJsonObjectWithOllamaTrailingMetadata()
    {
        const string modelContent = """
            {"evaluation":{"dimensionEvaluations":[{"rubricCode":"ACCURACY","evidence":["Docker packages applications consistently"],"missingEvidence":[],"suggestedScore":5}]}}repid="c1"
            """;
        var provider = CreateProvider(modelContent);

        var result = await provider.EvaluateAnswerAsync(CreateContext(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var dimension = Assert.Single(result.Data!.DimensionEvaluations);
        Assert.Equal("ACCURACY", dimension.RubricCode);
        Assert.Equal(5m, dimension.SuggestedScore);
    }

    [Fact]
    public async Task EvaluateAnswerAsync_RecoversJsonEmbeddedInLeadingProse()
    {
        const string modelContent = """
            Here is the evaluation: {"evaluation":{"dimensionEvaluations":[]}}
            """;
        var provider = CreateProvider(modelContent);

        var result = await provider.EvaluateAnswerAsync(CreateContext(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task EvaluateAnswerAsync_UsesDedicatedOllamaEvaluationModel()
    {
        var handler = new StaticResponseHandler("""
            {"model":"qwen2.5:7b","choices":[{"message":{"content":"{\\"evaluation\\":{\\"dimensionEvaluations\\":[]}}"}}]}
            """);
        var provider = CreateProvider(handler, "qwen2.5:7b");

        await provider.EvaluateAnswerAsync(CreateContext(), CancellationToken.None);

        Assert.Contains("\"model\":\"qwen2.5:7b\"", handler.RequestBody);
    }

    [Fact]
    public async Task EvaluateAnswerV2Async_RecoversJsonSurroundedByProseAndTrailingText()
    {
        const string modelContent = "Text before JSON\nHere is the evaluation: {\"evaluation\":{\"dimensionEvaluations\":[{\"rubricCode\":\"ACCURACY\",\"suggestedScore\":8,\"evidence\":[\"Docker packages applications consistently\"],\"missingEvidence\":[]}]}} JSON + trailing text.";
        var provider = CreateProvider(modelContent);

        var result = await provider.EvaluateAnswerV2Async(CreateV2Context(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(8m, result.Data!.Evaluation!.DimensionEvaluations!.Single().SuggestedScore);
        Assert.NotNull(result.JsonRecovery);
        Assert.NotEmpty(result.RawResponse!);
    }

    [Fact]
    public async Task EvaluateAnswerV2Async_RequestsOnlyBehaviouralStyleCriterionFields()
    {
        var handler = new StaticResponseHandler("""
            {"model":"aispeis","message":{"content":"{\"evaluation\":{\"dimensionEvaluations\":[]}}"}}
            """);
        var provider = CreateProvider(handler);

        await provider.EvaluateAnswerV2Async(CreateV2Context(), CancellationToken.None);

        Assert.Equal("http://ollama.test/api/chat", handler.RequestUri);
        Assert.Contains("\"stream\":false", handler.RequestBody);
        Assert.Contains("\"format\":{", handler.RequestBody);
        Assert.Contains("missingEvidence", handler.RequestBody);
        Assert.DoesNotContain("overallScore", handler.RequestBody);
        Assert.DoesNotContain("weightedScore", handler.RequestBody);
        Assert.DoesNotContain("\"strengths\"", handler.RequestBody);
        Assert.DoesNotContain("\"gaps\"", handler.RequestBody);
        Assert.Contains("TECHNICAL_DEPTH", handler.RequestBody);
        Assert.Contains("APPLICATION", handler.RequestBody);
        Assert.Contains("COMMUNICATION", handler.RequestBody);
    }

    [Fact]
    public async Task EvaluateAnswerV2Async_EmptyModelContentReturnsEmptyResponse()
    {
        var provider = CreateProvider("   ");

        var result = await provider.EvaluateAnswerV2Async(CreateV2Context(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("EMPTY_RESPONSE", result.ErrorCode);
        Assert.Null(result.RawResponse);
    }

    [Fact]
    public async Task EvaluateAnswerV2Async_UnrecoverableJsonReturnsFullResponseError()
    {
        var provider = CreateProvider("{\"evaluation\": { broken");

        var result = await provider.EvaluateAnswerV2Async(CreateV2Context(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("MALFORMED_JSON_UNRECOVERABLE", result.ErrorCode);
        Assert.NotEmpty(result.RawResponse!);
        Assert.NotNull(result.JsonRecovery);
    }

    [Fact]
    public async Task EvaluateAnswerV2Async_OllamaRepairsMissingClosingBrace()
    {
        const string modelContent = "{\"evaluation\":{\"dimensionEvaluations\":[{\"rubricCode\":\"ACCURACY\",\"suggestedScore\":8,\"evidence\":[\"Docker packages applications consistently\"],\"missingEvidence\":[]}]";
        var provider = CreateProvider(modelContent);

        var result = await provider.EvaluateAnswerV2Async(CreateV2Context(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(8m, result.Data!.Evaluation!.DimensionEvaluations!.Single().SuggestedScore);
        Assert.Contains("OLLAMA_JSON_BRACES_REPAIRED", result.JsonRecovery!.RecoveryFlags);
    }

    [Fact]
    public async Task EvaluateAnswerV2Async_OllamaNormalizesScalarMissingEvidence()
    {
        const string modelContent = "{\"evaluation\":{\"dimensionEvaluations\":[{\"rubricCode\":\"ACCURACY\",\"suggestedScore\":0,\"evidence\":[],\"missingEvidence\":\"No grounded evidence\"}]}}";
        var provider = CreateProvider(modelContent);

        var result = await provider.EvaluateAnswerV2Async(CreateV2Context(), CancellationToken.None);

        Assert.True(result.Success);
        var dimension = result.Data!.Evaluation!.DimensionEvaluations!.Single();
        Assert.Equal(new[] { "No grounded evidence" }, dimension.MissingEvidence);
        Assert.Contains("OLLAMA_SCALAR_ARRAY_NORMALIZED", result.JsonRecovery!.RecoveryFlags);
    }

    [Fact]
    public async Task EvaluateAnswerV2Async_RecoversObservedAispeisTextObjectInMissingEvidence()
    {
        // Raw content captured from a direct aispeis Technical V2 evaluation.
        // The JSON is valid, but APPLICATION.missingEvidence contains
        // [{"text":"..."}] instead of an array of strings.
        const string modelContent = """
            {"evaluation":{"dimensionEvaluations":[{"rubricCode":"ACCURACY","suggestedScore":7,"evidence":["The answer is correct and relevant."],"missingEvidence":[]},{"rubricCode":"TECHNICAL_DEPTH","suggestedScore":6,"evidence":["The answer explains the main points but does not analyze further."],"missingEvidence":[]},{"rubricCode":"REASONING","suggestedScore":7,"evidence":["The reasoning is clear and follows from the evidence."],"missingEvidence":[]},{"rubricCode":"APPLICATION","suggestedScore":6,"evidence":["A relevant example is provided, although the impact is not quantified."],"missingEvidence":[{"text":"The solution's impact is not measured."}]},{"rubricCode":"COMMUNICATION","suggestedScore":7,"evidence":["The answer is clear and professional."],"missingEvidence":[]}]}}
            """;
        var provider = CreateProvider(modelContent);

        var result = await provider.EvaluateAnswerV2Async(CreateV2Context(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(5, result.Data!.Evaluation!.DimensionEvaluations!.Count);
        var application = result.Data.Evaluation.DimensionEvaluations.Single(item => item.RubricCode == "APPLICATION");
        Assert.Equal(new[] { "The solution's impact is not measured." }, application.MissingEvidence);
        Assert.Contains("OLLAMA_TEXT_OBJECT_ARRAY_ITEM_NORMALIZED", result.JsonRecovery!.RecoveryFlags);
    }

    [Fact]
    public async Task EvaluateAnswerV2Async_ValidJsonWithWrongDtoShapeReportsContractMismatch()
    {
        const string modelContent = "{\"evaluation\":{\"dimensionEvaluations\":\"not-an-array\"}}";
        var provider = CreateProvider(modelContent);

        var result = await provider.EvaluateAnswerV2Async(CreateV2Context(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("INVALID_V2_CONTRACT", result.ErrorCode);
        Assert.Equal("CONTRACT_MISMATCH", result.JsonRecovery!.RecoveryStatus);
        Assert.Contains("V2_DTO_CONTRACT_MISMATCH", result.JsonRecovery.RecoveryFlags);
    }

    [Fact]
    public async Task EvaluateAnswerV2Async_OllamaUnwrapsStringifiedDimension()
    {
        const string modelContent = "{\"evaluation\":{\"dimensionEvaluations\":[\"{\\\"rubricCode\\\":\\\"ACCURACY\\\",\\\"suggestedScore\\\":8,\\\"evidence\\\":[\\\"Docker packages applications consistently\\\"],\\\"missingEvidence\\\":[]}\"]}}";
        var provider = CreateProvider(modelContent);

        var result = await provider.EvaluateAnswerV2Async(CreateV2Context(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("ACCURACY", result.Data!.Evaluation!.DimensionEvaluations!.Single().RubricCode);
        Assert.Contains("OLLAMA_STRINGIFIED_DIMENSION_UNWRAPPED", result.JsonRecovery!.RecoveryFlags);
    }

    [Fact]
    public async Task EvaluateAnswerV2Async_MalformedProviderEnvelopePreservesRawResponse()
    {
        const string providerEnvelope = "not a JSON response";
        var provider = CreateProvider(new StaticResponseHandler(providerEnvelope));

        var result = await provider.EvaluateAnswerV2Async(CreateV2Context(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("MALFORMED_PROVIDER_ENVELOPE", result.ErrorCode);
        Assert.Equal(providerEnvelope, result.RawResponse);
        Assert.Equal("UNRECOVERABLE", result.JsonRecovery!.RecoveryStatus);
    }

    [Fact]
    public async Task EvaluateAnswerV2Async_HttpErrorRemainsAFullProviderFailure()
    {
        var response = new StaticResponseHandler("{}", HttpStatusCode.BadGateway);
        var provider = CreateProvider(response);

        var result = await provider.EvaluateAnswerV2Async(CreateV2Context(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("HTTP_502", result.ErrorCode);
        Assert.Equal("aispeis", result.Model);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task OllamaProvider_OverridesGlobalGeminiConfiguration()
    {
        var handler = new StaticResponseHandler("""
            {"model":"aispeis","choices":[{"message":{"content":"{\"evaluation\":{\"dimensionEvaluations\":[]}}"}}]}
            """);
        var transport = CreateProviderWithOptions(handler, new TechnicalInterviewOptions
        {
            Provider = "gemini",
            BaseUrl = "http://gemini.test/v1/",
            OllamaBaseUrl = "http://ollama.test/v1/",
            OllamaModel = "aispeis",
            EvaluationTimeoutMs = 5_000,
            EvaluationMaxRetries = 0
        });

        var provider = new OllamaTechnicalInterviewAIProvider(transport);
        await provider.EvaluateAnswerAsync(CreateContext(), CancellationToken.None);

        Assert.Equal("ollama", provider.ProviderName);
        Assert.Equal("http://ollama.test/v1/chat/completions", handler.RequestUri);
        Assert.Contains("\"model\":\"aispeis\"", handler.RequestBody);
    }

    [Fact]
    public async Task EvaluateAnswerV2Async_GeminiKeepsOpenAiTransportAndPromptContract()
    {
        var handler = new StaticResponseHandler("""
            {"model":"gemini-test","choices":[{"message":{"content":"{\"evaluation\":{\"dimensionEvaluations\":[]}}"}}]}
            """);
        var provider = CreateProviderWithOptions(handler, new TechnicalInterviewOptions
        {
            Provider = "gemini",
            ApiKey = "test-key",
            BaseUrl = "http://gemini.test/v1/",
            OllamaBaseUrl = "http://ollama.test/v1/",
            OllamaModel = "aispeis",
            EvaluationTimeoutMs = 5_000,
            EvaluationMaxRetries = 0
        });

        await provider.EvaluateAnswerV2Async(CreateV2Context(), CancellationToken.None, "gemini");

        Assert.Equal("http://gemini.test/v1/chat/completions", handler.RequestUri);
        Assert.Contains("\"response_format\":{\"type\":\"json_schema\"", handler.RequestBody);
        Assert.Contains("\"name\":\"technical_v2_evaluation\"", handler.RequestBody);
        Assert.Contains("\"strict\":true", handler.RequestBody);
        Assert.DoesNotContain("\"stream\":false", handler.RequestBody);
        Assert.DoesNotContain("\"format\":{", handler.RequestBody);
        Assert.Contains("Use exactly the five supplied Technical rubric dimensions: ACCURACY, TECHNICAL_DEPTH, REASONING, APPLICATION and COMMUNICATION", handler.RequestBody);
    }

    [Fact]
    public async Task EvaluateAnswerV2Async_GeminiApplicationZeroResponseRemainsValid()
    {
        var handler = new StaticResponseHandler("""
            {"model":"gemini-test","choices":[{"message":{"content":"{\"evaluation\":{\"dimensionEvaluations\":[{\"rubricCode\":\"ACCURACY\",\"suggestedScore\":8,\"evidence\":[\"The answer is technically correct.\"],\"missingEvidence\":[]},{\"rubricCode\":\"TECHNICAL_DEPTH\",\"suggestedScore\":7,\"evidence\":[\"The mechanism is explained.\"],\"missingEvidence\":[]},{\"rubricCode\":\"REASONING\",\"suggestedScore\":6,\"evidence\":[\"The explanation follows a logical chain.\"],\"missingEvidence\":[]},{\"rubricCode\":\"APPLICATION\",\"suggestedScore\":0,\"evidence\":[],\"missingEvidence\":[\"No concrete real-world application example was provided.\"]},{\"rubricCode\":\"COMMUNICATION\",\"suggestedScore\":8,\"evidence\":[\"The answer is clear.\"],\"missingEvidence\":[]}]}}"}}]}
            """);
        var provider = CreateProviderWithOptions(handler, new TechnicalInterviewOptions
        {
            Provider = "gemini",
            ApiKey = "test-key",
            BaseUrl = "http://gemini.test/v1/",
            OllamaBaseUrl = "http://ollama.test/v1/",
            Model = "gemini-test",
            EvaluationTimeoutMs = 5_000,
            EvaluationMaxRetries = 0
        });

        var result = await provider.EvaluateAnswerV2Async(CreateV2Context(), CancellationToken.None, "gemini");

        Assert.True(result.Success);
        Assert.Equal("gemini-test", result.Model);
        Assert.Equal(5, result.Data!.Evaluation!.DimensionEvaluations!.Count);
        var application = result.Data.Evaluation.DimensionEvaluations.Single(item => item.RubricCode == "APPLICATION");
        Assert.Equal(0m, application.SuggestedScore);
        Assert.Empty(application.Evidence!);
        Assert.NotEmpty(application.MissingEvidence!);
    }

    private static ExternalTechnicalInterviewAIProvider CreateProvider(string modelContent)
    {
        var response = System.Text.Json.JsonSerializer.Serialize(new
        {
            model = "aispeis",
            choices = new[] { new { message = new { content = modelContent } } },
            message = new { content = modelContent },
            prompt_eval_count = 10,
            eval_count = 20
        });
        return CreateProvider(new StaticResponseHandler(response));
    }

    private static ExternalTechnicalInterviewAIProvider CreateProvider(
        StaticResponseHandler handler,
        string ollamaEvaluationModel = "")
        => CreateProviderWithOptions(handler, new TechnicalInterviewOptions
        {
            Provider = "ollama",
            OllamaBaseUrl = "http://ollama.test/v1/",
            OllamaModel = "aispeis",
            OllamaEvaluationModel = ollamaEvaluationModel,
            EvaluationTimeoutMs = 5_000,
            EvaluationMaxRetries = 0
        });

    private static ExternalTechnicalInterviewAIProvider CreateProviderWithOptions(
        StaticResponseHandler handler,
        TechnicalInterviewOptions options)
    {
        var client = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(item => item.CreateClient("TechnicalInterviewAI")).Returns(client);
        var gate = new Mock<ITechnicalAIConcurrencyGate>();
        gate.Setup(item => item.EnterAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NoOpLease());

        return new ExternalTechnicalInterviewAIProvider(
            factory.Object,
            gate.Object,
            options,
            NullLogger<ExternalTechnicalInterviewAIProvider>.Instance);
    }

    private static TechnicalAnswerProcessingContext CreateContext() =>
        TechnicalParallelTestData.CreateContext() with
        {
            CandidateAnswer = "Docker packages applications consistently",
            QuestionContent = "What is Docker?"
        };

    private static TechnicalV2AnswerProcessingContext CreateV2Context() => new()
    {
        SessionId = 501,
        QuestionId = 1001,
        QuestionType = "MAIN",
        QuestionContent = "What is Docker?",
        ExpectedAnswer = "Docker packages applications consistently",
        KeyPoints = "containers",
        QuestionSpecificRubric = "{}",
        GlobalRubricVersion = "technical-v2-runtime",
        Rubric = new TechnicalRubricPromptSnapshot(
            0m,
            10m,
            0m,
            ImmutableArray<TechnicalRubricPromptDimension>.Empty,
            ImmutableArray<TechnicalRubricPromptLevel>.Empty),
        CandidateAnswer = "Docker packages applications consistently",
        JobRole = "Backend Developer",
        ExperienceLevel = "Senior",
        Language = "en",
        CvContext = string.Empty,
        JdContext = string.Empty,
        QuestionOrder = 1,
        TargetQuestionCount = 3,
        ScoringPolicyVersion = "technical-v2-scoring"
    };

    private sealed class StaticResponseHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;
        public string RequestUri { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString() ?? string.Empty;
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class NoOpLease : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
