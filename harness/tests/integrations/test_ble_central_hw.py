"""Hardware-tier BLE central tests — one phone reads a Pi BLE peripheral.

Two variants:

- `test_ble_central_android_reads_pi_gatt` — Android central + Pi peripheral.
  Starts `src/pi/bluetoothle_demo.py` on the Pi inside a tmux session, builds
  the MAUI APK, installs+launches it, POSTs `/scenario/ble-gatt`, asserts the
  returned temperature/humidity/led fields.
- `test_ble_central_ios_reads_pi_gatt` — same flow with the iPhone as central
  via the `ios_app_driver` fixture.

Both end the run by killing the Pi tmux session in a `try/finally`.
"""
from __future__ import annotations

import time

import pytest
import requests

from harness.app.builder import AppBuilder

from ._phone_helpers import wait_health as _wait_health, wake_screen as _wake_screen


PACKAGE_ID = "com.qimata.mobileiot"
PI_DEVICE_NAME = "PiDHTSensor"
PI_TMUX_SESSION = "pi-ble-demo"
LOCAL_PORT = 47821


def _start_pi_peripheral(pi_transport) -> None:
    """Launch the Pi BLE peripheral in a detached tmux session.

    `bluetoothle_demo.py` runs forever; tmux gives us a clean kill in finally
    and keeps stdout off the SSH session that `transport.shell` waits on.
    """
    pi_transport.shell(
        f"tmux kill-session -t {PI_TMUX_SESSION} 2>/dev/null; true",
        timeout=5.0,
    )
    cmd = (
        f"tmux new-session -d -s {PI_TMUX_SESSION} "
        f"'python3 ~/mobileiot/src/pi/bluetoothle_demo.py "
        f"--device-name {PI_DEVICE_NAME} 2>&1 | tee /tmp/pi-ble-demo.log'"
    )
    r = pi_transport.shell(cmd, timeout=10.0)
    if r.rc != 0:
        pytest.skip(
            f"could not start Pi BLE peripheral via tmux (rc={r.rc}): "
            f"stdout={r.stdout!r} stderr={r.stderr!r}"
        )
    # Give bluezero a moment to register the GATT service before scan.
    time.sleep(2.0)


def _stop_pi_peripheral(pi_transport) -> None:
    try:
        pi_transport.shell(
            f"tmux kill-session -t {PI_TMUX_SESSION} 2>/dev/null; true",
            timeout=5.0,
        )
    except Exception:
        pass


@pytest.mark.hardware(roles=["android", "pi"], capabilities=["ble"])
@pytest.mark.timeout(360)
def test_ble_central_android_reads_pi_gatt(android_phone, pi, app_builder: AppBuilder):
    """Android phone reads temperature, humidity, and toggles the LED on Pi."""
    device, transport = android_phone
    pi_device, pi_transport = pi

    _start_pi_peripheral(pi_transport)
    try:
        artifact = app_builder.build("maui", "android")

        _wake_screen(transport, device.id)
        transport.shell(["svc", "bluetooth", "enable"], timeout=10.0)
        if "package:" not in transport.shell(["pm", "path", PACKAGE_ID]).stdout:
            transport.install_apk(str(artifact.path))
        resolved = transport.shell(
            ["cmd", "package", "resolve-activity", "--brief", PACKAGE_ID],
            timeout=5.0,
        )
        activity_line = next(
            (ln for ln in resolved.stdout.splitlines() if ln.startswith(f"{PACKAGE_ID}/")),
            None,
        )
        assert activity_line, (
            f"{device.id}: could not resolve launcher activity ({resolved.stdout!r})"
        )
        transport.shell(["am", "force-stop", PACKAGE_ID])
        transport.shell(["am", "start", "-n", activity_line], timeout=10.0)

        transport.forward_port(LOCAL_PORT, 47821)
        try:
            _wait_health(LOCAL_PORT, label=device.id)
            r = requests.post(
                f"http://127.0.0.1:{LOCAL_PORT}/scenario/ble-gatt",
                json={"deviceName": PI_DEVICE_NAME},
                timeout=60,
            )
            r.raise_for_status()
            body = r.json()
            result = body.get("result", {})

            if body.get("status") == "skipped":
                pytest.skip(
                    f"ble-gatt scenario skipped: {result.get('reason', body)}"
                )

            assert "temperature" in result, f"no temperature in {body}"
            assert "humidity" in result, f"no humidity in {body}"
            assert "led" in result, f"no led in {body}"
        finally:
            try:
                transport.unforward_port(LOCAL_PORT)
            except Exception:
                pass
    finally:
        _stop_pi_peripheral(pi_transport)


@pytest.mark.hardware(roles=["ios", "pi"], capabilities=["ble"])
@pytest.mark.timeout(420)
def test_ble_central_ios_reads_pi_gatt(ios_app_driver, pi):
    """iOS phone reads temperature, humidity, and toggles the LED on Pi.

    Uses the `ios_app_driver` fixture which installs the IPA, launches with
    `MIOT_TEST_MODE=1`, and forwards port 47821 via pymobiledevice3 usbmux.
    """
    pi_device, pi_transport = pi

    _start_pi_peripheral(pi_transport)
    try:
        ios_app_driver.ensure_installed()
        ios_app_driver.launch()
        hook = ios_app_driver.hook()
        hook.wait_ready(timeout=60.0)

        r = requests.post(
            f"http://127.0.0.1:{LOCAL_PORT}/scenario/ble-gatt",
            json={"deviceName": PI_DEVICE_NAME},
            timeout=60,
        )
        r.raise_for_status()
        body = r.json()
        result = body.get("result", {})

        if body.get("status") == "skipped":
            pytest.skip(
                f"ble-gatt scenario skipped: {result.get('reason', body)}"
            )

        assert "temperature" in result, f"no temperature in {body}"
        assert "humidity" in result, f"no humidity in {body}"
        assert "led" in result, f"no led in {body}"
    finally:
        _stop_pi_peripheral(pi_transport)
