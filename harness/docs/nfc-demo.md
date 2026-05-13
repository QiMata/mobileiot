# NFC HCE ↔ Reader-mode demo (two Androids)

End-to-end recipe for the `nfc` integration: phone A emulates a Type-4 NFC tag, phone B reads it via `NfcAdapter.enableReaderMode`, the harness asserts the payload round-trips. The two phones must be physically back-to-back during the ~10s reader scan window.

## What gets exercised

- `harness/integrations/nfc.py` — plugin (needs `nfc` capability on two `android` roles).
- `nfc-hce-emulate` and `nfc-reader` scenarios — registered in `src/MobileIoT/QiMata.MobileIoT.Shared/Services/TestHarness/Scenarios/IntegrationScenarios.cs`.
- `HceApduService` + `NfcReaderService_Android` — Android-platform implementations under `src/MobileIoT/QiMata.MobileIoT/Platforms/Android/`.
- `harness/tests/integrations/test_nfc_hw.py::test_nfc_hce_payload_round_trips` — orchestrates both phones.

Why HCE + reader mode and not Android Beam: Beam was deprecated in Android 10 and removed in Android 14. HCE + reader is supported on every Android 4.4+ phone, including the OnePlus Nord N300 (Android 12) and Samsung Note 8 (Android 9) the lab has.

## One-time setup (Mac dev box)

Run `tools/setup_dev_mac.sh`. It checks (and tells you how to install) every dependency, printing `! sudo ...` for the steps that need elevation. The full set:

1. **Homebrew** (https://brew.sh).
2. **Android Platform Tools (adb)**:
   ```
   brew install --cask android-platform-tools
   ```
3. **Python harness** (editable install):
   ```
   pip3 install -e harness
   ```
4. **.NET 8 SDK** (https://dotnet.microsoft.com/download).
5. **MAUI Android workload** — requires sudo on macOS because the .NET SDK lives in `/usr/local/share/dotnet`:
   ```
   ! sudo dotnet workload install maui-android
   ```
6. **NuGet cache ownership** — the workload install can leave root-owned files behind. If `setup_dev_mac.sh` flags this:
   ```
   ! sudo chown -R $(whoami):staff ~/.nuget ~/.local/share/NuGet
   ```
7. **Android SDK platform-34** — the MAUI csproj targets API 34:
   ```
   yes | ~/Library/Developer/Xamarin/android-sdk-macosx/cmdline-tools/7.0/bin/sdkmanager \
     --sdk_root=$HOME/Library/Developer/Xamarin/android-sdk-macosx "platforms;android-34"
   ```
8. **`devices.local.yaml`** — gitignored per-machine overrides for the inventory. Match by `id` (e.g. `pixel-a`, `pixel-b`) and fill in real `adb_serial`s.

## Per-phone setup

Once per phone, done manually on the device:

1. **Enable USB debugging**: Settings → About phone → tap *Build number* 7 times → Developer options → toggle *USB debugging*.
2. **Authorize this Mac**: plug in the phone, accept the *Allow USB debugging from this computer?* prompt. Tick *Always allow* to make it persistent.
3. **Remove the lockscreen** (or set it to *None* / *Swipe*) **and** turn on *Stay awake while charging* (in Developer options). Reader-mode requires a foreground activity, which a locked screen blocks. `tools/run_nfc_demo.sh` will refuse to run if either phone is still locked after a wake attempt.
4. **Confirm NFC is on**: Settings → Connections (Samsung) / Connection & sharing (OnePlus) → NFC. The harness also flips it on via `svc nfc enable`, but verify the toggle isn't behind a "show advanced" sub-menu.

Then on the Mac, `adb devices` should list both as `device` (not `unauthorized`). Drop their serials into `devices.local.yaml`:

```yaml
devices:
  - id: pixel-a
    kind: android
    adb_serial: <real-serial-of-phone-A>
    capabilities: [ble, nfc, wifi_direct, usb_host, audio_out, camera]
  - id: pixel-b
    kind: android
    adb_serial: <real-serial-of-phone-B>
    capabilities: [ble, nfc, wifi_direct]
```

Verify with `python3 -m harness doctor` — both phones should be `online`.

## Per-session execution

Wrapped end-to-end:

```
tools/run_nfc_demo.sh
```

That script:
1. Prints the doctor inventory.
2. Wakes both phones via `KEYCODE_WAKEUP` + a swipe-up gesture, bails if either is still locked.
3. Builds the MAUI TestHarness APK if not cached (`python3 -m harness build --app maui --platform android` — caches by git SHA + dirty-tree hash).
4. Installs the APK on both phones (`-r` reinstall).
5. Reminds you to position the phones back-to-back, then runs `python3 -m harness run --integration nfc --tier hardware --app maui`.

Flags:
- `--force-build` — wipe the cache and rebuild before running.
- `--no-install` — skip the install step (use whatever APK is already on the phones).
- `--skip-build` — assume the cache is good enough; don't even attempt a build.

Manual equivalent (each line is independent):

```
python3 -m harness doctor
python3 -m harness build --app maui --platform android
adb -s <serial-A> install -r harness/runs/.cache/maui-android-*.apk
adb -s <serial-B> install -r harness/runs/.cache/maui-android-*.apk
python3 -m harness run --integration nfc --tier hardware --app maui
```

Expected result: `2 passed, 18 deselected`. The radios-on test (`test_nfc_radios_ready_on_two_phones`) and the round-trip test (`test_nfc_hce_payload_round_trips`) both green.

## Physical positioning

NFC range is ~3 cm. The two antennas need to be physically overlapping, not just the phones touching.

- **OnePlus Nord N300 (GN2200)**: antenna sits in the upper third of the back, near the cameras.
- **Samsung Galaxy Note 8 (SM-N950U)**: antenna is in the upper-middle of the back.

Easiest alignment: lay one phone face-down, place the other face-up on top, line up the camera modules. You'll hear the Note 8's haptic tick on a successful tag detect.

## Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| `dotnet build` fails with `NETSDK1147: maui-android workload` | workload not installed | `! sudo dotnet workload install maui-android` |
| `dotnet build` fails with `Access denied … ~/.nuget` | sudo dotnet left root-owned cache files | `! sudo chown -R $(whoami):staff ~/.nuget ~/.local/share/NuGet` |
| `error XA5207: Could not find android.jar for API level 34` | platform-34 missing | sdkmanager command in step 7 above |
| `Build FAILED … Assets file … doesn't have a target for 'net8.0'` | stale `obj/` from a previous failed build | `find src/MobileIoT -name obj -type d -exec rm -rf {} +` |
| App crashes immediately after `adb install`, logcat shows `monodroid: No assemblies found … Fast Deployment` | APK built without `EmbedAssembliesIntoApk=true` | the harness now sets this by default; rebuild via `python3 -m harness build` |
| Reader scenario returns `status: skipped, reason: "no foreground activity"` | `MainActivity.Current` is null — app isn't in foreground | demo script waits for it; if you see this manually, give the app 2–3s after `am start` |
| Reader scenario returns `status: passed, ok: false, responseHex: null` | the reader was active but never saw a tag in the 10s window | phones not close enough or antennas misaligned — try aligning the camera bumps |
| `mScreenState=ON_LOCKED` after wake | lockscreen requires PIN / biometric that adb can't bypass | remove the lockscreen on that phone for testing |
| HCE service not in `dumpsys nfc \| grep RegisteredAidCache` | APK install didn't pick up the manifest service, or app was never launched | force-stop + relaunch, then `adb shell dumpsys nfc` again |

## Implementation notes (for future maintainers)

- Build flags baked into `harness/harness/app/builder.py` for Android: `-p:TargetFrameworks=<single-tfm>` (lets a dev box that only has `maui-android` build the multi-targeted csproj), `-p:EmbedAssembliesIntoApk=true`, `-p:AndroidPackageFormat=apk` (no Fast Deployment — the APK is self-contained).
- The HCE AID is `F0010203040506` (private-use range, no SE routing collisions on Samsung). Defined in `Platforms/Android/Resources/xml/apduservice.xml`.
- The harness HTTP host (`HarnessHttpHost`, 127.0.0.1:47821) is always-on for TEST_HARNESS Android builds. The env-var gate from `MauiProgram.IsTestHarnessEnabled` doesn't work on Android because `am start` can't set process environment variables.
- `MainActivity.Current` is the only way the reader-mode service can get an `Activity` to pass to `NfcAdapter.enableReaderMode`. It's set in `OnCreate` and cleared in `OnDestroy`.
