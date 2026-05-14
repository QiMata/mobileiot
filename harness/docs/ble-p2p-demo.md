# BLE peer-to-peer demo (phone ↔ phone)

End-to-end recipe for the `ble_p2p` integration: one phone advertises a custom GATT service via `IBleAdvertiseService`, the other scans-connects-writes via `IBleP2PCentralService`. Four variants run when the matching role pairs are available — Android↔Android, iOS↔Android, Android↔iOS, and (in future) iOS↔iOS.

## What gets exercised

- `harness/integrations/ble_p2p.py` — plugin (`ble` capability on two roles, default Android+Android; iOS variants override per-test).
- `ble-peripheral` and `ble-p2p-central` scenarios — new in `Scenarios/IntegrationScenarios.cs`.
- `IBleAdvertiseService` + `IBleP2PCentralService` (`Services/Interfaces/`) — phone-side surfaces.
- `BleAdvertiseService_Android` (BluetoothLeAdvertiser + GattServer) and `BleAdvertiseService_iOS` (native CBPeripheralManager wrapper).
- `BleP2PCentralService_Android` / `BleP2PCentralService_iOS` — Plugin.BLE-backed centrals.
- `harness/tests/integrations/test_ble_p2p_hw.py` — four tests for the cross-role matrix.

## One-time setup (Mac dev box)

Same as the BLE central demo. `pipx install pymobiledevice3` is required for the iOS legs.

## Per-phone setup

### Android (either role)

1. **USB debugging** authorised.
2. **Bluetooth on**; the test re-enables via `svc bluetooth enable`.
3. **Remove the lockscreen** (Plugin.BLE central path needs a foreground activity on Samsung).
4. On first launch, the app may prompt for `BLUETOOTH_CONNECT` / `BLUETOOTH_SCAN`; grant them. The manifest already declares all four BT permissions (`SCAN`, `CONNECT`, `ADVERTISE`, `PRIVILEGED`).

### iOS device trust

1. Cable + **Trust This Computer**.
2. `pymobiledevice3 usbmux list` should show the UDID; update `devices.local.yaml`.
3. **Bluetooth permission**: granted on first launch via the `NSBluetoothAlwaysUsageDescription` prompt. If declined, open the app and grant manually via Settings → MobileIoT → Bluetooth.
4. The Info.plist now declares `bluetooth-peripheral` background mode and `bluetooth-le` device-capability requirement (rejects install on non-BLE iPhones — all iPhone 4S+ pass).

## Per-session execution

### Two-Android run

```
tools/run_ble_p2p_demo.sh
```

That script: prints doctor, wakes both phones, builds the APK (cached), installs on both, runs the BLE P2P hardware tier. Flags identical to `run_nfc_demo.sh`.

### Mixed Android + iOS

The mixed variants run automatically when both kinds of phone are online and `ble`-capable. No separate wrapper script — just `python3 -m harness run --integration ble_p2p --tier hardware --app maui`. The pytest collector picks up the right tests via the per-test `roles=[...]` marker.

For just one direction:

```
pytest harness/tests/integrations/test_ble_p2p_hw.py::test_ble_p2p_ios_peripheral_android_central -v
```

## Code flow

1. `pytest_collection_modifyitems` evaluates the `@pytest.mark.hardware` marker; skip if either role/cap pair is unavailable.
2. The test resolves Device/Transport pairs (via the standard fixtures for the two-Android case, or via `inventory.find()` for mixed variants).
3. `app_builder.build("maui", "android")` (and `"ios"` for the iOS leg) — cached by git SHA + dirty-tree hash.
4. Install + launch on each phone. Port-forward `47821→47821` (peripheral) and `47822→47821` (central).
5. `_wait_health` on both forwarded ports.
6. **Background thread**: POST `/scenario/ble-peripheral` on the peripheral phone with a random `serviceUuid` + random 16-byte `payloadHex`. The `BlePeripheralScenario` calls `IBleAdvertiseService.StartAsync` and awaits `CharacteristicWritten` via a TCS.
7. Sleep 1.0 s for advertise warmup.
8. **Synchronous**: POST `/scenario/ble-p2p-central` on the central phone. `BleP2PCentralScenario` calls `IBleP2PCentralService.ConnectAndExchangeAsync` which scans by name, connects, writes the payload to the service's first characteristic, optionally reads back, returns the bytes.
9. Pytest asserts `central.result.ok is True`, `peripheral.result.served is True`, and `peripheral.result.centralBytesReceived` (hex) equals the sent `payloadHex`.
10. `try/finally` unforwards ports and tears down drivers.

## Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| `peripheral.served = false`, no central error | central never found the peripheral by name | confirm `_advertiser.SetName(deviceName)` worked — some OEMs (Samsung) silently rename; check `dumpsys bluetooth_manager \| grep -i name` |
| `peripheral` returns `BLE advertise did not start within 5s` | `OnStartFailure` fired (BLE Power Save, advertise queue full) | toggle BT off/on, kill other BLE-advertising apps, retry |
| iOS peripheral returns `skipped: CBPeripheralManager state is Unauthorized` | user declined the Bluetooth prompt | Settings → MobileIoT → Bluetooth → ON |
| Mixed variant `roles=["ios","android"]` is skipped immediately | inventory has only one kind online | `python3 -m harness doctor` to confirm both an `ios` and `android` device are `online` and `ble`-capable |
| Test hangs on iOS launch | `pymobiledevice3 apps launch` cold-starts slow on first run | bump `wait_ready(timeout=...)` to 90s, or pre-launch the app once via Xcode |
| Android central writes but peripheral never gets `CharacteristicWritten` | wrong characteristic UUID (different OEM stacks autorename) | the impl uses a fixed `00002a37-…` UUID for the test characteristic; check the central is targeting the same one — the scenario uses `service.GetCharacteristicsAsync()[0]`, so the first char wins |

## Implementation notes (for future maintainers)

- The `BleAdvertiseService_iOS` is a native `NSObject` subclass implementing `ICBPeripheralManagerDelegate`. State transitions are async: `StartAsync` returns when `peripheralManagerDidUpdateState:` fires with `PoweredOn` and `AddService` + `StartAdvertising` complete. A 8s timeout produces a clean InvalidOperationException so `ScenarioBase` returns `Skipped`.
- The Android advertise path wires `AdvertiseCallback.OnStartFailure` into a TCS — failures surface as `skipped` within 5s instead of hanging 15s for the scenario duration.
- For the iOS-peripheral test variants, `IBleAdvertiseService` must be registered on iOS — the new `MauiProgram.cs` block does that under `#if TEST_HARNESS`. If DI isn't registered, `Get<IBleAdvertiseService>()` throws and `ScenarioBase.cs:48-51` catches it and returns `Skipped`.
- Plugin.BLE 3.x is reused for the central role on both platforms; there's no separate native `CBCentralManager` code in this suite.
