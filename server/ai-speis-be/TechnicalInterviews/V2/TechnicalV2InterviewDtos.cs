using System.ComponentModel.DataAnnotations;

namespace ai_speis_be.TechnicalInterviews.V2
{
    public sealed class InitializeTechnicalV2Request
    {
        [MaxLength(50)]
        public List<string>? RequiredSkills { get; set; }
    }

    public sealed class SubmitTechnicalV2AnswerRequest
    {
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

    public sealed class TechnicalV2SessionDto
    {
        public int SessionId { get; set; }
        public string RuntimeVersion { get; set; } = "V2";
        public string JobRole { get; set; } = string.Empty;
        public string ExperienceLevel { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public List<string> RequiredSkills { get; set; } = new();
        public int TargetMainQuestionCount { get; set; }
        public int CompletedMainQuestionCount { get; set; }
        public string SessionStatus { get; set; } = string.Empty;
        public string QuestionSetStatus { get; set; } = string.Empty;
        public string EvaluationStatus { get; set; } = string.Empty;
        public string? RecoverableError { get; set; }
        public bool IsComplete { get; set; }
        public TechnicalV2CurrentQuestionDto? CurrentQuestion { get; set; }
        public List<TechnicalV2TranscriptEntryDto> Transcript { get; set; } = new();
    }

    public sealed class TechnicalV2CurrentQuestionDto
    {
        public int SessionQuestionId { get; set; }
        public int QuestionId { get; set; }
        public int? ParentSessionQuestionId { get; set; }
        public string QuestionType { get; set; } = string.Empty;
        public int QuestionOrder { get; set; }
        public int MainQuestionIndex { get; set; }
        public int TotalMainQuestions { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? Skill { get; set; }
        public string? Subskill { get; set; }
        public string? Difficulty { get; set; }
        public int TimeLimitSeconds { get; set; } = 120;
        public string Status { get; set; } = string.Empty;
        public string EvaluationStatus { get; set; } = "NOT_STARTED";
        public DateTime? AskedAt { get; set; }
        public DateTime? AnsweredAt { get; set; }
    }

    public sealed class TechnicalV2TranscriptEntryDto
    {
        public int SessionQuestionId { get; set; }
        public int? ParentSessionQuestionId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string QuestionType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public sealed class TechnicalV2SubmitAnswerResponseDto
    {
        public int SessionQuestionId { get; set; }
        public string EvaluationStatus { get; set; } = string.Empty;
        public bool FallbackUsed { get; set; }
        public string Decision { get; set; } = "NEXT_QUESTION";
        public TechnicalV2CurrentQuestionDto? NextQuestion { get; set; }
        public TechnicalV2SessionDto State { get; set; } = new();
    }

    public sealed class TechnicalV2ResultDto
    {
        public int SessionId { get; set; }
        public string RubricVersion { get; set; } = string.Empty;
        public string ScoringPolicyVersion { get; set; } = string.Empty;
        public decimal OverallScore { get; set; }
        public string PerformanceBand { get; set; } = string.Empty;
        public string FinalFeedbackStatus { get; set; } = string.Empty;
        public List<TechnicalV2QuestionResultDto> MainQuestions { get; set; } = new();
        public TechnicalV2SummaryDto Summary { get; set; } = new();
    }

    public sealed class TechnicalV2QuestionResultDto
    {
        public int SessionQuestionId { get; set; }
        public int QuestionId { get; set; }
        public int QuestionOrder { get; set; }
        public string Question { get; set; } = string.Empty;
        public string? AnswerTranscript { get; set; }
        public string? Skill { get; set; }
        public decimal Score { get; set; }
        public string EvaluationStatus { get; set; } = string.Empty;
        public int? ParentSessionQuestionId { get; set; }
        public List<TechnicalV2DimensionResultDto> Dimensions { get; set; } = new();
        public List<TechnicalV2QuestionResultDto> SubQuestions { get; set; } = new();
        public List<string> Strengths { get; set; } = new();
        public List<string> MissingPoints { get; set; } = new();
    }

    public sealed class TechnicalV2DimensionResultDto
    {
        public string RubricCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public decimal Weight { get; set; }
        public decimal WeightedScore { get; set; }
        public List<string> Evidence { get; set; } = new();
        public List<string> Strengths { get; set; } = new();
        public List<string> Gaps { get; set; } = new();
        public List<string> MissingEvidence { get; set; } = new();
    }

    public sealed class TechnicalV2SummaryDto
    {
        public string OverallTechnicalAssessment { get; set; } = string.Empty;
        public string ExecutiveSummary { get; set; } = string.Empty;
        public List<string> Strengths { get; set; } = new();
        public List<string> KnowledgeGaps { get; set; } = new();
        public string LevelAssessment { get; set; } = string.Empty;
        public List<string> RecommendationsForImprovement { get; set; } = new();
        public decimal FinalTechnicalScore { get; set; }
    }

    public enum TechnicalV2OperationStatus
    {
        Ok,
        Created,
        BadRequest,
        NotFound,
        Conflict,
        ExternalFailure
    }

    public sealed record TechnicalV2OperationResult<T>(
        TechnicalV2OperationStatus Status,
        T? Value = default,
        string? ErrorCode = null,
        string? Message = null)
    {
        public static TechnicalV2OperationResult<T> Ok(T value) => new(TechnicalV2OperationStatus.Ok, value);
        public static TechnicalV2OperationResult<T> Created(T value) => new(TechnicalV2OperationStatus.Created, value);
        public static TechnicalV2OperationResult<T> Failure(TechnicalV2OperationStatus status, string code, string message) => new(status, default, code, message);
    }
}
