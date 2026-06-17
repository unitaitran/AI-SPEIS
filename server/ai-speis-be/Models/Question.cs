using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ai_speis_be.Models
{
    [Table("Question")]
    [Index(nameof(QuestionId), Name = "IX_Question_QuestionId", IsUnique = true)]
    [Index(nameof(UserId), Name = "IX_Question_UserId")]
    public class Question
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int QuestionId { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }
        [Required]
        public string QuestionContent { get; set; } = string.Empty;
        [Required]
        public string SuggestedAnswer { get; set; } = string.Empty;
        [Required]
        public QuestionDifficultyEnum Difficulty { get; set; }
        [Required]
        public string RoleTarget { get; set; } = string.Empty;
        [Required]
        public string Major { get; set; } = string.Empty;
        [Required]
        public bool IsDeleted { get; set; } = false;
        [Required]
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // --- Navigation Properties ---
        public virtual User User { get; set; } = null!;




    }
}
