using System.Collections.Generic;
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

        public bool IsLocked { get; set; }

        [MaxLength(500)]
        public string? LockReason { get; set; }

        public DateTime? LockedAt { get; set; }

        public int? LockedByUserId { get; set; }

        public string? EmailConfirmationToken { get; set; }

        public DateTime? EmailConfirmationTokenExpiresAt { get; set; }

        public DateTime? EmailConfirmedAt { get; set; }

        public string? PasswordResetToken { get; set; }

        public DateTime? PasswordResetTokenExpiresAt { get; set; }

        [Required]
        public int RemainingInterviewQuota { get; set; } = 3;

        [Required]
        public int FreeInterviewQuotaRemaining { get; set; } = 3;

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public bool IsPremium { get; set; } = false;

        public DateTime? PremiumExpireAt { get; set; }

        public DateTime? LastQuotaResetAt { get; set; }

        [MaxLength(256)]
        public string? ImageUrl { get; set; }

        public bool SyncPremiumStatus(DateTime now)
        {
            if (PremiumExpireAt.HasValue)
            {
                bool shouldBePremium = PremiumExpireAt.Value > now;
                if (IsPremium != shouldBePremium)
                {
                    IsPremium = shouldBePremium;
                    UpdatedAt = now;
                    return true;
                }
            }
            return false;
        }

        // Navigation property
        public virtual  Role Role { get; set; } = null!;
        
        public virtual ICollection<CVFile> CVFiles { get; set; } = new List<CVFile>();
        public virtual ICollection<JDFile> JDFiles { get; set; } = new List<JDFile>();
    }
}
