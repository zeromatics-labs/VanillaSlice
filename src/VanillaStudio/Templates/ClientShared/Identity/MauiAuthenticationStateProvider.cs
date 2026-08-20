using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace {{ProjectName}}.ClientShared.Identity;

/// <summary>
/// Supplies AuthenticationState on MAUI hosts from tokens held in SecureStorage.
/// Claims are hydrated from GET /identity/manage/info, because Identity's tokens
/// are opaque and carry no readable claims of their own.
/// </summary>
public sealed class MauiAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly IdentityClient _identityClient;
    private readonly TokenStorage _tokens;

    public MauiAuthenticationStateProvider(IdentityClient identityClient, TokenStorage tokens)
    {
        _identityClient = identityClient;
        _tokens = tokens;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var accessToken = await _tokens.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(accessToken)) return Anonymous;

        if (await _tokens.IsExpiringSoonAsync() && !await _identityClient.RefreshAsync())
        {
            return Anonymous;
        }

        var info = await _identityClient.GetInfoAsync();
        if (info is null) return Anonymous;

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, info.Email), new Claim(ClaimTypes.Email, info.Email)],
            authenticationType: "Identity.Bearer");

        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public async Task<IdentityResultDto> LogInAsync(string email, string password)
    {
        var result = await _identityClient.LoginAsync(email, password);
        if (result.Succeeded)
        {
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
        return result;
    }

    public Task<IdentityResultDto> RegisterAsync(string email, string password) =>
        _identityClient.RegisterAsync(email, password);

    public async Task LogOutAsync()
    {
        await _identityClient.LogoutAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }
}
