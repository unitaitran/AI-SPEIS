using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ai_speis_be.Models
{
    [Table("CVExtractedProfile")]
    [Index(nameof(CVFileId), Name = "IX_CVExtractedProfile_CVFileId", IsUnique = true)]
    public class CVExtractedProfile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ExtractedProfileId { get; set; }

        [Required]
        [ForeignKey("CVFile")]
        public int CVFileId { get; set; }

        /// <summary>
        /// JSON array of education objects: [{school, major, gpa, graduationYear}]
        /// </summary>
        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string Education { get; set; } = "[]";

        /// <summary>
        /// JSON array of experience objects: [{company, position, duration, description}]
        /// </summary>
        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string Experience { get; set; } = "[]";

        /// <summary>
        /// Vị trí ứng tuyển hoặc hướng chuyên môn chính (ví dụ: Backend, Frontend, Fullstack, etc.)
        /// </summary>
        [MaxLength(100)]
        public string? RoleTarget { get; set; }

        /// <summary>
        /// Original JSON response from AI (for audit and debugging)
        /// </summary>
        [Column(TypeName = "nvarchar(max)")]
        public string? RawAiOutput { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? ConfidenceScore { get; set; }

        /// <summary>
        /// AI đánh giá chung về CV (ví dụ: "Ứng viên có nền tảng kỹ thuật tốt...")
        /// </summary>
        [Column(TypeName = "nvarchar(max)")]
        public string? OverallAssessment { get; set; }

        /// <summary>
        /// Điểm mạnh của ứng viên theo AI
        /// </summary>
        [Column(TypeName = "nvarchar(max)")]
        public string? Strengths { get; set; }

        /// <summary>
        /// Điểm yếu / cần cải thiện theo AI
        /// </summary>
        [Column(TypeName = "nvarchar(max)")]
        public string? Weaknesses { get; set; }

        /// <summary>
        /// Thông báo lỗi khi CV không hợp lệ hoặc cảnh báo khi confidence thấp
        /// </summary>
        [MaxLength(500)]
        public string? ErrorMessage { get; set; }

        [Required]
        public bool IsConfirmed { get; set; } = false;

        [ForeignKey("ConfirmedByUser")]
        public int? ConfirmedBy { get; set; }

        public DateTime? ConfirmedAt { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual CVFile CVFile { get; set; } = null!;
        public virtual User? ConfirmedByUser { get; set; }
        public virtual ICollection<CVSkill> Skills { get; set; } = new List<CVSkill>();
        public virtual ICollection<CVProject> Projects { get; set; } = new List<CVProject>();
    }
}
