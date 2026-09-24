using Microsoft.Extensions.Localization;
using System.Security.Claims;
using CvManagement.Web.Data;
using CvManagement.Web.Models;
using CvManagement.Web.Models.Enums;
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
    ICvPdfExportService pdfExport,
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IStringLocalizer<SharedResource> localizer) : Controller
{
    /// <summary>
    /// Creates the caller's own CV for a position. Candidates must satisfy the position's access rules;
    /// Administrators "perform all Candidate actions" with unrestricted access, so the rules are skipped
    /// for them (the CV is still their own, built from their own profile).
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = $"{RoleNames.Candidate},{RoleNames.Administrator}")]
    public async Task<IActionResult> Create(int positionId)
    {
        var userId = userManager.GetUserId(User)!;

        if (User.IsInRole(RoleNames.Administrator))
        {
            if (!await db.Positions.AnyAsync(p => p.Id == positionId)) return NotFound();
        }
        else if (!await accessEvaluator.IsEligibleAsync(userId, positionId))
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

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> ExportPdf(int id)
    {
        var cv = await db.Cvs.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (cv is null) return NotFound();

        var (canView, _) = await GetAccessAsync(cv, User);
        if (!canView) return NotFound();

        var model = await cvRender.BuildAsync(id);
        if (model is null) return NotFound();

        var qrUrl = Url.Action(nameof(Details), "Cv", new { id }, protocol: Request.Scheme)!;
        var pdfBytes = pdfExport.Generate(model, qrUrl);

        var safeName = string.Concat($"{model.CandidateDisplayName}-{model.PositionTitle}"
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or ' ' ? ch : '-'));
        return File(pdfBytes, "application/pdf", $"CV-{safeName}.pdf");
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
            TempData["StatusMessage"] = localizer["Msg_FillBeforePublish"].Value;
            return RedirectToAction(nameof(Details), new { id });
        }

        cv.Status = CvStatus.Published;
        cv.PublishedAt = DateTime.UtcNow;
        cv.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        TempData["StatusMessage"] = localizer["Msg_CvPublished"].Value;
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

        // All (candidate, position) pairs are checked in one bulk call -- two queries total.
        var eligible = await accessEvaluator.FilterEligibleAsync(published.Select(c => (c.UserId, c.PositionId)).ToList());
        var visibleCvIds = published.Where(c => eligible.Contains((c.UserId, c.PositionId))).Select(c => c.Id).ToList();

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
        // Only a CV the Recruiter can actually open may be liked -- not a Draft or a hidden CV guessed by id.
        var cv = await db.Cvs.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (cv is null) return NotFound();
        var (canView, _) = await GetAccessAsync(cv, User);
        if (!canView) return NotFound();

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
    /// "Existing CVs can be edited or deleted" (spec, Profile CVs tab). Set-based: the selection is
    /// narrowed to CVs the caller may edit (an Administrator: any; a Candidate: their own, still-visible
    /// ones), then removed with one DELETE -- CvLikes cascade in the database (CvConfiguration).
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Delete(int[] ids, string? userId)
    {
        var selected = await db.Cvs.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.UserId, c.PositionId })
            .ToListAsync();

        List<int> deletable;
        if (User.IsInRole(RoleNames.Administrator))
        {
            deletable = selected.Select(c => c.Id).ToList();
        }
        else
        {
            var callerId = userManager.GetUserId(User);
            var own = selected.Where(c => c.UserId == callerId).ToList();
            var eligible = await accessEvaluator.FilterEligibleAsync(own.Select(c => (c.UserId, c.PositionId)).ToList());
            deletable = own.Where(c => eligible.Contains((c.UserId, c.PositionId))).Select(c => c.Id).ToList();
        }

        var deletedCount = deletable.Count == 0 ? 0 : await db.Cvs.Where(c => deletable.Contains(c.Id)).ExecuteDeleteAsync();

        TempData["StatusMessage"] = localizer["Msg_CvsDeleted", deletedCount].Value;
        return RedirectToAction("Index", "Profile", new { tab = "cvs", userId });
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
