using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Models
{
    [Table("TechnicalAnswerEvaluation")]
    [Index(nameof(AttemptId), IsUnique = true)]
    public class TechnicalAnswerEvaluation
    {
        [Key]
        public Guid EvaluationId { get; set; } = Guid.NewGuid();

        [ForeignKey(nameof(Attempt))]
        public Guid AttemptId { get; set; }

        public Guid RootMainAttemptId { get; set; }

        [Required]
        [MaxLength(50)]
        public string RubricVersion { get; set; } = string.Empty;

        [Required]
        [MaxLength(80)]
        public string ScoringPolicyVersion { get; set; } = string.Empty;

        [Column(TypeName = "decimal(5,2)")]
        public decimal AiSuggestedOverallScore { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal FinalOverallScore { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string DimensionEvaluationsJson { get; set; } = "[]";

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string ScoringBreakdownJson { get; set; } = "[]";

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string StrengthsJson { get; set; } = "[]";

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string MissingPointsJson { get; set; } = "[]";

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string ImprovementSuggestionsJson { get; set; } = "[]";

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string FeedbackSummary { get; set; } = string.Empty;

        [Required]
        [MaxLength(80)]
        public string FeedbackPromptVersion { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string FeedbackModelName { get; set; } = string.Empty;

        public bool FeedbackFallbackUsed { get; set; }

        public TechnicalInterviewDecision Decision { get; set; }

        public TechnicalInterviewDecision BackendResolvedAction { get; set; }

        [MaxLength(1000)]
        public string? DecisionReason { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string TargetRubricCodesJson { get; set; } = "[]";

        [MaxLength(80)]
        public string? AdaptiveRuleVersion { get; set; }

        [MaxLength(500)]
        public string? OverrideReason { get; set; }

        public bool FallbackUsed { get; set; }

        [Required]
        [MaxLength(80)]
        public string PromptVersion { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string ModelName { get; set; } = string.Empty;

        public bool IsFinalForMainQuestion { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual TechnicalQuestionAttempt Attempt { get; set; } = null!;
    }
}
