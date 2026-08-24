# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**GLORIOUS SYSTEM** — Solar-powered hydroponic lettuce monitoring system with:
- **MAUI cross-platform app** (.NET 10, Android/iOS/macOS/Windows) — sensor dashboard, leaf classification via CNN, API connectivity
- **ASP.NET Core Web API** (.NET 10) — serves sensor readings from SQLite database
- **Shared Data Layer** — Entity Framework Core + SQLite with hydroponics domain models (Nodes, Pipes, Sensors, Readings, Cameras, LeafClassifications, Actuators)
- **CNN Training Pipeline** (Python/PyTorch) — MobileNetV2 trained on leaf images (deficient/diseased/healthy), exported to ONNX for mobile inference

## Solution Structure

```
GLORIOUSSYSTEM.slnx
├── src/
│   ├── GLORIOUSSYSTEM.App/          # MAUI app (net10.0-android/ios/maccatalyst/windows)
│   ├── GLORIOUSSYSTEM.Api/          # ASP.NET Core API (net10.0)
│   ├── GLORIOUSSYSTEM.Data/         # EF Core + SQLite models (net10.0)
│   └── GLORIOUSSYSTEM.ConsoleTest/  # Simple test console app
├── cnn-training/                    # Python PyTorch training & ONNX export
└── database/                        # SQLite DB file + schema.sql
```

## Key Dependencies

| Project | Key Packages |
|---------|--------------|
| **App** | Microsoft.Maui.Controls, Microcharts.Maui, SkiaSharp, Microsoft.ML.OnnxRuntime (1.27.1) |
| **Api** | Microsoft.AspNetCore.OpenApi, EF Core (via Data project) |
| **Data** | Microsoft.EntityFrameworkCore.Sqlite (10.0.11), Microsoft.EntityFrameworkCore.Design |
| **CNN** | torch, torchvision, onnx |

## Build / Run Commands

### .NET Projects (from repo root)

```bash
# Build entire solution
dotnet build GLORIOUSSYSTEM.slnx

# Build specific project
dotnet build src/GLORIOUSSYSTEM.App/GLORIOUSSYSTEM.App.csproj
dotnet build src/GLORIOUSSYSTEM.Api/GLORIOUSSYSTEM.Api.csproj

# Run API (defaults to http://localhost:5053)
dotnet run --project src/GLORIOUSSYSTEM.Api/GLORIOUSSYSTEM.Api.csproj

# Run MAUI app (Windows)
dotnet run --project src/GLORIOUSSYSTEM.App/GLORIOUSSYSTEM.App.csproj -f net10.0-windows10.0.19041.0

# Run console test
dotnet run --project src/GLORIOUSSYSTEM.ConsoleTest/GLORIOUSSYSTEM.ConsoleTest.csproj
```

### CNN Training (Python)

```bash
cd cnn-training
# Activate venv (created at cnn-training/venv)
source venv/bin/activate  # Linux/macOS
# or: .\venv\Scripts\Activate.ps1  # Windows PowerShell

# Train model (uses data/train/ and data/val/ folders)
python train.py

# Export to ONNX
python export_onnx.py

# Evaluate model
python evaluate.py
```

## Database

- **File**: `database/hydroponic.db` (SQLite)
- **Schema**: `database/hydroponic_schema.sql` — creates tables for Nodes, Pipes, Sensors, Readings, Cameras, LeafClassifications, Actuators, ActuatorEvents
- **Connection string** hardcoded in `HydroponicDbContext.OnConfiguring()` → `Data Source=C:\Dev\GLORIOUSSYSTEM\database\hydroponic.db`

## Architecture Notes

### Data Layer (`src/GLORIOUSSYSTEM.Data/Models/`)
- `HydroponicDbContext` — EF Core context with all DbSets and Fluent API configuration
- Domain models: `Node`, `Pipe`, `Sensor`, `Reading`, `Camera`, `LeafClassification`, `Actuator`, `ActuatorEvent`
- Sensor types: pH, TDS, WaterTemp, UltrasonicLevel, BME280, BH1750, FlowRate
- Readings indexed on (SensorId, Timestamp)

### API (`src/GLORIOUSSYSTEM.Api/`)
- Minimal `Program.cs` with OpenAPI/Swagger in Development
- `ReadingsController` with two endpoints:
  - `GET /api/readings/latest` — latest reading per sensor with thresholds
  - `GET /api/readings/{sensorId}/history` — time-series history for one sensor
- Creates new `HydroponicDbContext` per request (no DI registration)

### MAUI App (`src/GLORIOUSSYSTEM.App/`)
- **Pages**: MainPage (sensor dashboard), WebcamPage (leaf classification), ReportsPage (charts + API test), SettingsPage, AppShell (navigation)
- **Services**:
  - `LeafClassifierService` — ONNX Runtime inference (MobileNetV2, 224x224, ImageNet normalization), loads `Models/leaf_model.onnx` + `.data`
  - `ApiSensorService` — HttpClient wrapper calling API at `http://localhost:5053/`
- **Models**: `LeafPrediction` (label, confidence, all scores), `ApiSensorReading` (DTO matching API response)
- **UI**: CollectionView grouped by category (Water Quality / Environmental / Water Flow), color-coded status indicators, Microcharts line chart for pH history

### CNN Training (`cnn-training/`)
- **Data layout**: `data/train/{deficient,diseased,healthy}/`, `data/val/{deficient,diseased,healthy}/`
- **Model**: MobileNetV2 (ImageNet pretrained) → 3-class classifier
- **Training**: 15 epochs, batch 16, Adam lr=0.0005, CrossEntropyLoss
- **Export**: ONNX opset 18, dynamic batch axis, input `1x3x224x224`, classes: `["deficient", "diseased", "healthy"]` (alphabetical = ImageFolder order)
- **Artifacts**: `leaf_model.pth` (PyTorch), `leaf_model.onnx` + `leaf_model.onnx.data` (copied to App/Models/)

## Common Tasks

### Adding a new sensor type
1. Add to `Sensor.Type` enum values in schema/db
2. Update `MainPage.xaml.cs` grouping logic (line 64-66)
3. Add threshold logic if needed

### Retraining CNN
1. Add images to `cnn-training/data/train/{class}/` and `data/val/{class}/`
2. Run `python train.py` then `python export_onnx.py`
3. Copy new `.onnx` + `.onnx.data` to `src/GLORIOUSSYSTEM.App/Models/`

### Changing API base URL in App
Update `ApiSensorService` constructor default (line 22) or pass custom URL.

## .gitignore
Excludes: `bin/`, `obj/`, `*.user`, `desktop.ini` — **does NOT exclude** `database/hydroponic.db`, `cnn-training/*.onnx`, `cnn-training/*.pth`, or `venv/`

## Configuration Notes
- API port: 5053 (from `launchSettings.json` in Api project)
- MAUI app targets .NET 10 with WinUI 3 (Windows), iOS 15+, Android 21+, MacCatalyst 15+
- SQLite DB path is Windows-specific absolute path — would need config for cross-platform