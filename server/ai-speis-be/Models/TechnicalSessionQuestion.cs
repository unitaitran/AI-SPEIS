using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.Models
{
    [Table("TechnicalSessionQuestion")]
    public sealed class TechnicalSessionQuestion
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TechnicalSessionQuestionId { get; set; }

        [Required]
        public int TechnicalQuestionSetId { get; set; }

        [Required]
        public int QuestionId { get; set; }

        [Required]
        public int QuestionOrder { get; set; }

        [Required]
        public TechnicalSessionQuestionType QuestionType { get; set; } = TechnicalSessionQuestionType.Main;

        public int? ParentQuestionId { get; set; }

        [Required]
        public string QuestionSnapshotJson { get; set; } = string.Empty;

        [Required]
        public TechnicalSessionQuestionStatus Status { get; set; } = TechnicalSessionQuestionStatus.Pending;

        public DateTime? AskedAt { get; set; }
        public DateTime? AnsweredAt { get; set; }

        [MaxLength(120)]
        public string? Skill { get; set; }

        [MaxLength(120)]
        public string? Subskill { get; set; }

        [MaxLength(120)]
        public string? EvaluationObjective { get; set; }

        [MaxLength(50)]
        public string? DifficultySnapshot { get; set; }

        public TechnicalQuestionSet TechnicalQuestionSet { get; set; } = null!;
        public Question Question { get; set; } = null!;
        public TechnicalSessionQuestion? ParentQuestion { get; set; }
        public ICollection<TechnicalSessionQuestion> ChildQuestions { get; set; } = new List<TechnicalSessionQuestion>();
        public TechnicalAnswer? Answer { get; set; }
    }
}
