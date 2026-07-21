using ai_speis_be.TechnicalInterviews.Configuration;

namespace ai_speis_be.TechnicalInterviews.AI
{
    public sealed class TechnicalInterviewAIProviderResolver : ITechnicalInterviewAIProviderResolver
    {
        private readonly TechnicalInterviewOptions _options;
        private readonly GeminiTechnicalInterviewAIProvider _geminiProvider;

        public TechnicalInterviewAIProviderResolver(
            TechnicalInterviewOptions options,
            GeminiTechnicalInterviewAIProvider geminiProvider)
        {
            _options = options;
            _geminiProvider = geminiProvider;
        }

        public ITechnicalInterviewAIProvider Resolve()
        {
            return _options.Provider.ToLowerInvariant() switch
            {
                "external" or "gemini" => _geminiProvider,
                _ => throw new InvalidOperationException(
                    $"Unsupported Technical Interview AI provider '{_options.Provider}'.")
            };
        }
    }
}
