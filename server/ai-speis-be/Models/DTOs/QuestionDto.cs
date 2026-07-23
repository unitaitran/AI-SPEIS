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
        [StringLength(4000, ErrorMessage = "Nội dung câu hỏi không được vượt quá 4000 ký tự.")]
        public string? QuestionContent { get; set; }

        [StringLength(100, ErrorMessage = "Ngành học không được vượt quá 100 ký tự.")]
        public string? Major { get; set; }

        [StringLength(100, ErrorMessage = "Vị trí mục tiêu không được vượt quá 100 ký tự.")]
        public string? RoleTarget { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [Required(ErrorMessage = "Độ khó là bắt buộc.")]
        public QuestionDifficultyEnum? Difficulty { get; set; }
        
        [StringLength(4000, ErrorMessage = "Câu trả lời gợi ý không được vượt quá 4000 ký tự.")]
        public string? SuggestedAnswer { get; set; }

        [StringLength(20, ErrorMessage = "Trạng thái không được vượt quá 20 ký tự.")]
        public string? Status { get; set; }

        [StringLength(100, ErrorMessage = "Loại câu hỏi không được vượt quá 100 ký tự.")]
        public string? QuestionType { get; set; }

        [StringLength(200, ErrorMessage = "Tech stack không được vượt quá 200 ký tự.")]
        public string? TechStack { get; set; }

        [StringLength(500, ErrorMessage = "Tags không được vượt quá 500 ký tự.")]
        public string? Tags { get; set; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (NormalizeQuestionContent() is null)
            {
                yield return new ValidationResult(
                    "Nội dung câu hỏi là bắt buộc.",
                    new[] { nameof(QuestionContent) });
            }

            if (Normalize(Major) is null)
            {
                yield return new ValidationResult(
                    "Ngành học là bắt buộc.",
                    new[] { nameof(Major) });
            }

            if (Normalize(RoleTarget) is null)
            {
                yield return new ValidationResult(
                    "Vị trí mục tiêu là bắt buộc.",
                    new[] { nameof(RoleTarget) });
            }

            if (Difficulty.HasValue &&
                !Enum.IsDefined(typeof(QuestionDifficultyEnum), Difficulty.Value))
            {
                yield return new ValidationResult(
                    "Độ khó phải là Easy, Medium, Hard hoặc Expert.",
                    new[] { nameof(Difficulty) });
            }

            if (NormalizeSuggestedAnswer() is null)
            {
                yield return new ValidationResult(
                    "Câu trả lời gợi ý là bắt buộc.",
                    new[]
                    {
                        nameof(SuggestedAnswer)
                    });
            }

            if (ParseStatus() is null &&
                !string.IsNullOrWhiteSpace(Status))
            {
                yield return new ValidationResult(
                    "Trạng thái phải là Active hoặc Inactive.",
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
            return 
                Normalize(QuestionContent);
        }

        private string? NormalizeSuggestedAnswer()
        {
            return Normalize(SuggestedAnswer);
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
        [Range(1, 1_000_000, ErrorMessage = "Số trang phải từ 1 đến 1000000.")]
        public int PageNumber { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "Kích thước trang phải từ 1 đến 100.")]
        public int PageSize { get; set; } = 10;

        [StringLength(200, ErrorMessage = "Từ khóa không được vượt quá 200 ký tự.")]
        public string? Keyword { get; set; }

        [StringLength(100, ErrorMessage = "Ngành học không được vượt quá 100 ký tự.")]
        public string? Major { get; set; }

        [StringLength(100, ErrorMessage = "Vị trí mục tiêu không được vượt quá 100 ký tự.")]
        public string? RoleTarget { get; set; }

        [StringLength(200, ErrorMessage = "Tech stack không được vượt quá 200 ký tự.")]
        public string? TechStack { get; set; }

        [StringLength(100, ErrorMessage = "Loại câu hỏi không được vượt quá 100 ký tự.")]
        public string? InterviewType { get; set; }

        [StringLength(500, ErrorMessage = "Tags không được vượt quá 500 ký tự.")]
        public string? Tags { get; set; }

        public QuestionDifficultyEnum? Difficulty { get; set; }

        [StringLength(20, ErrorMessage = "Trạng thái không được vượt quá 20 ký tự.")]
        public string? Status { get; set; }

        [StringLength(50, ErrorMessage = "SortBy không được vượt quá 50 ký tự.")]
        public string? SortBy { get; set; }

        [StringLength(10, ErrorMessage = "SortDirection không được vượt quá 10 ký tự.")]
        public string? SortDirection { get; set; }

        public bool IncludeDeleted { get; set; } = false;
    }

    public sealed class UserQuestionQueryDto
    {
        [Range(1, 1_000_000, ErrorMessage = "Số trang phải từ 1 đến 1000000.")]
        public int PageNumber { get; set; } = 1;

        [Range(1, 100000, ErrorMessage = "Kích thước trang phải từ 1 đến 100000.")]
        public int PageSize { get; set; } = 10;

        [StringLength(100, ErrorMessage = "Ngành học không được vượt quá 100 ký tự.")]
        public string? Major { get; set; }

        [StringLength(100, ErrorMessage = "Vị trí mục tiêu không được vượt quá 100 ký tự.")]
        public string? RoleTarget { get; set; }

        [StringLength(20, ErrorMessage = "Độ khó không được vượt quá 20 ký tự.")]
        public string? Difficulty { get; set; }
    }

    public sealed class AdminQuestionListItemDto
    {
        public int QuestionId { get; set; }
        public string QuestionCode => $"Q-{QuestionId}";
        public int UserId { get; set; }
        public string QuestionContent { get; set; } = string.Empty;
        public string SuggestedAnswer { get; set; } = string.Empty;
        public QuestionDifficultyEnum Difficulty { get; set; }
        public string Role { get; set; } = string.Empty;
        public string RoleTarget { get; set; } = string.Empty;
        public string Major { get; set; } = string.Empty;
        public string InterviewType { get; set; } = string.Empty;
        public string QuestionType { get; set; } = string.Empty;
        public string TechStack { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
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
        public string QuestionCode => $"Q-{QuestionId}";
        public int UserId { get; set; }
        public string QuestionContent { get; set; } = string.Empty;
        public string SuggestedAnswer { get; set; } = string.Empty;
        public QuestionDifficultyEnum Difficulty { get; set; }
        public string Role { get; set; } = string.Empty;
        public string RoleTarget { get; set; } = string.Empty;
        public string Major { get; set; } = string.Empty;
        public string InterviewType { get; set; } = string.Empty;
        public string QuestionType { get; set; } = string.Empty;
        public string TechStack { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public int? DeletedBy { get; set; }
    }

    public sealed class QuestionImportSummaryDto
    {
        public int TotalRows { get; set; }
        public int ImportedRows { get; set; }
        public int FailedRows { get; set; }
        public List<QuestionImportRowErrorDto> Errors { get; set; } = new();
    }

    public sealed class QuestionImportRowErrorDto
    {
        public int RowNumber { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
