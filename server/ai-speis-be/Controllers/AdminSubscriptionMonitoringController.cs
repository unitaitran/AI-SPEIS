using ai_speis_be.Models.Enums;
using ai_speis_be.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Controllers;

[ApiController]
[Route("api/admin/subscription-monitoring")]
[Authorize(Roles = "admin,Admin")]
public class AdminSubscriptionMonitoringController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public AdminSubscriptionMonitoringController(ApplicationDbContext context) => _context = context;

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var activePremiumUsers = await _context.UserSubscriptions.CountAsync(item =>
            item.Plan.Code == "PREMIUM" && item.ExpiresAt > now && item.Status == UserSubscriptionStatus.Active,
            cancellationToken);
        var planSubscriberCounts = await _context.UserSubscriptions
            .AsNoTracking()
            .GroupBy(item => item.PlanId)
            .Select(group => new
            {
                planId = group.Key,
                subscriberCount = group.Select(item => item.UserId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);
        var quota = await _context.QuotaPeriods
            .Where(item => item.PeriodStart <= now && (item.PeriodEnd == null || item.PeriodEnd > now))
            .GroupBy(_ => 1)
            .Select(group => new
            {
                totalQuota = group.Sum(item => item.QuotaLimit),
                usedQuota = group.Sum(item => item.UsedQuota),
                reservedQuota = group.Sum(item => item.ReservedQuota)
            })
            .FirstOrDefaultAsync(cancellationToken);
        var payments = await _context.Payments
            .GroupBy(_ => 1)
            .Select(group => new
            {
                paidOrders = group.Count(item => item.Status == PaymentStatus.Paid || item.Status == PaymentStatus.PaidByReward),
                failedOrders = group.Count(item => item.Status == PaymentStatus.Failed || item.Status == PaymentStatus.Expired),
                revenueVnd = group.Where(item => item.Status == PaymentStatus.Paid).Sum(item => item.Amount),
                rewardDiscountVnd = group.Where(item => item.Status == PaymentStatus.Paid || item.Status == PaymentStatus.PaidByReward).Sum(item => item.DiscountAmount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new
        {
            generatedAt = now,
            activePremiumUsers,
            planSubscriberCounts,
            quota = quota ?? new { totalQuota = 0, usedQuota = 0, reservedQuota = 0 },
            payments = payments ?? new { paidOrders = 0, failedOrders = 0, revenueVnd = 0m, rewardDiscountVnd = 0m },
            alertsEnabled = false
        });
    }
}
