from __future__ import annotations

import pytest

from harness.integrations import load_all_plugins
from harness.inventory.model import Device, DeviceKind
from harness.inventory.registry import Inventory


@pytest.mark.mock
def test_usb_plugin_registered_via_entry_point():
    plugins = {p.name: p for p in load_all_plugins()}
    assert "usb" in plugins, f"expected usb plugin via entry point; got {sorted(plugins)}"
    p = plugins["usb"]
    # required_capabilities is empty by design — see usb.py docstring.
    assert p.required_capabilities == set()
    assert p.hardware_roles.roles == {"phone": "android", "gadget": "pi"}


@pytest.mark.mock
def test_usb_inventory_query_skips_without_usb_host_android_or_serial_gadget_pi():
    """Per-role filtering happens inside the test body, but mock-tier proves
    the inventory queries behave the way the hardware test relies on."""
    inv = Inventory([
        Device(id="pixel-x", kind=DeviceKind.ANDROID, online=True, capabilities={"ble", "nfc"}),
        Device(id="pi-x", kind=DeviceKind.PI, online=True, capabilities={"ble", "thread"}),
    ])
    usb_hosts = inv.find(kind=DeviceKind.ANDROID, capabilities=["usb_host"], online_only=True)
    gadgets = inv.find(kind=DeviceKind.PI, capabilities=["serial_gadget"], online_only=True)
    assert usb_hosts == []
    assert gadgets == []


@pytest.mark.mock
def test_usb_inventory_query_matches_when_capabilities_present():
    inv = Inventory([
        Device(
            id="pixel-a",
            kind=DeviceKind.ANDROID,
            online=True,
            capabilities={"usb_host", "ble"},
        ),
        Device(
            id="pi-lab",
            kind=DeviceKind.PI,
            online=True,
            capabilities={"serial_gadget", "usb_bulk"},
        ),
    ])
    usb_hosts = inv.find(kind=DeviceKind.ANDROID, capabilities=["usb_host"], online_only=True)
    gadgets = inv.find(kind=DeviceKind.PI, capabilities=["serial_gadget"], online_only=True)
    assert [d.id for d in usb_hosts] == ["pixel-a"]
    assert [d.id for d in gadgets] == ["pi-lab"]
