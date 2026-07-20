using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Models
{
    [Table("AIInteractionLog")]
    [Index(nameof(InterviewSessionId), nameof(CreatedAt))]
    public class AIInteractionLog
    {
        [Key]
        public Guid AIInteractionLogId { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(50)]
        public string Provider { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string Model { get; set; } = string.Empty;

        public AIInteractionOperationType OperationType { get; set; }

        [Required]
        [MaxLength(80)]
        public string PromptVersion { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string RubricVersion { get; set; } = string.Empty;

        public long LatencyMs { get; set; }

        public int RetryCount { get; set; }

        public int? InputTokenCount { get; set; }

        public int? OutputTokenCount { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal? EstimatedCost { get; set; }

        public AIInteractionStatus Status { get; set; }

        [MaxLength(100)]
        public string? ErrorCode { get; set; }

        public bool FallbackUsed { get; set; }

        [ForeignKey(nameof(InterviewSession))]
        public int InterviewSessionId { get; set; }

        [ForeignKey(nameof(Attempt))]
        public Guid? AttemptId { get; set; }

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual InterviewSession InterviewSession { get; set; } = null!;

        public virtual TechnicalQuestionAttempt? Attempt { get; set; }
    }
}
