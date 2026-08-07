using ai_speis_be.TechnicalInterviews.DTOs;

namespace ai_speis_be.TechnicalInterviews.PreGeneration
{
    /// <summary>
    /// Service quản lý việc tạo trước câu hỏi Technical chạy ngầm (background).
    /// Được đăng ký Singleton vì quản lý trạng thái in-memory cho các session.
    /// </summary>
    public interface ITechnicalPreGenerationService
    {
        /// <summary>
        /// Kích hoạt tiến trình tạo trước câu hỏi Technical chạy ngầm.
        /// Nếu session đã được tạo hoặc đang tạo, trả về trạng thái hiện tại.
        /// </summary>
        Task<TechnicalPreGenerationStatusDto> PreGenerateAsync(
            int userId,
            int technicalSessionId,
            CancellationToken cancellationToken);

        /// <summary>
        /// Lấy trạng thái hiện tại của tiến trình tạo trước.
        /// </summary>
        TechnicalPreGenerationStatusDto GetStatus(int technicalSessionId);

        /// <summary>
        /// Hủy tiến trình tạo trước nếu đang chạy.
        /// </summary>
        void CancelPreGeneration(int technicalSessionId);
    }
}
