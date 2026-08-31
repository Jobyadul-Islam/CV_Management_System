using CvManagement.Web.Data;
using CvManagement.Web.Domain;
using CvManagement.Web.Domain.Enums;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Position;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Services.Implementations;

public class PositionService(ApplicationDbContext db, IPositionAccessEvaluator accessEvaluator, ITagService tagService) : IPositionService
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
        var validationError = Validate(form);
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

        var validationError = Validate(form);
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

        return PositionSaveOutcome.Success(position.Id);
    }

    public async Task<PositionDeleteOutcome> DeleteAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
    {
        var deleted = new List<int>();
        var blocked = new List<PositionDeleteBlocked>();

        var positions = await db.Positions
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Title, CvCount = p.Cvs.Count })
            .ToListAsync(ct);

        foreach (var position in positions)
        {
            if (position.CvCount > 0)
            {
                blocked.Add(new PositionDeleteBlocked(position.Id, position.Title,
                    $"{position.CvCount} CV(s) exist for this position."));
                continue;
            }

            await db.Positions.Where(p => p.Id == position.Id).ExecuteDeleteAsync(ct);
            deleted.Add(position.Id);
        }

        return new PositionDeleteOutcome(deleted, blocked);
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

    private static string? Validate(PositionFormViewModel form)
    {
        if (form.AccessMode == PositionAccessMode.Restricted && form.AccessRules.Count == 0)
        {
            return "A restricted position needs at least one access rule -- otherwise no candidate could ever qualify.";
        }
        return null;
    }

    private static string DescribeRule(PositionAccessRule rule)
    {
        var attributeName = rule.Attribute.Name;
        return rule.Operator switch
        {
            ComparisonOperator.IsSet => $"{attributeName} is filled in",
            ComparisonOperator.IsNotSet => $"{attributeName} is not filled in",
            ComparisonOperator.IsTrue => $"{attributeName} is checked",
            ComparisonOperator.IsFalse => $"{attributeName} is not checked",
            ComparisonOperator.Equals when rule.ComparisonOption is not null => $"{attributeName} = {rule.ComparisonOption.Label}",
            ComparisonOperator.NotEquals when rule.ComparisonOption is not null => $"{attributeName} ≠ {rule.ComparisonOption.Label}",
            ComparisonOperator.Equals => $"{attributeName} = {rule.ComparisonValueString ?? rule.ComparisonValueNumeric?.ToString("0.####") ?? rule.ComparisonValueDate?.ToString()}",
            ComparisonOperator.NotEquals => $"{attributeName} ≠ {rule.ComparisonValueString ?? rule.ComparisonValueNumeric?.ToString("0.####") ?? rule.ComparisonValueDate?.ToString()}",
            ComparisonOperator.GreaterThan => $"{attributeName} > {rule.ComparisonValueNumeric?.ToString("0.####") ?? rule.ComparisonValueDate?.ToString()}",
            ComparisonOperator.GreaterThanOrEqual => $"{attributeName} ≥ {rule.ComparisonValueNumeric?.ToString("0.####") ?? rule.ComparisonValueDate?.ToString()}",
            ComparisonOperator.LessThan => $"{attributeName} < {rule.ComparisonValueNumeric?.ToString("0.####") ?? rule.ComparisonValueDate?.ToString()}",
            ComparisonOperator.LessThanOrEqual => $"{attributeName} ≤ {rule.ComparisonValueNumeric?.ToString("0.####") ?? rule.ComparisonValueDate?.ToString()}",
            ComparisonOperator.Contains => $"{attributeName} contains \"{rule.ComparisonValueString}\"",
            ComparisonOperator.StartsWith => $"{attributeName} starts with \"{rule.ComparisonValueString}\"",
            _ => attributeName
        };
    }

    private static void PopulateLevelOptions(PositionFormViewModel form)
    {
        form.LevelOptions =
        [
            new SelectListItem("(none)", ""),
            .. Enum.GetValues<PositionLevel>().Select(l => new SelectListItem(l.ToString(), ((int)l).ToString(), l == form.Level))
        ];
    }
}
