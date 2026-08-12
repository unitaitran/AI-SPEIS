namespace ai_speis_be.TechnicalInterviews.AI
{
    public interface ITechnicalInterviewAIProvider
    {
        string ProviderName { get; }

        Task<AIProviderResult<TechnicalAISelectionResponse>> SelectQuestionsAsync(
            TechnicalAISelectionRequest request,
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

        // Session-scoped calls can select the provider persisted for the V2 session.
        ITechnicalInterviewAIProvider ResolveFor(string? providerName) => Resolve();
    }
}
