using ai_speis_be.Models.Enums;
using ai_speis_be.TechnicalInterviews.Orchestration;
using ai_speis_be.TechnicalInterviews.Rubrics;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalFollowUpDecisionEngineTests
{
    private static readonly TechnicalQuestionLimits Limits = new()
    {
        MaxClarificationsPerMainQuestion = 1,
        MaxFollowUpsPerMainQuestion = 2,
        MaxTotalSubQuestionsPerMainQuestion = 2
    };

    [Fact]
    public void Resolve_AllowsClarificationWithinLimitWithoutCompletingMainQuestion()
    {
        var engine = new TechnicalFollowUpDecisionEngine();

        var result = engine.Resolve(
            TechnicalInterviewDecision.Clarification,
            0,
            0,
            0,
            5,
            true,
            Limits);

        Assert.False(result.FinalizeMainQuestion);
        Assert.Equal(TechnicalAttemptType.Clarification, result.NextAttemptType);
    }

    [Fact]
    public void Resolve_ForcesNextQuestionWhenSubQuestionLimitReached()
    {
        var engine = new TechnicalFollowUpDecisionEngine();

        var result = engine.Resolve(
            TechnicalInterviewDecision.FollowUp,
            1,
            1,
            2,
            5,
            true,
            Limits);

        Assert.True(result.FinalizeMainQuestion);
        Assert.Equal(TechnicalInterviewDecision.NextQuestion, result.Decision);
    }

    [Fact]
    public void Resolve_BackendEndsOnlyAfterTargetMainQuestionCount()
    {
        var engine = new TechnicalFollowUpDecisionEngine();

        var result = engine.Resolve(
            TechnicalInterviewDecision.EndInterview,
            0,
            0,
            4,
            5,
            false,
            Limits);

        Assert.Equal(TechnicalInterviewDecision.EndInterview, result.Decision);
        Assert.True(result.FinalizeMainQuestion);
    }
}
