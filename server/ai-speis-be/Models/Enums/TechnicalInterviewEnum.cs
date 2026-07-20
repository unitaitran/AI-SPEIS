namespace ai_speis_be.Models.Enums
{
    public enum TechnicalInterviewState
    {
        Created,
        SelectingQuestion,
        QuestionReady,
        Answering,
        Evaluating,
        Completed,
        Failed
    }

    public enum TechnicalAttemptType
    {
        Main,
        Clarification,
        FollowUp
    }

    public enum TechnicalAttemptStatus
    {
        Ready,
        Evaluating,
        Completed,
        Failed
    }

    public enum TechnicalInterviewDecision
    {
        Clarification,
        FollowUp,
        NextQuestion,
        EndInterview
    }

    public enum AIInteractionOperationType
    {
        QuestionSelection,
        AnswerEvaluation,
        FinalSummary,
        FeedbackGeneration,
        QuestionBundleGeneration
    }

    public enum AIInteractionStatus
    {
        Succeeded,
        Failed,
        Timeout,
        InvalidOutput,
        FallbackUsed
    }

    public enum TechnicalAITaskStatus
    {
        NotStarted,
        Processing,
        Fulfilled,
        Rejected,
        Timeout,
        InvalidOutput,
        FallbackUsed
    }
}
