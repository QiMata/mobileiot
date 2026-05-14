# BLE central demo (phone ↔ Pi peripheral)

End-to-end recipe for the `ble_central` integration: one phone (Android or iOS) acts as the BLE central and reads the DHT22 temperature/humidity service plus toggles the LED characteristic on a Pi running `bluetoothle_demo.py` (bluezero "PiDHTSensor").

## What gets exercised

- `harness/integrations/ble_central.py` — plugin (`ble` capability on `central` + `peripheral` roles).
- `ble-gatt` scenario — already shipped (`Scenarios/IntegrationScenarios.cs:BleGattScenario`).
- `BluetoothService.cs` + `BleDemoService.cs` — Plugin.BLE-backed central; works on both Android and iOS.
- `harness/tests/integrations/test_ble_central_hw.py::test_ble_central_android_reads_pi_gatt` and `::test_ble_central_ios_reads_pi_gatt`.
- `src/pi/bluetoothle_demo.py` — bluezero peripheral, launched in a `tmux` session inside the test.

## One-time setup (Mac dev box)

Same baseline as the NFC demo (`tools/setup_dev_mac.sh`):

1. **Homebrew**, **Android Platform Tools (adb)**, **Python harness** (`pip3 install -e harness`), **.NET 8 SDK**.
2. **MAUI workloads** — Android required; iOS required only for the iOS variant: `sudo dotnet workload install maui-android maui-ios`.
3. **pymobiledevice3** for iOS device trust + usbmux: `pipx install pymobiledevice3` (or `pip3 install pymobiledevice3`).
4. **`devices.local.yaml`** — gitignored per-machine overrides for the inventory.

## Per-phone setup

### Android

1. Enable **USB debugging** and authorize the Mac.
2. Bluetooth: the test runs `svc bluetooth enable` before each iteration; ensure no system-level toggle is blocking it.
3. Remove the lockscreen or set to swipe-only (Plugin.BLE scans need a foreground activity on some OEM stacks).

### iOS device trust

1. Cable-connect the iPhone to the Mac.
2. On the phone: tap **Trust This Computer**, enter the passcode.
3. Verify the UDID appears in `pymobiledevice3 usbmux list`. Copy that UDID into `devices.local.yaml` under the `iphone` entry.
4. **Allow Bluetooth permission** the first time the app launches (the iOS system prompt fires from `CBCentralManager` init). If the test reports a `scenario skipped: bluetooth not authorized` body, open the app once manually and grant the prompt.

## Per-Pi setup

The Pi runs bluezero, which needs:

- `sudo apt-get install python3-pip libdbus-glib-1-dev libgirepository1.0-dev`
- `pip3 install -r ~/mobileiot/src/pi/requirements.txt` (one-time, includes `bluezero`)
- BlueZ ≥ 5.50 (standard on Bookworm).

No manual launch step — the test invokes:

```
tmux new-session -d -s pi-ble-demo \
  'python3 ~/mobileiot/src/pi/bluetoothle_demo.py --device-name PiDHTSensor 2>&1 | tee /tmp/pi-ble-demo.log'
```

and kills the session in `finally`. Re-run with no manual cleanup.

## Per-session execution

```
tools/run_ble_central_demo.sh
```

That script:
1. Prints the doctor inventory.
2. Wakes the first authorised Android phone and turns on the BT radio.
3. Builds the MAUI APK if not cached.
4. Installs the APK.
5. Runs `python3 -m harness run --integration ble_central --tier hardware --app maui`.

Flags: `--force-build`, `--no-install`, `--skip-build` (same shape as `run_nfc_demo.sh`).

Manual equivalent:
```
python3 -m harness doctor
python3 -m harness build --app maui --platform android
adb -s <serial> install -r harness/runs/.cache/maui-android-*.apk
python3 -m harness run --integration ble_central --tier hardware --app maui
```

Expected: `1 passed` (Android variant). The iOS variant only runs when `roles=["ios","pi"]` matches an online iPhone with `ble` capability.

## Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| `Pi BLE peripheral tmux start failed` | bluezero not installed or BlueZ broken | `pip3 install bluezero` on the Pi; `systemctl status bluetooth` |
| `ble-gatt scenario skipped: BLE device 'PiDHTSensor' not found` | advertise not running yet, or wrong name | check `tmux ls` on Pi; verify `--device-name` matches the scenario arg |
| iOS scenario returns `skipped: bluetooth not authorized` | first-launch system prompt was declined | open the app on-device once, grant Bluetooth permission, re-run |
| Test hangs ~30s then times out | bluez crashed mid-run, or RSSI too low | restart bluetoothd on Pi (`sudo systemctl restart bluetooth`); move phone closer |
| `dotnet build` fails for iOS | Xcode CLT or `maui-ios` workload missing | `xcode-select --install`; `sudo dotnet workload install maui-ios` |

## Implementation notes

- The Pi peripheral's UUIDs are defined in `BleUuidConfig.cs`; the Android/iOS central reads `temp`, `humidity`, and `led` by those UUIDs.
- Plugin.BLE 3.x covers the central role on both platforms — no native CoreBluetooth code needed for this test.
- Phone-as-peripheral (the inverse) is covered by `ble-p2p-demo.md`.
