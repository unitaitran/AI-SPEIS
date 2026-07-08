using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.Models.DTOs
{
    public class InterviewCampaignDto
    {
        public int InterviewCampaignId { get; set; }
        public int UserId { get; set; }
        public int CVExtractedProfileId { get; set; }
        public int JDExtractedProfileId { get; set; }
        public string Language { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<InterviewSessionDto> Sessions { get; set; } = new List<InterviewSessionDto>();
    }

    public class InterviewSessionDto
    {
        public int InterviewSessionId { get; set; }
        public int InterviewCampaignId { get; set; }
        public string InterviewRoundType { get; set; } = string.Empty;
        public string Difficulty { get; set; } = string.Empty;
        public int QuestionCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateInterviewSessionRequest
    {
        [Required]
        public int CVFileId { get; set; }

        [Required]
        public int JDFileId { get; set; }

        public bool IncludeCoding { get; set; }

        [MaxLength(10)]
        public string Language { get; set; } = "vi";

        [Required]
        public string Mode { get; set; } = "Practice";
    }

    public class AvailableRoundsDto
    {
        public string RoleTarget { get; set; } = string.Empty;
        public List<string> AvailableRounds { get; set; } = new List<string>();
        public bool HasOptionalCoding { get; set; }
    }
}
