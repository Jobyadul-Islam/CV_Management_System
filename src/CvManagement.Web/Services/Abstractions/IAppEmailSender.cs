namespace CvManagement.Web.Services.Abstractions;

/// <summary>
/// Sends transactional email (currently just email-confirmation links). Falls back to logging the
/// message when no SMTP server is configured, so the confirmation flow is fully testable locally
/// without real credentials -- see SmtpEmailSender.
/// </summary>
public interface IAppEmailSender
{
    /// <summary>False when no SMTP host is configured -- callers can use this to show a dev-mode fallback.</summary>
    bool IsConfigured { get; }

    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}
