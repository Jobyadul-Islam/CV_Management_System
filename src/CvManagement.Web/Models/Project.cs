namespace CvManagement.Web.Models;

public class Project
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public string DescriptionMarkdown { get; set; } = string.Empty;

    public byte[] RowVersion { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProjectTag> ProjectTags { get; set; } = new List<ProjectTag>();
}
