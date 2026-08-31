using CvManagement.Web.Data;
using CvManagement.Web.Domain;
using CvManagement.Web.Domain.Enums;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Profile;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Services.Implementations;

public class ProfileAutoSaveService(ApplicationDbContext db, ISearchIndexService searchIndex) : IProfileAutoSaveService
{
    public async Task<List<AutoSaveResultDto>> SaveAsync(string userId, List<AutoSaveChangeDto> changes, CancellationToken ct = default)
    {
        var results = new List<AutoSaveResultDto>();
        var searchableTextChanged = false;

        foreach (var change in changes)
        {
            var (result, isSearchableText) = await SaveOneAsync(userId, change, ct);
            results.Add(result);
            if (result.Status == "ok" && isSearchableText) searchableTextChanged = true;
        }

        // One re-index for the whole batch, not per field -- and only when a String/Text value
        // (the only ones that actually feed the search index) was among the successful saves.
        if (searchableTextChanged)
        {
            await searchIndex.IndexCandidateAsync(userId, ct);
        }

        return results;
    }

    private async Task<(AutoSaveResultDto Result, bool IsSearchableText)> SaveOneAsync(string userId, AutoSaveChangeDto change, CancellationToken ct)
    {
        var attribute = await db.Attributes.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == change.AttributeId, ct);
        if (attribute is null)
        {
            return (new AutoSaveResultDto { AttributeId = change.AttributeId, Status = "error", ErrorMessage = "Unknown attribute." }, false);
        }
        var isSearchableText = attribute.DataType is AttributeDataType.String or AttributeDataType.Text;

        var tuningError = ValidateTuning(attribute, change);
        if (tuningError is not null)
        {
            return (new AutoSaveResultDto { AttributeId = change.AttributeId, Status = "error", ErrorMessage = tuningError }, false);
        }

        var existing = await db.UserAttributeValues
            .FirstOrDefaultAsync(v => v.UserId == userId && v.AttributeId == change.AttributeId, ct);

        if (existing is null)
        {
            var created = new UserAttributeValue { UserId = userId, AttributeId = change.AttributeId };
            ApplyValue(created, attribute.DataType, change);
            db.UserAttributeValues.Add(created);
            await db.SaveChangesAsync(ct);
            await SyncDisplayNameIfNeededAsync(userId, attribute.Name, ct);
            return (new AutoSaveResultDto { AttributeId = change.AttributeId, Status = "ok", NewRowVersion = Convert.ToBase64String(created.RowVersion) }, isSearchableText);
        }

        byte[] originalRowVersion;
        try
        {
            originalRowVersion = Convert.FromBase64String(change.RowVersion ?? string.Empty);
        }
        catch (FormatException)
        {
            originalRowVersion = [];
        }

        db.Entry(existing).Property(v => v.RowVersion).OriginalValue = originalRowVersion;
        ApplyValue(existing, attribute.DataType, change);
        existing.UpdatedAt = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.Entry(existing).State = EntityState.Detached;
            var current = await db.UserAttributeValues.AsNoTracking()
                .FirstOrDefaultAsync(v => v.UserId == userId && v.AttributeId == change.AttributeId, ct);
            if (current is null)
            {
                return (new AutoSaveResultDto { AttributeId = change.AttributeId, Status = "error", ErrorMessage = "Value was deleted." }, false);
            }

            return (new AutoSaveResultDto
            {
                AttributeId = change.AttributeId,
                Status = "conflict",
                CurrentRowVersion = Convert.ToBase64String(current.RowVersion),
                UpdatedAtUtc = current.UpdatedAt,
                CurrentValue = new AutoSaveChangeDto
                {
                    AttributeId = change.AttributeId,
                    ValueString = current.ValueString,
                    ValueText = current.ValueText,
                    ValueImageUrl = current.ValueImageUrl,
                    ValueNumeric = current.ValueNumeric,
                    ValueDate = current.ValueDate,
                    ValuePeriodStart = current.ValuePeriodStart,
                    ValuePeriodEnd = current.ValuePeriodEnd,
                    ValueBoolean = current.ValueBoolean,
                    ValueOptionId = current.ValueOptionId
                }
            }, false);
        }

        await SyncDisplayNameIfNeededAsync(userId, attribute.Name, ct);
        return (new AutoSaveResultDto { AttributeId = change.AttributeId, Status = "ok", NewRowVersion = Convert.ToBase64String(existing.RowVersion) }, isSearchableText);
    }

    // Client-side maxlength/pattern/min/max on the input are a convenience, not a guarantee -- a
    // hand-crafted autosave request must be re-checked here against the same per-attribute tuning.
    // Clearing a field (empty string / null number) is always allowed; only non-empty values are
    // checked against Min/Max, since there's no separate "required" concept for these attributes.
    private static string? ValidateTuning(AttributeDefinition attribute, AutoSaveChangeDto change)
    {
        switch (attribute.DataType)
        {
            case AttributeDataType.String:
            case AttributeDataType.Text:
                var text = attribute.DataType == AttributeDataType.String ? change.ValueString : change.ValueText;
                if (string.IsNullOrEmpty(text)) return null;

                if (attribute.MinLength is int minLen && text.Length < minLen)
                    return $"Must be at least {minLen} characters.";
                if (attribute.MaxLength is int maxLen && text.Length > maxLen)
                    return $"Must be at most {maxLen} characters.";
                if (!string.IsNullOrEmpty(attribute.RegexPattern) &&
                    !System.Text.RegularExpressions.Regex.IsMatch(text, attribute.RegexPattern))
                    return "Value does not match the required format.";
                return null;

            case AttributeDataType.Numeric:
                if (change.ValueNumeric is not { } number) return null;
                if (attribute.MinValue is { } min && number < min) return $"Must be at least {min}.";
                if (attribute.MaxValue is { } max && number > max) return $"Must be at most {max}.";
                return null;

            default:
                return null;
        }
    }

    private static void ApplyValue(UserAttributeValue entity, AttributeDataType dataType, AutoSaveChangeDto change)
    {
        switch (dataType)
        {
            case AttributeDataType.String: entity.ValueString = change.ValueString; break;
            case AttributeDataType.Text: entity.ValueText = change.ValueText; break;
            case AttributeDataType.Image: entity.ValueImageUrl = change.ValueImageUrl; break;
            case AttributeDataType.Numeric: entity.ValueNumeric = change.ValueNumeric; break;
            case AttributeDataType.Date: entity.ValueDate = change.ValueDate; break;
            case AttributeDataType.Period:
                entity.ValuePeriodStart = change.ValuePeriodStart;
                entity.ValuePeriodEnd = change.ValuePeriodEnd;
                break;
            case AttributeDataType.Boolean: entity.ValueBoolean = change.ValueBoolean; break;
            case AttributeDataType.OneOfMany: entity.ValueOptionId = change.ValueOptionId; break;
        }
    }

    /// <summary>Keeps ApplicationUser.DisplayName (the read cache used by lists/nav) in sync with First/Last Name.</summary>
    private async Task SyncDisplayNameIfNeededAsync(string userId, string attributeName, CancellationToken ct)
    {
        if (attributeName is not ("First Name" or "Last Name")) return;

        var names = await db.UserAttributeValues
            .Where(v => v.UserId == userId && (v.Attribute.Name == "First Name" || v.Attribute.Name == "Last Name"))
            .Select(v => new { v.Attribute.Name, v.ValueString })
            .ToListAsync(ct);

        var first = names.FirstOrDefault(n => n.Name == "First Name")?.ValueString ?? string.Empty;
        var last = names.FirstOrDefault(n => n.Name == "Last Name")?.ValueString ?? string.Empty;
        var displayName = $"{first} {last}".Trim();

        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.DisplayName, displayName), ct);
    }
}
