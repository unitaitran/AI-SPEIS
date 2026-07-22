using ai_speis_be.Models.Enums;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.TechnicalInterviews.Orchestration;
using ai_speis_be.TechnicalInterviews.Scoring;
using ai_speis_be.TechnicalInterviews.Validation;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalInterviewDecisionArbiterTests
{
    private static readonly TechnicalInterviewOptions Options = new()
    {
        ClarificationRecoveryFactor = 0.75m,
        ReliabilityMinimumQuestionCount = 5,
        ReliabilityFollowUpLimit = 2
    };

    private readonly TechnicalInterviewDecisionArbiter _arbiter = new(
        new TechnicalAIResponseValidator(),
        new TechnicalRubricScoringService(),
        new TechnicalFollowUpDecisionEngine(),
        new TechnicalFollowUpBonusCalculator(Options),
        Options);

    [Fact]
    public void Resolve_UsesRubricRuleForAmbiguousVeryWeakAnswer()
    {
        var result = Resolve(
            TechnicalParallelTestData.CreateContext(),
            TechnicalParallelTestData.CreateEvaluation(2m, "AMBIGUOUS"));

        Assert.Equal(TechnicalInterviewDecision.Clarification, result.Decision);
        Assert.Equal(TechnicalAttemptType.Clarification, result.NextQuestion!.AttemptType);
        Assert.Null(result.AiSuggestedAction);
        Assert.Equal("RUBRIC_RULE_LOW_SCORE_AMBIGUOUS_ANSWER", result.DecisionReason);
        Assert.Null(result.OverrideReason);
    }

    [Fact]
    public void Resolve_ClarificationAnswerCanContinueWithRubricFollowUp()
    {
        var context = TechnicalParallelTestData.CreateContext(
            clarificationCount: 0,
            attemptType: TechnicalAttemptType.Clarification,
            initialMainScore: 2m);

        var result = Resolve(context, TechnicalParallelTestData.CreateEvaluation(6m));

        Assert.False(result.FinalizeMainQuestion);
        Assert.Equal(TechnicalInterviewDecision.FollowUp, result.Decision);
    }

    [Fact]
    public void Resolve_ClarificationRecoveredIntoFiveToEightBandRequestsOneFollowUp()
    {
        var context = TechnicalParallelTestData.CreateContext(
            attemptType: TechnicalAttemptType.Clarification,
            initialMainScore: 2m);

        var result = Resolve(context, TechnicalParallelTestData.CreateEvaluation(8m));

        Assert.False(result.FinalizeMainQuestion);
        Assert.Equal(TechnicalInterviewDecision.FollowUp, result.Decision);
        Assert.Equal(1, result.RequiredFollowUpCount);
    }

    [Fact]
    public void Resolve_NeverAllowsSecondClarification()
    {
        var context = TechnicalParallelTestData.CreateContext(
            clarificationCount: 1,
            attemptType: TechnicalAttemptType.FollowUp,
            initialMainScore: 3m);

        var result = Resolve(context, TechnicalParallelTestData.CreateEvaluation(2m, "AMBIGUOUS"));

        Assert.NotEqual(TechnicalInterviewDecision.Clarification, result.Decision);
        Assert.Null(result.AiSuggestedAction);
    }

    [Fact]
    public void Resolve_EnforcesTwoFollowUpAndThreeSubQuestionBudgets()
    {
        var context = TechnicalParallelTestData.CreateContext(
            clarificationCount: 1,
            followUpCount: 1,
            attemptType: TechnicalAttemptType.FollowUp,
            initialMainScore: 3m);

        var result = Resolve(context, TechnicalParallelTestData.CreateEvaluation(4m));

        Assert.True(result.FinalizeMainQuestion);
        Assert.True(result.Decision is TechnicalInterviewDecision.NextQuestion or TechnicalInterviewDecision.EndInterview);
    }

    [Fact]
    public void Resolve_OverridesNextMainForReliabilityMinimum()
    {
        var context = TechnicalParallelTestData.CreateContext(
            completedMainQuestions: 2,
            targetMainQuestions: 3,
            reliabilityRequired: true);

        var result = Resolve(context, TechnicalParallelTestData.CreateEvaluation(9m));

        Assert.Equal(TechnicalInterviewDecision.FollowUp, result.Decision);
        Assert.Equal(TechnicalQuestionGenerationReason.ReliabilityMinimum, result.NextQuestion!.GenerationReason);
        Assert.Equal("RELIABILITY_MINIMUM", result.OverrideReason);
    }

    [Fact]
    public void Resolve_ClarificationDoesNotCountTowardReliabilityMinimum()
    {
        var context = TechnicalParallelTestData.CreateContext(
            clarificationCount: 1,
            completedMainQuestions: 2,
            targetMainQuestions: 3,
            reliabilityRequired: true);

        var result = Resolve(context, TechnicalParallelTestData.CreateEvaluation(9m));

        Assert.Equal(TechnicalInterviewDecision.FollowUp, result.Decision);
        Assert.Equal(TechnicalQuestionGenerationReason.ReliabilityMinimum, result.NextQuestion!.GenerationReason);
    }

    [Theory]
    [InlineData(TechnicalAITaskStatus.Timeout, "TIMEOUT")]
    [InlineData(TechnicalAITaskStatus.InvalidOutput, "MALFORMED_JSON")]
    public void Resolve_CriticalAiFailureUsesValidatedDeterministicFallback(
        TechnicalAITaskStatus status,
        string errorCode)
    {
        var result = _arbiter.Resolve(
            TechnicalParallelTestData.CreateContext(),
            TechnicalTestRubric.Create(),
            TechnicalParallelTestData.Results(
                evaluation: TechnicalParallelTestData.Failed<TechnicalAIEvaluationResponse>(status, errorCode)));

        Assert.True(result.IsSuccess);
        Assert.True(result.EvaluationFallbackUsed);
        Assert.NotNull(result.Score);
        Assert.NotNull(result.NextQuestion);
    }

    [Fact]
    public void Resolve_DoesNotCreatePerAnswerFeedbackTask()
    {
        var result = _arbiter.Resolve(
            TechnicalParallelTestData.CreateContext(),
            TechnicalTestRubric.Create(),
            TechnicalParallelTestData.Results());

        Assert.True(result.IsSuccess);
        Assert.Equal(TechnicalAITaskStatus.NotStarted, result.FeedbackStatus);
        Assert.False(result.FeedbackFallbackUsed);
        Assert.NotNull(result.Score);
    }

    private TechnicalDecisionArbiterResult Resolve(
        TechnicalAnswerProcessingContext context,
        TechnicalAIEvaluationResponse evaluation) =>
        _arbiter.Resolve(
            context,
            TechnicalTestRubric.Create(),
            TechnicalParallelTestData.Results(
                evaluation: TechnicalParallelTestData.Fulfilled(evaluation)));
}
