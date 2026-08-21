using System.Security.Claims;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.RewardService;
using ai_speis_be.Services.SubscriptionService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Controllers;

[ApiController]
[Route("api/subscription")]
[Authorize]
public class SubscriptionController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IRewardService _rewardService;

    public SubscriptionController(ApplicationDbContext context, ISubscriptionService subscriptionService, IRewardService rewardService)
    {
        _context = context;
        _subscriptionService = subscriptionService;
        _rewardService = rewardService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue("UserId"), out var userId)) return Unauthorized();
        var user = await _context.Users.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (user == null) return NotFound();

        var now = DateTime.UtcNow;
        var quota = await _subscriptionService.GetQuotaAsync(user, now, cancellationToken);
        var rewardPoints = await _rewardService.GetAvailablePointsAsync(userId, cancellationToken);
        
        var userSubscription = await _context.UserSubscriptions
            .Include(us => us.Plan)
            .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == UserSubscriptionStatus.Active, cancellationToken);

        var activeTerm = await _context.SubscriptionTerms
            .Include(term => term.Price)
            .Where(term => term.UserSubscription.UserId == userId
                && (term.Status == SubscriptionTermStatus.Active || (term.Status == SubscriptionTermStatus.Scheduled && term.EndsAt > now))
                && term.EndsAt > now)
            .OrderByDescending(term => term.Price.BillingCycle)
            .ThenByDescending(term => term.StartsAt)
            .FirstOrDefaultAsync(cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            planCode = quota.PlanCode,
            planId = userSubscription?.PlanId ?? (quota.PlanCode == "FREE" ? 1 : (int?)null),
            planName = userSubscription?.Plan?.Name,
            priceId = activeTerm?.PriceId,
            billingCycle = activeTerm != null ? ((int)activeTerm.Price.BillingCycle).ToString() : null,
            billingCycleName = activeTerm?.Price?.BillingCycle.ToString(),
            remainingInterviewQuota = quota.Remaining,
            maxInterviewQuota = quota.Limit,
            quotaPeriodEndsAt = quota.PeriodEndsAt,
            subscriptionExpiresAt = quota.SubscriptionExpiresAt,
            freeInterviewQuotaRemaining = user.FreeInterviewQuotaRemaining,
            rewardPoints,
            pointValueVnd = 1,
            pointsExpire = false
        });
    }
}
