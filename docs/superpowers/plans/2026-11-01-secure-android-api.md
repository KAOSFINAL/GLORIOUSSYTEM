# Secure Android API Connectivity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make physical Android API connectivity explicit, API-key protected, and HTTPS-configurable without exposing the development API automatically.

**Architecture:** The API reads `Security:RequireApiKey` and `Security:RequireHttps`. Startup fails when API-key protection is enabled without a key; existing middleware continues to accept the key or a valid session token. Development remains loopback/HTTP by default, while LAN and HTTPS are opt-in through environment variables and documented CMD commands.

**Tech Stack:** ASP.NET Core .NET 10, .NET MAUI XAML, JSON configuration, SQLite-backed API.

**Spec:** `docs/superpowers/specs/2026-11-01-secure-android-api-design.md`

## Global Constraints

- Do not auto-bind the API to a LAN address.
- Do not store API keys in source control or packaged app settings.
- Do not trust arbitrary self-signed certificates in Android.
- Preserve the Windows loopback auto-start workflow.

## Tasks

### Task 1: Add explicit API security policy

**Files:**
- Modify `src/GLORIOUSSYSTEM.Api/Program.cs`
- Modify `src/GLORIOUSSYSTEM.Api/appsettings.json`
- Modify `src/GLORIOUSSYSTEM.Api/appsettings.Development.json`

- [x] Add a failing source/runtime check for `Security:RequireApiKey` startup validation.
- [x] Read `RequireApiKey` with a production-safe default and fail fast when it is enabled without `Security:ApiKey`.
- [x] Keep `RequireHttps` explicit and preserve the existing HTTPS middleware.
- [x] Verify API key/session middleware behavior with a development API run.

### Task 2: Clarify Android secure setup

**Files:**
- Modify `src/GLORIOUSSYSTEM.App/SettingsPage.xaml`

- [x] Add concise copy explaining that LAN HTTP is development-only, HTTPS is required for deployment, and the API key must match the server.
- [x] Keep the existing URL, key, and connection-test controls unchanged.

### Task 3: Verify all targets

**Files:**
- No production files.

- [x] Run API source checks and `git diff --check`.
- [x] Build API, Windows MAUI, Android MAUI, and ConsoleTest.
- [x] Report the exact LAN development and HTTPS deployment commands.
