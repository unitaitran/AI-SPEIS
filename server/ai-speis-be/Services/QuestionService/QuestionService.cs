using ai_speis_be.Models.DTOs;
using ai_speis_be.Models;
using ai_speis_be.Repositories.QuestionRepo;

namespace ai_speis_be.Services.QuestionService
{
    public class QuestionService : IQuestionService
    {
        private readonly IQuestionRepoitory _repository;
        public QuestionService (IQuestionRepoitory repository)
        {
            _repository = repository;
        }

        public async Task<PagedResultDto<AdminQuestionListItemDto>> GetAdminQuestionsAsync(
            AdminQuestionQueryDto query,
            CancellationToken cancellationToken = default)
        {
            var questions = await _repository.GetAdminQuestionsAsync(
                query,
                cancellationToken);

            return new PagedResultDto<AdminQuestionListItemDto>
            {
                Items = questions.Items.Select(MapToAdminListItemDto).ToList(),
                PageNumber = questions.PageNumber,
                PageSize = questions.PageSize,
                TotalItems = questions.TotalItems
            };
        }

        public async Task<QuestionResponseDto?> GetQuestionByIdAdminAsync(int questionId)
        {
            var question =await _repository.GetQuestionByIdAdminAsync(questionId);
            return question != null ? MapToDto(question) : null; 
        }

        public async Task<QuestionResponseDto?> GetQuestionByIdAsync(int questionId)
        {
            var question = await _repository.GetQuestionByIdAsync(questionId);
            return question != null ? MapToDto(question) : null;
        }

        public async Task<IEnumerable<QuestionResponseDto>> GetQuestionsAsync(string? roleTarget, string? major, string? difficulty)
        {
            var questions = await _repository.GetQuestionsAsync(roleTarget, major, difficulty);
            return questions.Select(MapToDto);
        }
        private QuestionResponseDto MapToDto(Question question)
        {
            return new QuestionResponseDto
            {
                QuestionId = question.QuestionId,
                UserId = question.UserId,
                QuestionContent = question.QuestionContent,
                SuggestedAnswer = question.SuggestedAnswer,
                Difficulty = question.Difficulty,
                RoleTarget = question.RoleTarget,
                Major = question.Major,
                IsDeleted = question.IsDeleted,
                CreatedAt = question.CreatedAt,
                UpdatedAt = question.UpdatedAt
            };
        }

        private AdminQuestionListItemDto MapToAdminListItemDto(Question question)
        {
            return new AdminQuestionListItemDto
            {
                QuestionId = question.QuestionId,
                UserId = question.UserId,
                QuestionContent = question.QuestionContent,
                SuggestedAnswer = question.SuggestedAnswer,
                Difficulty = question.Difficulty,
                RoleTarget = question.RoleTarget,
                Major = question.Major,
                IsDeleted = question.IsDeleted,
                CreatedAt = question.CreatedAt,
                UpdatedAt = question.UpdatedAt
            };
        }
    }
}
