using System.Collections.Generic;

namespace ai_speis_be.DTOs.JdParsing
{
    /// <summary>
    /// DTO returned when fetching cached Fast Check results from the database.
    /// </summary>
    public class FastCheckResultDto
    {
        public int FastCheckResultId { get; set; }
        public int CVFileId { get; set; }
        public int JDFileId { get; set; }
        public int MatchScore { get; set; }
        public string SuitabilityLevel { get; set; } = string.Empty;
        public List<string> MatchingSkills { get; set; } = new List<string>();
        public List<string> MissingSkills { get; set; } = new List<string>();
        public string Advice { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Mirrors the CvJdMatchResultResponse fields for the frontend adapter.
        /// Always true for cached results.
        /// </summary>
        public bool Success { get; set; } = true;
    }
}
