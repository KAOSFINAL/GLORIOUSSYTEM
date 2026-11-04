# Secure Android API setup

The app keeps Windows auto-start on loopback. A physical Android phone cannot start a Windows process, so the API must be started intentionally on the computer and the phone must be pointed at that address.

## Trusted-LAN development

This is for local development only. It uses HTTP, but protects API data endpoints with an API key.

Open a new Windows CMD window and run:

```cmd
cd C:\Dev\GLORIOUSSYSTEM
for /f "delims=" %A in ('powershell -NoProfile -Command "[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))"') do set "GLORIOUS_API_KEY=%A"
set "Security__ApiKey=%GLORIOUS_API_KEY%"
set "Security__RequireApiKey=true"
set "Security__RequireHttps=false"
dotnet run --project src\GLORIOUSSYSTEM.Api\GLORIOUSSYSTEM.Api.csproj --no-launch-profile --urls http://0.0.0.0:5053
```

In the Android app:

1. Find the computer's IPv4 address with `ipconfig`.
2. Open `Settings` → `Connection`.
3. Enter `http://<computer-ip>:5053/` as the API address.
4. Paste the value of `%GLORIOUS_API_KEY%` into `Sensor API key`.
5. Save the connection and press `Test connection`.

Keep the phone and computer on a trusted private network. Do not port-forward this HTTP listener.

## HTTPS deployment

For a physical phone outside a trusted development network, use a real HTTPS certificate trusted by Android (for example, a deployed domain or a reverse proxy with a public certificate). Start the API with:

```cmd
cd C:\Dev\GLORIOUSSYSTEM
set "Security__ApiKey=<long-random-api-key>"
set "Security__RequireApiKey=true"
set "Security__RequireHttps=true"
dotnet run --project src\GLORIOUSSYSTEM.Api\GLORIOUSSYSTEM.Api.csproj --no-launch-profile --urls https://0.0.0.0:7049
```

The Android API address must use `https://`, and the same API key must be entered in Settings. The .NET development certificate trusted by Windows is not automatically trusted by Android; do not bypass certificate validation in the app.

## Configuration behavior

- `appsettings.Development.json` keeps local loopback development convenient.
- `appsettings.json` requires both an API key and HTTPS by default.
- When `Security:RequireApiKey=true` and no key is supplied, the API fails at startup instead of serving unprotected data.
