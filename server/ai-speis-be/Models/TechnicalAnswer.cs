using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.Models
{
    [Table("TechnicalAnswer")]
    public sealed class TechnicalAnswer
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TechnicalAnswerId { get; set; }

        [Required]
        public int TechnicalSessionQuestionId { get; set; }

        [Required]
        public string Transcript { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? AudioId { get; set; }

        [MaxLength(128)]
        public string? SubmissionIdempotencyKey { get; set; }

        public int AnswerVersion { get; set; } = 1;
        public decimal? SttConfidence { get; set; }

        public decimal? AiApplicationScore { get; set; }

        public string? AiCriteriaDetailJson { get; set; }
        public string? AiStrengths { get; set; }
        public string? AiMissingPoints { get; set; }
        public decimal? ComputedScore { get; set; }
        public decimal? FinalQuestionScore { get; set; }

        [Required]
        public TechnicalAnswerEvaluationStatus EvaluationStatus { get; set; } = TechnicalAnswerEvaluationStatus.Processing;

        [MaxLength(100)]
        public string? AiErrorCode { get; set; }

        [MaxLength(120)]
        public string? EvaluationModel { get; set; }

        [MaxLength(80)]
        public string? EvaluationPromptVersion { get; set; }

        public int? EvaluationInputTokens { get; set; }
        public int? EvaluationOutputTokens { get; set; }
        public long? EvaluationLatencyMs { get; set; }
        public int EvaluationRetryCount { get; set; }

        [MaxLength(120)]
        public string? AiProvider { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? EvaluatedAt { get; set; }

        public TechnicalSessionQuestion TechnicalSessionQuestion { get; set; } = null!;
    }
}
