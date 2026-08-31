using CvManagement.Web.ViewModels.Profile;

namespace CvManagement.Web.Services.Abstractions;

/// <summary>
/// Backs the profile auto-save endpoint. Each change is applied and committed independently (its
/// own SaveChangesAsync) so one conflicting field doesn't abort the rest of the batch.
/// </summary>
public interface IProfileAutoSaveService
{
    Task<List<AutoSaveResultDto>> SaveAsync(string userId, List<AutoSaveChangeDto> changes, CancellationToken ct = default);
}
