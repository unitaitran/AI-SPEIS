using ai_speis_be.Models.DTOs;

using Microsoft.AspNetCore.Http;

namespace ai_speis_be.Services.QuestionService
{
    public interface IQuestionService
    {
        Task<PagedResultDto<AdminQuestionListItemDto>> GetAdminQuestionsAsync(
            AdminQuestionQueryDto query,
            CancellationToken cancellationToken = default);
        Task<QuestionFiltersDto> GetQuestionFiltersAsync(CancellationToken cancellationToken = default);
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
        Task<QuestionOperationResult> RestoreAdminQuestionAsync(
            int questionId,
            CancellationToken cancellationToken = default);
        Task<QuestionOperationResult> RequestAdminQuestionPurgeAsync(
            int questionId,
            int actingUserId,
            CancellationToken cancellationToken = default);
        Task<PagedResultDto<AdminQuestionListItemDto>> GetDeletedAdminQuestionsAsync(
            AdminQuestionQueryDto query,
            CancellationToken cancellationToken = default);
        Task<QuestionImportOperationResult> ImportAdminQuestionsAsync(
            IFormFile? file,
            int actingUserId,
            CancellationToken cancellationToken = default);
        Task<PagedResultDto<QuestionResponseDto>> GetQuestionsAsync(
            UserQuestionQueryDto query,
            CancellationToken cancellationToken = default);
        Task<QuestionResponseDto?> GetQuestionByIdAsync(int questionId);
        Task<AdminQuestionListItemDto?> GetQuestionByIdAdminAsync(int questionId);
        Task<int> ReindexAllVectorsAsync(CancellationToken cancellationToken = default);
    }
}
