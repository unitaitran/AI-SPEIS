
using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Repositories.SavedQuestionRepo;

namespace ai_speis_be.Services.SavedQuestionService
{
    public class SavedQuestionService : ISavedQuestionService
    {
        private readonly ISavedQuestionRepository _repository;
        public SavedQuestionService(ISavedQuestionRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<SavedQuestionDto>> GetSavedQuestionsAsync(int userId)
        {
            var questions = await _repository.GetSavedQuestionsAsync(userId);
            return questions.Select(MapToDto);
        }

        public async Task<SavedQuestionDto?> SaveQuestionAsync(int userId, int questionId)
        {
            var question = await _repository.SaveQuestionAsync(userId, questionId);
            return question != null ? MapToDto(question) : null;
        }

        public async Task<bool> UnsaveQuestionAsync(int userId, int questionId)
        {
            return await _repository.UnsaveQuestionAsync(userId, questionId);
        }

        private SavedQuestionDto MapToDto(SavedQuestion savedQuestion)
        {
            return new SavedQuestionDto
            {
                SavedQuestionId = savedQuestion.SavedQuestionId,
                UserId = savedQuestion.UserId,
                QuestionId = savedQuestion.QuestionId,
                Question = new QuestionResponseDto
                {
                    QuestionId = savedQuestion.Question.QuestionId,
                    UserId = savedQuestion.Question.UserId, // ID của người tạo câu hỏi (Tác giả)
                    QuestionContent = savedQuestion.Question.QuestionContent,
                    SuggestedAnswer = savedQuestion.Question.SuggestedAnswer, // Đã sửa từ 'question' thành 'savedQuestion.Question'
                    Difficulty = savedQuestion.Question.Difficulty,
                    RoleTarget = savedQuestion.Question.RoleTarget,
                    Major = savedQuestion.Question.Major,
                    IsDeleted = savedQuestion.Question.IsDeleted,
                    CreatedAt = savedQuestion.Question.CreatedAt,
                    UpdatedAt = savedQuestion.Question.UpdatedAt
                },
                SavedAt = savedQuestion.SavedAt,

            };
        }
    }
}
