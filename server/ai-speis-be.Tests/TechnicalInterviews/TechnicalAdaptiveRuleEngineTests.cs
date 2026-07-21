using ai_speis_be.Models.Enums;
using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.TechnicalInterviews.Orchestration;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalAdaptiveRuleEngineTests
{
    private readonly TechnicalAdaptiveRuleEngine _engine = new(new TechnicalInterviewOptions());

    [Theory]
    [InlineData(2.99, TechnicalInterviewDecision.Clarification, 1, 0)]
    [InlineData(3, TechnicalInterviewDecision.FollowUp, 0, 2)]
    [InlineData(4.99, TechnicalInterviewDecision.FollowUp, 0, 2)]
    [InlineData(5, TechnicalInterviewDecision.FollowUp, 0, 1)]
    [InlineData(7.99, TechnicalInterviewDecision.FollowUp, 0, 1)]
    [InlineData(8, TechnicalInterviewDecision.NextQuestion, 0, 0)]
    public void Resolve_UsesDeterministicScoreBoundaries(
        double score,
        TechnicalInterviewDecision decision,
        int clarifications,
        int followUps)
    {
        var result = _engine.Resolve(Input((decimal)score));

        Assert.Equal(decision, result.Decision);
        Assert.Equal(clarifications, result.RequiredClarificationCount);
        Assert.Equal(followUps, result.RequiredFollowUpCount);
    }

    [Fact]
    public void Resolve_DoesNotGenerateFu2BeforeFu1IsCompleted()
    {
        var input = Input(4m) with
        {
            CurrentAttemptType = TechnicalAttemptType.FollowUp,
            RequiredFollowUpCount = 2,
            CompletedFollowUpCount = 0
        };

        var result = _engine.Resolve(input);

        Assert.Equal(TechnicalInterviewDecision.FollowUp, result.Decision);
        Assert.False(result.FinalizeMainQuestion);
    }

    [Fact]
    public void Resolve_ReliabilityRuleAddsOnlyOneMarkedFollowUp()
    {
        var result = _engine.Resolve(Input(9m) with
        {
            CompletedMainQuestionCount = 2,
            IsReliabilityFollowUpRequired = true
        });

        Assert.Equal(TechnicalQuestionGenerationReason.ReliabilityMinimum, result.NextGenerationReason);
        Assert.Equal(TechnicalAttemptType.FollowUp, result.NextAttemptType);
    }

    private static TechnicalAdaptiveRuleInput Input(decimal score) => new(
        TechnicalAttemptType.Main,
        score,
        0,
        0,
        0,
        0,
        0,
        3,
        false,
        false);
}
