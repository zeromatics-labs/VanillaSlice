using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using {{ProjectName}}.Server.Data;

namespace {{ProjectName}}.Server.Data.Services;

/// <summary>Sends account emails over SMTP. Reads credentials from the "Smtp" configuration section.</summary>
public sealed class SmtpEmailSender : IEmailSender<ApplicationUser>
{
    private readonly IConfiguration _configuration;

    public SmtpEmailSender(IConfiguration configuration) => _configuration = configuration;

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(email, "Confirm your account",
            $"<p>Please confirm your account by <a href=\"{confirmationLink}\">clicking here</a>.</p>");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(email, "Reset your password",
            $"<p>Reset your password by <a href=\"{resetLink}\">clicking here</a>.</p>");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(email, "Your password reset code", $"<p>Your reset code is: <strong>{resetCode}</strong></p>");

    private async Task SendAsync(string to, string subject, string htmlBody)
    {
        var section = _configuration.GetSection("Smtp");
        using var client = new SmtpClient(section["Host"], int.Parse(section["Port"] ?? "587"))
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(section["UserName"], section["Password"]),
        };

        using var message = new MailMessage("{{EmailFromAddress}}", to, subject, htmlBody) { IsBodyHtml = true };
        await client.SendMailAsync(message);
    }
}
