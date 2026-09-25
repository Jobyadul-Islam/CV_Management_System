using System.ComponentModel.DataAnnotations;

namespace CvManagement.Web.ViewModels.Account;

/// <summary>"Forgot password?" form: the address to send the reset link to.</summary>
public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = ValidationMessages.Required), EmailAddress(ErrorMessage = ValidationMessages.Email),
     Display(Name = "Account_Email")]
    public string Email { get; set; } = string.Empty;
}

/// <summary>Shown after the request, whether or not the account exists (no account enumeration).</summary>
public class ForgotPasswordConfirmationViewModel
{
    public string Email { get; set; } = string.Empty;

    /// <summary>Only set when no SMTP server is configured, so the flow is testable without one.</summary>
    public string? DevResetLink { get; set; }
}

/// <summary>"Set a new password" form opened from the emailed link.</summary>
public class ResetPasswordViewModel
{
    [Required(ErrorMessage = ValidationMessages.Required), EmailAddress(ErrorMessage = ValidationMessages.Email),
     Display(Name = "Account_Email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Base64Url-encoded Identity reset token from the link; single-use and time-limited.</summary>
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = ValidationMessages.Required),
     StringLength(100, MinimumLength = 8, ErrorMessage = ValidationMessages.StringLengthRange),
     DataType(DataType.Password), Display(Name = "Reset_NewPassword")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password), Display(Name = "Account_ConfirmPassword"),
     Compare(nameof(Password), ErrorMessage = ValidationMessages.PasswordsMatch)]
    public string ConfirmPassword { get; set; } = string.Empty;
}
