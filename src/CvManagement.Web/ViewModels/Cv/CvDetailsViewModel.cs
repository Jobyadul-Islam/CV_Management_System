using CvManagement.Web.Models.Enums;
using CvManagement.Web.ViewModels.Profile;

namespace CvManagement.Web.ViewModels.Cv;

/// <summary>
/// A fully-rendered CV. Built at read time by ICvRenderService -- nothing here is stored on the Cv
/// row itself; Fields and Projects are looked up live from the candidate's profile each time.
/// </summary>
public class CvDetailsViewModel
{
    public int CvId { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public CvStatus Status { get; set; }
    public DateTime? PublishedAt { get; set; }

    public string CandidateUserId { get; set; } = string.Empty;
    public string CandidateDisplayName { get; set; } = string.Empty;

    public int PositionId { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public string? Company { get; set; }
    public PositionLevel? Level { get; set; }

    public List<AttributeValueViewModel> Fields { get; set; } = [];
    public List<ProjectListItemViewModel> Projects { get; set; } = [];

    public int LikeCount { get; set; }
    public bool ViewerHasLiked { get; set; }

    public bool CanPublish => Fields.All(f => !f.NeedsValue);

    // Set by the controller based on the viewer, not computed here (rendering has no viewer identity).
    public bool ViewerCanEdit { get; set; }
    public bool ViewerCanLike { get; set; }
}
