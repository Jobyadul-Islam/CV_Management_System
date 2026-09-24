using CvManagement.Web.Data;
using CvManagement.Web.Models;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Discussion;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Services.Implementations;

public class DiscussionService(ApplicationDbContext db, IMarkdownRenderer markdown) : IDiscussionService
{
    public async Task<IReadOnlyList<DiscussionPostViewModel>> GetPostsAsync(int positionId, CancellationToken ct = default)
        => await db.DiscussionPosts
            .AsNoTracking()
            .Where(p => p.PositionId == positionId)
            .OrderBy(p => p.Id) // chronological, append-only -- Id (bigint identity) is the ordering key
            .Select(p => new DiscussionPostViewModel
            {
                Id = p.Id,
                AuthorUserId = p.AuthorUserId,
                AuthorDisplayName = p.AuthorDisplayNameSnapshot,
                BodyHtml = markdown.ToSafeHtml(p.BodyMarkdown),
                CreatedAtUtc = p.CreatedAt
            })
            .ToListAsync(ct);

    public async Task<DiscussionPostViewModel?> PostAsync(int positionId, string userId, string bodyMarkdown, CancellationToken ct = default)
    {
        if (!await db.Positions.AnyAsync(p => p.Id == positionId, ct)) return null;
        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);

        var post = new DiscussionPost
        {
            PositionId = positionId,
            AuthorUserId = userId,
            AuthorDisplayNameSnapshot = user.DisplayName,
            BodyMarkdown = bodyMarkdown
        };
        db.DiscussionPosts.Add(post);
        await db.SaveChangesAsync(ct);

        return new DiscussionPostViewModel
        {
            Id = post.Id,
            AuthorUserId = userId,
            AuthorDisplayName = user.DisplayName,
            BodyHtml = markdown.ToSafeHtml(bodyMarkdown),
            CreatedAtUtc = post.CreatedAt
        };
    }
}
