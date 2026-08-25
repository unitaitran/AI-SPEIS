using ai_speis_be.Models;

namespace ai_speis_be.Services.RagService
{
    public interface IQuestionVectorSyncClient
    {
        Task<bool> SyncQuestionAsync(Question question, CancellationToken cancellationToken = default);
        Task<bool> DeleteQuestionAsync(int questionId, CancellationToken cancellationToken = default);
        Task<bool> SyncBatchAsync(IReadOnlyList<Question> questions, CancellationToken cancellationToken = default);
    }
}
