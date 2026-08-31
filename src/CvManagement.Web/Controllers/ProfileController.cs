using CvManagement.Web.Data;
using CvManagement.Web.Domain;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Controllers;

[Authorize(Roles = $"{RoleNames.Candidate},{RoleNames.Administrator}")]
public class ProfileController(
    IProfileService profile,
    IProfileAutoSaveService autoSave,
    IProjectService projects,
    IPositionAccessEvaluator accessEvaluator,
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? tab)
    {
        var userId = userManager.GetUserId(User)!;
        var user = await userManager.FindByIdAsync(userId);

        var allCvs = await db.Cvs
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new { Summary = new CvSummaryViewModel
            {
                Id = c.Id,
                PositionTitle = c.Position.Title,
                Status = c.Status.ToString(),
                LikeCount = c.Likes.Count
            }, c.PositionId })
            .ToListAsync();

        // A CV whose position access rules no longer match is hidden even from its own candidate.
        var eligibility = await accessEvaluator.IsEligibleForManyAsync(userId, allCvs.Select(c => c.PositionId).ToList());

        var model = new ProfileIndexViewModel
        {
            ActiveTab = tab is "info" or "projects" or "cvs" ? tab : "me",
            DisplayName = user?.DisplayName ?? string.Empty,
            MeAttributes = await profile.GetMeTabAsync(userId),
            InfoAttributes = await profile.GetInfoTabAsync(userId),
            Projects = await projects.GetListAsync(userId),
            Cvs = allCvs.Where(c => eligibility.GetValueOrDefault(c.PositionId)).Select(c => c.Summary).ToList()
        };

        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddInfoAttribute(int[] attributeIds)
    {
        var userId = userManager.GetUserId(User)!;
        foreach (var attributeId in attributeIds)
        {
            await profile.AddToInfoAsync(userId, attributeId);
        }
        return RedirectToAction(nameof(Index), new { tab = "info" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveInfoAttribute(int attributeId)
    {
        await profile.RemoveFromInfoAsync(userManager.GetUserId(User)!, attributeId);
        return RedirectToAction(nameof(Index), new { tab = "info" });
    }

    [HttpPost("/api/profile/autosave"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AutoSave([FromBody] AutoSaveRequest request)
    {
        var userId = userManager.GetUserId(User)!;
        var results = await autoSave.SaveAsync(userId, request.Changes);
        return Json(new { results });
    }
}
