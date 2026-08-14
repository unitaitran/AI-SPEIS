using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ai_speis_be.Models
{
    [Table("JDExtractedProfile")]
    [Index(nameof(JDFileId), Name = "IX_JDExtractedProfile_JDFileId", IsUnique = true)]
    public class JDExtractedProfile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ExtractedProfileId { get; set; }

        [Required]
        [ForeignKey("JDFile")]
        public int JDFileId { get; set; }

        [MaxLength(255)]
        public string? JobTitle { get; set; }

        [MaxLength(100)]
        public string? ExperienceLevel { get; set; }

        [MaxLength(100)]
        public string? RoleTarget { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string RequiredSkills { get; set; } = "[]";

        [Column(TypeName = "nvarchar(max)")]
        public string NiceToHaveSkills { get; set; } = "[]";

        [Column(TypeName = "nvarchar(max)")]
        public string? Responsibilities { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? CompanyCharacteristics { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? RawAiOutput { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? ConfidenceScore { get; set; }

        [MaxLength(500)]
        public string? ErrorMessage { get; set; }

        [Required]
        public bool IsConfirmed { get; set; } = false;

        [ForeignKey("ConfirmedByUser")]
        public int? ConfirmedBy { get; set; }

        public DateTime? ConfirmedAt { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual JDFile JDFile { get; set; } = null!;
        public virtual User? ConfirmedByUser { get; set; }
    }
}
