using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.Models
{
    [Table("InterviewSession")]
    public class InterviewSession
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int InterviewSessionId { get; set; }

        [Required]
        [ForeignKey("InterviewCampaign")]
        public int InterviewCampaignId { get; set; }

        [Required]
        public InterviewRoundType InterviewRoundType { get; set; }

        [Required]
        public QuestionDifficultyEnum Difficulty { get; set; }

        [Required]
        public int QuestionCount { get; set; } = 5;

        [Required]
        public InterviewSessionStatus Status { get; set; } = InterviewSessionStatus.Pending;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [Required]
        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        // Navigation properties
        public virtual InterviewCampaign InterviewCampaign { get; set; } = null!;
    }
}
