# Raspberry Pi deployment plan

This guide describes the intended production shape of GLORIOUS SYSTEM. It is a future deployment plan, not a claim that the current repository already installs these services on a Pi.

## Why move the services to a Raspberry Pi?

The current Windows workflow is useful for development, but it has two limitations:

- Windows loopback is local to the development computer and is not a farm controller.
- Android currently carries a private packaged SQLite copy, so a user created on Windows is not automatically present on the phone.

A Raspberry Pi 5 can stay at the farm, run continuously from the solar-backed network, own the database, receive telemetry, and expose one authenticated API to every client.

## Target architecture

    Sensors
      ├─ Analog pH probe + PH-450C signal board
      ├─ DFR0300 TDS
      ├─ DS18B20 water temperature
      ├─ JSN-SR04T water level
      ├─ BME680 environment
      ├─ YF-S201 flow
      ├─ PZEM-017 solar output
      └─ INA219 battery telemetry
             │
             ▼
    ESP32-S3 sensor nodes
      - ADC / 1-Wire / I2C / pulse capture
      - local validation and retry queue
      - MQTT or authenticated HTTPS publish
             │
             ▼
    Raspberry Pi 5
      ├─ glorioussystem-api.service
      │    ASP.NET Core API and authentication
      ├─ hydroponic.db
      │    authoritative SQLite database
      ├─ glorioussystem-cnn.service
      │    queued ONNX inference worker
      ├─ optional Caddy/Nginx HTTPS reverse proxy
      ├─ backups and health monitoring
      └─ optional local display/maintenance tools
             │
             ▼
    MAUI clients
      - Windows desktop
      - Android phone/tablet
      - future iOS/macOS clients

The Pi should be the source of truth for users, sessions, sensors, readings, reports, classifications, and actuator events. A client can keep a cache for offline viewing, but it should not create an independent identity database.

## Service responsibilities

### API service

The existing ASP.NET Core API is the starting point. On the Pi it should:

- Serve latest readings and time-series history.
- Accept authenticated sensor-ingestion requests.
- Handle local account, Google, refresh-token, logout, and password-reset flows.
- Reconcile the sensor hardware catalog at startup.
- Apply migrations in a controlled deployment step.
- Expose /health for systemd and monitoring.
- Enforce HTTPS and API-key/session-token policy.

The API should bind to localhost behind a reverse proxy when remote access is required. Direct plain HTTP should be limited to a trusted isolated development network.

### Database

Use one authoritative SQLite file stored outside the application publish directory, for example:

    /var/lib/glorioussystem/hydroponic.db

The database directory must be owned by the service account and backed up before migrations. SQLite is a reasonable first Pi deployment because the data volume is small and the schema already exists, but the API should keep its data-access boundaries clean so PostgreSQL can be introduced later if the farm grows.

Do not copy a live SQLite file by copying its main file only while WAL mode is active. Use SQLite's backup command or stop the service first.

### CNN worker

The Android test showed a high memory peak during local ONNX inference. The preferred Pi design is an asynchronous worker:

1. The client uploads or references a crop image through an authenticated API request.
2. The API stores the image in a controlled data directory and creates a classification job.
3. The CNN worker reads one job at a time, runs the quantized/mobile ONNX model, writes the prediction, confidence, variety, and health values, and marks the job complete.
4. The client polls or receives the result through a later API request.

This keeps the MAUI UI responsive, avoids loading a large model in every client process, and gives the farm one consistent model version. A local mobile fallback can remain for disconnected demonstrations, but it should use an explicit memory/device capability check.

## Pi preparation

1. Install Raspberry Pi OS 64-bit on a Raspberry Pi 5 with a supported power supply and reliable storage.
2. Give the Pi a DHCP reservation or static LAN address.
3. Install .NET 10 runtime/SDK appropriate for the chosen publish mode.
4. Create a restricted service account and directories:

       sudo useradd --system --home /var/lib/glorioussystem --shell /usr/sbin/nologin glorioussystem
       sudo mkdir -p /var/lib/glorioussystem /etc/glorioussystem /var/log/glorioussystem
       sudo chown -R glorioussystem:glorioussystem /var/lib/glorioussystem /var/log/glorioussystem

5. Connect the ESP32-S3 nodes and validate voltage, ADC ranges, waterproofing, pull-up resistors, and isolation before enabling pumps or power telemetry.
6. Keep all SMTP/API/Google secrets in a protected environment file or secret manager, not in the Git checkout.

## Publish the API for ARM64

Publish from the development laptop after the API/Data projects build:

    cd /d C:\Dev\GLORIOUSSYSTEM
    dotnet publish src\GLORIOUSSYSTEM.Api\GLORIOUSSYSTEM.Api.csproj -c Release -r linux-arm64 --self-contained false -o publish\api-linux-arm64

Copy the publish output to the Pi with a secure transfer tool. Do not copy bin/obj or a developer's local secrets.

A self-contained publish can be used if the Pi should not install the .NET runtime:

    dotnet publish src\GLORIOUSSYSTEM.Api\GLORIOUSSYSTEM.Api.csproj -c Release -r linux-arm64 --self-contained true -o publish\api-linux-arm64

Choose one mode and document the runtime version beside the release artifact.

## Configuration on the Pi

Use a production configuration outside the repository or environment variables equivalent to:

    ConnectionStrings__HydroponicDb=Data Source=/var/lib/glorioussystem/hydroponic.db
    Security__ApiKey=<long-random-api-key>
    Security__SessionSigningKey=<different-long-random-signing-key>
    Security__RequireApiKey=true
    Security__RequireHttps=true
    Email__SmtpUser=<gmail-address>
    Email__SmtpPassword=<gmail-app-password>
    Email__FromAddress=<gmail-address>
    Email__FromName=GLORIOUS SYSTEM

The Google client ID is not a secret, but the OAuth redirect URI must match the deployed client configuration. API keys, signing keys, SMTP app passwords, refresh tokens, and database files must not be placed in public Git history.

## systemd service shape

Create a service that runs as the restricted account, restarts on failure, and starts after the network:

    [Unit]
    Description=GLORIOUS SYSTEM API
    After=network-online.target
    Wants=network-online.target

    [Service]
    User=glorioussystem
    Group=glorioussystem
    WorkingDirectory=/opt/glorioussystem/api
    EnvironmentFile=/etc/glorioussystem/api.env
    ExecStart=/usr/bin/dotnet /opt/glorioussystem/api/GLORIOUSSYSTEM.Api.dll
    Restart=on-failure
    RestartSec=5
    NoNewPrivileges=true
    PrivateTmp=true

    [Install]
    WantedBy=multi-user.target

The CNN worker should have its own service and queue limits so a failed model process cannot take down the API. Enable and inspect services with:

    sudo systemctl daemon-reload
    sudo systemctl enable --now glorioussystem-api.service
    sudo systemctl status glorioussystem-api.service
    journalctl -u glorioussystem-api.service -f

## Sensor ingestion plan

The current API reads seeded/local data; the physical-node ingestion contract still needs to be implemented. Use one explicit contract:

- Node identity and firmware version.
- Sensor ID/type/model and UTC timestamp.
- Metric, value, unit, and quality/status.
- Monotonic sequence number for duplicate detection.
- Optional battery/signal metadata.

The API should validate model/type pairs against SensorHardwareCatalog, reject impossible values, write readings in batches, and expose the last-seen time per node. ESP32-S3 nodes should queue readings locally when Wi-Fi is down and retry with backoff.

MQTT is a good fit when many nodes publish small readings and the Pi can run a broker. Authenticated HTTPS is simpler for a first pilot. Pick one transport for the first deployment instead of supporting both simultaneously.

## HTTPS and network boundary

For a trusted LAN pilot, bind the API behind a local reverse proxy and use a certificate trusted by the clients. For remote access, use a real domain/certificate or a private VPN. Do not expose the development HTTP port directly to the public internet.

The MAUI app should store the Pi base URL and API key through Settings/secure storage. The app must never disable certificate validation to make a self-signed certificate work.

## Database migration and backup procedure

Before a release:

1. Stop the API and CNN services.
2. Back up the existing database using SQLite's backup mechanism.
3. Copy the backup to a separate device or storage location.
4. Apply EF Core migrations.
5. Run SensorHardwareCatalog reconciliation.
6. Start the API and check /health.
7. Run a read-only latest-readings and report smoke test.
8. Keep the previous release and database backup until the new service passes a full farm-day observation.

A simple first backup while the API is stopped can be:

    sqlite3 /var/lib/glorioussystem/hydroponic.db ".backup '/var/backups/glorioussystem/hydroponic-$(date +%Y%m%d-%H%M%S).db'"

## Client transition

During the transition from laptop development to the Pi:

1. Keep Windows auto-start for local development.
2. Add the Pi URL as a saved API profile in Settings.
3. Make login, reports, and latest readings prefer API data when authenticated.
4. Treat the local Android database as an offline cache/demo only.
5. Show an offline banner instead of silently presenting old telemetry.
6. Verify two clients can see the same account, sensors, readings, and classification results.

## Validation checklist for the first Pi pilot

- API /health reports both service and database reachable.
- The sensor catalog contains exactly one pH sensor and no BH1750/BME280 records.
- Fresh readings arrive for pH, TDS, water temperature, water level, BME680, flow, solar, and battery.
- Reports show a fresh-data range and a clear empty state when no data exists.
- A phone and desktop can log in with the same API-backed account.
- Password reset sends a real Gmail message and expires codes correctly.
- One crop image can be classified without starving the API or restarting the Pi.
- A power/network interruption recovers readings from the ESP32 retry queue.
- Backups restore to a separate SQLite file.
- Logs and service restart policies are visible to the operator.

## Recommended implementation phases

### Phase 1: central service

Move users, sessions, readings, reports, and password reset to the Pi API/database. Keep the client UI and existing Windows workflow intact.

### Phase 2: telemetry ingestion

Implement and test one authenticated ESP32-S3-to-Pi transport. Start with the pH, TDS, DS18B20, JSN-SR04T, BME680, and YF-S201 sensors before adding actuators.

### Phase 3: CNN worker

Add the classification job table/API and a memory-bounded, versioned ONNX worker. Compare Pi CPU latency and memory against the Android path.

### Phase 4: operations

Add HTTPS/reverse proxy, systemd hardening, backups, health alerts, log rotation, and a documented rollback process.

### Phase 5: farm control

Only after telemetry is stable, add actuator commands, safety interlocks, manual override, and event auditing for pumps, lights, and nutrient dosing.
