using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GLORIOUSSYSTEM.Api;

public sealed record SessionIdentity(int UserId, string Email, string Name, string Role, int SessionVersion);

public static class SessionTokenService
{
    public static string Issue(
        int userId,
        string email,
        string name,
        string? role,
        string signingKey,
        TimeSpan lifetime,
        DateTimeOffset? now = null,
        int sessionVersion = 1)
    {
        var issuedAt = now ?? DateTimeOffset.UtcNow;
        var payload = JsonSerializer.SerializeToUtf8Bytes(new SessionPayload(
            userId,
            email,
            name,
            string.IsNullOrWhiteSpace(role) ? "User" : role,
            sessionVersion,
            issuedAt.ToUnixTimeSeconds(),
            issuedAt.Add(lifetime).ToUnixTimeSeconds()));
        var encodedPayload = Base64UrlEncode(payload);
        var signature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(signingKey),
            Encoding.UTF8.GetBytes(encodedPayload));

        return $"{encodedPayload}.{Base64UrlEncode(signature)}";
    }

    public static bool TryValidate(
        string? token,
        string signingKey,
        out SessionIdentity? identity,
        DateTimeOffset? now = null)
    {
        identity = null;
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(signingKey))
            return false;

        var segments = token.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2)
            return false;

        byte[] payloadBytes;
        byte[] providedSignature;
        try
        {
            payloadBytes = Base64UrlDecode(segments[0]);
            providedSignature = Base64UrlDecode(segments[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        var expectedSignature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(signingKey),
            Encoding.UTF8.GetBytes(segments[0]));
        if (providedSignature.Length != expectedSignature.Length
            || !CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature))
        {
            return false;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<SessionPayload>(payloadBytes);
            var currentTime = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
            if (payload == null || payload.UserId <= 0 || payload.SessionVersion <= 0
                || payload.ExpiresAt <= currentTime
                || string.IsNullOrWhiteSpace(payload.Email))
            {
                return false;
            }

            identity = new SessionIdentity(
                payload.UserId,
                payload.Email,
                payload.Name,
                string.IsNullOrWhiteSpace(payload.Role) ? "User" : payload.Role,
                payload.SessionVersion);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }

    sealed record SessionPayload(
        int UserId,
        string Email,
        string Name,
        string Role,
        int SessionVersion,
        long IssuedAt,
        long ExpiresAt);
}
