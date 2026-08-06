using ai_speis_be.Models.Enums;

namespace ai_speis_be.DTOs.Notifications;

public sealed class NotificationQueryParameters
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public NotificationReadStatus? ReadStatus { get; init; }
    public NotificationCategory? Category { get; init; }
    public NotificationType? Type { get; init; }
    public NotificationSeverity? Severity { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
}

public sealed class NotificationDto
{
    public long Id { get; init; }
    public string RecipientRole { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string? EntityId { get; init; }
    public string? ActionUrl { get; init; }
    public string ReadStatus { get; init; } = string.Empty;
    public DateTime? ReadAt { get; init; }
    public string ActionStatus { get; init; } = string.Empty;
    public string? Metadata { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime? ArchivedAt { get; init; }
}

public sealed record UnreadNotificationCountDto(int Count);
