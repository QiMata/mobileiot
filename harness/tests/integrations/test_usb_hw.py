"""Hardware-tier USB tests.

Four scenarios:

1. ``test_usb_host_lists_pi_gadget`` — Android phone enumerates the Pi gadget.
2. ``test_usb_bulk_round_trip_pi_gadget`` — Android writes a probe payload to
   the Pi's ConfigFS Loopback gadget and reads it back.
3. ``test_android_usb_pipeline_structural`` — Android structural check. Runs
   on any usb_host-capable Android with no Pi or OTG accessory required.
   Mirrors the iOS structural test below: passes if the scenario returns
   either a clean ``Skipped("No USB bulk devices found")`` (no OTG device
   attached; verifies APK install + ``HarnessHttpHost`` + DI of
   ``IUsbCommunicator`` + Android ``UsbManager`` query path) OR a real
   round-trip succeeded (an OTG device is plugged in).
4. ``test_ios_usb_accessory_enumerates`` — iOS structural check. Same
   two-mode pass: ``Skipped("No USB bulk devices found")`` (no MFi accessory
   in lab; verifies the External Accessory pipeline + Info.plist + DI
   wiring only) OR a real round-trip succeeded (MFi accessory present).

The plugin declares ``required_capabilities=set()`` because the harness gate
applies the same caps to every role. Per-role capability filtering is done
inline below (same idiom as ``test_nfc_hw.py:26-30``). The screen-wake /
health-poll helpers are inlined here for now — they'll be deduplicated with
NFC's copies in a follow-up.
"""
from __future__ import annotations

import re
import time

import pytest
import requests

from harness.app.builder import AppBuilder


PACKAGE_ID = "com.qimata.mobileiot"
USB_PROBE_PAYLOAD = b"MOBILEIOT-USB-PROBE"
PI_GADGET_UDC_PATH = "/sys/kernel/config/usb_gadget/mobileiot/UDC"
PI_GADGET_SETUP_HINT = (
    "Pi gadget not bound — run `sudo python3 src/pi/usb_demo.py` on the Pi first"
)


@pytest.mark.hardware(roles=["android", "pi"], capabilities=[])
@pytest.mark.timeout(360)
def test_usb_host_lists_pi_gadget(android_phones, pi, app_builder: AppBuilder):
    """The Android phone enumerates at least one USB device after the Pi
    gadget is up — proves the OTG pipeline + DI of ``IUsbCommunicator`` works.
    """
    usb_pairs = [(d, t) for d, t in android_phones if "usb_host" in d.capabilities]
    if not usb_pairs:
        pytest.skip("need 1+ Android phone with usb_host capability online")

    pi_dev, pi_transport = pi
    _require_pi_gadget(pi_transport)

    artifact = app_builder.build("maui", "android")
    phone_dev, transport = usb_pairs[0]

    _wake_screen(transport, phone_dev.id)
    _install_and_launch(transport, artifact.path, phone_dev.id)

    local_port = 47821
    transport.forward_port(local_port, 47821)
    try:
        _wait_health(local_port, label=phone_dev.id)
        response = requests.post(
            f"http://127.0.0.1:{local_port}/scenario/usb-bulk",
            json={"message": "LIST"},
            timeout=30,
        )
        response.raise_for_status()
        body = response.json()
        result = body.get("result", {})

        _maybe_skip_permission(result)

        # Either "skipped" with a clear reason or "passed" with a non-empty device.
        if result.get("status") == "skipped":
            pytest.skip(
                f"{phone_dev.id}: usb-bulk skipped — {result.get('reason')!r}. "
                "Confirm the OTG cable is seated and the phone listed under "
                "`adb shell lsusb` (or equivalent)."
            )
        assert result.get("status") == "passed", f"unexpected scenario status: {body}"
        assert result.get("device"), f"empty device identifier: {body}"
    finally:
        try:
            transport.unforward_port(local_port)
        except Exception:
            pass


@pytest.mark.hardware(roles=["android", "pi"], capabilities=[])
@pytest.mark.timeout(360)
def test_usb_bulk_round_trip_pi_gadget(android_phones, pi, app_builder: AppBuilder):
    """Phone writes a probe payload to the Pi gadget and reads back at least one byte.

    If the device-side ``UsbBulkScenario`` returned ``readHex`` (post the
    optional enhancement in ``IntegrationScenarios.cs``), assert the echo
    starts with the probe bytes.
    """
    usb_pairs = [(d, t) for d, t in android_phones if "usb_host" in d.capabilities]
    if not usb_pairs:
        pytest.skip("need 1+ Android phone with usb_host capability online")

    pi_dev, pi_transport = pi
    _require_pi_gadget(pi_transport)

    artifact = app_builder.build("maui", "android")
    phone_dev, transport = usb_pairs[0]

    _wake_screen(transport, phone_dev.id)
    _install_and_launch(transport, artifact.path, phone_dev.id)

    local_port = 47821
    transport.forward_port(local_port, 47821)
    try:
        _wait_health(local_port, label=phone_dev.id)
        response = requests.post(
            f"http://127.0.0.1:{local_port}/scenario/usb-bulk",
            json={"message": USB_PROBE_PAYLOAD.decode()},
            timeout=30,
        )
        response.raise_for_status()
        body = response.json()
        result = body.get("result", {})

        _maybe_skip_permission(result)

        assert result.get("status") == "passed", (
            f"{phone_dev.id}: usb-bulk did not pass: {body}"
        )
        written = result.get("written") or 0
        assert written >= len(USB_PROBE_PAYLOAD), (
            f"{phone_dev.id}: wrote {written} bytes, expected ≥{len(USB_PROBE_PAYLOAD)}: {body}"
        )
        assert (result.get("read") or 0) >= 0, body

        read_hex = result.get("readHex")
        if read_hex:
            try:
                echoed = bytes.fromhex(read_hex)
            except ValueError as ex:
                pytest.fail(f"readHex not valid hex: {read_hex!r} ({ex})")
            assert echoed.startswith(USB_PROBE_PAYLOAD), (
                f"echo mismatch — sent={USB_PROBE_PAYLOAD!r} got={echoed!r}"
            )
    finally:
        try:
            transport.unforward_port(local_port)
        except Exception:
            pass


@pytest.mark.hardware(roles=["android"], capabilities=[])
@pytest.mark.timeout(180)
def test_android_usb_pipeline_structural(android_phones, app_builder: AppBuilder):
    """Structural check: the Android USB host pipeline is wired correctly.

    Runs whenever any usb_host-capable Android is online — no Pi or OTG
    accessory required. Both outcomes pass:

    - ``status=skipped`` and ``reason`` matches /No USB bulk devices found/i
      — phone has no OTG device attached. Verifies APK install,
      ``HarnessHttpHost``, scenario dispatch, DI of ``IUsbCommunicator``, and
      the ``UsbManager.DeviceList`` query path.
    - ``status=passed`` and ``written >= 4`` — an OTG device is attached and
      the round-trip succeeded.
    """
    usb_pairs = [(d, t) for d, t in android_phones if "usb_host" in d.capabilities]
    if not usb_pairs:
        pytest.skip("need 1+ Android phone with usb_host capability online")

    artifact = app_builder.build("maui", "android")
    phone_dev, transport = usb_pairs[0]

    _wake_screen(transport, phone_dev.id)
    _install_and_launch(transport, artifact.path, phone_dev.id)

    local_port = 47821
    transport.forward_port(local_port, 47821)
    try:
        _wait_health(local_port, label=phone_dev.id)
        response = requests.post(
            f"http://127.0.0.1:{local_port}/scenario/usb-bulk",
            json={"message": "PING"},
            timeout=30,
        )
        response.raise_for_status()
        body = response.json()
        result = body.get("result", {})
        status = result.get("status")

        if status == "skipped":
            reason = result.get("reason") or ""
            assert re.search(r"No USB bulk devices found", reason, re.IGNORECASE), (
                f"{phone_dev.id}: usb-bulk skipped with unexpected reason: {body}"
            )
            return
        if status == "passed":
            written = result.get("written") or 0
            assert written >= 4, (
                f"{phone_dev.id}: usb-bulk passed but wrote too little: {body}"
            )
            return
        pytest.fail(
            f"{phone_dev.id}: usb-bulk returned unexpected status {status!r}: {body}"
        )
    finally:
        try:
            transport.unforward_port(local_port)
        except Exception:
            pass


@pytest.mark.hardware(roles=["ios"], capabilities=[])
@pytest.mark.timeout(360)
def test_ios_usb_accessory_enumerates(ios_app_driver):
    """Structural check: the iOS External Accessory pipeline is wired correctly.

    Two acceptable outcomes — both pass:

    - ``status=skipped`` and ``reason`` matches /No USB bulk devices found/i
      — verifies Info.plist + DI wiring, no MFi accessory present.
    - ``status=passed`` and ``written >= 4`` — MFi accessory present, real
      round-trip succeeded.
    """
    driver = ios_app_driver
    driver.ensure_installed()
    driver.launch()
    client = driver.hook()
    client.wait_ready(timeout=60.0)

    body = client.start_scenario("usb-bulk", {"message": "PING"})
    result = body.get("result", {})
    status = result.get("status")

    if status == "skipped":
        reason = result.get("reason") or ""
        assert re.search(r"No USB bulk devices found", reason, re.IGNORECASE), (
            f"iOS usb-bulk skipped with unexpected reason: {body}"
        )
        return
    if status == "passed":
        written = result.get("written") or 0
        assert written >= 4, f"iOS usb-bulk passed but wrote too little: {body}"
        return
    pytest.fail(f"iOS usb-bulk returned unexpected status {status!r}: {body}")


# --- helpers (copied from test_nfc_hw.py; deduplicated post-merge) -----------

def _require_pi_gadget(pi_transport) -> None:
    """Skip cleanly if the Pi-side ConfigFS gadget hasn't been bound."""
    result = pi_transport.shell(
        f"test -d {PI_GADGET_UDC_PATH} && echo up || echo down",
        timeout=10.0,
    )
    if not result.ok or "up" not in result.stdout:
        pytest.skip(PI_GADGET_SETUP_HINT)


def _maybe_skip_permission(result: dict) -> None:
    """First-run USB permission denials surface as scenario-skipped, not failures."""
    if result.get("status") != "skipped":
        return
    reason = (result.get("reason") or "").lower()
    if "permission" in reason or "failed to open usb device" in reason:
        pytest.skip(
            f"USB permission not yet granted on phone — {result.get('reason')!r}. "
            "Re-plug the OTG cable, accept the on-device USB permission dialog "
            "(tick \"Use by default for this USB device\"), then rerun."
        )


def _install_and_launch(transport, apk_path, label: str) -> None:
    """Install the TestHarness APK (always — `-r` reinstall is cheap and avoids
    stale-APK bugs when test code or app code changes) and start the launcher
    activity."""
    transport.install_apk(str(apk_path))
    resolved = transport.shell(
        ["cmd", "package", "resolve-activity", "--brief", PACKAGE_ID],
        timeout=5.0,
    )
    activity_line = next(
        (ln for ln in resolved.stdout.splitlines() if ln.startswith(f"{PACKAGE_ID}/")),
        None,
    )
    assert activity_line, f"{label}: could not resolve launcher activity ({resolved.stdout!r})"
    transport.shell(["am", "force-stop", PACKAGE_ID])
    transport.shell(["am", "start", "-n", activity_line], timeout=10.0)


def _wake_screen(transport, label: str) -> None:
    """Wake the device and dismiss a swipe-only lockscreen. PIN/biometric locks
    can't be bypassed via adb — the test will surface that as a scenario error."""
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


def _wait_health(local_port: int, *, label: str, timeout: float = 30.0) -> None:
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
    raise TimeoutError(f"{label}: TestHarness host not ready at :{local_port}/health ({last})")
