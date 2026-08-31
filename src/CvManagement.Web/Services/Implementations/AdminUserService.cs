using CvManagement.Web.Data;
using CvManagement.Web.Domain;
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

        if (toAdd.Count > 0) await userManager.AddToRolesAsync(user, toAdd);
        if (toRemove.Count > 0) await userManager.RemoveFromRolesAsync(user, toRemove);

        return true;
    }

    public async Task BlockAsync(IReadOnlyList<string> userIds, CancellationToken ct = default)
    {
        foreach (var userId in userIds)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user is null) continue;

            await userManager.SetLockoutEnabledAsync(user, true);
            await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        }
    }

    public async Task UnblockAsync(IReadOnlyList<string> userIds, CancellationToken ct = default)
    {
        foreach (var userId in userIds)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user is null) continue;

            await userManager.SetLockoutEndDateAsync(user, null);
        }
    }

    public async Task DeleteAsync(IReadOnlyList<string> userIds, CancellationToken ct = default)
    {
        foreach (var userId in userIds)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user is null) continue;

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.CvLikes.Where(l => l.RecruiterUserId == userId).ExecuteDeleteAsync(ct);
            await userManager.DeleteAsync(user);
            await tx.CommitAsync(ct);

            await searchIndex.RemoveCandidateAsync(userId, ct);
        }
    }
}
