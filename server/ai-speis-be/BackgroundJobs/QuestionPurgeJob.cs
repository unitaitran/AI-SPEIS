using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ai_speis_be.BackgroundJobs;

[Queue("default")]
[DisableConcurrentExecution(timeoutInSeconds: 1800)]
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 300, 900, 3600 }, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
public sealed class QuestionPurgeJob
{
    private const int BatchSize = 50;

    private readonly ApplicationDbContext _context;
    private readonly ILogger<QuestionPurgeJob> _logger;

    public QuestionPurgeJob(ApplicationDbContext context, ILogger<QuestionPurgeJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(-30);
        var candidates = await _context.Questions
            .Where(question => question.IsDeleted
                && question.PurgeStatus == QuestionPurgeStatus.Requested
                && question.DeletedAt != null
                && question.DeletedAt <= cutoff)
            .OrderBy(question => question.DeletedAt)
            .ThenBy(question => question.QuestionId)
            .Take(BatchSize)
            .ToListAsync();

        foreach (var question in candidates)
        {
            try
            {
                await PurgeOneAsync(question, now);
            }
            catch (Exception exception)
            {
                _context.ChangeTracker.Clear();
                var failedQuestion = await _context.Questions
                    .FirstOrDefaultAsync(item => item.QuestionId == question.QuestionId);
                if (failedQuestion is not null)
                {
                    failedQuestion.PurgeAttemptCount++;
                    failedQuestion.LastPurgeError = TrimError(exception.Message);
                    await _context.SaveChangesAsync();
                }
                _logger.LogError(exception, "Question purge failed for QuestionId {QuestionId}.", question.QuestionId);
            }
        }
    }

    private async Task PurgeOneAsync(Question question, DateTime now)
    {
        var questionId = question.QuestionId;
        await _context.Entry(question).ReloadAsync();
        if (!question.IsDeleted || question.PurgeStatus != QuestionPurgeStatus.Requested)
        {
            return;
        }

        if (await HasActiveDependencyAsync(questionId))
        {
            question.PurgeAttemptCount++;
            question.LastPurgeError = "Question is still referenced by an active interview or an in-progress retry.";
            await _context.SaveChangesAsync();
            _logger.LogInformation("Question purge deferred for QuestionId {QuestionId}: active dependency.", questionId);
            return;
        }

        if (!await HasCompleteHistorySnapshotsAsync(questionId))
        {
            question.PurgeAttemptCount++;
            question.LastPurgeError = "A historical interview record has no durable question snapshot.";
            await _context.SaveChangesAsync();
            _logger.LogWarning("Question purge deferred for QuestionId {QuestionId}: invalid historical snapshot.", questionId);
            return;
        }

        // Keep the explicit transaction only around the destructive operations.
        // Serializable isolation held range locks while checking dependencies and
        // could block normal Question Bank reads for the duration of the job.
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.Entry(question).ReloadAsync();
        if (!question.IsDeleted || question.PurgeStatus != QuestionPurgeStatus.Requested)
        {
            await transaction.CommitAsync();
            return;
        }

        var savedQuestions = await _context.SavedQuestion
            .Where(saved => saved.QuestionId == questionId)
            .ToListAsync();
        _context.SavedQuestion.RemoveRange(savedQuestions);
        _context.QuestionPurgeAudits.Add(new QuestionPurgeAudit
        {
            QuestionId = questionId,
            RequestedBy = question.PurgeRequestedBy,
            SoftDeletedAt = question.DeletedAt,
            RequestedAt = question.PurgeRequestedAt ?? now,
            PurgedAt = now,
            Outcome = "Purged",
            Detail = savedQuestions.Count == 0
                ? "Question permanently deleted after retention period."
                : $"Question permanently deleted; removed {savedQuestions.Count} saved-question reference(s)."
        });
        _context.Questions.Remove(question);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        _logger.LogInformation("Question {QuestionId} permanently purged.", questionId);
    }

    private async Task<bool> HasActiveDependencyAsync(int questionId)
    {
        var activeTechnicalSession = await _context.TechnicalSessionQuestions
            .IgnoreQueryFilters()
            .AnyAsync(question => question.QuestionId == questionId
                && (question.TechnicalQuestionSet.InterviewSession.Status == InterviewSessionStatus.Pending
                    || question.TechnicalQuestionSet.InterviewSession.Status == InterviewSessionStatus.Active));
        if (activeTechnicalSession) return true;

        var activeBehaviourSession = await _context.BehaviourSessionQuestions
            .AnyAsync(question => question.QuestionId == questionId
                && (question.BehaviourQuestionSet.InterviewSession.Status == InterviewSessionStatus.Pending
                    || question.BehaviourQuestionSet.InterviewSession.Status == InterviewSessionStatus.Active));
        if (activeBehaviourSession) return true;

        return await _context.SingleQuestionRetries
            .AnyAsync(retry => retry.QuestionId == questionId && retry.EvaluationStatus == "PROCESSING");
    }

    private async Task<bool> HasCompleteHistorySnapshotsAsync(int questionId)
    {
        var technicalSnapshots = await _context.TechnicalSessionQuestions
            .IgnoreQueryFilters()
            .Where(question => question.QuestionId == questionId)
            .Select(question => question.QuestionSnapshotJson)
            .ToListAsync();
        if (technicalSnapshots.Any(snapshot => !IsValidSnapshot(snapshot))) return false;

        var behaviourSnapshots = await _context.BehaviourSessionQuestions
            .Where(question => question.QuestionId == questionId)
            .Select(question => question.QuestionSnapshotJson)
            .ToListAsync();
        if (behaviourSnapshots.Any(snapshot => !IsValidSnapshot(snapshot))) return false;

        var retrySnapshots = await _context.SingleQuestionRetries
            .Where(retry => retry.QuestionId == questionId)
            .Select(retry => retry.QuestionSnapshot)
            .ToListAsync();
        return retrySnapshots.All(snapshot => !string.IsNullOrWhiteSpace(snapshot));
    }

    private static bool IsValidSnapshot(string? snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot)) return false;
        try
        {
            using var document = JsonDocument.Parse(snapshot);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string TrimError(string value) => value.Length <= 1000 ? value : value[..1000];
}
