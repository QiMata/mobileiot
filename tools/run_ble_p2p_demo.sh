#!/usr/bin/env bash
# Per-session BLE P2P demo runner. Wakes both phones, ensures the TestHarness
# APK is built and installed, then runs the BLE P2P hardware tier (peripheral
# on phone A, central on phone B) and prints the JSON stream.
#
# Usage:
#   tools/run_ble_p2p_demo.sh                  # build (if needed) + install + run
#   tools/run_ble_p2p_demo.sh --force-build    # force rebuild before running
#   tools/run_ble_p2p_demo.sh --no-install     # skip install (use whatever's on the phones)
#   tools/run_ble_p2p_demo.sh --skip-build     # don't even attempt a build

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

mapfile -t serials < <(adb devices 2>/dev/null | awk 'NR>1 && $2=="device" {print $1}')
if (( ${#serials[@]} < 2 )); then
    red "need 2 authorized Android phones; got ${#serials[@]}"
    red "connect via USB, enable USB debugging, accept the prompt, then rerun"
    exit 1
fi

cyan "== Wake both phones =="
for s in "${serials[@]:0:2}"; do
    adb -s "$s" shell input keyevent KEYCODE_WAKEUP >/dev/null
    sleep 0.2
    adb -s "$s" shell input swipe 500 1500 500 500 200 >/dev/null
    sleep 0.4
    adb -s "$s" shell svc bluetooth enable >/dev/null
    state=$(adb -s "$s" shell dumpsys bluetooth_manager 2>/dev/null | grep -E 'state[:=]' | head -1 | tr -d '[:space:]')
    printf '  %s  state=%s\n' "$s" "$state"
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
    cyan "== Install on both phones =="
    for s in "${serials[@]:0:2}"; do
        printf '  %s  ' "$s"
        adb -s "$s" install -r "$apk" 2>&1 | tail -1
    done
fi

cyan "== Run BLE P2P hardware tier =="
echo "(Phones can be a few feet apart — BLE range is ~10m line-of-sight.)"
sleep 1
python3 -m harness run --integration ble_p2p --tier hardware --app maui
