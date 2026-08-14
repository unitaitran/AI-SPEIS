namespace ai_speis_be.BehaviouralInterviews.AI
{
    public interface IBehaviouralInterviewAIProviderResolver
    {
        IBehaviouralInterviewAIProvider Resolve(string providerName);
    }

    public sealed class BehaviouralInterviewAIProviderResolver : IBehaviouralInterviewAIProviderResolver
    {
        private readonly IReadOnlyDictionary<string, IBehaviouralInterviewAIProvider> _providers;

        public BehaviouralInterviewAIProviderResolver(IEnumerable<IBehaviouralInterviewAIProvider> providers)
        {
            _providers = providers
                .GroupBy(provider => provider.ProviderName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }

        public IBehaviouralInterviewAIProvider Resolve(string providerName)
        {
            var canonicalProvider = Normalize(providerName);
            if (_providers.TryGetValue(canonicalProvider, out var provider))
            {
                return provider;
            }

            throw new InvalidOperationException(
                $"Unsupported Behavioural Interview AI provider '{providerName}'.");
        }

        public static string Normalize(string? providerName)
        {
            var normalized = providerName?.Trim().ToLowerInvariant();
            return normalized switch
            {
                "gemini" or "external" => "gemini",
                "ollama" or "local" or "aispeis" => "ollama",
                _ => throw new InvalidOperationException($"Unsupported Behavioural Interview AI provider '{providerName}'.")
            };
        }
    }
}
