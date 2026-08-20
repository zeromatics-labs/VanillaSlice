using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;

namespace {{ProjectName}}.ClientShared.Identity;

public record IdentityResultDto(bool Succeeded, string? Error);
public record UserInfoDto(string Email, bool IsEmailConfirmed);

/// <summary>
/// Typed wrapper over the endpoints mapped by MapIdentityApi on the API host under the
/// group prefix set in Task 5 (app.MapGroup("/identity").MapIdentityApi&lt;ApplicationUser&gt;()):
/// POST /identity/register, POST /identity/login, POST /identity/refresh,
/// POST /identity/forgotPassword, GET /identity/manage/info.
///
/// These are ASP.NET Core Identity's own opaque tokens, not JWTs. The login
/// endpoint returns tokens when useCookies is omitted, which is what native
/// clients need.
///
/// Ruling V: transport failure (offline device, timeout) is a failed result, never an
/// exception. Every method below catches HttpRequestException/TaskCanceledException around
/// its HTTP call(s) and returns the method's existing failure shape, the same way it already
/// handles a non-success status code — being offline is a normal state on a mobile app, not
/// an exceptional one, and an uncaught exception here would crash the caller instead of
/// surfacing "can't reach the server".
/// </summary>
public sealed class IdentityClient
{
    // Derived from HttpClientHelper.IdentityBasePath rather than re-typing "/identity" here,
    // so the group prefix set in Task 5 has exactly one source of truth.
    private const string Base = HttpClientHelper.IdentityBasePath;

    private const string OfflineError = "Could not reach the server. Check your connection and try again.";

    private readonly HttpClient _httpClient;
    private readonly TokenStorage _tokens;

    public IdentityClient(HttpClient httpClient, TokenStorage tokens)
    {
        _httpClient = httpClient;
        _tokens = tokens;
    }

    public async Task<IdentityResultDto> RegisterAsync(string email, string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/register", new { email, password });
            return response.IsSuccessStatusCode
                ? new IdentityResultDto(true, null)
                : new IdentityResultDto(false, await DescribeFailureAsync(response));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new IdentityResultDto(false, OfflineError);
        }
    }

    public async Task<IdentityResultDto> LoginAsync(string email, string password)
    {
        try
        {
            // No useCookies parameter: bearer mode.
            var response = await _httpClient.PostAsJsonAsync($"{Base}/login", new { email, password });
            if (!response.IsSuccessStatusCode)
                return new IdentityResultDto(false, await DescribeFailureAsync(response));

            var payload = await response.Content.ReadFromJsonAsync<AccessTokenPayload>();
            if (payload is null)
                return new IdentityResultDto(false, "Login succeeded but no tokens were returned.");

            await _tokens.SaveAsync(payload.AccessToken, payload.RefreshToken, payload.ExpiresIn);
            return new IdentityResultDto(true, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new IdentityResultDto(false, OfflineError);
        }
    }

    public async Task<IdentityResultDto> ForgotPasswordAsync(string email)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/forgotPassword", new { email });
            return response.IsSuccessStatusCode
                ? new IdentityResultDto(true, null)
                : new IdentityResultDto(false, await DescribeFailureAsync(response));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new IdentityResultDto(false, OfflineError);
        }
    }

    /// <summary>Exchanges the stored refresh token for a new access token. False means re-login is required.</summary>
    public async Task<bool> RefreshAsync()
    {
        var refreshToken = await _tokens.GetRefreshTokenAsync();
        if (string.IsNullOrEmpty(refreshToken)) return false;

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync($"{Base}/refresh", new { refreshToken });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // A transport failure is not proof the refresh token is invalid — do not clear it
            // here, or a network blip would silently sign the user out. Only an actual
            // rejection by the server (below) clears stored tokens.
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            await _tokens.ClearAsync();
            return false;
        }

        var payload = await response.Content.ReadFromJsonAsync<AccessTokenPayload>();
        if (payload is null) return false;

        await _tokens.SaveAsync(payload.AccessToken, payload.RefreshToken, payload.ExpiresIn);
        return true;
    }

    public Task LogoutAsync() => _tokens.ClearAsync();

    public async Task<UserInfoDto?> GetInfoAsync()
    {
        var accessToken = await _tokens.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(accessToken)) return null;

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Base}/manage/info");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode) return null;

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        return new UserInfoDto(
            root.GetProperty("email").GetString() ?? string.Empty,
            root.GetProperty("isEmailConfirmed").GetBoolean());
    }

    private static async Task<string> DescribeFailureAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body)) return $"Request failed: {response.StatusCode}";

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("detail", out var detail))
                return detail.GetString() ?? body;
            if (document.RootElement.TryGetProperty("errors", out var errors))
                return string.Join(" ", errors.EnumerateObject()
                    .SelectMany(p => p.Value.EnumerateArray().Select(v => v.GetString())));
        }
        catch (JsonException)
        {
            // Fall through to the raw body.
        }

        return body;
    }

    private sealed record AccessTokenPayload(
        string TokenType, string AccessToken, int ExpiresIn, string RefreshToken);
}
