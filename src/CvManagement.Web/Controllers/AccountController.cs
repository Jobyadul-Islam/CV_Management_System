using System.Security.Claims;
using CvManagement.Web.Data;
using CvManagement.Web.Domain;
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
    ILogger<AccountController> logger) : Controller
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

        await emailSender.SendAsync(user.Email!, "Confirm your CV Management System account",
            $"<p>Welcome! Please confirm your account by <a href=\"{link}\">clicking here</a>.</p>");

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
            ModelState.AddModelError(string.Empty, "Please confirm your email before signing in.");
            ViewBag.UnconfirmedEmail = model.Email;
        }
        else
        {
            ModelState.AddModelError(string.Empty, result.IsLockedOut
                ? "This account is locked. Try again later or contact an administrator."
                : "Invalid email or password.");
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
            ModelState.AddModelError(string.Empty, $"External provider error: {remoteError}");
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
            ModelState.AddModelError(string.Empty, "This account is locked.");
            return View(nameof(Login), new LoginViewModel());
        }

        // First time this external identity signs in: provision a new Candidate account for it.
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
        {
            ModelState.AddModelError(string.Empty, $"{info.LoginProvider} did not provide an email address; cannot create an account.");
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
        var names = new[] { "First Name", "Last Name" };
        var rows = await db.UserAttributeValues
            .Include(v => v.Attribute)
            .Where(v => v.UserId == userId && names.Contains(v.Attribute.Name))
            .ToListAsync();

        var first = rows.FirstOrDefault(r => r.Attribute.Name == "First Name");
        if (first is not null) first.ValueString = firstName;

        var last = rows.FirstOrDefault(r => r.Attribute.Name == "Last Name");
        if (last is not null) last.ValueString = lastName;

        await db.SaveChangesAsync();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
        => Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction("Index", "Home");
}
