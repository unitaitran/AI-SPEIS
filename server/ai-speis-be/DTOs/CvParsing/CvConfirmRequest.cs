using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ai_speis_be.DTOs.CvParsing
{
    public class CvConfirmRequest
    {
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
