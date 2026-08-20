using {{ProjectName}}.Framework;

namespace {{ProjectName}}.ClientShared.Identity;

/// <summary>
/// Access token, refresh token, and expiry, stored through ILocalStorageService
/// (SecureStorage on MAUI — platform Keychain / Android Keystore).
/// </summary>
public sealed class TokenStorage
{
    // Keys are passed explicitly. ILocalStorageService declares
    // [CallerMemberName] defaults, so an omitted key silently stores under the
    // calling method's name.
    private const string AccessTokenKey = "identity_access_token";
    private const string RefreshTokenKey = "identity_refresh_token";
    private const string ExpiresAtKey = "identity_expires_at";

    private readonly ILocalStorageService _storage;

    public TokenStorage(ILocalStorageService storage) => _storage = storage;

    public Task<string?> GetAccessTokenAsync() => _storage.GetValue(AccessTokenKey);

    public Task<string?> GetRefreshTokenAsync() => _storage.GetValue(RefreshTokenKey);

    public async Task SaveAsync(string accessToken, string refreshToken, int expiresInSeconds)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
        await _storage.SetValue(accessToken, AccessTokenKey);
        await _storage.SetValue(refreshToken, RefreshTokenKey);
        await _storage.SetValue(expiresAt.ToUnixTimeSeconds().ToString(), ExpiresAtKey);
    }

    public async Task ClearAsync()
    {
        await _storage.RemoveValue(AccessTokenKey);
        await _storage.RemoveValue(RefreshTokenKey);
        await _storage.RemoveValue(ExpiresAtKey);
    }

    /// <summary>True when the access token expires within two minutes, or is already gone.</summary>
    public async Task<bool> IsExpiringSoonAsync()
    {
        var raw = await _storage.GetValue(ExpiresAtKey);
        if (!long.TryParse(raw, out var unixSeconds)) return true;

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        return expiresAt - DateTimeOffset.UtcNow < TimeSpan.FromMinutes(2);
    }
}
