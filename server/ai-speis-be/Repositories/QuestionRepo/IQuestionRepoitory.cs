using ai_speis_be.Models;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.Repositories.QuestionRepo
{
    public interface IQuestionRepoitory
    {
        Task<IEnumerable<Question>> GetQuestionsAsync(string ? roleTarget, string? major, string? difficulty );
        Task<IEnumerable<Question>> GetQuestionsAdminAsync(string ? roleTarget, string? major, string? difficulty );
        Task<Question?> GetQuestionByIdAsync(int questionId);
        Task<Question?> GetQuestionByIdAdminAsync(int questionId);


    }
}
