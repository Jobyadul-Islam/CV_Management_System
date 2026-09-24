using CvManagement.Web.ViewModels;
using System.ComponentModel.DataAnnotations;

namespace CvManagement.Web.ViewModels.Account;

public class LoginViewModel
{
    [Required(ErrorMessage = ValidationMessages.Required), EmailAddress(ErrorMessage = ValidationMessages.Email), Display(Name = "Account_Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = ValidationMessages.Required), DataType(DataType.Password), Display(Name = "Account_Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Account_RememberMe")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }

    public IList<Microsoft.AspNetCore.Authentication.AuthenticationScheme> ExternalLogins { get; set; } = [];
}
