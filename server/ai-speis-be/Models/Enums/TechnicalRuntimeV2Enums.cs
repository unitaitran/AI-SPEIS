namespace ai_speis_be.Models.Enums
{
    public enum TechnicalQuestionSetSelectionSource
    {
        ExternalAi,
        DeterministicFallback
    }

    public enum TechnicalQuestionSetStatus
    {
        Active,
        Completed,
        Failed
    }

    public enum TechnicalSessionQuestionType
    {
        Main,
        Clarification,
        FollowUp
    }

    public enum TechnicalSessionQuestionStatus
    {
        Pending,
        Asked,
        Answered,
        Evaluated,
        Skipped
    }

    public enum TechnicalAnswerEvaluationStatus
    {
        Processing,
        Completed,
        Partial,
        Fallback,
        Failed
    }
}
