namespace CvManagement.Web.Models;

/// <summary>
/// Append-only per-position discussion post. Immutable by design -- there is deliberately no
/// update/delete service method, rather than enforcing "append-only" with a flag.
/// </summary>
public class DiscussionPost
{
    public long Id { get; set; }

    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;

    /// <summary>Null if the author's account has since been deleted; the post itself survives.</summary>
    public string? AuthorUserId { get; set; }
    public ApplicationUser? AuthorUser { get; set; }

    /// <summary>Snapshot of the author's display name at post time, so history renders without a join.</summary>
    public string AuthorDisplayNameSnapshot { get; set; } = string.Empty;

    public string BodyMarkdown { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
