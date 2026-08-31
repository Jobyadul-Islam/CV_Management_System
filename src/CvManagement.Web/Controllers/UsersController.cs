using CvManagement.Web.Domain;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CvManagement.Web.Controllers;

[Authorize(Roles = RoleNames.Administrator)]
public class UsersController(
    IAdminUserService adminUsers,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
        => View(await adminUsers.GetListAsync());

    [HttpGet]
    public async Task<IActionResult> EditRoles(string id)
    {
        var model = await adminUsers.GetForEditRolesAsync(id);
        if (model is null) return NotFound();
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRoles(string id, List<string> roles)
    {
        var isSelf = userManager.GetUserId(User) == id;
        var removingOwnAdmin = isSelf && !roles.Contains(RoleNames.Administrator);

        var ok = await adminUsers.UpdateRolesAsync(id, roles ?? []);
        if (!ok) return NotFound();

        TempData["StatusMessage"] = "Roles updated.";

        // Spec explicitly allows an Administrator to remove their own Administrator role -- their
        // current session cookie's role claims are now stale, so refresh it (or they'd keep acting
        // as Administrator until next login).
        if (removingOwnAdmin)
        {
            var user = await userManager.GetUserAsync(User);
            if (user is not null) await signInManager.RefreshSignInAsync(user);
            return RedirectToAction("Index", "Home");
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Block(string[] ids)
    {
        await adminUsers.BlockAsync(ids);
        TempData["StatusMessage"] = $"Blocked {ids.Length} user(s).";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Unblock(string[] ids)
    {
        await adminUsers.UnblockAsync(ids);
        TempData["StatusMessage"] = $"Unblocked {ids.Length} user(s).";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string[] ids)
    {
        var deletingSelf = ids.Contains(userManager.GetUserId(User));
        await adminUsers.DeleteAsync(ids);
        TempData["StatusMessage"] = $"Deleted {ids.Length} user(s).";

        if (deletingSelf)
        {
            await signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
        return RedirectToAction(nameof(Index));
    }
}
