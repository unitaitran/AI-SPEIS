using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.EmailService;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Services.NotificationService;

// SQL-backed transactional email outbox. The lease makes concurrent workers skip an
// in-flight delivery; successful records are terminal and never selected for retry.
public sealed class NotificationEmailDeliveryService : INotificationEmailDeliveryService
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationEmailDeliveryService> _logger;

    public NotificationEmailDeliveryService(ApplicationDbContext context, IEmailSender emailSender, IConfiguration configuration, ILogger<NotificationEmailDeliveryService> logger)
    {
        _context = context;
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<bool> AttemptPendingAsync(long notificationId, CancellationToken cancellationToken = default) =>
        AttemptAsync(notificationId, DateTime.UtcNow, includePending: true, cancellationToken);

    public async Task<NotificationEmailRetryResult> RetryFailedAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var batchSize = Math.Clamp(_configuration.GetValue<int?>("Notification:EmailDelivery:RetryBatchSize") ?? 100, 1, 500);
        var maxAttempts = MaxAttempts;
        var ids = await _context.Notifications.AsNoTracking()
            .Where(item => item.DeliveryChannel == DeliveryChannel.EMAIL
                && item.DeliveryStatus == DeliveryStatus.Failed
                && item.RetryCount < maxAttempts
                && item.NextRetryAt != null && item.NextRetryAt <= now
                && (item.DeliveryLeaseExpiresAt == null || item.DeliveryLeaseExpiresAt <= now))
            .OrderBy(item => item.NextRetryAt)
            .Select(item => item.NotificationId)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var sent = 0;
        var failed = 0;
        foreach (var id in ids)
        {
            try
            {
                if (await AttemptAsync(id, now, includePending: false, cancellationToken)) sent++;
                else failed++;
            }
            catch (Exception exception)
            {
                // Individual records are isolated; never log recipient, subject or body.
                _logger.LogError(exception, "Transactional email retry failed for one notification record.");
                failed++;
            }
            finally
            {
                _context.ChangeTracker.Clear();
            }
        }

        return new NotificationEmailRetryResult(ids.Count, sent, failed);
    }

    private async Task<bool> AttemptAsync(long notificationId, DateTime now, bool includePending, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString("N");
        var maxAttempts = MaxAttempts;
        var lease = now.AddMinutes(Math.Clamp(_configuration.GetValue<int?>("Notification:EmailDelivery:LeaseMinutes") ?? 10, 1, 60));
        var claimed = await _context.Notifications
            .Where(item => item.NotificationId == notificationId
                && item.DeliveryChannel == DeliveryChannel.EMAIL
                && item.DeliveryStatus != DeliveryStatus.Sent
                && item.RetryCount < maxAttempts
                && (item.DeliveryLeaseExpiresAt == null || item.DeliveryLeaseExpiresAt <= now)
                && (includePending
                    ? (item.DeliveryStatus == DeliveryStatus.Pending || (item.DeliveryStatus == DeliveryStatus.Failed && item.NextRetryAt <= now))
                    : item.DeliveryStatus == DeliveryStatus.Failed && item.NextRetryAt <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.DeliveryLeaseToken, token)
                .SetProperty(item => item.DeliveryLeaseExpiresAt, lease)
                .SetProperty(item => item.LastAttemptAt, now)
                .SetProperty(item => item.RetryCount, item => item.RetryCount + 1), cancellationToken);
        if (claimed == 0) return false;

        var delivery = await _context.Notifications.AsNoTracking()
            .Include(item => item.Recipient)
            .Where(item => item.NotificationId == notificationId && item.DeliveryLeaseToken == token)
            .Select(item => new { item.EmailSubject, item.EmailBody, item.Recipient.Email, item.RetryCount })
            .FirstOrDefaultAsync(cancellationToken);
        if (delivery is null || string.IsNullOrWhiteSpace(delivery.EmailSubject) || string.IsNullOrWhiteSpace(delivery.EmailBody))
        {
            await MarkFailureAsync(notificationId, token, now, 1, "InvalidDeliveryContent", cancellationToken);
            return false;
        }

        try
        {
            await _emailSender.SendEmailAsync(delivery.Email, delivery.EmailSubject, delivery.EmailBody);
            return await _context.Notifications
                .Where(item => item.NotificationId == notificationId && item.DeliveryLeaseToken == token && item.DeliveryStatus != DeliveryStatus.Sent)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.DeliveryStatus, DeliveryStatus.Sent)
                    .SetProperty(item => item.LastError, (string?)null)
                    .SetProperty(item => item.NextRetryAt, (DateTime?)null)
                    .SetProperty(item => item.DeliveryLeaseToken, (string?)null)
                    .SetProperty(item => item.DeliveryLeaseExpiresAt, (DateTime?)null)
                    .SetProperty(item => item.UpdatedAt, now), cancellationToken) == 1;
        }
        catch (Exception exception)
        {
            await MarkFailureAsync(notificationId, token, now, delivery.RetryCount, exception.GetType().Name, cancellationToken);
            return false;
        }
    }

    private Task<int> MarkFailureAsync(long notificationId, string token, DateTime now, int attempt, string error, CancellationToken cancellationToken)
    {
        var delayMinutes = Math.Min(10080, Math.Max(1, _configuration.GetValue<int?>("Notification:EmailDelivery:RetryBaseMinutes") ?? 60) * Math.Pow(2, Math.Max(0, attempt - 1)));
        var safeError = error.Length <= 500 ? error : error[..500];
        return _context.Notifications
            .Where(item => item.NotificationId == notificationId && item.DeliveryLeaseToken == token && item.DeliveryStatus != DeliveryStatus.Sent)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.DeliveryStatus, DeliveryStatus.Failed)
                .SetProperty(item => item.LastError, safeError)
                .SetProperty(item => item.NextRetryAt, now.AddMinutes(delayMinutes))
                .SetProperty(item => item.DeliveryLeaseToken, (string?)null)
                .SetProperty(item => item.DeliveryLeaseExpiresAt, (DateTime?)null)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);
    }

    private int MaxAttempts => Math.Clamp(_configuration.GetValue<int?>("Notification:EmailDelivery:MaxAttempts") ?? 5, 1, 20);
}
