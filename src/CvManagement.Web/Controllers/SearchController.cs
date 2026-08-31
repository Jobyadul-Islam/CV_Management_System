using CvManagement.Web.Data;
using CvManagement.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace CvManagement.Web.Controllers;

/// <summary>
/// Header search entry point, reachable from every page. This is a placeholder Contains()-based
/// match over Position titles for now (Positions CRUD lands in Phase 4); Phase 8 replaces the
/// implementation with the Lucene.NET full-text index and adds role-scoped CV search for
/// Recruiters/Admins, without changing this route or the header form that posts to it.
/// </summary>
public class SearchController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? q)
    {
        ViewData["Query"] = q;

        List<Position> positions = [];
        if (!string.IsNullOrWhiteSpace(q))
        {
            positions = await db.Positions
                .Where(p => EF.Functions.Like(p.Title, $"%{q}%") || EF.Functions.Like(p.ShortDescription, $"%{q}%"))
                .OrderByDescending(p => p.UpdatedAt)
                .Take(50)
                .ToListAsync();
        }

        return View(positions);
    }
}
