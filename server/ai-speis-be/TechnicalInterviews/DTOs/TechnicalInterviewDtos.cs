using System.ComponentModel.DataAnnotations;

namespace ai_speis_be.TechnicalInterviews.DTOs
{
    public sealed class InitializeTechnicalInterviewRequest
    {
        [Range(1, int.MaxValue)]
        public int InterviewSessionId { get; set; }

        [MaxLength(50)]
        public List<string>? SelectedSkills { get; set; }
    }

    public sealed class SubmitTechnicalAnswerRequest
    {
        public Guid AttemptId { get; set; }

        [Required]
        [MinLength(1)]
        public string Transcript { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? AudioId { get; set; }
    }

    public sealed class TechnicalInterviewSessionDto
    {
        public int SessionId { get; set; }
        public string JobRole { get; set; } = string.Empty;
        public string ExperienceLevel { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public List<string> SelectedSkills { get; set; } = new();
        public int TargetMainQuestionCount { get; set; }
        public int CompletedMainQuestionCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string AiProvider { get; set; } = string.Empty;
        public string RubricVersion { get; set; } = string.Empty;
        public string ScoringPolicyVersion { get; set; } = string.Empty;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public decimal? FinalScore { get; set; }
        public string? PerformanceBand { get; set; }
        public int? MatchScore { get; set; }
        public string? MatchBand { get; set; }
        public string? QuestionPlanVersion { get; set; }
        public string? AdaptiveRuleVersion { get; set; }
        public List<TechnicalLockedMainQuestionDto> LockedMainQuestions { get; set; } = new();
        public string? AdaptiveStage { get; set; }
        public string? RecoverableFailureReason { get; set; }
        public int? MainQuestionIndex { get; set; }
        public int TotalMainQuestions { get; set; }
        public string? QuestionType { get; set; }
        public int? SubQuestionIndex { get; set; }
        public int RequiredFollowUpCount { get; set; }
        public int CompletedFollowUpCount { get; set; }
        public string ProcessingStatus { get; set; } = string.Empty;
        public TechnicalProcessingStatusDto? ProcessingStatuses { get; set; }
        public string SessionStatus { get; set; } = string.Empty;
        public List<TechnicalTranscriptEntryDto> Transcript { get; set; } = new();
    }

    public sealed class TechnicalLockedMainQuestionDto
    {
        public int MainQuestionIndex { get; set; }
        public int SelectedQuestionId { get; set; }
        public string Skill { get; set; } = string.Empty;
        public string? Subskill { get; set; }
        public string Difficulty { get; set; } = string.Empty;
        public string SourceType { get; set; } = string.Empty;
        public string EvaluationObjective { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string QuestionPlanVersion { get; set; } = string.Empty;
        public string? QuestionBankVersion { get; set; }
        public DateTime LockedAt { get; set; }
    }

    public sealed class TechnicalTranscriptEntryDto
    {
        public string Id { get; set; } = string.Empty;
        public Guid AttemptId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string QuestionType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public sealed class TechnicalCurrentQuestionDto
    {
        public Guid AttemptId { get; set; }
        public int? QuestionId { get; set; }
        public int? SelectedQuestionId { get; set; }
        public TechnicalLockedMainQuestionDto? LockedQuestionSnapshot { get; set; }
        public string QuestionType { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? Skill { get; set; }
        public string? Difficulty { get; set; }
        public int MainQuestionIndex { get; set; }
        public int TotalMainQuestions { get; set; }
        public string SessionStatus { get; set; } = string.Empty;
        public int? SubQuestionIndex { get; set; }
        public int RequiredFollowUpCount { get; set; }
        public int CompletedFollowUpCount { get; set; }
        public int RequiredSubQuestionCount { get; set; }
        public string ProcessingStatus { get; set; } = string.Empty;
    }

    public sealed class TechnicalSubmitAnswerResponseDto
    {
        public Guid AttemptId { get; set; }
        public TechnicalProcessingStatusDto Processing { get; set; } = new();
        public TechnicalEvaluationDecisionDto Evaluation { get; set; } = new();
        public TechnicalCurrentQuestionDto? NextQuestion { get; set; }
        public string SessionStatus { get; set; } = string.Empty;
        public TechnicalFallbackStatusDto Fallbacks { get; set; } = new();
        public string ResolvedAction { get; set; } = string.Empty;
        public string? AiSuggestedAction { get; set; }
        public string BackendResolvedAction { get; set; } = string.Empty;
        public string? OverrideReason { get; set; }
        public string? AdaptiveStage { get; set; }
        public bool FallbackUsed { get; set; }
        public TechnicalInterviewProgressDto Progress { get; set; } = new();
    }

    public sealed class TechnicalInterviewProgressDto
    {
        public int MainQuestionIndex { get; set; }
        public int TotalMainQuestions { get; set; }
        public int? SubQuestionIndex { get; set; }
        public int RequiredSubQuestionCount { get; set; }
        public int RequiredFollowUpCount { get; set; }
        public int CompletedFollowUpCount { get; set; }
    }

    public sealed class TechnicalProcessingStatusDto
    {
        public string Evaluation { get; set; } = string.Empty;
        // Retained for old clients; this is now a derived validation/fallback status,
        // not a separate AI task.
        public string QuestionGeneration { get; set; } = string.Empty;
    }

    public sealed class TechnicalFallbackStatusDto
    {
        public bool EvaluationFallbackUsed { get; set; }
        public bool QuestionFallbackUsed { get; set; }
    }

    public sealed class TechnicalEvaluationDecisionDto
    {
        public string Decision { get; set; } = string.Empty;
    }

    public sealed class TechnicalInterviewResultDto
    {
        public int SessionId { get; set; }
        public string RubricVersion { get; set; } = string.Empty;
        public string ScoringPolicyVersion { get; set; } = string.Empty;
        public decimal OverallScore { get; set; }
        public decimal TechnicalScore { get; set; }
        public decimal MaxScore { get; set; } = 10m;
        public string PerformanceBand { get; set; } = string.Empty;
        public string FinalFeedbackStatus { get; set; } = string.Empty;
        public List<TechnicalMainQuestionResultDto> MainQuestions { get; set; } = new();
        public List<TechnicalMainQuestionResultDto> MainQuestionResults { get; set; } = new();
        public List<TechnicalSkillResultDto> SkillScores { get; set; } = new();
        public TechnicalFinalSummaryDto Summary { get; set; } = new();
        public List<string> Strengths { get; set; } = new();
        public List<string> Weaknesses { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public sealed class TechnicalMainQuestionResultDto
    {
        public Guid AttemptId { get; set; }
        public int? QuestionId { get; set; }
        public int MainQuestionIndex { get; set; }
        public string QuestionType { get; set; } = "MAIN";
        public string Question { get; set; } = string.Empty;
        public string? AnswerTranscript { get; set; }
        public string Skill { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public decimal InitialMainScore { get; set; }
        public decimal FinalMainScore { get; set; }
        public decimal CumulativeFollowUpBonus { get; set; }
        public string? SourceType { get; set; }
        public string? TargetSkill { get; set; }
        public string? EvaluationObjective { get; set; }
        public bool PlanDeviation { get; set; }
        public string? PlanDeviationReason { get; set; }
        public List<TechnicalDimensionResultDto> Dimensions { get; set; } = new();
        public List<string> Strengths { get; set; } = new();
        public List<string> MissingPoints { get; set; } = new();
        public List<string> IncorrectClaims { get; set; } = new();
        public List<string> ImprovementSuggestions { get; set; } = new();
        public string FeedbackSummary { get; set; } = string.Empty;
        public List<TechnicalSubQuestionResultDto> AdaptiveHistory { get; set; } = new();
    }

    public sealed class TechnicalSubQuestionResultDto
    {
        public Guid AttemptId { get; set; }
        public string QuestionType { get; set; } = string.Empty;
        public int SequenceWithinMain { get; set; }
        public string Question { get; set; } = string.Empty;
        public string? AnswerTranscript { get; set; }
        public decimal? RawScore { get; set; }
        public decimal? FollowUpBonus { get; set; }
        public string? GenerationReason { get; set; }
    }

    public sealed class TechnicalDimensionResultDto
    {
        public string RubricCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public decimal Weight { get; set; }
        public decimal WeightedScore { get; set; }
        public string Level { get; set; } = string.Empty;
        public List<string> Evidence { get; set; } = new();
        public List<string> MissingEvidence { get; set; } = new();
        public string ReasonSummary { get; set; } = string.Empty;
        public List<string> IncorrectClaims { get; set; } = new();
    }

    public sealed class TechnicalSkillResultDto
    {
        public string Skill { get; set; } = string.Empty;
        public int MainQuestionCount { get; set; }
        public decimal Score { get; set; }
    }

    public sealed class TechnicalFinalSummaryDto
    {
        public string OverallTechnicalAssessment { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<string> Strengths { get; set; } = new();
        public List<string> KnowledgeGaps { get; set; } = new();
        public List<string> AreasForImprovement { get; set; } = new();
        public string ReasoningAndApplicationAssessment { get; set; } = string.Empty;
        public string CommunicationAssessment { get; set; } = string.Empty;
        public List<TechnicalSkillFeedbackDto> PerformanceBySkill { get; set; } = new();
        public List<string> RecommendationsForImprovement { get; set; } = new();
        public List<string> RecommendedNextSteps { get; set; } = new();
        public decimal FinalTechnicalScore { get; set; }
    }

    public sealed class TechnicalSkillFeedbackDto
    {
        public string Skill { get; set; } = string.Empty;
        public string Assessment { get; set; } = string.Empty;
    }

    public enum TechnicalOperationStatus
    {
        Ok,
        Created,
        BadRequest,
        NotFound,
        Conflict,
        ExternalFailure
    }

    public sealed record TechnicalOperationResult<T>(
        TechnicalOperationStatus Status,
        T? Value = default,
        string? ErrorCode = null,
        string? Message = null)
    {
        public static TechnicalOperationResult<T> Ok(T value) => new(TechnicalOperationStatus.Ok, value);
        public static TechnicalOperationResult<T> Created(T value) => new(TechnicalOperationStatus.Created, value);
        public static TechnicalOperationResult<T> Failure(
            TechnicalOperationStatus status,
            string errorCode,
            string message) => new(status, default, errorCode, message);
    }
}
