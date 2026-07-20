using ai_speis_be.TechnicalInterviews.Configuration;

namespace ai_speis_be.TechnicalInterviews.AI
{
    public sealed class TechnicalInterviewAIProviderResolver : ITechnicalInterviewAIProviderResolver
    {
        private readonly TechnicalInterviewOptions _options;
        private readonly ExternalTechnicalInterviewAIProvider _externalProvider;

        public TechnicalInterviewAIProviderResolver(
            TechnicalInterviewOptions options,
            ExternalTechnicalInterviewAIProvider externalProvider)
        {
            _options = options;
            _externalProvider = externalProvider;
        }

        public ITechnicalInterviewAIProvider Resolve()
        {
            return _options.Provider.ToLowerInvariant() switch
            {
                "external" => _externalProvider,
                _ => throw new InvalidOperationException(
                    $"Unsupported Technical Interview AI provider '{_options.Provider}'.")
            };
        }
    }
}
