using Microsoft.Extensions.Localization;
using CvManagement.Web.Models;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Attribute;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CvManagement.Web.Controllers;

public class AttributesController(IAttributeService attributes, UserManager<ApplicationUser> userManager,
    IStringLocalizer<SharedResource> localizer) : Controller
{
    /// <summary>Any authenticated user can record picker usage (Candidates use the picker too, in Phase 5).</summary>
    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> RecordUsage(int attributeId)
    {
        await attributes.RecordUsageAsync(userManager.GetUserId(User)!, attributeId);
        return Ok();
    }

    [Authorize(Roles = $"{RoleNames.Recruiter},{RoleNames.Administrator}")]
    [HttpGet]
    public async Task<IActionResult> Index(string? prefix, int? categoryId)
    {
        ViewBag.Categories = await attributes.GetCategoriesAsync();
        ViewBag.Prefix = prefix;
        ViewBag.CategoryId = categoryId;
        var items = await attributes.GetLibraryAsync(prefix, categoryId);
        return View(items);
    }

    [HttpGet, Authorize(Roles = $"{RoleNames.Recruiter},{RoleNames.Administrator}")]
    public async Task<IActionResult> Create()
    {
        var model = new AttributeFormViewModel();
        await PopulateCategoriesAsync(model);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = $"{RoleNames.Recruiter},{RoleNames.Administrator}")]
    public async Task<IActionResult> Create(AttributeFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync(model);
            return View(model);
        }

        var userId = userManager.GetUserId(User)!;
        var outcome = await attributes.CreateAsync(model, userId);

        if (outcome.Status == AttributeSaveStatus.ValidationError)
        {
            foreach (var error in outcome.Errors ?? [])
            {
                ModelState.AddModelError(string.Empty, error);
            }
            await PopulateCategoriesAsync(model);
            return View(model);
        }

        TempData["StatusMessage"] = localizer["Msg_AttributeCreated", model.Name].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, Authorize(Roles = $"{RoleNames.Recruiter},{RoleNames.Administrator}")]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await attributes.GetForEditAsync(id);
        if (model is null) return NotFound();
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = $"{RoleNames.Recruiter},{RoleNames.Administrator}")]
    public async Task<IActionResult> Edit(AttributeFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync(model);
            return View(model);
        }

        var outcome = await attributes.UpdateAsync(model);

        switch (outcome.Status)
        {
            case AttributeSaveStatus.Success:
                TempData["StatusMessage"] = localizer["Msg_AttributeUpdated", model.Name].Value;
                return RedirectToAction(nameof(Index));

            case AttributeSaveStatus.NotFound:
                return NotFound();

            case AttributeSaveStatus.Conflict:
                ModelState.AddModelError(string.Empty,
                    localizer["Msg_AttributeConflict"]);
                model.RowVersion = Convert.ToBase64String(outcome.CurrentRowVersion!);
                await PopulateCategoriesAsync(model);
                return View(model);

            default:
                foreach (var error in outcome.Errors ?? [])
                {
                    ModelState.AddModelError(string.Empty, error);
                }
                await PopulateCategoriesAsync(model);
                return View(model);
        }
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = $"{RoleNames.Recruiter},{RoleNames.Administrator}")]
    public async Task<IActionResult> Delete(int[] ids)
    {
        var outcome = await attributes.DeleteAsync(ids);

        var parts = new List<string>();
        if (outcome.DeletedIds.Count > 0) parts.Add(localizer["Msg_AttributesDeleted", outcome.DeletedIds.Count]);
        foreach (var blocked in outcome.Blocked)
        {
            parts.Add(localizer["Msg_NotDeleted", blocked.Name, blocked.Reason]);
        }
        TempData["StatusMessage"] = string.Join(" ", parts);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>JSON endpoint backing the attribute picker (prefix/category search + recently-used).</summary>
    [HttpGet, Authorize]
    public async Task<IActionResult> Picker(string? prefix, int? categoryId, [FromQuery] int[]? exclude)
    {
        var userId = userManager.GetUserId(User)!;
        var recent = string.IsNullOrWhiteSpace(prefix) && categoryId is null
            ? await attributes.GetRecentlyUsedAsync(userId, exclude)
            : [];
        var results = await attributes.SearchForPickerAsync(prefix, categoryId, exclude);

        return Json(new { recent, results });
    }

    private async Task PopulateCategoriesAsync(AttributeFormViewModel model)
    {
        var categories = await attributes.GetCategoriesAsync();
        model.CategoryOptions = categories
            .Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(c.Name, c.Id.ToString(), c.Id == model.CategoryId))
            .ToList();
    }
}
