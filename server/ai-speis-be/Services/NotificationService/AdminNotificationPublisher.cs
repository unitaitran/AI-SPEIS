using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Services.NotificationService;

// Fan-out is deliberately performed at the application-event boundary. A notification
// remains owned by one admin account, preserving read/archive state and SignalR groups.
public sealed class AdminNotificationPublisher : IAdminNotificationPublisher
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationEventPublisher _publisher;
    private readonly ILogger<AdminNotificationPublisher> _logger;

    public AdminNotificationPublisher(
        ApplicationDbContext context,
        INotificationEventPublisher publisher,
        ILogger<AdminNotificationPublisher> logger)
    {
        _context = context;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task PublishAsync(AdminNotificationEvent notificationEvent, CancellationToken cancellationToken = default)
    {
        var admins = await _context.Users.AsNoTracking()
            .Where(user => user.Status && !user.IsLocked
                && (user.Role.RoleName == "admin" || user.Role.RoleName == "Admin"))
            .Select(user => new { user.UserId })
            .ToListAsync(cancellationToken);
        if (admins.Count == 0)
        {
            _logger.LogWarning("No active admin recipient is available for notification {NotificationType}.", notificationEvent.Type);
            return;
        }

        var userName = await _context.Users.AsNoTracking()
            .Where(user => user.UserId == notificationEvent.AffectedUserId)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "User";

        foreach (var admin in admins)
        {
            var metadata = notificationEvent.Metadata is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>(notificationEvent.Metadata);
            metadata["userName"] = userName;

            try
            {
                await _publisher.PublishAsync(new NotificationEvent(
                    admin.UserId,
                    NotificationRecipientRole.ADMIN,
                    notificationEvent.Type,
                    notificationEvent.Category,
                    notificationEvent.Severity,
                    notificationEvent.Title,
                    notificationEvent.Message,
                    notificationEvent.EntityType,
                    notificationEvent.EntityId,
                    notificationEvent.ActionUrl,
                    $"{notificationEvent.DeduplicationKey}:{admin.UserId}",
                    metadata,
                    notificationEvent.ActionStatus), cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Could not publish admin notification {NotificationType} to admin {AdminUserId}.", notificationEvent.Type, admin.UserId);
            }
        }
    }
}
