namespace ai_speis_be.Models.Enums
{
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

    public enum TechnicalMatchBand
    {
        Low,
        Medium,
        High
    }

    public enum TechnicalPerformanceBandCode
    {
        EXCELLENT,
        VERY_GOOD,
        GOOD,
        MINIMUM_REQUIREMENT_MET,
        WEAK,
        VERY_WEAK
    }

    public enum TechnicalQuestionSourceType
    {
        CV,
        JD
    }

    public enum TechnicalEvaluationObjective
    {
        CvSkillVerification,
        CvProjectApplication,
        JdCoreKnowledge,
        JdDepthAndTradeOff,
        JdOptimization,
        JdRealWorldApplication
    }

    public enum TechnicalAdaptiveStage
    {
        MainQuestion,
        AwaitingClarification,
        AwaitingFollowUp,
        AwaitingReliabilityFollowUp,
        Finalized
    }

}
