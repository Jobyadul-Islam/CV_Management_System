using CvManagement.Web.Data;
using CvManagement.Web.Domain;
using CvManagement.Web.Domain.Enums;
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
        var positions = await db.Positions
            .AsNoTracking()
            .Where(p => positionIds.Contains(p.Id))
            .Include(p => p.AccessRules).ThenInclude(r => r.Attribute)
            .ToListAsync(ct);

        var attributeIds = positions
            .SelectMany(p => p.AccessRules.Select(r => r.AttributeId))
            .Distinct()
            .ToList();

        var values = attributeIds.Count == 0
            ? new Dictionary<int, UserAttributeValue>()
            : await db.UserAttributeValues
                .AsNoTracking()
                .Where(v => v.UserId == userId && attributeIds.Contains(v.AttributeId))
                .ToDictionaryAsync(v => v.AttributeId, ct);

        var result = new Dictionary<int, bool>();
        foreach (var position in positions)
        {
            if (position.AccessMode == PositionAccessMode.Public)
            {
                result[position.Id] = true;
                continue;
            }

            // A Restricted position with zero rules is never eligible -- an empty AND is vacuously
            // true, which would silently open a "restricted" position to everyone.
            if (position.AccessRules.Count == 0)
            {
                result[position.Id] = false;
                continue;
            }

            result[position.Id] = position.AccessRules.All(rule =>
                RuleEvaluator.Evaluate(rule, values.GetValueOrDefault(rule.AttributeId)));
        }

        // Positions that no longer exist simply aren't eligible.
        foreach (var id in positionIds.Except(result.Keys))
        {
            result[id] = false;
        }

        return result;
    }
}
