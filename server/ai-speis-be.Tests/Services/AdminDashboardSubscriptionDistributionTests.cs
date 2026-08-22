using ai_speis_be.DTOs.GoogleQuota;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.AdminDashboardService;
using ai_speis_be.Services.GoogleQuotaService;
using ai_speis_be.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;

namespace ai_speis_be.Tests.Services;

public sealed class AdminDashboardSubscriptionDistributionTests : IDisposable
{
    private readonly ApplicationDbContext _context = TestDbContextFactory.Create();

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task Dashboard_GroupsCurrentSubscriptionsByPlanCode()
    {
        var now = DateTime.UtcNow;
        var vip = new SubscriptionPlan
        {
            Code = "VIP",
            Name = "VIP",
            InterviewQuota = 30,
            QuotaResetDays = 30,
            IsFree = false,
            IsActive = true,
            AiTier = "ADVANCED"
        };
        _context.SubscriptionPlans.Add(vip);
        AddUser(71, "free@example.com", now);
        var vipUser = AddUser(72, "vip@example.com", now);
        _context.UserSubscriptions.Add(new UserSubscription
        {
            User = vipUser,
            Plan = vip,
            Status = UserSubscriptionStatus.Active,
            StartedAt = now,
            ExpiresAt = now.AddMonths(1),
            CreatedAt = now
        });
        await _context.SaveChangesAsync();

        var quotaService = new Mock<IGoogleQuotaService>();
        quotaService.Setup(service => service.GetQuotaOverviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleQuotaResponseDto());
        var costService = new Mock<ICloudCostService>();
        costService.Setup(service => service.GetCloudCostAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CloudCostDto());
        var service = new AdminDashboardService(
            _context,
            quotaService.Object,
            costService.Object,
            Mock.Of<ILogger<AdminDashboardService>>());

        var dashboard = await service.GetDashboardOverviewAsync();

        Assert.Equal(1, dashboard.Overview.PaidUsers);
        Assert.Equal(1, dashboard.Overview.FreeUsers);
        var free = Assert.Single(dashboard.Subscriptions.Distribution, item => item.Label == "FREE");
        var vipDistribution = Assert.Single(dashboard.Subscriptions.Distribution, item => item.Label == "VIP");
        Assert.Equal(1, free.Count);
        Assert.Equal(1, vipDistribution.Count);
        Assert.Equal(50d, free.Percentage);
        Assert.Equal(50d, vipDistribution.Percentage);
    }

    private User AddUser(int userId, string email, DateTime now)
    {
        var user = new User
        {
            UserId = userId,
            RoleId = 1,
            FullName = email,
            Email = email,
            Status = true,
            CreatedAt = now
        };
        _context.Users.Add(user);
        return user;
    }
}
