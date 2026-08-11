using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.NotificationService;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Services.SubscriptionService;

// Application-layer batch operation used by the scheduler. Quota creation and
// entitlement calculations remain in SubscriptionService, which owns that logic.
public sealed class SubscriptionMaintenanceService : ISubscriptionMaintenanceService
{
    private readonly ApplicationDbContext _context;
    private readonly ISubscriptionService _subscriptionService;
    private readonly INotificationEventPublisher _notificationPublisher;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;

    public SubscriptionMaintenanceService(
        ApplicationDbContext context,
        ISubscriptionService subscriptionService,
        INotificationEventPublisher notificationPublisher,
        INotificationService notificationService,
        IConfiguration configuration)
    {
        _context = context;
        _subscriptionService = subscriptionService;
        _notificationPublisher = notificationPublisher;
        _notificationService = notificationService;
        _configuration = configuration;
    }

    public async Task<int> ReconcileExpiredEntitlementsAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var reminderDays = Math.Clamp(_configuration.GetValue<int?>("Notification:SubscriptionExpiryReminderDays") ?? 7, 1, 30);
        var reminderCutoff = now.AddDays(reminderDays);
        var subscriptionIds = await _context.UserSubscriptions
            .AsNoTracking()
            .Where(subscription => subscription.Status == UserSubscriptionStatus.Active
                && !subscription.Plan.IsFree
                && subscription.ExpiresAt != null
                && subscription.ExpiresAt <= reminderCutoff)
            .Select(subscription => subscription.UserSubscriptionId)
            .ToListAsync(cancellationToken);

        var reconciled = 0;
        foreach (var subscriptionId in subscriptionIds)
        {
            var subscription = await _context.UserSubscriptions
                .Include(item => item.Plan)
                .Include(item => item.User)
                .FirstOrDefaultAsync(item => item.UserSubscriptionId == subscriptionId, cancellationToken);
            if (subscription is null
                || subscription.Status != UserSubscriptionStatus.Active
                || subscription.Plan.IsFree
                || subscription.ExpiresAt is null
                || subscription.ExpiresAt > reminderCutoff)
            {
                continue;
            }

            var planName = subscription.Plan.Name;
            if (subscription.ExpiresAt > now)
            {
                var reminderDate = subscription.ExpiresAt.Value.Date.ToString("yyyyMMdd");
                await _notificationPublisher.PublishAsync(new NotificationEvent(
                    subscription.UserId, NotificationRecipientRole.USER,
                    NotificationType.SUBSCRIPTION_EXPIRING_SOON, NotificationCategory.SUBSCRIPTION,
                    NotificationSeverity.WARNING, "Subscription expiring soon",
                    $"Your {planName} subscription will expire on {subscription.ExpiresAt:yyyy-MM-dd}.",
                    NotificationEntityType.SUBSCRIPTION, subscription.UserSubscriptionId.ToString(),
                    "/user/packages", $"SUBSCRIPTION_EXPIRING_SOON:{subscription.UserSubscriptionId}:{reminderDate}",
                    new { planName, expiryDate = subscription.ExpiresAt.Value.Date }), cancellationToken);
                continue;
            }

            await _notificationPublisher.PublishAsync(new NotificationEvent(
                subscription.UserId, NotificationRecipientRole.USER,
                NotificationType.SUBSCRIPTION_EXPIRED, NotificationCategory.SUBSCRIPTION,
                NotificationSeverity.WARNING, "Subscription expired",
                "Your subscription has expired. Some AI-SPEIS features may no longer be available.",
                NotificationEntityType.SUBSCRIPTION, subscription.UserSubscriptionId.ToString(),
                "/user/packages", $"SUBSCRIPTION_EXPIRED:{subscription.UserSubscriptionId}",
                new { planName },
                EmailDelivery: new TransactionalEmailContent(
                    "Subscription expired - AI-SPEIS",
                    "<p>Your subscription has expired. Your account is now using the Free entitlement.</p>")), cancellationToken);
            await _notificationService.UpdateActionStatusAsync(
                subscription.UserId, NotificationRecipientRole.USER,
                NotificationEntityType.SUBSCRIPTION, subscription.UserSubscriptionId.ToString(),
                NotificationActionStatus.EXPIRED, cancellationToken);

            // SubscriptionService is the existing entitlement authority. Its current
            // model reconciles an expired paid record to the user's active FREE plan.
            // Do not set Status=Expired here: that would be immediately overwritten by
            // the established synchronization rule and leave current-plan semantics
            // ambiguous (there is only one UserSubscription per user).
            await _subscriptionService.GetQuotaAsync(subscription.User, now, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            reconciled++;
        }

        return reconciled;
    }

    public async Task<int> SynchronizeQuotaPeriodsAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var userIds = await _context.UserSubscriptions
            .AsNoTracking()
            .Where(subscription => subscription.Status == UserSubscriptionStatus.Active
                && !subscription.Plan.IsFree
                && (subscription.ExpiresAt == null || subscription.ExpiresAt > now))
            .Select(subscription => subscription.UserId)
            .ToListAsync(cancellationToken);

        var synchronized = 0;
        foreach (var userId in userIds)
        {
            var user = await _context.Users.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
            if (user is null)
            {
                continue;
            }

            // GetQuotaAsync determines the period and uses the plan's InterviewQuota
            // and QuotaResetDays. The unique period key makes retries idempotent.
            await _subscriptionService.GetQuotaAsync(user, now, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            synchronized++;
        }

        return synchronized;
    }
}
