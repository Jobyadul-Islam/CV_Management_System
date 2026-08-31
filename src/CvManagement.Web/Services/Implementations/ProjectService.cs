using CvManagement.Web.Data;
using CvManagement.Web.Domain;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Profile;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Services.Implementations;

public class ProjectService(ApplicationDbContext db, ITagService tagService) : IProjectService
{
    public async Task<IReadOnlyList<ProjectListItemViewModel>> GetListAsync(string userId, CancellationToken ct = default)
        => await db.Projects
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.PeriodEnd == null).ThenByDescending(p => p.PeriodStart)
            .Select(p => new ProjectListItemViewModel
            {
                Id = p.Id,
                Name = p.Name,
                PeriodStart = p.PeriodStart,
                PeriodEnd = p.PeriodEnd,
                Tags = p.ProjectTags.Select(t => t.Tag.Name).ToList()
            })
            .ToListAsync(ct);

    public async Task<ProjectFormViewModel?> GetForEditAsync(int id, string userId, CancellationToken ct = default)
    {
        var project = await db.Projects
            .AsNoTracking()
            .Include(p => p.ProjectTags).ThenInclude(t => t.Tag)
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
        if (project is null) return null;

        return new ProjectFormViewModel
        {
            Id = project.Id,
            Name = project.Name,
            PeriodStart = project.PeriodStart,
            PeriodEnd = project.PeriodEnd,
            DescriptionMarkdown = project.DescriptionMarkdown,
            Tags = project.ProjectTags.Select(t => t.Tag.Name).ToList(),
            RowVersion = Convert.ToBase64String(project.RowVersion)
        };
    }

    public async Task<ProjectSaveOutcome> CreateAsync(ProjectFormViewModel form, string userId, CancellationToken ct = default)
    {
        if (form.PeriodEnd is not null && form.PeriodEnd < form.PeriodStart)
        {
            return ProjectSaveOutcome.ValidationError("End date can't be before the start date.");
        }

        var project = new Project
        {
            UserId = userId,
            Name = form.Name.Trim(),
            PeriodStart = form.PeriodStart,
            PeriodEnd = form.PeriodEnd,
            DescriptionMarkdown = form.DescriptionMarkdown ?? string.Empty
        };

        var tags = await tagService.ResolveOrCreateTagsAsync(form.Tags, ct);
        foreach (var tag in tags)
        {
            project.ProjectTags.Add(new ProjectTag { TagId = tag.Id });
        }

        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
        return ProjectSaveOutcome.Success(project.Id);
    }

    public async Task<ProjectSaveOutcome> UpdateAsync(ProjectFormViewModel form, string userId, CancellationToken ct = default)
    {
        var project = await db.Projects
            .Include(p => p.ProjectTags)
            .FirstOrDefaultAsync(p => p.Id == form.Id && p.UserId == userId, ct);
        if (project is null) return ProjectSaveOutcome.NotFound();

        if (form.PeriodEnd is not null && form.PeriodEnd < form.PeriodStart)
        {
            return ProjectSaveOutcome.ValidationError("End date can't be before the start date.");
        }

        db.Entry(project).Property(p => p.RowVersion).OriginalValue = Convert.FromBase64String(form.RowVersion ?? string.Empty);

        project.Name = form.Name.Trim();
        project.PeriodStart = form.PeriodStart;
        project.PeriodEnd = form.PeriodEnd;
        project.DescriptionMarkdown = form.DescriptionMarkdown ?? string.Empty;
        project.UpdatedAt = DateTime.UtcNow;

        db.ProjectTags.RemoveRange(project.ProjectTags);
        project.ProjectTags.Clear();
        var tags = await tagService.ResolveOrCreateTagsAsync(form.Tags, ct);
        foreach (var tag in tags)
        {
            project.ProjectTags.Add(new ProjectTag { TagId = tag.Id });
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            var current = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == form.Id, ct);
            return current is null ? ProjectSaveOutcome.NotFound() : ProjectSaveOutcome.Conflict(Convert.ToBase64String(current.RowVersion));
        }

        return ProjectSaveOutcome.Success(project.Id);
    }

    public async Task DeleteAsync(IReadOnlyList<int> ids, string userId, CancellationToken ct = default)
        => await db.Projects.Where(p => ids.Contains(p.Id) && p.UserId == userId).ExecuteDeleteAsync(ct);
}
