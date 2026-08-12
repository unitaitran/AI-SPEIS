namespace ai_speis_be.TechnicalInterviews.V2
{
    public interface ITechnicalV2InterviewOrchestrator
    {
        Task<TechnicalV2OperationResult<TechnicalV2SessionDto>> InitializeAsync(int userId, int sessionId, InitializeTechnicalV2Request request, CancellationToken cancellationToken);
        Task<TechnicalV2OperationResult<TechnicalV2CurrentQuestionDto>> StartAsync(int userId, int sessionId, CancellationToken cancellationToken);
        Task<TechnicalV2OperationResult<TechnicalV2SessionDto>> GetStateAsync(int userId, int sessionId, CancellationToken cancellationToken);
        Task<TechnicalV2OperationResult<TechnicalV2CurrentQuestionDto>> GetCurrentQuestionAsync(int userId, int sessionId, CancellationToken cancellationToken);
        Task<TechnicalV2OperationResult<TechnicalV2SubmitAnswerResponseDto>> SubmitAnswerAsync(int userId, int sessionId, int sessionQuestionId, SubmitTechnicalV2AnswerRequest request, string idempotencyKey, CancellationToken cancellationToken);
        Task<TechnicalV2OperationResult<TechnicalV2ResultDto>> CompleteAsync(int userId, int sessionId, CancellationToken cancellationToken);
        Task<TechnicalV2OperationResult<TechnicalV2ResultDto>> GetResultAsync(int userId, int sessionId, CancellationToken cancellationToken);
        Task<TechnicalV2OperationResult<TechnicalV2ResultDto>> GenerateFeedbackAsync(int userId, int sessionId, CancellationToken cancellationToken);
    }
}
