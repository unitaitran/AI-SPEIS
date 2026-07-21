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
        TechnicalQuestionGenerationReason? generationReason = null,
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
            AskedQuestionIds = ImmutableHashSet.Create(1),
            CandidateQuestionPool = ImmutableArray.Create(
                new TechnicalAIQuestionCandidate(10, "Question 10", "ASP.NET Core", null, "Medium", "Junior"),
                new TechnicalAIQuestionCandidate(20, "Question 20", "Database", null, "Medium", "Junior")),
            SkillCoverage = ImmutableDictionary<string, int>.Empty.Add("ASP.NET Core", 1),
            DifficultyCoverage = ImmutableDictionary<string, int>.Empty.Add("Medium", 1),
            PromptVersions = new TechnicalPromptVersionSnapshot(
                TechnicalPromptVersions.Evaluation,
                TechnicalPromptVersions.Feedback,
                TechnicalPromptVersions.QuestionBundle),
            UseAdaptiveRubricFramework = true,
            MatchScore = 75,
            MatchBand = TechnicalMatchBand.High,
            InitialMainScore = attemptType == TechnicalAttemptType.Main ? null : initialMainScore,
            CurrentMainBaseScore = initialMainScore,
            RequiredClarificationCount = clarificationCount,
            CompletedClarificationCount = clarificationCount,
            RequiredFollowUpCount = requiredFollowUpCount,
            CompletedFollowUpCount = followUpCount,
            CumulativeFollowUpBonus = cumulativeFollowUpBonus,
            TotalMainCount = completedMainQuestions + 1,
            TotalFollowUpCount = followUpCount,
            TotalClarificationCount = clarificationCount,
            IsReliabilityFollowUpRequired = reliabilityRequired,
            CurrentGenerationReason = generationReason,
            ScoringPolicyVersion = rubric.ScoringPolicyVersion,
            AdaptiveRuleVersion = "technical-adaptive-v1",
            BonusCalculationVersion = "technical-follow-up-bonus-v1"
        };
    }

    public static TechnicalAIEvaluationResponse CreateEvaluation(
        string decision = "NEXT_QUESTION",
        decimal score = 9m)
    {
        var evaluation = TechnicalTestRubric.CreateEvaluation(score, score, score, score, score);
        evaluation.Decision = decision;
        evaluation.Strengths = new List<string> { "Clear dependency injection explanation" };
        evaluation.MissingPoints = new List<string> { "Needs a concrete lifetime example" };
        evaluation.ImprovementSuggestions = new List<string> { "Add a practical lifetime example" };
        evaluation.DimensionEvaluations[1].MissingEvidence = new List<string> { "A deeper trade-off analysis" };
        return evaluation;
    }

    public static TechnicalAIFeedbackDraftResponse CreateFeedback()
    {
        return new TechnicalAIFeedbackDraftResponse
        {
            Summary = "The dependency injection explanation is clear but needs a concrete lifetime example.",
            Strengths = new List<string> { "Clear explanation" },
            MissingPoints = new List<string> { "Missing lifetime example" },
            ImprovementSuggestions = new List<string> { "Add a concrete lifetime example" }
        };
    }

    public static TechnicalAIQuestionBundleResponse CreateBundle(int selectedQuestionId = 10)
    {
        return new TechnicalAIQuestionBundleResponse
        {
            ClarificationCandidate = new TechnicalAISubQuestionCandidate
            {
                Content = "Could you clarify how you reached that conclusion?",
                Purpose = "Clarify the reasoning",
                TargetRubricCodes = new List<string> { "REASONING" }
            },
            FollowUpCandidate = new TechnicalAISubQuestionCandidate
            {
                Content = "Could you add a concrete practical example?",
                Purpose = "Collect missing practical evidence",
                TargetRubricCodes = new List<string> { "TECHNICAL_DEPTH" }
            },
            NextMainQuestionCandidate = new TechnicalAINextMainQuestionCandidate
            {
                SelectedQuestionId = selectedQuestionId
            }
        };
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

    public static TechnicalParallelAIResults Results(
        TechnicalAITaskOutcome<TechnicalAIEvaluationResponse>? evaluation = null,
        TechnicalAITaskOutcome<TechnicalAIFeedbackDraftResponse>? feedback = null,
        TechnicalAITaskOutcome<TechnicalAIQuestionBundleResponse>? bundle = null)
    {
        return new TechnicalParallelAIResults(
            evaluation ?? Fulfilled(CreateEvaluation()),
            feedback ?? Fulfilled(CreateFeedback()),
            bundle ?? Fulfilled(CreateBundle()),
            new TechnicalParallelProcessingMetrics(25, 60, 35));
    }
}
