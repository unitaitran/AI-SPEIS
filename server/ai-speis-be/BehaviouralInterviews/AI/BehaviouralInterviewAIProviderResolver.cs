namespace ai_speis_be.BehaviouralInterviews.AI
{
    public interface IBehaviouralInterviewAIProviderResolver
    {
        IBehaviouralInterviewAIProvider Resolve(string providerName);
    }

    public sealed class BehaviouralInterviewAIProviderResolver : IBehaviouralInterviewAIProviderResolver
    {
        private readonly IEnumerable<IBehaviouralInterviewAIProvider> _providers;

        public BehaviouralInterviewAIProviderResolver(IEnumerable<IBehaviouralInterviewAIProvider> providers)
        {
            _providers = providers;
        }

        public IBehaviouralInterviewAIProvider Resolve(string providerName)
        {
            var provider = _providers.FirstOrDefault(p => p.ProviderName == providerName);
            return provider ?? _providers.First(); // Fallback to first registered
        }
    }
}
