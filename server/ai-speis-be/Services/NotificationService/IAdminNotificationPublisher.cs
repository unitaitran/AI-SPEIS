using ai_speis_be.Models.Enums;

namespace ai_speis_be.Services.NotificationService;

public interface IAdminNotificationPublisher
{
    Task PublishAsync(AdminNotificationEvent notificationEvent, CancellationToken cancellationToken = default);
}

public sealed record AdminNotificationEvent(
    int AffectedUserId,
    NotificationType Type,
    NotificationCategory Category,
    NotificationSeverity Severity,
    string Title,
    string Message,
    NotificationEntityType EntityType,
    string? EntityId,
    string? ActionUrl,
    string DeduplicationKey,
    IReadOnlyDictionary<string, object?>? Metadata = null,
    NotificationActionStatus ActionStatus = NotificationActionStatus.ACTIVE);
