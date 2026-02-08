"""Tests for gateway.models dataclasses."""

from datetime import datetime, timezone

from gateway.models import (
    CommandRequest,
    CommandResponse,
    DeviceStatus,
    DiscoveredDevice,
    OpcUaDataType,
    Protocol,
    TelemetryPoint,
)


class TestProtocolEnum:
    def test_values(self):
        assert Protocol.BLE.value == "ble"
        assert Protocol.USB_SERIAL.value == "usb_serial"
        assert Protocol.WIFI_DIRECT.value == "wifi_direct"


class TestDiscoveredDevice:
    def test_defaults(self):
        d = DiscoveredDevice(
            device_id="ble:AA:BB:CC",
            protocol=Protocol.BLE,
            protocol_address="AA:BB:CC",
        )
        assert d.status == DeviceStatus.ONLINE
        assert d.display_name == ""
        assert d.metadata == {}
        assert isinstance(d.last_seen, datetime)

    def test_metadata_is_independent(self):
        d1 = DiscoveredDevice(
            device_id="a", protocol=Protocol.BLE, protocol_address="x"
        )
        d2 = DiscoveredDevice(
            device_id="b", protocol=Protocol.BLE, protocol_address="y"
        )
        d1.metadata["key"] = "val"
        assert "key" not in d2.metadata


class TestTelemetryPoint:
    def test_defaults(self):
        p = TelemetryPoint(device_id="d", name="temperature", value=23.5)
        assert p.datatype == OpcUaDataType.DOUBLE
        assert p.quality == "Good"

    def test_custom_datatype(self):
        p = TelemetryPoint(
            device_id="d",
            name="uptime",
            value=1234,
            datatype=OpcUaDataType.INT32,
        )
        assert p.datatype == OpcUaDataType.INT32


class TestCommandRequest:
    def test_auto_generates_request_id(self):
        r1 = CommandRequest(device_id="d", command="LED_ON")
        r2 = CommandRequest(device_id="d", command="LED_ON")
        assert r1.request_id != r2.request_id


class TestCommandResponse:
    def test_creation(self):
        r = CommandResponse(request_id="abc", success=True, message="ok")
        assert r.success
        assert r.message == "ok"
