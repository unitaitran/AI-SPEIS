using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.Models.DTOs
{
    public class InterviewCampaignDto
    {
        public int InterviewCampaignId { get; set; }
        public int UserId { get; set; }
        public int CVExtractedProfileId { get; set; }
        public int JDExtractedProfileId { get; set; }
        public string? JobTitle { get; set; }
        public string Language { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? StartedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public int RemainingInterviewQuota { get; set; }
        public int MaxInterviewQuota { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public decimal? OverallScore { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<InterviewSessionDto> Sessions { get; set; } = new List<InterviewSessionDto>();
    }

    public sealed class CampaignInterviewResultDto
    {
        public int InterviewCampaignId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public decimal OverallScore { get; set; }
        public decimal MaxScore { get; set; } = 10m;
        public string PerformanceBand { get; set; } = string.Empty;
        public DateTime? CompletedAt { get; set; }
        public List<CampaignRoundResultDto> Rounds { get; set; } = new();
        public List<CampaignDashboardMetricDto> DashboardMetrics { get; set; } = new();
        public CampaignFinalFeedbackDto Feedback { get; set; } = new();
    }

    public sealed class CampaignRoundResultDto
    {
        public int InterviewSessionId { get; set; }
        public string RoundType { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public decimal MaxScore { get; set; } = 10m;
        public decimal BaseWeight { get; set; }
        public decimal AppliedWeight { get; set; }
        public string PerformanceBand { get; set; } = string.Empty;
        public int EvaluatedItemCount { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string? LevelAssessment { get; set; }
        public List<string> Strengths { get; set; } = new();
        public List<string> AreasForImprovement { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public List<CodingQuestionResultDto> CodingQuestions { get; set; } = new();
    }

    public sealed class CodingQuestionResultDto
    {
        public int CodingQuestionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public decimal PassRate { get; set; }
        public int PassedTestCases { get; set; }
        public int TotalTestCases { get; set; }
    }

    public sealed class CampaignDashboardMetricDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal? Score { get; set; }
        public List<string> Sources { get; set; } = new();
        public List<SkillHistoryPointDto> History { get; set; } = new();
    }

    public sealed class SkillHistoryPointDto
    {
        public int SessionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public DateTime Date { get; set; }
    }

    public sealed class CampaignFinalFeedbackDto
    {
        public string ExecutiveSummary { get; set; } = string.Empty;
        public List<string> Strengths { get; set; } = new();
        public List<string> AreasForImprovement { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
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
        public int CompletedQuestionCount { get; set; }
        public int? PassedTestCases { get; set; }
        public int? TotalTestCases { get; set; }
    }

    public sealed class ActiveInterviewConflictDto
    {
        public int CampaignId { get; set; }
        public int? SessionId { get; set; }
        public string InterviewType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? StartedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int CompletedQuestionCount { get; set; }
        public bool CanResume { get; set; }
        public bool CanEnd { get; set; }
        public bool CanCloseCampaign { get; set; }
        public InterviewCampaignDto Campaign { get; set; } = new();
    }

    public class CreateInterviewSessionRequest : IValidatableObject
    {
        [Required]
        public int CVFileId { get; set; }

        [Required]
        public int JDFileId { get; set; }

        public bool IncludeCoding { get; set; }

        [Required]
        public List<string> SelectedRounds { get; set; } = new List<string>();

        public Dictionary<string, int> QuestionCounts { get; set; } = new Dictionary<string, int>();

        [Required]
        [MaxLength(10)]
        public string Language { get; set; } = "vi";

        [Required]
        public string Mode { get; set; } = "Practice";

        public int DurationMinutes { get; set; } = 60;

        [MaxLength(50)]
        public string? AiProvider { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!string.Equals(Language, "vi", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(Language, "en", StringComparison.OrdinalIgnoreCase))
            {
                yield return new ValidationResult(
                    "Language chỉ chấp nhận 'vi' hoặc 'en'.",
                    new[] { nameof(Language) });
            }

            var validModeNames = Enum.GetNames(typeof(InterviewMode));
            var hasValidMode = validModeNames.Contains(Mode, StringComparer.OrdinalIgnoreCase);
            var parsedMode = InterviewMode.Practice;
            if (hasValidMode)
            {
                Enum.TryParse(Mode, true, out parsedMode);
            }
            if (!hasValidMode)
            {
                yield return new ValidationResult(
                    "Mode chỉ chấp nhận 'Practice' hoặc 'RealTest'.",
                    new[] { nameof(Mode) });
            }

            if (DurationMinutes < 5 || DurationMinutes > 120)
            {
                yield return new ValidationResult(
                    "DurationMinutes phải từ 5 đến 120 phút (mặc định 60 phút).",
                    new[] { nameof(DurationMinutes) });
            }

            var selectedRounds = SelectedRounds ?? new List<string>();
            var validRoundNames = Enum.GetNames(typeof(InterviewRoundType));
            var hasInvalidRound = selectedRounds.Any(round =>
                string.IsNullOrWhiteSpace(round)
                || !validRoundNames.Contains(round, StringComparer.OrdinalIgnoreCase));
            if (hasInvalidRound)
            {
                yield return new ValidationResult(
                    "SelectedRounds chứa vòng phỏng vấn không hợp lệ.",
                    new[] { nameof(SelectedRounds) });
            }

            if (hasValidMode && parsedMode == InterviewMode.Practice && selectedRounds.Count == 0)
            {
                yield return new ValidationResult(
                    "Chế độ Practice yêu cầu chọn ít nhất một vòng phỏng vấn.",
                    new[] { nameof(SelectedRounds) });
            }

            if (hasValidMode && parsedMode == InterviewMode.Practice && !hasInvalidRound)
            {
                var questionCounts = QuestionCounts ?? new Dictionary<string, int>();
                var invalidCountKey = questionCounts.Keys.FirstOrDefault(round =>
                    !validRoundNames.Contains(round, StringComparer.OrdinalIgnoreCase));

                if (invalidCountKey != null)
                {
                    yield return new ValidationResult(
                        "QuestionCounts chứa vòng phỏng vấn không hợp lệ.",
                        new[] { nameof(QuestionCounts) });
                }

                foreach (var roundName in selectedRounds.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var configuredCount = questionCounts
                        .FirstOrDefault(item => string.Equals(item.Key, roundName, StringComparison.OrdinalIgnoreCase));
                    var maxCount = string.Equals(roundName, InterviewRoundType.Code.ToString(), StringComparison.OrdinalIgnoreCase)
                        ? 5
                        : 7;

                    if (string.IsNullOrEmpty(configuredCount.Key)
                        || configuredCount.Value < 1
                        || configuredCount.Value > maxCount)
                    {
                        yield return new ValidationResult(
                            $"Số câu hỏi của vòng {roundName} phải từ 1 đến {maxCount}.",
                            new[] { nameof(QuestionCounts) });
                    }
                }
            }
        }
    }

    public class AvailableRoundsDto
    {
        public string RoleTarget { get; set; } = string.Empty;
        public List<string> AvailableRounds { get; set; } = new List<string>();
        public bool HasOptionalCoding { get; set; }
        public string Difficulty { get; set; } = string.Empty;
    }

    public class InterviewQuotaDto
    {
        public int RemainingInterviewQuota { get; set; }
        public int MaxInterviewQuota { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public DateTime? SubscriptionExpiresAt { get; set; }
    }
}
