using System.ComponentModel.DataAnnotations;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.Models.DTOs
{
    public class JDDto
    {
        public int JDFileId { get; set; }
        public int UserId { get; set; }
        public JDInputType InputType { get; set; } = JDInputType.Text;
        public string? RawText { get; set; } 
        public string ? FileName { get; set; } 
        public string ? FilePath { get; set; } 
        public long ? FileSize { get; set; } 
        public string ? FileType { get; set; } 
        public JDFileStatus Status {get; set;} = JDFileStatus.Pending;
        public DateTime UploadedAt { get; set; } = DateTime.Now;
        public DateTime ? UpdatedAt { get; set; } 
        
    }
     public class SubmitJDTextRequest
    {
        [Required(ErrorMessage = "Nội dung JD không được để trống.")]
        [MinLength(50, ErrorMessage = "Nội dung JD phải có ít nhất 50 ký tự.")]
        [MaxLength(50000, ErrorMessage = "Nội dung JD không được vượt quá 50.000 ký tự.")]
        public string RawText { get; set; } = null!;
    }
}