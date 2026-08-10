namespace ai_speis_be.Services.NotificationService;

public sealed record NotificationEmailRetryResult(int Attempted, int Sent, int Failed);

public interface INotificationEmailDeliveryService
{
    Task<bool> AttemptPendingAsync(long notificationId, CancellationToken cancellationToken = default);
    Task<NotificationEmailRetryResult> RetryFailedAsync(DateTime now, CancellationToken cancellationToken = default);
}
