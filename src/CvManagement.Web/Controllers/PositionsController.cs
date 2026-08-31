using CvManagement.Web.Data;
using CvManagement.Web.Domain;
using CvManagement.Web.Domain.Enums;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Position;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Controllers;

public class PositionsController(
    IPositionService positions,
    IAttributeService attributes,
    IPositionAccessEvaluator accessEvaluator,
    IDiscussionService discussions,
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> Index(string? tag)
    {
        ViewBag.Tag = tag;
        List<ViewModels.Position.PositionListItemViewModel> list;

        if (User.IsInRole(RoleNames.Recruiter) || User.IsInRole(RoleNames.Administrator))
        {
            ViewBag.IsManaging = true;
            list = (await positions.GetListForRecruiterAsync()).ToList();
        }
        else if (User.Identity?.IsAuthenticated == true)
        {
            ViewBag.IsManaging = false;
            list = (await positions.GetEligibleListForCandidateAsync(userManager.GetUserId(User)!)).ToList();
        }
        else
        {
            // Anonymous: public positions only, read-only browse.
            ViewBag.IsManaging = false;
            list = await db.Positions
                .Where(p => p.AccessMode == PositionAccessMode.Public)
                .OrderByDescending(p => p.UpdatedAt)
                .Select(p => new ViewModels.Position.PositionListItemViewModel
                {
                    Id = p.Id, Title = p.Title, Company = p.Company, Level = p.Level,
                    AccessMode = p.AccessMode, CvCount = p.Cvs.Count, UpdatedAt = p.UpdatedAt
                })
                .ToListAsync();
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var taggedPositionIds = await db.PositionProjectTags
                .Where(t => t.Tag.Name == tag)
                .Select(t => t.PositionId)
                .ToListAsync();
            list = list.Where(p => taggedPositionIds.Contains(p.Id)).ToList();
        }

        return View(list);
    }

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var model = await positions.GetDetailsAsync(id);
        if (model is null) return NotFound();

        var isManaging = User.IsInRole(RoleNames.Recruiter) || User.IsInRole(RoleNames.Administrator);
        if (!isManaging && model.AccessMode == PositionAccessMode.Restricted && User.Identity?.IsAuthenticated != true)
        {
            return NotFound();
        }

        if (User.IsInRole(RoleNames.Candidate))
        {
            var userId = userManager.GetUserId(User)!;
            model.ViewerIsEligible = await accessEvaluator.IsEligibleAsync(userId, id);
            // Only surface an existing CV link while still eligible -- otherwise the CV is hidden
            // (per spec, even from its own candidate) and the link would just 404.
            model.ViewerExistingCvId = model.ViewerIsEligible == true
                ? await db.Cvs.Where(c => c.UserId == userId && c.PositionId == id).Select(c => (int?)c.Id).FirstOrDefaultAsync()
                : null;
        }

        model.ViewerIsRecruiter = isManaging;
        model.ViewerCanSeeDiscussion = User.Identity?.IsAuthenticated == true;
        if (model.ViewerCanSeeDiscussion)
        {
            model.DiscussionPosts = (await discussions.GetPostsAsync(id)).ToList();
        }

        if (isManaging)
        {
            model.Cvs = await GetCvsForPositionAsync(id, isAdmin: User.IsInRole(RoleNames.Administrator));
        }

        ViewBag.IsManaging = isManaging;
        return View(model);
    }

    /// <summary>
    /// Admins see every CV for the position regardless of status/eligibility (they view every page as
    /// if they were the owner). Recruiters see only Published CVs whose candidate is still eligible --
    /// the same rule CvController.GetAccessAsync/Browse already enforce for CV visibility.
    /// </summary>
    private async Task<List<PositionCvListItemViewModel>> GetCvsForPositionAsync(int positionId, bool isAdmin)
    {
        var rows = await db.Cvs.AsNoTracking()
            .Where(c => c.PositionId == positionId)
            .Select(c => new { c.Id, c.UserId, c.User.DisplayName, Status = c.Status.ToString(), LikeCount = c.Likes.Count, c.UpdatedAt })
            .ToListAsync();

        var candidates = isAdmin ? rows : rows.Where(c => c.Status == nameof(CvStatus.Published)).ToList();

        var eligibleUserIds = isAdmin
            ? null
            : await accessEvaluator.IsEligibleForPositionAsync(positionId, candidates.Select(c => c.UserId).Distinct().ToList());

        return candidates
            .Where(c => isAdmin || eligibleUserIds!.GetValueOrDefault(c.UserId))
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new PositionCvListItemViewModel
            {
                Id = c.Id,
                CandidateDisplayName = c.DisplayName,
                Status = c.Status,
                LikeCount = c.LikeCount,
                UpdatedAt = c.UpdatedAt
            })
            .ToList();
    }

    [HttpGet, Authorize(Roles = $"{RoleNames.Recruiter},{RoleNames.Administrator}")]
    public async Task<IActionResult> Create()
        => View(await positions.GetBlankFormAsync());

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = $"{RoleNames.Recruiter},{RoleNames.Administrator}")]
    public async Task<IActionResult> Create(PositionFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await RepopulateAsync(model);
            return View(model);
        }

        var userId = userManager.GetUserId(User)!;
        var outcome = await positions.CreateAsync(model, userId);

        if (outcome.Status != PositionSaveStatus.Success)
        {
            foreach (var error in outcome.Errors ?? []) ModelState.AddModelError(string.Empty, error);
            await RepopulateAsync(model);
            return View(model);
        }

        TempData["StatusMessage"] = $"Position \"{model.Title}\" created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, Authorize(Roles = $"{RoleNames.Recruiter},{RoleNames.Administrator}")]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await positions.GetForEditAsync(id);
        if (model is null) return NotFound();
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = $"{RoleNames.Recruiter},{RoleNames.Administrator}")]
    public async Task<IActionResult> Edit(PositionFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await RepopulateAsync(model);
            return View(model);
        }

        var userId = userManager.GetUserId(User)!;
        var outcome = await positions.UpdateAsync(model, userId);

        switch (outcome.Status)
        {
            case PositionSaveStatus.Success:
                TempData["StatusMessage"] = $"Position \"{model.Title}\" updated.";
                return RedirectToAction(nameof(Index));

            case PositionSaveStatus.NotFound:
                return NotFound();

            case PositionSaveStatus.Conflict:
                ModelState.AddModelError(string.Empty,
                    "This position was changed by someone else since you opened it. Review the current version and save again.");
                model.RowVersion = outcome.CurrentRowVersion;
                await RepopulateAsync(model);
                return View(model);

            default:
                foreach (var error in outcome.Errors ?? []) ModelState.AddModelError(string.Empty, error);
                await RepopulateAsync(model);
                return View(model);
        }
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = $"{RoleNames.Recruiter},{RoleNames.Administrator}")]
    public async Task<IActionResult> Delete(int[] ids)
    {
        var outcome = await positions.DeleteAsync(ids);

        var parts = new List<string>();
        if (outcome.DeletedIds.Count > 0) parts.Add($"Deleted {outcome.DeletedIds.Count} position(s).");
        foreach (var blocked in outcome.Blocked) parts.Add($"\"{blocked.Title}\" not deleted: {blocked.Reason}");
        TempData["StatusMessage"] = string.Join(" ", parts);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = $"{RoleNames.Recruiter},{RoleNames.Administrator}")]
    public async Task<IActionResult> Duplicate(int[] ids)
    {
        if (ids.Length != 1) return BadRequest();

        var userId = userManager.GetUserId(User)!;
        var outcome = await positions.DuplicateAsync(ids[0], userId);
        if (outcome.Status != PositionSaveStatus.Success) return NotFound();

        TempData["StatusMessage"] = "Position duplicated. Review and save the copy.";
        return RedirectToAction(nameof(Edit), new { id = outcome.Id });
    }

    private async Task RepopulateAsync(PositionFormViewModel model)
    {
        var blank = await positions.GetBlankFormAsync();
        model.LevelOptions = blank.LevelOptions;

        if (model.SelectedAttributes.Count == 0 && model.AttributeIds.Count > 0)
        {
            var picked = await attributes.SearchForPickerAsync(null, null, null);
            model.SelectedAttributes = picked
                .Where(a => model.AttributeIds.Contains(a.Id))
                .Select(a => (a.Id, a.Name))
                .ToList();
        }
    }
}
