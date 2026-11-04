using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Authentication;
using Microsoft.Maui.Storage;

namespace GLORIOUSSYSTEM.App;

public sealed record GoogleAccount(string Subject, string Email, string Name, string AccessToken);

public sealed class GoogleAuthException : Exception
{
    public GoogleAuthException(string message) : base(message)
    {
    }
}

public sealed class GoogleAuthService
{
    const string DefaultRedirectUri = "glorioussystem://oauth2redirect";
    static readonly HttpClient Http = new();

    public async Task<GoogleAccount> SignInAsync()
    {
        var options = await LoadOptionsAsync();
        var clientId = OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(options.WindowsClientId)
            ? options.WindowsClientId
            : options.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
            throw new GoogleAuthException("Google sign-in is not configured yet. Add the Google client ID in Settings → Connection, then try again.");

        if (!Uri.TryCreate(options.RedirectUri, UriKind.Absolute, out var configuredRedirectUri))
            throw new GoogleAuthException("Google sign-in has an invalid redirect URI configuration.");

        var state = CreateVerifier();
        var verifier = CreateVerifier();
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var (authorizationCode, redirectUri) = await AcquireAuthorizationCodeAsync(
            clientId,
            configuredRedirectUri,
            state,
            challenge);

        using var tokenResponse = await Http.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["code"] = authorizationCode,
                ["code_verifier"] = verifier,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri.ToString()
            }));

        using var tokenDocument = await ReadJsonAsync(tokenResponse, "Google did not issue an access token.");
        if (!tokenDocument.RootElement.TryGetProperty("access_token", out var accessTokenElement))
            throw new GoogleAuthException("Google did not issue an access token.");

        var accessToken = accessTokenElement.GetString();
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new GoogleAuthException("Google returned an empty access token.");

        using var userInfoRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "https://openidconnect.googleapis.com/v1/userinfo");
        userInfoRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var userInfoResponse = await Http.SendAsync(userInfoRequest);
        using var userInfoDocument = await ReadJsonAsync(userInfoResponse, "Google did not return your account details.");
        var root = userInfoDocument.RootElement;
        var subject = ReadRequiredString(root, "sub");
        var email = ReadRequiredString(root, "email").Trim().ToLowerInvariant();
        var name = root.TryGetProperty("name", out var nameElement)
            ? nameElement.GetString()?.Trim()
            : null;

        if (root.TryGetProperty("email_verified", out var verifiedElement)
            && verifiedElement.ValueKind == JsonValueKind.False)
        {
            throw new GoogleAuthException("Google returned an unverified email address.");
        }

        return new GoogleAccount(subject, email, string.IsNullOrWhiteSpace(name) ? email : name, accessToken);
    }

    async Task<(string Code, Uri RedirectUri)> AcquireAuthorizationCodeAsync(
        string clientId,
        Uri configuredRedirectUri,
        string state,
        string challenge)
    {
        if (OperatingSystem.IsWindows())
            return await AuthenticateWithLoopbackAsync(clientId, state, challenge);

        var authUri = await BuildAuthorizeUriAsync(clientId, configuredRedirectUri, state, challenge);
        var authResult = await WebAuthenticator.Default.AuthenticateAsync(authUri, configuredRedirectUri);
        if (!authResult.Properties.TryGetValue("state", out var returnedState)
            || !string.Equals(returnedState, state, StringComparison.Ordinal))
        {
            throw new GoogleAuthException("Google sign-in could not verify the browser response.");
        }

        if (!authResult.Properties.TryGetValue("code", out var code)
            || string.IsNullOrWhiteSpace(code))
        {
            throw new GoogleAuthException("Google sign-in did not return an authorization code.");
        }

        return (code, configuredRedirectUri);
    }

    static async Task<(string Code, Uri RedirectUri)> AuthenticateWithLoopbackAsync(
        string clientId,
        string state,
        string challenge)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var redirectUri = new Uri($"http://127.0.0.1:{endpoint.Port}/oauth2redirect/");
        var authUri = await BuildAuthorizeUriAsync(clientId, redirectUri, state, challenge);

        if (!await Launcher.Default.OpenAsync(authUri))
            throw new GoogleAuthException("Could not open the browser for Google sign-in.");

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var client = await listener.AcceptTcpClientAsync(timeout.Token);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(timeout.Token);
        while (!string.IsNullOrEmpty(await reader.ReadLineAsync(timeout.Token)))
        {
        }

        if (string.IsNullOrWhiteSpace(requestLine))
            throw new GoogleAuthException("Google returned an empty browser response.");

        var requestParts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (requestParts.Length < 2)
            throw new GoogleAuthException("Google returned an invalid browser response.");

        var callbackUri = new Uri(new Uri($"http://127.0.0.1:{endpoint.Port}"), requestParts[1]);
        var callbackValues = ParseQuery(callbackUri.Query);
        var html = "<html><body>You can close this window and return to GLORIOUS SYSTEM.</body></html>";
        var body = Encoding.UTF8.GetBytes(html);
        var response = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(response, timeout.Token);
        await stream.WriteAsync(body, timeout.Token);

        if (callbackValues.TryGetValue("error", out var error))
            throw new GoogleAuthException($"Google sign-in was not completed: {error}.");
        if (!callbackValues.TryGetValue("state", out var returnedState)
            || !string.Equals(returnedState, state, StringComparison.Ordinal))
        {
            throw new GoogleAuthException("Google sign-in could not verify the browser response.");
        }
        if (!callbackValues.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            throw new GoogleAuthException("Google sign-in did not return an authorization code.");

        return (code, redirectUri);
    }

    static async Task<Uri> BuildAuthorizeUriAsync(
        string clientId,
        Uri redirectUri,
        string state,
        string challenge)
    {
        var authorizeQuery = await new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri.ToString(),
            ["response_type"] = "code",
            ["scope"] = "openid profile email",
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        }).ReadAsStringAsync();
        return new Uri($"https://accounts.google.com/o/oauth2/v2/auth?{authorizeQuery}");
    }

    static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = separator >= 0 ? pair[..separator] : pair;
            var value = separator >= 0 ? pair[(separator + 1)..] : string.Empty;
            values[Uri.UnescapeDataString(key.Replace('+', ' '))] = Uri.UnescapeDataString(value.Replace('+', ' '));
        }

        return values;
    }

    static async Task<(string ClientId, string WindowsClientId, string RedirectUri)> LoadOptionsAsync()
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync("appsettings.json");
        using var document = await JsonDocument.ParseAsync(stream);

        if (!document.RootElement.TryGetProperty("Authentication", out var authentication))
            return (string.Empty, string.Empty, DefaultRedirectUri);

        var clientId = authentication.TryGetProperty("GoogleClientId", out var clientIdElement)
            ? clientIdElement.GetString() ?? string.Empty
            : string.Empty;
        var windowsClientId = authentication.TryGetProperty("GoogleWindowsClientId", out var windowsClientIdElement)
            ? windowsClientIdElement.GetString() ?? string.Empty
            : string.Empty;
        var redirectUri = authentication.TryGetProperty("GoogleRedirectUri", out var redirectElement)
            ? redirectElement.GetString() ?? DefaultRedirectUri
            : DefaultRedirectUri;

        // Client IDs are public OAuth identifiers, so allow a local device override
        // without putting deployment-specific values or secrets in source control.
        clientId = Preferences.Get("Google_ClientId", clientId);
        windowsClientId = Preferences.Get("Google_WindowsClientId", windowsClientId);
        redirectUri = Preferences.Get("Google_RedirectUri", redirectUri);

        return (clientId.Trim(), windowsClientId.Trim(), redirectUri.Trim());
    }

    static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, string fallbackMessage)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new GoogleAuthException(fallbackMessage);

        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw new GoogleAuthException(fallbackMessage);
        }
    }

    static string ReadRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property)
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new GoogleAuthException("Google returned incomplete account details.");
        }

        return property.GetString()!;
    }

    static string CreateVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

    static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}
