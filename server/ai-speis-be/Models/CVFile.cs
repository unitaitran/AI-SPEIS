
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ai_speis_be.Models.Enums;
namespace ai_speis_be.Models
{
    [Table("CVFile")]
    [Index(nameof(CVFileId), Name = "IX_CVFile_CVFileId", IsUnique = true)]
    [Index(nameof(UserId), Name = "IX_CVFile_UserId")]
    public class CVFile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CVFileId { get; set; }
        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        public string FileName { get; set; } = null!;
        [Required]
        public string FilePath { get; set; } = null!;
        [Required]
        public long FileSize { get; set; } = 0;
        [Required]
        public string FileType { get; set; } = null!;
        [Required]
        public CVFileStatus Status {get; set;} = CVFileStatus.Pending;
        [Required]
        public DateTime UploadedAt { get; set; } = DateTime.Now;

        public DateTime ? UpdatedAt { get; set; }
        

        // Navigation property
        public virtual User User { get; set; } = null!;
        
    }
}