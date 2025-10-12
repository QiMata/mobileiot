# Manual Cross-Device Test Project

This project captures the hardware-in-the-loop test cases that cannot be
automated easily because they require either two mobile devices or a mobile
device paired with a Raspberry Pi running the demo scripts from `src/pi`.
It complements the automated unit and integration tests by providing detailed
manual procedures for validating end-to-end behavior before demos or releases.

## How to use this project

1. Pick the scenario that matches the hardware you have available:
   - **Phone ↔ Raspberry Pi** procedures validate the MAUI app working against
the Python demo services.
   - **Phone ↔ Phone** procedures validate peer-to-peer features that rely on
platform services such as Wi-Fi Direct, NFC, or beacon scanning.
2. Review the common setup section of the relevant scenario to provision
hardware and align app builds and configuration.
3. Execute each manual test case in order, marking the results and capturing
any deviations in your test log.

## Running the manual test harness

Install `pytest` in your Python environment (e.g., `pip install pytest`) if it is not already available.
A lightweight `pytest` suite mirrors the checklists so you can track results and
store execution notes alongside other test runs. To run the prompts, supply the
environment variable or CLI flag that opts into manual validation:

```bash
python -m pytest tests/manual --run-manual
# or
RUN_MANUAL_TESTS=1 python -m pytest tests/manual
```

Each test prints the associated plan, references the detailed markdown
specification, and waits for you to confirm the outcome. Type `PASS` (or `Y`) to
record success or provide any other response to fail the case for follow-up.
Skipped tests indicate that the manual flag was not provided.

## Test inventory

The following table lists every manual test type covered by this project along
with the primary app feature or Raspberry Pi script it exercises. Detailed
steps for each test appear in the linked scenario documents.

| ID | Scenario | Test type | App feature(s) | Raspberry Pi script(s) |
| --- | --- | --- | --- | --- |
| PI-BLE-01 | Phone ↔ Pi | BLE sensor telemetry handshake | `BlePage` / `BleViewModel` | `src/pi/bluetoothle_demo.py` |
| PI-BLE-02 | Phone ↔ Pi | BLE LED control loopback | `BlePage` / `BleViewModel` | `src/pi/bluetoothle_demo.py` |
| PI-BLE-03 | Phone ↔ Pi | BLE reconnection & error recovery | `BlePage` / `BleViewModel` | `src/pi/bluetoothle_demo.py` |
| PI-BEACON-01 | Phone ↔ Pi | Beacon advertisement detection | `BeaconPage` / `BeaconScanViewModel` | `src/pi/beacon_demo.py` |
| PI-SERIAL-01 | Phone ↔ Pi | USB serial LED toggling | `SerialPage` / `SerialDemoViewModel` | `src/pi/serial_demo.py`, `run_serial_demo.sh` |
| PI-USB-01 | Phone ↔ Pi | USB bulk ping echo | `UsbPage` / `UsbViewModel` | `src/pi/usb_demo.py` (USB gadget mode) |
| PI-AUDIO-01 | Phone ↔ Pi | Audio jack telemetry ingest | `AudioPage` / `AudioDemoViewModel` | `src/pi/audio_demo.py` |
| PHONE-P2P-01 | Phone ↔ Phone | Wi-Fi Direct discovery and ping | `WifiDirectPage` / `P2pViewModel` | — |
| PHONE-P2P-02 | Phone ↔ Phone | Peer message delivery | `WifiDirectPage` / `P2pViewModel` | — |
| PHONE-NFC-01 | Phone ↔ Phone | NFC tag read/write round-trip | `NfcPage` / `NfcPageViewModel` | — |
| PHONE-NFC-02 | Phone ↔ Phone | NFC peer-to-peer handover | `NfcP2PPage` / `NfcP2PViewModel` | — |
| PHONE-BEACON-01 | Phone ↔ Phone | Beacon detection via companion device | `BeaconPage` / `BeaconScanViewModel` | Companion beacon app |
| PHONE-VISION-01 | Phone ↔ Phone | QR scan interoperability smoke test | `VisionPage` / `VisionViewModel` | — |

## Manual test specifications

- [Phone ↔ Raspberry Pi manual tests](phone_pi.md)
- [Phone ↔ Phone manual tests](two_phones.md)

Each specification contains prerequisites, environment checklists, and the
step-by-step procedures for executing the listed test IDs.
