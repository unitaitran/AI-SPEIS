using ai_speis_be.Hubs;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Services.NotificationService;

public sealed class NotificationRealtimeNotifier : INotificationRealtimeNotifier
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationRealtimeNotifier> _logger;

    public NotificationRealtimeNotifier(ApplicationDbContext context, IHubContext<NotificationHub> hubContext, ILogger<NotificationRealtimeNotifier> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task CreatedAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        await SendAsync(notification.RecipientId, notification.RecipientRole, "notification.created", new
        {
            notification = ToRealtimeNotification(notification),
            unreadCount = await GetUnreadCountAsync(notification.RecipientId, notification.RecipientRole, cancellationToken)
        }, cancellationToken);
    }

    public async Task ReadAsync(int recipientId, NotificationRecipientRole recipientRole, long notificationId, CancellationToken cancellationToken = default)
    {
        await SendAsync(recipientId, recipientRole, "notification.read", new
        {
            notificationId,
            readStatus = NotificationReadStatus.READ.ToString(),
            readAt = DateTime.UtcNow,
            unreadCount = await GetUnreadCountAsync(recipientId, recipientRole, cancellationToken)
        }, cancellationToken);
    }

    public Task ReadAllAsync(int recipientId, NotificationRecipientRole recipientRole, CancellationToken cancellationToken = default) =>
        SendAsync(recipientId, recipientRole, "notification.read-all", new { unreadCount = 0 }, cancellationToken);

    public async Task ArchivedAsync(int recipientId, NotificationRecipientRole recipientRole, long notificationId, CancellationToken cancellationToken = default)
    {
        await SendAsync(recipientId, recipientRole, "notification.archived", new
        {
            notificationId,
            unreadCount = await GetUnreadCountAsync(recipientId, recipientRole, cancellationToken)
        }, cancellationToken);
    }

    private async Task SendAsync(int recipientId, NotificationRecipientRole recipientRole, string eventName, object payload, CancellationToken cancellationToken)
    {
        try
        {
            await _hubContext.Clients.Group(NotificationHub.GroupName(recipientId, recipientRole))
                .SendAsync(eventName, payload, cancellationToken);
        }
        catch (Exception exception)
        {
            if (exception is not OperationCanceledException)
                _logger.LogError(exception, "Unable to publish realtime notification event {EventName} for recipient {RecipientId}.", eventName, recipientId);
        }
    }

    private Task<int> GetUnreadCountAsync(int recipientId, NotificationRecipientRole recipientRole, CancellationToken cancellationToken) =>
        _context.Notifications.CountAsync(item => item.RecipientId == recipientId
            && item.RecipientRole == recipientRole
            && item.ReadStatus == NotificationReadStatus.UNREAD, cancellationToken);

    private static object ToRealtimeNotification(Notification notification) => new
    {
        id = notification.NotificationId,
        recipientRole = notification.RecipientRole.ToString(),
        type = notification.Type.ToString(),
        category = notification.Category.ToString(),
        severity = notification.Severity.ToString(),
        title = notification.Title,
        message = notification.Message,
        entityType = notification.EntityType.ToString(),
        entityId = notification.EntityId,
        actionUrl = notification.ActionUrl,
        readStatus = notification.ReadStatus.ToString(),
        readAt = AsUtc(notification.ReadAt),
        actionStatus = notification.ActionStatus.ToString(),
        metadata = notification.Metadata,
        createdAt = AsUtc(notification.CreatedAt),
        expiresAt = AsUtc(notification.ExpiresAt),
        archivedAt = AsUtc(notification.ArchivedAt)
    };

    // Notifications are stored as UTC. EF reads SQL Server datetime values as Unspecified,
    // which otherwise serializes without a timezone offset and is interpreted as browser-local.
    private static DateTime AsUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? AsUtc(DateTime? value) => value.HasValue ? AsUtc(value.Value) : null;
}
