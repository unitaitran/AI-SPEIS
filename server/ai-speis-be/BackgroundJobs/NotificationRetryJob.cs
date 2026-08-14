using ai_speis_be.Services.NotificationService;
using Hangfire;

namespace ai_speis_be.BackgroundJobs;

[Queue("notifications")]
[DisableConcurrentExecution(timeoutInSeconds: 1800)]
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 300, 900, 3600 }, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
public sealed class NotificationRetryJob
{
    private readonly INotificationEmailDeliveryService _deliveryService;
    private readonly ILogger<NotificationRetryJob> _logger;

    public NotificationRetryJob(INotificationEmailDeliveryService deliveryService, ILogger<NotificationRetryJob> logger)
    {
        _deliveryService = deliveryService;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var result = await _deliveryService.RetryFailedAsync(DateTime.UtcNow, CancellationToken.None);
        _logger.LogInformation("Transactional email retry completed. Attempted: {Attempted}; sent: {Sent}; failed: {Failed}.", result.Attempted, result.Sent, result.Failed);
    }
}
