namespace ai_speis_be.TechnicalInterviews.AI
{
    /// <summary>
    /// Session-scoped Ollama provider boundary. The transport remains shared,
    /// but the explicit provider override prevents global configuration from
    /// changing the selected session's endpoint or persisted metadata.
    /// </summary>
    public sealed class OllamaTechnicalInterviewAIProvider : ITechnicalInterviewAIProvider
    {
        private readonly ExternalTechnicalInterviewAIProvider _transport;

        public OllamaTechnicalInterviewAIProvider(ExternalTechnicalInterviewAIProvider transport)
        {
            _transport = transport;
        }

        public string ProviderName => "ollama";

        public Task<AIProviderResult<TechnicalAISelectionResponse>> SelectQuestionsAsync(
            TechnicalAISelectionRequest request,
            CancellationToken cancellationToken) =>
            _transport.SelectQuestionsAsync(request, cancellationToken, ProviderName);

        public Task<AIProviderResult<TechnicalV2EvaluationResponse>> EvaluateAnswerV2Async(
            TechnicalV2AnswerProcessingContext context,
            CancellationToken cancellationToken) =>
            _transport.EvaluateAnswerV2Async(context, cancellationToken, ProviderName);

        public Task<AIProviderResult<TechnicalAIFinalSummaryResponse>> GenerateFinalSummaryAsync(
            TechnicalAIFinalSummaryRequest request,
            CancellationToken cancellationToken) =>
            _transport.GenerateFinalSummaryAsync(request, cancellationToken, ProviderName);
    }
}
