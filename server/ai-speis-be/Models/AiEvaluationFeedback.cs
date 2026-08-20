using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ai_speis_be.Models
{
    [Table("AiEvaluationFeedback")]
    public sealed class AiEvaluationFeedback
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AiEvaluationFeedbackId { get; set; }

        public int UserId { get; set; }

        public int InterviewSessionId { get; set; }

        [Required]
        [MaxLength(30)]
        public string EvaluationType { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Reason { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Explanation { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public User User { get; set; } = null!;

        public InterviewSession InterviewSession { get; set; } = null!;
    }
}
