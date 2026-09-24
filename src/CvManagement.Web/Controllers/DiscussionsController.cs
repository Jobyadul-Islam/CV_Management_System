using Microsoft.Extensions.Localization;
using CvManagement.Web.Hubs;
using CvManagement.Web.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using CvManagement.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace CvManagement.Web.Controllers;

[Authorize]
public class DiscussionsController(
    IDiscussionService discussions,
    IHubContext<DiscussionHub> hub,
    UserManager<ApplicationUser> userManager,
    IStringLocalizer<SharedResource> localizer) : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Post(int positionId, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return BadRequest(localizer["Discussion_Empty"].Value);
        }

        if (body.Length > IDiscussionService.MaxBodyLength)
        {
            return BadRequest(localizer["Discussion_TooLong", IDiscussionService.MaxBodyLength].Value);
        }

        var userId = userManager.GetUserId(User)!;
        var post = await discussions.PostAsync(positionId, userId, body.Trim());
        if (post is null) return NotFound();

        // Broadcast to everyone viewing this position's discussion -- including the poster's own
        // connection, which is how their own new post appears without a separate client-side append.
        await hub.Clients.Group(DiscussionHub.GroupName(positionId)).SendAsync("NewPost", post);

        return Ok();
    }
}
