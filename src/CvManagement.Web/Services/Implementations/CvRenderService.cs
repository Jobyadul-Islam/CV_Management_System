using CvManagement.Web.Data;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Cv;
using CvManagement.Web.ViewModels.Profile;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Services.Implementations;

public class CvRenderService(ApplicationDbContext db) : ICvRenderService
{
    public async Task<CvDetailsViewModel?> BuildAsync(int cvId, CancellationToken ct = default)
    {
        var cv = await db.Cvs
            .AsNoTracking()
            .Include(c => c.User)
            .Include(c => c.Position).ThenInclude(p => p.PositionAttributes.OrderBy(pa => pa.SortOrder))
                .ThenInclude(pa => pa.Attribute).ThenInclude(a => a.Options.OrderBy(o => o.SortOrder))
            .Include(c => c.Position).ThenInclude(p => p.ProjectTags)
            .FirstOrDefaultAsync(c => c.Id == cvId, ct);
        if (cv is null) return null;

        var attributeIds = cv.Position.PositionAttributes.Select(pa => pa.AttributeId).ToList();
        var values = await db.UserAttributeValues
            .AsNoTracking()
            .Where(v => v.UserId == cv.UserId && attributeIds.Contains(v.AttributeId))
            .ToDictionaryAsync(v => v.AttributeId, ct);

        var fields = cv.Position.PositionAttributes
            .Select(pa => AttributeValueMapper.ToViewModel(pa.Attribute, values.GetValueOrDefault(pa.AttributeId)))
            .ToList();

        var tagIds = cv.Position.ProjectTags.Select(t => t.TagId).ToHashSet();
        var projects = tagIds.Count == 0
            ? []
            : await db.Projects
                .AsNoTracking()
                .Where(p => p.UserId == cv.UserId && p.ProjectTags.Any(t => tagIds.Contains(t.TagId)))
                .OrderByDescending(p => p.PeriodEnd == null).ThenByDescending(p => p.PeriodStart)
                .Take(cv.Position.MaxProjects)
                .Select(p => new ProjectListItemViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    PeriodStart = p.PeriodStart,
                    PeriodEnd = p.PeriodEnd,
                    Tags = p.ProjectTags.Select(t => t.Tag.Name).ToList(),
                    DescriptionMarkdown = p.DescriptionMarkdown
                })
                .ToListAsync(ct);

        var likeCount = await db.CvLikes.CountAsync(l => l.CvId == cvId, ct);

        return new CvDetailsViewModel
        {
            CvId = cv.Id,
            RowVersion = Convert.ToBase64String(cv.RowVersion),
            Status = cv.Status,
            PublishedAt = cv.PublishedAt,
            CandidateUserId = cv.UserId,
            CandidateDisplayName = cv.User.DisplayName,
            PositionId = cv.PositionId,
            PositionTitle = cv.Position.Title,
            Company = cv.Position.Company,
            Level = cv.Position.Level,
            Fields = fields,
            Projects = projects,
            LikeCount = likeCount
        };
    }
}
