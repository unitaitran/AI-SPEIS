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
        public string IncorrectClaimsJson { get; set; } = "[]";

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string ImprovementSuggestionsJson { get; set; } = "[]";

        public TechnicalInterviewDecision Decision { get; set; }

        [Column(TypeName = "decimal(5,4)")]
        public decimal Confidence { get; set; }

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
