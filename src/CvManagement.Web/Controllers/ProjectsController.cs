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
    public IActionResult Create() => View(new ProjectFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProjectFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var outcome = await projects.CreateAsync(model, userManager.GetUserId(User)!);
        if (outcome.Status == Services.Abstractions.ProjectSaveStatus.ValidationError)
        {
            ModelState.AddModelError(string.Empty, outcome.Error!);
            return View(model);
        }

        TempData["StatusMessage"] = $"Project \"{model.Name}\" added.";
        return RedirectToAction("Index", "Profile", new { tab = "projects" });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await projects.GetForEditAsync(id, userManager.GetUserId(User)!);
        if (model is null) return NotFound();
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProjectFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var outcome = await projects.UpdateAsync(model, userManager.GetUserId(User)!);
        switch (outcome.Status)
        {
            case Services.Abstractions.ProjectSaveStatus.Success:
                TempData["StatusMessage"] = $"Project \"{model.Name}\" updated.";
                return RedirectToAction("Index", "Profile", new { tab = "projects" });

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
    public async Task<IActionResult> Delete(int[] ids)
    {
        await projects.DeleteAsync(ids, userManager.GetUserId(User)!);
        TempData["StatusMessage"] = $"Deleted {ids.Length} project(s).";
        return RedirectToAction("Index", "Profile", new { tab = "projects" });
    }
}
