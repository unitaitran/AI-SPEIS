using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Models
{
    [Table("Payment")]
    [Index(nameof(OrderCode), Name = "IX_Payment_OrderCode", IsUnique = true)]
    [Index(nameof(UserId), Name = "IX_Payment_UserId")]
    [Index(nameof(Status), Name = "IX_Payment_Status")]
    public class Payment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PaymentId { get; set; }

        [Required]
        [ForeignKey(nameof(User))]
        public int UserId { get; set; }

        [Required]
        public int PackageId { get; set; }

        [ForeignKey(nameof(SubscriptionPrice))]
        public int? PriceId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OriginalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        public int RewardPointsUsed { get; set; }

        [Required, MaxLength(3)]
        public string Currency { get; set; } = "VND";

        [Required]
        [MaxLength(64)]
        public string OrderCode { get; set; } = string.Empty;

        [Required]
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? PaidAt { get; set; }

        public DateTime? ExpiredAt { get; set; }

        [MaxLength(100)]
        public string? ProviderTransactionId { get; set; }

        [MaxLength(500)]
        public string? FailureReason { get; set; }

        public virtual User User { get; set; } = null!;
        public virtual SubscriptionPrice? SubscriptionPrice { get; set; }
    }
}
