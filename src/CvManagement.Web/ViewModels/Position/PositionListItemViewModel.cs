using CvManagement.Web.Models.Enums;

namespace CvManagement.Web.ViewModels.Position;

public class PositionListItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Company { get; set; }
    public PositionLevel? Level { get; set; }
    public PositionAccessMode AccessMode { get; set; }
    public int CvCount { get; set; }
    public DateTime UpdatedAt { get; set; }
}
