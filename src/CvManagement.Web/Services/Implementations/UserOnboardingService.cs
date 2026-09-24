using CvManagement.Web.Data;
using CvManagement.Web.Models;
using CvManagement.Web.Services.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Services.Implementations;

public class UserOnboardingService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration) : IUserOnboardingService
{
    public const string AdminEmailsSetting = "Seed:AdminEmails";

    public async Task InitializeNewUserAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var builtInAttributeIds = await db.Attributes
            .Where(a => a.IsBuiltIn)
            .Select(a => a.Id)
            .ToListAsync(ct);

        var existingAttributeIds = await db.UserAttributeValues
            .Where(v => v.UserId == user.Id && builtInAttributeIds.Contains(v.AttributeId))
            .Select(v => v.AttributeId)
            .ToListAsync(ct);

        var missing = builtInAttributeIds.Except(existingAttributeIds);
        foreach (var attributeId in missing)
        {
            db.UserAttributeValues.Add(new UserAttributeValue { UserId = user.Id, AttributeId = attributeId });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> PromoteBootstrapAdminAsync(ApplicationUser user, CancellationToken ct = default)
    {
        // Confirmed only: registering with someone else's address must not be enough to become admin.
        if (!user.EmailConfirmed || string.IsNullOrEmpty(user.Email)) return false;
        if (!BootstrapAdminEmails(configuration).Contains(user.Email, StringComparer.OrdinalIgnoreCase)) return false;
        if (await userManager.IsInRoleAsync(user, RoleNames.Administrator)) return false;

        return (await userManager.AddToRoleAsync(user, RoleNames.Administrator)).Succeeded;
    }

    /// <summary>"Seed:AdminEmails" as a list; accepts a comma/semicolon-separated string (handy as a
    /// single hosting-platform setting) or a JSON array.</summary>
    public static IReadOnlyList<string> BootstrapAdminEmails(IConfiguration configuration)
    {
        var section = configuration.GetSection(AdminEmailsSetting);
        var values = section.GetChildren().Select(c => c.Value).Append(section.Value);
        return values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .SelectMany(v => v!.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
