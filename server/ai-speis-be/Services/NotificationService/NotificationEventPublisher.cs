namespace ai_speis_be.Services.NotificationService;

// Deliberately small application-event boundary; the project does not currently use an event bus.
public sealed class NotificationEventPublisher : INotificationEventPublisher
{
    private readonly INotificationService _notificationService;
    public NotificationEventPublisher(INotificationService notificationService) => _notificationService = notificationService;

    public async Task PublishAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken = default)
    {
        await _notificationService.CreateAsync(new NotificationCreateRequest(
            notificationEvent.RecipientId, notificationEvent.RecipientRole, notificationEvent.Type,
            notificationEvent.Category, notificationEvent.Severity, notificationEvent.Title,
            notificationEvent.Message, notificationEvent.EntityType, notificationEvent.EntityId,
            notificationEvent.ActionUrl, notificationEvent.ActionStatus, notificationEvent.DeduplicationKey,
            notificationEvent.Metadata, notificationEvent.ExpiresAt), cancellationToken);
    }

    public Task UpdateActionStatusAsync(int recipientId, Models.Enums.NotificationRecipientRole recipientRole, Models.Enums.NotificationEntityType entityType, string entityId, Models.Enums.NotificationActionStatus actionStatus, CancellationToken cancellationToken = default) =>
        _notificationService.UpdateActionStatusAsync(recipientId, recipientRole, entityType, entityId, actionStatus, cancellationToken);
}
