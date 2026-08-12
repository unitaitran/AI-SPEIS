using System.Collections.Immutable;
using System.Net.Http;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
using Microsoft.Extensions.Logging;

var options = new TechnicalInterviewOptions
{
    Provider = "ollama",
    OllamaBaseUrl = "http://localhost:11434/v1/",
    OllamaModel = "aispeis",
    OllamaEvaluationModel = "aispeis",
    EvaluationTimeoutMs = 180_000,
    EvaluationMaxRetries = 0
};

using var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole(options => options.SingleLine = true));
var provider = new ExternalTechnicalInterviewAIProvider(
    new ProbeHttpClientFactory(),
    new ProbeConcurrencyGate(),
    options,
    loggerFactory.CreateLogger<ExternalTechnicalInterviewAIProvider>());

var context = new TechnicalV2AnswerProcessingContext
{
    SessionId = 1,
    QuestionId = 4856,
    QuestionType = "MAIN",
    QuestionContent = "Describe the frontend process that calls a REST API to display a product list.",
    ExpectedAnswer = string.Empty,
    KeyPoints = string.Empty,
    QuestionSpecificRubric = string.Empty,
    GlobalRubricVersion = "technical-v2-runtime",
    Rubric = new TechnicalRubricPromptSnapshot(
        0m,
        10m,
        0m,
        ImmutableArray.Create(
            new TechnicalRubricPromptDimension("ACCURACY", "Accuracy", "", 0.30m),
            new TechnicalRubricPromptDimension("TECHNICAL_DEPTH", "Technical Depth", "", 0.25m),
            new TechnicalRubricPromptDimension("REASONING", "Reasoning", "", 0.20m),
            new TechnicalRubricPromptDimension("APPLICATION", "Application", "", 0.15m),
            new TechnicalRubricPromptDimension("COMMUNICATION", "Communication", "", 0.10m)),
        ImmutableArray<TechnicalRubricPromptLevel>.Empty),
    CandidateAnswer = "I would call the REST API from a dedicated data-access hook. The component starts loading, sends an asynchronous GET request, checks the HTTP status, parses JSON, maps typed product data, and renders the list. I handle non-2xx errors with a retry action, cancel stale requests with AbortController, and separate loading, empty, error, and success states.",
    JobRole = "Frontend Engineer",
    ExperienceLevel = "Mid",
    Language = "en",
    CvContext = string.Empty,
    JdContext = string.Empty,
    QuestionOrder = 1,
    TargetQuestionCount = 1,
    ScoringPolicyVersion = "technical-v2-weighted-v1"
};

var result = await provider.EvaluateAnswerV2Async(context, CancellationToken.None);
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
{
    result.Success,
    result.ErrorCode,
    result.Model,
    RecoveryStatus = result.JsonRecovery?.RecoveryStatus,
    RecoveryFlags = result.JsonRecovery?.RecoveryFlags,
    Dimensions = result.Data?.Evaluation?.DimensionEvaluations?.Select(item => new { item.RubricCode, item.SuggestedScore, Evidence = item.Evidence?.Count, MissingEvidence = item.MissingEvidence?.Count })
}));

sealed class ProbeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client = new();
    public HttpClient CreateClient(string name) => _client;
}

sealed class ProbeConcurrencyGate : ITechnicalAIConcurrencyGate
{
    public ValueTask<IAsyncDisposable> EnterAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IAsyncDisposable>(new ProbeLease());

    private sealed class ProbeLease : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
