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

        public async Task<QuestionOperationResult> CreateAdminQuestionAsync(
            AdminQuestionCreateRequestDto request,
            int actingUserId,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var status = request.GetStatus();
            var isDeleted = status == AdminQuestionStatus.Inactive;

            var question = new Question
            {
                UserId = actingUserId,
                QuestionContent = request.GetQuestionContent(),
                SuggestedAnswer = request.GetSuggestedAnswer(),
                Difficulty = request.Difficulty!.Value,
                RoleTarget = request.GetRoleTarget(),
                Major = request.GetMajor(),
                IsDeleted = isDeleted,
                CreatedAt = now,
                UpdatedAt = null,
                DeletedAt = isDeleted ? now : null,
                DeletedBy = isDeleted ? actingUserId : null
            };

            var createdQuestion = await _repository.CreateQuestionAsync(
                question,
                cancellationToken);

            return new QuestionOperationResult(
                QuestionOperationOutcome.Created,
                MapToDto(createdQuestion));
        }

        public async Task<QuestionOperationResult> UpdateAdminQuestionAsync(
            int questionId,
            AdminQuestionUpdateRequestDto request,
            int actingUserId,
            CancellationToken cancellationToken = default)
        {
            var question = await _repository.GetQuestionByIdAdminAsync(
                questionId,
                cancellationToken);

            if (question is null)
            {
                return new QuestionOperationResult(
                    QuestionOperationOutcome.QuestionNotFound);
            }

            if (question.IsDeleted)
            {
                return new QuestionOperationResult(
                    QuestionOperationOutcome.QuestionDeleted);
            }

            ApplyQuestionMutation(question, request, actingUserId);

            await _repository.UpdateQuestionAsync(question, cancellationToken);

            return new QuestionOperationResult(
                QuestionOperationOutcome.Updated,
                MapToDto(question));
        }

        public async Task<QuestionOperationResult> SoftDeleteAdminQuestionAsync(
            int questionId,
            int actingUserId,
            CancellationToken cancellationToken = default)
        {
            var question = await _repository.GetQuestionByIdAdminAsync(
                questionId,
                cancellationToken);

            if (question is null)
            {
                return new QuestionOperationResult(
                    QuestionOperationOutcome.QuestionNotFound);
            }

            if (question.IsDeleted)
            {
                return new QuestionOperationResult(
                    QuestionOperationOutcome.AlreadyDeleted,
                    MapToDto(question));
            }

            var now = DateTime.UtcNow;

            question.IsDeleted = true;
            question.DeletedAt = now;
            question.DeletedBy = actingUserId;
            question.UpdatedAt = now;

            await _repository.UpdateQuestionAsync(question, cancellationToken);

            return new QuestionOperationResult(
                QuestionOperationOutcome.Deleted,
                MapToDto(question));
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

        private static void ApplyQuestionMutation(
            Question question,
            AdminQuestionMutationRequestDto request,
            int actingUserId)
        {
            var now = DateTime.UtcNow;
            var status = request.GetStatus();

            question.QuestionContent = request.GetQuestionContent();
            question.SuggestedAnswer = request.GetSuggestedAnswer();
            question.Difficulty = request.Difficulty!.Value;
            question.RoleTarget = request.GetRoleTarget();
            question.Major = request.GetMajor();
            question.UpdatedAt = now;

            if (status == AdminQuestionStatus.Inactive)
            {
                question.IsDeleted = true;
                question.DeletedAt = now;
                question.DeletedBy = actingUserId;
            }
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
                Status = GetQuestionStatus(question),
                CreatedAt = question.CreatedAt,
                UpdatedAt = question.UpdatedAt,
                DeletedAt = question.DeletedAt,
                DeletedBy = question.DeletedBy
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
                Status = GetQuestionStatus(question),
                CreatedAt = question.CreatedAt,
                UpdatedAt = question.UpdatedAt,
                DeletedAt = question.DeletedAt,
                DeletedBy = question.DeletedBy
            };
        }

        private static string GetQuestionStatus(Question question)
        {
            return question.IsDeleted
                ? AdminQuestionStatus.Inactive.ToString()
                : AdminQuestionStatus.Active.ToString();
        }
    }
}
