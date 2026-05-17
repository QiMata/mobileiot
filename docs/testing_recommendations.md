# Testing Expansion Roadmap

This document consolidates the current guidance for strengthening automated testing across the MobileIoT demos. The recommendations are grouped by platform and layer so you can track progress systematically.

## .NET MAUI application

### View-model coverage
- Exercise command/property flows for **UsbViewModel**, **SerialDemoViewModel**, **AudioDemoViewModel**, **BleViewModel**, **MainViewModel**, **VisionViewModel**, **NfcPageViewModel**, **NfcP2PViewModel**, and **P2pViewModel**. Include both happy paths and failure scenarios such as empty device lists, denied permissions, or non-open ports so that observable properties and logs change as expected.
- Expand the existing USB tests beyond the single success assertion by adding xUnit theories that cover `ConnectAsync` and `SendPingAsync` branches (no devices, short reads, failure to open). Verify that the ping command writes the expected buffers, appends receive logs when the port is open, and remains quiet when `IUsbDeviceService.IsOpen` is false.
- Validate that `SerialDemoViewModel` logs state transitions correctly during connect/disconnect sequences, cancellation flows, and LED toggling edges when paired with mocked services.
- Cover `AudioDemoViewModel` status transitions (`Idle → Listening… → Stopped`) and confirm that events from mocked audio services update bindable properties.

### Service orchestration
- Design richer unit tests around the audio/BLE/serial orchestration layers by mocking `IAudioManager`, `IAudioRecorder`, and `IBluetoothService`. Assert that `AudioModemService` requests microphone permission, starts idempotently, cancels cleanly, and raises `DataReceived` when the decoder emits text; ensure `BleDemoService` flips its `_ledState` flag and relays sensor readings; and verify that serial services log transitions and handle cancellation edge cases without real hardware.
- Add tests for `AudioModemService` using fake `IAudioRecorder`/`IAudioDecoder` implementations to confirm the decode loop behavior, microphone permissions, and event propagation. Consider injecting recorder/decoder dependencies to simplify mocking.
- Abstract `BluetoothService` behind injectable interfaces (wrapping CrossBluetoothLE) so you can simulate adapter discovery, unsuccessful characteristic discovery, sensor read failures, and non-zero result codes. Pair these with `BleDemoService` tests that ensure LED toggling and sensor aggregation behave correctly.
- Strengthen `RootMeanSquareAudioDecoder` tests with cases for cancellation, very short or odd-length buffers, near-threshold amplitudes, and buffers smaller than a sample to guarantee guard clauses operate as intended.
- For QR scanning, extract permission checks and navigation callbacks into testable collaborators so you can assert camera permission fallbacks and modal behavior without shell dependencies.

### Integration and contract tests
- Create contract tests that load `src/shared/ble_constants.json`, the Python BLE server definitions, and the MAUI `BleUuidConfig` resource to keep UUIDs in sync.
- Build end-to-end smoke tests that feed prerecorded audio samples (mirroring `audio_demo` output) through `AudioModemService` to ensure the decoder raises the expected messages.
- Add entry-point smoke tests for `bluetoothle_demo.py` and `thread_demo.py` so the runnable scripts stay covered even though their internals now live in helper modules.
- Script BLE adapter simulations to validate data exchange between the app and Pi utilities using the shared UUIDs.

## Raspberry Pi utilities (Python)

### Serial demo
- Broaden coverage beyond `handle_command` to include the command loop (`SerialDemo.run`), GPIO lifecycle, serial error handling, and Unicode decoding safeguards. Stub `serial.Serial` and `RPi.GPIO` as in the existing test, then drive `serial_connection`, `read_serial_lines`, and `run` to assert behavior for empty reads, undecodable payloads, exceptions, and cleanup.
- Verify logging updates and LED toggling logic when commands succeed or fail, mirroring the .NET view-model assertions.

### BLE demo
- Add targeted tests for helper functions in `bluetoothle_demo.py`: ensure `load_ble_constants` rejects missing keys, characteristic callbacks respect cached sensor values, and LED writes toggle the expected GPIO pins. Mock `Adafruit_DHT`, `GPIO`, and `adapter.Adapter.available()` to avoid hardware dependencies.
- Reuse the lightweight stubbing strategy from the serial demo tests to validate JSON configuration loading, byte encoding helpers, and sensor retry caching windows.
- Encapsulate Bluezero adapter discovery and GPIO access behind thin wrappers so tests can simulate discovery, read/write errors, and disconnections without the real plugins.
- If the beacon example remains, factor its construction into a helper so you can assert UUID/major/minor packing and TX power bytes.

### Audio demo
- Write fast unit tests that monkeypatch filesystem reads (`/sys/class/thermal/thermal_zone0/temp`) and intercept `subprocess.run` to confirm telemetry formatting, minimodem command construction, and loop timing boundaries.
- Ensure `read_cpu_temp` tolerates temporary files or missing sensors and that `transmit` assembles the correct minimodem arguments without invoking the real utility.

### Shared process improvements
- Organize Python tests by module (mirroring the `pi` package structure) and adopt `pytest` as a consistent runner while maintaining compatibility with existing unittest modules.
- Provide fixtures or sample payloads (BLE UUIDs, audio buffers) so regression tests can validate encoding/decoding logic deterministically.

## Cross-cutting process and tooling
- Configure CI to run both `dotnet test` and `python -m pytest`/`python -m unittest` so regressions in either stack are caught early.
- Generate coverage reports for the .NET and Python suites to measure progress as you expand tests.
- Publish minimal hardware-in-the-loop checklists for manual verification when GPIO/BLE peripherals are available.
- Document the usb demo placeholder with a smoke test that ensures the explanatory module text stays accurate and imports cleanly.

These steps will broaden coverage across UI logic, service orchestration, and the Raspberry Pi utilities, resulting in safer evolution of the MobileIoT demos.
