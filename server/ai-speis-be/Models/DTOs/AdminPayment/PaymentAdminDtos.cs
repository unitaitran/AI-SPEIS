using ai_speis_be.Models.Enums;

namespace ai_speis_be.Models.DTOs.AdminPayment
{
    public class PaginatedResultDto<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    }

    public class PaymentListDto
    {
        public int PaymentId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string? ProviderTransactionId { get; set; }
        public int UserId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PlanBefore { get; set; } = "Free";
        public string PlanAfter { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal OriginalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public int RewardPointsUsed { get; set; }
        public string Currency { get; set; } = "VND";
        public string PaymentMethod { get; set; } = "MoMo";
        public PaymentStatus Status { get; set; }
        public string StatusName => Status.ToString();
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? ExpiredAt { get; set; }
        public string? FailureReason { get; set; }
    }

    public class UserInfoDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class SubscriptionDetailInfoDto
    {
        public int? UserSubscriptionId { get; set; }
        public int? PriceId { get; set; }
        public string PlanCode { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public BillingCycle BillingCycle { get; set; }
        public string BillingCycleName => BillingCycle == BillingCycle.Yearly ? "Năm" : "Tháng";
        public string PlanBefore { get; set; } = "Free";
        public string PlanAfter { get; set; } = string.Empty;
        public DateTime? TermStartsAt { get; set; }
        public DateTime? TermEndsAt { get; set; }
    }

    public class MoMoTransactionDetailDto
    {
        public string PartnerCode { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string TransId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int? ResultCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string PayType { get; set; } = string.Empty;
        public long ResponseTime { get; set; }
        public string RawCallbackJson { get; set; } = string.Empty;
        public string RawResponseJson { get; set; } = string.Empty;
        public bool VerificationFailed { get; set; }
        public string? VerificationError { get; set; }
    }

    public class PaymentTimelineStepDto
    {
        public string Step { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime? Timestamp { get; set; }
        public string Status { get; set; } = "completed"; // completed, pending, failed
        public string? Description { get; set; }
    }

    public class SubscriptionHistoryDto
    {
        public int PaymentId { get; set; }
        public string PlanBefore { get; set; } = "Free";
        public string PlanAfter { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class PaymentDetailDto : PaymentListDto
    {
        public UserInfoDto User { get; set; } = new();
        public SubscriptionDetailInfoDto Subscription { get; set; } = new();
        public MoMoTransactionDetailDto MoMoDetails { get; set; } = new();
        public List<PaymentTimelineStepDto> Timeline { get; set; } = new();
        public List<SubscriptionHistoryDto> UpgradeHistory { get; set; } = new();
    }

    public class RevenueTrendPointDto
    {
        public string Label { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int Count { get; set; }
    }

    public class StatusDistributionDto
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class TopPlanSalesDto
    {
        public string PlanName { get; set; } = string.Empty;
        public int SalesCount { get; set; }
        public decimal Revenue { get; set; }
    }

    public class PaymentStatisticsDto
    {
        public decimal TodayRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public int SuccessfulPayments { get; set; }
        public int FailedPayments { get; set; }
        public int PendingPayments { get; set; }
        public int RefundedPayments { get; set; }
        public int NewPremiumToday { get; set; }
        public int NewPremiumThisMonth { get; set; }
        public int RenewalCount { get; set; }
        public int UpgradeCount { get; set; }
        public List<RevenueTrendPointDto> RevenueTrend { get; set; } = new();
        public List<StatusDistributionDto> StatusDistribution { get; set; } = new();
        public List<TopPlanSalesDto> TopSellingPlans { get; set; } = new();
    }
}
