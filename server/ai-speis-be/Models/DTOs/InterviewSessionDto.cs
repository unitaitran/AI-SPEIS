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
        public string Language { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? StartedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public int RemainingInterviewQuota { get; set; }
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

        public int DurationMinutes { get; set; } = 10;

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

            if (DurationMinutes != 10 && DurationMinutes != 15 && DurationMinutes != 20)
            {
                yield return new ValidationResult(
                    "DurationMinutes chỉ chấp nhận 10, 15 hoặc 20 phút.",
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
                        ? 3
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
    }
}
