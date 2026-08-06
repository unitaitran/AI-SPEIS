using System.Security.Claims;
using ai_speis_be.DTOs.Notifications;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.NotificationService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ai_speis_be.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    public NotificationsController(INotificationService notificationService) => _notificationService = notificationService;

    [HttpGet]
    public async Task<ActionResult<PagedResult<NotificationDto>>> Get([FromQuery] NotificationQueryParameters query, CancellationToken cancellationToken)
    {
        if (!TryGetRecipient(out var userId, out var role)) return Unauthorized();
        return Ok(await _notificationService.GetForRecipientAsync(userId, role, query, cancellationToken));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadNotificationCountDto>> GetUnreadCount(CancellationToken cancellationToken)
    {
        if (!TryGetRecipient(out var userId, out var role)) return Unauthorized();
        return Ok(new UnreadNotificationCountDto(await _notificationService.GetUnreadCountAsync(userId, role, cancellationToken)));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<NotificationDto>> GetById(long id, CancellationToken cancellationToken)
    {
        if (!TryGetRecipient(out var userId, out var role)) return Unauthorized();
        var notification = await _notificationService.GetByIdAsync(id, userId, role, cancellationToken);
        return notification is null ? NotFound() : Ok(notification);
    }

    [HttpPatch("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken cancellationToken)
    {
        if (!TryGetRecipient(out var userId, out var role)) return Unauthorized();
        return await _notificationService.MarkReadAsync(id, userId, role, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        if (!TryGetRecipient(out var userId, out var role)) return Unauthorized();
        await _notificationService.MarkAllReadAsync(userId, role, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:long}/archive")]
    public async Task<IActionResult> Archive(long id, CancellationToken cancellationToken)
    {
        if (!TryGetRecipient(out var userId, out var role)) return Unauthorized();
        return await _notificationService.ArchiveAsync(id, userId, role, cancellationToken) ? NoContent() : NotFound();
    }

    private bool TryGetRecipient(out int userId, out NotificationRecipientRole recipientRole)
    {
        recipientRole = NotificationRecipientRole.USER;
        if (!int.TryParse(User.FindFirstValue("UserId"), out userId)) return false;
        if (User.IsInRole("admin") || User.IsInRole("Admin")) recipientRole = NotificationRecipientRole.ADMIN;
        return true;
    }
}
