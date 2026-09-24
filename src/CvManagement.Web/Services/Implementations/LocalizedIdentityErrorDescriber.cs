using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace CvManagement.Web.Services.Implementations;

/// <summary>
/// ASP.NET Core Identity's built-in error texts come from its own English-only resources. This
/// describer returns the same error codes with descriptions from SharedResource, so registration and
/// sign-in errors follow the UI language. Only the errors reachable through this app's flows are
/// overridden; anything else falls back to Identity's default text.
/// </summary>
public class LocalizedIdentityErrorDescriber(IStringLocalizer<SharedResource> localizer) : IdentityErrorDescriber
{
    private IdentityError Error(string code, string key, params object[] args)
        => new() { Code = code, Description = localizer[key, args] };

    public override IdentityError DefaultError()
        => Error(nameof(DefaultError), "Identity_DefaultError");

    public override IdentityError ConcurrencyFailure()
        => Error(nameof(ConcurrencyFailure), "Identity_ConcurrencyFailure");

    public override IdentityError DuplicateEmail(string email)
        => Error(nameof(DuplicateEmail), "Identity_DuplicateEmail", email);

    public override IdentityError DuplicateUserName(string userName)
        => Error(nameof(DuplicateUserName), "Identity_DuplicateUserName", userName);

    public override IdentityError InvalidEmail(string? email)
        => Error(nameof(InvalidEmail), "Identity_InvalidEmail", email ?? string.Empty);

    public override IdentityError InvalidUserName(string? userName)
        => Error(nameof(InvalidUserName), "Identity_InvalidUserName", userName ?? string.Empty);

    public override IdentityError InvalidToken()
        => Error(nameof(InvalidToken), "Identity_InvalidToken");

    public override IdentityError LoginAlreadyAssociated()
        => Error(nameof(LoginAlreadyAssociated), "Identity_LoginAlreadyAssociated");

    public override IdentityError PasswordTooShort(int length)
        => Error(nameof(PasswordTooShort), "Identity_PasswordTooShort", length);

    public override IdentityError PasswordRequiresNonAlphanumeric()
        => Error(nameof(PasswordRequiresNonAlphanumeric), "Identity_PasswordRequiresNonAlphanumeric");

    public override IdentityError PasswordRequiresDigit()
        => Error(nameof(PasswordRequiresDigit), "Identity_PasswordRequiresDigit");

    public override IdentityError PasswordRequiresLower()
        => Error(nameof(PasswordRequiresLower), "Identity_PasswordRequiresLower");

    public override IdentityError PasswordRequiresUpper()
        => Error(nameof(PasswordRequiresUpper), "Identity_PasswordRequiresUpper");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars)
        => Error(nameof(PasswordRequiresUniqueChars), "Identity_PasswordRequiresUniqueChars", uniqueChars);

    public override IdentityError UserAlreadyInRole(string role)
        => Error(nameof(UserAlreadyInRole), "Identity_UserAlreadyInRole", role);

    public override IdentityError UserNotInRole(string role)
        => Error(nameof(UserNotInRole), "Identity_UserNotInRole", role);
}
