using System.Collections.Generic;

namespace ai_speis_be.DTOs.JdParsing
{
    public class CvJdMatchResultResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        
        public int MatchScore { get; set; }
        public string SuitabilityLevel { get; set; } = string.Empty;
        public List<string> MatchingSkills { get; set; } = new List<string>();
        public List<string> MissingSkills { get; set; } = new List<string>();
        public string Advice { get; set; } = string.Empty;
    }
}
