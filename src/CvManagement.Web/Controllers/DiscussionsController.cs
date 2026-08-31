using CvManagement.Web.Hubs;
using CvManagement.Web.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using CvManagement.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace CvManagement.Web.Controllers;

[Authorize]
public class DiscussionsController(
    IDiscussionService discussions,
    IHubContext<DiscussionHub> hub,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Post(int positionId, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return BadRequest("Post body can't be empty.");
        }

        var userId = userManager.GetUserId(User)!;
        var post = await discussions.PostAsync(positionId, userId, body.Trim());

        // Broadcast to everyone viewing this position's discussion -- including the poster's own
        // connection, which is how their own new post appears without a separate client-side append.
        await hub.Clients.Group(DiscussionHub.GroupName(positionId)).SendAsync("NewPost", post);

        return Ok();
    }
}
