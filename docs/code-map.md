# Code Map

This repo has three main demo surfaces and a set of supporting libraries and tools. Start here when you want to understand where a feature lives.

## MAUI app
- Purpose: The primary mobile and desktop demo app for the QiMata.MobileIoT experience.
- Key paths: `src/MobileIoT/QiMata.MobileIoT/`, `src/MobileIoT/QiMata.MobileIoT.Shared/`
- Inspect first: `MainPage.xaml`, `Views/`, `ViewModels/`, `MauiProgram.cs`, then the shared service interfaces and models used by the pages.

## Uno app
- Purpose: A second app surface for Windows-centric demo and integration flows.
- Key paths: `src/MobileIoT/QiMata.MobileIoT.Uno/`
- Inspect first: `MainPage.xaml`, `MainPage.xaml.cs`, `Views/`, `Services/`, and the platform project files that define how the app boots.

## Shared C# contracts/core
- Purpose: Shared models, interfaces, and demo services used by both app surfaces.
- Key paths: `src/MobileIoT/QiMata.MobileIoT.Shared/`
- Inspect first: `Models/`, `Services/Interfaces/`, `Thread/Models/`, `Thread/Services/`, and `Usb/` for the contracts that drive the demos.

## Platform services
- Purpose: OS-specific implementations that back the shared contracts.
- Key paths: `src/MobileIoT/QiMata.MobileIoT/Platforms/`, `src/MobileIoT/QiMata.MobileIoT/Services/`
- Inspect first: the Android, iOS, Windows, MacCatalyst, and Tizen folders plus the service classes in `Services/` that coordinate permissions, BLE, NFC, audio, camera, and shell navigation.

## Raspberry Pi demos/gateway
- Purpose: Python demo entry points and the local gateway that talks to hardware and exposes device flows.
- Key paths: `src/pi/`, especially `src/pi/gateway/`
- Inspect first: `src/pi/thread_demo.py`, `usb_demo.py`, `serial_demo.py`, `nfc_provision_demo.py`, `camera_demo.py`, `bluetoothle_demo.py`, `audio_demo.py`, and `gateway/main.py` with `command_router.py`, `config.py`, and `models.py`.

## Python hardware harness
- Purpose: Test harness and device drivers for exercising the demos against real or mocked hardware.
- Key paths: `harness/`
- Inspect first: `harness/harness/cli.py`, `harness/harness/config.py`, `harness/harness/integrations/`, `harness/harness/transports/`, and `harness/tests/` to see how scenarios are assembled and validated.

## Tests
- Purpose: Automated checks for the MAUI app, Uno app, Pi gateway, and harness behavior.
- Key paths: `src/MobileIoT/QiMata.MobileIoT.Tests/`, `src/pi/tests/`, `harness/tests/`
- Inspect first: the tests closest to the area you are changing, then the shared fixtures or helpers that those tests import.

## Docs/tools
- Purpose: Operational notes, demo guides, and command wrappers for local workflows.
- Key paths: `docs/`, `tools/`, `README.md`, `src/pi/README.md`, `harness/README.md`, `src/MobileIoT/QiMata.MobileIoT/README.md`, `src/MobileIoT/QiMata.MobileIoT.Uno/ReadMe.md`
- Inspect first: `docs/` for cross-cutting guidance, then the relevant README or tool script for the specific demo or workflow you are running.

Generated or local-only environments such as `.venv` are not part of the codebase map.
