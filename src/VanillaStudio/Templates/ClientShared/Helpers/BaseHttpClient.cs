using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using {{ProjectName}}.ClientShared.Identity;
using {{ProjectName}}.Framework.Utils;
using {{ProjectName}}.ServiceContracts.Models;

namespace {{ProjectName}}.ClientShared;

public interface BaseHttpClient
{
    Task<T> GetFromJsonAsync<T>(string requestUri);
    Task<T> PostAsJsonAsync<T>(string requestUri, object formBusinessObject);
    Task<T> PutAsJsonAsync<T>(string requestUri, object formBusinessObject);
    Task<T> DeleteAsync<T>(string requestUri);
    public string BaseAddress { get; set; }
}

public class HttpTokenClient : BaseHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenStorage _tokens;
    private readonly IdentityClient _identityClient;
    public string BaseAddress { get; set; } = string.Empty;

    // ILocalStorageService is not injected here: TokenStorage already wraps it, and routing
    // every method through SendWithRefreshAsync means TokenStorage.GetAccessTokenAsync() is
    // the only thing this class needs. Reading raw storage keys here would duplicate the key
    // literals TokenStorage owns (Ruling S) and let the two silently drift apart on a rename.
    public HttpTokenClient(HttpClient client, TokenStorage tokens, IdentityClient identityClient)
    {
        _httpClient = client;
        _tokens = tokens;
        _identityClient = identityClient;
    }

    /// <summary>
    /// Attaches the bearer token, sends, and on a 401 refreshes once and retries.
    /// Without this an expired access token surfaces as a login prompt mid-session.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRefreshAsync(
        Func<HttpRequestMessage> buildRequest)
    {
        _ = _httpClient ?? throw new ArgumentNullException($"{nameof(_httpClient)} is null in {GetType().Name}");

        async Task<HttpResponseMessage> SendOnceAsync()
        {
            var request = buildRequest();
            var authToken = await _tokens.GetAccessTokenAsync();
            if (!string.IsNullOrEmpty(authToken))
            {
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
            }
            return await _httpClient.SendAsync(request);
        }

        var response = await SendOnceAsync();
        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

        if (!await _identityClient.RefreshAsync())
        {
            throw new UnauthorizedAccessException("Please login to continue");
        }

        response.Dispose();
        // Retried once, unconditionally returned: whatever status comes back here (even
        // another 401) is handled by the caller's normal response check below, not by
        // recursing back into SendWithRefreshAsync.
        return await SendOnceAsync();
    }

    public async Task<T> GetFromJsonAsync<T>(string requestUri)
    {
        var response = await SendWithRefreshAsync(() => new HttpRequestMessage(HttpMethod.Get, requestUri));
        var responseString = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var responseObject = JsonSerializer.Deserialize<T>(responseString, JsonSerializerOptions.Web);
            return responseObject ?? throw new Exception("response is null");
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Please login to continue");
        }

        throw new HttpRequestException($"Request failed: {response.StatusCode} - {responseString}");
    }

    public async Task<T> PostAsJsonAsync<T>(string requestUri, object formBusinessObject)
    {
        var response = await SendWithRefreshAsync(() => new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(formBusinessObject)
        });
        var responseString = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            if (typeof(T) == typeof(string))
            {
                return (dynamic)responseString;
            }

            if (typeof(T) == typeof(int))
            {
                return (dynamic)SafeConvert.ToInt32(responseString);
            }

            if (typeof(T) == typeof(bool))
            {
                return (dynamic)Convert.ToBoolean(responseString);
            }

            if (typeof(T) == typeof(Guid))
            {
                return (dynamic)JsonSerializer.Deserialize<Guid>(responseString);
            }

            throw new InvalidCastException($"Conversion for {typeof(T)} is not defined in BaseHttpClient");
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Please login to continue");
        }

        throw new HttpRequestException($"Request failed: {response.StatusCode} - {responseString}");
    }

    public async Task<T> PutAsJsonAsync<T>(string requestUri, object formBusinessObject)
    {
        var response = await SendWithRefreshAsync(() => new HttpRequestMessage(HttpMethod.Put, requestUri)
        {
            Content = JsonContent.Create(formBusinessObject)
        });
        var responseString = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            if (typeof(T) == typeof(string))
            {
                return (dynamic)responseString;
            }

            if (typeof(T) == typeof(int))
            {
                return (dynamic)SafeConvert.ToInt32(responseString);
            }

            if (typeof(T) == typeof(bool))
            {
                return (dynamic)Convert.ToBoolean(responseString);
            }

            if (typeof(T) == typeof(Guid))
            {
                return (dynamic)JsonSerializer.Deserialize<Guid>(responseString);
            }

            throw new InvalidCastException($"Conversion for {typeof(T)} is not defined in BaseHttpClient");
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Please login to continue");
        }

        throw new HttpRequestException($"Request failed: {response.StatusCode} - {responseString}");
    }

    public async Task<T> DeleteAsync<T>(string requestUri)
    {
        var response = await SendWithRefreshAsync(() => new HttpRequestMessage(HttpMethod.Delete, requestUri));
        var responseString = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            if (typeof(T) == typeof(string))
            {
                return (dynamic)responseString;
            }

            if (typeof(T) == typeof(int))
            {
                return (dynamic)SafeConvert.ToInt32(responseString);
            }

            if (typeof(T) == typeof(bool))
            {
                return (dynamic)Convert.ToBoolean(responseString);
            }

            if (typeof(T) == typeof(Guid))
            {
                return (dynamic)JsonSerializer.Deserialize<Guid>(responseString);
            }

            throw new InvalidCastException($"Conversion for {typeof(T)} is not defined in BaseHttpClient");
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Please login to continue");
        }

        throw new HttpRequestException($"Request failed: {response.StatusCode} - {responseString}");
    }
}

public class HttpCookieClient : BaseHttpClient
{
    private readonly HttpClient _httpClient;
    public string BaseAddress { get; set; } = string.Empty;

    public HttpCookieClient(HttpClient client)
    {
        _httpClient = client;
    }

    public async Task<T> GetFromJsonAsync<T>(string requestUri)
    {
        _ = _httpClient ?? throw new ArgumentNullException($"{nameof(_httpClient)} is null in {GetType().Name}");

        var response = await _httpClient.GetAsync(requestUri);
        var responseString = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var responseObject = JsonSerializer.Deserialize<T>(responseString, JsonSerializerOptions.Web);
            return responseObject ?? throw new Exception("response is null");
        }

        throw new HttpRequestException($"Request failed: {response.StatusCode} - {responseString}");
    }

    public async Task<T> PostAsJsonAsync<T>(string requestUri, object formBusinessObject)
    {
        _ = _httpClient ?? throw new ArgumentNullException($"{nameof(_httpClient)} is null in {GetType().Name}");

        var response = await _httpClient.PostAsJsonAsync(requestUri, formBusinessObject);
        var responseString = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            if (typeof(T) == typeof(string))
            {
                return (dynamic)responseString;
            }

            if (typeof(T) == typeof(int))
            {
                return (dynamic)SafeConvert.ToInt32(responseString);
            }

            if (typeof(T) == typeof(bool))
            {
                return (dynamic)Convert.ToBoolean(responseString);
            }

            if (typeof(T) == typeof(Guid))
            {
                return (dynamic)JsonSerializer.Deserialize<Guid>(responseString);
            }

            throw new InvalidCastException($"Conversion for {typeof(T)} is not defined in BaseHttpClient");
        }

        throw new HttpRequestException($"Request failed: {response.StatusCode} - {responseString}");
    }

    public async Task<T> PutAsJsonAsync<T>(string requestUri, object formBusinessObject)
    {
        _ = _httpClient ?? throw new ArgumentNullException($"{nameof(_httpClient)} is null in {GetType().Name}");

        var response = await _httpClient.PutAsJsonAsync(requestUri, formBusinessObject);
        var responseString = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            if (typeof(T) == typeof(string))
            {
                return (dynamic)responseString;
            }

            if (typeof(T) == typeof(int))
            {
                return (dynamic)SafeConvert.ToInt32(responseString);
            }

            if (typeof(T) == typeof(bool))
            {
                return (dynamic)Convert.ToBoolean(responseString);
            }

            if (typeof(T) == typeof(Guid))
            {
                return (dynamic)JsonSerializer.Deserialize<Guid>(responseString);
            }

            throw new InvalidCastException($"Conversion for {typeof(T)} is not defined in BaseHttpClient");
        }

        throw new HttpRequestException($"Request failed: {response.StatusCode} - {responseString}");
    }

    public async Task<T> DeleteAsync<T>(string requestUri)
    {
        _ = _httpClient ?? throw new ArgumentNullException($"{nameof(_httpClient)} is null in {GetType().Name}");

        var response = await _httpClient.DeleteAsync(requestUri);
        var responseString = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            if (typeof(T) == typeof(string))
            {
                return (dynamic)responseString;
            }

            if (typeof(T) == typeof(int))
            {
                return (dynamic)SafeConvert.ToInt32(responseString);
            }

            if (typeof(T) == typeof(bool))
            {
                return (dynamic)Convert.ToBoolean(responseString);
            }

            if (typeof(T) == typeof(Guid))
            {
                return (dynamic)JsonSerializer.Deserialize<Guid>(responseString);
            }

            throw new InvalidCastException($"Conversion for {typeof(T)} is not defined in BaseHttpClient");
        }

        throw new HttpRequestException($"Request failed: {response.StatusCode} - {responseString}");
    }
}
