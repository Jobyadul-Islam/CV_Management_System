using CvManagement.Web.Domain.Enums;
using CvManagement.Web.ViewModels.Discussion;

namespace CvManagement.Web.ViewModels.Position;

public class PositionDetailsViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? Company { get; set; }
    public PositionLevel? Level { get; set; }
    public PositionAccessMode AccessMode { get; set; }
    public int MaxProjects { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<string> TemplateAttributeNames { get; set; } = [];
    public List<string> ProjectTagNames { get; set; } = [];
    public List<string> AccessRuleDescriptions { get; set; } = [];

    /// <summary>Set only for the viewing Candidate; null for Recruiter/Admin/anonymous viewers.</summary>
    public bool? ViewerIsEligible { get; set; }
    public int? ViewerExistingCvId { get; set; }

    public bool ViewerCanSeeDiscussion { get; set; }
    public bool ViewerIsRecruiter { get; set; }
    public List<DiscussionPostViewModel> DiscussionPosts { get; set; } = [];
}
