using System.Net;
using System.Text;
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

    private static ExternalTechnicalInterviewAIProvider CreateProvider(string modelContent)
    {
        var response = System.Text.Json.JsonSerializer.Serialize(new
        {
            model = "aispeis",
            choices = new[] { new { message = new { content = modelContent } } }
        });
        return CreateProvider(new StaticResponseHandler(response));
    }

    private static ExternalTechnicalInterviewAIProvider CreateProvider(
        StaticResponseHandler handler,
        string ollamaEvaluationModel = "")
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
            new TechnicalInterviewOptions
            {
                Provider = "ollama",
                OllamaBaseUrl = "http://ollama.test/v1/",
                OllamaModel = "aispeis",
                OllamaEvaluationModel = ollamaEvaluationModel,
                EvaluationTimeoutMs = 5_000,
                EvaluationMaxRetries = 0
            },
            NullLogger<ExternalTechnicalInterviewAIProvider>.Instance);
    }

    private static TechnicalAnswerProcessingContext CreateContext() =>
        TechnicalParallelTestData.CreateContext() with
        {
            CandidateAnswer = "Docker packages applications consistently",
            QuestionContent = "What is Docker?"
        };

    private sealed class StaticResponseHandler(string body) : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
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
