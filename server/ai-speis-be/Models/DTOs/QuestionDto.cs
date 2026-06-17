using ai_speis_be.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace ai_speis_be.Models.DTOs
{
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
