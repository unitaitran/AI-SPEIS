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
        Assert.Equal("RUBRIC_RULE_LOW_SCORE_CLARIFICATION", result.DecisionReason);
        Assert.Null(result.OverrideReason);
    }

    [Fact]
    public void Resolve_VeryWeakMainAlwaysUsesSingleClarification()
    {
        var result = Resolve(
            TechnicalParallelTestData.CreateContext(),
            TechnicalParallelTestData.CreateEvaluation(2m, "INCORRECT"));

        Assert.Equal(TechnicalInterviewDecision.Clarification, result.Decision);
        Assert.Equal(TechnicalAttemptType.Clarification, result.NextQuestion!.AttemptType);
    }

    [Fact]
    public void Resolve_StillVeryWeakClarificationAdvancesWithoutAnotherSubQuestion()
    {
        var context = TechnicalParallelTestData.CreateContext(
            clarificationCount: 0,
            attemptType: TechnicalAttemptType.Clarification,
            initialMainScore: 2m);

        var result = Resolve(context, TechnicalParallelTestData.CreateEvaluation(2m, "INSUFFICIENT"));

        Assert.True(result.FinalizeMainQuestion);
        Assert.NotEqual(TechnicalInterviewDecision.Clarification, result.Decision);
        Assert.NotEqual(TechnicalInterviewDecision.FollowUp, result.Decision);
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
    public void Resolve_ClarificationScoreUsesSeventyFivePercentBaseWhenChainEnds()
    {
        var context = TechnicalParallelTestData.CreateContext(
            attemptType: TechnicalAttemptType.Clarification,
            initialMainScore: 2m);

        var result = Resolve(context, TechnicalParallelTestData.CreateEvaluation(8m));

        Assert.True(result.FinalizeMainQuestion);
        Assert.Equal(6m, result.FinalMainQuestionScore);
    }

    [Fact]
    public void Resolve_MainBelowFiveRequiresBothFollowUpsEvenWhenFirstFollowUpScoresHighly()
    {
        var context = TechnicalParallelTestData.CreateContext(
            followUpCount: 0,
            attemptType: TechnicalAttemptType.FollowUp,
            initialMainScore: 4m,
            requiredFollowUpCount: 2);

        var result = Resolve(context, TechnicalParallelTestData.CreateEvaluation(9m));

        Assert.False(result.FinalizeMainQuestion);
        Assert.Equal(TechnicalInterviewDecision.FollowUp, result.Decision);
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
    public void Resolve_CriticalAiFailureDoesNotPersistArtificialZeroScore(
        TechnicalAITaskStatus status,
        string errorCode)
    {
        var result = _arbiter.Resolve(
            TechnicalParallelTestData.CreateContext(),
            TechnicalTestRubric.Create(),
            TechnicalParallelTestData.Results(
                evaluation: TechnicalParallelTestData.Failed<TechnicalAIEvaluationResponse>(status, errorCode)));

        Assert.False(result.IsSuccess);
        Assert.False(result.EvaluationFallbackUsed);
        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Null(result.Score);
        Assert.Null(result.NextQuestion);
    }

    [Fact]
    public void Resolve_InvalidEvidenceDoesNotPersistArtificialZeroScore()
    {
        var evaluation = TechnicalParallelTestData.CreateEvaluation(4m);
        evaluation.DimensionEvaluations[0].Evidence = new List<string> { "invented evidence" };

        var result = Resolve(
            TechnicalParallelTestData.CreateContext(),
            evaluation);

        Assert.False(result.IsSuccess);
        Assert.Equal("EVIDENCE_NOT_IN_ANSWER", result.ErrorCode);
        Assert.Equal(TechnicalAITaskStatus.InvalidOutput, result.EvaluationStatus);
        Assert.False(result.EvaluationFallbackUsed);
        Assert.Null(result.Score);
    }

    [Fact]
    public void Resolve_MissingScoreEvidenceUsesVerbatimTranscriptWithoutChangingScore()
    {
        var context = TechnicalParallelTestData.CreateContext();
        var evaluation = TechnicalParallelTestData.CreateEvaluation(4m);
        evaluation.DimensionEvaluations[3].Evidence.Clear();

        var result = Resolve(context, evaluation);

        Assert.True(result.IsSuccess);
        Assert.Equal(4m, result.Score!.FinalOverallScore);
        Assert.Equal(
            new[] { context.CandidateAnswer },
            result.EffectiveEvaluation!.DimensionEvaluations[3].Evidence);
        Assert.Equal("EVIDENCE_GROUNDED_FROM_TRANSCRIPT", result.OverrideReason);
        Assert.False(result.EvaluationFallbackUsed);
    }

    [Fact]
    public void Resolve_PerAnswerFeedbackRemainsDisabledWithoutLosingEvaluationOrTransition()
    {
        var result = _arbiter.Resolve(
            TechnicalParallelTestData.CreateContext(),
            TechnicalTestRubric.Create(),
            TechnicalParallelTestData.Results());

        Assert.True(result.IsSuccess);
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
