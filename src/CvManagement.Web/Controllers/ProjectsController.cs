using CvManagement.Web.Domain;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CvManagement.Web.Controllers;

[Authorize(Roles = $"{RoleNames.Candidate},{RoleNames.Administrator}")]
public class ProjectsController(IProjectService projects, ITagService tags, UserManager<ApplicationUser> userManager) : Controller
{
    /// <summary>Tagify autocomplete source -- previously-used technology tags matching the prefix.</summary>
    [HttpGet]
    public async Task<IActionResult> TagSuggestions(string? prefix)
        => Json(await tags.SearchNamesAsync(prefix ?? string.Empty));

    [HttpGet]
    public IActionResult Create(string? userId)
        => View(new ProjectFormViewModel { UserId = ResolveTargetUserId(userId) });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProjectFormViewModel model)
    {
        var targetUserId = ResolveTargetUserId(model.UserId);
        model.UserId = targetUserId;
        if (!ModelState.IsValid) return View(model);

        var outcome = await projects.CreateAsync(model, targetUserId);
        if (outcome.Status == Services.Abstractions.ProjectSaveStatus.ValidationError)
        {
            ModelState.AddModelError(string.Empty, outcome.Error!);
            return View(model);
        }

        TempData["StatusMessage"] = $"Project \"{model.Name}\" added.";
        return RedirectToAction("Index", "Profile", new { tab = "projects", userId = OwnerRouteValue(targetUserId) });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, string? userId)
    {
        var targetUserId = ResolveTargetUserId(userId);
        var model = await projects.GetForEditAsync(id, targetUserId);
        if (model is null) return NotFound();

        model.UserId = targetUserId;
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProjectFormViewModel model)
    {
        var targetUserId = ResolveTargetUserId(model.UserId);
        model.UserId = targetUserId;
        if (!ModelState.IsValid) return View(model);

        var outcome = await projects.UpdateAsync(model, targetUserId);
        switch (outcome.Status)
        {
            case Services.Abstractions.ProjectSaveStatus.Success:
                TempData["StatusMessage"] = $"Project \"{model.Name}\" updated.";
                return RedirectToAction("Index", "Profile", new { tab = "projects", userId = OwnerRouteValue(targetUserId) });

            case Services.Abstractions.ProjectSaveStatus.NotFound:
                return NotFound();

            case Services.Abstractions.ProjectSaveStatus.Conflict:
                ModelState.AddModelError(string.Empty,
                    "This project was changed elsewhere since you opened it. Review the current version and save again.");
                model.RowVersion = outcome.CurrentRowVersion;
                return View(model);

            default:
                ModelState.AddModelError(string.Empty, outcome.Error!);
                return View(model);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int[] ids, string? userId)
    {
        var targetUserId = ResolveTargetUserId(userId);
        await projects.DeleteAsync(ids, targetUserId);
        TempData["StatusMessage"] = $"Deleted {ids.Length} project(s).";
        return RedirectToAction("Index", "Profile", new { tab = "projects", userId = OwnerRouteValue(targetUserId) });
    }

    /// <summary>
    /// An Administrator may act as the owner of any candidate's page (spec: "effectively acting as the
    /// owner of every personal page"); a Candidate can only ever operate on their own projects, so any
    /// requested override is ignored for them -- this is re-validated on every request, GET and POST
    /// alike, never trusted from a hidden form field alone.
    /// </summary>
    private string ResolveTargetUserId(string? requested)
        => requested is not null && User.IsInRole(RoleNames.Administrator) ? requested : userManager.GetUserId(User)!;

    /// <summary>Omit the userId route value when it's just the caller's own -- keeps normal-user URLs clean.</summary>
    private string? OwnerRouteValue(string targetUserId)
        => targetUserId == userManager.GetUserId(User) ? null : targetUserId;
}