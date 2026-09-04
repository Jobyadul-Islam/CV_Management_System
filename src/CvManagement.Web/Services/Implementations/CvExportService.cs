using System.Text;
using CvManagement.Web.Data;
using CvManagement.Web.Domain;
using CvManagement.Web.Domain.Enums;
using CvManagement.Web.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Services.Implementations;

public class CvExportService(ApplicationDbContext db, IPositionAccessEvaluator accessEvaluator) : ICvExportService
{
    public async Task<string?> BuildCsvAsync(int positionId, bool isAdmin, CancellationToken ct = default)
    {
        var position = await db.Positions
            .AsNoTracking()
            .Include(p => p.PositionAttributes.OrderBy(pa => pa.SortOrder))
                .ThenInclude(pa => pa.Attribute).ThenInclude(a => a.Options.OrderBy(o => o.SortOrder))
            .FirstOrDefaultAsync(p => p.Id == positionId, ct);
        if (position is null) return null;

        var cvRows = await db.Cvs.AsNoTracking()
            .Where(c => c.PositionId == positionId)
            .Select(c => new
            {
                c.Id,
                c.UserId,
                c.User.DisplayName,
                c.Status,
                LikeCount = c.Likes.Count,
                c.CreatedAt,
                c.PublishedAt
            })
            .ToListAsync(ct);

        var candidates = isAdmin ? cvRows : cvRows.Where(c => c.Status == CvStatus.Published).ToList();

        if (!isAdmin)
        {
            var eligibility = await accessEvaluator.IsEligibleForPositionAsync(
                positionId, candidates.Select(c => c.UserId).Distinct().ToList(), ct);
            candidates = candidates.Where(c => eligibility.GetValueOrDefault(c.UserId)).ToList();
        }

        var attributes = position.PositionAttributes.Select(pa => pa.Attribute).ToList();
        var attributeIds = attributes.Select(a => a.Id).ToList();
        var userIds = candidates.Select(c => c.UserId).Distinct().ToList();

        var valuesByUser = (attributeIds.Count == 0 || userIds.Count == 0
                ? []
                : await db.UserAttributeValues.AsNoTracking()
                    .Where(v => userIds.Contains(v.UserId) && attributeIds.Contains(v.AttributeId))
                    .ToListAsync(ct))
            .GroupBy(v => v.UserId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(v => v.AttributeId));

        var sb = new StringBuilder();

        var headers = new List<string> { "Candidate", "Status", "Likes", "Created", "Published" };
        headers.AddRange(attributes.Select(a => a.Name));
        sb.AppendLine(string.Join(',', headers.Select(CsvField)));

        foreach (var cv in candidates.OrderByDescending(c => c.CreatedAt))
        {
            var userValues = valuesByUser.GetValueOrDefault(cv.UserId);
            var row = new List<string>
            {
                cv.DisplayName,
                cv.Status.ToString(),
                cv.LikeCount.ToString(),
                cv.CreatedAt.ToString("yyyy-MM-dd"),
                cv.PublishedAt?.ToString("yyyy-MM-dd") ?? string.Empty
            };
            foreach (var attribute in attributes)
            {
                var value = userValues?.GetValueOrDefault(attribute.Id);
                var field = AttributeValueMapper.ToViewModel(attribute, value);
                row.Add(AttributeValueFormatter.ToPlainText(field));
            }
            sb.AppendLine(string.Join(',', row.Select(CsvField)));
        }

        return sb.ToString();
    }

    private static string CsvField(string value)
    {
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0) return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
