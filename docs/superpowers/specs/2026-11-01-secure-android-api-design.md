# Secure Android API Connectivity Design

## Intent

Make physical Android connectivity explicit and safe without changing the current Windows loopback workflow. A LAN API must be started intentionally with an API key, and production deployments must be able to require HTTPS.

## Approaches

1. **Automatic LAN binding from the Windows app** — rejected because it could expose the development database to the local network without a user choosing a key or firewall policy.
2. **HTTPS-only local development with the .NET development certificate** — useful on Windows, but Android does not automatically trust the Windows development certificate.
3. **Explicit security policy plus documented launch modes (selected)** — add `Security:RequireApiKey` and `Security:RequireHttps` configuration, fail fast when a required API key is missing, keep loopback development defaults, and provide a physical-phone setup guide. This gives a secure production path and a controlled LAN development path without silently exposing the service.

## Goals

- Require an API key whenever a deployment opts into API-key protection.
- Require HTTPS whenever a deployment opts into HTTPS enforcement.
- Keep the existing Windows loopback auto-start behavior unchanged.
- Make the Android Settings screen explain when HTTP is development-only and when an API key is required.
- Document the exact Windows CMD commands for LAN development and HTTPS deployment.

## Non-goals

- Automatically opening firewall ports.
- Trusting arbitrary self-signed certificates in the Android app.
- Storing API keys in source control or in packaged app settings.
- Changing the existing authentication/session protocol.

## Files

- Modify `src/GLORIOUSSYSTEM.Api/Program.cs` to enforce the explicit API-key policy.
- Modify `src/GLORIOUSSYSTEM.Api/appsettings.json` and `appsettings.Development.json` with safe production/development defaults.
- Modify `src/GLORIOUSSYSTEM.App/SettingsPage.xaml` to clarify secure LAN configuration.
- Create `docs/superpowers/plans/2026-11-01-secure-android-api.md` with verification steps.

## Success criteria

- Production configuration fails before serving requests when `RequireApiKey=true` and no key is supplied.
- API builds successfully.
- Windows and Android MAUI builds remain warning-free.
- The app does not auto-bind the API to a LAN address.
