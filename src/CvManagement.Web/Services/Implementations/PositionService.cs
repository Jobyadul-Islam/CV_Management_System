using Microsoft.Extensions.Localization;
using CvManagement.Web.Data;
using CvManagement.Web.Models;
using CvManagement.Web.Models.Enums;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Position;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Services.Implementations;

public class PositionService(
    ApplicationDbContext db,
    IPositionAccessEvaluator accessEvaluator,
    ITagService tagService,
    ISearchIndexService searchIndex,
    IStringLocalizer<SharedResource> localizer) : IPositionService
{
    public async Task<IReadOnlyList<PositionListItemViewModel>> GetListForRecruiterAsync(CancellationToken ct = default)
        => await db.Positions
            .OrderByDescending(p => p.UpdatedAt)
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

    public async Task<IReadOnlyList<PositionListItemViewModel>> GetEligibleListForCandidateAsync(string userId, CancellationToken ct = default)
    {
        var all = await db.Positions
            .OrderByDescending(p => p.UpdatedAt)
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

        var eligibility = await accessEvaluator.IsEligibleForManyAsync(userId, all.Select(p => p.Id).ToList(), ct);
        return all.Where(p => eligibility.GetValueOrDefault(p.Id)).ToList();
    }

    public async Task<PositionDetailsViewModel?> GetDetailsAsync(int id, CancellationToken ct = default)
    {
        var position = await db.Positions
            .AsNoTracking()
            .Include(p => p.PositionAttributes.OrderBy(pa => pa.SortOrder)).ThenInclude(pa => pa.Attribute)
            .Include(p => p.AccessRules).ThenInclude(r => r.Attribute)
            .Include(p => p.AccessRules).ThenInclude(r => r.ComparisonOption)
            .Include(p => p.ProjectTags).ThenInclude(t => t.Tag)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (position is null) return null;

        return new PositionDetailsViewModel
        {
            Id = position.Id,
            Title = position.Title,
            ShortDescription = position.ShortDescription,
            Company = position.Company,
            Level = position.Level,
            AccessMode = position.AccessMode,
            MaxProjects = position.MaxProjects,
            UpdatedAt = position.UpdatedAt,
            TemplateAttributeNames = position.PositionAttributes.Select(pa => pa.Attribute.Name).ToList(),
            ProjectTagNames = position.ProjectTags.Select(t => t.Tag.Name).ToList(),
            AccessRuleDescriptions = position.AccessRules.Select(DescribeRule).ToList()
        };
    }

    public async Task<PositionFormViewModel?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var position = await db.Positions
            .AsNoTracking()
            .Include(p => p.PositionAttributes.OrderBy(pa => pa.SortOrder)).ThenInclude(pa => pa.Attribute)
            .Include(p => p.AccessRules)
            .Include(p => p.ProjectTags).ThenInclude(t => t.Tag)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (position is null) return null;

        var form = new PositionFormViewModel
        {
            Id = position.Id,
            Title = position.Title,
            ShortDescription = position.ShortDescription,
            Company = position.Company,
            Level = position.Level,
            AccessMode = position.AccessMode,
            MaxProjects = position.MaxProjects,
            RowVersion = Convert.ToBase64String(position.RowVersion),
            AttributeIds = position.PositionAttributes.Select(pa => pa.AttributeId).ToList(),
            SelectedAttributes = position.PositionAttributes.Select(pa => (pa.AttributeId, pa.Attribute.Name)).ToList(),
            AccessRules = position.AccessRules.Select(r => new PositionAccessRuleInputModel
            {
                AttributeId = r.AttributeId,
                Operator = r.Operator,
                ComparisonValueString = r.ComparisonValueString,
                ComparisonValueNumeric = r.ComparisonValueNumeric,
                ComparisonValueDate = r.ComparisonValueDate,
                ComparisonOptionId = r.ComparisonOptionId
            }).ToList(),
            ProjectTags = position.ProjectTags.Select(t => t.Tag.Name).ToList()
        };
        PopulateLevelOptions(form);
        return form;
    }

    public Task<PositionFormViewModel> GetBlankFormAsync(CancellationToken ct = default)
    {
        var form = new PositionFormViewModel();
        PopulateLevelOptions(form);
        return Task.FromResult(form);
    }

    public async Task<PositionSaveOutcome> CreateAsync(PositionFormViewModel form, string userId, CancellationToken ct = default)
    {
        var validationError = await ValidateAsync(form, ct);
        if (validationError is not null) return PositionSaveOutcome.ValidationError(validationError);

        var position = new Position
        {
            Title = form.Title.Trim(),
            ShortDescription = form.ShortDescription?.Trim() ?? string.Empty,
            Company = form.Company?.Trim(),
            Level = form.Level,
            AccessMode = form.AccessMode,
            MaxProjects = form.MaxProjects,
            CreatedByUserId = userId,
            UpdatedByUserId = userId
        };

        ApplyAttributes(position, form.AttributeIds);
        ApplyAccessRules(position, form.AccessMode, form.AccessRules);
        await ApplyProjectTagsAsync(position, form.ProjectTags, ct);

        db.Positions.Add(position);
        await db.SaveChangesAsync(ct);
        await searchIndex.IndexPositionAsync(position.Id, ct);
        return PositionSaveOutcome.Success(position.Id);
    }

    public async Task<PositionSaveOutcome> UpdateAsync(PositionFormViewModel form, string userId, CancellationToken ct = default)
    {
        var position = await db.Positions
            .Include(p => p.PositionAttributes)
            .Include(p => p.AccessRules)
            .Include(p => p.ProjectTags)
            .FirstOrDefaultAsync(p => p.Id == form.Id, ct);
        if (position is null) return PositionSaveOutcome.NotFound();

        var validationError = await ValidateAsync(form, ct);
        if (validationError is not null) return PositionSaveOutcome.ValidationError(validationError);

        db.Entry(position).Property(p => p.RowVersion).OriginalValue = Convert.FromBase64String(form.RowVersion ?? string.Empty);

        position.Title = form.Title.Trim();
        position.ShortDescription = form.ShortDescription?.Trim() ?? string.Empty;
        position.Company = form.Company?.Trim();
        position.Level = form.Level;
        position.AccessMode = form.AccessMode;
        position.MaxProjects = form.MaxProjects;
        position.UpdatedByUserId = userId;
        // Always touch UpdatedAt (which regenerates the SQL Server rowversion) even when only the
        // child collections below change -- one form, one Save button, one meaningful concurrency check.
        position.UpdatedAt = DateTime.UtcNow;

        db.PositionAttributes.RemoveRange(position.PositionAttributes);
        position.PositionAttributes.Clear();
        ApplyAttributes(position, form.AttributeIds);

        db.PositionAccessRules.RemoveRange(position.AccessRules);
        position.AccessRules.Clear();
        ApplyAccessRules(position, form.AccessMode, form.AccessRules);

        db.PositionProjectTags.RemoveRange(position.ProjectTags);
        position.ProjectTags.Clear();
        await ApplyProjectTagsAsync(position, form.ProjectTags, ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            var current = await db.Positions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == form.Id, ct);
            return current is null ? PositionSaveOutcome.NotFound() : PositionSaveOutcome.Conflict(Convert.ToBase64String(current.RowVersion));
        }

        await searchIndex.IndexPositionAsync(position.Id, ct);
        return PositionSaveOutcome.Success(position.Id);
    }

    /// <summary>
    /// One DELETE for the whole selection. CVs (and their likes), template attributes, access rules,
    /// project-tag filters and discussion posts all go with it through ON DELETE CASCADE -- the database
    /// keeps referential integrity, so there's no application-side "delete children in a loop".
    /// </summary>
    public async Task<PositionDeleteOutcome> DeleteAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
    {
        var existing = await db.Positions.Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
        if (existing.Count > 0)
        {
            await db.Positions.Where(p => existing.Contains(p.Id)).ExecuteDeleteAsync(ct);
            foreach (var id in existing)
            {
                await searchIndex.RemovePositionAsync(id, ct); // in-process Lucene index, not a DB query
            }
        }
        return new PositionDeleteOutcome(existing, []);
    }

    public async Task<PositionSaveOutcome> DuplicateAsync(int id, string userId, CancellationToken ct = default)
    {
        var source = await db.Positions
            .AsNoTracking()
            .Include(p => p.PositionAttributes)
            .Include(p => p.AccessRules)
            .Include(p => p.ProjectTags)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (source is null) return PositionSaveOutcome.NotFound();

        var copy = new Position
        {
            Title = $"{source.Title} (Copy)",
            ShortDescription = source.ShortDescription,
            Company = source.Company,
            Level = source.Level,
            AccessMode = source.AccessMode,
            MaxProjects = source.MaxProjects,
            CreatedByUserId = userId,
            UpdatedByUserId = userId,
            PositionAttributes = source.PositionAttributes
                .Select(pa => new PositionAttribute { AttributeId = pa.AttributeId, SortOrder = pa.SortOrder })
                .ToList(),
            AccessRules = source.AccessRules
                .Select(r => new PositionAccessRule
                {
                    AttributeId = r.AttributeId,
                    Operator = r.Operator,
                    ComparisonValueString = r.ComparisonValueString,
                    ComparisonValueNumeric = r.ComparisonValueNumeric,
                    ComparisonValueDate = r.ComparisonValueDate,
                    ComparisonOptionId = r.ComparisonOptionId
                })
                .ToList(),
            ProjectTags = source.ProjectTags
                .Select(t => new PositionProjectTag { TagId = t.TagId })
                .ToList()
        };

        db.Positions.Add(copy);
        await db.SaveChangesAsync(ct);
        await searchIndex.IndexPositionAsync(copy.Id, ct);
        return PositionSaveOutcome.Success(copy.Id);
    }

    private static void ApplyAttributes(Position position, List<int> attributeIds)
    {
        var sortOrder = 0;
        foreach (var attributeId in attributeIds.Distinct())
        {
            position.PositionAttributes.Add(new PositionAttribute { AttributeId = attributeId, SortOrder = sortOrder++ });
        }
    }

    private static void ApplyAccessRules(Position position, PositionAccessMode mode, List<PositionAccessRuleInputModel> rules)
    {
        if (mode != PositionAccessMode.Restricted) return;

        foreach (var rule in rules)
        {
            position.AccessRules.Add(new PositionAccessRule
            {
                AttributeId = rule.AttributeId,
                Operator = rule.Operator,
                ComparisonValueString = rule.ComparisonValueString,
                ComparisonValueNumeric = rule.ComparisonValueNumeric,
                ComparisonValueDate = rule.ComparisonValueDate,
                ComparisonOptionId = rule.ComparisonOptionId
            });
        }
    }

    private async Task ApplyProjectTagsAsync(Position position, List<string> tagNames, CancellationToken ct)
    {
        var tags = await tagService.ResolveOrCreateTagsAsync(tagNames, ct);
        foreach (var tag in tags)
        {
            position.ProjectTags.Add(new PositionProjectTag { TagId = tag.Id });
        }
    }

    private async Task<string?> ValidateAsync(PositionFormViewModel form, CancellationToken ct)
    {
        if (form.MaxProjects < 0) return localizer["Pos_MaxProjectsNegative"];
        if (form.AccessMode != PositionAccessMode.Restricted) return null;

        if (form.AccessRules.Count == 0)
        {
            return localizer["Pos_RestrictedNeedsRule"];
        }

        // The rule builder only offers valid combinations, but a crafted request must not be able to
        // store e.g. "Photo > 5" or a dropdown option belonging to a different attribute.
        var attributeIds = form.AccessRules.Select(r => r.AttributeId).Distinct().ToList();
        var attributes = await db.Attributes.AsNoTracking()
            .Where(a => attributeIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Name, a.DataType, OptionIds = a.Options.Select(o => o.Id).ToList() })
            .ToDictionaryAsync(a => a.Id, ct);

        foreach (var rule in form.AccessRules)
        {
            if (!attributes.TryGetValue(rule.AttributeId, out var attribute))
                return localizer["Pos_RuleAttributeMissing"];
            if (!OperatorCatalog.AllowedOperators[attribute.DataType].Contains(rule.Operator))
                return localizer["Pos_OperatorNotAllowed", localizer[OperatorCatalog.LabelKey(rule.Operator)], attribute.Name];

            if (rule.Operator is ComparisonOperator.IsSet or ComparisonOperator.IsNotSet
                or ComparisonOperator.IsTrue or ComparisonOperator.IsFalse)
                continue;

            var missingValue = attribute.DataType switch
            {
                AttributeDataType.Numeric => rule.ComparisonValueNumeric is null,
                AttributeDataType.Date => rule.ComparisonValueDate is null,
                AttributeDataType.OneOfMany => rule.ComparisonOptionId is not { } optionId || !attribute.OptionIds.Contains(optionId),
                _ => string.IsNullOrWhiteSpace(rule.ComparisonValueString)
            };
            if (missingValue) return localizer["Pos_RuleNeedsValue", attribute.Name];
        }
        return null;
    }

    private string DescribeRule(PositionAccessRule rule)
    {
        // "<attribute> <operator label> [value]" -- attribute names and values are user data and stay as
        // entered; only the operator wording is localized (Op_* keys).
        var label = localizer[OperatorCatalog.LabelKey(rule.Operator)].Value;
        string? value = rule.Operator switch
        {
            ComparisonOperator.IsSet or ComparisonOperator.IsNotSet
                or ComparisonOperator.IsTrue or ComparisonOperator.IsFalse => null,
            ComparisonOperator.Contains or ComparisonOperator.StartsWith => $"\"{rule.ComparisonValueString}\"",
            _ when rule.ComparisonOption is not null => rule.ComparisonOption.Label,
            _ => rule.ComparisonValueString
                 ?? rule.ComparisonValueNumeric?.ToString("0.####")
                 ?? rule.ComparisonValueDate?.ToString("yyyy-MM-dd")
        };
        return value is null ? $"{rule.Attribute.Name} {label}" : $"{rule.Attribute.Name} {label} {value}";
    }

    private void PopulateLevelOptions(PositionFormViewModel form)
    {
        form.LevelOptions =
        [
            new SelectListItem(localizer["Level_None"], ""),
            .. Enum.GetValues<PositionLevel>().Select(l => new SelectListItem(localizer[$"Level_{l}"], ((int)l).ToString(), l == form.Level))
        ];
    }
}
