using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Services.SubscriptionService;

public class SubscriptionService : ISubscriptionService
{
    private const string CampaignReference = "InterviewCampaign";
    private readonly ApplicationDbContext _context;

    public SubscriptionService(ApplicationDbContext context) => _context = context;

    public async Task<(bool Allowed, string? ErrorCode, string? ErrorMessage)> CanPurchaseAsync(
        int userId,
        int priceId,
        CancellationToken cancellationToken = default)
    {
        var price = await _context.SubscriptionPrices
            .Include(item => item.Plan)
            .FirstOrDefaultAsync(item => item.PriceId == priceId, cancellationToken);
        if (price == null || !price.IsActive || !price.Plan.IsActive || price.Plan.IsFree)
            return (false, "SUBSCRIPTION_PRICE_NOT_AVAILABLE", "Gói hoặc mức giá không còn khả dụng.");

        var now = DateTime.UtcNow;
        if (price.EffectiveFrom > now || price.EffectiveTo <= now)
            return (false, "SUBSCRIPTION_PRICE_NOT_AVAILABLE", "Mức giá chưa có hiệu lực hoặc đã hết hiệu lực.");

        if (price.BillingCycle != BillingCycle.Monthly) return (true, null, null);

        var activeYearlyTerm = await _context.SubscriptionTerms
            .Include(term => term.Price)
            .AnyAsync(term => term.UserSubscription.UserId == userId
                && term.Status == SubscriptionTermStatus.Active
                && term.StartsAt <= now
                && term.EndsAt > now
                && term.Price.BillingCycle == BillingCycle.Yearly,
                cancellationToken);

        return activeYearlyTerm
            ? (false, "SUBSCRIPTION_DOWNGRADE_NOT_ALLOWED", "Không thể đăng ký gói tháng khi gói năm vẫn còn hiệu lực.")
            : (true, null, null);
    }

    public async Task ActivateFromPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        if (await _context.SubscriptionTerms.AnyAsync(term => term.SourcePaymentId == payment.PaymentId, cancellationToken)) return;

        var priceId = payment.PriceId ?? payment.PackageId;
        var price = await _context.SubscriptionPrices
            .Include(item => item.Plan)
            .FirstAsync(item => item.PriceId == priceId, cancellationToken);
        var user = await _context.Users.FirstAsync(item => item.UserId == payment.UserId, cancellationToken);
        var subscription = await EnsureSubscriptionAsync(user, DateTime.UtcNow, cancellationToken);
        var now = payment.PaidAt ?? DateTime.UtcNow;
        await SynchronizeAsync(subscription, user, now, cancellationToken);

        var activeTerm = subscription.Terms
            .Where(term => term.Status == SubscriptionTermStatus.Active && term.StartsAt <= now && term.EndsAt > now)
            .OrderByDescending(term => term.StartsAt)
            .FirstOrDefault();
        if (price.BillingCycle == BillingCycle.Monthly && activeTerm?.Price.BillingCycle == BillingCycle.Yearly)
            throw new InvalidOperationException("SUBSCRIPTION_DOWNGRADE_NOT_ALLOWED");

        var startsAt = subscription.Plan.IsFree || subscription.ExpiresAt == null || subscription.ExpiresAt <= now
            ? now
            : subscription.ExpiresAt.Value;
        var endsAt = price.BillingCycle switch
        {
            BillingCycle.Monthly => startsAt.AddMonths(price.BillingCycleCount),
            BillingCycle.Yearly => startsAt.AddYears(price.BillingCycleCount),
            _ => throw new InvalidOperationException("Unsupported billing cycle.")
        };

        subscription.Terms.Add(new SubscriptionTerm
        {
            PriceId = price.PriceId,
            SourcePaymentId = payment.PaymentId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = startsAt <= now ? SubscriptionTermStatus.Active : SubscriptionTermStatus.Scheduled,
            CreatedAt = now,
            Price = price
        });
        subscription.ExpiresAt = endsAt;
        subscription.Status = UserSubscriptionStatus.Active;
        subscription.UpdatedAt = now;

        // A new premium activation receives a fresh 15-quota period. Early renewal or
        // Monthly -> Yearly only extends/schedules the next term and keeps current quota.
        if (startsAt <= now)
        {
            subscription.PlanId = price.PlanId;
            subscription.Plan = price.Plan;
            subscription.StartedAt = now;
            CreateQuotaPeriod(subscription, price.Plan, now);
            user.RemainingInterviewQuota = price.Plan.InterviewQuota;
            user.LastQuotaResetAt = now;
        }

        user.IsPremium = true;
        user.PremiumExpireAt = endsAt;
        user.UpdatedAt = now;
    }

    public async Task<UserQuotaSnapshot> GetQuotaAsync(
        User user,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var subscription = await EnsureSubscriptionAsync(user, now, cancellationToken);
        await SynchronizeAsync(subscription, user, now, cancellationToken);
        return BuildSnapshot(subscription, user, now);
    }

    public async Task<UserQuotaSnapshot> ConsumeCampaignQuotaAsync(
        User user,
        int interviewCampaignId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var subscription = await EnsureSubscriptionAsync(user, now, cancellationToken);
        await SynchronizeAsync(subscription, user, now, cancellationToken);
        var period = GetCurrentPeriod(subscription, now);
        var referenceId = interviewCampaignId.ToString();
        var alreadyConsumed = period != null && await _context.QuotaTransactions.AnyAsync(transaction =>
            transaction.QuotaPeriodId == period.QuotaPeriodId
            && transaction.Type == QuotaTransactionType.Consume
            && transaction.ReferenceType == CampaignReference
            && transaction.ReferenceId == referenceId,
            cancellationToken);

        if (!alreadyConsumed && period != null && period.UsedQuota + period.ReservedQuota < period.QuotaLimit)
        {
            period.UsedQuota++;
            _context.QuotaTransactions.Add(new QuotaTransaction
            {
                QuotaPeriod = period,
                Type = QuotaTransactionType.Consume,
                Delta = -1,
                ReferenceType = CampaignReference,
                ReferenceId = referenceId,
                Reason = "Completed interview campaign.",
                CreatedAt = now
            });
            var remaining = period.QuotaLimit - period.UsedQuota - period.ReservedQuota;
            user.RemainingInterviewQuota = remaining;
            if (subscription.Plan.IsFree) user.FreeInterviewQuotaRemaining = remaining;
            user.UpdatedAt = now;
        }

        return BuildSnapshot(subscription, user, now);
    }

    private async Task<UserSubscription> EnsureSubscriptionAsync(User user, DateTime now, CancellationToken cancellationToken)
    {
        var subscription = await _context.UserSubscriptions
            .Include(item => item.Plan)
            .Include(item => item.Terms).ThenInclude(term => term.Price).ThenInclude(price => price.Plan)
            .Include(item => item.QuotaPeriods)
            .FirstOrDefaultAsync(item => item.UserId == user.UserId, cancellationToken);
        if (subscription != null) return subscription;

        var isPremium = user.IsPremium && user.PremiumExpireAt > now;
        var plan = await _context.SubscriptionPlans.FirstAsync(item => item.Code == (isPremium ? "PREMIUM" : "FREE"), cancellationToken);
        subscription = new UserSubscription
        {
            UserId = user.UserId,
            PlanId = plan.PlanId,
            Plan = plan,
            Status = UserSubscriptionStatus.Active,
            StartedAt = isPremium ? user.LastQuotaResetAt ?? now : user.CreatedAt,
            ExpiresAt = isPremium ? user.PremiumExpireAt : null,
            CreatedAt = now
        };
        _context.UserSubscriptions.Add(subscription);
        CreateQuotaPeriod(subscription, plan, subscription.StartedAt, user.RemainingInterviewQuota);
        return subscription;
    }

    private async Task SynchronizeAsync(UserSubscription subscription, User user, DateTime now, CancellationToken cancellationToken)
    {
        foreach (var term in subscription.Terms)
        {
            if (term.EndsAt <= now && term.Status != SubscriptionTermStatus.Cancelled) term.Status = SubscriptionTermStatus.Completed;
            else if (term.StartsAt <= now && term.EndsAt > now) term.Status = SubscriptionTermStatus.Active;
        }

        var activeTerm = subscription.Terms
            .Where(term => term.Status == SubscriptionTermStatus.Active && term.StartsAt <= now && term.EndsAt > now)
            .OrderByDescending(term => term.StartsAt)
            .FirstOrDefault();

        if (subscription.ExpiresAt <= now || (subscription.ExpiresAt == null && !subscription.Plan.IsFree))
        {
            var freePlan = await _context.SubscriptionPlans.FirstAsync(item => item.Code == "FREE", cancellationToken);
            subscription.PlanId = freePlan.PlanId;
            subscription.Plan = freePlan;
            subscription.Status = UserSubscriptionStatus.Active;
            subscription.StartedAt = now;
            subscription.ExpiresAt = null;
            subscription.UpdatedAt = now;
            CreateQuotaPeriod(subscription, freePlan, now, user.FreeInterviewQuotaRemaining);
            user.IsPremium = false;
            user.PremiumExpireAt = null;
            user.LastQuotaResetAt = null;
            user.RemainingInterviewQuota = user.FreeInterviewQuotaRemaining;
            user.UpdatedAt = now;
            return;
        }

        if (activeTerm != null && subscription.PlanId != activeTerm.Price.PlanId)
        {
            subscription.PlanId = activeTerm.Price.PlanId;
            subscription.Plan = activeTerm.Price.Plan;
            subscription.StartedAt = activeTerm.StartsAt;
            subscription.UpdatedAt = now;
            CreateQuotaPeriod(subscription, subscription.Plan, activeTerm.StartsAt);
            user.RemainingInterviewQuota = subscription.Plan.InterviewQuota;
            user.LastQuotaResetAt = activeTerm.StartsAt;
        }

        if (!subscription.Plan.IsFree)
        {
            var period = GetCurrentPeriod(subscription, now)
                ?? subscription.QuotaPeriods.OrderByDescending(item => item.PeriodStart).FirstOrDefault();
            while (period?.PeriodEnd <= now && period.PeriodEnd < subscription.ExpiresAt)
            {
                var nextStart = period.PeriodEnd.Value;
                CreateQuotaPeriod(subscription, subscription.Plan, nextStart);
                period = subscription.QuotaPeriods.OrderByDescending(item => item.PeriodStart).First();
                user.RemainingInterviewQuota = subscription.Plan.InterviewQuota;
                user.LastQuotaResetAt = nextStart;
                user.UpdatedAt = now;
            }
        }
    }

    private static void CreateQuotaPeriod(UserSubscription subscription, SubscriptionPlan plan, DateTime start, int? remaining = null)
    {
        if (subscription.QuotaPeriods.Any(period => period.PeriodStart == start)) return;
        var normalizedRemaining = Math.Clamp(remaining ?? plan.InterviewQuota, 0, plan.InterviewQuota);
        subscription.QuotaPeriods.Add(new QuotaPeriod
        {
            PeriodStart = start,
            PeriodEnd = plan.QuotaResetDays.HasValue ? start.AddDays(plan.QuotaResetDays.Value) : null,
            QuotaLimit = plan.InterviewQuota,
            UsedQuota = plan.InterviewQuota - normalizedRemaining,
            ReservedQuota = 0
        });
    }

    private static QuotaPeriod? GetCurrentPeriod(UserSubscription subscription, DateTime now) =>
        subscription.QuotaPeriods
            .Where(period => period.PeriodStart <= now && (period.PeriodEnd == null || period.PeriodEnd > now))
            .OrderByDescending(period => period.PeriodStart)
            .FirstOrDefault();

    private static UserQuotaSnapshot BuildSnapshot(UserSubscription subscription, User user, DateTime now)
    {
        var period = GetCurrentPeriod(subscription, now);
        var remaining = period == null
            ? Math.Max(0, user.RemainingInterviewQuota)
            : Math.Max(0, period.QuotaLimit - period.UsedQuota - period.ReservedQuota);
        return new UserQuotaSnapshot(
            remaining,
            period?.QuotaLimit ?? subscription.Plan.InterviewQuota,
            subscription.Plan.Code,
            period?.PeriodEnd,
            subscription.ExpiresAt);
    }
}
