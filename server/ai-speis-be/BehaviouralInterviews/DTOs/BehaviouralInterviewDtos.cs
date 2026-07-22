using System.ComponentModel.DataAnnotations;

namespace ai_speis_be.BehaviouralInterviews.DTOs
{
    public sealed class InitializeBehaviouralInterviewRequest
    {
        [Range(1, int.MaxValue)]
        public int InterviewSessionId { get; set; }

        [MaxLength(50)]
        public List<string>? RequiredSkills { get; set; }
    }

    public sealed class SubmitBehaviouralAnswerRequest
    {
        public int SessionQuestionId { get; set; }

        [Required]
        [MinLength(1)]
        public string Transcript { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? AudioId { get; set; }

        [Range(0, 3600)]
        public int? AnswerDurationSeconds { get; set; }

        [Range(0, 1)]
        public decimal? SttConfidence { get; set; }
    }

    public sealed class BehaviouralInterviewSessionDto
    {
        public int SessionId { get; set; }
        public string JobRole { get; set; } = string.Empty;
        public string ExperienceLevel { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public List<string> RequiredSkills { get; set; } = new();
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
        public List<BehaviouralTranscriptEntryDto> Transcript { get; set; } = new();
    }

    public sealed class BehaviouralTranscriptEntryDto
    {
        public string Id { get; set; } = string.Empty;
        public int SessionQuestionId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string QuestionType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public sealed class BehaviouralCurrentQuestionDto
    {
        public int SessionQuestionId { get; set; }
        public int? QuestionId { get; set; }
        public string QuestionType { get; set; } = string.Empty;
        public int? ParentSessionQuestionId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? Skill { get; set; }
        public string? Difficulty { get; set; }
        public int TimeLimitSeconds { get; set; } = 120;
        public string? Hint { get; set; }
        public int MainQuestionIndex { get; set; }
        public int TotalMainQuestions { get; set; }
        public string SessionStatus { get; set; } = string.Empty;
    }

    public sealed class BehaviouralSubmitAnswerResponseDto
    {
        public int AnswerId { get; set; }
        public int SessionQuestionId { get; set; }
        public decimal? QuestionScore { get; set; }
        public string NextAction { get; set; } = string.Empty;
        public BehaviouralEvaluationDecisionDto Evaluation { get; set; } = new();
        public BehaviouralCurrentQuestionDto? NextQuestion { get; set; }
        public string SessionStatus { get; set; } = string.Empty;
        public string RoundStatus { get; set; } = string.Empty;
    }

    public sealed class BehaviouralEvaluationDecisionDto
    {
        public string Decision { get; set; } = string.Empty;
        public string? AnswerQuality { get; set; }
        public string EvaluationStatus { get; set; } = "COMPLETED";
    }

    public sealed class BehaviouralInterviewResultDto
    {
        public int SessionId { get; set; }
        public string RubricVersion { get; set; } = string.Empty;
        public string ScoringPolicyVersion { get; set; } = string.Empty;
        public decimal OverallScore { get; set; }
        public string PerformanceBand { get; set; } = string.Empty;
        public List<BehaviouralMainQuestionResultDto> MainQuestions { get; set; } = new();
        public BehaviouralFinalSummaryDto Summary { get; set; } = new();
        public string FeedbackStatus { get; set; } = string.Empty;
        public BehaviouralTokenUsageDto TokenUsage { get; set; } = new();
        public DateTime? CompletedAt { get; set; }
    }

    public sealed class BehaviouralTokenUsageDto
    {
        public int EvaluationInputTokens { get; set; }
        public int EvaluationOutputTokens { get; set; }
        public int FeedbackInputTokens { get; set; }
        public int FeedbackOutputTokens { get; set; }
    }

    public sealed class BehaviouralMainQuestionResultDto
    {
        public int SessionQuestionId { get; set; }
        public int? QuestionId { get; set; }
        public int MainQuestionIndex { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Skill { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public List<BehaviouralDimensionResultDto> Dimensions { get; set; } = new();
        public List<string> Strengths { get; set; } = new();
        public List<string> MissingPoints { get; set; } = new();
    }

    public sealed class BehaviouralDimensionResultDto
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

    public sealed class BehaviouralFinalSummaryDto
    {
        public string ExecutiveSummary { get; set; } = string.Empty;
        public List<string> CompetencyStrengths { get; set; } = new();
        public List<string> CompetencyGaps { get; set; } = new();
        public string LevelAssessment { get; set; } = string.Empty;
        public List<string> TopRecommendations { get; set; } = new();
    }

    public enum BehaviouralOperationStatus
    {
        Ok,
        Created,
        BadRequest,
        NotFound,
        Conflict,
        ExternalFailure
    }

    public sealed record BehaviouralOperationResult<T>(
        BehaviouralOperationStatus Status,
        T? Value = default,
        string? ErrorCode = null,
        string? Message = null)
    {
        public static BehaviouralOperationResult<T> Ok(T value) => new(BehaviouralOperationStatus.Ok, value);
        public static BehaviouralOperationResult<T> Created(T value) => new(BehaviouralOperationStatus.Created, value);
        public static BehaviouralOperationResult<T> Failure(
            BehaviouralOperationStatus status,
            string errorCode,
            string message) => new(status, default, errorCode, message);
    }
}
