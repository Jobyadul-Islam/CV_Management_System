using CvManagement.Web.Data;
using CvManagement.Web.Models;
using CvManagement.Web.Models.Enums;
using CvManagement.Web.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Services.Implementations;

public class PositionAccessEvaluator(ApplicationDbContext db) : IPositionAccessEvaluator
{
    public async Task<bool> IsEligibleAsync(string userId, int positionId, CancellationToken ct = default)
    {
        var result = await IsEligibleForManyAsync(userId, [positionId], ct);
        return result.TryGetValue(positionId, out var eligible) && eligible;
    }

    public async Task<IReadOnlyDictionary<int, bool>> IsEligibleForManyAsync(
        string userId, IReadOnlyCollection<int> positionIds, CancellationToken ct = default)
    {
        var eligible = await FilterEligibleAsync(positionIds.Distinct().Select(id => (userId, id)).ToList(), ct);
        // Positions that no longer exist simply aren't eligible.
        return positionIds.Distinct().ToDictionary(id => id, id => eligible.Contains((userId, id)));
    }

    public async Task<IReadOnlySet<(string UserId, int PositionId)>> FilterEligibleAsync(
        IReadOnlyCollection<(string UserId, int PositionId)> pairs, CancellationToken ct = default)
    {
        var eligible = new HashSet<(string UserId, int PositionId)>();
        if (pairs.Count == 0) return eligible;

        var positionIds = pairs.Select(p => p.PositionId).Distinct().ToList();
        var positions = await db.Positions
            .AsNoTracking()
            .Where(p => positionIds.Contains(p.Id))
            .Include(p => p.AccessRules).ThenInclude(r => r.Attribute)
            .ToDictionaryAsync(p => p.Id, ct);

        // Only candidates who have a pair on a Restricted position need their values loaded at all.
        var restrictedPairs = pairs
            .Where(p => positions.TryGetValue(p.PositionId, out var pos) && pos.AccessMode == PositionAccessMode.Restricted)
            .ToList();
        var userIds = restrictedPairs.Select(p => p.UserId).Distinct().ToList();
        var attributeIds = restrictedPairs
            .SelectMany(p => positions[p.PositionId].AccessRules.Select(r => r.AttributeId))
            .Distinct()
            .ToList();

        var valuesByUser = userIds.Count == 0 || attributeIds.Count == 0
            ? new Dictionary<string, Dictionary<int, UserAttributeValue>>()
            : (await db.UserAttributeValues
                    .AsNoTracking()
                    .Where(v => userIds.Contains(v.UserId) && attributeIds.Contains(v.AttributeId))
                    .ToListAsync(ct))
                .GroupBy(v => v.UserId)
                .ToDictionary(g => g.Key, g => g.ToDictionary(v => v.AttributeId));

        foreach (var pair in pairs)
        {
            if (!positions.TryGetValue(pair.PositionId, out var position)) continue;
            if (IsEligible(position, valuesByUser.GetValueOrDefault(pair.UserId))) eligible.Add(pair);
        }

        return eligible;
    }

    /// <summary>
    /// The one place the eligibility rule is spelled out. Public: always. Restricted with zero rules:
    /// never -- an empty AND is vacuously true, which would silently open a "restricted" position to
    /// everyone. Otherwise every rule must pass.
    /// </summary>
    private static bool IsEligible(Position position, IReadOnlyDictionary<int, UserAttributeValue>? values)
    {
        if (position.AccessMode == PositionAccessMode.Public) return true;
        if (position.AccessRules.Count == 0) return false;
        return position.AccessRules.All(rule => RuleEvaluator.Evaluate(rule, values?.GetValueOrDefault(rule.AttributeId)));
    }

    public async Task<IReadOnlyDictionary<string, bool>> IsEligibleForPositionAsync(
        int positionId, IReadOnlyCollection<string> userIds, CancellationToken ct = default)
    {
        var eligible = await FilterEligibleAsync(userIds.Distinct().Select(u => (u, positionId)).ToList(), ct);
        return userIds.Distinct().ToDictionary(u => u, u => eligible.Contains((u, positionId)));
    }
}
