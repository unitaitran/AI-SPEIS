using System.Collections.Immutable;
using ai_speis_be.Models.Enums;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Orchestration;

namespace ai_speis_be.Tests.TechnicalInterviews;

internal static class TechnicalParallelTestData
{
    public static TechnicalAnswerProcessingContext CreateContext(
        int clarificationCount = 0,
        int followUpCount = 0,
        int completedMainQuestions = 0,
        int targetMainQuestions = 3,
        TechnicalAttemptType attemptType = TechnicalAttemptType.Main,
        decimal initialMainScore = 9m,
        int requiredFollowUpCount = 0,
        bool reliabilityRequired = false,
        decimal cumulativeFollowUpBonus = 0m)
    {
        var rubric = TechnicalTestRubric.Create();
        return new TechnicalAnswerProcessingContext
        {
            SessionId = 7,
            AttemptId = Guid.NewGuid(),
            RootMainAttemptId = Guid.NewGuid(),
            QuestionId = 1,
            QuestionType = attemptType == TechnicalAttemptType.FollowUp
                ? "FOLLOW_UP"
                : attemptType.ToString().ToUpperInvariant(),
            AttemptType = attemptType,
            QuestionContent = "Explain dependency injection.",
            MainQuestionContent = "Explain dependency injection.",
            ExpectedAnswer = "Dependency injection separates object construction from object use.",
            KeyPoints = "separation of concerns, testability",
            QuestionSpecificRubric = string.Empty,
            GlobalRubricVersion = rubric.Version,
            Rubric = new TechnicalRubricPromptSnapshot(
                rubric.MinimumScore,
                rubric.MaximumScore,
                rubric.EvidenceRequiredWhenScoreAbove,
                rubric.Dimensions.Select(item => new TechnicalRubricPromptDimension(
                    item.Code,
                    item.Name,
                    item.Description,
                    item.Weight)).ToImmutableArray(),
                rubric.Levels.Select(item => new TechnicalRubricPromptLevel(
                    item.Code,
                    item.Score,
                    item.Description)).ToImmutableArray()),
            CandidateAnswer = "dependency injection improves testability by separating construction from use",
            PreviousAnswers = ImmutableArray<TechnicalAnswerContext>.Empty,
            RemainingMissingEvidence = ImmutableArray<string>.Empty,
            JobRole = "Backend Developer",
            ExperienceLevel = "Junior",
            Language = "vi",
            CvContext = "{}",
            JdContext = "{}",
            ClarificationCount = clarificationCount,
            FollowUpCount = followUpCount,
            CompletedMainQuestionCount = completedMainQuestions,
            MainQuestionIndex = completedMainQuestions + 1,
            TargetMainQuestionCount = targetMainQuestions,
            PromptVersions = new TechnicalPromptVersionSnapshot(
                TechnicalPromptVersions.Evaluation),
            UseAdaptiveRubricFramework = true,
            InitialMainScore = attemptType == TechnicalAttemptType.Main ? null : initialMainScore,
            CurrentMainBaseScore = initialMainScore,
            RequiredClarificationCount = clarificationCount,
            CompletedClarificationCount = clarificationCount,
            RequiredFollowUpCount = requiredFollowUpCount,
            CompletedFollowUpCount = followUpCount,
            CumulativeFollowUpBonus = cumulativeFollowUpBonus,
            IsReliabilityFollowUpRequired = reliabilityRequired,
            ScoringPolicyVersion = rubric.ScoringPolicyVersion,
            AdaptiveRuleVersion = "technical-rubric-bank-v2",
            BonusCalculationVersion = "technical-follow-up-bonus-v1"
        };
    }

    public static TechnicalAIEvaluationResponse CreateEvaluation(
        decimal score = 9m,
        string answerQuality = "PARTIAL")
    {
        var evaluation = TechnicalTestRubric.CreateEvaluation(score, score, score, score, score);
        evaluation.Evaluation.AnswerQuality = answerQuality;
        evaluation.Evaluation.Evidence = new List<string>
        {
            "dependency injection improves testability by separating construction from use"
        };
        evaluation.DimensionEvaluations[1].MissingEvidence = new List<string> { "A deeper trade-off analysis" };
        return evaluation;
    }

    public static TechnicalAITaskOutcome<T> Fulfilled<T>(T data, long latencyMs = 20)
    {
        var startedAt = DateTime.UtcNow.AddMilliseconds(-latencyMs);
        return new TechnicalAITaskOutcome<T>(
            TechnicalAITaskStatus.Fulfilled,
            new AIProviderResult<T>
            {
                Success = true,
                Data = data,
                Model = "fake-gemini",
                LatencyMs = latencyMs,
                StartedAt = startedAt,
                CompletedAt = DateTime.UtcNow
            },
            startedAt,
            DateTime.UtcNow,
            latencyMs,
            null);
    }

    public static TechnicalAITaskOutcome<T> Failed<T>(
        TechnicalAITaskStatus status,
        string errorCode,
        long latencyMs = 20)
    {
        var startedAt = DateTime.UtcNow.AddMilliseconds(-latencyMs);
        return new TechnicalAITaskOutcome<T>(
            status,
            new AIProviderResult<T>
            {
                Success = false,
                Model = "fake-gemini",
                ErrorCode = errorCode,
                LatencyMs = latencyMs,
                StartedAt = startedAt,
                CompletedAt = DateTime.UtcNow
            },
            startedAt,
            DateTime.UtcNow,
            latencyMs,
            errorCode);
    }

    public static TechnicalAnswerEvaluationProcessingResult Results(
        TechnicalAITaskOutcome<TechnicalAIEvaluationResponse>? evaluation = null)
    {
        return new TechnicalAnswerEvaluationProcessingResult(
            evaluation ?? Fulfilled(CreateEvaluation()),
            new TechnicalEvaluationProcessingMetrics(25, 25, 0));
    }
}
