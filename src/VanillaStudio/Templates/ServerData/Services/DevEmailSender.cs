using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using {{ProjectName}}.Server.Data;

namespace {{ProjectName}}.Server.Data.Services;

/// <summary>
/// Development email sender. Writes each message to the logger AND to
/// sent-emails/*.html next to the running application.
///
/// The file output is not redundant: when testing on a phone or emulator there is
/// no console to read a confirmation link from, and account confirmation is
/// required (SignIn.RequireConfirmedAccount = true).
/// </summary>
public sealed class DevEmailSender : IEmailSender<ApplicationUser>
{
    private readonly ILogger<DevEmailSender> _logger;
    private readonly string _outputDirectory;

    public DevEmailSender(ILogger<DevEmailSender> logger)
    {
        _logger = logger;
        _outputDirectory = Path.Combine(AppContext.BaseDirectory, "sent-emails");
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        WriteAsync(email, "Confirm your account", confirmationLink);

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        WriteAsync(email, "Reset your password", resetLink);

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        WriteAsync(email, "Your password reset code", resetCode);

    private async Task WriteAsync(string email, string subject, string body)
    {
        Directory.CreateDirectory(_outputDirectory);
        var safeEmail = string.Concat(email.Split(Path.GetInvalidFileNameChars()));
        var fileName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{safeEmail}.html";
        var filePath = Path.Combine(_outputDirectory, fileName);

        var html = $"""
            <html><body>
              <p><strong>To:</strong> {email}</p>
              <p><strong>From:</strong> {{EmailFromAddress}}</p>
              <p><strong>Subject:</strong> {subject}</p>
              <hr />
              <p>{body}</p>
            </body></html>
            """;

        await File.WriteAllTextAsync(filePath, html);

        // A phone or emulator has no console to read a confirmation link from, so the
        // absolute path is logged here at Warning level to make it discoverable regardless
        // of log verbosity configuration.
        _logger.LogWarning("[DevEmailSender] Wrote \"{Subject}\" for {Email} to {FilePath}", subject, email, filePath);
    }
}
