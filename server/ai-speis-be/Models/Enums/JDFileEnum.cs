namespace ai_speis_be.Models.Enums
{
    public enum JDInputType
    {
        Text,
        File
    }
    public enum JDFileStatus
    {
        Pending,              // Uploaded, chờ parse
        Processing,           // Đang gọi AI parse
        ConfirmationRequired, // AI parse xong, chờ user review & confirm
        Confirmed,            // User đã xác nhận dữ liệu trích xuất
        Failed,               // Upload thất bại
        AnalysisFailed,       // AI parse thất bại
        Archived              // CV bị thay thế/xóa (soft delete)
    }
}
