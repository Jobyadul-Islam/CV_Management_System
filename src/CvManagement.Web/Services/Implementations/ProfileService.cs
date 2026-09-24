using CvManagement.Web.Data;
using CvManagement.Web.Models;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Attribute;
using CvManagement.Web.ViewModels.Profile;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Services.Implementations;

public class ProfileService(ApplicationDbContext db) : IProfileService
{
    public async Task<IReadOnlyList<AttributeValueViewModel>> GetMeTabAsync(string userId, CancellationToken ct = default)
        => await LoadValuesAsync(userId, isBuiltIn: true, ct);

    public async Task<IReadOnlyList<AttributeValueViewModel>> GetInfoTabAsync(string userId, CancellationToken ct = default)
        => await LoadValuesAsync(userId, isBuiltIn: false, ct);

    public async Task<IReadOnlyList<int>> GetInfoTabAttributeIdsAsync(string userId, CancellationToken ct = default)
        => await db.UserAttributeValues
            .Where(v => v.UserId == userId && !v.Attribute.IsBuiltIn)
            .Select(v => v.AttributeId)
            .ToListAsync(ct);

    public async Task AddToInfoAsync(string userId, IReadOnlyCollection<int> attributeIds, CancellationToken ct = default)
    {
        // One query for what's already there and what actually exists, one INSERT batch for the rest.
        var existing = await db.UserAttributeValues
            .Where(v => v.UserId == userId && attributeIds.Contains(v.AttributeId))
            .Select(v => v.AttributeId)
            .ToListAsync(ct);
        var toAdd = await db.Attributes
            .Where(a => attributeIds.Contains(a.Id) && !existing.Contains(a.Id))
            .Select(a => a.Id)
            .ToListAsync(ct);
        if (toAdd.Count == 0) return;

        db.UserAttributeValues.AddRange(toAdd.Select(id => new UserAttributeValue { UserId = userId, AttributeId = id }));
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveFromInfoAsync(string userId, IReadOnlyCollection<int> attributeIds, CancellationToken ct = default)
    {
        // Built-in attributes can't be removed from the profile -- IsBuiltIn is checked, not just DataType.
        await db.UserAttributeValues
            .Where(v => v.UserId == userId && attributeIds.Contains(v.AttributeId) && !v.Attribute.IsBuiltIn)
            .ExecuteDeleteAsync(ct);
    }

    private async Task<IReadOnlyList<AttributeValueViewModel>> LoadValuesAsync(string userId, bool isBuiltIn, CancellationToken ct)
    {
        var rows = await db.UserAttributeValues
            .AsNoTracking()
            .Include(v => v.Attribute).ThenInclude(a => a.Category)
            .Include(v => v.Attribute).ThenInclude(a => a.Options.OrderBy(o => o.SortOrder))
            .Where(v => v.UserId == userId && v.Attribute.IsBuiltIn == isBuiltIn)
            .OrderBy(v => v.Attribute.Category.Name).ThenBy(v => v.Attribute.Name)
            .ToListAsync(ct);

        return rows.Select(v => AttributeValueMapper.ToViewModel(v.Attribute, v)).ToList();
    }
}
