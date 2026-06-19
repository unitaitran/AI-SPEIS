using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ai_speis_be.DTOs.CvParsing
{
    public class CvConfirmRequest
    {
        [Required]
        public int CvFileId { get; set; }

        public string? RoleTarget { get; set; }
        
        [Required]
        public List<EducationDto> Education { get; set; } = new();
        
        [Required]
        public List<ExperienceDto> Experience { get; set; } = new();
        
        [Required]
        public List<ProjectDto> Projects { get; set; } = new();
        
        [Required]
        public List<SkillDto> Skills { get; set; } = new();
    }
}
