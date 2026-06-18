using ai_speis_be.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ai_speis_be.Models.DTOs
{
    public enum AdminQuestionStatus
    {
        Active,
        Inactive
    }

    public abstract class AdminQuestionMutationRequestDto : IValidatableObject
    {
        [StringLength(4000)]
        public string? QuestionText { get; set; }

        [StringLength(4000)]
        public string? QuestionContent { get; set; }

        [StringLength(100)]
        public string? Major { get; set; }

        [StringLength(100)]
        public string? RoleTarget { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [Required]
        public QuestionDifficultyEnum? Difficulty { get; set; }

        [StringLength(4000)]
        public string? ExpectedAnswer { get; set; }

        [StringLength(4000)]
        public string? Rubric { get; set; }

        [StringLength(4000)]
        public string? SuggestedAnswer { get; set; }

        [StringLength(20)]
        public string? Status { get; set; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (NormalizeQuestionContent() is null)
            {
                yield return new ValidationResult(
                    "QuestionText or QuestionContent is required.",
                    new[] { nameof(QuestionText), nameof(QuestionContent) });
            }

            if (Normalize(Major) is null)
            {
                yield return new ValidationResult(
                    "Major is required.",
                    new[] { nameof(Major) });
            }

            if (Normalize(RoleTarget) is null)
            {
                yield return new ValidationResult(
                    "RoleTarget is required.",
                    new[] { nameof(RoleTarget) });
            }

            if (NormalizeSuggestedAnswer() is null)
            {
                yield return new ValidationResult(
                    "ExpectedAnswer, Rubric, or SuggestedAnswer is required.",
                    new[]
                    {
                        nameof(ExpectedAnswer),
                        nameof(Rubric),
                        nameof(SuggestedAnswer)
                    });
            }

            if (ParseStatus() is null &&
                !string.IsNullOrWhiteSpace(Status))
            {
                yield return new ValidationResult(
                    "Status must be Active or Inactive.",
                    new[] { nameof(Status) });
            }
        }

        public string GetQuestionContent()
        {
            return NormalizeQuestionContent() ?? string.Empty;
        }

        public string GetMajor()
        {
            return Normalize(Major) ?? string.Empty;
        }

        public string GetRoleTarget()
        {
            return Normalize(RoleTarget) ?? string.Empty;
        }

        public string GetSuggestedAnswer()
        {
            return NormalizeSuggestedAnswer() ?? string.Empty;
        }

        public AdminQuestionStatus GetStatus()
        {
            return ParseStatus() ?? AdminQuestionStatus.Active;
        }

        private AdminQuestionStatus? ParseStatus()
        {
            var status = Normalize(Status);
            if (status is null)
            {
                return AdminQuestionStatus.Active;
            }

            return Enum.TryParse<AdminQuestionStatus>(
                status,
                ignoreCase: true,
                out var parsedStatus)
                ? parsedStatus
                : null;
        }

        private string? NormalizeQuestionContent()
        {
            return Normalize(QuestionText) ??
                Normalize(QuestionContent);
        }

        private string? NormalizeSuggestedAnswer()
        {
            return Normalize(ExpectedAnswer) ??
                Normalize(Rubric) ??
                Normalize(SuggestedAnswer);
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }

    public sealed class AdminQuestionCreateRequestDto
        : AdminQuestionMutationRequestDto
    {
    }

    public sealed class AdminQuestionUpdateRequestDto
        : AdminQuestionMutationRequestDto
    {
    }

    public sealed class AdminQuestionQueryDto
    {
        [Range(1, 1_000_000)]
        public int PageNumber { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 10;

        [StringLength(200)]
        public string? Keyword { get; set; }

        [StringLength(100)]
        public string? Major { get; set; }

        [StringLength(100)]
        public string? RoleTarget { get; set; }

        public QuestionDifficultyEnum? Difficulty { get; set; }

        public bool IncludeDeleted { get; set; } = false;
    }

    public sealed class AdminQuestionListItemDto
    {
        public int QuestionId { get; set; }
        public int UserId { get; set; }
        public string QuestionContent { get; set; } = string.Empty;
        public string SuggestedAnswer { get; set; } = string.Empty;
        public QuestionDifficultyEnum Difficulty { get; set; }
        public string RoleTarget { get; set; } = string.Empty;
        public string Major { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public int? DeletedBy { get; set; }
    }

    public class QuestionResponseDto
    {
        public int QuestionId { get; set; }
        public int UserId { get; set; }
        public string QuestionContent { get; set; } = string.Empty;
        public string SuggestedAnswer { get; set; } = string.Empty;
        public QuestionDifficultyEnum Difficulty { get; set; }
        public string RoleTarget { get; set; } = string.Empty;
        public string Major { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public int? DeletedBy { get; set; }
    }
}
