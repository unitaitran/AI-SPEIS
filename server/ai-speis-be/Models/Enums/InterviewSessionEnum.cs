namespace ai_speis_be.Models.Enums
{
    public enum InterviewCampaignStatus
    {
        Pending,
        Active,
        Completed,
        Cancelled,
        Expired
    }

    public enum InterviewSessionStatus
    {
        Pending,       // Mới setup
        Active,        // Đang diễn ra
        Completed,     // Đã hoàn thành
        Cancelled      // Đã hủy bỏ
    }

    public enum InterviewRoundType
    {
        Technical,     // Phỏng vấn lý thuyết kỹ thuật (mặc định 5 câu)
        Code,          // Phỏng vấn thực hành code (mặc định 10 câu)
        Behavior       // Phỏng vấn hành vi (mặc định 5 câu)
    }

    public enum InterviewMode
    {
        Practice,      // Chế độ Luyện tập
        RealTest       // Chế độ Thực chiến
    }
}
