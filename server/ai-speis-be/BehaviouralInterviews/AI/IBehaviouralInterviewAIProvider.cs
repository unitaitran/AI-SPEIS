namespace ai_speis_be.BehaviouralInterviews.AI
{
    public interface IBehaviouralInterviewAIProvider
    {
        string ProviderName { get; }
        
        Task<BehaviouralAIProviderResult<BehaviouralAISelectionResponse>> SelectQuestionsAsync(
            BehaviouralAISelectionRequest request,
            CancellationToken cancellationToken = default);

        Task<BehaviouralAIProviderResult<BehaviouralAIEvaluationResponse>> EvaluateAnswerAsync(
            BehaviouralAIEvaluationRequest request, 
            CancellationToken cancellationToken = default);

        Task<BehaviouralAIProviderResult<BehaviouralAIFinalSummaryResponse>> GenerateFinalSummaryAsync(
            BehaviouralAIFinalSummaryRequest request, 
            CancellationToken cancellationToken = default);
    }
}
