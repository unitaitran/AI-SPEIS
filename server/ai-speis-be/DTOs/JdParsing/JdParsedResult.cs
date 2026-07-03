using System.Text.Json.Serialization;

namespace ai_speis_be.DTOs.JdParsing
{
    public class JdParsedResult
    {
        [JsonPropertyName("isValidJd")]
        public bool IsValidJd { get; set; }

        [JsonPropertyName("jdConfidenceScore")]
        public decimal JdConfidenceScore { get; set; }

        [JsonPropertyName("invalidReason")]
        public string? InvalidReason { get; set; }

        [JsonPropertyName("jobTitle")]
        public string? JobTitle { get; set; }

        [JsonPropertyName("experienceLevel")]
        public string? ExperienceLevel { get; set; }

        [JsonPropertyName("roleTarget")]
        public string? RoleTarget { get; set; }

        [JsonPropertyName("requiredSkills")]
        public List<string> RequiredSkills { get; set; } = new List<string>();

        [JsonPropertyName("niceToHaveSkills")]
        public List<string> NiceToHaveSkills { get; set; } = new List<string>();

        [JsonPropertyName("responsibilities")]
        public string? Responsibilities { get; set; }

        [JsonPropertyName("companyCharacteristics")]
        public string? CompanyCharacteristics { get; set; }
    }
}
