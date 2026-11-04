# Windows API Auto-Start Design

## Intent

When the Windows MAUI app opens in the local development setup, it should start the companion ASP.NET API automatically so the dashboard and reports do not require a second terminal. Android keeps using its existing configured API address.

## Scope and constraints

- Start only on Windows; never attempt to launch a desktop process on Android, iOS, or macOS.
- Reuse the existing API project and `http://127.0.0.1:5053` health endpoint.
- If the API is already healthy, do not create a duplicate process.
- If the API project or `dotnet` host is unavailable, leave the app usable and log the failure.
- Stop only the API process started by this app when the MAUI window is destroyed.
- Do not change authentication, database schema, or page UI.

## Approach

The app will use a small `LocalApiProcessManager` service. It resolves the configured API project path, starts `dotnet run` with the Development environment and local URL, polls `/health` until the API is ready, and owns the child process lifetime. `App.CreateWindow` wires window destruction to the manager's stop method. The manager is a no-op on non-Windows targets.

## Success criteria

- Opening the Windows app starts the API when it is not already running.
- Opening the app while the API is already running does not start a second copy.
- Closing the Windows app stops only the API process it started.
- Windows and Android MAUI builds pass.
