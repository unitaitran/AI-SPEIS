using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalInterviewAIProviderResolverTests
{
    [Theory]
    [InlineData("gemini")]
    [InlineData("external")]
    public void Resolve_GeminiProvider_ReturnsGemini(string providerName)
    {
        var (resolver, gemini, _) = CreateResolver(providerName);

        Assert.Same(gemini, resolver.Resolve());
        Assert.Equal("gemini", resolver.Resolve().ProviderName);
    }

    [Theory]
    [InlineData("ollama")]
    [InlineData("local")]
    public void Resolve_OllamaProvider_ReturnsOllama(string providerName)
    {
        var (resolver, _, ollama) = CreateResolver("gemini");

        Assert.Same(ollama, resolver.ResolveFor(providerName));
        Assert.Equal("ollama", resolver.ResolveFor(providerName).ProviderName);
    }

    [Fact]
    public void Resolve_InvalidProvider_ThrowsControlledError()
    {
        var (resolver, _, _) = CreateResolver("gemini");

        var error = Assert.Throws<InvalidOperationException>(() => resolver.ResolveFor("unknown"));

        Assert.Contains("Unsupported Technical Interview AI provider", error.Message);
    }

    [Fact]
    public void Resolve_ExplicitSessionProvider_OverridesGlobalDefault()
    {
        var (resolver, gemini, ollama) = CreateResolver("gemini");

        Assert.Same(ollama, resolver.ResolveFor("ollama"));
        Assert.NotSame(gemini, resolver.ResolveFor("ollama"));
    }

    [Fact]
    public void ResolveFor_WithoutSessionProvider_UsesGlobalDefault()
    {
        var (resolver, gemini, _) = CreateResolver("gemini");

        Assert.Same(gemini, resolver.ResolveFor(null));
    }

    private static (TechnicalInterviewAIProviderResolver Resolver, FakeProvider Gemini, FakeProvider Ollama) CreateResolver(string globalProvider)
    {
        var gemini = new FakeProvider("gemini");
        var ollama = new FakeProvider("ollama");
        var resolver = new TechnicalInterviewAIProviderResolver(
            new TechnicalInterviewOptions { Provider = globalProvider },
            new ITechnicalInterviewAIProvider[] { gemini, ollama });
        return (resolver, gemini, ollama);
    }

    private sealed class FakeProvider : ITechnicalInterviewAIProvider
    {
        public FakeProvider(string providerName) => ProviderName = providerName;
        public string ProviderName { get; }

        public Task<AIProviderResult<TechnicalAISelectionResponse>> SelectQuestionsAsync(
            TechnicalAISelectionRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AIProviderResult<TechnicalV2EvaluationResponse>> EvaluateAnswerV2Async(
            TechnicalV2AnswerProcessingContext context,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AIProviderResult<TechnicalAIFinalSummaryResponse>> GenerateFinalSummaryAsync(
            TechnicalAIFinalSummaryRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
