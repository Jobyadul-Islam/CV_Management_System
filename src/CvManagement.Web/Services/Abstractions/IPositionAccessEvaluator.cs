namespace CvManagement.Web.Services.Abstractions;

/// <summary>
/// Single source of truth for "is this candidate allowed to create/see a CV for this position."
/// Backs: eligible-positions listing, "Create CV" gating, and CV visibility (a CV becomes hidden --
/// from the candidate AND from Recruiters, per spec -- if the position's rules later exclude them).
/// </summary>
public interface IPositionAccessEvaluator
{
    Task<bool> IsEligibleAsync(string userId, int positionId, CancellationToken ct = default);

    /// <summary>Bulk variant: exactly two queries total regardless of position count, never one per row.</summary>
    Task<IReadOnlyDictionary<int, bool>> IsEligibleForManyAsync(
        string userId, IReadOnlyCollection<int> positionIds, CancellationToken ct = default);
}
