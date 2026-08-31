using CvManagement.Web.ViewModels.Profile;

namespace CvManagement.Web.Services.Abstractions;

/// <summary>Me tab (built-in attributes) and Info tab (candidate-selected library attributes).</summary>
public interface IProfileService
{
    Task<IReadOnlyList<AttributeValueViewModel>> GetMeTabAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<AttributeValueViewModel>> GetInfoTabAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<int>> GetInfoTabAttributeIdsAsync(string userId, CancellationToken ct = default);
    Task AddToInfoAsync(string userId, int attributeId, CancellationToken ct = default);
    Task RemoveFromInfoAsync(string userId, int attributeId, CancellationToken ct = default);
}
