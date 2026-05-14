#!/usr/bin/env bash
# Per-session BLE-central demo runner. Wakes one phone, ensures the TestHarness
# APK is built and installed, starts the Pi BLE peripheral in a tmux session,
# then runs the BLE-central hardware tier and prints the JSON stream.
#
# Usage:
#   tools/run_ble_central_demo.sh                  # build (if needed) + install + run
#   tools/run_ble_central_demo.sh --force-build    # force rebuild before running
#   tools/run_ble_central_demo.sh --no-install     # skip install (use whatever's on the phone)
#   tools/run_ble_central_demo.sh --skip-build     # build-related path only when already cached

set -uo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"

force_build=0
do_install=1
do_build=1

for arg in "$@"; do
    case "$arg" in
        --force-build) force_build=1 ;;
        --no-install)  do_install=0 ;;
        --skip-build)  do_build=0 ;;
        -h|--help)
            grep '^#' "$0" | sed 's/^# \?//'
            exit 0
            ;;
        *) printf 'unknown flag: %s\n' "$arg" >&2; exit 2 ;;
    esac
done

cyan() { printf '\033[36m%s\033[0m\n' "$*"; }
red()  { printf '\033[31m%s\033[0m\n' "$*"; }

cyan "== Inventory =="
python3 -m harness doctor | tail -20 || { red "doctor failed"; exit 1; }

# Pull adb serials of online Android phones (need at least one).
mapfile -t serials < <(adb devices 2>/dev/null | awk 'NR>1 && $2=="device" {print $1}')
if (( ${#serials[@]} < 1 )); then
    red "need 1 authorized Android phone; got ${#serials[@]}"
    red "connect via USB, enable USB debugging, accept the prompt, then rerun"
    exit 1
fi

cyan "== Wake phone =="
for s in "${serials[@]:0:1}"; do
    adb -s "$s" shell input keyevent KEYCODE_WAKEUP >/dev/null
    sleep 0.2
    adb -s "$s" shell input swipe 500 1500 500 500 200 >/dev/null
    sleep 0.4
    adb -s "$s" shell svc bluetooth enable >/dev/null
done

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
    for s in "${serials[@]:0:1}"; do
        printf '  %s  ' "$s"
        adb -s "$s" install -r "$apk" 2>&1 | tail -1
    done
fi

cyan "== Run BLE-central hardware tier =="
echo "(The test will start bluetoothle_demo.py on the Pi via tmux automatically.)"
sleep 1
python3 -m harness run --integration ble_central --tier hardware --app maui
