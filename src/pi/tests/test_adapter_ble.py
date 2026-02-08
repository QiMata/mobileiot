"""Tests for gateway.adapters.ble — BLE central adapter."""

import struct

import pytest
from unittest.mock import AsyncMock, MagicMock, patch

from gateway.adapters.ble import (
    BleAdapter,
    HUM_CHAR_UUID,
    LED_CHAR_UUID,
    SERVICE_UUID,
    TEMP_CHAR_UUID,
)
from gateway.command_router import CommandRouter
from gateway.config import BleAdapterConfig, RegistryConfig
from gateway.models import CommandRequest, Protocol
from gateway.registry import DeviceRegistry


@pytest.fixture
def ble_stack(tmp_path):
    reg_config = RegistryConfig(
        heartbeat_timeout_s=10.0,
        offline_after_s=20.0,
        persist_path=str(tmp_path / "reg.json"),
    )
    registry = DeviceRegistry(reg_config)
    router = CommandRouter(registry)
    config = BleAdapterConfig()
    adapter = BleAdapter(config, registry, router)
    return adapter, registry, router


class TestBleDiscover:
    @pytest.mark.asyncio
    @patch("gateway.adapters.ble.BleakScanner.discover")
    async def test_discover_filters_by_name(self, mock_scan, ble_stack):
        adapter, _, _ = ble_stack

        mock_device = MagicMock()
        mock_device.name = "PiSensor"
        mock_device.address = "AA:BB:CC:DD:EE:FF"
        mock_device.rssi = -45

        mock_other = MagicMock()
        mock_other.name = "OtherDevice"
        mock_other.address = "11:22:33:44:55:66"
        mock_other.rssi = -80

        mock_scan.return_value = [mock_device, mock_other]

        devices = await adapter.discover()
        assert len(devices) == 1
        assert devices[0].device_id == "ble:AA:BB:CC:DD:EE:FF"
        assert devices[0].display_name == "PiSensor"
        assert devices[0].protocol == Protocol.BLE

    @pytest.mark.asyncio
    @patch("gateway.adapters.ble.BleakScanner.discover")
    async def test_discover_empty_scan(self, mock_scan, ble_stack):
        adapter, _, _ = ble_stack
        mock_scan.return_value = []
        devices = await adapter.discover()
        assert devices == []

    @pytest.mark.asyncio
    @patch("gateway.adapters.ble.BleakScanner.discover")
    async def test_discover_includes_rssi_in_metadata(self, mock_scan, ble_stack):
        adapter, _, _ = ble_stack

        mock_device = MagicMock()
        mock_device.name = "PiSensor"
        mock_device.address = "AA:BB:CC:DD:EE:FF"
        mock_device.rssi = -55

        mock_scan.return_value = [mock_device]
        devices = await adapter.discover()
        assert devices[0].metadata["rssi"] == -55


class TestBleReadPoints:
    @pytest.mark.asyncio
    async def test_read_temperature_and_humidity(self, ble_stack):
        adapter, _, _ = ble_stack

        mock_client = AsyncMock()
        mock_client.is_connected = True
        mock_client.read_gatt_char = AsyncMock(
            side_effect=[
                struct.pack("<h", 2345),  # temp: 23.45 C
                struct.pack("<h", 5570),  # hum: 55.70 %
            ]
        )
        adapter._clients["ble:AA:BB:CC:DD:EE:FF"] = mock_client

        points = await adapter.read_points("ble:AA:BB:CC:DD:EE:FF")
        assert len(points) == 2
        assert abs(points[0].value - 23.45) < 0.01
        assert points[0].name == "temperature"
        assert abs(points[1].value - 55.70) < 0.01
        assert points[1].name == "humidity"

    @pytest.mark.asyncio
    async def test_read_disconnected_returns_empty(self, ble_stack):
        adapter, _, _ = ble_stack

        mock_client = AsyncMock()
        mock_client.is_connected = False
        adapter._clients["ble:AA:BB:CC"] = mock_client

        points = await adapter.read_points("ble:AA:BB:CC")
        assert points == []

    @pytest.mark.asyncio
    async def test_read_unknown_device_returns_empty(self, ble_stack):
        adapter, _, _ = ble_stack
        points = await adapter.read_points("ble:UNKNOWN")
        assert points == []

    @pytest.mark.asyncio
    async def test_read_negative_temperature(self, ble_stack):
        adapter, _, _ = ble_stack

        mock_client = AsyncMock()
        mock_client.is_connected = True
        mock_client.read_gatt_char = AsyncMock(
            side_effect=[
                struct.pack("<h", -500),  # -5.00 C
                struct.pack("<h", 3000),  # 30.00 %
            ]
        )
        adapter._clients["ble:AA:BB:CC"] = mock_client

        points = await adapter.read_points("ble:AA:BB:CC")
        assert abs(points[0].value - (-5.0)) < 0.01


class TestBleCommand:
    @pytest.mark.asyncio
    async def test_led_on_writes_0x01(self, ble_stack):
        adapter, _, _ = ble_stack

        mock_client = AsyncMock()
        mock_client.is_connected = True
        adapter._clients["ble:AA:BB:CC"] = mock_client

        req = CommandRequest(device_id="ble:AA:BB:CC", command="LED_ON")
        resp = await adapter.execute_command(req)
        assert resp.success
        mock_client.write_gatt_char.assert_called_once_with(
            LED_CHAR_UUID, bytes([0x01])
        )

    @pytest.mark.asyncio
    async def test_led_off_writes_0x00(self, ble_stack):
        adapter, _, _ = ble_stack

        mock_client = AsyncMock()
        mock_client.is_connected = True
        adapter._clients["ble:AA:BB:CC"] = mock_client

        req = CommandRequest(device_id="ble:AA:BB:CC", command="LED_OFF")
        resp = await adapter.execute_command(req)
        assert resp.success
        mock_client.write_gatt_char.assert_called_once_with(
            LED_CHAR_UUID, bytes([0x00])
        )

    @pytest.mark.asyncio
    async def test_disallowed_command_rejected(self, ble_stack):
        adapter, _, _ = ble_stack

        mock_client = AsyncMock()
        mock_client.is_connected = True
        adapter._clients["ble:AA:BB:CC"] = mock_client

        req = CommandRequest(device_id="ble:AA:BB:CC", command="REBOOT")
        resp = await adapter.execute_command(req)
        assert not resp.success
        assert "allowlist" in resp.message

    @pytest.mark.asyncio
    async def test_command_on_disconnected_device(self, ble_stack):
        adapter, _, _ = ble_stack

        mock_client = AsyncMock()
        mock_client.is_connected = False
        adapter._clients["ble:AA:BB:CC"] = mock_client

        req = CommandRequest(device_id="ble:AA:BB:CC", command="LED_ON")
        resp = await adapter.execute_command(req)
        assert not resp.success
        assert "not connected" in resp.message.lower()

    @pytest.mark.asyncio
    async def test_command_on_unknown_device(self, ble_stack):
        adapter, _, _ = ble_stack

        req = CommandRequest(device_id="ble:UNKNOWN", command="LED_ON")
        resp = await adapter.execute_command(req)
        assert not resp.success
