namespace CvManagement.Web.Services.Abstractions;

public interface IBadgeSvgRenderer
{
    /// <summary>Renders a self-contained, downloadable SVG panel of the given earned badges.</summary>
    string RenderPanel(IReadOnlyList<EarnedBadge> badges, string displayName);
}
