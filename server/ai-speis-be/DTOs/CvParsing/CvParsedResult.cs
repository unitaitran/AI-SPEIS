using System.Collections.Generic;

namespace ai_speis_be.DTOs.CvParsing
{
    public class CvParsedResult
    {
        // CV Classification
        public bool IsValidCv { get; set; } = true;
        public string? InvalidReason { get; set; }
        public decimal CvConfidenceScore { get; set; } = 1.0m;

        // CV Assessment
        public string? OverallAssessment { get; set; }
        public string? Strengths { get; set; }
        public string? Weaknesses { get; set; }

        // CV Parsed Data
        public string? RoleTarget { get; set; }
        public List<EducationDto> Education { get; set; } = new();
        public List<ExperienceDto> Experience { get; set; } = new();
        public List<ProjectDto> Projects { get; set; } = new();
        public List<SkillDto> Skills { get; set; } = new();
    }

    public class EducationDto
    {
        public string School { get; set; } = string.Empty;
        public string Major { get; set; } = string.Empty;
        public string? Gpa { get; set; }
        public string? GraduationYear { get; set; }
    }

    public class ExperienceDto
    {
        public string Company { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string? Duration { get; set; }
        public string? Description { get; set; }
    }

    public class ProjectDto
    {
        public string ProjectName { get; set; } = string.Empty;
        public string? RoleDescription { get; set; }
        public string TechnologyStack { get; set; } = string.Empty;
        public string? ProjectSummary { get; set; }
        public string? Duration { get; set; }
    }

    public class SkillDto
    {
        public string SkillName { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        /// <summary>
        /// Skill category: Language, Framework, Database, Tool, Cloud, Other
        /// </summary>
        public string Category { get; set; } = "Other";
    }
}
