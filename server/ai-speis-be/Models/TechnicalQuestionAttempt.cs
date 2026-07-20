using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Models
{
    [Table("TechnicalQuestionAttempt")]
    [Index(nameof(InterviewSessionId), nameof(SequenceNumber), IsUnique = true)]
    public class TechnicalQuestionAttempt
    {
        [Key]
        public Guid AttemptId { get; set; } = Guid.NewGuid();

        [ForeignKey(nameof(InterviewSession))]
        public int InterviewSessionId { get; set; }

        [ForeignKey(nameof(Question))]
        public int? QuestionId { get; set; }

        [ForeignKey(nameof(ParentAttempt))]
        public Guid? ParentAttemptId { get; set; }

        public Guid RootMainAttemptId { get; set; }

        public TechnicalAttemptType QuestionType { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string QuestionContentSnapshot { get; set; } = string.Empty;

        public int SequenceNumber { get; set; }

        public int MainQuestionIndex { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? AnswerTranscript { get; set; }

        [MaxLength(200)]
        public string? AudioId { get; set; }

        [MaxLength(128)]
        public string? SubmissionIdempotencyKey { get; set; }

        [MaxLength(200)]
        public string? SkillSnapshot { get; set; }

        [MaxLength(200)]
        public string? SubskillSnapshot { get; set; }

        public QuestionDifficultyEnum? DifficultySnapshot { get; set; }

        public TechnicalAttemptStatus Status { get; set; } = TechnicalAttemptStatus.Ready;

        public TechnicalAITaskStatus EvaluationTaskStatus { get; set; } = TechnicalAITaskStatus.NotStarted;

        public TechnicalAITaskStatus FeedbackTaskStatus { get; set; } = TechnicalAITaskStatus.NotStarted;

        public TechnicalAITaskStatus QuestionGenerationTaskStatus { get; set; } = TechnicalAITaskStatus.NotStarted;

        public bool EvaluationFallbackUsed { get; set; }

        public bool FeedbackFallbackUsed { get; set; }

        public bool QuestionFallbackUsed { get; set; }

        public long? TotalProcessingLatencyMs { get; set; }

        public long? CriticalPathLatencyMs { get; set; }

        public long? SequentialEstimatedLatencyMs { get; set; }

        public long? ParallelLatencySavingMs { get; set; }

        public DateTime? ProcessingStartedAt { get; set; }

        public DateTime? ProcessingCompletedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? AnsweredAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public virtual InterviewSession InterviewSession { get; set; } = null!;

        public virtual Question? Question { get; set; }

        public virtual TechnicalQuestionAttempt? ParentAttempt { get; set; }

        public virtual ICollection<TechnicalQuestionAttempt> ChildAttempts { get; set; } = new List<TechnicalQuestionAttempt>();

        public virtual ICollection<TechnicalAnswerEvaluation> Evaluations { get; set; } = new List<TechnicalAnswerEvaluation>();
    }
}
