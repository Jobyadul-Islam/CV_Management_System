namespace CvManagement.Web.ViewModels.Account;

public class RegisterConfirmationViewModel
{
    public string Email { get; set; } = string.Empty;

    /// <summary>Only set when no SMTP server is configured, so the flow is testable without one.</summary>
    public string? DevConfirmationLink { get; set; }
}
