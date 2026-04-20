from harness.transports.base import Transport, TransportResult, TransportError
from harness.transports.local import LocalHostTransport
from harness.transports.adb import AndroidTransport
from harness.transports.idevice import IosTransport
from harness.transports.pi_usb import PiTransport
from harness.transports.windows_local import WindowsLocalTransport

__all__ = [
    "Transport",
    "TransportResult",
    "TransportError",
    "LocalHostTransport",
    "AndroidTransport",
    "IosTransport",
    "PiTransport",
    "WindowsLocalTransport",
]


def transport_for(device) -> Transport:
    from harness.inventory.model import DeviceKind
    if device.kind == DeviceKind.ANDROID:
        return AndroidTransport(device)
    if device.kind == DeviceKind.IOS:
        return IosTransport(device)
    if device.kind == DeviceKind.PI:
        return PiTransport(device)
    if device.kind == DeviceKind.WINDOWS:
        return WindowsLocalTransport(device)
    return LocalHostTransport()
