namespace ai_speis_be.TechnicalInterviews.AI
{
    public interface ITechnicalInterviewAIProvider
    {
        string ProviderName { get; }

        Task<AIProviderResult<TechnicalAISelectionResponse>> SelectQuestionsAsync(
            TechnicalAISelectionRequest request,
            CancellationToken cancellationToken);

        Task<AIProviderResult<TechnicalAIEvaluationResponse>> EvaluateAnswerAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken);

        Task<AIProviderResult<TechnicalV2EvaluationResponse>> EvaluateAnswerV2Async(
            TechnicalV2AnswerProcessingContext context,
            CancellationToken cancellationToken);

        Task<AIProviderResult<TechnicalAIFinalSummaryResponse>> GenerateFinalSummaryAsync(
            TechnicalAIFinalSummaryRequest request,
            CancellationToken cancellationToken);
    }

    public interface ITechnicalInterviewAIProviderResolver
    {
        ITechnicalInterviewAIProvider Resolve();

        // Session-scoped Technical V2 calls use this overload. The default keeps
        // existing legacy test doubles and callers source-compatible.
        ITechnicalInterviewAIProvider ResolveFor(string? providerName) => Resolve();
    }
}
