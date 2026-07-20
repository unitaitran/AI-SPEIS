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
        AdaptiveRuleVersion = "technical-adaptive-v1",
        BonusCalculationVersion = "technical-follow-up-bonus-v1",
        ReliabilityFollowUpEnabled = true,
        ReliabilityMinimumQuestionCount = 5,
        ReliabilityFollowUpLimit = 1
    };

    private readonly TechnicalInterviewDecisionArbiter _arbiter = new(
        new TechnicalAIResponseValidator(),
        new TechnicalRubricScoringService(),
        new TechnicalFollowUpDecisionEngine(),
        new TechnicalAdaptiveRuleEngine(Options),
        new TechnicalFollowUpBonusCalculator(Options),
        Options);

    [Fact]
    public void Resolve_ScoreBelowThreeCreatesExactlyOneClarification()
    {
        var evaluation = TechnicalParallelTestData.CreateEvaluation("NEXT_QUESTION", 2m);

        var result = Resolve(TechnicalParallelTestData.CreateContext(), evaluation);

        Assert.True(result.IsSuccess);
        Assert.Equal(TechnicalInterviewDecision.Clarification, result.Decision);
        Assert.Equal(TechnicalAttemptType.Clarification, result.NextQuestion!.AttemptType);
        Assert.Equal(1, result.RequiredClarificationCount);
        Assert.Equal(0, result.RequiredFollowUpCount);
        Assert.Equal("BACKEND_ADAPTIVE_RULE_OVERRIDE", result.OverrideReason);
    }

    [Theory]
    [InlineData(3, 2)]
    [InlineData(4.99, 2)]
    [InlineData(5, 1)]
    [InlineData(7.99, 1)]
    public void Resolve_MainScoreBoundariesCreateRequiredFollowUps(double rawScore, int requiredCount)
    {
        var evaluation = TechnicalParallelTestData.CreateEvaluation("END_INTERVIEW", (decimal)rawScore);

        var result = Resolve(TechnicalParallelTestData.CreateContext(), evaluation);

        Assert.Equal(TechnicalInterviewDecision.FollowUp, result.Decision);
        Assert.Equal(requiredCount, result.RequiredFollowUpCount);
        Assert.False(result.FinalizeMainQuestion);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(10)]
    public void Resolve_ScoreAtLeastEightMovesToNextMain(double rawScore)
    {
        var evaluation = TechnicalParallelTestData.CreateEvaluation("CLARIFICATION", (decimal)rawScore);

        var result = Resolve(TechnicalParallelTestData.CreateContext(), evaluation);

        Assert.True(result.FinalizeMainQuestion);
        Assert.Equal(TechnicalInterviewDecision.NextQuestion, result.Decision);
        Assert.Equal((decimal)rawScore, result.FinalMainQuestionScore);
        Assert.Equal(10, result.NextQuestion!.SelectedMainQuestionId);
    }

    [Fact]
    public void Resolve_Fu2IsGeneratedOnlyAfterFu1Answer()
    {
        var context = TechnicalParallelTestData.CreateContext(
            attemptType: TechnicalAttemptType.FollowUp,
            initialMainScore: 4m,
            requiredFollowUpCount: 2,
            followUpCount: 0);

        var result = Resolve(context, TechnicalParallelTestData.CreateEvaluation(score: 7m));

        Assert.False(result.FinalizeMainQuestion);
        Assert.Equal(TechnicalInterviewDecision.FollowUp, result.Decision);
        Assert.Equal(2, result.RequiredFollowUpCount);
    }

    [Fact]
    public void Resolve_FollowUpUsesBonusInsteadOfAddingRawScore()
    {
        var context = TechnicalParallelTestData.CreateContext(
            attemptType: TechnicalAttemptType.FollowUp,
            initialMainScore: 6m,
            requiredFollowUpCount: 1,
            followUpCount: 0);

        var result = Resolve(context, TechnicalParallelTestData.CreateEvaluation(score: 10m));

        Assert.True(result.FinalizeMainQuestion);
        Assert.Equal(10m, result.RawScore);
        Assert.Equal(1m, result.AppliedBonus);
        Assert.Equal(7m, result.FinalMainQuestionScore);
    }

    [Fact]
    public void Resolve_CapsCumulativeFollowUpBonusAtTwoAndFinalScoreAtTen()
    {
        var context = TechnicalParallelTestData.CreateContext(
            attemptType: TechnicalAttemptType.FollowUp,
            initialMainScore: 9m,
            requiredFollowUpCount: 1,
            cumulativeFollowUpBonus: 1.5m);

        var result = Resolve(context, TechnicalParallelTestData.CreateEvaluation(score: 10m));

        Assert.Equal(1m, result.AppliedBonus);
        Assert.Equal(2m, result.CumulativeFollowUpBonus);
        Assert.Equal(10m, result.FinalMainQuestionScore);
    }

    [Fact]
    public void Resolve_ClarificationFinalScoreIsSeventyFivePercent()
    {
        var context = TechnicalParallelTestData.CreateContext(
            attemptType: TechnicalAttemptType.Clarification,
            initialMainScore: 2m);

        var result = Resolve(context, TechnicalParallelTestData.CreateEvaluation(score: 8m));

        Assert.True(result.FinalizeMainQuestion);
        Assert.Equal(6m, result.FinalMainQuestionScore);
    }

    [Fact]
    public void Resolve_LastMainAddsOneReliabilityFollowUpWhenCountIsBelowFive()
    {
        var context = TechnicalParallelTestData.CreateContext(
            completedMainQuestions: 2,
            targetMainQuestions: 3,
            reliabilityRequired: true);

        var result = Resolve(context, TechnicalParallelTestData.CreateEvaluation(score: 9m));

        Assert.False(result.FinalizeMainQuestion);
        Assert.Equal(TechnicalInterviewDecision.FollowUp, result.Decision);
        Assert.Equal(TechnicalQuestionGenerationReason.ReliabilityMinimum, result.NextQuestion!.GenerationReason);
    }

    [Fact]
    public void Resolve_ReliabilityFollowUpFinalizesWithoutRepeating()
    {
        var context = TechnicalParallelTestData.CreateContext(
            completedMainQuestions: 2,
            targetMainQuestions: 3,
            attemptType: TechnicalAttemptType.FollowUp,
            initialMainScore: 9m,
            reliabilityRequired: false,
            generationReason: TechnicalQuestionGenerationReason.ReliabilityMinimum);

        var result = Resolve(context, TechnicalParallelTestData.CreateEvaluation(score: 10m));

        Assert.True(result.FinalizeMainQuestion);
        Assert.Equal(TechnicalInterviewDecision.EndInterview, result.Decision);
        Assert.Equal(10m, result.FinalMainQuestionScore);
    }

    [Fact]
    public void Resolve_InvalidSpeculativeCandidateUsesStableBackendFallback()
    {
        var result = _arbiter.Resolve(
            TechnicalParallelTestData.CreateContext(),
            TechnicalTestRubric.Create(),
            TechnicalParallelTestData.Results(
                bundle: TechnicalParallelTestData.Fulfilled(
                    TechnicalParallelTestData.CreateBundle(999))),
            new HashSet<int> { 10, 20 });

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.NextQuestion!.SelectedMainQuestionId);
        Assert.True(result.QuestionFallbackUsed);
    }

    [Fact]
    public void Resolve_FeedbackFailureDoesNotBlockNextQuestion()
    {
        var result = _arbiter.Resolve(
            TechnicalParallelTestData.CreateContext(),
            TechnicalTestRubric.Create(),
            TechnicalParallelTestData.Results(
                feedback: TechnicalParallelTestData.Failed<TechnicalAIFeedbackDraftResponse>(
                    TechnicalAITaskStatus.Timeout,
                    "TIMEOUT")),
            new HashSet<int> { 10, 20 });

        Assert.True(result.IsSuccess);
        Assert.True(result.FeedbackFallbackUsed);
        Assert.NotNull(result.NextQuestion);
    }

    [Fact]
    public void Resolve_EvaluationFailureRejectsAllSpeculativeResults()
    {
        var result = _arbiter.Resolve(
            TechnicalParallelTestData.CreateContext(),
            TechnicalTestRubric.Create(),
            TechnicalParallelTestData.Results(
                evaluation: TechnicalParallelTestData.Failed<TechnicalAIEvaluationResponse>(
                    TechnicalAITaskStatus.Rejected,
                    "GEMINI_QUOTA_EXCEEDED")),
            new HashSet<int> { 10, 20 });

        Assert.False(result.IsSuccess);
        Assert.Null(result.Score);
        Assert.Null(result.NextQuestion);
    }

    [Fact]
    public void Resolve_InvalidAiActionCannotOverrideBackendRule()
    {
        var evaluation = TechnicalParallelTestData.CreateEvaluation("DO_WHATEVER", 2m);

        var result = Resolve(TechnicalParallelTestData.CreateContext(), evaluation);

        Assert.True(result.IsSuccess);
        Assert.Null(result.AiSuggestedAction);
        Assert.Equal(TechnicalInterviewDecision.Clarification, result.Decision);
        Assert.Equal("AI_SUGGESTED_ACTION_INVALID_OR_MISSING", result.OverrideReason);
    }

    private TechnicalDecisionArbiterResult Resolve(
        TechnicalAnswerProcessingContext context,
        TechnicalAIEvaluationResponse evaluation)
    {
        return _arbiter.Resolve(
            context,
            TechnicalTestRubric.Create(),
            TechnicalParallelTestData.Results(
                evaluation: TechnicalParallelTestData.Fulfilled(evaluation)),
            new HashSet<int> { 10, 20 });
    }
}
