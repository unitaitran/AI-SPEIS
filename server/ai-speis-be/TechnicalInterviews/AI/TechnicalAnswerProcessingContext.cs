using System.Collections.Immutable;
using ai_speis_be.Models.Enums;
using ai_speis_be.TechnicalInterviews.Planning;

namespace ai_speis_be.TechnicalInterviews.AI
{
    public sealed record TechnicalRubricPromptDimension(
        string Code,
        string Name,
        string Description,
        decimal Weight);

    public sealed record TechnicalRubricPromptLevel(
        string Code,
        int Score,
        string Description);

    public sealed record TechnicalRubricPromptSnapshot(
        decimal MinimumScore,
        decimal MaximumScore,
        decimal EvidenceRequiredWhenScoreAbove,
        ImmutableArray<TechnicalRubricPromptDimension> Dimensions,
        ImmutableArray<TechnicalRubricPromptLevel> Levels);

    public sealed record TechnicalPromptVersionSnapshot(
        string Evaluation);

    public sealed record TechnicalAnswerProcessingContext
    {
        public required int SessionId { get; init; }
        public required Guid AttemptId { get; init; }
        public required Guid RootMainAttemptId { get; init; }
        public required int QuestionId { get; init; }
        public required string QuestionType { get; init; }
        public required TechnicalAttemptType AttemptType { get; init; }
        public required string QuestionContent { get; init; }
        public required string MainQuestionContent { get; init; }
        public required string ExpectedAnswer { get; init; }
        public required string KeyPoints { get; init; }
        public required string QuestionSpecificRubric { get; init; }
        public required string GlobalRubricVersion { get; init; }
        public required TechnicalRubricPromptSnapshot Rubric { get; init; }
        public required string CandidateAnswer { get; init; }
        public required ImmutableArray<TechnicalAnswerContext> PreviousAnswers { get; init; }
        public required ImmutableArray<string> RemainingMissingEvidence { get; init; }
        public ImmutableArray<string> CollectedEvidence { get; init; } = ImmutableArray<string>.Empty;
        public ImmutableArray<string> PreviousIncorrectClaims { get; init; } = ImmutableArray<string>.Empty;
        public ImmutableArray<decimal> PreviousAttemptScores { get; init; } = ImmutableArray<decimal>.Empty;
        public required string JobRole { get; init; }
        public required string ExperienceLevel { get; init; }
        public required string Language { get; init; }
        public required string CvContext { get; init; }
        public required string JdContext { get; init; }
        public required int ClarificationCount { get; init; }
        public required int FollowUpCount { get; init; }
        public required int CompletedMainQuestionCount { get; init; }
        public required int MainQuestionIndex { get; init; }
        public required int TargetMainQuestionCount { get; init; }
        public required TechnicalPromptVersionSnapshot PromptVersions { get; init; }
        public required bool UseAdaptiveRubricFramework { get; init; }
        public TechnicalQuestionPlanSlot? CurrentPlanSlot { get; init; }
        public TechnicalQuestionSourceType? SourceType { get; init; }
        public string? TargetSkill { get; init; }
        public string? TargetSubskill { get; init; }
        public TechnicalEvaluationObjective? EvaluationObjective { get; init; }
        public decimal? InitialMainScore { get; init; }
        public decimal CurrentMainBaseScore { get; init; }
        public int RequiredClarificationCount { get; init; }
        public int CompletedClarificationCount { get; init; }
        public int RequiredFollowUpCount { get; init; }
        public int CompletedFollowUpCount { get; init; }
        public decimal CumulativeFollowUpBonus { get; init; }
        public int RemainingSubQuestionBudget { get; init; }
        public int ReliabilityCount { get; init; }
        public int ReliabilityMinimumQuestionCount { get; init; }
        public bool IsReliabilityFollowUpRequired { get; init; }
        public required string ScoringPolicyVersion { get; init; }
        public required string AdaptiveRuleVersion { get; init; }
        public required string BonusCalculationVersion { get; init; }
        public TechnicalLockedMainQuestionSnapshot? LockedMainQuestion { get; init; }

        public ImmutableArray<TechnicalAnswerContext> BuildCompleteAnswerContext()
        {
            return PreviousAnswers.Add(new TechnicalAnswerContext(
                QuestionType,
                QuestionContent,
                CandidateAnswer));
        }
    }
}
