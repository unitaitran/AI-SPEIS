using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Repositories.QuestionRepo
{
    public interface IQuestionRepoitory
    {
        Task<PagedResultDto<Question>> GetAdminQuestionsAsync(
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


    }
}
