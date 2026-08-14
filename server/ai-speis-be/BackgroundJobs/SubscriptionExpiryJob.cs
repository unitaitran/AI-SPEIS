using ai_speis_be.Services.SubscriptionService;
using Hangfire;

namespace ai_speis_be.BackgroundJobs;

[Queue("subscriptions")]
[DisableConcurrentExecution(timeoutInSeconds: 1800)]
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 300, 900, 3600 }, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
public sealed class SubscriptionExpiryJob
{
    private readonly ISubscriptionMaintenanceService _subscriptionMaintenanceService;
    private readonly ILogger<SubscriptionExpiryJob> _logger;

    public SubscriptionExpiryJob(
        ISubscriptionMaintenanceService subscriptionMaintenanceService,
        ILogger<SubscriptionExpiryJob> logger)
    {
        _subscriptionMaintenanceService = subscriptionMaintenanceService;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        try
        {
            var count = await _subscriptionMaintenanceService.ReconcileExpiredEntitlementsAsync(DateTime.UtcNow, CancellationToken.None);
            _logger.LogInformation("Subscription expiry reconciliation completed for {SubscriptionCount} subscriptions.", count);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Subscription expiry reconciliation failed and will be retried by Hangfire.");
            throw;
        }
    }
}
