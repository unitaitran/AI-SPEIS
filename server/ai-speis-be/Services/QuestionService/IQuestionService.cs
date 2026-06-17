using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Services.QuestionService
{
    public interface IQuestionService
    {
        Task<IEnumerable<QuestionResponseDto>> GetQuestionsAsync(string? roleTarget, string? major, string? difficulty);
        Task<IEnumerable<QuestionResponseDto>> GetQuestionsAdminAsync(string? roleTarget, string? major, string? difficulty);
        Task<QuestionResponseDto?> GetQuestionByIdAsync(int questionId);
        Task<QuestionResponseDto?> GetQuestionByIdAdminAsync(int questionId);

    }
}
