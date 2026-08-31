using System.Security.Claims;
using CvManagement.Web.Data;
using CvManagement.Web.Domain;
using CvManagement.Web.Domain.Enums;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Cv;
using CvManagement.Web.ViewModels.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Controllers;

public class CvController(
    ICvRenderService cvRender,
    IProfileAutoSaveService autoSave,
    IPositionAccessEvaluator accessEvaluator,
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = RoleNames.Candidate)]
    public async Task<IActionResult> Create(int positionId)
    {
        var userId = userManager.GetUserId(User)!;

        if (!await accessEvaluator.IsEligibleAsync(userId, positionId))
        {
            return Forbid();
        }

        var existing = await db.Cvs.FirstOrDefaultAsync(c => c.UserId == userId && c.PositionId == positionId);
        if (existing is not null)
        {
            return RedirectToAction(nameof(Details), new { id = existing.Id });
        }

        var cv = new Cv { UserId = userId, PositionId = positionId, Status = CvStatus.Draft };
        db.Cvs.Add(cv);
        await db.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = cv.Id });
    }

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var cv = await db.Cvs.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (cv is null) return NotFound();

        var (canView, canEdit) = await GetAccessAsync(cv, User);
        if (!canView) return NotFound();

        var model = await cvRender.BuildAsync(id);
        if (model is null) return NotFound();

        model.ViewerCanEdit = canEdit;
        model.ViewerCanLike = User.IsInRole(RoleNames.Recruiter) || User.IsInRole(RoleNames.Administrator);
        if (model.ViewerCanLike)
        {
            var likerId = userManager.GetUserId(User)!;
            model.ViewerHasLiked = await db.CvLikes.AnyAsync(l => l.CvId == id && l.RecruiterUserId == likerId);
        }

        return View(model);
    }

    [HttpPost("/api/cv/{id:int}/autosave"), ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> AutoSave(int id, [FromBody] AutoSaveRequest request)
    {
        var cv = await db.Cvs.AsNoTracking()
            .Include(c => c.Position).ThenInclude(p => p.PositionAttributes)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (cv is null) return NotFound();

        var (_, canEdit) = await GetAccessAsync(cv, User);
        if (!canEdit) return Forbid();

        // Defense against a tampered request: only allow writing attributes that are actually part
        // of this position's template, regardless of what the client claims.
        var templateAttributeIds = cv.Position.PositionAttributes.Select(pa => pa.AttributeId).ToHashSet();
        var allowedChanges = request.Changes.Where(c => templateAttributeIds.Contains(c.AttributeId)).ToList();

        var results = await autoSave.SaveAsync(cv.UserId, allowedChanges);
        return Json(new { results });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Publish(int id)
    {
        var cv = await db.Cvs.FirstOrDefaultAsync(c => c.Id == id);
        if (cv is null) return NotFound();

        var (_, canEdit) = await GetAccessAsync(cv, User);
        if (!canEdit) return Forbid();

        var model = await cvRender.BuildAsync(id);
        if (model is null || !model.CanPublish)
        {
            TempData["StatusMessage"] = "Fill in every field before publishing.";
            return RedirectToAction(nameof(Details), new { id });
        }

        cv.Status = CvStatus.Published;
        cv.PublishedAt = DateTime.UtcNow;
        cv.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        TempData["StatusMessage"] = "CV published -- Recruiters can now see it.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet, Authorize(Roles = $"{RoleNames.Recruiter},{RoleNames.Administrator}")]
    public async Task<IActionResult> Browse(string? tag)
    {
        ViewBag.Tag = tag;

        var published = await db.Cvs
            .AsNoTracking()
            .Where(c => c.Status == CvStatus.Published)
            .Select(c => new { c.Id, c.UserId, c.PositionId })
            .ToListAsync();

        // Eligibility is evaluated per candidate (their own attribute values), so batch per UserId --
        // still a handful of queries total, never one per CV.
        var visibleCvIds = new List<int>();
        foreach (var group in published.GroupBy(c => c.UserId))
        {
            var eligibility = await accessEvaluator.IsEligibleForManyAsync(group.Key, group.Select(c => c.PositionId).ToList());
            visibleCvIds.AddRange(group.Where(c => eligibility.GetValueOrDefault(c.PositionId)).Select(c => c.Id));
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            // Tag cloud links Recruiters to CVs whose candidate has a project tagged with that tech.
            var taggedUserIds = await db.ProjectTags
                .Where(pt => pt.Tag.Name == tag)
                .Select(pt => pt.Project.UserId)
                .Distinct()
                .ToListAsync();
            visibleCvIds = await db.Cvs.Where(c => visibleCvIds.Contains(c.Id) && taggedUserIds.Contains(c.UserId))
                .Select(c => c.Id).ToListAsync();
        }

        var items = await db.Cvs
            .AsNoTracking()
            .Where(c => visibleCvIds.Contains(c.Id))
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new CvBrowseItemViewModel
            {
                Id = c.Id,
                CandidateDisplayName = c.User.DisplayName,
                PositionTitle = c.Position.Title,
                LikeCount = c.Likes.Count,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync();

        return View(items);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = $"{RoleNames.Recruiter},{RoleNames.Administrator}")]
    public async Task<IActionResult> Like(int id)
    {
        var userId = userManager.GetUserId(User)!;
        var exists = await db.CvLikes.AnyAsync(l => l.CvId == id && l.RecruiterUserId == userId);
        if (!exists)
        {
            db.CvLikes.Add(new CvLike { CvId = id, RecruiterUserId = userId });
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException) { /* unique-constraint race: already liked by a concurrent request */ }
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = $"{RoleNames.Recruiter},{RoleNames.Administrator}")]
    public async Task<IActionResult> Unlike(int id)
    {
        var userId = userManager.GetUserId(User)!;
        await db.CvLikes.Where(l => l.CvId == id && l.RecruiterUserId == userId).ExecuteDeleteAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// (canView, canEdit). Administrators always both. A losing-eligibility position hides the CV
    /// from everyone including its own candidate, per spec. Candidates never see another candidate's
    /// CV; Recruiters only see Published + currently-eligible CVs, and can never edit.
    /// </summary>
    private async Task<(bool CanView, bool CanEdit)> GetAccessAsync(Cv cv, ClaimsPrincipal viewer)
    {
        if (viewer.IsInRole(RoleNames.Administrator)) return (true, true);

        var viewerId = userManager.GetUserId(viewer);
        var isOwner = viewerId is not null && viewerId == cv.UserId;

        if (!isOwner)
        {
            if (!viewer.IsInRole(RoleNames.Recruiter)) return (false, false); // anonymous / other Candidate
            var visibleToRecruiter = cv.Status == CvStatus.Published
                && await accessEvaluator.IsEligibleAsync(cv.UserId, cv.PositionId);
            return (visibleToRecruiter, false);
        }

        var stillEligible = await accessEvaluator.IsEligibleAsync(cv.UserId, cv.PositionId);
        return (stillEligible, stillEligible);
    }
}
