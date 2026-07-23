using ai_speis_be.TechnicalInterviews.DTOs;

namespace ai_speis_be.TechnicalInterviews.Orchestration
{
    public interface ITechnicalInterviewOrchestrator
    {
        Task<TechnicalOperationResult<TechnicalInterviewSessionDto>> InitializeAsync(
            int userId,
            InitializeTechnicalInterviewRequest request,
            CancellationToken cancellationToken);

        Task<TechnicalOperationResult<TechnicalCurrentQuestionDto>> StartAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken);

        Task<TechnicalOperationResult<TechnicalInterviewSessionDto>> GetSessionAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken);

        Task<TechnicalOperationResult<TechnicalCurrentQuestionDto>> GetCurrentQuestionAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken);

        Task<TechnicalOperationResult<TechnicalSubmitAnswerResponseDto>> SubmitAnswerAsync(
            int userId,
            int sessionId,
            SubmitTechnicalAnswerRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken);

        Task<TechnicalOperationResult<TechnicalInterviewResultDto>> CompleteAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken);

        Task<TechnicalOperationResult<TechnicalInterviewResultDto>> GetResultAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken);

        Task<TechnicalOperationResult<TechnicalInterviewResultDto>> GenerateFeedbackAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken);
    }
}
