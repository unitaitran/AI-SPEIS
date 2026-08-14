using ai_speis_be.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ai_speis_be.Hubs;

[Authorize]
public sealed class NotificationHub : Hub
{
    public static string GroupName(int recipientId, NotificationRecipientRole recipientRole) =>
        $"notifications:{recipientRole}:{recipientId}";

    public override async Task OnConnectedAsync()
    {
        var userIdValue = Context.User?.FindFirst("UserId")?.Value;
        var recipientRole = Context.User?.IsInRole("admin") == true || Context.User?.IsInRole("Admin") == true
            ? NotificationRecipientRole.ADMIN
            : Context.User?.IsInRole("user") == true || Context.User?.IsInRole("User") == true
                ? NotificationRecipientRole.USER
                : (NotificationRecipientRole?)null;
        if (!int.TryParse(userIdValue, out var userId) || !recipientRole.HasValue)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(userId, recipientRole.Value));
        await base.OnConnectedAsync();
    }
}
