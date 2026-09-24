namespace CvManagement.Web.Models;

public class ProjectTag
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
