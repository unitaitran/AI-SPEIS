using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.Models.DTOs
{
    public class CVDto
    {
        public int CVFileId { get; set; }
        public int UserId { get; set; }
        public string FileName { get; set; } = null!;
        public string FilePath { get; set; } = null!;
        public long FileSize { get; set; } = 0;
        public string FileType { get; set; } = null!;
        public CVFileStatus Status {get; set;} = CVFileStatus.Pending;
        public DateTime UploadedAt { get; set; } = DateTime.Now;
        public DateTime ? UpdatedAt { get; set; } 
        
    }
}