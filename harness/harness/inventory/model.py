from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from typing import Optional


class DeviceKind(str, Enum):
    ANDROID = "android"
    IOS = "ios"
    PI = "pi"
    LOCAL = "local"
    WINDOWS = "windows"


@dataclass
class Device:
    id: str
    kind: DeviceKind
    capabilities: set[str] = field(default_factory=set)
    labels: dict[str, str] = field(default_factory=dict)

    adb_serial: Optional[str] = None
    udid: Optional[str] = None
    usb_gadget_ip: Optional[str] = None
    ssh_user: Optional[str] = None
    ssh_key_path: Optional[str] = None

    online: bool = False
    reason_offline: Optional[str] = None

    def has(self, *caps: str) -> bool:
        return set(caps).issubset(self.capabilities)


class DeviceUnavailable(Exception):
    """Raised when a required device/role cannot be satisfied from the live inventory."""

    def __init__(self, message: str, *, role: Optional[str] = None, reason: Optional[str] = None):
        super().__init__(message)
        self.role = role
        self.reason = reason
