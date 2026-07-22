namespace ai_speis_be.Models.Enums
{
    public enum BehaviourSelectionSource
    {
        ExternalAi,
        RuleBasedFallback
    }

    public enum BehaviourQuestionSetStatus
    {
        Active,
        Completed,
        Failed,
        Cancelled
    }

    public enum BehaviourQuestionType
    {
        Main,
        Clarification,
        FollowUp1,
        FollowUp2
    }

    public enum BehaviourQuestionStatus
    {
        Pending,
        Asked,
        Answered,
        Skipped
    }

    public enum BehaviourAnswerQuality
    {
        Excellent,
        Good,
        Partial,
        Vague,
        Insufficient
    }

    public enum BehaviourResolvedAction
    {
        NextMainQuestion,
        Clarification,
        FollowUp1,
        FollowUp2
    }

    public enum RoundFeedbackStatus
    {
        NotStarted,
        Processing,
        Completed,
        Failed
    }
}
