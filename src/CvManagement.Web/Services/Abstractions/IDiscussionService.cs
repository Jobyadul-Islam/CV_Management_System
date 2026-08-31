using CvManagement.Web.ViewModels.Discussion;

namespace CvManagement.Web.Services.Abstractions;

/// <summary>Per-position discussion. Strictly append-only -- there is deliberately no edit/delete method.</summary>
public interface IDiscussionService
{
    Task<IReadOnlyList<DiscussionPostViewModel>> GetPostsAsync(int positionId, CancellationToken ct = default);
    Task<DiscussionPostViewModel> PostAsync(int positionId, string userId, string bodyMarkdown, CancellationToken ct = default);
}
