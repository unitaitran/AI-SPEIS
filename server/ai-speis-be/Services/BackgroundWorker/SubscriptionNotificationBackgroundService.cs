using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.NotificationService;
using ai_speis_be.Services.SubscriptionService;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Services.BackgroundWorker;

public sealed class SubscriptionNotificationBackgroundService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SubscriptionNotificationBackgroundService> _logger;

    public SubscriptionNotificationBackgroundService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<SubscriptionNotificationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { _logger.LogError(exception, "Subscription notification scheduler failed."); }
            try { await Task.Delay(CheckInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationEventPublisher>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var subscriptionService = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        var now = DateTime.UtcNow;
        var reminderDays = Math.Clamp(_configuration.GetValue<int?>("Notification:SubscriptionExpiryReminderDays") ?? 7, 1, 30);
        var reminderCutoff = now.AddDays(reminderDays);
        var subscriptions = await context.UserSubscriptions.Include(item => item.Plan).Include(item => item.User)
            .Where(item => item.Status == UserSubscriptionStatus.Active && !item.Plan.IsFree && item.ExpiresAt != null && item.ExpiresAt <= reminderCutoff)
            .ToListAsync(cancellationToken);

        foreach (var subscription in subscriptions)
        {
            var expiresAt = subscription.ExpiresAt!.Value;
            if (expiresAt <= now)
            {
                await publisher.PublishAsync(new NotificationEvent(subscription.UserId, NotificationRecipientRole.USER,
                    NotificationType.SUBSCRIPTION_EXPIRED, NotificationCategory.SUBSCRIPTION, NotificationSeverity.WARNING,
                    "Subscription expired", "Your subscription has expired. Some AI-SPEIS features may no longer be available.",
                    NotificationEntityType.SUBSCRIPTION, subscription.UserSubscriptionId.ToString(), "/user/packages",
                    $"SUBSCRIPTION_EXPIRED:{subscription.UserSubscriptionId}", new { planName = subscription.Plan.Name }), cancellationToken);
                await notificationService.UpdateActionStatusAsync(subscription.UserId, NotificationRecipientRole.USER, NotificationEntityType.SUBSCRIPTION, subscription.UserSubscriptionId.ToString(), NotificationActionStatus.EXPIRED, cancellationToken);
                await subscriptionService.GetQuotaAsync(subscription.User, now, cancellationToken);
            }
            else
            {
                var reminderDate = expiresAt.Date.ToString("yyyyMMdd");
                await publisher.PublishAsync(new NotificationEvent(subscription.UserId, NotificationRecipientRole.USER,
                    NotificationType.SUBSCRIPTION_EXPIRING_SOON, NotificationCategory.SUBSCRIPTION, NotificationSeverity.WARNING,
                    "Subscription expiring soon", $"Your {subscription.Plan.Name} subscription will expire on {expiresAt:yyyy-MM-dd}.",
                    NotificationEntityType.SUBSCRIPTION, subscription.UserSubscriptionId.ToString(), "/user/packages",
                    $"SUBSCRIPTION_EXPIRING_SOON:{subscription.UserSubscriptionId}:{reminderDate}", new { planName = subscription.Plan.Name, expiryDate = expiresAt.Date }), cancellationToken);
            }
        }
        if (context.ChangeTracker.HasChanges()) await context.SaveChangesAsync(cancellationToken);
    }
}
