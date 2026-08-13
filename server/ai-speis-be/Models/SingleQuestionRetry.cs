using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ai_speis_be.Models
{
    [Table("SingleQuestionRetry")]
    public class SingleQuestionRetry
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RetryId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int QuestionId { get; set; }

        public int? OriginalSessionId { get; set; }

        [Required]
        [MaxLength(20)]
        public string RoundType { get; set; } = "Technical";

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string QuestionSnapshot { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Skill { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? Transcript { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? Score { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? AiCriteriaDetailJson { get; set; }

        public string? AiStrengths { get; set; }

        public string? AiMissingPoints { get; set; }

        [MaxLength(30)]
        public string EvaluationStatus { get; set; } = "NOT_STARTED";

        [MaxLength(120)]
        public string? EvaluationModel { get; set; }

        public int? EvaluationInputTokens { get; set; }

        public int? EvaluationOutputTokens { get; set; }

        public long? EvaluationLatencyMs { get; set; }

        [MaxLength(100)]
        public string? EvaluationErrorCode { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? EvaluationRawResponse { get; set; }

        public int EvaluationRetryCount { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? InvalidCriterionCodesJson { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? EvaluatedAt { get; set; }

        public virtual User User { get; set; } = null!;

        public virtual Question Question { get; set; } = null!;
    }
}
