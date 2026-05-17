"""Hardware-tier BLE peer-to-peer tests — two phones, peripheral + central.

Mirrors `test_nfc_hw.py` shape: peripheral side advertises a GATT service in
a background thread; central side scans-connects-writes synchronously; both
results are payload-checked at the end. Four variants:

- `test_ble_radios_ready_on_two_phones` — Android+Android smoke.
- `test_ble_p2p_payload_round_trips` — Android peripheral + Android central.
- `test_ble_p2p_ios_peripheral_android_central` — iOS peripheral, Android
  central. Resolves both roles from inventory directly because the
  hardware-marker convention works one kind per role.
- `test_ble_p2p_android_peripheral_ios_central` — inverse.

iOS variants are best-effort: if the iOS peripheral wrapper isn't registered
in DI or the platform doesn't support advertising, `ScenarioBase` returns a
`skipped` body which surfaces as `pytest.skip` here.
"""
from __future__ import annotations

import secrets
import threading
import time
import uuid

import pytest
import requests

from harness.app.builder import AppBuilder
from harness.app.ios_driver import IosAppDriver
from harness.inventory.model import DeviceKind
from harness.transports import transport_for

from ._phone_helpers import (
    install_and_launch_android_app,
    require_android_devices_with_capability,
    wait_health as _wait_health,
)


PACKAGE_ID = "com.qimata.mobileiot"
PERI_PORT = 47821
CENT_PORT = 47822
ADVERTISE_WARMUP_S = 1.0


def _post_peripheral(
    local_port: int,
    *,
    service_uuid: str,
    payload_hex: str,
    device_name: str,
    duration_ms: int,
) -> tuple[dict, dict]:
    """POST to the peripheral phone's `ble-peripheral` scenario.

    Returns (result_or_empty, error_or_empty); the caller probes both.
    """
    result: dict = {}
    error: dict = {}
    try:
        r = requests.post(
            f"http://127.0.0.1:{local_port}/scenario/ble-peripheral",
            json={
                "serviceUuid": service_uuid,
                "payloadHex": payload_hex,
                "deviceName": device_name,
                "durationMs": duration_ms,
            },
            timeout=max(30.0, duration_ms / 1000.0 + 10.0),
        )
        r.raise_for_status()
        result.update(r.json())
    except Exception as ex:  # noqa: BLE001
        error["error"] = repr(ex)
    return result, error


def _ble_pairs(android_phones):
    return require_android_devices_with_capability(android_phones, "ble")


def _assert_ble_exchange(
    *,
    central_body: dict,
    peripheral_body: dict,
    payload_hex: str,
    central_label: str,
    peripheral_label: str,
    central_error_label: str,
    peripheral_error_label: str,
) -> None:
    if peripheral_body.get("status") == "skipped":
        pytest.skip(
            f"{peripheral_label} skipped: "
            f"{peripheral_body.get('result', peripheral_body)}"
        )
    if central_body.get("status") == "skipped":
        pytest.skip(f"{central_label} skipped: {central_body}")

    central_state = central_body.get("result", {})
    peripheral_state = peripheral_body.get("result", {})

    assert central_state.get("ok") is True, (
        f"{central_error_label} did not complete exchange: {central_body}"
    )
    assert peripheral_state.get("served") is True, (
        f"{peripheral_error_label} never saw a write: {peripheral_body}"
    )
    received_hex = (peripheral_state.get("centralBytesReceived") or "").upper()
    assert received_hex == payload_hex, (
        f"payload mismatch: sent={payload_hex} got={received_hex} "
        f"peripheral={peripheral_state} central={central_state}"
    )


@pytest.mark.hardware(roles=["android", "android"], capabilities=["ble"])
def test_ble_radios_ready_on_two_phones(android_phones):
    """Both Android phones report a Bluetooth adapter in the ON state."""
    pairs = _ble_pairs(android_phones)

    states: dict[str, str] = {}
    for device, transport in pairs[:2]:
        transport.shell(["svc", "bluetooth", "enable"], timeout=10.0)
        # `dumpsys bluetooth_manager` works on all Android versions we ship to;
        # the `state=` line in the curState block is the canonical adapter state.
        result = transport.shell(
            "dumpsys bluetooth_manager | grep -E 'mState|state:'",
            timeout=10.0,
        )
        assert result.ok, (
            f"{device.id}: dumpsys bluetooth_manager failed: {result.stderr}"
        )
        states[device.id] = result.stdout.strip()

    assert len(states) == 2, f"expected two distinct phones, got {states}"
    for device_id, snapshot in states.items():
        ok = ("state=ON" in snapshot) or ("state: ON" in snapshot) or ("mState=ON" in snapshot)
        assert ok, (
            f"{device_id}: Bluetooth adapter not ON after `svc bluetooth enable` — "
            f"dumpsys says: {snapshot!r}"
        )


@pytest.mark.hardware(roles=["android", "android"], capabilities=["ble"])
@pytest.mark.timeout(360)
def test_ble_p2p_payload_round_trips(android_phones, app_builder: AppBuilder):
    """Phone P advertises a GATT service; phone C scans, connects, writes a
    random 16-byte payload to the characteristic; both phones report the
    same bytes."""
    pairs = _ble_pairs(android_phones)

    artifact = app_builder.build("maui", "android")
    (peri_dev, peri), (cent_dev, cent) = pairs[0], pairs[1]

    install_and_launch_android_app(
        peri,
        label=peri_dev.id,
        apk_path=str(artifact.path),
        package_id=PACKAGE_ID,
        always_install=True,
        enable_command=["svc", "bluetooth", "enable"],
        extra_permissions=(
            "android.permission.BLUETOOTH_SCAN",
            "android.permission.BLUETOOTH_CONNECT",
            "android.permission.BLUETOOTH_ADVERTISE",
            "android.permission.ACCESS_FINE_LOCATION",
        ),
    )
    install_and_launch_android_app(
        cent,
        label=cent_dev.id,
        apk_path=str(artifact.path),
        package_id=PACKAGE_ID,
        always_install=True,
        enable_command=["svc", "bluetooth", "enable"],
        extra_permissions=(
            "android.permission.BLUETOOTH_SCAN",
            "android.permission.BLUETOOTH_CONNECT",
            "android.permission.BLUETOOTH_ADVERTISE",
            "android.permission.ACCESS_FINE_LOCATION",
        ),
    )

    peri.forward_port(PERI_PORT, 47821)
    cent.forward_port(CENT_PORT, 47821)
    try:
        _wait_health(PERI_PORT, label=peri_dev.id)
        _wait_health(CENT_PORT, label=cent_dev.id)

        payload = secrets.token_bytes(16)
        payload_hex = payload.hex().upper()
        service_uuid = str(uuid.uuid4())
        device_name = "MobileIotPeripheralA"

        peri_result: dict = {}
        peri_error: dict = {}

        def _start_peripheral():
            r, e = _post_peripheral(
                PERI_PORT,
                service_uuid=service_uuid,
                payload_hex=payload_hex,
                device_name=device_name,
                duration_ms=15000,
            )
            peri_result.update(r)
            peri_error.update(e)

        peri_thread = threading.Thread(target=_start_peripheral, daemon=True)
        peri_thread.start()
        time.sleep(ADVERTISE_WARMUP_S)

        cent_response = requests.post(
            f"http://127.0.0.1:{CENT_PORT}/scenario/ble-p2p-central",
            json={
                "deviceName": device_name,
                "serviceUuid": service_uuid,
                "payloadHex": payload_hex,
                "timeoutMs": 12000,
            },
            timeout=30,
        )
        cent_response.raise_for_status()
        cent_json = cent_response.json()

        peri_thread.join(timeout=20)
        assert not peri_error, f"peripheral POST failed: {peri_error['error']}"

        _assert_ble_exchange(
            central_body=cent_json,
            peripheral_body=peri_result,
            payload_hex=payload_hex,
            central_label="ble-p2p-central",
            peripheral_label="ble-peripheral",
            central_error_label="central",
            peripheral_error_label="peripheral",
        )
    finally:
        for tr, port in ((peri, PERI_PORT), (cent, CENT_PORT)):
            try:
                tr.unforward_port(port)
            except Exception:
                pass


def _require_ble(inventory, role: str, kind: DeviceKind):
    """Find first online device of `kind` with `ble` capability or skip."""
    matches = inventory.find(kind=kind, capabilities={"ble"}, online_only=True)
    if not matches:
        pytest.skip(f"role '{role}' ({kind.value}+ble) not satisfied in inventory")
    return matches[0]


@pytest.mark.hardware(roles=["ios", "android"], capabilities=["ble"])
@pytest.mark.timeout(420)
def test_ble_p2p_ios_peripheral_android_central(inventory, app_builder: AppBuilder):
    """iOS phone is the BLE peripheral; Android phone is the central."""
    ios_dev = _require_ble(inventory, "peripheral_ios", DeviceKind.IOS)
    and_dev = _require_ble(inventory, "central_android", DeviceKind.ANDROID)

    ios_transport = transport_for(ios_dev)
    and_transport = transport_for(and_dev)

    ios_driver = IosAppDriver(ios_transport, app_builder, "maui")
    try:
        ios_driver.ensure_installed()
        ios_driver.launch()
        ios_hook = ios_driver.hook()
        ios_hook.wait_ready(timeout=60.0)

        # Forward Android side to a different host port to avoid collision with
        # the iOS driver's :47821 forward.
        and_apk = app_builder.build("maui", "android")
        install_and_launch_android_app(
            and_transport,
            label=and_dev.id,
            apk_path=str(and_apk.path),
            package_id=PACKAGE_ID,
            always_install=True,
            enable_command=["svc", "bluetooth", "enable"],
            extra_permissions=(
                "android.permission.BLUETOOTH_SCAN",
                "android.permission.BLUETOOTH_CONNECT",
                "android.permission.BLUETOOTH_ADVERTISE",
                "android.permission.ACCESS_FINE_LOCATION",
            ),
        )
        and_transport.forward_port(CENT_PORT, 47821)
        try:
            _wait_health(CENT_PORT, label=and_dev.id)

            payload = secrets.token_bytes(16)
            payload_hex = payload.hex().upper()
            service_uuid = str(uuid.uuid4())
            device_name = "MobileIotPeripheralIos"

            # iOS is on the default local port via the iOS driver's forward.
            peri_result: dict = {}
            peri_error: dict = {}

            def _start_ios_peripheral():
                r, e = _post_peripheral(
                    PERI_PORT,
                    service_uuid=service_uuid,
                    payload_hex=payload_hex,
                    device_name=device_name,
                    duration_ms=15000,
                )
                peri_result.update(r)
                peri_error.update(e)

            t = threading.Thread(target=_start_ios_peripheral, daemon=True)
            t.start()
            time.sleep(ADVERTISE_WARMUP_S)

            cent_response = requests.post(
                f"http://127.0.0.1:{CENT_PORT}/scenario/ble-p2p-central",
                json={
                    "deviceName": device_name,
                    "serviceUuid": service_uuid,
                    "payloadHex": payload_hex,
                    "timeoutMs": 12000,
                },
                timeout=30,
            )
            cent_response.raise_for_status()
            cent_json = cent_response.json()
            t.join(timeout=20)
            assert not peri_error, f"iOS peripheral POST failed: {peri_error['error']}"

            _assert_ble_exchange(
                central_body=cent_json,
                peripheral_body=peri_result,
                payload_hex=payload_hex,
                central_label="android ble-p2p-central",
                peripheral_label="iOS ble-peripheral",
                central_error_label="central",
                peripheral_error_label="iOS peripheral",
            )
        finally:
            try:
                and_transport.unforward_port(CENT_PORT)
            except Exception:
                pass
    finally:
        try:
            ios_driver.stop()
        finally:
            ios_transport.close()
            and_transport.close()


@pytest.mark.hardware(roles=["android", "ios"], capabilities=["ble"])
@pytest.mark.timeout(420)
def test_ble_p2p_android_peripheral_ios_central(inventory, app_builder: AppBuilder):
    """Android phone is the BLE peripheral; iOS phone is the central."""
    and_dev = _require_ble(inventory, "peripheral_android", DeviceKind.ANDROID)
    ios_dev = _require_ble(inventory, "central_ios", DeviceKind.IOS)

    and_transport = transport_for(and_dev)
    ios_transport = transport_for(ios_dev)

    ios_driver = IosAppDriver(ios_transport, app_builder, "maui")
    try:
        and_apk = app_builder.build("maui", "android")
        install_and_launch_android_app(
            and_transport,
            label=and_dev.id,
            apk_path=str(and_apk.path),
            package_id=PACKAGE_ID,
            always_install=True,
            enable_command=["svc", "bluetooth", "enable"],
            extra_permissions=(
                "android.permission.BLUETOOTH_SCAN",
                "android.permission.BLUETOOTH_CONNECT",
                "android.permission.BLUETOOTH_ADVERTISE",
                "android.permission.ACCESS_FINE_LOCATION",
            ),
        )
        and_transport.forward_port(PERI_PORT, 47821)

        ios_driver.ensure_installed()
        ios_driver.launch()  # forwards 47821 → ios:47821
        ios_hook = ios_driver.hook()
        ios_hook.wait_ready(timeout=60.0)

        # iOS driver already grabbed local 47821 — we need to put it on CENT_PORT.
        # Since the IosAppDriver always uses the default HOOK_PORT, re-forward it
        # to our central port and unforward the default. But the cleanest path is:
        # peripheral on a *higher* port (CENT_PORT) on the Android side and let
        # iOS keep PERI_PORT.
        # Re-forwarding the android side: drop 47821 and use 47822 instead.
        try:
            and_transport.unforward_port(PERI_PORT)
        except Exception:
            pass
        and_transport.forward_port(CENT_PORT, 47821)

        try:
            _wait_health(CENT_PORT, label=and_dev.id)
            _wait_health(PERI_PORT, label=ios_dev.id)

            payload = secrets.token_bytes(16)
            payload_hex = payload.hex().upper()
            service_uuid = str(uuid.uuid4())
            device_name = "MobileIotPeripheralAnd"

            peri_result: dict = {}
            peri_error: dict = {}

            def _start_android_peripheral():
                r, e = _post_peripheral(
                    CENT_PORT,  # android peripheral lives on CENT_PORT here
                    service_uuid=service_uuid,
                    payload_hex=payload_hex,
                    device_name=device_name,
                    duration_ms=15000,
                )
                peri_result.update(r)
                peri_error.update(e)

            t = threading.Thread(target=_start_android_peripheral, daemon=True)
            t.start()
            time.sleep(ADVERTISE_WARMUP_S)

            cent_response = requests.post(
                f"http://127.0.0.1:{PERI_PORT}/scenario/ble-p2p-central",
                json={
                    "deviceName": device_name,
                    "serviceUuid": service_uuid,
                    "payloadHex": payload_hex,
                    "timeoutMs": 12000,
                },
                timeout=30,
            )
            cent_response.raise_for_status()
            cent_json = cent_response.json()
            t.join(timeout=20)
            assert not peri_error, f"android peripheral POST failed: {peri_error['error']}"

            _assert_ble_exchange(
                central_body=cent_json,
                peripheral_body=peri_result,
                payload_hex=payload_hex,
                central_label="iOS ble-p2p-central",
                peripheral_label="android ble-peripheral",
                central_error_label="iOS central",
                peripheral_error_label="android peripheral",
            )
        finally:
            try:
                and_transport.unforward_port(CENT_PORT)
            except Exception:
                pass
    finally:
        try:
            ios_driver.stop()
        finally:
            ios_transport.close()
            and_transport.close()
