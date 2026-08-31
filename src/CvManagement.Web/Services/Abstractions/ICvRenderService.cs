using CvManagement.Web.ViewModels.Cv;

namespace CvManagement.Web.Services.Abstractions;

/// <summary>
/// Builds a CV's display content at read time: Position's template attributes joined against the
/// candidate's UserAttributeValue rows (a template attribute with no matching row renders empty,
/// per "attributes not presented in profile are empty by default"), and the candidate's Projects
/// filtered by the position's project tags, capped at MaxProjects. Never reads from the Cv row
/// itself beyond its technical fields -- there is no stored content to read.
/// </summary>
public interface ICvRenderService
{
    Task<CvDetailsViewModel?> BuildAsync(int cvId, CancellationToken ct = default);
}
