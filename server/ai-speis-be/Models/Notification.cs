using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Models;

[Table("Notification")]
[Index(nameof(RecipientId), nameof(RecipientRole), nameof(CreatedAt), Name = "IX_Notification_Recipient_CreatedAt")]
[Index(nameof(DeduplicationKey), IsUnique = true, Name = "UX_Notification_DeduplicationKey")]
public class Notification
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long NotificationId { get; set; }

    [Required]
    public int RecipientId { get; set; }

    [Required]
    public NotificationRecipientRole RecipientRole { get; set; }

    [Required]
    public NotificationType Type { get; set; }

    [Required]
    public NotificationCategory Category { get; set; }

    [Required]
    public NotificationSeverity Severity { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    [Required]
    public NotificationEntityType EntityType { get; set; }

    [MaxLength(100)]
    public string? EntityId { get; set; }

    [MaxLength(500)]
    public string? ActionUrl { get; set; }

    [Required]
    public NotificationReadStatus ReadStatus { get; set; } = NotificationReadStatus.UNREAD;

    public DateTime? ReadAt { get; set; }

    [Required]
    public NotificationActionStatus ActionStatus { get; set; } = NotificationActionStatus.ACTIVE;

    [Required, MaxLength(300)]
    public string DeduplicationKey { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string? Metadata { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public DeliveryChannel DeliveryChannel { get; set; } = DeliveryChannel.IN_APP;
    public DeliveryStatus DeliveryStatus { get; set; } = DeliveryStatus.Pending;
    public int RetryCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? NextRetryAt { get; set; }
    [MaxLength(500)] public string? LastError { get; set; }
    [MaxLength(200)] public string? EmailSubject { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? EmailBody { get; set; }
    [MaxLength(64)] public string? DeliveryLeaseToken { get; set; }
    public DateTime? DeliveryLeaseExpiresAt { get; set; }

    public virtual User Recipient { get; set; } = null!;
}
