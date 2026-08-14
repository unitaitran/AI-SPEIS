using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Models
{
    [Table("UserSubscription")]
    [Index(nameof(UserId), IsUnique = true)]
    public class UserSubscription
    {
        [Key]
        public int UserSubscriptionId { get; set; }

        [ForeignKey(nameof(User))]
        public int UserId { get; set; }

        [ForeignKey(nameof(Plan))]
        public int PlanId { get; set; }

        public UserSubscriptionStatus Status { get; set; } = UserSubscriptionStatus.Active;

        public DateTime StartedAt { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual User User { get; set; } = null!;
        public virtual SubscriptionPlan Plan { get; set; } = null!;
        public virtual ICollection<SubscriptionTerm> Terms { get; set; } = new List<SubscriptionTerm>();
        public virtual ICollection<QuotaPeriod> QuotaPeriods { get; set; } = new List<QuotaPeriod>();
    }

    [Table("SubscriptionTerm")]
    [Index(nameof(UserSubscriptionId), nameof(StartsAt), nameof(EndsAt))]
    public class SubscriptionTerm
    {
        [Key]
        public int SubscriptionTermId { get; set; }

        [ForeignKey(nameof(UserSubscription))]
        public int UserSubscriptionId { get; set; }

        [ForeignKey(nameof(Price))]
        public int PriceId { get; set; }

        public int? SourcePaymentId { get; set; }

        public DateTime StartsAt { get; set; }

        public DateTime EndsAt { get; set; }

        public SubscriptionTermStatus Status { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual UserSubscription UserSubscription { get; set; } = null!;
        public virtual SubscriptionPrice Price { get; set; } = null!;
    }

    [Table("QuotaPeriod")]
    [Index(nameof(UserSubscriptionId), nameof(PeriodStart), IsUnique = true)]
    public class QuotaPeriod
    {
        [Key]
        public int QuotaPeriodId { get; set; }

        [ForeignKey(nameof(UserSubscription))]
        public int UserSubscriptionId { get; set; }

        public DateTime PeriodStart { get; set; }

        public DateTime? PeriodEnd { get; set; }

        public int QuotaLimit { get; set; }

        public int UsedQuota { get; set; }

        public int ReservedQuota { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual UserSubscription UserSubscription { get; set; } = null!;
        public virtual ICollection<QuotaTransaction> Transactions { get; set; } = new List<QuotaTransaction>();
    }

    [Table("QuotaTransaction")]
    [Index(nameof(QuotaPeriodId), nameof(Type), nameof(ReferenceType), nameof(ReferenceId), IsUnique = true)]
    public class QuotaTransaction
    {
        [Key]
        public long QuotaTransactionId { get; set; }

        [ForeignKey(nameof(QuotaPeriod))]
        public int QuotaPeriodId { get; set; }

        public QuotaTransactionType Type { get; set; }

        public int Delta { get; set; }

        [MaxLength(50)]
        public string? ReferenceType { get; set; }

        [MaxLength(100)]
        public string? ReferenceId { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual QuotaPeriod QuotaPeriod { get; set; } = null!;
    }
}
