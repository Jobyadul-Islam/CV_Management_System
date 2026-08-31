using CvManagement.Web.ViewModels.Profile;

namespace CvManagement.Web.Services.Abstractions;

public enum ProjectSaveStatus { Success, Conflict, NotFound, ValidationError }

public record ProjectSaveOutcome(ProjectSaveStatus Status, int Id = 0, string? CurrentRowVersion = null, string? Error = null)
{
    public static ProjectSaveOutcome Success(int id) => new(ProjectSaveStatus.Success, id);
    public static ProjectSaveOutcome Conflict(string currentRowVersion) => new(ProjectSaveStatus.Conflict, CurrentRowVersion: currentRowVersion);
    public static ProjectSaveOutcome NotFound() => new(ProjectSaveStatus.NotFound);
    public static ProjectSaveOutcome ValidationError(string error) => new(ProjectSaveStatus.ValidationError, Error: error);
}

/// <summary>Candidate-owned project entries (name, period, markdown description, technology tags).</summary>
public interface IProjectService
{
    Task<IReadOnlyList<ProjectListItemViewModel>> GetListAsync(string userId, CancellationToken ct = default);
    Task<ProjectFormViewModel?> GetForEditAsync(int id, string userId, CancellationToken ct = default);
    Task<ProjectSaveOutcome> CreateAsync(ProjectFormViewModel form, string userId, CancellationToken ct = default);
    Task<ProjectSaveOutcome> UpdateAsync(ProjectFormViewModel form, string userId, CancellationToken ct = default);
    Task DeleteAsync(IReadOnlyList<int> ids, string userId, CancellationToken ct = default);
}
