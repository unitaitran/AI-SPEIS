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
    public void Resolve_TriggersClarificationWhenScoreBelowThreshold()
    {
        var engine = new TechnicalFollowUpDecisionEngine();

        var result = engine.Resolve(
            2.5m,
            0,
            0,
            true,
            true,
            true,
            Limits);

        Assert.False(result.FinalizeMainQuestion);
        Assert.Equal(TechnicalSessionQuestionType.Clarification, result.NextQuestionType);
    }

    [Fact]
    public void Resolve_TriggersFollowUpWhenScoreIsMedium()
    {
        var engine = new TechnicalFollowUpDecisionEngine();

        var result = engine.Resolve(
            4.5m,
            0,
            0,
            true,
            true,
            true,
            Limits);

        Assert.False(result.FinalizeMainQuestion);
        Assert.Equal(TechnicalSessionQuestionType.FollowUp, result.NextQuestionType);
    }

    [Fact]
    public void Resolve_AdvancesToNextQuestionWhenScoreIsHigh()
    {
        var engine = new TechnicalFollowUpDecisionEngine();

        var result = engine.Resolve(
            8.5m,
            0,
            0,
            true,
            true,
            true,
            Limits);

        Assert.True(result.FinalizeMainQuestion);
        Assert.Equal(TechnicalInterviewDecision.NextQuestion, result.Decision);
    }
}
