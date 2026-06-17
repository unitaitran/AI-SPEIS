using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ai_speis_be.Models
{
    [Table("SavedQuestion")]
    public class SavedQuestion
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SavedQuestionId { get; set; }
        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }
        [Required]
        [ForeignKey("Question")]
        public int QuestionId { get; set; }
        [Required]
        public DateTime SavedAt { get; set; } = DateTime.UtcNow;
        // --- Navigation Properties ---
        public virtual User User { get; set; } = null!;
        public virtual Question Question { get; set; } = null!;
    }
}
