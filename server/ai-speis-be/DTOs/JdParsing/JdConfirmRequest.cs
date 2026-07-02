using System.ComponentModel.DataAnnotations;

namespace ai_speis_be.DTOs.JdParsing
{
    public class JdConfirmRequest
    {
        [Required]
        [MaxLength(255)]
        public string JobTitle { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ExperienceLevel { get; set; }

        public List<string> RequiredSkills { get; set; } = new List<string>();

        public List<string> NiceToHaveSkills { get; set; } = new List<string>();

        public string? Responsibilities { get; set; }

        public string? CompanyCharacteristics { get; set; }
    }
}
