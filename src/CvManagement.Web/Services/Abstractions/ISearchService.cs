using System.Security.Claims;
using CvManagement.Web.ViewModels.Home;

namespace CvManagement.Web.Services.Abstractions;

/// <summary>
/// Role-scoped header search: Candidates/anonymous search Positions only (filtered to what they're
/// allowed to see); Recruiters/Admins additionally search candidate profile+project text, mapped to
/// that candidate's currently-visible CVs.
/// </summary>
public interface ISearchService
{
    Task<SearchResultsViewModel> SearchAsync(string? query, ClaimsPrincipal viewer, CancellationToken ct = default);
}
