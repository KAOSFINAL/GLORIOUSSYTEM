# Project file map

This document explains what the major files contribute to the running system. It is intentionally organized by responsibility so a new developer can start at the solution entry points and follow the data flow.

## End-to-end data flow

1. The MAUI App starts and applies the centralized theme.
2. App.xaml.cs configures dependency injection and opens the correct SQLite database. Windows points at the configured development database; Android stages a private writable package copy.
3. HydroponicDbContext opens the shared EF Core model. Migrations and SensorHardwareCatalog reconcile the schema and physical sensor catalog.
4. MainPage reads local sensor records for the dashboard. ReportsPage aggregates local readings and draws the reporting chart.
5. ApiSensorService calls the ASP.NET API when live/remote telemetry or API-backed authentication is required.
6. The API validates API keys/session tokens, reads the same domain model, and returns sensor/authentication data.
7. WebcamPage stages a selected image, LeafClassifierService runs ONNX Runtime inference, and the result can be stored as a LeafClassification record.
8. The future Raspberry Pi deployment will make the API/database/CNN service authoritative so multiple MAUI clients share one farm state.

## Solution and root files

| File or folder | Purpose | Contribution |
| --- | --- | --- |
| GLORIOUSSYSTEM.slnx | .NET solution definition | Lets Visual Studio and dotnet build the App, API, Data, and ConsoleTest projects together. |
| AGENTS.md | Repository operating instructions | Records architecture, build commands, database expectations, and maintenance rules for coding agents. |
| CLAUDE.md | Companion project guidance | Gives another coding assistant the same repository context and conventions. |
| .gitignore | Git exclusion rules | Keeps standard build output and user-specific files out of commits. Add new generated-cache patterns here when needed. |
| run_app.ps1, run_app_debug.ps1, run_debug.ps1, run_diagnostic.ps1, run_dotnet.ps1, run_verbose.ps1 | Local developer helpers | Wrap common launch/diagnostic flows; the README documents CMD equivalents for portability. |
| get_app_crash.ps1, get_app_crash2.ps1, get_crash_events.ps1, get_crash_events_system.ps1 | Crash inspection helpers | Collect Android/Windows logs while diagnosing app exits. |
| check_events.ps1, check_events_broad.ps1, check_wasdk.ps1 | Environment diagnostics | Inspect Windows events and Windows App SDK conditions when the desktop target fails to start. |
| TestBCrypt.cs and TestBCrypt | Password-hash smoke test | Confirms BCrypt behavior independently from the full MAUI/API applications. |

## MAUI client: src/GLORIOUSSYSTEM.App

### Startup, navigation, and services

| File | Purpose | Contribution |
| --- | --- | --- |
| GLORIOUSSYSTEM.App.csproj | MAUI project definition | Targets Android, Windows, iOS, and MacCatalyst; packages the database, ONNX models, images, fonts, and app settings. |
| MauiProgram.cs | MAUI host builder | Creates the MAUI application host and logging/configuration foundation. |
| App.xaml and App.xaml.cs | Application root | Loads shared resources, configures services/database paths, runs migrations/reconciliation, applies preferences, starts Windows API auto-start, and chooses LoginPage or AppShell. |
| AppShell.xaml and AppShell.xaml.cs | Shell/navigation root | Owns the Dashboard, Webcam, Reports, and Settings destinations and lazily creates each page so navigation can be reused. |
| AppSidebar.xaml and AppSidebar.xaml.cs | Desktop/sidebar navigation | Supplies the wider desktop navigation surface while keeping route changes centralized in AppShell. |
| AdaptivePageLayout.cs | Responsive layout helper | Shares desktop/mobile sizing behavior between pages without duplicating breakpoints. |
| ThemeManager.cs | Central appearance manager | Reads saved theme preferences and updates DynamicResource colors for surfaces, text, outlines, primary/accent colors, and backgrounds at runtime. |
| MaterialIcon.cs | Icon vocabulary/rendering helper | Maps the app's icon enum to the Material-style symbols used by navigation and cards. |
| Converters/InverseBoolConverter.cs | XAML converter | Supports simple inverse boolean bindings in the UI. |

### Pages and their code-behind

| File | Purpose | Contribution |
| --- | --- | --- |
| LoginPage.xaml and LoginPage.xaml.cs | Authentication UI | Handles local account sign-in/registration, confirmation password, Google sign-in entry, remember-me behavior, and navigation into AppShell. |
| PasswordResetPage.xaml and PasswordResetPage.xaml.cs | Password reset UI | Requests a Gmail-delivered code through the API and confirms a new password. |
| MainPage.xaml and MainPage.xaml.cs | Dashboard | Displays farm status, solar/battery cards, and the infinite sensor carousel. Loads local sensor/readings data and maps hardware types to cards/statuses. |
| WebcamPage.xaml and WebcamPage.xaml.cs | Crop scanner | Opens Android/Windows image selection or camera capture, stages files, runs CNN inference, renders lettuce/variety/health results, and can save classifications. |
| ReportsPage.xaml and ReportsPage.xaml.cs | Analytics | Summarizes configured sensors, freshness, exceptions, latest readings, and the pH/time-series chart from the local database. |
| SettingsPage.xaml and SettingsPage.xaml.cs | Configuration | Edits sensor thresholds/enabled state, API connection settings, Google client ID, appearance presets, and reset-all behavior. |
| PhChartDrawable.cs | Chart renderer | Draws the pH trend chart consumed by ReportsPage. |

### App services and models

| File | Purpose | Contribution |
| --- | --- | --- |
| ApiSensorService.cs | API client | Calls health, latest readings, password-reset, Google exchange, refresh, and logout endpoints; stores API/session tokens in secure storage. |
| LocalApiProcessManager.cs | Windows development helper | Checks the local API health endpoint, starts only the API child process owned by the Windows app, waits for readiness, and stops it on window close. It is a no-op off Windows. |
| GoogleAuthService.cs | Google identity bridge | Starts the platform browser/OAuth flow and exchanges the returned Google token with the API. |
| LeafClassifierService.cs | ONNX inference engine | Stages packaged ONNX model/sidecar files, decodes/resizes images with SkiaSharp, executes MobileNetV2, and produces lettuce, variety, and health probabilities. |
| Models/*.onnx and *.onnx.data | Packaged inference artifacts | Provide the multitask and Green Ice model candidates used by the mobile/desktop scanner. |
| appsettings.json and appsettings.Development.json | Client configuration | Define database/API defaults, Windows local API auto-start, Google client placeholders, and development overrides without storing secrets. |

### MAUI resources and platforms

| Path | Purpose | Contribution |
| --- | --- | --- |
| Resources/Styles/Colors.xaml | Base color tokens | Defines the named color resources consumed by the theme system. |
| Resources/Styles/DesignSystem.xaml | Component tokens/templates | Defines shared typography, spacing, cards, buttons, navigation surfaces, and DynamicResource bindings. |
| Resources/Styles/Styles.xaml | Control styles | Applies the design tokens to common MAUI controls. |
| Resources/Images/*.svg and glorious_mark.png | App imagery/icons | Supply navigation icons, sensor illustrations, crop frame, status art, and the brand mark. |
| Resources/AppIcon and Resources/Splash | Package branding | Provide the application icon and startup splash asset. |
| Resources/Fonts | Typography | Packages Open Sans regular and semibold fonts used by the visual system. |
| Platforms/Android | Android host and permissions | Defines MainActivity/MainApplication, manifest permissions, Android color resources, and the OAuth callback activity. |
| Platforms/Windows | Windows host packaging | Defines the WinUI application manifest and Windows App SDK entry point. |
| Platforms/iOS and Platforms/MacCatalyst | Apple hosts | Define app delegates, privacy metadata, entitlements, and platform startup behavior. |

## ASP.NET Core API: src/GLORIOUSSYSTEM.Api

| File | Purpose | Contribution |
| --- | --- | --- |
| GLORIOUSSYSTEM.Api.csproj | API project definition | Targets .NET 10 and references the shared Data project plus ASP.NET/OpenAPI, EF Core, and BCrypt packages. |
| Program.cs | API composition root | Configures controllers, EF Core, Gmail sender, Google HTTP client, rate limiting, API-key/session middleware, HTTPS policy, migrations, reconciliation, and the /health endpoint. |
| Controllers/ReadingsController.cs | Telemetry endpoints | Returns latest readings with thresholds and historical readings for charts. |
| Controllers/AuthController.cs | Authentication endpoints | Handles local/API login, Google token exchange, refresh-token rotation, logout, password-reset request, and password-reset confirmation. |
| PasswordResetEmailSender.cs | Gmail SMTP adapter | Sends one-time reset codes using a Gmail SMTP app password supplied through configuration. |
| SessionTokenService.cs | Signed session tokens | Issues and validates short-lived access tokens tied to a user's session version. |
| appsettings.json and appsettings.Development.json | API configuration | Define database/security/email defaults. Production secrets are supplied by environment variables. |
| Properties/launchSettings.json | Local launch profiles | Provides the development HTTP/HTTPS ports used by Visual Studio and dotnet run. |
| GLORIOUSSYSTEM.Api.http | HTTP smoke requests | Provides a quick manual request collection for local API testing. |

## Shared data layer: src/GLORIOUSSYSTEM.Data

| File or folder | Purpose | Contribution |
| --- | --- | --- |
| GLORIOUSSYSTEM.Data.csproj | Data project definition | Packages EF Core SQLite/design/configuration support for .NET 10. |
| Models/HydroponicDbContext.cs | EF Core context | Exposes all domain tables, indexes, relationships, defaults, and the configured SQLite connection. |
| Models/HydroponicDatabasePath.cs | Path resolver | Converts relative/absolute/configured database paths into a usable platform path. |
| Models/Node.cs | Controller node entity | Represents ESP32-S3 or future Raspberry Pi controller hardware. |
| Models/Pipe.cs | NFT pipe entity | Represents the four physical growing pipes. |
| Models/Sensor.cs | Sensor entity | Stores name, type, model, placement, enablement, notes, and thresholds. |
| Models/Reading.cs | Time-series entity | Stores timestamped metric/value pairs linked to a sensor. |
| Models/SensorHardwareCatalog.cs | Hardware reconciliation | Adds missing current sensors, normalizes models, removes legacy BH1750 data, removes duplicate pH/environment records, and preserves an idempotent catalog. |
| Models/SensorFreshnessPolicy.cs | Freshness rules | Centralizes how telemetry is classified as fresh, stale, or offline. |
| Models/Camera.cs | Camera entity | Identifies farm cameras used by classification records. |
| Models/LeafClassification.cs | CNN result entity | Stores the selected image path, prediction label, confidence, camera, pipe, and timestamp. |
| Models/Actuator.cs and ActuatorEvent.cs | Control entities | Reserve the data model for future pumps/lights and their on/off history. |
| Models/Display.cs | Output-device entity | Represents TFT/LCD/OLED/LED output devices attached to a controller. |
| Models/User.cs | Account/session entity | Stores BCrypt hashes, roles, active state, session version, refresh tokens, and reset-token metadata. |
| Migrations | Schema history | Lets API/App startup migrate an existing SQLite database as the model evolves. |
| appsettings.json and appsettings.Development.json | Data defaults | Provide connection-string defaults for tools that construct the context directly. |

## Database artifacts

| Path | Purpose | Contribution |
| --- | --- | --- |
| database/hydroponic.db | Development/demo SQLite database | Contains the current schema, sensor catalog, readings, camera/pipe records, and local users used by the Windows app and packaged Android copy. |
| database/hydroponic_schema.sql | Human-readable schema and seed | Documents table design and seeds the current hardware examples, solar/battery cards, and four NFT pipes. |
| database/hydroponic.db-wal and related files, if present | SQLite runtime state | Must not be copied while the database is actively open; use a proper SQLite backup when moving production data. |

## CNN training pipeline

| File or folder | Purpose | Contribution |
| --- | --- | --- |
| cnn-training/train.py | Baseline health training | Trains the earlier health-only model. |
| cnn-training/train_multitask.py | Main model training | Trains lettuce/non-lettuce, variety, and health heads from the prepared dataset. |
| cnn-training/export_onnx.py | Baseline export | Exports the legacy health model for ONNX Runtime. |
| cnn-training/export_onnx_multitask.py | Multitask export | Exports the multitask PyTorch model and external tensor sidecar for app packaging. |
| cnn-training/evaluate.py | Evaluation | Reports held-out classification performance for the health model. |
| cnn-training/evaluate_independent_onnx.py | Independent ONNX evaluation | Tests exported artifacts separately from training code before app integration. |
| cnn-training/evaluate_iceberg_stress.py | Variety stress test | Checks difficult cultivar/variety cases and helps prevent leakage-driven model claims. |
| cnn-training/build_variety_manifest.py | Dataset manifest builder | Records source/provenance and cultivar labels for reproducible variety experiments. |
| cnn-training/prepare_green_ice.py | Green Ice preparation | Prepares source-preserving Green Ice/not-Green-Ice training data. |
| cnn-training/prepare_independent_test.py | Independent test preparation | Builds an evaluation set that does not silently overlap training data. |
| cnn-training/balance_data.py and sort_npk.py | Dataset utilities | Help balance and organize source images before training. |
| cnn-training/data | Training/validation/test images | Supplies the supervised data; preserve provenance folders and manifests. |
| colab-package | Colab bundles | Provides portable scripts and candidate model artifacts for GPU training outside the development laptop. |

## Console and documentation tools

| Path | Purpose | Contribution |
| --- | --- | --- |
| src/GLORIOUSSYSTEM.ConsoleTest/Program.cs | Database smoke runner | Opens the shared context, reconciles hardware, and can generate sample readings to validate local data behavior. |
| docs/android-api-secure-setup.md | Android API guide | Explains emulator versus physical-phone addresses, API keys, HTTPS, and Windows firewall considerations. |
| docs/gmail-password-reset-setup.md | Gmail guide | Explains app-password setup and environment variables for SMTP reset emails. |
| docs/ui-references | UI references | Stores the visual references used to guide the dashboard/reports material design. |
| docs/roadmap.md | Issue and feature roadmap | Records the observed defects, priorities, and next implementation sequence. |
| docs/raspberry-pi-deployment.md | Future deployment plan | Explains how the API, database, CNN, sensor ingestion, security, and operations should move to a Pi. |
| docs/superpowers/specs and docs/superpowers/plans | Engineering records | Preserve design decisions and implementation plans for completed or ongoing work. |

## What is intentionally not an application dependency

Visual Studio folders, Android build output, bin/obj directories, Python __pycache__ files, graph/query caches, screenshots, and local log files are developer artifacts. They do not contribute to the deployed system and should not be transferred through a source commit.
