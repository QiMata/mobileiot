"""USB integration — Android-OTG-host ↔ Pi-ConfigFS-gadget round-trip + iOS structural.

Hardware-tier scope:

- An Android phone with USB-OTG (capability ``usb_host``) opens a USB connection
  to a Pi running ``src/pi/usb_demo.py`` (ConfigFS Loopback gadget; capability
  ``serial_gadget`` on the Pi) and exercises the existing ``usb-bulk`` scenario.
- An iOS phone POSTs the same ``usb-bulk`` scenario. With an MFi accessory
  attached, this is a real round-trip; without one it returns a clean
  ``Skipped("No USB bulk devices found")`` that the test treats as a pass — the
  point is to verify the External Accessory pipeline + Info.plist
  ``UISupportedExternalAccessoryProtocols`` + DI wiring of ``IUsbCommunicator``
  rather than to require lab hardware.

Why ``required_capabilities`` is empty here:

The harness gate ``evaluate_hardware_requirement`` (see
``harness/fidelity.py:34-50``) applies the *same* capability set to every role
in the marker. We can't put ``usb_host`` on the plugin because the Pi role
wouldn't satisfy it, and we can't put ``serial_gadget`` because the Android
role wouldn't satisfy it. The hardware tests filter per role inside the test
body (same idiom as ``test_nfc_hw.py:26-30``: ``[d for d, t in android_phones
if "usb_host" in d.capabilities]``).

Out of scope today:

- ``usb-serial`` scenario — needs a CDC/FTDI dongle, not a loopback gadget.
"""
from harness.integrations.base import HardwareRoles, IntegrationPlugin


plugin = IntegrationPlugin(
    name="usb",
    display_name="USB Bulk",
    required_capabilities=set(),
    hardware_roles=HardwareRoles(roles={"phone": "android", "gadget": "pi"}),
    description=(
        "Android USB-OTG host ↔ Pi ConfigFS gadget round-trip, plus iOS "
        "External Accessory structural check (skips cleanly without an MFi cable)."
    ),
    tags={"usb", "bulk"},
)
