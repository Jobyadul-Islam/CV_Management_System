using CvManagement.Web.ViewModels.Admin;

namespace CvManagement.Web.Services.Abstractions;

public interface IAdminUserService
{
    Task<IReadOnlyList<AdminUserListItemViewModel>> GetListAsync(CancellationToken ct = default);
    Task<AdminUserEditRolesViewModel?> GetForEditRolesAsync(string userId, CancellationToken ct = default);
    Task<bool> UpdateRolesAsync(string userId, List<string> roles, CancellationToken ct = default);
    Task BlockAsync(IReadOnlyList<string> userIds, CancellationToken ct = default);
    Task UnblockAsync(IReadOnlyList<string> userIds, CancellationToken ct = default);

    /// <summary>
    /// Deletes a user in an explicit transaction: their CvLikes-as-recruiter are removed first
    /// (that FK is Restrict, not Cascade, to avoid SQL Server's multiple-cascade-paths error), then
    /// the Identity user itself, which cascades everything else (their own Cvs/CvLikes/Projects/
    /// UserAttributeValues/AttributeUsages). Also drops them from the search index.
    /// </summary>
    Task DeleteAsync(IReadOnlyList<string> userIds, CancellationToken ct = default);
}
