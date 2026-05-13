---
name: test-harness
description: Run, debug, and extend the MobileIoT cross-platform test harness (Python `harness/` + .NET `TestHarness` build configuration). Use when invoking pytest tiers, building TestHarness binaries, adding a new integration plugin, or interpreting harness JSON output.
---

# MobileIoT test harness

The harness drives end-to-end tests across phones and a Raspberry Pi from a Windows or Mac dev box. The Python framework lives under `harness/`; the in-app HTTP host lives in the shared .NET project under the `TestHarness` build configuration.

## When to use

- Running any tier of harness tests (`mock`, `hardware`, `auto`)
- Building the TestHarness APK / IPA / MSIX
- Adding a new integration (BLE, NFC, WiFi Direct, etc.)
- Debugging why a hardware-tier test is skipping
- Reading or producing the harness JSON event stream

## Layout

```
harness/harness/        framework code (cli, inventory, transports, app, integrations, reporting)
harness/tests/          self-tests for the harness (mock tier)
harness/tests/integrations/   per-integration hardware-tier tests (test_<name>_hw.py)
src/MobileIoT/QiMata.MobileIoT.Shared/Services/TestHarness/
                        HarnessHttpHost, IScenario, scenario implementations, DI extension
devices.yaml            committed inventory (no secrets)
devices.local.yaml      per-dev-box overrides (gitignored)
.env.harness            secrets (gitignored); .env.harness.example is the template
harness/runs/           run artifacts + build cache (gitignored)
```

## CLI cheatsheet

Run from repo root.

```
python -m harness doctor
python -m harness run --tier mock|hardware|auto --app maui|uno|both [--json]
python -m harness run --integration <name> --tier hardware --app <variant>
python -m harness build --app maui|uno --platform android|ios|windows
python -m harness repl
```

Exit codes: `0` = green, `1` = real failure, `2` = hardware skipped (offline / wrong host).

JSON mode (`--json`) emits one event per line on stdout — use for agent-driven runs.

## Adding a new integration

The framework is pluggable; **do not edit `harness/harness/` core files** when adding integrations — entry-points auto-discover plugins.

Four touch points (see [project_test_harness.md](../../../../../C:/Users/Recording/.claude/projects/D--Code-mobileiot/memory/project_test_harness.md) for design rationale):

1. **Plugin** — `harness/harness/integrations/<name>.py`, subclass `IntegrationPlugin` from `harness.integrations.base`. Use `_smoke.py` as a template.
2. **Entry point** — register in `harness/pyproject.toml` under `[project.entry-points."mobileiot_harness.integrations"]`.
3. **Scenario** (only if the app must respond) — `src/MobileIoT/QiMata.MobileIoT.Shared/Services/TestHarness/Scenarios/<Name>Scenario.cs` implementing `IScenario`; register it in `HarnessServiceCollectionExtensions`.
4. **Tests** — mock-tier alongside existing mocks (`src/MobileIoT/QiMata.MobileIoT.Tests/` or `src/pi/tests/`); hardware-tier at `harness/tests/integrations/test_<name>_hw.py` using the `android_phone` / `ios_phone` / `windows_local` / `pi` fixtures with `@pytest.mark.hardware(roles=[...], capabilities=[...])`.

Gating: hardware tests auto-skip when inventory can't satisfy `roles`/`capabilities`; the reason surfaces in the JSON reporter. iOS tests skip cleanly on Windows hosts (`reason_offline = "ios-requires-macos"`).

## Build configuration

- `dotnet build -c TestHarness` defines `TEST_HARNESS` + `DEBUG` on `QiMata.MobileIoT`, `QiMata.MobileIoT.Uno`, and `QiMata.MobileIoT.Shared`.
- `HarnessHttpHost` (127.0.0.1:47821) is compiled under `#if TEST_HARNESS` and started by `MauiProgram.cs` / `App.xaml.cs` only when `MIOT_TEST_MODE=1` (env or Android intent extra) — Uno also accepts `--miot-test-mode` on the command line. Stripped from `Release` builds.
- App binaries are built on demand by `harness/harness/app/builder.py` and cached under `harness/runs/.cache/` by git SHA + dirty-tree hash.

## Transports

USB-only, no LAN SSH:

- Android: ADB (`adb forward` for port forwarding).
- iOS: pymobiledevice3 usbmux (Mac dev box only).
- Pi: paramiko SSH tunnel over USB-gadget ether (e.g. `192.168.7.2`).
- Windows: local execution of the published Uno executable.

## Pointers

- [`harness/README.md`](../../../harness/README.md) — quick-start, app selection, build pairs.
- [`devices.yaml`](../../../devices.yaml) — committed inventory shape.
- [`.env.harness.example`](../../../.env.harness.example) — required secret keys.
- Memory: `project_test_harness.md` (design decisions), `MEMORY.md` (project overview).
- `tools/run_checks.sh` — baseline CI script (Python unit tests + `dotnet test QiMata.MobileIoT.Tests`); the harness CLI is the integration-level entry point.
