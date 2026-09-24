using CvManagement.Web.Services.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace CvManagement.Web.Services.Implementations;

/// <summary>
/// MailKit-based SMTP sender (System.Net.Mail.SmtpClient is marked obsolete for new development by
/// Microsoft, which recommends MailKit). Without Email:SmtpHost configured it only logs the message,
/// so the confirmation and reminder flows are fully verifiable locally without real credentials.
/// </summary>
public class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IAppEmailSender
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["Email:SmtpHost"]);

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var host = configuration["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation(
                "Email:SmtpHost not configured -- would have sent to {ToEmail}, subject '{Subject}':\n{Body}",
                toEmail, subject, htmlBody);
            return;
        }

        var port = int.TryParse(configuration["Email:SmtpPort"], out var p) ? p : 587;
        var username = configuration["Email:SmtpUsername"];
        var password = configuration["Email:SmtpPassword"];
        var fromAddress = configuration["Email:FromAddress"] ?? username ?? "no-reply@example.com";
        var fromName = configuration["Email:FromName"] ?? "CV Management System";
        var enableSsl = configuration.GetValue("Email:EnableSsl", true);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        // Port 465 is implicit TLS; anything else upgrades via STARTTLS when SSL is enabled.
        var socketOptions = !enableSsl ? SecureSocketOptions.None
            : port == 465 ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;

        await client.ConnectAsync(host, port, socketOptions, ct);
        if (!string.IsNullOrEmpty(username))
        {
            await client.AuthenticateAsync(username, password ?? string.Empty, ct);
        }
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }
}
