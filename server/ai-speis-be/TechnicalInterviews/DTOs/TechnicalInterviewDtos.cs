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
    }

    public sealed class TechnicalCurrentQuestionDto
    {
        public Guid AttemptId { get; set; }
        public int? QuestionId { get; set; }
        public string QuestionType { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? Skill { get; set; }
        public string? Difficulty { get; set; }
        public int MainQuestionIndex { get; set; }
        public int TotalMainQuestions { get; set; }
        public string SessionStatus { get; set; } = string.Empty;
    }

    public sealed class TechnicalSubmitAnswerResponseDto
    {
        public Guid AttemptId { get; set; }
        public TechnicalProcessingStatusDto Processing { get; set; } = new();
        public TechnicalEvaluationDecisionDto Evaluation { get; set; } = new();
        public TechnicalFeedbackAcknowledgementDto Feedback { get; set; } = new();
        public TechnicalCurrentQuestionDto? NextQuestion { get; set; }
        public string SessionStatus { get; set; } = string.Empty;
        public TechnicalFallbackStatusDto Fallbacks { get; set; } = new();
    }

    public sealed class TechnicalProcessingStatusDto
    {
        public string Evaluation { get; set; } = string.Empty;
        public string Feedback { get; set; } = string.Empty;
        public string QuestionGeneration { get; set; } = string.Empty;
    }

    public sealed class TechnicalFeedbackAcknowledgementDto
    {
        public string Status { get; set; } = string.Empty;
        public bool AvailableInResult { get; set; } = true;
    }

    public sealed class TechnicalFallbackStatusDto
    {
        public bool EvaluationFallbackUsed { get; set; }
        public bool FeedbackFallbackUsed { get; set; }
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
        public string PerformanceBand { get; set; } = string.Empty;
        public List<TechnicalMainQuestionResultDto> MainQuestions { get; set; } = new();
        public List<TechnicalSkillResultDto> SkillScores { get; set; } = new();
        public TechnicalFinalSummaryDto Summary { get; set; } = new();
    }

    public sealed class TechnicalMainQuestionResultDto
    {
        public Guid AttemptId { get; set; }
        public int? QuestionId { get; set; }
        public int MainQuestionIndex { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Skill { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public List<TechnicalDimensionResultDto> Dimensions { get; set; } = new();
        public List<string> Strengths { get; set; } = new();
        public List<string> MissingPoints { get; set; } = new();
        public List<string> IncorrectClaims { get; set; } = new();
        public List<string> ImprovementSuggestions { get; set; } = new();
        public string FeedbackSummary { get; set; } = string.Empty;
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
    }

    public sealed class TechnicalSkillResultDto
    {
        public string Skill { get; set; } = string.Empty;
        public int MainQuestionCount { get; set; }
        public decimal Score { get; set; }
    }

    public sealed class TechnicalFinalSummaryDto
    {
        public string Summary { get; set; } = string.Empty;
        public List<string> Strengths { get; set; } = new();
        public List<string> AreasForImprovement { get; set; } = new();
        public List<string> RecommendedNextSteps { get; set; } = new();
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
