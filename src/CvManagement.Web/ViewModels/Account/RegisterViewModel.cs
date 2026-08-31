using System.ComponentModel.DataAnnotations;

namespace CvManagement.Web.ViewModels.Account;

public class RegisterViewModel
{
    [Required, EmailAddress, Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required, StringLength(100), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
