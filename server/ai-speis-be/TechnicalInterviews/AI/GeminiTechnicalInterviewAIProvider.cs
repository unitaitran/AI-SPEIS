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

        public Task<AIProviderResult<TechnicalAIEvaluationResponse>> EvaluateAnswerAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken) =>
            _transport.EvaluateAnswerAsync(context, cancellationToken);

        public Task<AIProviderResult<TechnicalAIFinalSummaryResponse>> GenerateFinalSummaryAsync(
            TechnicalAIFinalSummaryRequest request,
            CancellationToken cancellationToken) =>
            _transport.GenerateFinalSummaryAsync(request, cancellationToken);
    }
}
