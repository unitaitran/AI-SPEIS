using ai_speis_be.Models;
using ai_speis_be.Repositories.QuestionRepo;
using ai_speis_be.Services.RagService;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace ai_speis_be.BackgroundJobs
{
    [Queue("default")]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 }, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public class QuestionSyncJob
    {
        private readonly IQuestionRepoitory _repository;
        private readonly IQuestionVectorSyncClient _syncClient;
        private readonly ILogger<QuestionSyncJob> _logger;

        public QuestionSyncJob(
            IQuestionRepoitory repository,
            IQuestionVectorSyncClient syncClient,
            ILogger<QuestionSyncJob> logger)
        {
            _repository = repository;
            _syncClient = syncClient;
            _logger = logger;
        }

        public async Task SyncQuestionAsync(int questionId, CancellationToken cancellationToken = default)
        {
            if (questionId <= 0)
            {
                _logger.LogWarning("Invalid questionId {QuestionId} received in QuestionSyncJob.", questionId);
                return;
            }

            _logger.LogInformation("Reconciling Question #{QuestionId} in Qdrant with current SQL state...", questionId);

            var question = await _repository.GetQuestionByIdAdminAsync(questionId, cancellationToken);

            if (question is null || question.IsDeleted)
            {
                _logger.LogInformation("Question #{QuestionId} is either deleted or missing in SQL. Purging from Qdrant.", questionId);
                await _syncClient.DeleteQuestionAsync(questionId, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Question #{QuestionId} is active in SQL. Upserting to Qdrant.", questionId);
                await _syncClient.SyncQuestionAsync(question, cancellationToken);
            }
        }

        public async Task SyncBatchAsync(List<int> questionIds, CancellationToken cancellationToken = default)
        {
            if (questionIds == null || questionIds.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Starting batch vector synchronization for {Count} questions...", questionIds.Count);

            var activeQuestions = new List<Question>();
            foreach (var id in questionIds.Distinct())
            {
                var question = await _repository.GetQuestionByIdAdminAsync(id, cancellationToken);
                if (question != null && !question.IsDeleted)
                {
                    activeQuestions.Add(question);
                }
            }

            if (activeQuestions.Count > 0)
            {
                await _syncClient.SyncBatchAsync(activeQuestions, cancellationToken);
            }

            _logger.LogInformation("Completed batch vector synchronization for {Count} active questions.", activeQuestions.Count);
        }
    }
}
