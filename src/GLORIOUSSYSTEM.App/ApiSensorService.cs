using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.Maui.Storage;

namespace GLORIOUSSYSTEM.App;

public class ApiSensorReading
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Model { get; set; }
    public double? MinThreshold { get; set; }
    public double? MaxThreshold { get; set; }
    public double? LatestValue { get; set; }
    public string? LatestMetric { get; set; }
    public DateTime? LatestTimestamp { get; set; }
}

public sealed class ApiSessionResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
}

public class ApiSensorService
{
    public const string ApiKeyStorageKey = "Api_ApiKey";
    public const string SessionTokenStorageKey = "Api_SessionToken";
    public const string RefreshTokenStorageKey = "Api_RefreshToken";

    readonly HttpClient _http;

    public ApiSensorService(string? baseUrl = null)
    {
        var platformDefault = OperatingSystem.IsAndroid()
            ? "http://10.0.2.2:5053/"
            : "http://localhost:5053/";
        baseUrl ??= Preferences.Get("Api_BaseUrl", platformDefault);
        if (!baseUrl.EndsWith('/'))
            baseUrl += "/";

        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(8)
        };
    }

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("health", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/password-reset/request")
        {
            Content = JsonContent.Create(new { email })
        };
        AddApiKey(request, await SecureStorage.Default.GetAsync(ApiKeyStorageKey));

        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return;

        throw new InvalidOperationException(await ReadErrorAsync(
            response,
            "The password reset request could not be sent."));
    }

    public async Task ConfirmPasswordResetAsync(
        string email,
        string code,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/password-reset/confirm")
        {
            Content = JsonContent.Create(new { email, code, newPassword })
        };
        AddApiKey(request, await SecureStorage.Default.GetAsync(ApiKeyStorageKey));

        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return;

        throw new InvalidOperationException(await ReadErrorAsync(
            response,
            "The reset code could not be verified."));
    }

    static void AddApiKey(HttpRequestMessage request, string? apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.Add("X-Api-Key", apiKey);
    }

    static async Task<string> ReadErrorAsync(HttpResponseMessage response, string fallback)
    {
        var body = (await response.Content.ReadAsStringAsync()).Trim();
        if (string.IsNullOrWhiteSpace(body))
            return fallback;

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("message", out var message)
                && message.ValueKind == System.Text.Json.JsonValueKind.String
                && !string.IsNullOrWhiteSpace(message.GetString()))
            {
                return message.GetString()!;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // The API also returns plain-text errors; use that body below.
        }

        return body.Length > 240 ? body[..240] : body;
    }

    public async Task<List<ApiSensorReading>> GetLatestAsync()
    {
        var response = await SendLatestAsync();
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            if (await TryRefreshSessionAsync())
                response = await SendLatestAsync();
        }

        using (response)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                SecureStorage.Default.Remove(SessionTokenStorageKey);
                SecureStorage.Default.Remove(RefreshTokenStorageKey);
            }
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<List<ApiSensorReading>>();
            return result ?? new List<ApiSensorReading>();
        }
    }

    async Task<HttpResponseMessage> SendLatestAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/readings/latest");
        var apiKey = await SecureStorage.Default.GetAsync(ApiKeyStorageKey);
        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.Add("X-Api-Key", apiKey);
        var sessionToken = await SecureStorage.Default.GetAsync(SessionTokenStorageKey);
        if (!string.IsNullOrWhiteSpace(sessionToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

        return await _http.SendAsync(request);
    }

    public async Task<string> ExchangeGoogleTokenAsync(string googleAccessToken)
    {
        if (string.IsNullOrWhiteSpace(googleAccessToken))
            throw new InvalidOperationException("Google did not return an access token.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/google")
        {
            Content = JsonContent.Create(new { accessToken = googleAccessToken })
        };
        var apiKey = await SecureStorage.Default.GetAsync(ApiKeyStorageKey);
        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.Add("X-Api-Key", apiKey);

        using var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiSessionResponse>();
        if (result == null || string.IsNullOrWhiteSpace(result.AccessToken))
            throw new InvalidOperationException("The API did not return a session token.");

        await StoreSessionAsync(result);
        return result.AccessToken;
    }

    public async Task<bool> TryRefreshSessionAsync()
    {
        var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenStorageKey);
        if (string.IsNullOrWhiteSpace(refreshToken))
            return false;

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/refresh")
        {
            Content = JsonContent.Create(new { refreshToken })
        };
        var apiKey = await SecureStorage.Default.GetAsync(ApiKeyStorageKey);
        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.Add("X-Api-Key", apiKey);

        try
        {
            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content.ReadFromJsonAsync<ApiSessionResponse>();
            if (result == null || string.IsNullOrWhiteSpace(result.AccessToken)
                || string.IsNullOrWhiteSpace(result.RefreshToken))
                return false;

            await StoreSessionAsync(result);
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    async Task StoreSessionAsync(ApiSessionResponse result)
    {
        await SecureStorage.Default.SetAsync(SessionTokenStorageKey, result.AccessToken);
        if (!string.IsNullOrWhiteSpace(result.RefreshToken))
            await SecureStorage.Default.SetAsync(RefreshTokenStorageKey, result.RefreshToken);
    }

    public async Task LogoutAsync(string? refreshToken = null)
    {
        refreshToken ??= await SecureStorage.Default.GetAsync(RefreshTokenStorageKey);
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout");
        request.Content = JsonContent.Create(new { refreshToken });
        var apiKey = await SecureStorage.Default.GetAsync(ApiKeyStorageKey);
        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.Add("X-Api-Key", apiKey);

        try
        {
            using var response = await _http.SendAsync(request);
        }
        catch (HttpRequestException)
        {
        }
        catch (TaskCanceledException)
        {
        }
    }
}
