using CvManagement.Web.ViewModels.Position;

namespace CvManagement.Web.Services.Abstractions;

public enum PositionSaveStatus { Success, Conflict, NotFound, ValidationError }

public record PositionSaveOutcome(PositionSaveStatus Status, int Id = 0, string? CurrentRowVersion = null, IReadOnlyList<string>? Errors = null)
{
    public static PositionSaveOutcome Success(int id) => new(PositionSaveStatus.Success, id);
    public static PositionSaveOutcome Conflict(string currentRowVersion) => new(PositionSaveStatus.Conflict, CurrentRowVersion: currentRowVersion);
    public static PositionSaveOutcome NotFound() => new(PositionSaveStatus.NotFound);
    public static PositionSaveOutcome ValidationError(params string[] errors) => new(PositionSaveStatus.ValidationError, Errors: errors);
}

public record PositionDeleteBlocked(int Id, string Title, string Reason);
public record PositionDeleteOutcome(IReadOnlyList<int> DeletedIds, IReadOnlyList<PositionDeleteBlocked> Blocked);

/// <summary>
/// Shared Position CRUD (all Recruiters manage the same pool, no ownership). Positions serve as CV
/// templates: attribute selection, access rules, and project tags are edited here but never copied
/// into a CV -- ICvRenderService (Phase 6) looks them up live from a Position at render time.
/// </summary>
public interface IPositionService
{
    Task<IReadOnlyList<PositionListItemViewModel>> GetListForRecruiterAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PositionListItemViewModel>> GetEligibleListForCandidateAsync(string userId, CancellationToken ct = default);
    Task<PositionDetailsViewModel?> GetDetailsAsync(int id, CancellationToken ct = default);
    Task<PositionFormViewModel?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<PositionFormViewModel> GetBlankFormAsync(CancellationToken ct = default);

    Task<PositionSaveOutcome> CreateAsync(PositionFormViewModel form, string userId, CancellationToken ct = default);
    Task<PositionSaveOutcome> UpdateAsync(PositionFormViewModel form, string userId, CancellationToken ct = default);
    Task<PositionDeleteOutcome> DeleteAsync(IReadOnlyList<int> ids, CancellationToken ct = default);
    Task<PositionSaveOutcome> DuplicateAsync(int id, string userId, CancellationToken ct = default);
}
