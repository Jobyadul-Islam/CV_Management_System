namespace CvManagement.Web.ViewModels.Discussion;

public class DiscussionPostViewModel
{
    public long Id { get; set; }
    public string? AuthorUserId { get; set; }
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
