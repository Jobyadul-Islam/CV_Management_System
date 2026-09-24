using CvManagement.Web.Data;
using CvManagement.Web.Models;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Services.Implementations;

public class AdminUserService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    ISearchIndexService searchIndex) : IAdminUserService
{
    public async Task<IReadOnlyList<AdminUserListItemViewModel>> GetListAsync(CancellationToken ct = default)
    {
        var users = await db.Users.AsNoTracking()
            .Select(u => new { u.Id, u.DisplayName, u.Email, u.LockoutEnd })
            .ToListAsync(ct);

        var roleAssignments = await (from ur in db.UserRoles
                                      join r in db.Roles on ur.RoleId equals r.Id
                                      select new { ur.UserId, RoleName = r.Name })
            .ToListAsync(ct);
        var rolesByUser = roleAssignments.GroupBy(r => r.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName!).ToList());

        return users.Select(u => new AdminUserListItemViewModel
        {
            Id = u.Id,
            DisplayName = u.DisplayName,
            Email = u.Email ?? string.Empty,
            Roles = rolesByUser.GetValueOrDefault(u.Id, []),
            IsBlocked = u.LockoutEnd is not null && u.LockoutEnd > DateTimeOffset.UtcNow
        }).ToList();
    }

    public async Task<AdminUserEditRolesViewModel?> GetForEditRolesAsync(string userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return null;

        var currentRoles = await userManager.GetRolesAsync(user);
        return new AdminUserEditRolesViewModel
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            SelectedRoles = [.. currentRoles],
            AllRoles = [.. RoleNames.All]
        };
    }

    public async Task<bool> UpdateRolesAsync(string userId, List<string> roles, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return false;

        var validRoles = roles.Where(r => RoleNames.All.Contains(r)).Distinct().ToList();
        var currentRoles = await userManager.GetRolesAsync(user);

        var toAdd = validRoles.Except(currentRoles).ToList();
        var toRemove = currentRoles.Except(validRoles).ToList();

        if (toAdd.Count > 0 && !(await userManager.AddToRolesAsync(user, toAdd)).Succeeded) return false;
        if (toRemove.Count > 0 && !(await userManager.RemoveFromRolesAsync(user, toRemove)).Succeeded) return false;

        // Roles live in the auth cookie; rotating the stamp makes that user's existing sessions
        // re-validate (see SecurityStampValidatorOptions in Program.cs) and pick up the new roles.
        await userManager.UpdateSecurityStampAsync(user);
        return true;
    }

    // Block/Unblock/Delete are set-based -- one statement per operation for the whole selection, never
    // one query per selected user. Rotating SecurityStamp in the same UPDATE is what signs a blocked
    // user out of sessions that are already open; lockout on its own only stops *new* sign-ins.
    public async Task BlockAsync(IReadOnlyList<string> userIds, CancellationToken ct = default)
    {
        var stamp = NewStamp();
        await db.Users.Where(u => userIds.Contains(u.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.LockoutEnabled, true)
                .SetProperty(u => u.LockoutEnd, DateTimeOffset.MaxValue)
                .SetProperty(u => u.SecurityStamp, stamp)
                .SetProperty(u => u.ConcurrencyStamp, stamp), ct);
    }

    public async Task UnblockAsync(IReadOnlyList<string> userIds, CancellationToken ct = default)
    {
        var stamp = NewStamp();
        await db.Users.Where(u => userIds.Contains(u.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.LockoutEnd, (DateTimeOffset?)null)
                .SetProperty(u => u.AccessFailedCount, 0)
                .SetProperty(u => u.ConcurrencyStamp, stamp), ct);
    }

    public async Task DeleteAsync(IReadOnlyList<string> userIds, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Likes this user *gave* are the one Restrict path (see CvLikeConfiguration); everything else
        // hanging off AspNetUsers (roles, logins, profile values, projects, CVs + their likes) cascades
        // in the database, and discussion posts keep their author-name snapshot via SET NULL.
        await db.CvLikes.Where(l => userIds.Contains(l.RecruiterUserId)).ExecuteDeleteAsync(ct);
        await db.Users.Where(u => userIds.Contains(u.Id)).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);

        foreach (var userId in userIds)
        {
            await searchIndex.RemoveCandidateAsync(userId, ct); // in-process Lucene index, not a DB query
        }
    }

    private static string NewStamp() => Guid.NewGuid().ToString("N").ToUpperInvariant();
}
