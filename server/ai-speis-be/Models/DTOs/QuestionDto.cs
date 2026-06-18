using ai_speis_be.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace ai_speis_be.Models.DTOs
{
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
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
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
        public bool IsDeleted { get; set; } = true;    
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
