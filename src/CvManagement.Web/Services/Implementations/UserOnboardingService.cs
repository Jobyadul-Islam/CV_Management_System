using CvManagement.Web.Data;
using CvManagement.Web.Models;
using CvManagement.Web.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Services.Implementations;

public class UserOnboardingService(ApplicationDbContext db) : IUserOnboardingService
{
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
}
