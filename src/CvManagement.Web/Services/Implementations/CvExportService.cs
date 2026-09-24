using Microsoft.Extensions.Localization;
using System.Text;
using CvManagement.Web.Data;
using CvManagement.Web.Models;
using CvManagement.Web.Models.Enums;
using CvManagement.Web.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Services.Implementations;

public class CvExportService(ApplicationDbContext db, IPositionAccessEvaluator accessEvaluator,
    IStringLocalizer<SharedResource> localizer) : ICvExportService
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

        var headers = new List<string>
        {
            localizer["Cv_ColCandidate"], localizer["Profile_ColStatus"], localizer["Profile_ColLikes"],
            localizer["Export_ColCreated"], localizer["Cv_Published"]
        };
        headers.AddRange(attributes.Select(a => a.Name));
        sb.AppendLine(string.Join(',', headers.Select(CsvField)));

        foreach (var cv in candidates.OrderByDescending(c => c.CreatedAt))
        {
            var userValues = valuesByUser.GetValueOrDefault(cv.UserId);
            var row = new List<string>
            {
                cv.DisplayName,
                localizer[cv.Status == CvStatus.Published ? "Cv_Published" : "Cv_Draft"],
                cv.LikeCount.ToString(),
                cv.CreatedAt.ToString("yyyy-MM-dd"),
                cv.PublishedAt?.ToString("yyyy-MM-dd") ?? string.Empty
            };
            foreach (var attribute in attributes)
            {
                var value = userValues?.GetValueOrDefault(attribute.Id);
                var field = AttributeValueMapper.ToViewModel(attribute, value);
                row.Add(AttributeValueFormatter.ToPlainText(field, localizer));
            }
            sb.AppendLine(string.Join(',', row.Select(CsvField)));
        }

        return sb.ToString();
    }

    /// <summary>
    /// RFC 4180 quoting, plus formula-injection protection: every cell is candidate-authored text that a
    /// Recruiter opens in Excel/Sheets, so a value starting with = + - @ (or tab/CR) is prefixed with an
    /// apostrophe to be shown as text instead of evaluated (OWASP "CSV Injection").
    /// </summary>
    public static string CsvField(string value)
    {
        // A plain number such as "-3.5" is not a formula, and keeping it numeric keeps it aggregatable.
        var isPlainNumber = decimal.TryParse(value, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out _);
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' && !isPlainNumber)
        {
            value = "'" + value;
        }
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0) return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
