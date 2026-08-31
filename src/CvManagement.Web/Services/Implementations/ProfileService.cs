using CvManagement.Web.Data;
using CvManagement.Web.Domain;
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

    public async Task AddToInfoAsync(string userId, int attributeId, CancellationToken ct = default)
    {
        var exists = await db.UserAttributeValues.AnyAsync(v => v.UserId == userId && v.AttributeId == attributeId, ct);
        if (exists) return;

        db.UserAttributeValues.Add(new UserAttributeValue { UserId = userId, AttributeId = attributeId });
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveFromInfoAsync(string userId, int attributeId, CancellationToken ct = default)
    {
        // Built-in attributes can't be removed from the profile -- IsBuiltIn is checked, not just DataType.
        await db.UserAttributeValues
            .Where(v => v.UserId == userId && v.AttributeId == attributeId && !v.Attribute.IsBuiltIn)
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

        return rows.Select(ToViewModel).ToList();
    }

    private static AttributeValueViewModel ToViewModel(UserAttributeValue value) => new()
    {
        AttributeId = value.AttributeId,
        AttributeName = value.Attribute.Name,
        Description = value.Attribute.Description,
        DataType = value.Attribute.DataType,
        Options = value.Attribute.Options.Select(o => new AttributePickerOptionViewModel { Id = o.Id, Label = o.Label }).ToList(),
        RowVersion = Convert.ToBase64String(value.RowVersion),
        IsEmpty = AttributeEmptiness.IsEmpty(value, value.Attribute.DataType),
        ValueString = value.ValueString,
        ValueText = value.ValueText,
        ValueImageUrl = value.ValueImageUrl,
        ValueNumeric = value.ValueNumeric,
        ValueDate = value.ValueDate,
        ValuePeriodStart = value.ValuePeriodStart,
        ValuePeriodEnd = value.ValuePeriodEnd,
        ValueBoolean = value.ValueBoolean,
        ValueOptionId = value.ValueOptionId
    };
}
