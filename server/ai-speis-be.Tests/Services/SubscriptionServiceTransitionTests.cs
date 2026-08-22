using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.SubscriptionService;
using ai_speis_be.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Tests.Services;

public sealed class SubscriptionServiceTransitionTests : IDisposable
{
    private readonly ApplicationDbContext _context = TestDbContextFactory.Create();
    private readonly SubscriptionService _service;

    public SubscriptionServiceTransitionTests() => _service = new SubscriptionService(_context);

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task SameCodeRenewal_AppendsDurationWithoutResettingQuota()
    {
        var now = new DateTime(2026, 8, 22, 8, 0, 0, DateTimeKind.Utc);
        var premium = await _context.SubscriptionPlans.SingleAsync(plan => plan.Code == "PREMIUM");
        var monthly = await _context.SubscriptionPrices.SingleAsync(price =>
            price.PlanId == premium.PlanId && price.BillingCycle == BillingCycle.Monthly);
        var user = AddUser(31, now, remainingQuota: 7);
        var originalExpiry = now.AddDays(10);
        var subscription = AddSubscription(user, premium, monthly, now.AddDays(-20), originalExpiry, usedQuota: 8);
        var payment = AddPaidPayment(user.UserId, monthly.PriceId, now);
        await _context.SaveChangesAsync();

        await _service.ActivateFromPaymentAsync(payment);
        await _context.SaveChangesAsync();

        var renewed = await LoadSubscriptionAsync(user.UserId);
        var newTerm = renewed.Terms.Single(term => term.SourcePaymentId == payment.PaymentId);
        Assert.Equal(originalExpiry, newTerm.StartsAt);
        Assert.Equal(originalExpiry.AddMonths(1), newTerm.EndsAt);
        Assert.Equal(SubscriptionTermStatus.Scheduled, newTerm.Status);
        Assert.Equal(originalExpiry.AddMonths(1), renewed.ExpiresAt);
        Assert.Equal(7, user.RemainingInterviewQuota);
        Assert.Single(renewed.QuotaPeriods);
    }

    [Fact]
    public async Task HigherDifferentCode_ReplacesTermsAndStartsWithFullTargetQuota()
    {
        var now = new DateTime(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc);
        var premium = await _context.SubscriptionPlans.SingleAsync(plan => plan.Code == "PREMIUM");
        var premiumYearly = await _context.SubscriptionPrices.SingleAsync(price =>
            price.PlanId == premium.PlanId && price.BillingCycle == BillingCycle.Yearly);
        var (vip, vipMonthly) = AddVipPlan(now);
        var user = AddUser(32, now, remainingQuota: 10);
        var subscription = AddSubscription(user, premium, premiumYearly, now.AddMonths(-2), now.AddMonths(10), usedQuota: 5);
        subscription.Terms.Add(new SubscriptionTerm
        {
            Price = premiumYearly,
            PriceId = premiumYearly.PriceId,
            StartsAt = now.AddMonths(10),
            EndsAt = now.AddMonths(22),
            Status = SubscriptionTermStatus.Scheduled,
            CreatedAt = now.AddDays(-1),
        });
        var payment = AddPaidPayment(user.UserId, vipMonthly.PriceId, now);
        await _context.SaveChangesAsync();

        await _service.ActivateFromPaymentAsync(payment);
        await _context.SaveChangesAsync();

        var upgraded = await LoadSubscriptionAsync(user.UserId);
        Assert.Equal(vip.PlanId, upgraded.PlanId);
        Assert.Equal(now.AddMonths(1), upgraded.ExpiresAt);
        Assert.All(upgraded.Terms.Where(term => term.SourcePaymentId != payment.PaymentId),
            term => Assert.Equal(SubscriptionTermStatus.Cancelled, term.Status));
        var vipTerm = upgraded.Terms.Single(term => term.SourcePaymentId == payment.PaymentId);
        Assert.Equal(SubscriptionTermStatus.Active, vipTerm.Status);
        Assert.Equal(now, vipTerm.StartsAt);
        Assert.Equal(now.AddMonths(1), vipTerm.EndsAt);
        Assert.Equal(30, user.RemainingInterviewQuota);
        Assert.Equal(30, upgraded.QuotaPeriods.Single(period => period.PeriodStart == now).QuotaLimit);
        Assert.Equal(now, upgraded.QuotaPeriods.Single(period => period.PeriodStart < now).PeriodEnd);
    }

    [Fact]
    public async Task DifferentCodeWithLowerQuota_IsRejected()
    {
        var now = DateTime.UtcNow;
        var premium = await _context.SubscriptionPlans.SingleAsync(plan => plan.Code == "PREMIUM");
        var premiumMonthly = await _context.SubscriptionPrices.SingleAsync(price =>
            price.PlanId == premium.PlanId && price.BillingCycle == BillingCycle.Monthly);
        var (vip, _) = AddVipPlan(now);
        var user = AddUser(33, now, remainingQuota: 30);
        AddSubscription(user, vip, vip.Prices.Single(), now.AddDays(-1), now.AddMonths(1), usedQuota: 0);
        await _context.SaveChangesAsync();

        var result = await _service.CanPurchaseAsync(user.UserId, premiumMonthly.PriceId);

        Assert.False(result.Allowed);
        Assert.Equal("SUBSCRIPTION_DOWNGRADE_NOT_ALLOWED", result.ErrorCode);
    }

    private User AddUser(int userId, DateTime now, int remainingQuota)
    {
        var user = new User
        {
            UserId = userId,
            RoleId = 1,
            FullName = $"User {userId}",
            Email = $"user{userId}@example.com",
            CreatedAt = now.AddYears(-1),
            IsPremium = true,
            RemainingInterviewQuota = remainingQuota,
            FreeInterviewQuotaRemaining = 3,
        };
        _context.Users.Add(user);
        return user;
    }

    private UserSubscription AddSubscription(
        User user,
        SubscriptionPlan plan,
        SubscriptionPrice price,
        DateTime startsAt,
        DateTime expiresAt,
        int usedQuota)
    {
        user.PremiumExpireAt = expiresAt;
        user.LastQuotaResetAt = startsAt;
        var subscription = new UserSubscription
        {
            User = user,
            UserId = user.UserId,
            Plan = plan,
            PlanId = plan.PlanId,
            Status = UserSubscriptionStatus.Active,
            StartedAt = startsAt,
            ExpiresAt = expiresAt,
            CreatedAt = startsAt,
        };
        subscription.Terms.Add(new SubscriptionTerm
        {
            Price = price,
            PriceId = price.PriceId,
            StartsAt = startsAt,
            EndsAt = expiresAt,
            Status = SubscriptionTermStatus.Active,
            CreatedAt = startsAt,
        });
        subscription.QuotaPeriods.Add(new QuotaPeriod
        {
            PeriodStart = startsAt,
            PeriodEnd = expiresAt,
            QuotaLimit = plan.InterviewQuota,
            UsedQuota = usedQuota,
        });
        _context.UserSubscriptions.Add(subscription);
        return subscription;
    }

    private (SubscriptionPlan Plan, SubscriptionPrice MonthlyPrice) AddVipPlan(DateTime now)
    {
        var vip = new SubscriptionPlan
        {
            Code = "VIP",
            Name = "VIP",
            InterviewQuota = 30,
            QuotaResetDays = 30,
            IsFree = false,
            IsActive = true,
            AiTier = "ADVANCED",
        };
        var monthly = new SubscriptionPrice
        {
            Plan = vip,
            BillingCycle = BillingCycle.Monthly,
            BillingCycleCount = 1,
            Amount = 99_000m,
            Currency = "VND",
            EffectiveFrom = now.AddDays(-1),
            IsActive = true,
        };
        vip.Prices.Add(monthly);
        _context.SubscriptionPlans.Add(vip);
        return (vip, monthly);
    }

    private Payment AddPaidPayment(int userId, int priceId, DateTime paidAt)
    {
        var payment = new Payment
        {
            UserId = userId,
            PackageId = priceId,
            PriceId = priceId,
            Amount = 0,
            OriginalAmount = 0,
            Currency = "VND",
            OrderCode = $"ORDER-{Guid.NewGuid():N}",
            Status = PaymentStatus.Paid,
            CreatedAt = paidAt,
            PaidAt = paidAt,
        };
        _context.Payments.Add(payment);
        return payment;
    }

    private Task<UserSubscription> LoadSubscriptionAsync(int userId) =>
        _context.UserSubscriptions
            .Include(subscription => subscription.Terms)
            .Include(subscription => subscription.QuotaPeriods)
            .SingleAsync(subscription => subscription.UserId == userId);
}
