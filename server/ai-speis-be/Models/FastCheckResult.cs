using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ai_speis_be.Models
{
    [Table("FastCheckResult")]
    public class FastCheckResult
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int FastCheckResultId { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        [ForeignKey("CVFile")]
        public int CVFileId { get; set; }

        [Required]
        [ForeignKey("JDFile")]
        public int JDFileId { get; set; }

        /// <summary>
        /// Điểm phù hợp (0–100)
        /// </summary>
        [Required]
        public int MatchScore { get; set; }

        /// <summary>
        /// Mức độ phù hợp (e.g. "Rất phù hợp", "Phù hợp", "Cần cải thiện", "Không phù hợp")
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string SuitabilityLevel { get; set; } = string.Empty;

        /// <summary>
        /// Danh sách kỹ năng trùng khớp, lưu dạng JSON array
        /// </summary>
        [Required]
        public string MatchingSkillsJson { get; set; } = "[]";

        /// <summary>
        /// Danh sách kỹ năng còn thiếu, lưu dạng JSON array
        /// </summary>
        [Required]
        public string MissingSkillsJson { get; set; } = "[]";

        /// <summary>
        /// Lời khuyên / phân tích bổ sung từ AI
        /// </summary>
        [Required]
        public string Advice { get; set; } = string.Empty;

        /// <summary>
        /// Dữ liệu JSON thô đầy đủ từ AI (để phân tích bổ sung / additionalAnalysis)
        /// </summary>
        public string? RawAiResponseJson { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual CVFile CVFile { get; set; } = null!;
        public virtual JDFile JDFile { get; set; } = null!;
    }
}
