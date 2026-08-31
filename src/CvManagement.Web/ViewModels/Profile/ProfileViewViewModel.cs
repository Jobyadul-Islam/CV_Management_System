namespace CvManagement.Web.ViewModels.Profile;

/// <summary>Read-only profile view for Recruiters/Admins, e.g. reached from a discussion post author link.</summary>
public class ProfileViewViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public IReadOnlyList<AttributeValueViewModel> MeAttributes { get; set; } = [];
    public IReadOnlyList<CvSummaryViewModel> PublishedCvs { get; set; } = [];
}
