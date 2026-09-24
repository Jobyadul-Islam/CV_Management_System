using System.Diagnostics;
using System.Linq.Expressions;
using CvManagement.Web.Data;
using CvManagement.Web.Models;
using CvManagement.Web.Models.Enums;
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
    private const int LatestCount = 8;
    private const int PopularCount = 5;
    private const int ChartDays = 14;

    public async Task<IActionResult> Index()
    {
        // Positions this viewer may open: everything for Recruiters/Admins; Public ones for anonymous
        // visitors; Public + the Restricted ones whose rules they meet for a signed-in Candidate. Built
        // once as a SQL predicate, so Latest and Most Popular are both ranked across *all* visible
        // positions by the database (ORDER BY ... TOP n), never from a pre-trimmed or fully loaded list.
        var visible = await BuildVisibilityFilterAsync();

        var latest = await ProjectList(db.Positions.Where(visible).OrderByDescending(p => p.UpdatedAt))
            .Take(LatestCount)
            .ToListAsync();

        var popular = await ProjectList(db.Positions.Where(visible)
                .OrderByDescending(p => p.Cvs.Count(c => c.Status == CvStatus.Published))
                .ThenByDescending(p => p.UpdatedAt))
            .Take(PopularCount)
            .ToListAsync();

        var tagCloud = await db.Tags
            .AsNoTracking()
            .Where(t => t.ProjectTags.Any())
            .OrderByDescending(t => t.ProjectTags.Count)
            .Take(25)
            .Select(t => new TagCloudEntry(t.Name, t.ProjectTags.Count))
            .ToListAsync();

        var since = DateTime.UtcNow.AddHours(-24);
        var stats = new SiteStats
        {
            CvsCreatedLast24h = await db.Cvs.CountAsync(c => c.CreatedAt >= since),
            TotalPositions = await db.Positions.CountAsync(),
            TotalCandidates = await CountInRoleAsync(RoleNames.Candidate),
            TotalRecruiters = await CountInRoleAsync(RoleNames.Recruiter),
            // "Submitted" (spec's own word, distinct from "created" just above) means Published --
            // a Draft is a private scratch, not yet submitted for a Recruiter's consideration.
            TotalCvs = await db.Cvs.CountAsync(c => c.Status == CvStatus.Published)
        };

        var model = new HomeIndexViewModel
        {
            LatestPositions = latest,
            PopularPositions = popular,
            TagCloud = tagCloud,
            Stats = stats,
            CvsPerDay = await GetCvsPerDayAsync(),
            PositionsByLevel = await GetPositionsByLevelAsync()
        };

        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private async Task<Expression<Func<Position, bool>>> BuildVisibilityFilterAsync()
    {
        if (User.IsInRole(RoleNames.Recruiter) || User.IsInRole(RoleNames.Administrator))
        {
            return p => true;
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return p => p.AccessMode == PositionAccessMode.Public;
        }

        // Rules are evaluated in C# (RuleEvaluator), so resolve which Restricted positions this
        // candidate qualifies for once -- three queries total -- and hand the ids to SQL.
        var userId = userManager.GetUserId(User)!;
        var restrictedIds = await db.Positions
            .Where(p => p.AccessMode == PositionAccessMode.Restricted)
            .Select(p => p.Id)
            .ToListAsync();
        var eligible = await accessEvaluator.FilterEligibleAsync(restrictedIds.Select(id => (userId, id)).ToList());
        var eligibleIds = eligible.Select(e => e.PositionId).ToList();

        return p => p.AccessMode == PositionAccessMode.Public || eligibleIds.Contains(p.Id);
    }

    private static IQueryable<PositionListItemViewModel> ProjectList(IQueryable<Position> positions)
        => positions.AsNoTracking().Select(p => new PositionListItemViewModel
        {
            Id = p.Id,
            Title = p.Title,
            Company = p.Company,
            Level = p.Level,
            AccessMode = p.AccessMode,
            CvCount = p.Cvs.Count(c => c.Status == CvStatus.Published),
            UpdatedAt = p.UpdatedAt
        });

    /// <summary>One GROUP BY query; days with no CVs are zero-filled so the chart's x-axis is continuous.</summary>
    private async Task<List<ChartPoint>> GetCvsPerDayAsync()
    {
        var firstDay = DateTime.UtcNow.Date.AddDays(-(ChartDays - 1));
        var counts = await db.Cvs
            .Where(c => c.CreatedAt >= firstDay)
            .GroupBy(c => c.CreatedAt.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Day, g => g.Count);

        return Enumerable.Range(0, ChartDays)
            .Select(i => firstDay.AddDays(i))
            .Select(day => new ChartPoint(day.ToString("MMM d"), counts.GetValueOrDefault(day)))
            .ToList();
    }

    private async Task<List<ChartPoint>> GetPositionsByLevelAsync()
    {
        var counts = await db.Positions
            .GroupBy(p => p.Level)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToListAsync();

        return Enum.GetValues<PositionLevel>()
            // Labels are enum names; the view localizes them (Level_Junior, ..., Level_Unspecified).
            .Select(level => new ChartPoint(level.ToString(), counts.FirstOrDefault(c => c.Level == level)?.Count ?? 0))
            .Append(new ChartPoint("Unspecified", counts.FirstOrDefault(c => c.Level == null)?.Count ?? 0))
            .ToList();
    }

    private async Task<int> CountInRoleAsync(string roleName)
        => await (from ur in db.UserRoles
                  join r in db.Roles on ur.RoleId equals r.Id
                  where r.Name == roleName
                  select ur.UserId)
            .CountAsync();
}
