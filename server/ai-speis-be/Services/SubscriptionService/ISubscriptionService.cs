using ai_speis_be.Models;

namespace ai_speis_be.Services.SubscriptionService;

public sealed record UserQuotaSnapshot(int Remaining, int Limit, string PlanCode, DateTime? PeriodEndsAt, DateTime? SubscriptionExpiresAt);

public interface ISubscriptionService
{
    Task<(bool Allowed, string? ErrorCode, string? ErrorMessage)> CanPurchaseAsync(int userId, int priceId, CancellationToken cancellationToken = default);
    Task ActivateFromPaymentAsync(Payment payment, CancellationToken cancellationToken = default);
    Task<UserQuotaSnapshot> GetQuotaAsync(User user, DateTime now, CancellationToken cancellationToken = default);
    Task<UserQuotaSnapshot> ConsumeCampaignQuotaAsync(User user, int interviewCampaignId, DateTime now, CancellationToken cancellationToken = default);
}
