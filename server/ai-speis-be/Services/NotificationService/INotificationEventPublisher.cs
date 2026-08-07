using ai_speis_be.Models.Enums;

namespace ai_speis_be.Services.NotificationService;

public interface INotificationEventPublisher
{
    Task PublishAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken = default);
    Task UpdateActionStatusAsync(int recipientId, NotificationRecipientRole recipientRole, NotificationEntityType entityType, string entityId, NotificationActionStatus actionStatus, CancellationToken cancellationToken = default);
}

public sealed record NotificationEvent(
    int RecipientId,
    NotificationRecipientRole RecipientRole,
    NotificationType Type,
    NotificationCategory Category,
    NotificationSeverity Severity,
    string Title,
    string Message,
    NotificationEntityType EntityType,
    string? EntityId,
    string? ActionUrl,
    string DeduplicationKey,
    object? Metadata = null,
    NotificationActionStatus ActionStatus = NotificationActionStatus.ACTIVE,
    DateTime? ExpiresAt = null);
