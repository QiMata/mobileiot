"""Tests for gateway.adapters.usb_serial — USB serial adapter."""

import pytest
from unittest.mock import AsyncMock, MagicMock, patch

from gateway.adapters.usb_serial import SerialConnection, UsbSerialAdapter, _parse_kv_response
from gateway.command_router import CommandRouter
from gateway.config import RegistryConfig, SerialAdapterConfig
from gateway.models import CommandRequest, OpcUaDataType, Protocol
from gateway.registry import DeviceRegistry


@pytest.fixture
def serial_stack(tmp_path):
    reg_config = RegistryConfig(
        heartbeat_timeout_s=10.0,
        offline_after_s=20.0,
        persist_path=str(tmp_path / "reg.json"),
    )
    registry = DeviceRegistry(reg_config)
    router = CommandRouter(registry)
    config = SerialAdapterConfig()
    adapter = UsbSerialAdapter(config, registry, router)
    return adapter, registry, router


class TestParseKvResponse:
    def test_basic_parsing(self):
        points = _parse_kv_response("dev1", "temp=23.4,hum=55.7")
        assert len(points) == 2
        assert points[0].name == "temp"
        assert points[0].value == 23.4
        assert points[1].name == "hum"

    def test_with_name_map(self):
        points = _parse_kv_response(
            "dev1", "temp=23.4,hum=55.7", {"temp": "temperature", "hum": "humidity"}
        )
        assert points[0].name == "temperature"
        assert points[1].name == "humidity"

    def test_non_numeric_skipped(self):
        points = _parse_kv_response("dev1", "temp=23.4,status=ok")
        assert len(points) == 1
        assert points[0].name == "temp"

    def test_empty_payload(self):
        points = _parse_kv_response("dev1", "")
        assert points == []

    def test_malformed_pairs_skipped(self):
        points = _parse_kv_response("dev1", "temp=23.4,badpair,hum=55.7")
        assert len(points) == 2


class TestSerialReadPoints:
    @pytest.mark.asyncio
    async def test_read_points_parses_sensor_and_status(self, serial_stack):
        adapter, _, _ = serial_stack

        mock_conn = AsyncMock(spec=SerialConnection)
        mock_conn.send_command = AsyncMock(
            side_effect=[
                "SENSOR:temp=23.4,hum=55.7",
                "STATUS:uptime=1234,cpu_temp=45.2,mem_free=123456",
            ]
        )
        adapter._connections["serial:1234:5678:test"] = mock_conn

        points = await adapter.read_points("serial:1234:5678:test")
        names = {p.name for p in points}
        assert "temperature" in names
        assert "humidity" in names
        assert "uptime" in names
        assert "cpu_temp" in names
        assert "mem_free" in names

    @pytest.mark.asyncio
    async def test_read_points_handles_no_response(self, serial_stack):
        adapter, _, _ = serial_stack

        mock_conn = AsyncMock(spec=SerialConnection)
        mock_conn.send_command = AsyncMock(return_value=None)
        adapter._connections["serial:test"] = mock_conn

        points = await adapter.read_points("serial:test")
        assert points == []

    @pytest.mark.asyncio
    async def test_read_points_unknown_device(self, serial_stack):
        adapter, _, _ = serial_stack
        points = await adapter.read_points("serial:unknown")
        assert points == []


class TestSerialCommand:
    @pytest.mark.asyncio
    async def test_allowed_command_succeeds(self, serial_stack):
        adapter, _, _ = serial_stack

        mock_conn = AsyncMock(spec=SerialConnection)
        mock_conn.send_command = AsyncMock(return_value="ACK: LED turned ON")
        adapter._connections["serial:test"] = mock_conn

        req = CommandRequest(device_id="serial:test", command="LED_ON")
        resp = await adapter.execute_command(req)
        assert resp.success
        assert "ACK" in resp.message

    @pytest.mark.asyncio
    async def test_allowed_led_off(self, serial_stack):
        adapter, _, _ = serial_stack

        mock_conn = AsyncMock(spec=SerialConnection)
        mock_conn.send_command = AsyncMock(return_value="ACK: LED turned OFF")
        adapter._connections["serial:test"] = mock_conn

        req = CommandRequest(device_id="serial:test", command="LED_OFF")
        resp = await adapter.execute_command(req)
        assert resp.success

    @pytest.mark.asyncio
    async def test_gpio_write_builds_command_string(self, serial_stack):
        adapter, _, _ = serial_stack

        mock_conn = AsyncMock(spec=SerialConnection)
        mock_conn.send_command = AsyncMock(return_value="ACK: GPIO 17 set to 1")
        adapter._connections["serial:test"] = mock_conn

        req = CommandRequest(
            device_id="serial:test",
            command="GPIO_WRITE",
            args={"pin": "17", "value": "1"},
        )
        resp = await adapter.execute_command(req)
        assert resp.success
        mock_conn.send_command.assert_called_once_with("GPIO_WRITE 17 1")

    @pytest.mark.asyncio
    async def test_non_allowed_command_rejected(self, serial_stack):
        adapter, _, _ = serial_stack

        mock_conn = AsyncMock(spec=SerialConnection)
        adapter._connections["serial:test"] = mock_conn

        req = CommandRequest(device_id="serial:test", command="FORMAT_DISK")
        resp = await adapter.execute_command(req)
        assert not resp.success
        assert "allowlist" in resp.message

    @pytest.mark.asyncio
    async def test_nack_response_fails(self, serial_stack):
        adapter, _, _ = serial_stack

        mock_conn = AsyncMock(spec=SerialConnection)
        mock_conn.send_command = AsyncMock(return_value="NACK: Invalid command")
        adapter._connections["serial:test"] = mock_conn

        req = CommandRequest(device_id="serial:test", command="LED_ON")
        resp = await adapter.execute_command(req)
        assert not resp.success

    @pytest.mark.asyncio
    async def test_command_on_disconnected_device(self, serial_stack):
        adapter, _, _ = serial_stack

        req = CommandRequest(device_id="serial:missing", command="LED_ON")
        resp = await adapter.execute_command(req)
        assert not resp.success
        assert "not connected" in resp.message.lower()
