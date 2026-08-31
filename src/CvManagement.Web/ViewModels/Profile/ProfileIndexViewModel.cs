namespace CvManagement.Web.ViewModels.Profile;

public class ProfileIndexViewModel
{
    public string ActiveTab { get; set; } = "me";
    public string DisplayName { get; set; } = string.Empty;
    public bool IsOwner { get; set; } = true;

    public IReadOnlyList<AttributeValueViewModel> MeAttributes { get; set; } = [];
    public IReadOnlyList<AttributeValueViewModel> InfoAttributes { get; set; } = [];
    public IReadOnlyList<ProjectListItemViewModel> Projects { get; set; } = [];
    public IReadOnlyList<CvSummaryViewModel> Cvs { get; set; } = [];
}

public class CvSummaryViewModel
{
    public int Id { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int LikeCount { get; set; }
}
