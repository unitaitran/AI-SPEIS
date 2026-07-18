using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Services.CodingService
{
    public interface ICodingService
    {
        /// <summary>
        /// Submit code để chạy qua Judge0, so sánh kết quả với test cases.
        /// </summary>
        Task<(bool Success, string? ErrorMessage, SubmissionResponseDto? Data)> SubmitCodeAsync(
            int userId,
            SubmitCodeRequestDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy danh sách câu hỏi coding của 1 session (chỉ trả sample test cases).
        /// </summary>
        Task<(bool Success, string? ErrorMessage, List<CodingQuestionResponseDto>? Data)> GetCodingQuestionsAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy chi tiết 1 submission.
        /// </summary>
        Task<(bool Success, string? ErrorMessage, SubmissionResponseDto? Data)> GetSubmissionAsync(
            int userId,
            int submissionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy lịch sử submissions của 1 câu hỏi trong 1 session.
        /// </summary>
        Task<(bool Success, string? ErrorMessage, List<SubmissionSummaryDto>? Data)> GetSubmissionHistoryAsync(
            int userId,
            int sessionId,
            int questionId,
            CancellationToken cancellationToken = default);
    }
}
