#!/usr/bin/env bash
# Idempotent setup checker for a Mac dev box running the MobileIoT test harness.
#
# Verifies (and where possible installs) every dependency the Android side
# needs. Prints clear `! sudo ...` instructions for the steps that require
# elevation rather than running them itself.
#
# Run from the repo root:
#   tools/setup_dev_mac.sh

set -uo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"

ok=0
warn=0
fail=0

green()  { printf '\033[32m%s\033[0m\n' "$*"; }
yellow() { printf '\033[33m%s\033[0m\n' "$*"; }
red()    { printf '\033[31m%s\033[0m\n' "$*"; }

pass() { green "  ok      $*"; ok=$((ok+1)); }
miss() { yellow "  todo    $*"; warn=$((warn+1)); }
err()  { red    "  fail    $*"; fail=$((fail+1)); }

heading() { printf '\n== %s ==\n' "$*"; }

heading "macOS prerequisites"

if [[ "$(uname -s)" != "Darwin" ]]; then
    err "this script targets macOS (found $(uname -s))"
    exit 1
else
    pass "running on macOS"
fi

if command -v brew >/dev/null 2>&1; then
    pass "homebrew installed ($(brew --version | head -1))"
else
    miss "homebrew not found — install from https://brew.sh"
fi

heading "Python harness"

if command -v python3 >/dev/null 2>&1; then
    py_ver=$(python3 -c 'import sys; print(".".join(map(str, sys.version_info[:3])))')
    pass "python3 $py_ver"
else
    err "python3 not found"
fi

if python3 -c 'import harness' >/dev/null 2>&1; then
    pass "mobileiot_harness installed (python3 -m harness works)"
else
    miss "mobileiot_harness not installed — run: pip3 install -e harness"
fi

heading "Android Platform Tools (adb)"

if command -v adb >/dev/null 2>&1; then
    pass "adb $(adb --version | head -1 | awk '{print $NF}')"
else
    miss "adb not found — run: brew install --cask android-platform-tools"
fi

heading ".NET SDK + MAUI workloads"

if command -v dotnet >/dev/null 2>&1; then
    pass "dotnet $(dotnet --version)"
else
    err "dotnet not found — install .NET 8 SDK from https://dotnet.microsoft.com/download"
fi

if command -v dotnet >/dev/null 2>&1; then
    if dotnet workload list 2>/dev/null | grep -q '^maui-android\b'; then
        pass "maui-android workload installed"
    else
        miss "maui-android workload missing — run:  ! sudo dotnet workload install maui-android"
    fi
fi

heading "NuGet cache ownership"

root_files=0
for d in "$HOME/.nuget" "$HOME/.local/share/NuGet"; do
    if [[ -d "$d" ]]; then
        c=$(find "$d" -user root 2>/dev/null | head -1 | wc -l | tr -d ' ')
        root_files=$((root_files + c))
    fi
done
if [[ "$root_files" -eq 0 ]]; then
    pass "no root-owned files in ~/.nuget or ~/.local/share/NuGet"
else
    miss "root-owned files in NuGet caches — run:  ! sudo chown -R \$(whoami):staff ~/.nuget ~/.local/share/NuGet"
fi

heading "Android SDK platform-34"

xa_sdk="$HOME/Library/Developer/Xamarin/android-sdk-macosx"
if [[ -d "$xa_sdk/platforms/android-34" ]]; then
    pass "android-34 platform present at $xa_sdk"
elif [[ -x "$xa_sdk/cmdline-tools/7.0/bin/sdkmanager" ]]; then
    miss "android-34 missing — run:  yes | $xa_sdk/cmdline-tools/7.0/bin/sdkmanager --sdk_root=$xa_sdk \"platforms;android-34\""
else
    err "no Xamarin Android SDK at $xa_sdk (open Visual Studio for Mac or .NET MAUI installer once to bootstrap)"
fi

heading "Device inventory"

if [[ -f "$repo_root/devices.local.yaml" ]]; then
    pass "devices.local.yaml exists"
else
    miss "no devices.local.yaml — copy/edit from devices.yaml with real ADB serials"
fi

if command -v adb >/dev/null 2>&1; then
    adb_count=$(adb devices 2>/dev/null | awk 'NR>1 && $2=="device"' | wc -l | tr -d ' ')
    if [[ "$adb_count" -gt 0 ]]; then
        pass "$adb_count Android device(s) authorized via adb"
    else
        miss "0 authorized Android devices — connect phones via USB, enable USB debugging, accept the prompt"
    fi
fi

heading "Summary"
printf '  %s ok, %s todo, %s fail\n' "$ok" "$warn" "$fail"
if [[ "$fail" -gt 0 ]]; then
    exit 1
elif [[ "$warn" -gt 0 ]]; then
    echo
    echo "Address the 'todo' items above before running hardware-tier tests."
    exit 2
fi
exit 0
