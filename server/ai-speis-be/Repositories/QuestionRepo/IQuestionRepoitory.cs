using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Repositories.QuestionRepo
{
    public interface IQuestionRepoitory
    {
        Task<PagedResultDto<Question>> GetAdminQuestionsAsync(
            AdminQuestionQueryDto query,
            CancellationToken cancellationToken = default);
        Task<PagedResultDto<Question>> GetDeletedAdminQuestionsAsync(
            AdminQuestionQueryDto query,
            CancellationToken cancellationToken = default);
        Task<PagedResultDto<Question>> GetQuestionsAsync(
            UserQuestionQueryDto query,
            CancellationToken cancellationToken = default);
        Task<Question?> GetQuestionByIdAsync(
            int questionId,
            CancellationToken cancellationToken = default);
        Task<Question?> GetQuestionByIdAdminAsync(
            int questionId,
            CancellationToken cancellationToken = default);
        Task<Question> CreateQuestionAsync(
            Question question,
            CancellationToken cancellationToken = default);
        Task<int> CreateQuestionsAsync(
            IReadOnlyCollection<Question> questions,
            CancellationToken cancellationToken = default);
        Task UpdateQuestionAsync(
            Question question,
            CancellationToken cancellationToken = default);
        Task<QuestionFiltersDto> GetQuestionFiltersAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Question>> GetTechnicalCandidatesAsync(
            TechnicalQuestionCandidateQuery query,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Question>> GetActiveTechnicalQuestionsByIdsAsync(
            IReadOnlyCollection<int> questionIds,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<string>> GetTechnicalSkillsAsync(
            string language,
            IReadOnlyCollection<string> roleTargets,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Question>> GetBehaviouralCandidatesAsync(
            BehaviouralQuestionCandidateQuery query,
            CancellationToken cancellationToken = default);
    }

    public sealed class BehaviouralQuestionCandidateQuery
    {
        public string Language { get; set; } = string.Empty;
        public IReadOnlyCollection<string> RoleTargets { get; set; } = Array.Empty<string>();
        public IReadOnlyCollection<string> ExperienceLevels { get; set; } = Array.Empty<string>();
        public IReadOnlyCollection<string> Skills { get; set; } = Array.Empty<string>();
        public Models.Enums.QuestionDifficultyEnum? Difficulty { get; set; }
        public IReadOnlySet<int> ExcludedQuestionIds { get; set; } = new HashSet<int>();
        public int MaximumResults { get; set; } = 50;
    }
}
