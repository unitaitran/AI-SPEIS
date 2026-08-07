using ai_speis_be.DTOs.Notifications;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.NotificationService;
using ai_speis_be.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace ai_speis_be.Tests.Services;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task GetForRecipient_Returns_only_current_user_notifications()
    {
        await using var context = TestDbContextFactory.Create();
        var service = CreateService(context);
        await CreateAsync(service, 1, "user-1");
        await CreateAsync(service, 2, "user-2");

        var page = await service.GetForRecipientAsync(1, NotificationRecipientRole.USER, new NotificationQueryParameters());

        Assert.Single(page.Items);
        Assert.Equal("user-1", page.Items[0].Title);
    }

    [Fact]
    public async Task GetForRecipient_Does_not_expose_admin_notification_to_user()
    {
        await using var context = TestDbContextFactory.Create();
        var service = CreateService(context);
        await CreateAsync(service, 1, "admin-alert", NotificationRecipientRole.ADMIN);

        var userPage = await service.GetForRecipientAsync(1, NotificationRecipientRole.USER, new NotificationQueryParameters());
        var adminPage = await service.GetForRecipientAsync(1, NotificationRecipientRole.ADMIN, new NotificationQueryParameters());

        Assert.Empty(userPage.Items);
        Assert.Single(adminPage.Items);
    }

    [Fact]
    public async Task Unread_count_and_mark_read_are_correct_and_idempotent()
    {
        await using var context = TestDbContextFactory.Create();
        var service = CreateService(context);
        var notification = await CreateAsync(service, 1, "read-me");

        Assert.Equal(1, await service.GetUnreadCountAsync(1, NotificationRecipientRole.USER));
        Assert.True(await service.MarkReadAsync(notification!.NotificationId, 1, NotificationRecipientRole.USER));
        Assert.True(await service.MarkReadAsync(notification.NotificationId, 1, NotificationRecipientRole.USER));
        Assert.Equal(0, await service.GetUnreadCountAsync(1, NotificationRecipientRole.USER));
    }

    [Fact]
    public async Task Mark_all_read_affects_only_current_recipient()
    {
        await using var context = TestDbContextFactory.Create();
        var service = CreateService(context);
        await CreateAsync(service, 1, "one");
        await CreateAsync(service, 2, "two");

        Assert.Equal(1, await service.MarkAllReadAsync(1, NotificationRecipientRole.USER));

        Assert.Equal(0, await service.GetUnreadCountAsync(1, NotificationRecipientRole.USER));
        Assert.Equal(1, await service.GetUnreadCountAsync(2, NotificationRecipientRole.USER));
    }

    [Fact]
    public async Task Pagination_and_filters_follow_contract()
    {
        await using var context = TestDbContextFactory.Create();
        var service = CreateService(context);
        await CreateAsync(service, 1, "interview", category: NotificationCategory.INTERVIEW);
        await CreateAsync(service, 1, "subscription", category: NotificationCategory.SUBSCRIPTION);
        await CreateAsync(service, 1, "profile", category: NotificationCategory.PROFILE);

        var page = await service.GetForRecipientAsync(1, NotificationRecipientRole.USER,
            new NotificationQueryParameters { Page = 1, PageSize = 1, Category = NotificationCategory.SUBSCRIPTION });

        Assert.Equal(1, page.TotalItems);
        Assert.Single(page.Items);
        Assert.Equal("subscription", page.Items[0].Title);
    }

    [Fact]
    public async Task Same_domain_event_twice_creates_one_notification()
    {
        await using var context = TestDbContextFactory.Create();
        var publisher = new NotificationEventPublisher(CreateService(context));
        var notificationEvent = new NotificationEvent(1, NotificationRecipientRole.USER, NotificationType.INTERVIEW_ROUND_COMPLETED,
            NotificationCategory.INTERVIEW, NotificationSeverity.SUCCESS, "Round completed", "Completed.",
            NotificationEntityType.INTERVIEW_ROUND, "9", "/user/interview-history", "INTERVIEW_ROUND_COMPLETED:9:1");

        await publisher.PublishAsync(notificationEvent);
        await publisher.PublishAsync(notificationEvent);

        Assert.Equal(1, context.Notifications.Count());
    }

    [Fact]
    public async Task Archive_and_business_action_status_are_updated_without_cross_account_access()
    {
        await using var context = TestDbContextFactory.Create();
        var service = CreateService(context);
        var notification = await CreateAsync(service, 1, "session", entityId: "42");

        Assert.False(await service.ArchiveAsync(notification!.NotificationId, 2, NotificationRecipientRole.USER));
        Assert.True(await service.ArchiveAsync(notification.NotificationId, 1, NotificationRecipientRole.USER));
        Assert.Equal(1, await service.UpdateActionStatusAsync(1, NotificationRecipientRole.USER,
            NotificationEntityType.INTERVIEW_SESSION, "42", NotificationActionStatus.COMPLETED));
        var updated = await service.GetByIdAsync(notification.NotificationId, 1, NotificationRecipientRole.USER);
        Assert.Equal("ARCHIVED", updated!.ReadStatus);
        Assert.Equal("COMPLETED", updated.ActionStatus);
    }

    [Fact]
    public async Task Sensitive_metadata_is_rejected()
    {
        await using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(new NotificationCreateRequest(
            1, NotificationRecipientRole.USER, NotificationType.CV_PROCESSING_FAILED, NotificationCategory.PROFILE,
            NotificationSeverity.ERROR, "Failed", "Failed", NotificationEntityType.CV, "1", null,
            NotificationActionStatus.ACTIVE, "sensitive-metadata", new { rawResponse = "do not persist" })));
    }

    private static NotificationService CreateService(ai_speis_be.Models.ApplicationDbContext context) =>
        new(context, NullLogger<NotificationService>.Instance);

    private static Task<ai_speis_be.Models.Notification?> CreateAsync(
        NotificationService service, int recipientId, string title, NotificationRecipientRole role = NotificationRecipientRole.USER,
        NotificationCategory category = NotificationCategory.INTERVIEW, string entityId = "1") =>
        service.CreateAsync(new NotificationCreateRequest(recipientId, role, NotificationType.INTERVIEW_SESSION_READY,
            category, NotificationSeverity.INFO, title, "Message", NotificationEntityType.INTERVIEW_SESSION,
            entityId, "/user/interview/setup", NotificationActionStatus.ACTIVE, $"{role}:{recipientId}:{title}", new { sessionId = entityId }));
}
