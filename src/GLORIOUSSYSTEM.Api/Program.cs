using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using GLORIOUSSYSTEM.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using GLORIOUSSYSTEM.Data.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddSingleton<IPasswordResetEmailSender, GmailPasswordResetEmailSender>();
builder.Services.AddHttpClient("Google", client =>
{
    client.BaseAddress = new Uri("https://openidconnect.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("google-auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
             {
                 PermitLimit = 10,
                 Window = TimeSpan.FromMinutes(1),
                 QueueLimit = 0
             }));
    options.AddPolicy("password-reset", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var databasePath = HydroponicDatabasePath.Resolve(
    builder.Configuration.GetConnectionString("HydroponicDb"),
    builder.Environment.ContentRootPath,
    AppContext.BaseDirectory,
    Directory.GetCurrentDirectory());
builder.Services.AddDbContext<GLORIOUSSYSTEM.Data.Models.HydroponicDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

var apiKey = builder.Configuration["Security:ApiKey"]?.Trim();
var sessionSigningKey = builder.Configuration["Security:SessionSigningKey"]?.Trim();
if (string.IsNullOrWhiteSpace(sessionSigningKey))
    sessionSigningKey = apiKey;
if (string.IsNullOrWhiteSpace(sessionSigningKey))
    sessionSigningKey = "development-only-session-key";
var requireApiKey = builder.Configuration.GetValue("Security:RequireApiKey", !builder.Environment.IsDevelopment());
var requireHttps = builder.Configuration.GetValue("Security:RequireHttps", !builder.Environment.IsDevelopment());
if (requireApiKey && string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("Security:ApiKey must be configured when Security:RequireApiKey is enabled.");
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HydroponicDbContext>();
    db.Database.Migrate();
    SensorHardwareCatalog.Reconcile(db);
    db.SaveChanges();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (requireHttps)
{
    app.UseHttpsRedirection();
    app.Use(async (context, next) =>
    {
        if (!context.Request.IsHttps)
        {
            context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            await context.Response.WriteAsync("HTTPS is required.");
            return;
        }

        await next();
    });
}

app.UseRateLimiter();

if (requireApiKey || !string.IsNullOrWhiteSpace(apiKey))
{
    var configuredApiKey = apiKey;
    if (string.IsNullOrWhiteSpace(configuredApiKey))
        throw new InvalidOperationException("Security:ApiKey must be configured when API protection is enabled.");

    app.Use(async (context, next) =>
    {
        var isApiPath = context.Request.Path.StartsWithSegments("/api");
        var isAuthBootstrap = context.Request.Path.StartsWithSegments("/api/auth/google")
            || context.Request.Path.StartsWithSegments("/api/auth/refresh")
            || context.Request.Path.StartsWithSegments("/api/auth/logout")
            || context.Request.Path.StartsWithSegments("/api/auth/password-reset");
        if (isApiPath && !isAuthBootstrap)
        {
            var providedKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
            var hasValidApiKey = ApiKeysMatch(providedKey, configuredApiKey);
            var hasValidSession = false;
            if (context.Request.Headers.Authorization.ToString() is { Length: > 7 } authorization
                && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                hasValidSession = SessionTokenService.TryValidate(
                    authorization[7..].Trim(),
                    sessionSigningKey,
                    out var identity);
                if (hasValidSession && identity != null)
                {
                    var db = context.RequestServices.GetRequiredService<HydroponicDbContext>();
                    var user = await db.Users
                        .AsNoTracking()
                        .SingleOrDefaultAsync(candidate => candidate.Id == identity.UserId, context.RequestAborted);
                    if (user?.IsActive == 1 && user.SessionVersion == identity.SessionVersion)
                    {
                        context.Items[nameof(SessionIdentity)] = new SessionIdentity(
                            user.Id,
                            user.Email,
                            user.Name,
                            string.IsNullOrWhiteSpace(user.Role) ? "User" : user.Role,
                            user.SessionVersion);
                    }
                    else
                    {
                        hasValidSession = false;
                    }
                }
            }

            if (!hasValidApiKey && !hasValidSession)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("A valid API key or session token is required.");
                return;
            }
        }

        await next();
    });
}

app.MapGet("/health", async (HydroponicDbContext db, CancellationToken cancellationToken) =>
{
    var databaseReachable = await db.Database.CanConnectAsync(cancellationToken);
    return databaseReachable
        ? Results.Ok(new { status = "Healthy", database = "Reachable", utc = DateTime.UtcNow })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

app.MapControllers();

app.Run();

static bool ApiKeysMatch(string? providedKey, string expectedKey)
{
    if (string.IsNullOrWhiteSpace(providedKey))
        return false;

    var providedBytes = Encoding.UTF8.GetBytes(providedKey);
    var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
    return providedBytes.Length == expectedBytes.Length
        && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
}
