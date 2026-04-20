from __future__ import annotations

from pathlib import Path
from textwrap import dedent

import pytest

from harness.inventory.loader import load_inventory
from harness.inventory.model import DeviceKind


@pytest.mark.mock
def test_load_inventory_parses_devices(tmp_path: Path):
    primary = tmp_path / "devices.yaml"
    primary.write_text(dedent("""\
        devices:
          - id: pixel-a
            kind: android
            adb_serial: ABC123
            capabilities: [ble, nfc]
          - id: pi-lab
            kind: pi
            usb_gadget_ip: 192.168.7.2
            ssh_user: pi
            capabilities: [ble, thread]
    """))
    devs = load_inventory(primary, None)
    by_id = {d.id: d for d in devs}
    assert by_id["pixel-a"].kind == DeviceKind.ANDROID
    assert by_id["pixel-a"].adb_serial == "ABC123"
    assert by_id["pi-lab"].kind == DeviceKind.PI
    assert by_id["pi-lab"].usb_gadget_ip == "192.168.7.2"
    assert "thread" in by_id["pi-lab"].capabilities


@pytest.mark.mock
def test_local_overrides_replace_by_id(tmp_path: Path):
    primary = tmp_path / "devices.yaml"
    primary.write_text(dedent("""\
        devices:
          - id: pi-lab
            kind: pi
            usb_gadget_ip: 192.168.7.2
            ssh_user: pi
            capabilities: [ble]
    """))
    overrides = tmp_path / "devices.local.yaml"
    overrides.write_text(dedent("""\
        devices:
          - id: pi-lab
            kind: pi
            usb_gadget_ip: 10.0.0.5
            ssh_user: pi
            capabilities: [ble, camera]
    """))
    devs = load_inventory(primary, overrides)
    assert len(devs) == 1
    assert devs[0].usb_gadget_ip == "10.0.0.5"
    assert "camera" in devs[0].capabilities


@pytest.mark.mock
def test_load_inventory_missing_file(tmp_path: Path):
    assert load_inventory(tmp_path / "nope.yaml", None) == []
