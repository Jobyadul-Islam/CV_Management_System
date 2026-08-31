namespace CvManagement.Web.ViewModels.Cv;

public class CvBrowseItemViewModel
{
    public int Id { get; set; }
    public string CandidateDisplayName { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public int LikeCount { get; set; }
    public DateTime UpdatedAt { get; set; }
}
