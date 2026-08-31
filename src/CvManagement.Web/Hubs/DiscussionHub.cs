using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CvManagement.Web.Hubs;

/// <summary>
/// Thin transport: never touches the DB. DiscussionsController persists a post via IDiscussionService,
/// then broadcasts it to this hub's group -- clients don't push posts through the hub directly.
/// </summary>
[Authorize]
public class DiscussionHub : Hub
{
    public async Task JoinPosition(int positionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(positionId));
    }

    public async Task LeavePosition(int positionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(positionId));
    }

    public static string GroupName(int positionId) => $"position-{positionId}";
}
