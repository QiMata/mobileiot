"""Shared phone-side helpers for hardware-tier integration tests.

Extracted from `test_nfc_hw.py` so the NFC and BLE suites can share the same
screen-wake / health-poll primitives.

Three helpers:

- `wake_screen(transport, label)` — wake the device and dismiss a swipe-only
  lockscreen. PIN/biometric locks can't be bypassed via adb; tests that need
  a foreground activity surface that as a skip.
- `wait_health(local_port, label)` — poll `GET /health` on a forwarded port
  until the in-app TestHarness HTTP host reports `{"ok": true}`.
- `wait_activity_resumed(device, local_port, label, ...)` — NFC-specific:
  probe the `nfc-reader` scenario until it stops reporting "no foreground
  activity", meaning MainActivity.Current has attached. Generalised here in
  case other Android-foreground-dependent scenarios reuse it.
"""
from __future__ import annotations

from collections.abc import Sequence
import time

import pytest
import requests


HCE_AID_HEX = "F0010203040506"


def android_devices_with_capability(android_phones, capability: str):
    """Return online Android devices that advertise the requested capability."""
    return [
        (device, transport)
        for device, transport in android_phones
        if capability in device.capabilities
    ]


def require_android_devices_with_capability(
    android_phones,
    capability: str,
    *,
    minimum: int = 2,
):
    """Return matching Android devices or skip with a stable, descriptive message."""
    matches = android_devices_with_capability(android_phones, capability)
    if len(matches) < minimum:
        phone_label = "phone" if minimum == 1 else "phones"
        pytest.skip(
            f"need {minimum} Android {phone_label} with {capability} capability online; "
            f"got {len(matches)}"
        )
    return matches


def wake_screen(transport, label: str) -> None:
    """Wake the device and dismiss a swipe-only lockscreen.

    PIN/biometric locks can't be bypassed via adb — callers will surface that
    as a foreground-activity error from a downstream scenario.
    """
    state_line = transport.shell("dumpsys nfc | grep mScreenState").stdout
    if "ON_UNLOCKED" in state_line:
        return
    transport.shell(["input", "keyevent", "KEYCODE_WAKEUP"], timeout=5.0)
    transport.shell(["input", "swipe", "500", "1500", "500", "500", "200"], timeout=5.0)
    final = transport.shell("dumpsys nfc | grep mScreenState").stdout.strip()
    if "ON_UNLOCKED" not in final:
        pytest.skip(
            f"{label}: screen not unlocked after wake (state={final!r}); "
            "remove the lockscreen or unlock manually before re-running"
        )


def wait_health(local_port: int, *, label: str, timeout: float = 30.0) -> None:
    """Poll the in-app harness `/health` endpoint until it reports OK."""
    deadline = time.monotonic() + timeout
    last: Exception | None = None
    while time.monotonic() < deadline:
        try:
            r = requests.get(f"http://127.0.0.1:{local_port}/health", timeout=1.5)
            if r.ok and r.json().get("ok"):
                return
        except Exception as ex:  # noqa: BLE001
            last = ex
        time.sleep(0.5)
    raise TimeoutError(
        f"{label}: TestHarness host not ready at :{local_port}/health ({last})"
    )


def wait_activity_resumed(
    device,
    local_port: int,
    *,
    label: str,
    timeout: float = 15.0,
    aid_hex: str = HCE_AID_HEX,
) -> None:
    """Probe the NFC reader scenario until it stops reporting 'no foreground activity'.

    The MAUI harness HTTP host starts during application init; MainActivity
    finishes a tick later, and `NfcAdapter.enableReaderMode` needs an
    Activity. Used by NFC tests; available here in case BLE-on-foreground
    scenarios ever need the same gate.
    """
    deadline = time.monotonic() + timeout
    last_reason: str | None = None
    while time.monotonic() < deadline:
        try:
            r = requests.post(
                f"http://127.0.0.1:{local_port}/scenario/nfc-reader",
                json={"aidHex": aid_hex, "timeoutMs": 200},
                timeout=5,
            )
            if r.ok:
                state = r.json().get("result", {})
                reason = state.get("reason") or ""
                if "foreground activity" not in reason:
                    return
                last_reason = reason
        except Exception:
            pass
        time.sleep(0.5)
    raise TimeoutError(
        f"{label}: MainActivity never reached foreground (last={last_reason!r})"
    )


def resolve_launcher_activity(transport, package_id: str, label: str) -> str:
    """Resolve the launchable activity for an Android package."""
    resolved = transport.shell(
        ["cmd", "package", "resolve-activity", "--brief", package_id],
        timeout=5.0,
    )
    activity_line = next(
        (ln for ln in resolved.stdout.splitlines() if ln.startswith(f"{package_id}/")),
        None,
    )
    assert activity_line, (
        f"{label}: could not resolve launcher activity ({resolved.stdout!r})"
    )
    return activity_line


def install_and_launch_android_app(
    transport,
    *,
    label: str,
    apk_path: str,
    package_id: str,
    always_install: bool = False,
    enable_command: Sequence[str] | None = None,
    extra_permissions: Sequence[str] = (),
) -> None:
    """Wake, optionally enable a radio, install the app, and start its launcher."""
    wake_screen(transport, label)
    if enable_command is not None:
        transport.shell(list(enable_command), timeout=10.0)
    if always_install or "package:" not in transport.shell(["pm", "path", package_id]).stdout:
        transport.install_apk(apk_path)
    for perm in extra_permissions:
        transport.shell(["pm", "grant", package_id, perm], timeout=5.0)
    activity_line = resolve_launcher_activity(transport, package_id, label)
    transport.shell(["am", "force-stop", package_id])
    transport.shell(["am", "start", "-n", activity_line], timeout=10.0)


# Backwards-compatible aliases (older tests reference the underscore-prefixed names).
_wake_screen = wake_screen
_wait_health = wait_health
_wait_activity_resumed = wait_activity_resumed
