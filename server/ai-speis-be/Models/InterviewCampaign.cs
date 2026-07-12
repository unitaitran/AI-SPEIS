using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.Models
{
    [Table("InterviewCampaign")]
    public class InterviewCampaign
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int InterviewCampaignId { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        [ForeignKey("CVExtractedProfile")]
        public int CVExtractedProfileId { get; set; }

        [Required]
        [ForeignKey("JDExtractedProfile")]
        public int JDExtractedProfileId { get; set; }

        [Required]
        [MaxLength(10)]
        public string Language { get; set; } = "vi";

        [Required]
        public InterviewMode Mode { get; set; } = InterviewMode.Practice;

        [Required]
        public int DurationMinutes { get; set; } = 10;

        [Required]
        public InterviewCampaignStatus Status { get; set; } = InterviewCampaignStatus.Pending;

        public DateTime? StartedAt { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public DateTime? CancelledAt { get; set; }

        [Required]
        public bool QuotaRefunded { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [Required]
        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;

        public virtual CVExtractedProfile CVExtractedProfile { get; set; } = null!;

        public virtual JDExtractedProfile JDExtractedProfile { get; set; } = null!;

        public virtual ICollection<InterviewSession> InterviewSessions { get; set; } = new List<InterviewSession>();
    }
}
