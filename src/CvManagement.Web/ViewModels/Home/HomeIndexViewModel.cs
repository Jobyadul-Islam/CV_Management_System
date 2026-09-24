using CvManagement.Web.ViewModels.Position;

namespace CvManagement.Web.ViewModels.Home;

public class HomeIndexViewModel
{
    public List<PositionListItemViewModel> LatestPositions { get; set; } = [];
    public List<PositionListItemViewModel> PopularPositions { get; set; } = [];
    public List<TagCloudEntry> TagCloud { get; set; } = [];
    public SiteStats Stats { get; set; } = new();

    /// <summary>New CVs per UTC day, oldest first, one entry per day (zero-filled) -- Chart.js bar chart.</summary>
    public List<ChartPoint> CvsPerDay { get; set; } = [];

    /// <summary>Position count per level (plus "unspecified") -- Chart.js bar chart.</summary>
    public List<ChartPoint> PositionsByLevel { get; set; } = [];
}

public record ChartPoint(string Label, int Value);

public record TagCloudEntry(string Name, int Count);

public class SiteStats
{
    public int CvsCreatedLast24h { get; set; }
    public int TotalPositions { get; set; }
    public int TotalCandidates { get; set; }
    public int TotalRecruiters { get; set; }
    public int TotalCvs { get; set; }
}
