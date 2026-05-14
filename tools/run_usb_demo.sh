#!/usr/bin/env bash
# Per-session USB demo runner. Confirms the Pi gadget is bound, wakes the
# Android phone, ensures the TestHarness APK is built and installed, then runs
# the USB hardware tier. Repeatable.
#
# Usage:
#   tools/run_usb_demo.sh                  # build (if needed) + install + run
#   tools/run_usb_demo.sh --force-build    # force rebuild before running
#   tools/run_usb_demo.sh --no-install     # skip install (use whatever's on the phone)
#   tools/run_usb_demo.sh --skip-build     # skip the build step entirely
#   tools/run_usb_demo.sh --skip-pi-check  # don't try to verify the Pi gadget
#
# The Pi gadget needs root, so this script does NOT auto-start it. Run on the Pi:
#   sudo python3 src/pi/usb_demo.py
# (Once per Pi boot. The script tells you the exact command if the gadget is down.)

set -uo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"

force_build=0
do_install=1
do_build=1
skip_pi_check=0

for arg in "$@"; do
    case "$arg" in
        --force-build)    force_build=1 ;;
        --no-install)     do_install=0 ;;
        --skip-build)     do_build=0 ;;
        --skip-pi-check)  skip_pi_check=1 ;;
        -h|--help)
            grep '^#' "$0" | sed 's/^# \?//'
            exit 0
            ;;
        *) printf 'unknown flag: %s\n' "$arg" >&2; exit 2 ;;
    esac
done

cyan()   { printf '\033[36m%s\033[0m\n' "$*"; }
red()    { printf '\033[31m%s\033[0m\n' "$*"; }
yellow() { printf '\033[33m%s\033[0m\n' "$*"; }

cyan "== Inventory =="
python3 -m harness doctor | tail -20 || { red "doctor failed"; exit 1; }

# Pick the first authorized adb device with usb_host capability advertised in inventory.
mapfile -t serials < <(adb devices 2>/dev/null | awk 'NR>1 && $2=="device" {print $1}')
if (( ${#serials[@]} < 1 )); then
    red "no authorized Android phones; connect via USB-OTG-aware port + USB debugging"
    exit 1
fi
serial="${serials[0]}"
printf 'Using phone: %s\n' "$serial"

if (( !skip_pi_check )); then
    cyan "== Pi gadget check =="
    if ! pi_host=$(python3 -c '
import yaml, pathlib, sys
root = pathlib.Path("'"$repo_root"'")
candidates = [root / "devices.local.yaml", root / "devices.yaml"]
data = {}
for c in candidates:
    if c.exists():
        with c.open() as fh:
            data.update(yaml.safe_load(fh) or {})
        break
for d in data.get("devices", []):
    if d.get("kind") == "pi":
        print(d.get("usb_gadget_ip") or d.get("hostname") or "")
        sys.exit(0)
print("")
' 2>/dev/null); then
        yellow "could not resolve pi host from devices.yaml — skipping Pi check"
    elif [[ -z "$pi_host" ]]; then
        yellow "no pi entry in devices.yaml — skipping Pi check"
    else
        if ssh -o ConnectTimeout=4 -o BatchMode=yes "pi@$pi_host" \
            'test -d /sys/kernel/config/usb_gadget/mobileiot/UDC' 2>/dev/null; then
            printf '  pi@%s  gadget bound (UDC present)\n' "$pi_host"
        else
            red "  pi@$pi_host  gadget NOT bound."
            red "  On the Pi, run:  sudo python3 src/pi/usb_demo.py"
            red "  Or pass --skip-pi-check to bypass this check."
            exit 1
        fi
    fi
fi

cyan "== Wake phone =="
state_before=$(adb -s "$serial" shell dumpsys nfc 2>/dev/null | awk -F= '/mScreenState/ {print $2; exit}')
adb -s "$serial" shell input keyevent KEYCODE_WAKEUP >/dev/null
sleep 0.2
adb -s "$serial" shell input swipe 500 1500 500 500 200 >/dev/null
sleep 0.4
state_after=$(adb -s "$serial" shell dumpsys nfc 2>/dev/null | awk -F= '/mScreenState/ {print $2; exit}')
printf '  %s  before=%-12s after=%s\n' "$serial" "$state_before" "$state_after"
if [[ "$state_after" != *"ON_UNLOCKED"* ]]; then
    red "  $serial: still locked — unlock manually (or remove lockscreen) and rerun"
    exit 1
fi

if (( do_build )); then
    if (( force_build )); then
        cyan "== Build (forced) =="
        rm -f harness/runs/.cache/maui-android-*.apk
    else
        cyan "== Build (cached if available) =="
    fi
    python3 -m harness build --app maui --platform android | tail -2
fi

apk=$(ls -t harness/runs/.cache/maui-android-*.apk 2>/dev/null | head -1)
if [[ -z "$apk" ]]; then
    red "no APK found under harness/runs/.cache/ — rerun with --force-build"
    exit 1
fi
printf 'APK: %s (%s)\n' "$apk" "$(du -h "$apk" | cut -f1)"

if (( do_install )); then
    cyan "== Install on phone =="
    printf '  %s  ' "$serial"
    adb -s "$serial" install -r "$apk" 2>&1 | tail -1
fi

cyan "== Run USB hardware tier =="
echo "(Connect the OTG cable Pi→Android now if not already. On first run, accept the USB permission dialog on the phone.)"
sleep 2
python3 -m harness run --integration usb --tier hardware --app maui
