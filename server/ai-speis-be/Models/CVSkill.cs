using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ai_speis_be.Models
{
    [Table("CVSkill")]
    [Index(nameof(ExtractedProfileId), Name = "IX_CVSkill_ExtractedProfileId")]
    public class CVSkill
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CVSkillId { get; set; }

        [Required]
        [ForeignKey("ExtractedProfile")]
        public int ExtractedProfileId { get; set; }

        [Required]
        [MaxLength(100)]
        public string SkillName { get; set; } = null!;

        /// <summary>
        /// Source of this skill: "AI" (auto-extracted) or "USER" (manually added by user)
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string Source { get; set; } = "AI";

        /// <summary>
        /// Skill category: Language, Framework, Database, Tool, Cloud, Other
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string Category { get; set; } = "Other";

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        public virtual CVExtractedProfile ExtractedProfile { get; set; } = null!;
    }
}
