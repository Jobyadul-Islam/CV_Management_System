using CvManagement.Web.Domain.Enums;

namespace CvManagement.Web.Domain;

/// <summary>
/// A CV template, shared by all Recruiters (no ownership). Defines which attributes a CV for this
/// position shows, who is allowed to create/see a CV for it, and which candidate projects qualify.
/// </summary>
public class Position
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;

    public string? Company { get; set; }
    public PositionLevel? Level { get; set; }

    public PositionAccessMode AccessMode { get; set; } = PositionAccessMode.Public;

    public int MaxProjects { get; set; } = 5;

    public byte[] RowVersion { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }
    public string? UpdatedByUserId { get; set; }

    public ICollection<PositionAttribute> PositionAttributes { get; set; } = new List<PositionAttribute>();
    public ICollection<PositionAccessRule> AccessRules { get; set; } = new List<PositionAccessRule>();
    public ICollection<PositionProjectTag> ProjectTags { get; set; } = new List<PositionProjectTag>();
    public ICollection<Cv> Cvs { get; set; } = new List<Cv>();
    public ICollection<DiscussionPost> DiscussionPosts { get; set; } = new List<DiscussionPost>();
}
