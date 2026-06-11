using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using  Microsoft.EntityFrameworkCore;


namespace ai_speis_be.Models
{
    [Table("User")]
    [Index(nameof(UserId), Name = "IX_User_UserId", IsUnique = true)]
    [Index(nameof(Email), Name = "IX_User_Email")]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserId { get; set; }

        [Required]
        [ForeignKey("Role")]
        public int RoleId { get; set; }

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Phone]
        public string? PhoneNumber { get; set; }
       
        public string? PasswordHash { get; set; } 

        public bool Status { get; set; } = true;
        
        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // Navigation property
        public virtual  Role Role { get; set; } = null!;
        
    }
}
