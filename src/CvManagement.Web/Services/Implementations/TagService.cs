using CvManagement.Web.Data;
using CvManagement.Web.Models;
using CvManagement.Web.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Services.Implementations;

public class TagService(ApplicationDbContext db) : ITagService
{
    public async Task<List<Tag>> ResolveOrCreateTagsAsync(IEnumerable<string> tagNames, CancellationToken ct = default)
    {
        var normalized = tagNames
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalized.Count == 0) return [];

        var existing = await db.Tags.Where(t => normalized.Contains(t.Name)).ToListAsync(ct);
        var missing = normalized.Where(n => !existing.Any(t => string.Equals(t.Name, n, StringComparison.OrdinalIgnoreCase))).ToList();

        var created = missing.Select(n => new Tag { Name = n }).ToList();
        if (created.Count > 0)
        {
            db.Tags.AddRange(created);
            await db.SaveChangesAsync(ct);
        }

        return [.. existing, .. created];
    }

    public async Task<IReadOnlyList<string>> SearchNamesAsync(string prefix, int take = 20, CancellationToken ct = default)
        => await db.Tags
            .Where(t => EF.Functions.Like(t.Name, $"{prefix}%"))
            .OrderBy(t => t.Name)
            .Take(take)
            .Select(t => t.Name)
            .ToListAsync(ct);
}
