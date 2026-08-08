namespace ai_speis_be.Models.Enums
{
    /// <summary>
    /// Luồng hiển thị chính của phỏng vấn (Dành cho UI & Navigation).
    /// </summary>
    public enum InterviewStage
    {
        Behavior,
        Technical,
        Coding,
        Final
    }

    /// <summary>
    /// Trạng thái chuẩn bị bộ câu hỏi ngầm (Dành cho Pre-generation Validator & Cache).
    /// </summary>
    public enum QuestionPreparationState
    {
        Idle,
        Preparing,
        Ready,
        Failed
    }

    /// <summary>
    /// Trạng thái của Background Worker (Dành cho Background Job Tracking).
    /// </summary>
    public enum BackgroundGenerationState
    {
        Idle,
        Generating,
        Completed,
        Failed
    }
}
