namespace CvManagement.Web.Domain;

/// <summary>Shared technology tag library, used by both Project tags and Position project-tag filters.</summary>
public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<ProjectTag> ProjectTags { get; set; } = new List<ProjectTag>();
    public ICollection<PositionProjectTag> PositionTags { get; set; } = new List<PositionProjectTag>();
}
