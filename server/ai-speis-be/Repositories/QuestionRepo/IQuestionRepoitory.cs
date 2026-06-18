using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Repositories.QuestionRepo
{
    public interface IQuestionRepoitory
    {
        Task<PagedResultDto<Question>> GetAdminQuestionsAsync(
            AdminQuestionQueryDto query,
            CancellationToken cancellationToken = default);
        Task<IEnumerable<Question>> GetQuestionsAsync(string ? roleTarget, string? major, string? difficulty );
        Task<Question?> GetQuestionByIdAsync(int questionId);
        Task<Question?> GetQuestionByIdAdminAsync(int questionId);


    }
}
