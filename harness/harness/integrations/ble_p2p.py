"""BLE peer-to-peer integration — two phones, one as peripheral and one as central.

Hardware-tier scope: one phone advertises a GATT service via
`IBleAdvertiseService` and the other scans-connects-exchanges via
`IBleP2PCentralService`. Mirrors the NFC two-phone shape exactly. iOS can
play either role thanks to the native `CBPeripheralManager` wrapper.

The declared `hardware_roles` describe the most-common pair (two Androids);
iOS variants override via per-test `pytest.mark.hardware(roles=[...])`.
"""
from harness.integrations.base import HardwareRoles, IntegrationPlugin


plugin = IntegrationPlugin(
    name="ble_p2p",
    display_name="BLE P2P (phone ↔ phone)",
    required_capabilities={"ble"},
    hardware_roles=HardwareRoles(roles={"peripheral": "android", "central": "android"}),
    description="Two phones with BLE: one advertises a GATT service, the other reads/writes.",
    tags={"ble", "p2p"},
)
