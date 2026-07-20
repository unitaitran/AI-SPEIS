namespace ai_speis_be.TechnicalInterviews.AI
{
    /// <summary>
    /// Gemini-specific provider boundary. The current transport uses Gemini's
    /// OpenAI-compatible backend endpoint while keeping controllers/orchestrators
    /// independent from the concrete vendor implementation.
    /// </summary>
    public sealed class GeminiTechnicalInterviewAIProvider : ITechnicalInterviewAIProvider
    {
        private readonly ExternalTechnicalInterviewAIProvider _transport;

        public GeminiTechnicalInterviewAIProvider(ExternalTechnicalInterviewAIProvider transport)
        {
            _transport = transport;
        }

        public string ProviderName => "gemini";

        public Task<AIProviderResult<TechnicalAISelectionResponse>> SelectQuestionAsync(
            TechnicalAISelectionRequest request,
            CancellationToken cancellationToken) =>
            _transport.SelectQuestionAsync(request, cancellationToken);

        public Task<AIProviderResult<TechnicalAIEvaluationResponse>> EvaluateAnswerAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken) =>
            _transport.EvaluateAnswerAsync(context, cancellationToken);

        public Task<AIProviderResult<TechnicalAIFeedbackDraftResponse>> GenerateFeedbackDraftAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken) =>
            _transport.GenerateFeedbackDraftAsync(context, cancellationToken);

        public Task<AIProviderResult<TechnicalAIQuestionBundleResponse>> GenerateQuestionBundleAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken) =>
            _transport.GenerateQuestionBundleAsync(context, cancellationToken);

        public Task<AIProviderResult<TechnicalAIFinalSummaryResponse>> GenerateFinalSummaryAsync(
            TechnicalAIFinalSummaryRequest request,
            CancellationToken cancellationToken) =>
            _transport.GenerateFinalSummaryAsync(request, cancellationToken);
    }
}
