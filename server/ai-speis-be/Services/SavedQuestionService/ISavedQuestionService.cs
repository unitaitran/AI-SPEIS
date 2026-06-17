
using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Services.SavedQuestionService
{
    public interface ISavedQuestionService
    {
        Task<IEnumerable<SavedQuestionDto>> GetSavedQuestionsAsync(int userId);
        Task<SavedQuestionDto?> SaveQuestionAsync(int userId, int questionId);
        Task<bool> UnsaveQuestionAsync(int userId, int questionId);
    }
}
