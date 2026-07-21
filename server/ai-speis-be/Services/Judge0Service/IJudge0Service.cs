using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Services.Judge0Service
{
    public interface IJudge0Service
    {
        /// <summary>
        /// Gửi batch submissions đến Judge0 và đợi kết quả (synchronous).
        /// Sử dụng endpoint POST /submissions/batch?wait=true.
        /// </summary>
        Task<List<Judge0SubmissionResponse>> SubmitBatchAsync(
            List<Judge0SubmissionRequest> submissions,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy danh sách ngôn ngữ lập trình mà Judge0 hỗ trợ.
        /// </summary>
        Task<List<Judge0LanguageDto>> GetLanguagesAsync(
            CancellationToken cancellationToken = default);
    }
}
