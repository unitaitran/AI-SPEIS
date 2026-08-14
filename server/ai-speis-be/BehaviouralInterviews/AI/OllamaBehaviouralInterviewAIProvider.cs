namespace ai_speis_be.BehaviouralInterviews.AI
{
    public sealed class OllamaBehaviouralInterviewAIProvider : IBehaviouralInterviewAIProvider
    {
        private readonly ExternalBehaviouralInterviewAIProvider _transport;

        public OllamaBehaviouralInterviewAIProvider(ExternalBehaviouralInterviewAIProvider transport)
        {
            _transport = transport;
        }

        public string ProviderName => "ollama";

        public Task<BehaviouralAIProviderResult<BehaviouralAISelectionResponse>> SelectQuestionsAsync(
            BehaviouralAISelectionRequest request,
            CancellationToken cancellationToken = default) =>
            _transport.SelectQuestionsAsync(request, cancellationToken, ProviderName);

        public Task<BehaviouralAIProviderResult<BehaviouralAIEvaluationResponse>> EvaluateAnswerAsync(
            BehaviouralAIEvaluationRequest request,
            CancellationToken cancellationToken = default) =>
            _transport.EvaluateAnswerAsync(request, cancellationToken, ProviderName);

        public Task<BehaviouralAIProviderResult<BehaviouralAIFinalSummaryResponse>> GenerateFinalSummaryAsync(
            BehaviouralAIFinalSummaryRequest request,
            CancellationToken cancellationToken = default) =>
            _transport.GenerateFinalSummaryAsync(request, cancellationToken, ProviderName);
    }
}
