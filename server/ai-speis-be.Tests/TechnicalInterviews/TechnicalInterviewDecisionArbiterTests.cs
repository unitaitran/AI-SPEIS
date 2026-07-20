using ai_speis_be.Models.Enums;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Orchestration;
using ai_speis_be.TechnicalInterviews.Scoring;
using ai_speis_be.TechnicalInterviews.Validation;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalInterviewDecisionArbiterTests
{
    private readonly TechnicalInterviewDecisionArbiter _arbiter = new(
        new TechnicalAIResponseValidator(),
        new TechnicalRubricScoringService(),
        new TechnicalFollowUpDecisionEngine());

    [Fact]
    public void Resolve_ClarificationUsesValidatedClarificationCandidate()
    {
        var evaluation = TechnicalParallelTestData.CreateEvaluation("CLARIFICATION");

        var result = _arbiter.Resolve(
            TechnicalParallelTestData.CreateContext(),
            TechnicalTestRubric.Create(),
            TechnicalParallelTestData.Results(
                evaluation: TechnicalParallelTestData.Fulfilled(evaluation)),
            new HashSet<int> { 10, 20 });

        Assert.True(result.IsSuccess);
        Assert.Equal(TechnicalInterviewDecision.Clarification, result.Decision);
        Assert.Equal(TechnicalAttemptType.Clarification, result.NextQuestion!.AttemptType);
        Assert.Contains("làm rõ", result.NextQuestion.Content!, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.QuestionFallbackUsed);
    }

    [Fact]
    public void Resolve_FollowUpLimitForcesNextMainQuestion()
    {
        var evaluation = TechnicalParallelTestData.CreateEvaluation("FOLLOW_UP");

        var result = _arbiter.Resolve(
            TechnicalParallelTestData.CreateContext(followUpCount: 2),
            TechnicalTestRubric.Create(),
            TechnicalParallelTestData.Results(
                evaluation: TechnicalParallelTestData.Fulfilled(evaluation)),
            new HashSet<int> { 10, 20 });

        Assert.True(result.IsSuccess);
        Assert.True(result.FinalizeMainQuestion);
        Assert.Equal(TechnicalInterviewDecision.NextQuestion, result.Decision);
        Assert.Equal(10, result.NextQuestion!.SelectedMainQuestionId);
    }

    [Fact]
    public void Resolve_QuestionIdOutsidePoolUsesStableBackendFallback()
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
        Assert.Equal(TechnicalAITaskStatus.FallbackUsed, result.QuestionStatus);
    }

    [Fact]
    public void Resolve_EndInterviewIgnoresRejectedQuestionBundle()
    {
        var evaluation = TechnicalParallelTestData.CreateEvaluation("END_INTERVIEW");
        var result = _arbiter.Resolve(
            TechnicalParallelTestData.CreateContext(completedMainQuestions: 4, targetMainQuestions: 5),
            TechnicalTestRubric.Create(),
            TechnicalParallelTestData.Results(
                evaluation: TechnicalParallelTestData.Fulfilled(evaluation),
                bundle: TechnicalParallelTestData.Failed<TechnicalAIQuestionBundleResponse>(
                    TechnicalAITaskStatus.Rejected,
                    "GEMINI_QUOTA_EXCEEDED")),
            new HashSet<int>());

        Assert.True(result.IsSuccess);
        Assert.Equal(TechnicalInterviewDecision.EndInterview, result.Decision);
        Assert.Null(result.NextQuestion);
        Assert.False(result.QuestionFallbackUsed);
    }

    [Fact]
    public void Resolve_FeedbackFailureUsesDeterministicFeedbackWithoutBlockingQuestion()
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
        Assert.Equal(TechnicalAITaskStatus.FallbackUsed, result.FeedbackStatus);
        Assert.Equal(10, result.NextQuestion!.SelectedMainQuestionId);
        Assert.NotEmpty(result.Feedback!.MissingPoints);
    }

    [Fact]
    public void Resolve_EvaluationFailureRejectsSpeculativeResults()
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
        Assert.Equal("GEMINI_QUOTA_EXCEEDED", result.ErrorCode);
    }

    [Fact]
    public void Resolve_RemovesDraftFeedbackThatIsNotSupportedByEvaluation()
    {
        var draft = TechnicalParallelTestData.CreateFeedback();
        draft.ImprovementSuggestions.Add("Migrate the entire system to COBOL immediately.");

        var result = _arbiter.Resolve(
            TechnicalParallelTestData.CreateContext(),
            TechnicalTestRubric.Create(),
            TechnicalParallelTestData.Results(
                feedback: TechnicalParallelTestData.Fulfilled(draft)),
            new HashSet<int> { 10, 20 });

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Feedback!.ImprovementSuggestions, item =>
            item.Contains("COBOL", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Resolve_InvalidFollowUpCandidateUsesDeterministicSubQuestionFallback()
    {
        var evaluation = TechnicalParallelTestData.CreateEvaluation("FOLLOW_UP");
        var bundle = TechnicalParallelTestData.CreateBundle();
        bundle.FollowUpCandidate!.TargetRubricCodes = new List<string> { "UNKNOWN_RUBRIC" };

        var result = _arbiter.Resolve(
            TechnicalParallelTestData.CreateContext(),
            TechnicalTestRubric.Create(),
            TechnicalParallelTestData.Results(
                evaluation: TechnicalParallelTestData.Fulfilled(evaluation),
                bundle: TechnicalParallelTestData.Fulfilled(bundle)),
            new HashSet<int> { 10, 20 });

        Assert.True(result.IsSuccess);
        Assert.Equal(TechnicalAttemptType.FollowUp, result.NextQuestion!.AttemptType);
        Assert.True(result.QuestionFallbackUsed);
        Assert.NotEmpty(result.NextQuestion.TargetRubricCodes);
    }
}
