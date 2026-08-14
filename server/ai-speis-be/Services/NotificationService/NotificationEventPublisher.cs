namespace ai_speis_be.Services.NotificationService;

// Deliberately small application-event boundary; the project does not currently use an event bus.
public sealed class NotificationEventPublisher : INotificationEventPublisher
{
    private readonly INotificationService _notificationService;
    private readonly INotificationRealtimeNotifier _realtimeNotifier;
    private readonly INotificationEmailDeliveryService? _emailDeliveryService;

    public NotificationEventPublisher(INotificationService notificationService)
        : this(notificationService, NoopNotificationRealtimeNotifier.Instance)
    {
    }

    public NotificationEventPublisher(INotificationService notificationService, INotificationRealtimeNotifier realtimeNotifier, INotificationEmailDeliveryService? emailDeliveryService = null)
    {
        _notificationService = notificationService;
        _realtimeNotifier = realtimeNotifier;
        _emailDeliveryService = emailDeliveryService;
    }

    public async Task PublishAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken = default)
    {
        var notification = await _notificationService.CreateAsync(new NotificationCreateRequest(
            notificationEvent.RecipientId, notificationEvent.RecipientRole, notificationEvent.Type,
            notificationEvent.Category, notificationEvent.Severity, notificationEvent.Title,
            notificationEvent.Message, notificationEvent.EntityType, notificationEvent.EntityId,
            notificationEvent.ActionUrl, notificationEvent.ActionStatus, notificationEvent.DeduplicationKey,
            notificationEvent.Metadata, notificationEvent.ExpiresAt, notificationEvent.EmailDelivery), cancellationToken);
        if (notification is not null)
        {
            if (notification.DeliveryChannel == Models.Enums.DeliveryChannel.EMAIL && _emailDeliveryService is not null)
                await _emailDeliveryService.AttemptPendingAsync(notification.NotificationId, cancellationToken);
            await _realtimeNotifier.CreatedAsync(notification, cancellationToken);
        }
    }

    public Task UpdateActionStatusAsync(int recipientId, Models.Enums.NotificationRecipientRole recipientRole, Models.Enums.NotificationEntityType entityType, string entityId, Models.Enums.NotificationActionStatus actionStatus, CancellationToken cancellationToken = default) =>
        _notificationService.UpdateActionStatusAsync(recipientId, recipientRole, entityType, entityId, actionStatus, cancellationToken);

    private sealed class NoopNotificationRealtimeNotifier : INotificationRealtimeNotifier
    {
        public static readonly NoopNotificationRealtimeNotifier Instance = new();
        public Task CreatedAsync(Models.Notification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReadAsync(int recipientId, Models.Enums.NotificationRecipientRole recipientRole, long notificationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReadAllAsync(int recipientId, Models.Enums.NotificationRecipientRole recipientRole, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ArchivedAsync(int recipientId, Models.Enums.NotificationRecipientRole recipientRole, long notificationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
