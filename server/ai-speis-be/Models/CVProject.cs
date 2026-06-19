using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ai_speis_be.Models
{
    [Table("CVProject")]
    [Index(nameof(ExtractedProfileId), Name = "IX_CVProject_ExtractedProfileId")]
    public class CVProject
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CVProjectId { get; set; }

        [Required]
        [ForeignKey("ExtractedProfile")]
        public int ExtractedProfileId { get; set; }

        [Required]
        [MaxLength(255)]
        public string ProjectName { get; set; } = null!;

        [Column(TypeName = "nvarchar(max)")]
        public string? RoleDescription { get; set; }

        /// <summary>
        /// JSON array of technology names: ["React", "Java", "SQL Server"]
        /// </summary>
        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string TechnologyStack { get; set; } = "[]";

        [Column(TypeName = "nvarchar(max)")]
        public string? ProjectSummary { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        public virtual CVExtractedProfile ExtractedProfile { get; set; } = null!;
    }
}
