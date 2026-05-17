from __future__ import annotations

import secrets
import threading
import time

import pytest
import requests

from harness.app.builder import AppBuilder

from ._phone_helpers import (
    HCE_AID_HEX,
    install_and_launch_android_app,
    require_android_devices_with_capability,
    wait_activity_resumed as _wait_activity_resumed,
    wait_health as _wait_health,
)


PACKAGE_ID = "com.qimata.mobileiot"


@pytest.mark.hardware(roles=["android", "android"], capabilities=["nfc"])
def test_nfc_radios_ready_on_two_phones(android_phones):
    """Both Android phones report an NFC radio in the `on` state.

    `android_phones` fixture skips when fewer than 2 androids are online; this
    test additionally filters to the subset that advertises the `nfc` capability
    in inventory, since the harness gate (`pytest.mark.hardware`) checks the
    capability per-role but doesn't enforce that two *distinct* phones match.
    """
    nfc_pairs = require_android_devices_with_capability(android_phones, "nfc")

    states: dict[str, str] = {}
    for device, transport in nfc_pairs[:2]:
        transport.shell(["svc", "nfc", "enable"], timeout=10.0)
        result = transport.shell("dumpsys nfc | grep mState", timeout=10.0)
        assert result.ok, f"{device.id}: dumpsys nfc failed: {result.stderr}"
        states[device.id] = result.stdout.strip()

    assert len(states) == 2, f"expected two distinct phones, got {states}"
    for device_id, snapshot in states.items():
        assert "mState=on" in snapshot, (
            f"{device_id}: NFC radio not on after `svc nfc enable` — dumpsys says: {snapshot}"
        )


@pytest.mark.hardware(roles=["android", "android"], capabilities=["nfc"])
@pytest.mark.timeout(360)
def test_nfc_hce_payload_round_trips(android_phones, app_builder: AppBuilder):
    """Phone A serves an HCE payload; phone B reads it via reader-mode.

    Pre-conditions: TestHarness MAUI APK installed and the two phones placed
    back-to-back (NFC antennas aligned).
    """
    nfc_pairs = require_android_devices_with_capability(android_phones, "nfc")

    artifact = app_builder.build("maui", "android")
    (emu_dev, emu), (rdr_dev, rdr) = nfc_pairs[0], nfc_pairs[1]

    # Both phones: wake screen, ensure radios on, app installed, app launched.
    # The MAUI activity class is auto-generated (crcXXXX.MainActivity), so we
    # resolve it from the LAUNCHER filter and start it by FQCN.
    for device, transport in nfc_pairs[:2]:
        install_and_launch_android_app(
            transport,
            label=device.id,
            apk_path=str(artifact.path),
            package_id=PACKAGE_ID,
            always_install=False,
            enable_command=["svc", "nfc", "enable"],
        )

    # Distinct local ports → both phones' 47821 forwarded to the dev box.
    emu_port, rdr_port = 47821, 47822
    emu.forward_port(emu_port, 47821)
    rdr.forward_port(rdr_port, 47821)
    try:
        _wait_health(emu_port, label=emu_dev.id)
        _wait_health(rdr_port, label=rdr_dev.id)

        # The MAUI host starts during application init; MainActivity.OnCreate
        # finishes a bit later. Reader-mode needs `MainActivity.Current`, so
        # give the activity a moment to attach.
        _wait_activity_resumed(rdr_dev, rdr_port, label=rdr_dev.id)

        payload = secrets.token_bytes(16)
        payload_hex = payload.hex().upper()

        # HCE side: fire in background — synchronous POST will block until the
        # service responds or the scenario's own duration elapses.
        emu_result: dict = {}
        emu_error: dict = {}

        def _start_emulator():
            try:
                r = requests.post(
                    f"http://127.0.0.1:{emu_port}/scenario/nfc-hce-emulate",
                    json={"aidHex": HCE_AID_HEX, "payloadHex": payload_hex, "durationMs": 15000},
                    timeout=30,
                )
                r.raise_for_status()
                emu_result.update(r.json())
            except Exception as ex:  # noqa: BLE001
                emu_error["error"] = repr(ex)

        emu_thread = threading.Thread(target=_start_emulator, daemon=True)
        emu_thread.start()
        time.sleep(0.5)  # let HCE service stage payload before reader scans

        rdr_response = requests.post(
            f"http://127.0.0.1:{rdr_port}/scenario/nfc-reader",
            json={"aidHex": HCE_AID_HEX, "timeoutMs": 10000},
            timeout=30,
        )
        rdr_response.raise_for_status()
        rdr_json = rdr_response.json()

        emu_thread.join(timeout=20)
        assert not emu_error, f"emulator POST failed: {emu_error['error']}"

        reader_state = rdr_json.get("result", {})
        emulator_state = emu_result.get("result", {})

        assert reader_state.get("ok") is True, f"reader did not capture a response: {rdr_json}"
        resp_hex = reader_state.get("responseHex") or ""
        assert resp_hex.endswith("9000"), f"reader response missing 9000 status word: {resp_hex}"
        recovered = bytes.fromhex(resp_hex[:-4])
        assert recovered == payload, (
            f"payload mismatch: sent={payload.hex()} got={recovered.hex()} "
            f"emulator={emulator_state} reader={reader_state}"
        )
        assert emulator_state.get("served") is True, (
            f"HCE service did not see a SELECT-AID: {emulator_state}"
        )
    finally:
        for tr, port in ((emu, emu_port), (rdr, rdr_port)):
            try:
                tr.unforward_port(port)
            except Exception:
                pass
