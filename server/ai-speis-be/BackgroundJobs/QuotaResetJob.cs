using ai_speis_be.Services.SubscriptionService;
using Hangfire;

namespace ai_speis_be.BackgroundJobs;

[Queue("subscriptions")]
[DisableConcurrentExecution(timeoutInSeconds: 1800)]
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 300, 900, 3600 }, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
public sealed class QuotaResetJob
{
    private readonly ISubscriptionMaintenanceService _subscriptionMaintenanceService;
    private readonly ILogger<QuotaResetJob> _logger;

    public QuotaResetJob(
        ISubscriptionMaintenanceService subscriptionMaintenanceService,
        ILogger<QuotaResetJob> logger)
    {
        _subscriptionMaintenanceService = subscriptionMaintenanceService;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        try
        {
            var count = await _subscriptionMaintenanceService.SynchronizeQuotaPeriodsAsync(DateTime.UtcNow, CancellationToken.None);
            _logger.LogInformation("Quota reset reconciliation completed for {SubscriptionCount} active paid subscriptions.", count);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Quota reset reconciliation failed and will be retried by Hangfire.");
            throw;
        }
    }
}
