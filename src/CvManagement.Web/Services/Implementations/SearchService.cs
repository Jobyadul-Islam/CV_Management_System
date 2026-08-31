using System.Security.Claims;
using CvManagement.Web.Data;
using CvManagement.Web.Domain;
using CvManagement.Web.Domain.Enums;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Cv;
using CvManagement.Web.ViewModels.Home;
using CvManagement.Web.ViewModels.Position;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Services.Implementations;

public class SearchService(
    ISearchIndexService index,
    IPositionAccessEvaluator accessEvaluator,
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : ISearchService
{
    public async Task<SearchResultsViewModel> SearchAsync(string? query, ClaimsPrincipal viewer, CancellationToken ct = default)
    {
        var result = new SearchResultsViewModel { Query = query ?? string.Empty };
        if (string.IsNullOrWhiteSpace(query)) return result;

        var isRecruiter = viewer.IsInRole(RoleNames.Recruiter) || viewer.IsInRole(RoleNames.Administrator);
        var hits = index.Search(query, includeCandidates: isRecruiter);

        result.Positions = await ResolvePositionsAsync(hits.PositionIds, viewer, isRecruiter, ct);

        if (isRecruiter && hits.CandidateUserIds.Count > 0)
        {
            result.Cvs = await ResolveCandidateCvsAsync(hits.CandidateUserIds, ct);
        }

        return result;
    }

    private async Task<List<PositionListItemViewModel>> ResolvePositionsAsync(
        IReadOnlyList<int> positionIds, ClaimsPrincipal viewer, bool isRecruiter, CancellationToken ct)
    {
        if (positionIds.Count == 0) return [];

        var positions = await db.Positions
            .AsNoTracking()
            .Where(p => positionIds.Contains(p.Id))
            .Select(p => new PositionListItemViewModel
            {
                Id = p.Id,
                Title = p.Title,
                Company = p.Company,
                Level = p.Level,
                AccessMode = p.AccessMode,
                CvCount = p.Cvs.Count,
                UpdatedAt = p.UpdatedAt
            })
            .ToListAsync(ct);

        if (isRecruiter) return positions;

        if (viewer.Identity?.IsAuthenticated == true)
        {
            var userId = userManager.GetUserId(viewer)!;
            var eligibility = await accessEvaluator.IsEligibleForManyAsync(userId, positions.Select(p => p.Id).ToList(), ct);
            return positions.Where(p => eligibility.GetValueOrDefault(p.Id)).ToList();
        }

        // Anonymous: Public positions only.
        return positions.Where(p => p.AccessMode == PositionAccessMode.Public).ToList();
    }

    private async Task<List<CvBrowseItemViewModel>> ResolveCandidateCvsAsync(IReadOnlyList<string> candidateUserIds, CancellationToken ct)
    {
        var published = await db.Cvs
            .AsNoTracking()
            .Where(c => candidateUserIds.Contains(c.UserId) && c.Status == CvStatus.Published)
            .Select(c => new { c.Id, c.UserId, c.PositionId })
            .ToListAsync(ct);

        var visibleIds = new List<int>();
        foreach (var group in published.GroupBy(c => c.UserId))
        {
            var eligibility = await accessEvaluator.IsEligibleForManyAsync(group.Key, group.Select(c => c.PositionId).ToList(), ct);
            visibleIds.AddRange(group.Where(c => eligibility.GetValueOrDefault(c.PositionId)).Select(c => c.Id));
        }
        if (visibleIds.Count == 0) return [];

        return await db.Cvs
            .AsNoTracking()
            .Where(c => visibleIds.Contains(c.Id))
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new CvBrowseItemViewModel
            {
                Id = c.Id,
                CandidateDisplayName = c.User.DisplayName,
                PositionTitle = c.Position.Title,
                LikeCount = c.Likes.Count,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync(ct);
    }
}
