using CvManagement.Web.ViewModels.Cv;
using CvManagement.Web.ViewModels.Position;

namespace CvManagement.Web.ViewModels.Home;

public class SearchResultsViewModel
{
    public string Query { get; set; } = string.Empty;
    public List<PositionListItemViewModel> Positions { get; set; } = [];

    /// <summary>Only populated for Recruiter/Admin viewers.</summary>
    public List<CvBrowseItemViewModel> Cvs { get; set; } = [];
}
