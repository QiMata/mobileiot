"""NFC peer-to-peer integration — two Android phones placed back-to-back.

Hardware-tier scope today: confirm both phones expose an NFC radio that can be
brought up via adb. A full HCE / reader-mode exchange will require a matching
in-app scenario (see Scenarios/) and is out of scope for the initial plugin.
"""
from harness.integrations.base import HardwareRoles, IntegrationPlugin


plugin = IntegrationPlugin(
    name="nfc",
    display_name="NFC P2P",
    required_capabilities={"nfc"},
    hardware_roles=HardwareRoles(roles={"phone_a": "android", "phone_b": "android"}),
    description="Two Android phones with NFC radios for HCE / reader-mode exchanges.",
    tags={"nfc", "p2p"},
)
