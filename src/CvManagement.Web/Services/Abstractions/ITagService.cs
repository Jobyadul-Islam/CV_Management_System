using CvManagement.Web.Domain;

namespace CvManagement.Web.Services.Abstractions;

/// <summary>Shared technology-tag resolution, used by Project tags and Position project-tag filters.</summary>
public interface ITagService
{
    Task<List<Tag>> ResolveOrCreateTagsAsync(IEnumerable<string> tagNames, CancellationToken ct = default);
    Task<IReadOnlyList<string>> SearchNamesAsync(string prefix, int take = 20, CancellationToken ct = default);
}
