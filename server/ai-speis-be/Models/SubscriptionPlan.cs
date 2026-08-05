using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Models
{
    [Table("SubscriptionPlan")]
    [Index(nameof(Code), IsUnique = true)]
    public class SubscriptionPlan
    {
        [Key]
        public int PlanId { get; set; }

        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        public int InterviewQuota { get; set; }

        public int? QuotaResetDays { get; set; }

        public bool IsFree { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; } = true;

        [Required, MaxLength(20)]
        public string AiTier { get; set; } = "ADVANCED";

        public bool AdvancedAnalyticsEnabled { get; set; }

        public bool IsPopular { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual ICollection<SubscriptionPrice> Prices { get; set; } = new List<SubscriptionPrice>();
    }

    [Table("SubscriptionPrice")]
    [Index(nameof(PlanId), nameof(BillingCycle), nameof(IsActive))]
    public class SubscriptionPrice
    {
        [Key]
        public int PriceId { get; set; }

        [ForeignKey(nameof(Plan))]
        public int PlanId { get; set; }

        public BillingCycle BillingCycle { get; set; }

        public int BillingCycleCount { get; set; } = 1;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required, MaxLength(3)]
        public string Currency { get; set; } = "VND";

        public DateTime EffectiveFrom { get; set; }

        public DateTime? EffectiveTo { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual SubscriptionPlan Plan { get; set; } = null!;
    }

}
