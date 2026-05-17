# MobileIoT Test Harness

Framework that lets a coding agent (or a human) drive end-to-end tests across phones and a Raspberry Pi from either a Windows or Mac dev box.

## Quick start

```bash
# From repo root
cd harness
python -m pip install -e .[ios,ble,serial,dev]

# See what's plugged in and what's eligible to run
python -m harness doctor

# Run mock-tier tests only (no hardware required)
python -m harness run --tier mock

# Build a specific app/platform pair
python -m harness build --app maui --platform android
python -m harness build --app uno --platform windows

# Run against the Uno WinUI demo on the local Windows host
python -m harness run --integration smoke_uno_windows --app uno --tier hardware

# Run everything that can run (mock + hardware for devices that are online)
python -m harness run --tier auto --json > run.jsonl
```

## Integration demos

- **NFC HCE ↔ reader (two Androids)**: see [`docs/nfc-demo.md`](docs/nfc-demo.md). One-shot wrapper: `tools/run_nfc_demo.sh`. Mac dev-box setup: `tools/setup_dev_mac.sh`.

## What's here

This directory contains the **framework skeleton only**. Per-integration test suites live next to the code they exercise (`src/pi/tests/`, `src/MobileIoT/QiMata.MobileIoT.Tests/`) and plug in via entry-point registered `IntegrationPlugin` classes under [`harness/integrations/`](harness/integrations/).

The directory layout below shows where the harness runtime, fixtures, transports, and self-tests live.

## Layout

```
harness/
  harness/
    cli.py              # Typer CLI entry point
    inventory/          # Device inventory + discovery (adb, usbmuxd, Pi USB-ether)
    transports/         # ADB / iDevice / Pi-SSH / LocalHost transports
    app/                # Build, install, launch MAUI/Uno apps; drive the in-app HTTP hook
    integrations/       # One plugin file per integration (metadata only)
    reporting/          # Artifacts + console + JSON stream for agents
    fidelity.py         # Tier gating + @requires_hardware marker
    fixtures.py         # pytest fixtures: android_phone, ios_phone, pi, artifacts_dir
    secrets.py          # keyring -> .env.harness -> env
  tests/                # Self-tests for the harness itself
devices.yaml            # (at repo root) committed device inventory, no secrets
```

## Prerequisites

- **Windows dev box**: Python 3.11+, `adb` in `PATH` (Android Platform Tools), Windows App SDK runtime for Uno WinUI.
- **Mac dev box**: Python 3.11+, `adb`, `pymobiledevice3` (`pip install pymobiledevice3`), Xcode command-line tools.
- **Pi**: USB-ether gadget (RNDIS/ECM) configured so the Pi appears at a known IP when USB-connected; SSH key in `~/.ssh/authorized_keys`.
- **Uno workloads**: install the Uno/.NET workloads needed by your host, then restore/build from the repo root. The Uno app uses `Uno.Sdk` pinned in `global.json`.
- **App binaries**: built on demand via `dotnet build -c TestHarness` for mobile and `dotnet publish -c TestHarness` for Uno Windows (see [../src/MobileIoT/](../src/MobileIoT/)).

## App selection

`--app` selects which demo app the harness builds and launches:

- `maui` (default): `com.qimata.mobileiot`, Android/iOS.
- `uno`: `com.qimata.mobileiot.uno`, Windows/Android/iOS.
- `both`: test selection mode for suites that explicitly parametrize both app variants.

Supported build pairs:

```bash
python -m harness build --app maui --platform android
python -m harness build --app maui --platform ios
python -m harness build --app uno --platform windows
python -m harness build --app uno --platform android
python -m harness build --app uno --platform ios
```

The Windows transport uses the committed `windows-local` inventory entry and is online only on Windows hosts. It launches the published Uno executable locally and reaches the shared `HarnessHttpHost` at `http://127.0.0.1:47821`.

## Adding a new integration

The goal of the framework is that new integrations require **no harness-core edits**:

1. Add a metadata-only plugin file under `harness/integrations/<name>.py`.
2. If the app must do something in test mode, add a scenario class under `src/MobileIoT/QiMata.MobileIoT.Shared/Services/TestHarness/Scenarios/<Name>Scenario.cs`.
3. Add mock-tier tests next to the existing mock tests (`src/MobileIoT/QiMata.MobileIoT.Tests/` or `src/pi/tests/`).
4. Add hardware-tier tests under `harness/tests/integrations/test_<name>_hw.py` using the stock fixtures and `@requires_hardware`.

Each integration gets its own plan before implementation — this directory only contains the shared skeleton.
