using System.Net;
using System.Text;
using ai_speis_be.BehaviouralInterviews.AI;
using ai_speis_be.BehaviouralInterviews.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ai_speis_be.Tests.BehaviouralInterviews;

public sealed class ExternalBehaviouralInterviewAIProviderTests
{
    [Fact]
    public async Task EvaluateAnswerAsync_PreservesMalformedScoreForTheValidator()
    {
        var response = "Here is the evaluation: {\"dimensionEvaluations\":[{\"rubricCode\":\"ACTION\",\"evidence\":[],\"missingEvidence\":[],\"suggestedScore\":\"abc\"}]}";
        var handler = new StaticHandler("{\"model\":\"aispeis\",\"choices\":[{\"message\":{\"content\":" + System.Text.Json.JsonSerializer.Serialize(response) + "}}]}");
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient("BehaviouralInterviewAI")).Returns(new HttpClient(handler));
        var provider = new ExternalBehaviouralInterviewAIProvider(
            factory.Object,
            new BehaviouralInterviewOptions { Provider = "ollama", OllamaModel = "aispeis", OllamaEvaluationModel = "qwen-eval" },
            NullLogger<ExternalBehaviouralInterviewAIProvider>.Instance);

        var result = await provider.EvaluateAnswerAsync(new BehaviouralAIEvaluationRequest(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(Assert.Single(result.Data!.DimensionEvaluations).SuggestedScore);
        Assert.Contains("qwen-eval", handler.RequestBody);
    }

    [Fact]
    public async Task EvaluateAnswerAsync_UnwrapsEvaluationPayloadAndUsesStrictGeminiSchema()
    {
        const string content = """
            {"evaluation":{"dimensionEvaluations":[
              {"rubricCode":"SITUATION_TASK","suggestedScore":7,"evidence":[],"missingEvidence":[]},
              {"rubricCode":"ACTION","suggestedScore":8,"evidence":[],"missingEvidence":[]},
              {"rubricCode":"RESULT","suggestedScore":6,"evidence":[],"missingEvidence":[]},
              {"rubricCode":"COMPETENCY","suggestedScore":7,"evidence":[],"missingEvidence":[]},
              {"rubricCode":"COMMUNICATION","suggestedScore":8,"evidence":[],"missingEvidence":[]}
            ]}}
            """;
        var handler = new StaticHandler("{\"model\":\"gemini-test\",\"choices\":[{\"message\":{\"content\":" + System.Text.Json.JsonSerializer.Serialize(content) + "}}]}");
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient("BehaviouralInterviewAI")).Returns(new HttpClient(handler));
        var provider = new ExternalBehaviouralInterviewAIProvider(
            factory.Object,
            new BehaviouralInterviewOptions { ApiKey = "test-key" },
            NullLogger<ExternalBehaviouralInterviewAIProvider>.Instance);

        var result = await provider.EvaluateAnswerAsync(new BehaviouralAIEvaluationRequest(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(5, result.Data!.DimensionEvaluations.Count);
        Assert.Equal(8m, result.Data.DimensionEvaluations.Single(item => item.RubricCode == "ACTION").SuggestedScore);
        Assert.Contains("json_schema", handler.RequestBody);
        Assert.Contains("SITUATION_TASK", handler.RequestBody);
    }

    [Fact]
    public async Task GeminiProvider_OverridesGlobalOllamaConfiguration()
    {
        const string content = """
            {"dimensionEvaluations":[{"rubricCode":"ACTION","suggestedScore":8,"evidence":[],"missingEvidence":[]}]}
            """;
        var handler = new StaticHandler("{\"model\":\"gemini-test\",\"choices\":[{\"message\":{\"content\":" + System.Text.Json.JsonSerializer.Serialize(content) + "}}]}");
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient("BehaviouralInterviewAI")).Returns(new HttpClient(handler));
        var transport = new ExternalBehaviouralInterviewAIProvider(
            factory.Object,
            new BehaviouralInterviewOptions
            {
                Provider = "ollama",
                ApiKey = "test-key",
                OllamaModel = "local-model",
                Model = "gemini-test"
            },
            NullLogger<ExternalBehaviouralInterviewAIProvider>.Instance);
        var provider = new GeminiBehaviouralInterviewAIProvider(transport);

        var result = await provider.EvaluateAnswerAsync(new BehaviouralAIEvaluationRequest(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("gemini", result.ProviderName);
        Assert.Contains("json_schema", handler.RequestBody);
        Assert.DoesNotContain("local-model", handler.RequestBody);
    }

    [Fact]
    public void Resolver_NormalizesAliasesInsteadOfFallingBackToAnUnrelatedProvider()
    {
        var gemini = new Mock<IBehaviouralInterviewAIProvider>();
        gemini.SetupGet(item => item.ProviderName).Returns("gemini");
        var ollama = new Mock<IBehaviouralInterviewAIProvider>();
        ollama.SetupGet(item => item.ProviderName).Returns("ollama");
        var resolver = new BehaviouralInterviewAIProviderResolver(new[] { gemini.Object, ollama.Object });

        Assert.Same(gemini.Object, resolver.Resolve("external"));
        Assert.Same(ollama.Object, resolver.Resolve("local"));
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve("unknown-provider"));
    }

    private sealed class StaticHandler(string body) : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
