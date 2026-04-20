from __future__ import annotations

import pytest

from harness.integrations import load_all_plugins
from harness.integrations.base import HardwareRoles, IntegrationPlugin
from harness.inventory.model import Device, DeviceKind
from harness.inventory.registry import Inventory


@pytest.mark.mock
def test_smoke_plugin_registered():
    plugins = load_all_plugins()
    names = {p.name for p in plugins}
    assert "_smoke" in names, f"expected _smoke plugin to be loaded via entry point; got {names}"


@pytest.mark.mock
def test_eligibility_reflects_inventory():
    p = IntegrationPlugin(
        name="fake",
        display_name="Fake",
        required_capabilities={"ble"},
        hardware_roles=HardwareRoles(roles={"phone": "android", "peer": "pi"}),
    )
    inv = Inventory([
        Device(id="pixel", kind=DeviceKind.ANDROID, online=True, capabilities={"ble"}),
    ])
    ok, reason = p.eligible(inv)
    assert not ok
    assert "peer" in reason

    inv2 = Inventory([
        Device(id="pixel", kind=DeviceKind.ANDROID, online=True, capabilities={"ble"}),
        Device(id="pi-lab", kind=DeviceKind.PI, online=True, capabilities={"ble"}),
    ])
    ok2, reason2 = p.eligible(inv2)
    assert ok2, reason2
