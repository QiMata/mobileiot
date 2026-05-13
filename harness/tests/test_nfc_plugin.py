from __future__ import annotations

import pytest

from harness.integrations import load_all_plugins
from harness.inventory.model import Device, DeviceKind
from harness.inventory.registry import Inventory


@pytest.mark.mock
def test_nfc_plugin_registered_via_entry_point():
    plugins = {p.name: p for p in load_all_plugins()}
    assert "nfc" in plugins, f"expected nfc plugin via entry point; got {sorted(plugins)}"
    p = plugins["nfc"]
    assert p.required_capabilities == {"nfc"}
    assert set(p.hardware_roles.roles.values()) == {"android"}
    assert len(p.hardware_roles.roles) == 2


@pytest.mark.mock
def test_nfc_eligibility_skips_when_no_nfc_phone():
    plugins = {p.name: p for p in load_all_plugins()}
    p = plugins["nfc"]

    inv = Inventory([
        Device(id="pixel-x", kind=DeviceKind.ANDROID, online=True, capabilities={"ble"}),
        Device(id="pixel-y", kind=DeviceKind.ANDROID, online=True, capabilities={"ble"}),
    ])
    ok, reason = p.eligible(inv)
    assert not ok
    assert "missing capabilities" in reason or "nfc" in reason


@pytest.mark.mock
def test_nfc_eligibility_passes_with_two_nfc_phones():
    plugins = {p.name: p for p in load_all_plugins()}
    p = plugins["nfc"]

    inv = Inventory([
        Device(id="pixel-a", kind=DeviceKind.ANDROID, online=True, capabilities={"nfc"}),
        Device(id="pixel-b", kind=DeviceKind.ANDROID, online=True, capabilities={"nfc"}),
    ])
    ok, reason = p.eligible(inv)
    assert ok, reason
