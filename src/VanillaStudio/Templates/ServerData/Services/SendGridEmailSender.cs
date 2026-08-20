using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using {{ProjectName}}.Server.Data;

namespace {{ProjectName}}.Server.Data.Services;

/// <summary>Sends account emails through the SendGrid v3 API.</summary>
public sealed class SendGridEmailSender : IEmailSender<ApplicationUser>
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public SendGridEmailSender(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

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
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send")
        {
            Content = JsonContent.Create(new
            {
                personalizations = new[] { new { to = new[] { new { email = to } } } },
                from = new { email = "{{EmailFromAddress}}" },
                subject,
                content = new[] { new { type = "text/html", value = htmlBody } },
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration["SendGrid:ApiKey"]);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
