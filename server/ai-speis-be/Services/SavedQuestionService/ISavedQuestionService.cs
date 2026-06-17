
using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Services.SavedQuestionService
{
    public interface ISavedQuestionService
    {
        Task<IEnumerable<SavedQuestionDto>> GetSavedQuestionsAsync(int userId);
    }
}
