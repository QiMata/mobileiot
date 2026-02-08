"""End-to-end integration tests with simulated peripherals."""

import asyncio
import struct

import pytest
from unittest.mock import AsyncMock, MagicMock, patch

from gateway.adapters.wifidirect import WifiDirectFraming
from gateway.config import (
    BleAdapterConfig,
    GatewayConfig,
    HealthConfig,
    OpcUaConfig,
    RegistryConfig,
    SerialAdapterConfig,
    WifiDirectAdapterConfig,
)
from gateway.models import Protocol

asyncua = pytest.importorskip("asyncua")
Client = asyncua.Client


def _make_test_config(tmp_path, opcua_port, wifidirect_port=0):
    return GatewayConfig(
        ble=BleAdapterConfig(enabled=False),
        serial=SerialAdapterConfig(enabled=False),
        wifi_direct=WifiDirectAdapterConfig(
            enabled=wifidirect_port > 0,
            listen_host="127.0.0.1",
            listen_port=wifidirect_port,
            reconnect_delay_s=1.0,
        ),
        opcua=OpcUaConfig(
            endpoint=f"opc.tcp://127.0.0.1:{opcua_port}",
            security_mode="None",
            anonymous_allowed=True,
        ),
        registry=RegistryConfig(
            heartbeat_timeout_s=30.0,
            offline_after_s=60.0,
            persist_path=str(tmp_path / "registry.json"),
        ),
        health=HealthConfig(enabled=False),
    )


class TestBleIntegration:
    @pytest.mark.asyncio
    @patch("gateway.adapters.ble.BleakScanner.discover")
    @patch("gateway.adapters.ble.BleakClient")
    async def test_ble_device_appears_in_opcua(
        self, MockBleakClient, mock_discover, tmp_path
    ):
        """Simulated BLE device should create OPC UA nodes."""
        from gateway.adapters.ble import BleAdapter
        from gateway.command_router import CommandRouter
        from gateway.opcua_server import GatewayOpcUaServer
        from gateway.registry import DeviceRegistry

        config = _make_test_config(tmp_path, opcua_port=48410)
        config.ble.enabled = True

        registry = DeviceRegistry(config.registry)
        opcua = GatewayOpcUaServer(config.opcua, registry)
        router = CommandRouter(registry)

        # Mock BLE scanner
        mock_device = MagicMock()
        mock_device.name = "PiSensor"
        mock_device.address = "AA:BB:CC:DD:EE:FF"
        mock_device.rssi = -45
        mock_discover.return_value = [mock_device]

        # Mock BLE client
        mock_client_instance = AsyncMock()
        mock_client_instance.is_connected = True
        mock_client_instance.connect = AsyncMock()
        mock_client_instance.disconnect = AsyncMock()
        mock_client_instance.read_gatt_char = AsyncMock(
            side_effect=[
                struct.pack("<h", 2345),  # temp
                struct.pack("<h", 5570),  # hum
            ]
        )
        MockBleakClient.return_value = mock_client_instance

        await opcua.start()
        adapter = BleAdapter(config.ble, registry, router)

        # Run one discovery + read cycle manually
        devices = await adapter.discover()
        for d in devices:
            await registry.register_device(d)

        adapter._clients["ble:AA:BB:CC:DD:EE:FF"] = mock_client_instance
        points = await adapter.read_points("ble:AA:BB:CC:DD:EE:FF")
        for p in points:
            await registry.report_telemetry(p)

        # Verify OPC UA nodes
        async with Client(url=f"opc.tcp://127.0.0.1:48410") as client:
            node = client.get_node(
                "ns=2;s=Devices/ble:AA:BB:CC:DD:EE:FF/Meta/protocol"
            )
            assert await node.read_value() == "ble"

            node = client.get_node(
                "ns=2;s=Devices/ble:AA:BB:CC:DD:EE:FF/Telemetry/temperature"
            )
            val = await node.read_value()
            assert abs(val - 23.45) < 0.01

        await opcua.stop()


class TestWifiDirectIntegration:
    @pytest.mark.asyncio
    async def test_wifidirect_telemetry_flows_to_opcua(self, tmp_path):
        """WiFi Direct telemetry should appear in OPC UA namespace."""
        from gateway.adapters.wifidirect import WifiDirectAdapter
        from gateway.command_router import CommandRouter
        from gateway.opcua_server import GatewayOpcUaServer
        from gateway.registry import DeviceRegistry

        # Start a mock WiFi Direct server
        telemetry_sent = asyncio.Event()

        async def mock_handler(reader, writer):
            # Read the start_telemetry request
            header = await reader.readexactly(4)
            length = struct.unpack(">I", header)[0]
            await reader.readexactly(length)

            # Send telemetry
            msg = {
                "type": "telemetry",
                "ts": 1700000000,
                "temp": 22.5,
                "hum": 65.0,
                "cpu_temp": 42.0,
                "uptime": 3600,
            }
            writer.write(WifiDirectFraming.encode(msg))
            await writer.drain()
            telemetry_sent.set()
            await asyncio.sleep(3)
            writer.close()

        server = await asyncio.start_server(mock_handler, "127.0.0.1", 0)
        wd_port = server.sockets[0].getsockname()[1]

        config = _make_test_config(
            tmp_path, opcua_port=48411, wifidirect_port=wd_port
        )
        registry = DeviceRegistry(config.registry)
        opcua = GatewayOpcUaServer(config.opcua, registry)
        router = CommandRouter(registry)

        await opcua.start()
        adapter = WifiDirectAdapter(config.wifi_direct, registry, router)
        await adapter.start()

        # Wait for telemetry to flow
        await asyncio.wait_for(telemetry_sent.wait(), timeout=5.0)
        await asyncio.sleep(0.5)  # give registry callback time to fire

        # Verify OPC UA
        device_id = f"wifidirect:127.0.0.1:{wd_port}"
        async with Client(url="opc.tcp://127.0.0.1:48411") as client:
            node = client.get_node(
                f"ns=2;s=Devices/{device_id}/Telemetry/temperature"
            )
            val = await node.read_value()
            assert abs(val - 22.5) < 0.1

        await adapter.stop()
        await opcua.stop()
        server.close()
        await server.wait_closed()
