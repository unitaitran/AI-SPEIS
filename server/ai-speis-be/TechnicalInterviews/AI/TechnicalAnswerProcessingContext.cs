using System.Collections.Immutable;

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
        string Evaluation,
        string Feedback,
        string QuestionBundle);

    public sealed record TechnicalAnswerProcessingContext
    {
        public required int SessionId { get; init; }
        public required Guid AttemptId { get; init; }
        public required Guid RootMainAttemptId { get; init; }
        public required int QuestionId { get; init; }
        public required string QuestionType { get; init; }
        public required string QuestionContent { get; init; }
        public required string MainQuestionContent { get; init; }
        public required string ExpectedAnswer { get; init; }
        public required string KeyPoints { get; init; }
        public required string QuestionSpecificRubric { get; init; }
        public required string GlobalRubricVersion { get; init; }
        public required TechnicalRubricPromptSnapshot Rubric { get; init; }
        public required string CandidateAnswer { get; init; }
        public required ImmutableArray<TechnicalAnswerContext> PreviousAnswers { get; init; }
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
        public required ImmutableHashSet<int> AskedQuestionIds { get; init; }
        public required ImmutableArray<TechnicalAIQuestionCandidate> CandidateQuestionPool { get; init; }
        public required ImmutableDictionary<string, int> SkillCoverage { get; init; }
        public required ImmutableDictionary<string, int> DifficultyCoverage { get; init; }
        public required TechnicalPromptVersionSnapshot PromptVersions { get; init; }

        public ImmutableArray<TechnicalAnswerContext> BuildCompleteAnswerContext()
        {
            return PreviousAnswers.Add(new TechnicalAnswerContext(
                QuestionType,
                QuestionContent,
                CandidateAnswer));
        }
    }
}
