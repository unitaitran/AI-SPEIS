using ai_speis_be.TechnicalInterviews.Configuration;

namespace ai_speis_be.TechnicalInterviews.AI
{
    public sealed class TechnicalInterviewAIProviderResolver : ITechnicalInterviewAIProviderResolver
    {
        private readonly TechnicalInterviewOptions _options;
        private readonly IReadOnlyDictionary<string, ITechnicalInterviewAIProvider> _providers;

        public TechnicalInterviewAIProviderResolver(
            TechnicalInterviewOptions options,
            IEnumerable<ITechnicalInterviewAIProvider> providers)
        {
            _options = options;
            _providers = providers
                .GroupBy(provider => provider.ProviderName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key.ToLowerInvariant(), group => group.First(), StringComparer.OrdinalIgnoreCase);
        }

        public ITechnicalInterviewAIProvider Resolve()
        {
            return ResolveFor(_options.Provider);
        }

        public ITechnicalInterviewAIProvider ResolveFor(string? providerName)
        {
            var requestedProvider = string.IsNullOrWhiteSpace(providerName)
                ? _options.Provider
                : providerName;
            var canonicalProvider = Normalize(requestedProvider);
            if (_providers.TryGetValue(canonicalProvider, out var provider))
                return provider;

            throw new InvalidOperationException(
                $"Unsupported Technical Interview AI provider '{requestedProvider}'.");
        }

        public static string Normalize(string? providerName)
        {
            return providerName?.Trim().ToLowerInvariant() switch
            {
                "gemini" or "external" => "gemini",
                "ollama" or "local" => "ollama",
                _ => throw new InvalidOperationException(
                    $"Unsupported Technical Interview AI provider '{providerName}'.")
            };
        }
    }
}
