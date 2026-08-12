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

        // Technical rounds are created on the current V2 runtime.
        [MaxLength(20)]
        public string? TechnicalRuntimeVersion { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [Required]
        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        // Technical Interview runtime metadata. Nullable fields preserve existing sessions.

        [MaxLength(50)]
        public string? TechnicalAiProvider { get; set; }

        // Navigation properties
        public virtual InterviewCampaign InterviewCampaign { get; set; } = null!;

        public virtual TechnicalQuestionSet? TechnicalQuestionSet { get; set; }

        public virtual TechnicalRoundResult? TechnicalRoundResult { get; set; }

        public virtual ICollection<AIInteractionLog> AIInteractionLogs { get; set; } = new List<AIInteractionLog>();
    }
}
