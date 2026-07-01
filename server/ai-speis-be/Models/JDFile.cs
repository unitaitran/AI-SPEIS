using ai_speis_be.Migrations;
using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ai_speis_be.Models
{
    [Table("JDFile")]
    [Index(nameof(JDFileId), Name = "IX_JDFile_JDFileId", IsUnique = true)]
    [Index(nameof(UserId), Name = "IX_JDFile_UserId")]
    public class JDFile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int JDFileId { get; set; }
        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }
        [Required]
        public JDInputType InputType { get; set; } = JDInputType.Text; // Enum: Text hoặc File
        // Text thô để AI đọc.
        // InputType=Text → set ngay khi tạo
        // InputType=File → null ban đầu, background worker extract PDF rồi điền sau
        [Column(TypeName = "nvarchar(max)")]
        public string? RawText { get; set; }
        // Các thuộc tính File (nullable vì có thể user chỉ paste text)
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
        public long? FileSize { get; set; }
        public string? FileType { get; set; }
        [Required]
        public JDFileStatus Status { get; set; } = JDFileStatus.Pending;
        [Required]
        public DateTime UploadedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        // Navigation properties
        public virtual User User { get; set; } = null!;
    }
}
