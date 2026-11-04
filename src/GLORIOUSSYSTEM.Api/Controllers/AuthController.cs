using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GLORIOUSSYSTEM.Api;
using GLORIOUSSYSTEM.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GLORIOUSSYSTEM.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
    static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromMinutes(15);
    readonly HydroponicDbContext _db;
    readonly IHttpClientFactory _httpClientFactory;
    readonly IPasswordResetEmailSender _passwordResetEmailSender;
    readonly string _sessionSigningKey;

    public AuthController(
        HydroponicDbContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IPasswordResetEmailSender passwordResetEmailSender)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _passwordResetEmailSender = passwordResetEmailSender;
        _sessionSigningKey = configuration["Security:SessionSigningKey"]?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_sessionSigningKey))
            _sessionSigningKey = configuration["Security:ApiKey"]?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_sessionSigningKey))
            _sessionSigningKey = "development-only-session-key";
    }

    [HttpPost("password-reset/request")]
    [EnableRateLimiting("password-reset")]
    public async Task<IActionResult> RequestPasswordReset(
        [FromBody] PasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        if (!_passwordResetEmailSender.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Password reset email is not configured on the API.");

        var email = NormalizeEmail(request.Email);
        if (!IsValidEmail(email))
            return Accepted(new { message = "If an active account exists for that email, a reset code has been sent." });

        var user = await _db.Users.SingleOrDefaultAsync(
            candidate => candidate.Email.ToLower() == email && candidate.IsActive == 1,
            cancellationToken);
        if (user == null)
            return Accepted(new { message = "If an active account exists for that email, a reset code has been sent." });

        var code = CreatePasswordResetCode();
        user.PasswordResetTokenHash = HashPasswordResetCode(email, code);
        user.PasswordResetExpiresAt = DateTime.UtcNow.Add(PasswordResetLifetime);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _passwordResetEmailSender.SendCodeAsync(user.Email, user.Name, code, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            user.PasswordResetTokenHash = null;
            user.PasswordResetExpiresAt = null;
            await _db.SaveChangesAsync(cancellationToken);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Password reset email could not be sent.");
        }

        return Accepted(new { message = "If an active account exists for that email, a reset code has been sent." });
    }

    [HttpPost("password-reset/confirm")]
    [EnableRateLimiting("password-reset")]
    public async Task<IActionResult> ConfirmPasswordReset(
        [FromBody] PasswordResetConfirmation request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var code = request.Code?.Trim() ?? string.Empty;
        if (!IsValidEmail(email)
            || code.Length != 6
            || !code.All(char.IsDigit)
            || string.IsNullOrWhiteSpace(request.NewPassword)
            || request.NewPassword.Length < 8)
        {
            return BadRequest("Enter a valid email, six-digit reset code, and password with at least 8 characters.");
        }

        var tokenHash = HashPasswordResetCode(email, code);
        var user = await _db.Users.SingleOrDefaultAsync(
            candidate => candidate.Email.ToLower() == email && candidate.IsActive == 1,
            cancellationToken);
        if (user == null
            || !string.Equals(user.PasswordResetTokenHash, tokenHash, StringComparison.Ordinal)
            || user.PasswordResetExpiresAt is not { } expiresAt
            || expiresAt <= DateTime.UtcNow)
        {
            return Unauthorized("The reset code is invalid or expired.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        user.PasswordResetTokenHash = null;
        user.PasswordResetExpiresAt = null;
        user.RefreshTokenHash = null;
        user.RefreshTokenExpiresAt = null;
        user.SessionVersion++;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("google")]
    [EnableRateLimiting("google-auth")]
    public async Task<IActionResult> ExchangeGoogleToken(
        [FromBody] GoogleTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AccessToken))
            return BadRequest("A Google access token is required.");

        using var userInfoRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "https://openidconnect.googleapis.com/v1/userinfo");
        userInfoRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.AccessToken);

        var client = _httpClientFactory.CreateClient("Google");
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(userInfoRequest, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Google verification is unavailable.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Google verification timed out.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                return Unauthorized("The Google access token was rejected.");

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            JsonDocument userInfo;
            try
            {
                userInfo = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                return Unauthorized("Google returned invalid account details.");
            }

            using (userInfo)
            {
                var root = userInfo.RootElement;
                if (!TryReadString(root, "email", out var email)
                    || !TryReadString(root, "name", out var name)
                    || !root.TryGetProperty("email_verified", out var verified)
                    || verified.ValueKind != JsonValueKind.True)
                {
                    return Unauthorized("Google did not return a verified email address.");
                }

                email = email.Trim().ToLowerInvariant();
                name = name.Trim();
                var refreshToken = CreateRefreshToken();
                var user = await _db.Users.FirstOrDefaultAsync(
                    candidate => candidate.Email.ToLower() == email,
                    cancellationToken);

                if (user == null)
                {
                    user = new User
                    {
                        Name = string.IsNullOrWhiteSpace(name) ? email : name,
                        Email = email,
                        PasswordHash = $"GOOGLE:{Guid.NewGuid():N}",
                        IsActive = 1,
                        CreatedAt = DateTime.UtcNow,
                        Role = "User"
                    };
                    _db.Users.Add(user);
                }
                else if (user.IsActive != 1)
                {
                    return Unauthorized("This account is disabled.");
                }

                user.Name = string.IsNullOrWhiteSpace(name) ? user.Name : name;
                user.LastLoginAt = DateTime.UtcNow;
                user.RefreshTokenHash = HashRefreshToken(refreshToken);
                user.RefreshTokenExpiresAt = DateTime.UtcNow.Add(RefreshTokenLifetime);
                await _db.SaveChangesAsync(cancellationToken);

                var expiresAt = DateTimeOffset.UtcNow.Add(SessionLifetime);
                var sessionToken = SessionTokenService.Issue(
                    user.Id,
                    user.Email,
                    user.Name,
                    user.Role,
                    _sessionSigningKey,
                    SessionLifetime,
                    sessionVersion: user.SessionVersion);

                return Ok(new
                {
                    accessToken = sessionToken,
                    expiresAt,
                    refreshToken,
                    user = new { user.Id, user.Name, user.Email, user.Role }
                });
            }
        }
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("google-auth")]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest("A refresh token is required.");

        var tokenHash = HashRefreshToken(request.RefreshToken);
        var user = await _db.Users.SingleOrDefaultAsync(
            candidate => candidate.RefreshTokenHash == tokenHash,
            cancellationToken);
        if (user == null || user.IsActive != 1
            || user.RefreshTokenExpiresAt is not { } expiresAt
            || expiresAt <= DateTime.UtcNow)
        {
            return Unauthorized("The refresh token is invalid or expired.");
        }

        var newRefreshToken = CreateRefreshToken();
        user.RefreshTokenHash = HashRefreshToken(newRefreshToken);
        user.RefreshTokenExpiresAt = DateTime.UtcNow.Add(RefreshTokenLifetime);
        await _db.SaveChangesAsync(cancellationToken);

        var expiresAtUtc = DateTimeOffset.UtcNow.Add(SessionLifetime);
        var sessionToken = SessionTokenService.Issue(
            user.Id,
            user.Email,
            user.Name,
            user.Role,
            _sessionSigningKey,
            SessionLifetime,
            sessionVersion: user.SessionVersion);

        return Ok(new
        {
            accessToken = sessionToken,
            expiresAt = expiresAtUtc,
            refreshToken = newRefreshToken,
            user = new { user.Id, user.Name, user.Email, user.Role }
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest? request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.RefreshToken))
            return Unauthorized("A valid refresh token is required.");

        var tokenHash = HashRefreshToken(request.RefreshToken);
        var user = await _db.Users.SingleOrDefaultAsync(
            candidate => candidate.RefreshTokenHash == tokenHash,
            cancellationToken);
        if (user == null || user.IsActive != 1)
            return Unauthorized("The refresh token is invalid.");

        user.SessionVersion++;
        user.RefreshTokenHash = null;
        user.RefreshTokenExpiresAt = null;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    static string CreateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    static string CreatePasswordResetCode()
        => RandomNumberGenerator.GetInt32(100000, 1000000).ToString(System.Globalization.CultureInfo.InvariantCulture);

    static string HashPasswordResetCode(string email, string code)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{email}\n{code}")));

    static string HashRefreshToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    static string NormalizeEmail(string? email) => email?.Trim().ToLowerInvariant() ?? string.Empty;

    static bool IsValidEmail(string email)
    {
        try
        {
            var address = new System.Net.Mail.MailAddress(email);
            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    static bool TryReadString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    public sealed record GoogleTokenRequest(string AccessToken);
    public sealed record RefreshRequest(string RefreshToken);
    public sealed record LogoutRequest(string RefreshToken);
    public sealed record PasswordResetRequest(string? Email);
    public sealed record PasswordResetConfirmation(string? Email, string? Code, string? NewPassword);
}
