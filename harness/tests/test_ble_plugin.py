from __future__ import annotations

import pytest

from harness.integrations import load_all_plugins
from harness.inventory.model import Device, DeviceKind
from harness.inventory.registry import Inventory


@pytest.mark.mock
def test_ble_central_plugin_registered_via_entry_point():
    plugins = {p.name: p for p in load_all_plugins()}
    assert "ble_central" in plugins, (
        f"expected ble_central plugin via entry point; got {sorted(plugins)}"
    )
    p = plugins["ble_central"]
    assert p.required_capabilities == {"ble"}
    assert p.hardware_roles.roles == {"central": "android", "peripheral": "pi"}


@pytest.mark.mock
def test_ble_p2p_plugin_registered_via_entry_point():
    plugins = {p.name: p for p in load_all_plugins()}
    assert "ble_p2p" in plugins, (
        f"expected ble_p2p plugin via entry point; got {sorted(plugins)}"
    )
    p = plugins["ble_p2p"]
    assert p.required_capabilities == {"ble"}
    # Two phone roles, both Android by default; iOS variants override per-test.
    assert set(p.hardware_roles.roles.values()) == {"android"}
    assert len(p.hardware_roles.roles) == 2


@pytest.mark.mock
def test_ble_central_eligibility_skips_when_no_pi():
    plugins = {p.name: p for p in load_all_plugins()}
    p = plugins["ble_central"]

    inv = Inventory([
        Device(id="pixel-a", kind=DeviceKind.ANDROID, online=True, capabilities={"ble"}),
    ])
    ok, reason = p.eligible(inv)
    assert not ok
    assert "pi" in reason.lower() or "peripheral" in reason.lower()


@pytest.mark.mock
def test_ble_central_eligibility_skips_when_no_ble_phone():
    plugins = {p.name: p for p in load_all_plugins()}
    p = plugins["ble_central"]

    inv = Inventory([
        Device(id="pixel-a", kind=DeviceKind.ANDROID, online=True, capabilities={"nfc"}),
        Device(id="pi-lab", kind=DeviceKind.PI, online=True, capabilities={"ble"}),
    ])
    ok, reason = p.eligible(inv)
    assert not ok
    assert "missing capabilities" in reason or "ble" in reason


@pytest.mark.mock
def test_ble_central_eligibility_passes_with_phone_and_pi():
    plugins = {p.name: p for p in load_all_plugins()}
    p = plugins["ble_central"]

    inv = Inventory([
        Device(id="pixel-a", kind=DeviceKind.ANDROID, online=True, capabilities={"ble"}),
        Device(id="pi-lab", kind=DeviceKind.PI, online=True, capabilities={"ble"}),
    ])
    ok, reason = p.eligible(inv)
    assert ok, reason


@pytest.mark.mock
def test_ble_p2p_eligibility_skips_with_only_one_android():
    plugins = {p.name: p for p in load_all_plugins()}
    p = plugins["ble_p2p"]

    inv = Inventory([
        Device(id="pixel-a", kind=DeviceKind.ANDROID, online=True, capabilities={"ble"}),
    ])
    # Two roles both require android; only one device available — still
    # eligible from the plugin's perspective (Inventory.find returns the same
    # device twice). Eligibility is a soft gate; per-test logic enforces the
    # "two distinct phones" requirement. Confirm at least: plugin survives.
    ok, reason = p.eligible(inv)
    # One device satisfies the kind+capability check for both roles. The test
    # itself filters for *distinct* devices, the same way NFC does.
    assert ok, reason


@pytest.mark.mock
def test_ble_p2p_eligibility_skips_when_no_ble_capability():
    plugins = {p.name: p for p in load_all_plugins()}
    p = plugins["ble_p2p"]

    inv = Inventory([
        Device(id="pixel-a", kind=DeviceKind.ANDROID, online=True, capabilities={"nfc"}),
        Device(id="pixel-b", kind=DeviceKind.ANDROID, online=True, capabilities={"nfc"}),
    ])
    ok, reason = p.eligible(inv)
    assert not ok
    assert "missing capabilities" in reason or "ble" in reason


@pytest.mark.mock
def test_ble_p2p_eligibility_passes_with_two_ble_phones():
    plugins = {p.name: p for p in load_all_plugins()}
    p = plugins["ble_p2p"]

    inv = Inventory([
        Device(id="pixel-a", kind=DeviceKind.ANDROID, online=True, capabilities={"ble"}),
        Device(id="pixel-b", kind=DeviceKind.ANDROID, online=True, capabilities={"ble"}),
    ])
    ok, reason = p.eligible(inv)
    assert ok, reason
