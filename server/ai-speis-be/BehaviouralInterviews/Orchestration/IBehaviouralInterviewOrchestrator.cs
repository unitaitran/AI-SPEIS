using ai_speis_be.BehaviouralInterviews.DTOs;

namespace ai_speis_be.BehaviouralInterviews.Orchestration
{
    public interface IBehaviouralInterviewOrchestrator
    {
        Task<BehaviouralOperationResult<BehaviouralInterviewSessionDto>> InitializeAsync(
            int userId,
            InitializeBehaviouralInterviewRequest request,
            CancellationToken cancellationToken = default);

        Task<BehaviouralOperationResult<BehaviouralCurrentQuestionDto>> StartAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken = default);

        Task<BehaviouralOperationResult<BehaviouralInterviewSessionDto>> GetSessionAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken = default);

        Task<BehaviouralOperationResult<BehaviouralCurrentQuestionDto>> GetCurrentQuestionAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken = default);

        Task<BehaviouralOperationResult<BehaviouralSubmitAnswerResponseDto>> SubmitAnswerAsync(
            int userId,
            int sessionId,
            SubmitBehaviouralAnswerRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken = default);

        Task<BehaviouralOperationResult<BehaviouralInterviewResultDto>> CompleteAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken = default);

        Task<BehaviouralOperationResult<BehaviouralInterviewResultDto>> GetResultAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken = default);

        Task<BehaviouralOperationResult<BehaviouralInterviewResultDto>> GenerateFeedbackAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken = default);
    }
}
