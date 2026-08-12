using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.Models
{
    [Table("TechnicalQuestionSet")]
    public sealed class TechnicalQuestionSet
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TechnicalQuestionSetId { get; set; }

        [Required]
        public int InterviewSessionId { get; set; }

        [Required]
        public TechnicalQuestionSetSelectionSource SelectionSource { get; set; } = TechnicalQuestionSetSelectionSource.ExternalAi;

        [MaxLength(120)]
        public string? AiExecutionRunId { get; set; }

        [Required]
        public int QuestionCount { get; set; }

        public string? CoveredSkillsJson { get; set; }
        public string? ConstraintsJson { get; set; }

        [Required]
        public TechnicalQuestionSetStatus Status { get; set; } = TechnicalQuestionSetStatus.Active;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public InterviewSession InterviewSession { get; set; } = null!;
        public ICollection<TechnicalSessionQuestion> Questions { get; set; } = new List<TechnicalSessionQuestion>();
    }
}
