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
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;

        public virtual CVExtractedProfile CVExtractedProfile { get; set; } = null!;

        public virtual JDExtractedProfile JDExtractedProfile { get; set; } = null!;

        public virtual ICollection<InterviewSession> InterviewSessions { get; set; } = new List<InterviewSession>();
    }
}
