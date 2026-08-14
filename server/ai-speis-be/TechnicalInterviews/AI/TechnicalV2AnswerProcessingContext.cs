using System.Collections.Immutable;
using ai_speis_be.TechnicalInterviews.Rubrics;

namespace ai_speis_be.TechnicalInterviews.AI
{
    public sealed record TechnicalV2AnswerProcessingContext
    {
        public required int SessionId { get; init; }
        public required int QuestionId { get; init; }
        public required string QuestionType { get; init; }
        public required string QuestionContent { get; init; }
        public required string ExpectedAnswer { get; init; }
        public required string KeyPoints { get; init; }
        public required string QuestionSpecificRubric { get; init; }
        public required string GlobalRubricVersion { get; init; }
        public required TechnicalRubricPromptSnapshot Rubric { get; init; }
        public required string CandidateAnswer { get; init; }
        public required string JobRole { get; init; }
        public required string ExperienceLevel { get; init; }
        public required string Language { get; init; }
        public required string CvContext { get; init; }
        public required string JdContext { get; init; }
        public required int QuestionOrder { get; init; }
        public required int TargetQuestionCount { get; init; }
        public required string ScoringPolicyVersion { get; init; }
        public bool EvidenceRepairAttempt { get; init; }
        public string EvaluationModelOverride { get; init; } = string.Empty;

        public ImmutableArray<TechnicalAnswerContext> BuildAnswerContext() => ImmutableArray.Create(
            new TechnicalAnswerContext(QuestionType, QuestionContent, CandidateAnswer));
    }
}
