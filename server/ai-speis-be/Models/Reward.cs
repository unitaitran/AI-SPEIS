using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Models
{
    [Table("RewardAccount")]
    public class RewardAccount
    {
        [Key, ForeignKey(nameof(User))]
        public int UserId { get; set; }

        public int AvailablePoints { get; set; }

        public int ReservedPoints { get; set; }

        public int LifetimeEarnedPoints { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual User User { get; set; } = null!;
        public virtual ICollection<RewardTransaction> Transactions { get; set; } = new List<RewardTransaction>();
    }

    [Table("RewardTransaction")]
    [Index(nameof(UserId), nameof(Type), nameof(ReferenceType), nameof(ReferenceId), IsUnique = true)]
    public class RewardTransaction
    {
        [Key]
        public long RewardTransactionId { get; set; }

        [ForeignKey(nameof(Account))]
        public int UserId { get; set; }

        public RewardTransactionType Type { get; set; }

        public int Delta { get; set; }

        [Required, MaxLength(50)]
        public string ReferenceType { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string ReferenceId { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual RewardAccount Account { get; set; } = null!;
    }

    [Table("RewardRule")]
    public class RewardRule
    {
        [Key]
        public int RewardRuleId { get; set; }

        public int PointValueVnd { get; set; } = 1;

        public bool PointsExpire { get; set; }

        public bool AllowFullPaymentByPoints { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public DateTime EffectiveFrom { get; set; }

        public DateTime? EffectiveTo { get; set; }
    }
}
