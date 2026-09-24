using Microsoft.Extensions.Localization;
using CvManagement.Web.Data;
using CvManagement.Web.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Services.Implementations;

public class BadgeService(ApplicationDbContext db, IStringLocalizer<SharedResource> localizer) : IBadgeService
{
    private enum Metric { Projects, Cvs, Likes }

    // Fixed catalog, not DB-configurable -- this is a stretch feature ("badges/achievements"), not a
    // rule engine. Thresholds mirror the spec's own examples ("10 projects", "5 CVs", "25 likes") plus
    // a lower tier per metric so the panel isn't empty for a candidate who's only just getting started.
    private static readonly (Metric Metric, int Threshold, string LabelKey, string Icon, string ColorHex)[] Catalog =
    [
        (Metric.Projects, 1, "Badge_FirstProject", "\U0001F4C1", "#0d6efd"),
        (Metric.Projects, 5, "Badge_Projects5", "\U0001F4C1", "#0d6efd"),
        (Metric.Projects, 10, "Badge_Projects10", "\U0001F4C1", "#0d6efd"),
        (Metric.Cvs, 1, "Badge_FirstCv", "\U0001F4C4", "#198754"),
        (Metric.Cvs, 5, "Badge_Cvs5", "\U0001F4C4", "#198754"),
        (Metric.Likes, 5, "Badge_Likes5", "⭐", "#fd7e14"),
        (Metric.Likes, 10, "Badge_Likes10", "⭐", "#fd7e14"),
        (Metric.Likes, 25, "Badge_Likes25", "⭐", "#fd7e14"),
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
            .Select(b => new EarnedBadge(localizer[b.LabelKey], b.Icon, b.ColorHex))
            .ToList();
    }
}
