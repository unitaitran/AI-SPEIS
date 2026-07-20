namespace ai_speis_be.TechnicalInterviews.AI
{
    public interface ITechnicalInterviewAIProvider
    {
        string ProviderName { get; }

        Task<AIProviderResult<TechnicalAISelectionResponse>> SelectQuestionAsync(
            TechnicalAISelectionRequest request,
            CancellationToken cancellationToken);

        Task<AIProviderResult<TechnicalAIEvaluationResponse>> EvaluateAnswerAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken);

        Task<AIProviderResult<TechnicalAIFeedbackDraftResponse>> GenerateFeedbackDraftAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken);

        Task<AIProviderResult<TechnicalAIQuestionBundleResponse>> GenerateQuestionBundleAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken);

        Task<AIProviderResult<TechnicalAIFinalSummaryResponse>> GenerateFinalSummaryAsync(
            TechnicalAIFinalSummaryRequest request,
            CancellationToken cancellationToken);
    }

    public interface ITechnicalInterviewAIProviderResolver
    {
        ITechnicalInterviewAIProvider Resolve();
    }
}
