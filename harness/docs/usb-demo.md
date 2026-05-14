# USB bulk demo (Android OTG ↔ Pi gadget, plus iOS structural)

End-to-end recipe for the `usb` integration. Two role sets:

- **Android-OTG ↔ Pi-gadget** — phone opens a USB bulk endpoint to a Pi
  running the ConfigFS Loopback gadget (`src/pi/usb_demo.py`) and writes /
  reads a probe payload back through it.
- **iOS structural** — POSTs the same `usb-bulk` scenario to verify the
  External Accessory pipeline + Info.plist `UISupportedExternalAccessoryProtocols`
  + DI wiring of `IUsbCommunicator`. With an MFi accessory connected it's a
  full round-trip; without one it returns a clean `Skipped("No USB bulk
  devices found")` — which the test treats as pass.

## What gets exercised

- `harness/integrations/usb.py` — plugin (empty `required_capabilities`;
  per-role filtering happens inside the test body).
- `usb-bulk` scenario — registered in
  `src/MobileIoT/QiMata.MobileIoT.Shared/Services/TestHarness/Scenarios/IntegrationScenarios.cs`.
- Android: `Platforms/Android/UsbCommunicatorAndroid.cs` +
  `UsbPermissionBroadcastReceiver.cs`.
- iOS: `Platforms/iOS/UsbCommunicatoriOS.cs` (External Accessory Framework).
- Pi: `src/pi/usb_demo.py` (libcomposite ConfigFS gadget at VID:PID
  `1d6b:0104`).
- `harness/tests/integrations/test_usb_hw.py` — three tests:
  `test_usb_host_lists_pi_gadget`, `test_usb_bulk_round_trip_pi_gadget`,
  `test_ios_usb_accessory_enumerates`.

## One-time setup (Mac dev box)

Run `tools/setup_dev_mac.sh`. Same prerequisites as the NFC demo (Homebrew,
adb, .NET 8 SDK, MAUI Android workload, Android SDK platform-34, harness
editable install) plus `pymobiledevice3` for the iOS path:

```
pip3 install -e 'harness[ios]'
```

Then on the iPhone, accept *Trust This Computer* and verify
`pymobiledevice3 usbmux list` shows the UDID.

## Per-phone setup

### Android (USB host role)

1. **USB-OTG-aware port** — Android phones differ. Pixel 6+ and most modern
   Samsungs do OTG over the same USB-C port used for charging. OnePlus
   N300 does too. If `lsusb` (run via `adb shell`) doesn't show the Pi
   gadget when plugged in, try a different cable / a known-good OTG adapter.
2. **USB permission** — the first time the phone sees a new VID:PID it
   prompts. Tick *Always use this app for this USB device* before tapping
   OK; otherwise every test run re-prompts. The phone caches the grant per
   VID:PID — re-pairing with a different gadget will re-prompt.
3. **Settings → Developer → Stay awake while charging** — recommended.

### iOS (External Accessory role)

1. **Trust This Computer** on first pair.
2. *(Optional)* MFi accessory — if you've got one (e.g. Redpark cable),
   plug it in via the Lightning / USB-C adapter. Without it, the scenario
   reports `Skipped("No USB bulk devices found")` — which the test accepts
   as pass.

## Per-Pi setup

1. **Enable the `dwc2` overlay** (Pi 4 + Pi Zero W; one-time, requires reboot):
   ```
   sudo sed -i.bak 's/^#\?dtoverlay=dwc2.*/dtoverlay=dwc2/' /boot/config.txt
   ```
2. **Load `libcomposite` at boot**:
   ```
   echo libcomposite | sudo tee -a /etc/modules
   ```
3. **Run the gadget script** (every boot — needs root to write ConfigFS):
   ```
   sudo python3 ~/mobileiot/src/pi/usb_demo.py
   ```
   Confirm with `ls /sys/kernel/config/usb_gadget/mobileiot/UDC` — must
   be non-empty.

## Per-session execution

Wrapped end-to-end:

```
tools/run_usb_demo.sh
```

That script:

1. Prints the doctor inventory.
2. SSHes the Pi (host taken from `devices.yaml`) and bails if
   `/sys/kernel/config/usb_gadget/mobileiot/UDC` doesn't exist, printing
   the exact command to run on the Pi.
3. Wakes the Android phone, refuses to run if still locked.
4. Builds the MAUI APK if not cached (`python3 -m harness build --app maui
   --platform android`).
5. Installs on the phone.
6. Runs `python3 -m harness run --integration usb --tier hardware --app maui`.

Flags:

- `--force-build` — wipe the cache and rebuild before running.
- `--no-install` — skip the install step.
- `--skip-build` — skip the build step entirely.
- `--skip-pi-check` — skip the SSH probe (for ad-hoc runs against a
  non-standard Pi).

Manual equivalent:

```
ssh pi@<pi-ip> 'sudo python3 ~/mobileiot/src/pi/usb_demo.py' &
python3 -m harness doctor
python3 -m harness build --app maui --platform android
adb -s <serial> install -r harness/runs/.cache/maui-android-*.apk
python3 -m harness run --integration usb --tier hardware --app maui
```

Expected result: 3 passed (Android list, Android round-trip, iOS structural).
The iOS test's `passed` body may show `status=skipped, reason="No USB bulk
devices found"` inside `result` — that's the intentional structural-only
behavior, not a test failure.

## Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| Test skipped: `Pi gadget not bound — run …` | `/sys/kernel/config/usb_gadget/mobileiot/UDC` missing | on the Pi: `sudo python3 src/pi/usb_demo.py` (then keep it foregrounded or `tmux new-session -d`) |
| Test skipped: `USB permission not yet granted on phone` | first-run pairing dialog wasn't ack'd, or denied | re-plug OTG cable, tap *OK* on the dialog (tick *Always use*) |
| `status=skipped, reason="Failed to open USB device"` | permission revoked or VID:PID mismatch | re-plug cable; in extreme cases `adb shell pm clear com.qimata.mobileiot` then accept the dialog again |
| Phone enumerates 0 devices but Pi gadget is up | cable doesn't carry data (charge-only) or no OTG support | swap to a known-good USB-C cable + a real OTG adapter |
| iOS reports `status=skipped` reason="No USB bulk devices found" | no MFi accessory connected — expected | not a failure; the test's two-mode assertion treats this as pass |
| `dotnet build` errors | see `nfc-demo.md` troubleshooting | same fixes apply |

## Implementation notes (for future maintainers)

- The plugin declares `required_capabilities=set()`. The harness gate
  (`evaluate_hardware_requirement` in `harness/fidelity.py`) applies the
  same caps to every role in the marker, so it can't distinguish
  "`usb_host` on Android" from "`serial_gadget` on Pi". The hardware test
  filters per role inside the test body — same idiom as `test_nfc_hw.py:26-30`.
- `UsbBulkScenario` returns `readHex` so the test can assert the echoed
  payload starts with the probe bytes, not just that *some* bytes came
  back. Pre-enhancement runs (without `readHex`) still pass on count.
- iOS USB without an MFi accessory is *structurally* tested only: this is
  intentional per the scoping decision. A future enhancement could add an
  `mfi_accessory` capability and a dormant round-trip test path.
- No `usb-serial` test today — that scenario needs a CDC/FTDI dongle, not
  a loopback gadget. Future work.
