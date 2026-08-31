using CvManagement.Web.Domain;
using CvManagement.Web.ViewModels.Attribute;

namespace CvManagement.Web.Services.Abstractions;

public enum AttributeSaveStatus { Success, Conflict, NotFound, ValidationError }

public record AttributeSaveOutcome(AttributeSaveStatus Status, int Id = 0, byte[]? CurrentRowVersion = null, IReadOnlyList<string>? Errors = null)
{
    public static AttributeSaveOutcome Success(int id) => new(AttributeSaveStatus.Success, id);
    public static AttributeSaveOutcome Conflict(byte[] currentRowVersion) => new(AttributeSaveStatus.Conflict, CurrentRowVersion: currentRowVersion);
    public static AttributeSaveOutcome NotFound() => new(AttributeSaveStatus.NotFound);
    public static AttributeSaveOutcome ValidationError(params string[] errors) => new(AttributeSaveStatus.ValidationError, Errors: errors);
}

public record AttributeDeleteBlocked(int Id, string Name, string Reason);
public record AttributeDeleteOutcome(IReadOnlyList<int> DeletedIds, IReadOnlyList<AttributeDeleteBlocked> Blocked);

/// <summary>
/// The shared Attribute Library: CRUD for the Recruiter/Admin-managed pool of attributes, plus the
/// lookup services (prefix search, category filter, recently-used) backing the attribute picker used
/// here, in position templates (Phase 4), and in profile "Info" tab attribute selection (Phase 5).
/// </summary>
public interface IAttributeService
{
    Task<IReadOnlyList<AttributeListItemViewModel>> GetLibraryAsync(string? prefix, int? categoryId, CancellationToken ct = default);
    Task<IReadOnlyList<AttributeCategory>> GetCategoriesAsync(CancellationToken ct = default);
    Task<AttributeFormViewModel?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<AttributeSaveOutcome> CreateAsync(AttributeFormViewModel form, string userId, CancellationToken ct = default);
    Task<AttributeSaveOutcome> UpdateAsync(AttributeFormViewModel form, CancellationToken ct = default);
    Task<AttributeDeleteOutcome> DeleteAsync(IReadOnlyList<int> ids, CancellationToken ct = default);

    Task<IReadOnlyList<AttributePickerItemViewModel>> SearchForPickerAsync(
        string? prefix, int? categoryId, IReadOnlyCollection<int>? excludeIds, CancellationToken ct = default);
    Task<IReadOnlyList<AttributePickerItemViewModel>> GetRecentlyUsedAsync(
        string userId, IReadOnlyCollection<int>? excludeIds, int take = 8, CancellationToken ct = default);
    Task RecordUsageAsync(string userId, int attributeId, CancellationToken ct = default);
}
