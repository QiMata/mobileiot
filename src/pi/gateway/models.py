"""Canonical data types shared across all gateway components."""

from __future__ import annotations

import enum
import uuid
from dataclasses import dataclass, field
from datetime import datetime, timezone
from typing import Any


class Protocol(enum.Enum):
    BLE = "ble"
    USB_SERIAL = "usb_serial"
    WIFI_DIRECT = "wifi_direct"


class DeviceStatus(enum.Enum):
    ONLINE = "online"
    OFFLINE = "offline"
    STALE = "stale"


class OpcUaDataType(enum.Enum):
    FLOAT = "Float"
    DOUBLE = "Double"
    INT16 = "Int16"
    INT32 = "Int32"
    STRING = "String"
    BOOLEAN = "Boolean"


@dataclass
class DiscoveredDevice:
    """A physical device discovered by a protocol adapter."""

    device_id: str
    protocol: Protocol
    protocol_address: str
    display_name: str = ""
    metadata: dict[str, Any] = field(default_factory=dict)
    last_seen: datetime = field(default_factory=lambda: datetime.now(timezone.utc))
    status: DeviceStatus = DeviceStatus.ONLINE
    firmware_version: str = ""


@dataclass
class TelemetryPoint:
    """A single telemetry reading from a device."""

    device_id: str
    name: str
    value: Any
    datatype: OpcUaDataType = OpcUaDataType.DOUBLE
    timestamp: datetime = field(default_factory=lambda: datetime.now(timezone.utc))
    quality: str = "Good"


@dataclass
class CommandRequest:
    """A write-back command to be routed to a device."""

    device_id: str
    command: str
    args: dict[str, Any] = field(default_factory=dict)
    request_id: str = field(default_factory=lambda: str(uuid.uuid4()))


@dataclass
class CommandResponse:
    """Result of executing a command on a device."""

    request_id: str
    success: bool
    message: str = ""
    timestamp: datetime = field(default_factory=lambda: datetime.now(timezone.utc))
