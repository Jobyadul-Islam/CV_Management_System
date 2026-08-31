using CvManagement.Web.Domain.Enums;

namespace CvManagement.Web.Domain;

/// <summary>
/// Technical fields only -- almost "virtual". Content is never stored here: rendering a CV means
/// looking up Position.PositionAttributes against the candidate's UserAttributeValue rows, and
/// Position.ProjectTags against the candidate's tagged Projects, at read time. See ICvRenderService.
/// </summary>
public class Cv
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;

    public CvStatus Status { get; set; } = CvStatus.Draft;

    public byte[] RowVersion { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }

    public ICollection<CvLike> Likes { get; set; } = new List<CvLike>();
}
