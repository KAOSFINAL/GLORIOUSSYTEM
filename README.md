# GLORIOUS SYSTEM

GLORIOUS SYSTEM is a solar-powered hydroponic lettuce monitoring platform. It combines a .NET MAUI client, an ASP.NET Core API, an EF Core/SQLite data layer, and an on-device crop-classification pipeline. The client displays water-quality, environmental, flow, solar, and battery telemetry; it also lets an operator classify a crop photo with a MobileNetV2-based ONNX model.

This repository is the complete development workspace. It is intended to run on Windows while the project is being developed, on Android as a field/mobile client, and eventually on a Raspberry Pi that hosts the farm services.

## System at a glance

    Physical sensors / ESP32-S3 nodes
                  │  (future HTTP or MQTT ingestion)
                  ▼
    Raspberry Pi 5 (future deployment)
      ├─ ASP.NET Core API
      ├─ SQLite database
      ├─ CNN inference worker / ONNX Runtime
      └─ systemd-managed services
                  │  HTTPS + authenticated API
                  ▼
    Windows / Android / iOS / macOS MAUI clients
      ├─ Dashboard
      ├─ Webcam crop scanner
      ├─ Reports
      └─ Settings and authentication

The Raspberry Pi arrangement is a future target. Today, the Windows app can start the local API automatically, while Android uses an explicitly configured API address. Android also receives a private copy of the packaged SQLite database for local dashboard and account flows; that copy is not synchronized with the Windows database.

## Current hardware catalog

The active sensor catalog is reconciled by SensorHardwareCatalog at application/API startup:

| Dashboard label | Type | Hardware | Purpose |
| --- | --- | --- | --- |
| Reservoir pH | pH | PH-450C with E201-BNC electrode | Reservoir acidity/alkalinity |
| Nutrient TDS | TDS | DFR0300 | Dissolved-solids estimate for nutrient concentration |
| Water Temperature | WaterTemp | DS18B20 | Reservoir temperature |
| Water Level | UltrasonicLevel | JSN-SR04T | Waterproof reservoir level measurement |
| Environment | BME680 | BME680 | Temperature, humidity, pressure, and air-quality values |
| Flow Main Supply | FlowRate | YF-S201 | Main nutrient-supply flow |
| Solar Array Output | SolarPower | PZEM-017 | Solar DC output in watts |
| Battery Charge | BatteryPercent | INA219 | Battery charge percentage |

BH1750 and BME280 are not part of the active hardware catalog. The catalog removes legacy BH1750 records and keeps a single reservoir pH record when it reconciles an upgraded database.

## Repository layout

| Path | Role |
| --- | --- |
| GLORIOUSSYSTEM.slnx | .NET solution containing the App, API, Data, and ConsoleTest projects. |
| src/GLORIOUSSYSTEM.App | Cross-platform .NET MAUI application used by operators on Windows and mobile. |
| src/GLORIOUSSYSTEM.Api | ASP.NET Core API for sensor readings, authentication, sessions, Google sign-in, and Gmail password-reset delivery. |
| src/GLORIOUSSYSTEM.Data | EF Core models, database context, migrations, hardware reconciliation, and freshness policies shared by the App and API. |
| src/GLORIOUSSYSTEM.ConsoleTest | Small console smoke test for reading/reconciling the SQLite database and generating sample readings. |
| database | SQLite database file and the human-readable schema/seed script. |
| cnn-training | PyTorch training, dataset preparation, evaluation, and ONNX export scripts. |
| colab-package | Reproducible Google Colab training/export bundles and candidate model artifacts. |
| docs | Setup guides, UI references, implementation plans, issue roadmap, and the Raspberry Pi deployment plan. |
| TestBCrypt and TestBCrypt.cs | Local password-hashing compatibility checks used during authentication work. |

For the detailed file-by-file map, see docs/project-map.md.

## Prerequisites

- Windows 10/11 with Visual Studio and the .NET 10 MAUI workload for Windows/Android development.
- .NET 10 SDK.
- Android SDK/platform-tools and a USB-debuggable Android phone or emulator for Android testing.
- Python 3.10+ and the packages listed by the CNN training scripts when retraining the model.
- A Gmail SMTP app password and API/security keys supplied through environment variables for real password-reset and protected API use. Never commit those values.

## Build and run from Windows CMD

Run these commands from the repository root. Keep each command on one line; CMD does not use PowerShell's backtick continuation syntax.

    cd /d C:\Dev\GLORIOUSSYSTEM
    dotnet restore GLORIOUSSYSTEM.slnx
    dotnet build src\GLORIOUSSYSTEM.Data\GLORIOUSSYSTEM.Data.csproj --no-restore
    dotnet build src\GLORIOUSSYSTEM.Api\GLORIOUSSYSTEM.Api.csproj --no-restore
    dotnet build src\GLORIOUSSYSTEM.ConsoleTest\GLORIOUSSYSTEM.ConsoleTest.csproj --no-restore
    dotnet build src\GLORIOUSSYSTEM.App\GLORIOUSSYSTEM.App.csproj -f net10.0-windows10.0.19041.0 --no-restore
    dotnet build src\GLORIOUSSYSTEM.App\GLORIOUSSYSTEM.App.csproj -f net10.0-android --no-restore

Start the API manually in a development terminal:

    cd /d C:\Dev\GLORIOUSSYSTEM
    dotnet run --project src\GLORIOUSSYSTEM.Api\GLORIOUSSYSTEM.Api.csproj --no-launch-profile --urls http://127.0.0.1:5053

Check it from another CMD window:

    curl http://127.0.0.1:5053/health

Run the Windows MAUI target:

    cd /d C:\Dev\GLORIOUSSYSTEM
    dotnet run --project src\GLORIOUSSYSTEM.App\GLORIOUSSYSTEM.App.csproj -f net10.0-windows10.0.19041.0 --no-restore

On Windows, LocalApiProcessManager starts the companion API if the health endpoint is not already available. It is intentionally a no-op on Android, iOS, and macOS.

Run on a connected Android device:

    cd /d C:\Dev\GLORIOUSSYSTEM
    "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe" devices -l
    dotnet build src\GLORIOUSSYSTEM.App\GLORIOUSSYSTEM.App.csproj -f net10.0-android --no-restore -t:Run

If Visual Studio is open on the same project, it may hold Android obj files open. Save work, close Visual Studio, and then rerun the build. Do not put PowerShell backticks or -f on a separate CMD line.

## Android API connectivity

- Android emulator: use http://10.0.2.2:5053/ to reach the development computer.
- Physical Android phone: start the API on the computer's trusted LAN interface and enter the computer's LAN address in Settings, for example http://192.168.1.10:5053/.
- Production or untrusted networks: use HTTPS with a certificate trusted by Android, enable the API key, and configure the same API key in the app. Do not disable certificate validation.

The detailed steps are in docs/android-api-secure-setup.md.

## Authentication and email configuration

Local account creation stores a BCrypt password hash in the Users table. API-backed sessions use short-lived signed access tokens and rotating refresh tokens. Google sign-in is available when a Google client ID and API configuration are supplied. Password reset codes are delivered by Gmail SMTP when the API has the required environment variables.

Use environment variables rather than committing secrets:

    set Security__ApiKey=<long-random-api-key>
    set Security__SessionSigningKey=<different-long-random-signing-key>
    set Email__SmtpUser=<gmail-address>
    set Email__SmtpPassword=<gmail-app-password>

See docs/gmail-password-reset-setup.md for Gmail-specific setup. The empty or placeholder values in checked-in settings files are intentional.

## CNN training and model flow

The training scripts prepare lettuce health and variety data, train a MobileNetV2 multitask model, evaluate it, and export ONNX artifacts. The MAUI app packages ONNX models under src/GLORIOUSSYSTEM.App/Models, stages them into app storage, and runs inference with ONNX Runtime and SkiaSharp. The current model heads cover lettuce/non-lettuce, Green Ice/not-Green-Ice variety, and deficient/diseased/healthy health classification.

Typical training commands are documented in the scripts and Colab package. Model files can be large; do not replace the packaged app model until a candidate has passed the independent evaluation scripts.

## Current status and next work

The current branch contains the responsive MAUI UI, centralized appearance presets, corrected sensor catalog, local registration, API session/authentication work, Gmail reset support, Windows API auto-start, and hardened webcam analysis paths. Some issues were found during Android testing and remain open. See docs/roadmap.md for the evidence-backed priority list and docs/raspberry-pi-deployment.md for the future farm deployment plan.

## Moving the project to another laptop

1. Clone the repository and open GLORIOUSSYSTEM.slnx.
2. Install the .NET 10 MAUI workload and Android SDK if Android testing is needed.
3. Run dotnet restore GLORIOUSSYSTEM.slnx.
4. Copy secrets into environment variables or local user secrets; do not put them in Git.
5. Build the API/Data/ConsoleTest projects first, then build the desired MAUI target.
6. For Android, connect the phone, confirm it appears in adb devices -l, and configure the API address in Settings.
7. For the Windows development workflow, run the MAUI app; it will attempt to start the local API automatically.
