using CvManagement.Web.ViewModels.Discussion;

namespace CvManagement.Web.Services.Abstractions;

/// <summary>Per-position discussion. Strictly append-only -- there is deliberately no edit/delete method.</summary>
public interface IDiscussionService
{
    Task<IReadOnlyList<DiscussionPostViewModel>> GetPostsAsync(int positionId, CancellationToken ct = default);
    public const int MaxBodyLength = 4000;

    /// <summary>Null when the position doesn't exist (e.g. deleted while the page was open).</summary>
    Task<DiscussionPostViewModel?> PostAsync(int positionId, string userId, string bodyMarkdown, CancellationToken ct = default);
}
