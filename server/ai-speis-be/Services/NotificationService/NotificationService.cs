using System.Text.Json;
using ai_speis_be.DTOs.Notifications;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Services.NotificationService;

public sealed class NotificationService : INotificationService
{
    private static readonly string[] SensitiveMetadataFragments =
    ["raw", "transcript", "prompt", "response", "payment", "card", "token", "password"];

    private readonly ApplicationDbContext _context;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ApplicationDbContext context, ILogger<NotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Notification?> CreateAsync(NotificationCreateRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        if (await _context.Notifications.AnyAsync(item => item.DeduplicationKey == request.DeduplicationKey, cancellationToken))
            return null;

        var now = DateTime.UtcNow;
        var notification = new Notification
        {
            RecipientId = request.RecipientId,
            RecipientRole = request.RecipientRole,
            Type = request.Type,
            Category = request.Category,
            Severity = request.Severity,
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            ActionUrl = request.ActionUrl,
            ActionStatus = request.ActionStatus,
            DeduplicationKey = request.DeduplicationKey,
            Metadata = SerializeSafeMetadata(request.Metadata),
            ExpiresAt = request.ExpiresAt,
            DeliveryChannel = request.EmailDelivery is null ? DeliveryChannel.IN_APP : DeliveryChannel.EMAIL,
            DeliveryStatus = DeliveryStatus.Pending,
            EmailSubject = request.EmailDelivery?.Subject,
            EmailBody = request.EmailDelivery?.Body,
            CreatedAt = now,
            UpdatedAt = now
        };
        _context.Notifications.Add(notification);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return notification;
        }
        catch (DbUpdateException exception)
        {
            _context.Entry(notification).State = EntityState.Detached;
            _logger.LogInformation(exception, "Notification with deduplication key {DeduplicationKey} already exists.", request.DeduplicationKey);
            return null;
        }
    }

    public async Task<PagedResult<NotificationDto>> GetForRecipientAsync(int recipientId, NotificationRecipientRole recipientRole, NotificationQueryParameters query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var source = _context.Notifications.AsNoTracking().Where(item => item.RecipientId == recipientId && item.RecipientRole == recipientRole);
        if (query.ReadStatus.HasValue) source = source.Where(item => item.ReadStatus == query.ReadStatus.Value);
        if (query.Category.HasValue) source = source.Where(item => item.Category == query.Category.Value);
        if (query.Type.HasValue) source = source.Where(item => item.Type == query.Type.Value);
        if (query.Severity.HasValue) source = source.Where(item => item.Severity == query.Severity.Value);
        if (query.FromDate.HasValue) source = source.Where(item => item.CreatedAt >= query.FromDate.Value);
        if (query.ToDate.HasValue) source = source.Where(item => item.CreatedAt <= query.ToDate.Value);

        var total = await source.CountAsync(cancellationToken);
        var items = await source.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.NotificationId)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(item => ToDto(item)).ToListAsync(cancellationToken);
        return new PagedResult<NotificationDto> { Items = items, PageNumber = page, PageSize = pageSize, TotalItems = total };
    }

    public async Task<NotificationDto?> GetByIdAsync(long notificationId, int recipientId, NotificationRecipientRole recipientRole, CancellationToken cancellationToken = default)
    {
        var item = await FindOwnedAsync(notificationId, recipientId, recipientRole, false, cancellationToken);
        return item is null ? null : ToDto(item);
    }

    public async Task<bool> MarkReadAsync(long notificationId, int recipientId, NotificationRecipientRole recipientRole, CancellationToken cancellationToken = default)
    {
        var item = await FindOwnedAsync(notificationId, recipientId, recipientRole, true, cancellationToken);
        if (item is null) return false;
        if (item.ReadStatus == NotificationReadStatus.UNREAD)
        {
            item.ReadStatus = NotificationReadStatus.READ;
            item.ReadAt = DateTime.UtcNow;
            item.UpdatedAt = item.ReadAt.Value;
            await _context.SaveChangesAsync(cancellationToken);
        }
        return true;
    }

    public async Task<int> MarkAllReadAsync(int recipientId, NotificationRecipientRole recipientRole, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var items = await _context.Notifications
            .Where(item => item.RecipientId == recipientId && item.RecipientRole == recipientRole && item.ReadStatus == NotificationReadStatus.UNREAD)
            .ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            item.ReadStatus = NotificationReadStatus.READ;
            item.ReadAt = now;
            item.UpdatedAt = now;
        }
        if (items.Count > 0) await _context.SaveChangesAsync(cancellationToken);
        return items.Count;
    }

    public async Task<bool> ArchiveAsync(long notificationId, int recipientId, NotificationRecipientRole recipientRole, CancellationToken cancellationToken = default)
    {
        var item = await FindOwnedAsync(notificationId, recipientId, recipientRole, true, cancellationToken);
        if (item is null) return false;
        if (item.ReadStatus != NotificationReadStatus.ARCHIVED)
        {
            var now = DateTime.UtcNow;
            item.ReadStatus = NotificationReadStatus.ARCHIVED;
            item.ArchivedAt = now;
            item.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);
        }
        return true;
    }

    public Task<int> GetUnreadCountAsync(int recipientId, NotificationRecipientRole recipientRole, CancellationToken cancellationToken = default) =>
        _context.Notifications.CountAsync(item => item.RecipientId == recipientId && item.RecipientRole == recipientRole && item.ReadStatus == NotificationReadStatus.UNREAD, cancellationToken);

    public async Task<int> UpdateActionStatusAsync(int recipientId, NotificationRecipientRole recipientRole, NotificationEntityType entityType, string entityId, NotificationActionStatus actionStatus, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var items = await _context.Notifications.Where(item => item.RecipientId == recipientId && item.RecipientRole == recipientRole && item.EntityType == entityType && item.EntityId == entityId && item.ActionStatus == NotificationActionStatus.ACTIVE)
            .ToListAsync(cancellationToken);
        foreach (var item in items) { item.ActionStatus = actionStatus; item.UpdatedAt = now; }
        if (items.Count > 0) await _context.SaveChangesAsync(cancellationToken);
        return items.Count;
    }

    private Task<Notification?> FindOwnedAsync(long id, int recipientId, NotificationRecipientRole role, bool tracked, CancellationToken cancellationToken) =>
        (tracked ? _context.Notifications : _context.Notifications.AsNoTracking()).FirstOrDefaultAsync(item => item.NotificationId == id && item.RecipientId == recipientId && item.RecipientRole == role, cancellationToken);

    private static NotificationDto ToDto(Notification item) => new()
    {
        Id = item.NotificationId, RecipientRole = item.RecipientRole.ToString(), Type = item.Type.ToString(), Category = item.Category.ToString(), Severity = item.Severity.ToString(), Title = item.Title, Message = item.Message,
        EntityType = item.EntityType.ToString(), EntityId = item.EntityId, ActionUrl = item.ActionUrl, ReadStatus = item.ReadStatus.ToString(), ReadAt = AsUtc(item.ReadAt), ActionStatus = item.ActionStatus.ToString(), Metadata = item.Metadata,
        CreatedAt = AsUtc(item.CreatedAt), ExpiresAt = AsUtc(item.ExpiresAt), ArchivedAt = AsUtc(item.ArchivedAt)
    };

    // SQL Server returns datetime/datetime2 values with Kind=Unspecified. Notifications are
    // persisted in UTC, so mark them explicitly before JSON serialization emits them to clients.
    private static DateTime AsUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? AsUtc(DateTime? value) => value.HasValue ? AsUtc(value.Value) : null;

    private static void Validate(NotificationCreateRequest request)
    {
        if (request.RecipientId <= 0 || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message) || string.IsNullOrWhiteSpace(request.DeduplicationKey))
            throw new ArgumentException("Notification recipient, title, message and deduplication key are required.");
        if (request.Title.Length > 200 || request.Message.Length > 1000 || request.DeduplicationKey.Length > 300 || request.ActionUrl?.Length > 500)
            throw new ArgumentException("Notification fields exceed their configured limits.");
        if (request.EmailDelivery is not null && (string.IsNullOrWhiteSpace(request.EmailDelivery.Subject) || string.IsNullOrWhiteSpace(request.EmailDelivery.Body) || request.EmailDelivery.Subject.Length > 200))
            throw new ArgumentException("Transactional email subject and body are required.");
    }

    private static string? SerializeSafeMetadata(object? metadata)
    {
        if (metadata is null) return null;
        var json = JsonSerializer.Serialize(metadata);
        using var document = JsonDocument.Parse(json);
        ValidateMetadata(document.RootElement);
        return json;
    }

    private static void ValidateMetadata(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (SensitiveMetadataFragments.Any(fragment => property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException($"Notification metadata property '{property.Name}' is not allowed.");
                ValidateMetadata(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray()) ValidateMetadata(child);
        }
    }
}
