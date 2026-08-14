using ai_speis_be.DTOs.Notifications;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.Services.NotificationService;

public interface INotificationService
{
    Task<Notification?> CreateAsync(NotificationCreateRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<NotificationDto>> GetForRecipientAsync(int recipientId, NotificationRecipientRole recipientRole, NotificationQueryParameters query, CancellationToken cancellationToken = default);
    Task<NotificationDto?> GetByIdAsync(long notificationId, int recipientId, NotificationRecipientRole recipientRole, CancellationToken cancellationToken = default);
    Task<bool> MarkReadAsync(long notificationId, int recipientId, NotificationRecipientRole recipientRole, CancellationToken cancellationToken = default);
    Task<int> MarkAllReadAsync(int recipientId, NotificationRecipientRole recipientRole, CancellationToken cancellationToken = default);
    Task<bool> ArchiveAsync(long notificationId, int recipientId, NotificationRecipientRole recipientRole, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(int recipientId, NotificationRecipientRole recipientRole, CancellationToken cancellationToken = default);
    Task<int> UpdateActionStatusAsync(int recipientId, NotificationRecipientRole recipientRole, NotificationEntityType entityType, string entityId, NotificationActionStatus actionStatus, CancellationToken cancellationToken = default);
}

public sealed record NotificationCreateRequest(
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
    NotificationActionStatus ActionStatus,
    string DeduplicationKey,
    object? Metadata = null,
    DateTime? ExpiresAt = null,
    TransactionalEmailContent? EmailDelivery = null);

public sealed record TransactionalEmailContent(string Subject, string Body);
