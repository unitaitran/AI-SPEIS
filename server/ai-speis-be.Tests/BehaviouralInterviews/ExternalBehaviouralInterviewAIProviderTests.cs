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
    public async Task EvaluateAnswerAsync_RecoversLeadingProse()
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
        Assert.Equal(0m, Assert.Single(result.Data!.DimensionEvaluations).SuggestedScore);
        Assert.Contains("qwen-eval", handler.RequestBody);
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
