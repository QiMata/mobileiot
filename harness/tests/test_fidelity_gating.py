from __future__ import annotations

import pytest

from harness.fidelity import evaluate_hardware_requirement
from harness.inventory.model import Device, DeviceKind
from harness.inventory.registry import Inventory


def _inv(*devices: Device) -> Inventory:
    return Inventory(list(devices))


@pytest.mark.mock
def test_gate_passes_when_role_online():
    inv = _inv(Device(id="pixel", kind=DeviceKind.ANDROID, adb_serial="S", online=True, capabilities={"ble"}))
    ok, reason = evaluate_hardware_requirement(inv, ["android"], ["ble"])
    assert ok
    assert reason == ""


@pytest.mark.mock
def test_gate_skips_when_kind_not_configured():
    inv = _inv(Device(id="pi", kind=DeviceKind.PI, usb_gadget_ip="1.2.3.4"))
    ok, reason = evaluate_hardware_requirement(inv, ["android"], [])
    assert not ok
    assert "android" in reason


@pytest.mark.mock
def test_gate_skips_when_capability_missing():
    inv = _inv(Device(id="pixel", kind=DeviceKind.ANDROID, adb_serial="S", online=True, capabilities={"nfc"}))
    ok, reason = evaluate_hardware_requirement(inv, ["android"], ["ble"])
    assert not ok
    assert "ble" in reason


@pytest.mark.mock
def test_gate_skips_when_device_offline():
    inv = _inv(Device(
        id="pixel", kind=DeviceKind.ANDROID, adb_serial="S",
        online=False, reason_offline="cable unplugged", capabilities={"ble"},
    ))
    ok, reason = evaluate_hardware_requirement(inv, ["android"], ["ble"])
    assert not ok
    assert "cable unplugged" in reason
