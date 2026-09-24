using Microsoft.Extensions.Localization;
using System.Text.RegularExpressions;
using CvManagement.Web.Data;
using CvManagement.Web.Models;
using CvManagement.Web.Models.Enums;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Profile;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Services.Implementations;

public class ProfileAutoSaveService(
    ApplicationDbContext db,
    ISearchIndexService searchIndex,
    IUploadValidator uploadValidator,
    IStringLocalizer<SharedResource> localizer) : IProfileAutoSaveService
{
    // Recruiter-authored patterns run against candidate input; a pathological pattern must not be able
    // to pin a request thread (catastrophic backtracking), so every match is time-boxed.
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    public async Task<List<AutoSaveResultDto>> SaveAsync(string userId, List<AutoSaveChangeDto> changes, CancellationToken ct = default)
    {
        // Last write wins within a single batch for the same attribute.
        changes = changes.GroupBy(c => c.AttributeId).Select(g => g.Last()).ToList();
        if (changes.Count == 0) return [];

        var attributeIds = changes.Select(c => c.AttributeId).ToList();

        // Fixed query count regardless of batch size: one for definitions (+ option ids), one for existing rows.
        var attributes = await db.Attributes.AsNoTracking()
            .Where(a => attributeIds.Contains(a.Id))
            .Select(a => new
            {
                Definition = a,
                OptionIds = a.Options.Select(o => o.Id).ToList()
            })
            .ToDictionaryAsync(a => a.Definition.Id, ct);

        var existingRows = await db.UserAttributeValues
            .Where(v => v.UserId == userId && attributeIds.Contains(v.AttributeId))
            .ToDictionaryAsync(v => v.AttributeId, ct);

        var results = new Dictionary<int, AutoSaveResultDto>();
        var pending = new Dictionary<int, (UserAttributeValue Row, AttributeDefinition Attribute)>();

        foreach (var change in changes)
        {
            if (!attributes.TryGetValue(change.AttributeId, out var attribute))
            {
                results[change.AttributeId] = Error(change.AttributeId, localizer["Autosave_UnknownAttribute"]);
                continue;
            }

            var validationError = Validate(attribute.Definition, attribute.OptionIds, change, uploadValidator, localizer);
            if (validationError is not null)
            {
                results[change.AttributeId] = Error(change.AttributeId, validationError);
                continue;
            }

            if (existingRows.TryGetValue(change.AttributeId, out var row))
            {
                db.Entry(row).Property(v => v.RowVersion).OriginalValue = ParseRowVersion(change.RowVersion);
                row.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                row = new UserAttributeValue { UserId = userId, AttributeId = change.AttributeId };
                db.UserAttributeValues.Add(row);
            }

            ApplyValue(row, attribute.Definition.DataType, change);
            pending[change.AttributeId] = (row, attribute.Definition);
        }

        var conflicted = await CommitDroppingConflictsAsync(pending, ct);

        foreach (var (attributeId, (row, attribute)) in pending)
        {
            results[attributeId] = new AutoSaveResultDto
            {
                AttributeId = attributeId,
                Status = "ok",
                NewRowVersion = Convert.ToBase64String(row.RowVersion),
                IsEmpty = AttributeEmptiness.IsEmpty(row, attribute.DataType)
            };
        }

        foreach (var result in await BuildConflictResultsAsync(userId, conflicted, ct))
        {
            results[result.AttributeId] = result;
        }

        var saved = pending.Values.Select(p => p.Attribute).ToList();
        if (saved.Any(a => a.SystemKey is BuiltInAttributeKeys.FirstName or BuiltInAttributeKeys.LastName))
        {
            await SyncDisplayNameAsync(userId, ct);
        }
        // One re-index for the whole batch, and only when a String/Text value (the only kinds that
        // feed the search index) was actually saved.
        if (saved.Any(a => a.DataType is AttributeDataType.String or AttributeDataType.Text))
        {
            await searchIndex.IndexCandidateAsync(userId, ct);
        }

        return changes.Select(c => results[c.AttributeId]).ToList();
    }

    /// <summary>
    /// Commits every pending row in one SaveChanges. If some rows are stale, EF reports exactly which
    /// entries failed; those are detached and the rest retried. Each retry removes at least one entry,
    /// so this terminates, and the common no-conflict case is a single round trip.
    /// </summary>
    private async Task<List<int>> CommitDroppingConflictsAsync(
        Dictionary<int, (UserAttributeValue Row, AttributeDefinition Attribute)> pending, CancellationToken ct)
    {
        var conflicted = new List<int>();
        while (pending.Count > 0)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                return conflicted;
            }
            catch (DbUpdateException ex)
            {
                var failed = ex.Entries
                    .Select(e => e.Entity).OfType<UserAttributeValue>()
                    .Select(v => v.AttributeId)
                    .ToHashSet();

                // A plain DbUpdateException without entries (e.g. a concurrent insert tripping the
                // (UserId, AttributeId) unique index) can't be attributed -- treat new rows as conflicted.
                if (failed.Count == 0)
                {
                    failed = pending.Where(p => db.Entry(p.Value.Row).State == EntityState.Added)
                        .Select(p => p.Key).ToHashSet();
                    if (failed.Count == 0) throw;
                }

                foreach (var attributeId in failed)
                {
                    db.Entry(pending[attributeId].Row).State = EntityState.Detached;
                    pending.Remove(attributeId);
                    conflicted.Add(attributeId);
                }
            }
        }
        return conflicted;
    }

    private async Task<List<AutoSaveResultDto>> BuildConflictResultsAsync(string userId, List<int> attributeIds, CancellationToken ct)
    {
        if (attributeIds.Count == 0) return [];

        var current = await db.UserAttributeValues.AsNoTracking()
            .Where(v => v.UserId == userId && attributeIds.Contains(v.AttributeId))
            .ToDictionaryAsync(v => v.AttributeId, ct);

        return attributeIds.Select(id => current.TryGetValue(id, out var v)
            ? new AutoSaveResultDto
            {
                AttributeId = id,
                Status = "conflict",
                CurrentRowVersion = Convert.ToBase64String(v.RowVersion),
                UpdatedAtUtc = v.UpdatedAt,
                CurrentValue = new AutoSaveChangeDto
                {
                    AttributeId = id,
                    ValueString = v.ValueString,
                    ValueText = v.ValueText,
                    ValueImageUrl = v.ValueImageUrl,
                    ValueNumeric = v.ValueNumeric,
                    ValueDate = v.ValueDate,
                    ValuePeriodStart = v.ValuePeriodStart,
                    ValuePeriodEnd = v.ValuePeriodEnd,
                    ValueBoolean = v.ValueBoolean,
                    ValueOptionId = v.ValueOptionId
                }
            }
            : Error(id, localizer["Autosave_ValueRemoved"])).ToList();
    }

    /// <summary>
    /// Server-side re-check of everything the client already constrains (maxlength/pattern/min/max,
    /// dropdown choices, upload host) -- a hand-crafted request must not bypass it. Clearing a field is
    /// always allowed; only non-empty values are checked, since there's no separate "required" concept.
    /// </summary>
    public static string? Validate(AttributeDefinition attribute, IReadOnlyCollection<int> optionIds,
        AutoSaveChangeDto change, IUploadValidator uploadValidator, IStringLocalizer localizer)
    {
        switch (attribute.DataType)
        {
            case AttributeDataType.String:
            case AttributeDataType.Text:
                var text = attribute.DataType == AttributeDataType.String ? change.ValueString : change.ValueText;
                if (string.IsNullOrEmpty(text)) return null;

                if (attribute.DataType == AttributeDataType.String && text.Length > 400)
                    return localizer["Validation_MaxChars", 400];
                if (attribute.MinLength is int minLen && text.Length < minLen)
                    return localizer["Validation_MinChars", minLen];
                if (attribute.MaxLength is int maxLen && text.Length > maxLen)
                    return localizer["Validation_MaxChars", maxLen];
                if (!string.IsNullOrEmpty(attribute.RegexPattern))
                {
                    try
                    {
                        if (!Regex.IsMatch(text, attribute.RegexPattern, RegexOptions.None, RegexTimeout))
                            return localizer["Validation_Format"];
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        return localizer["Validation_FormatTimeout"];
                    }
                }
                return null;

            case AttributeDataType.Numeric:
                if (change.ValueNumeric is not { } number) return null;
                if (attribute.MinValue is { } min && number < min) return localizer["Validation_MinValue", min.ToString("0.####")];
                if (attribute.MaxValue is { } max && number > max) return localizer["Validation_MaxValue", max.ToString("0.####")];
                return null;

            case AttributeDataType.Period:
                if (change.ValuePeriodStart is { } start && change.ValuePeriodEnd is { } end && end < start)
                    return localizer["Validation_EndBeforeStart"];
                return null;

            case AttributeDataType.OneOfMany:
                if (change.ValueOptionId is { } optionId && !optionIds.Contains(optionId))
                    return localizer["Validation_SelectOption"];
                return null;

            case AttributeDataType.Image:
                return uploadValidator.ValidateImageUrl(change.ValueImageUrl);

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
            case AttributeDataType.Image: entity.ValueImageUrl = string.IsNullOrWhiteSpace(change.ValueImageUrl) ? null : change.ValueImageUrl; break;
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

    private static byte[] ParseRowVersion(string? rowVersion)
    {
        try
        {
            return Convert.FromBase64String(rowVersion ?? string.Empty);
        }
        catch (FormatException)
        {
            return []; // never matches a real rowversion -> reported as a conflict, never a silent overwrite
        }
    }

    private static AutoSaveResultDto Error(int attributeId, string message)
        => new() { AttributeId = attributeId, Status = "error", ErrorMessage = message };

    /// <summary>Keeps ApplicationUser.DisplayName (the read cache used by lists/nav) in sync with First/Last Name.</summary>
    private async Task SyncDisplayNameAsync(string userId, CancellationToken ct)
    {
        var names = await db.UserAttributeValues
            .Where(v => v.UserId == userId
                && (v.Attribute.SystemKey == BuiltInAttributeKeys.FirstName || v.Attribute.SystemKey == BuiltInAttributeKeys.LastName))
            .Select(v => new { v.Attribute.SystemKey, v.ValueString })
            .ToListAsync(ct);

        var first = names.FirstOrDefault(n => n.SystemKey == BuiltInAttributeKeys.FirstName)?.ValueString ?? string.Empty;
        var last = names.FirstOrDefault(n => n.SystemKey == BuiltInAttributeKeys.LastName)?.ValueString ?? string.Empty;
        var displayName = $"{first} {last}".Trim();

        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.DisplayName, displayName), ct);
    }
}
