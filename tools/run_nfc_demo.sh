#!/usr/bin/env bash
# Per-session NFC demo runner. Wakes both phones, ensures the TestHarness APK
# is built and installed, then runs the NFC hardware tier and prints the JSON
# stream. Repeatable — safe to invoke as many times as you like.
#
# Usage:
#   tools/run_nfc_demo.sh                  # build (if needed) + install + run
#   tools/run_nfc_demo.sh --force-build    # force rebuild before running
#   tools/run_nfc_demo.sh --no-install     # skip install (use whatever's on the phones)
#   tools/run_nfc_demo.sh --skip-build     # build-related path only when already cached

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

# Pull adb serials of online Android phones from doctor JSON (single source of truth).
mapfile -t serials < <(adb devices 2>/dev/null | awk 'NR>1 && $2=="device" {print $1}')
if (( ${#serials[@]} < 2 )); then
    red "need 2 authorized Android phones; got ${#serials[@]}"
    red "connect via USB, enable USB debugging, accept the prompt, then rerun"
    exit 1
fi

cyan "== Wake both phones =="
for s in "${serials[@]:0:2}"; do
    state_before=$(adb -s "$s" shell dumpsys nfc 2>/dev/null | awk -F= '/mScreenState/ {print $2; exit}')
    adb -s "$s" shell input keyevent KEYCODE_WAKEUP >/dev/null
    sleep 0.2
    adb -s "$s" shell input swipe 500 1500 500 500 200 >/dev/null
    sleep 0.4
    state_after=$(adb -s "$s" shell dumpsys nfc 2>/dev/null | awk -F= '/mScreenState/ {print $2; exit}')
    printf '  %s  before=%-12s after=%s\n' "$s" "$state_before" "$state_after"
    if [[ "$state_after" != *"ON_UNLOCKED"* ]]; then
        red "  $s: still locked — unlock manually (or remove lockscreen) and rerun"
        exit 1
    fi
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

cyan "== Run NFC hardware tier =="
echo "(Position phones back-to-back NOW — NFC antennas roughly aligned with the cameras.)"
sleep 2
python3 -m harness run --integration nfc --tier hardware --app maui
