using System.Diagnostics;
using CvManagement.Web.Data;
using CvManagement.Web.Domain;
using CvManagement.Web.Models;
using CvManagement.Web.ViewModels.Home;
using CvManagement.Web.ViewModels.Position;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Controllers;

public class HomeController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var latest = await db.Positions
            .AsNoTracking()
            .OrderByDescending(p => p.UpdatedAt)
            .Take(8)
            .Select(p => new PositionListItemViewModel
            {
                Id = p.Id, Title = p.Title, Company = p.Company, Level = p.Level,
                AccessMode = p.AccessMode, CvCount = p.Cvs.Count, UpdatedAt = p.UpdatedAt
            })
            .ToListAsync();

        var popular = await db.Positions
            .AsNoTracking()
            .OrderByDescending(p => p.Cvs.Count)
            .ThenByDescending(p => p.UpdatedAt)
            .Take(5)
            .Select(p => new PositionListItemViewModel
            {
                Id = p.Id, Title = p.Title, Company = p.Company, Level = p.Level,
                AccessMode = p.AccessMode, CvCount = p.Cvs.Count, UpdatedAt = p.UpdatedAt
            })
            .ToListAsync();

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

    private async Task<int> CountInRoleAsync(string roleName)
        => await (from ur in db.UserRoles
                  join r in db.Roles on ur.RoleId equals r.Id
                  where r.Name == roleName
                  select ur.UserId)
            .CountAsync();
}
