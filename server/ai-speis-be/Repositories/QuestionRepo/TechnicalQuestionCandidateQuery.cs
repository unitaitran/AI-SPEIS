using ai_speis_be.Models.Enums;

namespace ai_speis_be.Repositories.QuestionRepo
{
    public sealed class TechnicalQuestionCandidateQuery
    {
        public string Language { get; init; } = string.Empty;
        public IReadOnlyCollection<string> RoleTargets { get; init; } = Array.Empty<string>();
        public IReadOnlyCollection<string> ExperienceLevels { get; init; } = Array.Empty<string>();
        public IReadOnlyCollection<string> Skills { get; init; } = Array.Empty<string>();
        public QuestionDifficultyEnum? Difficulty { get; init; }
        public IReadOnlyCollection<int> ExcludedQuestionIds { get; init; } = Array.Empty<int>();
        public int MaximumResults { get; init; } = 100;
    }
}
