using CvManagement.Web.Data;
using CvManagement.Web.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Services.Implementations;

public class BadgeService(ApplicationDbContext db) : IBadgeService
{
    private enum Metric { Projects, Cvs, Likes }

    // Fixed catalog, not DB-configurable -- this is a stretch feature ("badges/achievements"), not a
    // rule engine. Thresholds mirror the spec's own examples ("10 projects", "5 CVs", "25 likes") plus
    // a lower tier per metric so the panel isn't empty for a candidate who's only just getting started.
    private static readonly (Metric Metric, int Threshold, string Label, string Icon, string ColorHex)[] Catalog =
    [
        (Metric.Projects, 1, "First Project", "\U0001F4C1", "#0d6efd"),
        (Metric.Projects, 5, "5 Projects", "\U0001F4C1", "#0d6efd"),
        (Metric.Projects, 10, "10 Projects", "\U0001F4C1", "#0d6efd"),
        (Metric.Cvs, 1, "First CV", "\U0001F4C4", "#198754"),
        (Metric.Cvs, 5, "5 CVs", "\U0001F4C4", "#198754"),
        (Metric.Likes, 5, "5 Likes", "⭐", "#fd7e14"),
        (Metric.Likes, 10, "10 Likes", "⭐", "#fd7e14"),
        (Metric.Likes, 25, "25 Likes", "⭐", "#fd7e14"),
    ];

    public async Task<IReadOnlyList<EarnedBadge>> GetEarnedBadgesAsync(string userId, CancellationToken ct = default)
    {
        // Exactly three count queries total, regardless of how many badges are in the catalog.
        var projectCount = await db.Projects.CountAsync(p => p.UserId == userId, ct);
        var cvCount = await db.Cvs.CountAsync(c => c.UserId == userId, ct);
        var likeCount = await db.CvLikes.CountAsync(l => l.Cv.UserId == userId, ct);

        var counts = new Dictionary<Metric, int>
        {
            [Metric.Projects] = projectCount,
            [Metric.Cvs] = cvCount,
            [Metric.Likes] = likeCount
        };

        return Catalog
            .Where(b => counts[b.Metric] >= b.Threshold)
            .Select(b => new EarnedBadge(b.Label, b.Icon, b.ColorHex))
            .ToList();
    }
}
