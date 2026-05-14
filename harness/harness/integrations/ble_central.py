"""BLE central integration — one phone reads a Pi BLE peripheral.

Hardware-tier scope: an Android (or iOS) phone runs the existing `ble-gatt`
scenario against `src/pi/bluetoothle_demo.py` (bluezero "PiDHTSensor"). The
peripheral side is the Pi; the phone is the central.

Capability gate is `ble` on both roles. iOS variants are picked up by per-test
`pytest.mark.hardware` role overrides; the plugin's declared roles describe the
most-common pair (Android central + Pi peripheral).
"""
from harness.integrations.base import HardwareRoles, IntegrationPlugin


plugin = IntegrationPlugin(
    name="ble_central",
    display_name="BLE central (phone ↔ Pi)",
    required_capabilities={"ble"},
    hardware_roles=HardwareRoles(roles={"central": "android", "peripheral": "pi"}),
    description="One phone (Android or iOS) reads a Pi BLE peripheral via the ble-gatt scenario.",
    tags={"ble", "central"},
)
