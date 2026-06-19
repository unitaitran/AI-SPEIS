using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Services.QuestionService
{
    public interface IQuestionService
    {
        Task<PagedResultDto<AdminQuestionListItemDto>> GetAdminQuestionsAsync(
            AdminQuestionQueryDto query,
            CancellationToken cancellationToken = default);
        Task<QuestionOperationResult> CreateAdminQuestionAsync(
            AdminQuestionCreateRequestDto request,
            int actingUserId,
            CancellationToken cancellationToken = default);
        Task<QuestionOperationResult> UpdateAdminQuestionAsync(
            int questionId,
            AdminQuestionUpdateRequestDto request,
            int actingUserId,
            CancellationToken cancellationToken = default);
        Task<QuestionOperationResult> SoftDeleteAdminQuestionAsync(
            int questionId,
            int actingUserId,
            CancellationToken cancellationToken = default);
        Task<IEnumerable<QuestionResponseDto>> GetQuestionsAsync(string? roleTarget, string? major, string? difficulty);
        Task<QuestionResponseDto?> GetQuestionByIdAsync(int questionId);
        Task<QuestionResponseDto?> GetQuestionByIdAdminAsync(int questionId);

    }
}
