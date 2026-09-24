using CvManagement.Web.Models;

namespace CvManagement.Web.Services.Abstractions;

/// <summary>
/// One-time setup performed the first time a user account exists: creates the empty Me-tab
/// UserAttributeValue rows (First Name, Last Name, Location, Personal Photo) so the profile's
/// Me section always has a row per built-in attribute, ready to be filled in. Idempotent.
/// </summary>
public interface IUserOnboardingService
{
    Task InitializeNewUserAsync(ApplicationUser user, CancellationToken ct = default);
}
