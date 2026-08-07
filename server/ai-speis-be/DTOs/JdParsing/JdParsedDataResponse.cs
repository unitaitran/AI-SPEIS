namespace ai_speis_be.DTOs.JdParsing
{
    public class JdParsedDataResponse
    {
        public int ExtractedProfileId { get; set; }
        public int JDFileId { get; set; }
        public string? FileName { get; set; }
        public string? RawText { get; set; }
        public string? InputType { get; set; }
        public string? JobTitle { get; set; }
        public string? ExperienceLevel { get; set; }
        public string? RoleTarget { get; set; }
        public List<string> RequiredSkills { get; set; } = new List<string>();
        public List<string> NiceToHaveSkills { get; set; } = new List<string>();
        public string? Responsibilities { get; set; }
        public string? CompanyCharacteristics { get; set; }
        public decimal? ConfidenceScore { get; set; }
        public string? WarningMessage { get; set; } // If confidence score < 0.80
    }
}
