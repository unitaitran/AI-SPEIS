using System;
using System.Collections.Generic;

namespace ai_speis_be.DTOs.CvParsing
{
    /// <summary>
    /// Response DTO cho API GET /parsed-data — chỉ trả đúng fields cần thiết, không lộ internal data.
    /// </summary>
    public class CvParsedDataResponse
    {
        public int CVFileId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? RoleTarget { get; set; }
        public bool IsConfirmed { get; set; }
        public DateTime CreatedAt { get; set; }

        // AI Assessment (readonly — FE chỉ hiển thị, không cho sửa)
        public string? OverallAssessment { get; set; }
        public string? Strengths { get; set; }
        public string? Weaknesses { get; set; }
        public decimal? ConfidenceScore { get; set; }
        public string? ErrorMessage { get; set; }

        public List<EducationDto> Education { get; set; } = new();
        public List<ExperienceDto> Experience { get; set; } = new();
        public List<CvProjectResponse> Projects { get; set; } = new();
        public List<CvSkillResponse> Skills { get; set; } = new();
    }

    public class CvSkillResponse
    {
        public int CVSkillId { get; set; }
        public string SkillName { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Category { get; set; } = "Other";
    }

    public class CvProjectResponse
    {
        public int CVProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string? RoleDescription { get; set; }
        public string TechnologyStack { get; set; } = string.Empty;
        public string? ProjectSummary { get; set; }
        public string? Duration { get; set; }
    }

    /// <summary>
    /// Response DTO cho API GET /status — chỉ trả trạng thái polling.
    /// </summary>
    public class CvParseStatusResponse
    {
        public int CVFileId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
