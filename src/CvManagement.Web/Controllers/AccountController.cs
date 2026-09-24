using Microsoft.Extensions.Localization;
using System.Security.Claims;
using CvManagement.Web.Data;
using CvManagement.Web.Models;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Controllers;

public class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IUserOnboardingService onboarding,
    IAppEmailSender emailSender,
    ApplicationDbContext db,
    ILogger<AccountController> logger,
    IStringLocalizer<SharedResource> localizer) : Controller
{
    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
        => View(new RegisterViewModel { ReturnUrl = returnUrl });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = $"{model.FirstName} {model.LastName}"
        };

        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        await userManager.AddToRoleAsync(user, RoleNames.Candidate);
        await onboarding.InitializeNewUserAsync(user);
        await SetBuiltInNameAsync(user.Id, model.FirstName, model.LastName);

        var confirmationLink = await SendConfirmationEmailAsync(user);
        logger.LogInformation("New candidate registered (email confirmation pending): {Email}", user.Email);

        // Not signed in yet -- RequireConfirmedAccount blocks sign-in until they click the link.
        return View("RegisterConfirmation", new RegisterConfirmationViewModel
        {
            Email = user.Email!,
            DevConfirmationLink = emailSender.IsConfigured ? null : confirmationLink
        });
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            return RedirectToAction(nameof(Login));
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return View("ConfirmEmail", false);
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        return View("ConfirmEmail", result.Succeeded);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendConfirmation(string email)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null && !await userManager.IsEmailConfirmedAsync(user))
        {
            var link = await SendConfirmationEmailAsync(user);
            return View("RegisterConfirmation", new RegisterConfirmationViewModel
            {
                Email = email,
                DevConfirmationLink = emailSender.IsConfigured ? null : link
            });
        }

        // Deliberately vague if the account doesn't exist or is already confirmed -- don't leak
        // which emails are registered.
        return View("RegisterConfirmation", new RegisterConfirmationViewModel { Email = email });
    }

    private async Task<string> SendConfirmationEmailAsync(ApplicationUser user)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = Url.Action(nameof(ConfirmEmail), "Account",
            new { userId = user.Id, token }, protocol: Request.Scheme)!;

        // Sent in the language the user registered in (the request culture).
        await emailSender.SendAsync(user.Email!, localizer["Email_ConfirmSubject"],
            localizer["Email_ConfirmBody", System.Net.WebUtility.HtmlEncode(link)]);

        return link;
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        var model = new LoginViewModel
        {
            ReturnUrl = returnUrl,
            ExternalLogins = (await signInManager.GetExternalAuthenticationSchemesAsync()).ToList()
        };
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.ExternalLogins = (await signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            return RedirectToLocal(model.ReturnUrl);
        }

        if (result.IsNotAllowed)
        {
            // RequireConfirmedAccount blocked this sign-in specifically because the email isn't
            // confirmed yet (as opposed to a wrong password) -- point them at "resend" instead of
            // a generic error.
            ModelState.AddModelError(string.Empty, localizer["Login_ConfirmFirst"]);
            ViewBag.UnconfirmedEmail = model.Email;
        }
        else
        {
            ModelState.AddModelError(string.Empty, result.IsLockedOut
                ? localizer["Login_Locked"]
                : localizer["Login_Invalid"]);
        }
        model.ExternalLogins = (await signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (remoteError is not null)
        {
            ModelState.AddModelError(string.Empty, localizer["Login_ExternalError", remoteError]);
            return View(nameof(Login), new LoginViewModel { ExternalLogins = (await signInManager.GetExternalAuthenticationSchemesAsync()).ToList() });
        }

        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var signInResult = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        if (signInResult.Succeeded)
        {
            return RedirectToLocal(returnUrl);
        }
        if (signInResult.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, localizer["Login_AccountLocked"]);
            return View(nameof(Login), new LoginViewModel());
        }

        // First time this external identity signs in: provision a new Candidate account for it.
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
        {
            ModelState.AddModelError(string.Empty, localizer["Login_ExternalNoEmail", info.LoginProvider]);
            return View(nameof(Login), new LoginViewModel { ExternalLogins = (await signInManager.GetExternalAuthenticationSchemesAsync()).ToList() });
        }

        var firstName = info.Principal.FindFirstValue(ClaimTypes.GivenName);
        var lastName = info.Principal.FindFirstValue(ClaimTypes.Surname);
        if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
        {
            var fullName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email;
            var parts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            firstName = parts.Length > 0 ? parts[0] : string.Empty;
            lastName = parts.Length > 1 ? parts[1] : string.Empty;
        }

        var existingUser = await userManager.FindByEmailAsync(email);

        // Linking a new external login to an existing account ends in SignInAsync below, which does not
        // check lockout itself -- without this, an Administrator-blocked user could get back in simply by
        // choosing "Sign in with Google" for the first time.
        if (existingUser is not null && await userManager.IsLockedOutAsync(existingUser))
        {
            ModelState.AddModelError(string.Empty, localizer["Login_AccountLocked"]);
            return View(nameof(Login), new LoginViewModel { ExternalLogins = (await signInManager.GetExternalAuthenticationSchemesAsync()).ToList() });
        }

        var user = existingUser ?? new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = $"{firstName} {lastName}".Trim()
        };

        if (existingUser is null)
        {
            var createResult = await userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(nameof(Login), new LoginViewModel { ExternalLogins = (await signInManager.GetExternalAuthenticationSchemesAsync()).ToList() });
            }

            await userManager.AddToRoleAsync(user, RoleNames.Candidate);
            await onboarding.InitializeNewUserAsync(user);
            await SetBuiltInNameAsync(user.Id, firstName ?? string.Empty, lastName ?? string.Empty);
        }

        var addLoginResult = await userManager.AddLoginAsync(user, info);
        if (!addLoginResult.Succeeded)
        {
            foreach (var error in addLoginResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(nameof(Login), new LoginViewModel { ExternalLogins = (await signInManager.GetExternalAuthenticationSchemesAsync()).ToList() });
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        logger.LogInformation("User signed in via {Provider}: {Email}", info.LoginProvider, email);
        return RedirectToLocal(returnUrl);
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    private async Task SetBuiltInNameAsync(string userId, string firstName, string lastName)
    {
        string[] keys = [BuiltInAttributeKeys.FirstName, BuiltInAttributeKeys.LastName];
        var rows = await db.UserAttributeValues
            .Include(v => v.Attribute)
            .Where(v => v.UserId == userId && keys.Contains(v.Attribute.SystemKey!))
            .ToListAsync();

        var first = rows.FirstOrDefault(r => r.Attribute.SystemKey == BuiltInAttributeKeys.FirstName);
        if (first is not null) first.ValueString = firstName;

        var last = rows.FirstOrDefault(r => r.Attribute.SystemKey == BuiltInAttributeKeys.LastName);
        if (last is not null) last.ValueString = lastName;

        await db.SaveChangesAsync();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
        => Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction("Index", "Home");
}
