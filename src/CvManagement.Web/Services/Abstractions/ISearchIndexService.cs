namespace CvManagement.Web.Services.Abstractions;

public record SearchIndexResults(IReadOnlyList<int> PositionIds, IReadOnlyList<string> CandidateUserIds);

/// <summary>
/// Lucene.NET-backed full-text index (SQL Server Full-Text Search isn't installed on this machine --
/// the spec explicitly allows "native database features OR an external library"). Two document
/// "types" share one index, disambiguated by a doctype field: positions (Title/ShortDescription/
/// Company) and candidates (DisplayName + all their String/Text attribute values + their project
/// names/descriptions, as one blob per user -- good enough to answer "which candidates match this
/// term," which the caller then maps to that candidate's actual visible CVs).
/// </summary>
public interface ISearchIndexService
{
    Task RebuildAllAsync(CancellationToken ct = default);
    Task IndexPositionAsync(int positionId, CancellationToken ct = default);
    Task RemovePositionAsync(int positionId, CancellationToken ct = default);
    Task IndexCandidateAsync(string userId, CancellationToken ct = default);
    Task RemoveCandidateAsync(string userId, CancellationToken ct = default);
    SearchIndexResults Search(string queryText, bool includeCandidates, int maxResults = 50);
}
