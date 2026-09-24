using CvManagement.Web.ViewModels;
using System.ComponentModel.DataAnnotations;

namespace CvManagement.Web.ViewModels.Account;

public class RegisterViewModel
{
    [Required(ErrorMessage = ValidationMessages.Required), EmailAddress(ErrorMessage = ValidationMessages.Email), Display(Name = "Account_Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = ValidationMessages.Required), StringLength(100, MinimumLength = 8, ErrorMessage = ValidationMessages.StringLengthRange)]
    [DataType(DataType.Password), Display(Name = "Account_Password")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password), Display(Name = "Account_ConfirmPassword")]
    [Compare(nameof(Password), ErrorMessage = ValidationMessages.PasswordsMatch)]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = ValidationMessages.Required), StringLength(100, ErrorMessage = ValidationMessages.StringLength), Display(Name = "Account_FirstName")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = ValidationMessages.Required), StringLength(100, ErrorMessage = ValidationMessages.StringLength), Display(Name = "Account_LastName")]
    public string LastName { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
