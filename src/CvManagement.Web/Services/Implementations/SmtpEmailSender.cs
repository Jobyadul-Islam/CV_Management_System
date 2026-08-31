using System.Net;
using System.Net.Mail;
using CvManagement.Web.Services.Abstractions;

namespace CvManagement.Web.Services.Implementations;

public class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IAppEmailSender
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["Email:SmtpHost"]);

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var host = configuration["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            // No SMTP configured -- log the email (link included) so the confirmation flow is
            // fully verifiable locally without needing real credentials.
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

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            Credentials = string.IsNullOrEmpty(username) ? null : new NetworkCredential(username, password)
        };

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        await client.SendMailAsync(message, ct);
    }
}
