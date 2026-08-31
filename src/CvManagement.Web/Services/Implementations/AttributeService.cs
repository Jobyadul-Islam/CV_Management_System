using CvManagement.Web.Data;
using CvManagement.Web.Domain;
using CvManagement.Web.Domain.Enums;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Attribute;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Services.Implementations;

public class AttributeService(ApplicationDbContext db) : IAttributeService
{
    public async Task<IReadOnlyList<AttributeListItemViewModel>> GetLibraryAsync(
        string? prefix, int? categoryId, CancellationToken ct = default)
    {
        var query = db.Attributes.Include(a => a.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(prefix))
        {
            query = query.Where(a => EF.Functions.Like(a.Name, $"{prefix}%"));
        }
        if (categoryId is not null)
        {
            query = query.Where(a => a.CategoryId == categoryId);
        }

        return await query
            .OrderBy(a => a.Category.Name).ThenBy(a => a.Name)
            .Select(a => new AttributeListItemViewModel
            {
                Id = a.Id,
                Name = a.Name,
                CategoryName = a.Category.Name,
                DataType = a.DataType,
                Description = a.Description,
                IsBuiltIn = a.IsBuiltIn,
                PositionUsageCount = a.PositionAttributes.Count,
                UpdatedAt = a.UpdatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AttributeCategory>> GetCategoriesAsync(CancellationToken ct = default)
        => await db.AttributeCategories.OrderBy(c => c.Name).ToListAsync(ct);

    public async Task<AttributeFormViewModel?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var attribute = await db.Attributes
            .Include(a => a.Options.OrderBy(o => o.SortOrder))
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (attribute is null) return null;

        return new AttributeFormViewModel
        {
            Id = attribute.Id,
            Name = attribute.Name,
            Description = attribute.Description,
            CategoryId = attribute.CategoryId,
            DataType = attribute.DataType,
            IsBuiltIn = attribute.IsBuiltIn,
            MinLength = attribute.MinLength,
            MaxLength = attribute.MaxLength,
            RegexPattern = attribute.RegexPattern,
            MinValue = attribute.MinValue,
            MaxValue = attribute.MaxValue,
            RowVersion = Convert.ToBase64String(attribute.RowVersion),
            Options = attribute.Options.Select(o => new AttributeOptionInputModel
            {
                Id = o.Id,
                Label = o.Label,
                SortOrder = o.SortOrder,
                RowVersion = Convert.ToBase64String(o.RowVersion)
            }).ToList(),
            CategoryOptions = await BuildCategoryOptionsAsync(attribute.CategoryId, ct)
        };
    }

    public async Task<AttributeSaveOutcome> CreateAsync(AttributeFormViewModel form, string userId, CancellationToken ct = default)
    {
        var validationError = await ValidateAsync(form, existingId: null, ct);
        if (validationError is not null) return AttributeSaveOutcome.ValidationError(validationError);

        var attribute = new AttributeDefinition
        {
            Name = form.Name.Trim(),
            Description = form.Description?.Trim() ?? string.Empty,
            CategoryId = form.CategoryId,
            DataType = form.DataType,
            IsBuiltIn = false,
            CreatedByUserId = userId,
            MinLength = form.MinLength,
            MaxLength = form.MaxLength,
            RegexPattern = form.RegexPattern,
            MinValue = form.MinValue,
            MaxValue = form.MaxValue
        };

        if (form.DataType == AttributeDataType.OneOfMany)
        {
            attribute.Options = BuildOptions(form.Options);
        }

        db.Attributes.Add(attribute);
        await db.SaveChangesAsync(ct);
        return AttributeSaveOutcome.Success(attribute.Id);
    }

    public async Task<AttributeSaveOutcome> UpdateAsync(AttributeFormViewModel form, CancellationToken ct = default)
    {
        var attribute = await db.Attributes
            .Include(a => a.Options)
            .FirstOrDefaultAsync(a => a.Id == form.Id, ct);
        if (attribute is null) return AttributeSaveOutcome.NotFound();

        var validationError = await ValidateAsync(form, existingId: attribute.Id, ct);
        if (validationError is not null) return AttributeSaveOutcome.ValidationError(validationError);

        db.Entry(attribute).Property(a => a.RowVersion).OriginalValue = Convert.FromBase64String(form.RowVersion ?? string.Empty);

        attribute.Name = form.Name.Trim();
        attribute.Description = form.Description?.Trim() ?? string.Empty;
        attribute.UpdatedAt = DateTime.UtcNow;
        attribute.MinLength = form.MinLength;
        attribute.MaxLength = form.MaxLength;
        attribute.RegexPattern = form.RegexPattern;
        attribute.MinValue = form.MinValue;
        attribute.MaxValue = form.MaxValue;

        // Built-in attributes keep their original category/type -- changing "Personal Photo" away
        // from Image, for example, would break Me-tab rendering for every user.
        if (!attribute.IsBuiltIn)
        {
            attribute.CategoryId = form.CategoryId;
            attribute.DataType = form.DataType;
        }

        if (attribute.DataType == AttributeDataType.OneOfMany)
        {
            SyncOptions(attribute, form.Options);
        }
        else if (attribute.Options.Count > 0)
        {
            db.AttributeOptions.RemoveRange(attribute.Options);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            var current = await db.Attributes.AsNoTracking().FirstOrDefaultAsync(a => a.Id == form.Id, ct);
            return current is null ? AttributeSaveOutcome.NotFound() : AttributeSaveOutcome.Conflict(current.RowVersion);
        }

        return AttributeSaveOutcome.Success(attribute.Id);
    }

    public async Task<AttributeDeleteOutcome> DeleteAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
    {
        var deleted = new List<int>();
        var blocked = new List<AttributeDeleteBlocked>();

        var attributes = await db.Attributes
            .Where(a => ids.Contains(a.Id))
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.IsBuiltIn,
                TemplateUsage = a.PositionAttributes.Count,
                RuleUsage = a.AccessRules.Count
            })
            .ToListAsync(ct);

        foreach (var attribute in attributes)
        {
            if (attribute.IsBuiltIn)
            {
                blocked.Add(new AttributeDeleteBlocked(attribute.Id, attribute.Name, "Built-in attribute cannot be deleted."));
                continue;
            }

            var totalUsage = attribute.TemplateUsage + attribute.RuleUsage;
            if (totalUsage > 0)
            {
                blocked.Add(new AttributeDeleteBlocked(attribute.Id, attribute.Name,
                    $"In use by {totalUsage} position template/rule(s). Remove it from those first."));
                continue;
            }

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.UserAttributeValues.Where(v => v.AttributeId == attribute.Id).ExecuteDeleteAsync(ct);
            await db.Attributes.Where(a => a.Id == attribute.Id).ExecuteDeleteAsync(ct);
            await tx.CommitAsync(ct);

            deleted.Add(attribute.Id);
        }

        return new AttributeDeleteOutcome(deleted, blocked);
    }

    public async Task<IReadOnlyList<AttributePickerItemViewModel>> SearchForPickerAsync(
        string? prefix, int? categoryId, IReadOnlyCollection<int>? excludeIds, CancellationToken ct = default)
    {
        var query = db.Attributes.Include(a => a.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(prefix))
        {
            query = query.Where(a => EF.Functions.Like(a.Name, $"{prefix}%"));
        }
        if (categoryId is not null)
        {
            query = query.Where(a => a.CategoryId == categoryId);
        }
        if (excludeIds is { Count: > 0 })
        {
            query = query.Where(a => !excludeIds.Contains(a.Id));
        }

        return await query
            .OrderBy(a => a.Name)
            .Take(200)
            .Select(a => new AttributePickerItemViewModel
            {
                Id = a.Id,
                Name = a.Name,
                CategoryName = a.Category.Name,
                DataType = a.DataType,
                Description = a.Description,
                Options = a.Options.OrderBy(o => o.SortOrder)
                    .Select(o => new AttributePickerOptionViewModel { Id = o.Id, Label = o.Label })
                    .ToList()
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AttributePickerItemViewModel>> GetRecentlyUsedAsync(
        string userId, IReadOnlyCollection<int>? excludeIds, int take = 8, CancellationToken ct = default)
    {
        var query = db.AttributeUsages
            .Include(u => u.Attribute).ThenInclude(a => a.Category)
            .Where(u => u.UserId == userId);

        if (excludeIds is { Count: > 0 })
        {
            query = query.Where(u => !excludeIds.Contains(u.AttributeId));
        }

        return await query
            .OrderByDescending(u => u.LastUsedAt)
            .Take(take)
            .Select(u => new AttributePickerItemViewModel
            {
                Id = u.Attribute.Id,
                Name = u.Attribute.Name,
                CategoryName = u.Attribute.Category.Name,
                DataType = u.Attribute.DataType,
                Description = u.Attribute.Description
            })
            .ToListAsync(ct);
    }

    public async Task RecordUsageAsync(string userId, int attributeId, CancellationToken ct = default)
    {
        var usage = await db.AttributeUsages
            .FirstOrDefaultAsync(u => u.UserId == userId && u.AttributeId == attributeId, ct);

        if (usage is null)
        {
            db.AttributeUsages.Add(new AttributeUsage { UserId = userId, AttributeId = attributeId });
        }
        else
        {
            usage.LastUsedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<string?> ValidateAsync(AttributeFormViewModel form, int? existingId, CancellationToken ct)
    {
        var nameTaken = await db.Attributes
            .AnyAsync(a => a.Name == form.Name.Trim() && a.Id != (existingId ?? -1), ct);
        if (nameTaken) return $"An attribute named \"{form.Name}\" already exists.";

        var categoryExists = await db.AttributeCategories.AnyAsync(c => c.Id == form.CategoryId, ct);
        if (!categoryExists) return "Select a valid category.";

        if (form.DataType == AttributeDataType.OneOfMany)
        {
            var labels = form.Options.Select(o => o.Label?.Trim() ?? string.Empty).Where(l => l.Length > 0).ToList();
            if (labels.Count == 0) return "A dropdown attribute needs at least one option.";
            if (labels.Distinct(StringComparer.OrdinalIgnoreCase).Count() != labels.Count)
                return "Dropdown options must have unique labels.";
        }

        return ValidateTuning(form);
    }

    // Tuning only applies to the data type it was entered for -- e.g. a length limit typed in while
    // the type dropdown was on "String" must not silently survive a switch to "Numeric". Clearing the
    // irrelevant fields here (rather than trusting the form/JS) keeps stored data consistent even if a
    // request is crafted by hand.
    private static string? ValidateTuning(AttributeFormViewModel form)
    {
        if (form.DataType is AttributeDataType.String or AttributeDataType.Text)
        {
            form.MinValue = null;
            form.MaxValue = null;

            if (form.MinLength is < 0) return "Minimum length cannot be negative.";
            if (form.MaxLength is < 0) return "Maximum length cannot be negative.";
            if (form.MinLength is not null && form.MaxLength is not null && form.MinLength > form.MaxLength)
                return "Minimum length cannot exceed maximum length.";

            if (!string.IsNullOrWhiteSpace(form.RegexPattern))
            {
                try
                {
                    _ = new System.Text.RegularExpressions.Regex(form.RegexPattern);
                }
                catch (ArgumentException)
                {
                    return "The regex pattern is not a valid regular expression.";
                }
            }
        }
        else if (form.DataType == AttributeDataType.Numeric)
        {
            form.MinLength = null;
            form.MaxLength = null;
            form.RegexPattern = null;

            if (form.MinValue is not null && form.MaxValue is not null && form.MinValue > form.MaxValue)
                return "Minimum value cannot exceed maximum value.";
        }
        else
        {
            form.MinLength = null;
            form.MaxLength = null;
            form.RegexPattern = null;
            form.MinValue = null;
            form.MaxValue = null;
        }

        return null;
    }

    private static List<AttributeOption> BuildOptions(List<AttributeOptionInputModel> input)
    {
        var sortOrder = 0;
        return input
            .Where(o => !string.IsNullOrWhiteSpace(o.Label))
            .Select(o => new AttributeOption { Label = o.Label!.Trim(), SortOrder = sortOrder++ })
            .ToList();
    }

    private void SyncOptions(AttributeDefinition attribute, List<AttributeOptionInputModel> input)
    {
        var incoming = input.Where(o => !string.IsNullOrWhiteSpace(o.Label)).ToList();
        var incomingIds = incoming.Where(o => o.Id > 0).Select(o => o.Id).ToHashSet();

        var toRemove = attribute.Options.Where(o => !incomingIds.Contains(o.Id)).ToList();
        foreach (var option in toRemove)
        {
            attribute.Options.Remove(option);
            db.AttributeOptions.Remove(option);
        }

        var sortOrder = 0;
        foreach (var item in incoming)
        {
            var existing = item.Id > 0 ? attribute.Options.FirstOrDefault(o => o.Id == item.Id) : null;
            if (existing is not null)
            {
                existing.Label = item.Label!.Trim();
                existing.SortOrder = sortOrder++;
            }
            else
            {
                attribute.Options.Add(new AttributeOption { Label = item.Label!.Trim(), SortOrder = sortOrder++ });
            }
        }
    }

    private async Task<List<SelectListItem>> BuildCategoryOptionsAsync(int? selectedId, CancellationToken ct)
    {
        var categories = await GetCategoriesAsync(ct);
        return categories
            .Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == selectedId))
            .ToList();
    }
}
