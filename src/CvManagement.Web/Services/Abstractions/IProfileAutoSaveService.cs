using CvManagement.Web.ViewModels.Profile;

namespace CvManagement.Web.Services.Abstractions;

/// <summary>
/// Backs the profile auto-save endpoint. The whole batch is loaded with a fixed number of queries and
/// committed in one SaveChanges; a field whose version is stale is reported as a conflict and dropped
/// from the batch, so one conflicting field doesn't abort the rest.
/// </summary>
public interface IProfileAutoSaveService
{
    Task<List<AutoSaveResultDto>> SaveAsync(string userId, List<AutoSaveChangeDto> changes, CancellationToken ct = default);
}
