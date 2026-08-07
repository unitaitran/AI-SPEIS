namespace ai_speis_be.TechnicalInterviews.DTOs
{
    /// <summary>
    /// Trạng thái của tiến trình tạo trước câu hỏi Technical chạy ngầm.
    /// </summary>
    public enum TechnicalPreGenerationStatus
    {
        /// <summary>Chưa bắt đầu tạo.</summary>
        Idle,
        /// <summary>Đang tạo câu hỏi ngầm.</summary>
        Generating,
        /// <summary>Đã tạo xong và lưu vào DB.</summary>
        Completed,
        /// <summary>Đã xảy ra lỗi sau khi thử lại.</summary>
        Failed
    }

    /// <summary>
    /// DTO trả về trạng thái tiến trình tạo trước câu hỏi Technical.
    /// </summary>
    public sealed class TechnicalPreGenerationStatusDto
    {
        public TechnicalPreGenerationStatus Status { get; set; } = TechnicalPreGenerationStatus.Idle;
        public int TechnicalSessionId { get; set; }
        public string? ErrorMessage { get; set; }
        public int RetryCount { get; set; }
    }
}
