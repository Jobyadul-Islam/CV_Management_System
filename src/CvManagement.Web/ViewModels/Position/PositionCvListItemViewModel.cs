namespace CvManagement.Web.ViewModels.Position;

/// <summary>Row of the Recruiter/Admin-only "CVs created from this position" list on Position Details.</summary>
public class PositionCvListItemViewModel
{
    public int Id { get; set; }
    public string CandidateDisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int LikeCount { get; set; }
    public DateTime UpdatedAt { get; set; }
}
