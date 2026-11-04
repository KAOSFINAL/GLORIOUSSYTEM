# Windows API Auto-Start Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Start the local ASP.NET API automatically from the Windows MAUI app and clean it up when the app closes.

**Architecture:** Add a small process-lifetime service in the MAUI app. It is a no-op on non-Windows platforms, checks the local health endpoint before launching, starts the existing API project only when needed, waits for readiness, and stops only its own child process on window destruction.

**Tech Stack:** .NET 10 MAUI, `System.Diagnostics.Process`, `HttpClient`, existing ASP.NET `/health` endpoint.

**Spec:** `docs/superpowers/specs/2026-11-01-windows-api-autostart-design.md`

## Global Constraints

- Start only on Windows.
- Use `http://127.0.0.1:5053` and the existing `/health` endpoint.
- Do not duplicate an already-running API.
- Fail gracefully when the project or `dotnet` host is unavailable.
- Stop only a process created by this app.
- Do not change authentication, database schema, or page UI.

## Review Focus

- API already running: health check prevents a duplicate child process.
- API project missing or `dotnet` unavailable: startup failure is logged without preventing the app from opening.
- API takes time to boot: readiness polling completes before API-dependent calls are expected.
- App closes while API is starting: process cleanup does not leave a child process behind.
- Android startup: manager remains a no-op and existing API URL behavior is unchanged.

---

### Task 1: Add the local API process manager

**Files:**
- Create: `src/GLORIOUSSYSTEM.App/LocalApiProcessManager.cs`

**Interfaces:**
- Produces: `StartAsync(CancellationToken)`, `Stop()`, and `Dispose()` for `App`.

- [ ] Implement a Windows-gated manager that resolves `LocalApi:ProjectPath` from packaged app settings, falls back to the repository API project path relative to the app/current directory, checks `/health`, starts `dotnet run --no-launch-profile`, polls readiness for up to 10 seconds, and stops only its owned process.
- [ ] Catch process, HTTP, and cancellation failures; write diagnostic messages with `Debug.WriteLine` and allow the MAUI app to continue.

### Task 2: Wire startup and shutdown in `App`

**Files:**
- Modify: `src/GLORIOUSSYSTEM.App/App.xaml.cs`
- Modify: `src/GLORIOUSSYSTEM.App/appsettings.json`

**Interfaces:**
- Consumes: `LocalApiProcessManager` from Task 1.

- [ ] Create one manager during app construction and start it without blocking UI creation.
- [ ] Attach window destruction to `Stop()` and `Dispose()`.
- [ ] Add the Windows development API project path and URL to the app configuration without changing Android defaults.

### Task 3: Verify the integration

**Files:**
- Test: source assertions plus Windows and Android builds.

- [ ] Assert the manager is referenced from app startup and shutdown and has a non-Windows early return.
- [ ] Build API, Windows MAUI, and Android MAUI targets with `--no-restore`.
- [ ] Run the API health/startup smoke check and `git diff --check`.
