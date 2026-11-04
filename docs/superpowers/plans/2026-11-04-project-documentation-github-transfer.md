# Project Documentation and GitHub Transfer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make the current GLORIOUS SYSTEM repository transferable to another laptop by documenting the system, its files, known issues, and Raspberry Pi deployment direction, then commit and push the intended project changes to GitHub.

**Architecture:** Add a concise root README for onboarding, a file-by-file project map, a current-state/fix roadmap, and a Raspberry Pi deployment guide. The documentation will describe the existing MAUI client, ASP.NET API, EF Core/SQLite data layer, and PyTorch/ONNX pipeline without changing application behavior. Git will stage project source, data, model, and documentation changes while excluding IDE caches, Python bytecode, and graph/query artifacts from this transfer commit.

**Tech Stack:** Markdown, .NET 10 MAUI, ASP.NET Core, EF Core, SQLite, PyTorch, ONNX Runtime, Raspberry Pi 5/Linux/systemd.

**Spec:** User request in the current conversation: commit and push the project, document file responsibilities, list fixes, and explain the future Raspberry Pi deployment plan.

## Global Constraints

- Preserve the existing application behavior and unrelated user changes.
- Do not commit passwords, SMTP app passwords, API keys, OAuth secrets, tokens, or private test credentials.
- Do not commit Visual Studio state, generated Python bytecode, graphify caches, or temporary test artifacts.
- Document Raspberry Pi deployment as a future architecture, not as already implemented.
- Use Windows CMD-compatible commands in the onboarding documentation.
- Verify the final staged contents, builds, and remote push before claiming completion.

## Review Focus

- A new laptop must be able to identify the correct solution, API, app, data, and CNN entry points without reading every source file.
- Raspberry Pi placement must clearly separate the authoritative API/database/CNN services from the MAUI client and ESP32 sensor nodes.
- The roadmap must distinguish observed defects from future enhancements and avoid claiming unresolved issues are fixed.
- Documentation must not expose credentials or imply that Android's private bundled SQLite copy is synchronized across devices.
- The commit must include meaningful project changes while leaving IDE and generated artifacts out of the transfer.

---

### Task 1: Create transfer and file-purpose documentation

**Files:**
- Create: `README.md`
- Create: `docs/project-map.md`

**Interfaces:**
- Consumes: the current solution structure and existing configuration/build commands.
- Produces: newcomer onboarding, file responsibility map, current sensor/CNN/API architecture, and safe Windows CMD commands.

- [ ] Write the root README with project purpose, architecture diagram in prose, prerequisites, solution projects, correct hardware list, build/run commands, Android API notes, and links to the detailed guides.
- [ ] Write `docs/project-map.md` covering the solution file, App pages/services/resources, API controllers/services/configuration, Data models/migrations, database artifacts, CNN training/export/evaluation scripts, and test utilities. Explain what each group contributes to the system.
- [ ] Verify every path named in both documents exists or is explicitly marked as future/planned.

### Task 2: Document defects and the Raspberry Pi future plan

**Files:**
- Create: `docs/roadmap.md`
- Create: `docs/raspberry-pi-deployment.md`

**Interfaces:**
- Consumes: observed Android test results, current security/setup docs, API configuration, and database schema.
- Produces: prioritized fixes with evidence and a phased deployment plan for Raspberry Pi 5.

- [ ] List completed work, known issues ordered by severity, reproduction evidence, and recommended fix order. Include the empty-email reset crash, Android local-account isolation, CNN memory pressure, navigation/startup delay, light-theme status-bar contrast, Settings bottom-navigation overlap, picker-result warning, stale report data, and CLI/Visual Studio build-lock friction where applicable.
- [ ] Describe the target Pi architecture: ESP32-S3 sensor nodes, Raspberry Pi API, authoritative SQLite database, CNN inference service/worker, LAN/TLS boundary, MAUI clients, backups, secrets, observability, and offline behavior.
- [ ] Provide a phased implementation plan: Pi OS setup, publish API for `linux-arm64`, migrate database, systemd services, sensor ingestion, CNN optimization/isolation, secure remote access, client configuration, and validation/rollback.
- [ ] Clearly label the Pi deployment as future work and state what is currently implemented on Windows/Android.

### Task 3: Verify, stage, commit, and push the transfer

**Files:**
- Modify: `.gitignore` only if needed to prevent newly generated transfer noise.
- Test: `git diff --check`, project builds, staged-content audit, and remote push.

**Interfaces:**
- Consumes: documentation from Tasks 1–2 and all existing meaningful project changes in the worktree.
- Produces: a reviewable commit on `codex/theme-sensor-hardware` pushed to `origin`.

- [ ] Add narrowly scoped ignore rules for generated IDE/cache artifacts if they are not already covered, without deleting existing user files.
- [ ] Build the API, Data, ConsoleTest, and Android MAUI targets using the repository's supported commands; record any environment-specific build limitation accurately.
- [ ] Stage source, database/schema, model/training assets, setup docs, and the new documentation, while excluding `.vs`, `.playwright-mcp`, `__pycache__`, `graphify-out`, and temporary logs/screenshots.
- [ ] Inspect `git diff --cached --stat` and `git diff --cached --check`, confirm no secret-looking values are staged, then commit with a descriptive message.
- [ ] Push with `git push -u origin codex/theme-sensor-hardware` and verify the remote branch points to the new commit.
