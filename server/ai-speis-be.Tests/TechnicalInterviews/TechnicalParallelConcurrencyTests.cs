using ai_speis_be.Models;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalParallelConcurrencyTests
{
    [Fact]
    public async Task GlobalGate_DoesNotAllowMoreThanConfiguredConcurrentGeminiCalls()
    {
        using var gate = new TechnicalAIConcurrencyGate(new TechnicalInterviewOptions
        {
            GlobalConcurrencyLimit = 1
        });
        await using var firstLease = await gate.EnterAsync(CancellationToken.None);

        var secondLeaseTask = gate.EnterAsync(CancellationToken.None).AsTask();
        await Task.Delay(30);
        Assert.False(secondLeaseTask.IsCompleted);

        await firstLease.DisposeAsync();
        await using var secondLease = await secondLeaseTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.NotNull(secondLease);
    }

    [Fact]
    public void EfModel_PreservesSessionConcurrencyAndDuplicateAttemptGuards()
    {
        using var context = TestDbContextFactory.Create();
        var session = context.Model.FindEntityType(typeof(InterviewSession))!;
        var attempt = context.Model.FindEntityType(typeof(TechnicalQuestionAttempt))!;
        var evaluation = context.Model.FindEntityType(typeof(TechnicalAnswerEvaluation))!;

        Assert.True(session.FindProperty(nameof(InterviewSession.TechnicalConcurrencyVersion))!.IsConcurrencyToken);
        Assert.Contains(attempt.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(TechnicalQuestionAttempt.InterviewSessionId),
                nameof(TechnicalQuestionAttempt.SubmissionIdempotencyKey)
            }));
        Assert.Contains(attempt.GetIndexes(), index =>
            index.IsUnique
            && index.GetFilter() == "[Status] = 0");
        Assert.Contains(evaluation.GetIndexes(), index =>
            index.IsUnique
            && index.GetFilter() == "[IsFinalForMainQuestion] = 1");
    }
}
