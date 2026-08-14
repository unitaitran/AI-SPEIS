using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ai_speis_be.Models
{
    [Table("UserSkillScore")]
    public class UserSkillScore
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserSkillScoreId { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }
        public virtual User User { get; set; } = null!;

        [ForeignKey("InterviewCampaign")]
        public int? InterviewCampaignId { get; set; }
        public virtual InterviewCampaign? InterviewCampaign { get; set; }

        [ForeignKey("InterviewSession")]
        public int? InterviewSessionId { get; set; }
        public virtual InterviewSession? InterviewSession { get; set; }

        [Required]
        [MaxLength(100)]
        public string SkillCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string SkillName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(5,2)")]
        public decimal Score { get; set; }

        [MaxLength(255)]
        public string? SessionTitle { get; set; }

        [Required]
        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
