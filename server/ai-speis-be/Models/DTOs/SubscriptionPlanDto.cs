using System.ComponentModel.DataAnnotations;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.Models.DTOs
{
    public sealed class SubscriptionPriceDto
    {
        public int PriceId { get; init; }
        public BillingCycle BillingCycle { get; init; }
        public int BillingCycleCount { get; init; }
        public decimal Amount { get; init; }
        public string Currency { get; init; } = "VND";
        public DateTime EffectiveFrom { get; init; }
        public DateTime? EffectiveTo { get; init; }
        public bool IsActive { get; init; }
    }

    public sealed class PlanFeatureDto
    {
        public int PlanFeatureId { get; init; }
        public string FeatureCode { get; init; } = string.Empty;
        public int? LimitValue { get; init; }
        public int DisplayOrder { get; init; }
        public bool IsEnabled { get; init; }
    }

    public sealed class SubscriptionPlanDto
    {
        public int PlanId { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public int InterviewQuota { get; init; }
        public int? QuotaResetDays { get; init; }
        public bool IsFree { get; init; }
        public int DisplayOrder { get; init; }
        public bool IsActive { get; init; }
        public IReadOnlyList<SubscriptionPriceDto> Prices { get; init; } = Array.Empty<SubscriptionPriceDto>();
        public IReadOnlyList<PlanFeatureDto> Features { get; init; } = Array.Empty<PlanFeatureDto>();
    }

    public class CreateSubscriptionPlanRequestDto
    {
        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Range(0, 1_000_000)]
        public int InterviewQuota { get; set; }

        [Range(1, 3650)]
        public int? QuotaResetDays { get; set; }

        public bool IsFree { get; set; }

        public int DisplayOrder { get; set; }
    }

    public sealed class UpdateSubscriptionPlanRequestDto : CreateSubscriptionPlanRequestDto
    {
    }

    public class CreateSubscriptionPriceRequestDto
    {
        public BillingCycle BillingCycle { get; set; }

        [Range(1, 120)]
        public int BillingCycleCount { get; set; } = 1;

        [Range(typeof(decimal), "0", "999999999999")]
        public decimal Amount { get; set; }

        [Required, StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "VND";

        public DateTime EffectiveFrom { get; set; }

        public DateTime? EffectiveTo { get; set; }
    }

    public sealed class UpdateSubscriptionPriceRequestDto : CreateSubscriptionPriceRequestDto
    {
    }

    public sealed class SetActiveRequestDto
    {
        public bool IsActive { get; set; }
    }
}
