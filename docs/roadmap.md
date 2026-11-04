# GLORIOUS SYSTEM roadmap

This is an evidence-based work list for the current branch. It separates changes already present from issues observed during Windows/Android development so the next laptop transfer starts with an honest baseline.

## Already implemented in the current branch

- Centralized DynamicResource appearance system with multiple background palettes, light/dark surface shades, readable text/outline colors, live previews, persistence, and Reset All support.
- Material-inspired responsive MAUI layouts for the Dashboard, Webcam, Reports, Settings, and navigation surfaces.
- Dashboard infinite sensor carousel with current hardware names and models.
- Hardware reconciliation that keeps one pH sensor, adds DFR0300 TDS, normalizes BME680, keeps DS18B20/JSN-SR04T/YF-S201, and removes legacy BH1750 records/readings.
- Solar output and battery reserve dashboard cards using SolarPower and BatteryPercent records.
- Local account creation with password and confirm-password validation, BCrypt password storage, remember-me handling, and logout.
- API session/refresh-token flow, Google sign-in integration points, Gmail SMTP password-reset flow, API-key protection, and HTTPS configuration controls.
- Windows-only local API auto-start that checks /health and owns only the child process it starts.
- Webcam error handling, staged image files, multitask ONNX model support, and safer result rendering. Lettuce images from Android Images → Download were successfully analyzed in the current phone test.
- Android and Windows configuration guidance for local API addresses.

## Priority 0: fix before treating authentication as production-ready

### 1. Empty password-reset email can terminate the Android app

Evidence: on Android, opening Forgot password, leaving the email field empty, and tapping Send reset code killed the app. The stack trace came from MailAddress.TryParse through PasswordResetPage.IsValidEmail.

Cause: PasswordResetPage.xaml.cs catches FormatException, but MailAddress constructed with an empty string throws ArgumentException. Validation happens before the surrounding request try/catch.

Fix plan:

1. Reject null/blank email before constructing MailAddress.
2. Use MailAddress.TryCreate or catch the full validation exception.
3. Add the same shared validation behavior to LoginPage.
4. Add a regression test or a small validation test before shipping the patch.
5. Re-run the Android flow and confirm the page shows an inline error instead of exiting.

### 2. Move authentication and users to one authoritative service

Evidence: the Windows database contained users that were not present in the Android phone's private packaged database. Credentials created on Windows therefore did not work on Android, while an account created locally on the phone did.

The current private-copy behavior is safe for offline demo use but is not multi-device authentication.

Fix plan:

1. Make the Raspberry Pi API/database the source of truth.
2. Have Android and Windows authenticate through the API.
3. Keep local SQLite only as a read cache/offline queue, not as the identity store.
4. Add a clear offline message when the API cannot be reached.
5. Add account/session integration tests across two clients.

## Priority 1: reliability and performance

### 3. Reduce CNN memory pressure on Android

Evidence: Android reported approximately 386 MB RSS rising to approximately 483 MB during one lettuce analysis. After two scans, the app was still around 390 MB PSS and 494 MB RSS. The tested lettuce images completed successfully, but this memory level can cause Android to kill the process on a device under pressure.

Fix plan:

1. Measure ONNX session and image allocations separately.
2. Decode images at a bounded size before keeping a preview.
3. Dispose inference tensors/results and replace old preview sources before a new scan.
4. Compare the current model with a quantized/mobile ONNX candidate.
5. Consider moving inference to the Raspberry Pi worker and returning only a prediction/result to the MAUI client.
6. Keep the mobile fallback only when the device has enough memory.

### 4. Reduce startup and first-navigation latency

Evidence from the connected phone:

- Cold launch displayed the splash/logo for about 1–2 seconds and took roughly 5–6 seconds to become fully usable.
- Dashboard → Webcam took about 2–3 seconds to settle.
- Dashboard → Reports took about 3–4 seconds to finish its first load.
- Startup logging showed variable delays between page construction, InitializeComponent, and sensor loading.

Fix plan:

1. Render page shells immediately and load database data after the first frame.
2. Cache sensor snapshots and reuse page instances.
3. Run report aggregation/chart preparation after Reports is visible.
4. Keep CNN/model staging off the navigation critical path.
5. Add timing logs around each page lifecycle and verify on Windows and Android.

### 5. Harden photo-picker result handling

Evidence: Android logged an ActivityThread deliverResultsIfNeeded NullPointerException when returning from the DocumentsUI picker. The app continued running, but the result path should not rely on a framework warning.

Fix plan:

1. Audit the MAUI FilePicker/IntermediateActivity result lifecycle on the supported Android version.
2. Guard null result extras and cancellation paths.
3. Test cancel, select, rotate, background/return, and repeat selection.
4. Keep inline scan errors visible when a picker result is incomplete.

## Priority 1: visual/layout quality

### 6. Fix light-mode system-bar contrast

Evidence: switching from dark mode to a light Slate background left white status-bar icons on a light surface, making them difficult to see.

Fix plan:

1. Set Android status/navigation-bar icon appearance from the active theme brightness.
2. Apply the same behavior to Windows title/system surfaces where applicable.
3. Test Neutral, Slate, and a light custom palette in both orientations.

### 7. Keep Settings actions above the fixed navigation bar

Evidence: the Save changes control at the bottom of Settings was partially clipped/covered by the fixed bottom navigation during phone testing.

Fix plan:

1. Add bottom safe-area/content padding or move the action into the page's scrollable content.
2. Test at the smallest supported phone height and a wide desktop layout.
3. Verify Reset All remains reachable without overlapping navigation.

## Priority 2: data/reporting and development workflow

### 8. Make stale report data explicit

Evidence: Reports displayed zero fresh sensors and an empty 24-hour chart because the bundled readings were old relative to the test date. The database query completed without an exception, but the result looked like a broken report.

Fix plan:

1. Show the selected range and a clear “no readings in this period” state.
2. Add a demo-data timestamp indicator.
3. Add a controlled sample-reading generator for test environments.
4. Verify chart behavior with fresh, stale, and offline fixtures.

### 9. Eliminate CLI/Visual Studio build contention

Evidence: running the Android CLI build while Visual Studio held the same obj files produced locked-file errors and stale incremental Android artifacts, including an R.java read failure.

Fix plan:

1. Close or isolate the active target before a CLI deployment.
2. Prefer separate output/intermediate paths for CI/device builds.
3. Add a documented clean/rebuild recovery sequence.
4. Avoid committing .vs, bin, obj, Python bytecode, or temporary logs.

### 10. Expand automated coverage

Add tests for:

- SensorHardwareCatalog reconciliation and BH1750/BME280/duplicate-pH removal.
- Password and email validation.
- Session refresh/logout and password-reset token expiry.
- API authorization and HTTPS/API-key requirements.
- Reports freshness and empty ranges.
- Webcam cancellation and classification error paths.
- Configuration parsing on Windows, Android emulator, physical Android, and Raspberry Pi.

## Future Raspberry Pi target

The detailed target is in docs/raspberry-pi-deployment.md. The core decision is:

- ESP32-S3 nodes remain close to the sensors and publish validated readings.
- Raspberry Pi 5 becomes the authoritative API/database host.
- A Pi worker handles CNN inference asynchronously, preferably with a quantized ONNX model.
- MAUI clients become authenticated dashboards and control surfaces rather than separate identity/database islands.
- HTTPS, API keys/session tokens, backups, health checks, and systemd restart policies are part of the deployment rather than afterthoughts.

## Suggested delivery order

1. Fix empty-email validation and add regression coverage.
2. Establish Pi/API-backed authentication and remove the expectation of cross-device local users.
3. Reduce/measure CNN memory and decide whether inference belongs on the Pi.
4. Fix system-bar contrast and Settings action overlap.
5. Improve startup/navigation loading and reports empty-state messaging.
6. Add sensor/API/reporting integration tests.
7. Deploy the first Pi pilot on a trusted LAN, then add HTTPS and remote access only after local telemetry is stable.
