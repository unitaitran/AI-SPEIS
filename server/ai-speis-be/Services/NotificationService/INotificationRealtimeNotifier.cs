using ai_speis_be.Models;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.Services.NotificationService;

public interface INotificationRealtimeNotifier
{
    Task CreatedAsync(Notification notification, CancellationToken cancellationToken = default);
    Task ReadAsync(int recipientId, NotificationRecipientRole recipientRole, long notificationId, CancellationToken cancellationToken = default);
    Task ReadAllAsync(int recipientId, NotificationRecipientRole recipientRole, CancellationToken cancellationToken = default);
    Task ArchivedAsync(int recipientId, NotificationRecipientRole recipientRole, long notificationId, CancellationToken cancellationToken = default);
}
