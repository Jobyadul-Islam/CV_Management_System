using CvManagement.Web.Data;
using CvManagement.Web.Models;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Controllers;

[Authorize]
public class ProfileController(
    IProfileService profile,
    IProfileAutoSaveService autoSave,
    IProjectService projects,
    IPositionAccessEvaluator accessEvaluator,
    IBadgeService badges,
    IBadgeSvgRenderer badgeSvgRenderer,
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet, Authorize(Roles = $"{RoleNames.Candidate},{RoleNames.Administrator}")]
    public async Task<IActionResult> Index(string? tab, string? userId)
    {
        var targetUserId = ResolveTargetUserId(userId);
        var user = await userManager.FindByIdAsync(targetUserId);
        if (user is null) return NotFound();

        var allCvs = await db.Cvs
            .Where(c => c.UserId == targetUserId)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new { Summary = new CvSummaryViewModel
            {
                Id = c.Id,
                PositionTitle = c.Position.Title,
                Status = c.Status.ToString(),
                LikeCount = c.Likes.Count
            }, c.PositionId })
            .ToListAsync();

        // A CV whose position access rules no longer match is hidden even from its own candidate --
        // but an Administrator has unrestricted access and sees every CV regardless.
        List<CvSummaryViewModel> visibleCvs;
        if (User.IsInRole(RoleNames.Administrator))
        {
            visibleCvs = allCvs.Select(c => c.Summary).ToList();
        }
        else
        {
            var eligibility = await accessEvaluator.IsEligibleForManyAsync(targetUserId, allCvs.Select(c => c.PositionId).ToList());
            visibleCvs = allCvs.Where(c => eligibility.GetValueOrDefault(c.PositionId)).Select(c => c.Summary).ToList();
        }

        var model = new ProfileIndexViewModel
        {
            ActiveTab = tab is "info" or "projects" or "cvs" ? tab : "me",
            UserId = targetUserId,
            IsOwner = targetUserId == userManager.GetUserId(User),
            DisplayName = user.DisplayName,
            MeAttributes = await profile.GetMeTabAsync(targetUserId),
            InfoAttributes = await profile.GetInfoTabAsync(targetUserId),
            Projects = await projects.GetListAsync(targetUserId),
            Cvs = visibleCvs
        };

        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = $"{RoleNames.Candidate},{RoleNames.Administrator}")]
    public async Task<IActionResult> AddInfoAttribute(int[] attributeIds, string? userId)
    {
        var targetUserId = ResolveTargetUserId(userId);
        await profile.AddToInfoAsync(targetUserId, attributeIds);
        return RedirectToAction(nameof(Index), new { tab = "info", userId = OwnerRouteValue(targetUserId) });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = $"{RoleNames.Candidate},{RoleNames.Administrator}")]
    public async Task<IActionResult> RemoveInfoAttribute(int[] attributeIds, string? userId)
    {
        var targetUserId = ResolveTargetUserId(userId);
        await profile.RemoveFromInfoAsync(targetUserId, attributeIds);
        return RedirectToAction(nameof(Index), new { tab = "info", userId = OwnerRouteValue(targetUserId) });
    }

    [HttpPost("/api/profile/autosave/{userId?}"), ValidateAntiForgeryToken, Authorize(Roles = $"{RoleNames.Candidate},{RoleNames.Administrator}")]
    public async Task<IActionResult> AutoSave(string? userId, [FromBody] AutoSaveRequest request)
    {
        var targetUserId = ResolveTargetUserId(userId);
        var results = await autoSave.SaveAsync(targetUserId, request.Changes);
        return Json(new { results });
    }

    /// <summary>Read-only profile view for Recruiters/Admins -- e.g. a discussion post's author link.</summary>
    [HttpGet("/Profile/View/{userId}"), Authorize(Roles = $"{RoleNames.Recruiter},{RoleNames.Administrator}")]
    public new async Task<IActionResult> View(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var publishedCvs = await db.Cvs
            .Where(c => c.UserId == userId && c.Status == Models.Enums.CvStatus.Published)
            .Select(c => new { Summary = new CvSummaryViewModel
            {
                Id = c.Id,
                PositionTitle = c.Position.Title,
                Status = c.Status.ToString(),
                LikeCount = c.Likes.Count
            }, c.PositionId })
            .ToListAsync();

        var eligibility = await accessEvaluator.IsEligibleForManyAsync(userId, publishedCvs.Select(c => c.PositionId).ToList());

        var model = new ProfileViewViewModel
        {
            UserId = userId,
            DisplayName = user.DisplayName,
            MeAttributes = await profile.GetMeTabAsync(userId),
            PublishedCvs = publishedCvs.Where(c => eligibility.GetValueOrDefault(c.PositionId)).Select(c => c.Summary).ToList()
        };

        return base.View("View", model);
    }

    /// <summary>
    /// Downloadable/embeddable SVG achievement panel. Own badges are always visible; another
    /// candidate's require Recruiter/Administrator, matching the read-only profile View permission.
    /// </summary>
    [HttpGet("/Profile/Badges/{userId?}")]
    public async Task<IActionResult> Badges(string? userId, bool download = false)
    {
        var callerId = userManager.GetUserId(User)!;
        var targetUserId = userId ?? callerId;

        if (targetUserId != callerId && !(User.IsInRole(RoleNames.Recruiter) || User.IsInRole(RoleNames.Administrator)))
        {
            return Forbid();
        }

        var user = await userManager.FindByIdAsync(targetUserId);
        if (user is null) return NotFound();

        var earned = await badges.GetEarnedBadgesAsync(targetUserId);
        var svg = badgeSvgRenderer.RenderPanel(earned, user.DisplayName);

        if (download)
        {
            return File(System.Text.Encoding.UTF8.GetBytes(svg), "image/svg+xml", "badges.svg");
        }
        return Content(svg, "image/svg+xml");
    }

    /// <summary>
    /// An Administrator may act as the owner of any candidate's page (spec: "effectively acting as the
    /// owner of every personal page"); a Candidate can only ever operate on their own profile, so any
    /// requested override is ignored for them -- this is re-validated on every request, GET and POST
    /// alike, never trusted from a query string or hidden form field alone.
    /// </summary>
    private string ResolveTargetUserId(string? requested)
        => requested is not null && User.IsInRole(RoleNames.Administrator) ? requested : userManager.GetUserId(User)!;

    /// <summary>Omit the userId route value when it's just the caller's own -- keeps normal-user URLs clean.</summary>
    private string? OwnerRouteValue(string targetUserId)
        => targetUserId == userManager.GetUserId(User) ? null : targetUserId;
}
