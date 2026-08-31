using System.Diagnostics;
using CvManagement.Web.Data;
using CvManagement.Web.Domain;
using CvManagement.Web.Domain.Enums;
using CvManagement.Web.Models;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Home;
using CvManagement.Web.ViewModels.Position;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Controllers;

public class HomeController(
    ApplicationDbContext db,
    IPositionAccessEvaluator accessEvaluator,
    UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Index()
    {
        var isManaging = User.IsInRole(RoleNames.Recruiter) || User.IsInRole(RoleNames.Administrator);

        // Candidates/anonymous only ever see positions they're actually allowed to open -- fetch a
        // slightly larger candidate pool, filter by visibility, then trim to the display count, so a
        // restricted position never appears here for a viewer who'd get a 404 clicking into it.
        var candidatePool = await db.Positions
            .AsNoTracking()
            .OrderByDescending(p => p.UpdatedAt)
            .Take(isManaging ? 8 : 30)
            .Select(p => new PositionListItemViewModel
            {
                Id = p.Id, Title = p.Title, Company = p.Company, Level = p.Level,
                AccessMode = p.AccessMode, CvCount = p.Cvs.Count, UpdatedAt = p.UpdatedAt
            })
            .ToListAsync();

        var visiblePool = await FilterVisibleAsync(candidatePool, isManaging);
        var latest = visiblePool.Take(8).ToList();

        var popularPool = isManaging
            ? candidatePool
            : await FilterVisibleAsync(
                await db.Positions.AsNoTracking()
                    .Select(p => new PositionListItemViewModel
                    {
                        Id = p.Id, Title = p.Title, Company = p.Company, Level = p.Level,
                        AccessMode = p.AccessMode, CvCount = p.Cvs.Count, UpdatedAt = p.UpdatedAt
                    })
                    .ToListAsync(),
                isManaging);
        var popular = popularPool.OrderByDescending(p => p.CvCount).ThenByDescending(p => p.UpdatedAt).Take(5).ToList();

        var tagCounts = await db.Tags
            .AsNoTracking()
            .Select(t => new { t.Name, Count = t.ProjectTags.Count })
            .ToListAsync();
        var tagCloud = tagCounts
            .Where(t => t.Count > 0)
            .OrderByDescending(t => t.Count)
            .Take(25)
            .Select(t => new TagCloudEntry(t.Name, t.Count))
            .ToList();

        var since = DateTime.UtcNow.AddHours(-24);
        var stats = new SiteStats
        {
            CvsCreatedLast24h = await db.Cvs.CountAsync(c => c.CreatedAt >= since),
            TotalPositions = await db.Positions.CountAsync(),
            TotalCandidates = await CountInRoleAsync(RoleNames.Candidate),
            TotalRecruiters = await CountInRoleAsync(RoleNames.Recruiter),
            TotalCvs = await db.Cvs.CountAsync()
        };

        var model = new HomeIndexViewModel
        {
            LatestPositions = latest,
            PopularPositions = popular,
            TagCloud = tagCloud,
            Stats = stats
        };

        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private async Task<List<PositionListItemViewModel>> FilterVisibleAsync(List<PositionListItemViewModel> positions, bool isManaging)
    {
        if (isManaging) return positions;

        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = userManager.GetUserId(User)!;
            var eligibility = await accessEvaluator.IsEligibleForManyAsync(userId, positions.Select(p => p.Id).ToList());
            return positions.Where(p => eligibility.GetValueOrDefault(p.Id)).ToList();
        }

        return positions.Where(p => p.AccessMode == PositionAccessMode.Public).ToList();
    }

    private async Task<int> CountInRoleAsync(string roleName)
        => await (from ur in db.UserRoles
                  join r in db.Roles on ur.RoleId equals r.Id
                  where r.Name == roleName
                  select ur.UserId)
            .CountAsync();
}
