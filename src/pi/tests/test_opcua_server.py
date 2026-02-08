"""Tests for gateway.opcua_server — OPC UA node management."""

import pytest

from gateway.config import OpcUaConfig, RegistryConfig
from gateway.models import (
    DeviceStatus,
    DiscoveredDevice,
    OpcUaDataType,
    Protocol,
    TelemetryPoint,
)
from gateway.opcua_server import GatewayOpcUaServer
from gateway.registry import DeviceRegistry

# These tests require asyncua to be installed.
asyncua = pytest.importorskip("asyncua")
Client = asyncua.Client


@pytest.fixture
async def opcua_stack(tmp_path):
    """Start a test OPC UA server + registry."""
    reg_config = RegistryConfig(
        heartbeat_timeout_s=60.0,
        offline_after_s=120.0,
        persist_path=str(tmp_path / "reg.json"),
    )
    registry = DeviceRegistry(reg_config)

    opcua_config = OpcUaConfig(
        endpoint="opc.tcp://127.0.0.1:48400",
        security_mode="None",
        anonymous_allowed=True,
    )
    server = GatewayOpcUaServer(opcua_config, registry)
    await server.start()

    yield server, registry

    await server.stop()


class TestOpcUaNodeCreation:
    @pytest.mark.asyncio
    async def test_device_online_creates_meta_nodes(self, opcua_stack):
        server, registry = opcua_stack

        dev = DiscoveredDevice(
            device_id="ble:AA:BB:CC",
            protocol=Protocol.BLE,
            protocol_address="AA:BB:CC",
            display_name="TestSensor",
        )
        await server._on_device_online(dev)

        async with Client(url="opc.tcp://127.0.0.1:48400") as client:
            node = client.get_node("ns=2;s=Devices/ble:AA:BB:CC/Meta/protocol")
            val = await node.read_value()
            assert val == "ble"

            node = client.get_node("ns=2;s=Devices/ble:AA:BB:CC/Meta/status")
            val = await node.read_value()
            assert val == "online"

            node = client.get_node("ns=2;s=Devices/ble:AA:BB:CC/Meta/display_name")
            val = await node.read_value()
            assert val == "TestSensor"

    @pytest.mark.asyncio
    async def test_duplicate_device_online_updates_status(self, opcua_stack):
        server, registry = opcua_stack

        dev = DiscoveredDevice(
            device_id="ble:AA:BB:CC",
            protocol=Protocol.BLE,
            protocol_address="AA:BB:CC",
        )
        await server._on_device_online(dev)
        # Mark offline then bring back online
        await server._on_device_offline(dev)
        await server._on_device_online(dev)

        async with Client(url="opc.tcp://127.0.0.1:48400") as client:
            node = client.get_node("ns=2;s=Devices/ble:AA:BB:CC/Meta/status")
            val = await node.read_value()
            assert val == "online"


class TestOpcUaTelemetry:
    @pytest.mark.asyncio
    async def test_telemetry_creates_and_updates_variable(self, opcua_stack):
        server, registry = opcua_stack

        dev = DiscoveredDevice(
            device_id="ble:AA:BB:CC",
            protocol=Protocol.BLE,
            protocol_address="AA:BB:CC",
        )
        await server._on_device_online(dev)

        # First telemetry point creates the variable
        point = TelemetryPoint(
            device_id="ble:AA:BB:CC",
            name="temperature",
            value=23.45,
            datatype=OpcUaDataType.DOUBLE,
        )
        await server._on_telemetry(point)

        async with Client(url="opc.tcp://127.0.0.1:48400") as client:
            node = client.get_node(
                "ns=2;s=Devices/ble:AA:BB:CC/Telemetry/temperature"
            )
            val = await node.read_value()
            assert abs(val - 23.45) < 0.01

        # Update value
        point2 = TelemetryPoint(
            device_id="ble:AA:BB:CC",
            name="temperature",
            value=25.00,
            datatype=OpcUaDataType.DOUBLE,
        )
        await server._on_telemetry(point2)

        async with Client(url="opc.tcp://127.0.0.1:48400") as client:
            node = client.get_node(
                "ns=2;s=Devices/ble:AA:BB:CC/Telemetry/temperature"
            )
            val = await node.read_value()
            assert abs(val - 25.00) < 0.01

    @pytest.mark.asyncio
    async def test_telemetry_for_unregistered_device_is_ignored(self, opcua_stack):
        server, _ = opcua_stack

        point = TelemetryPoint(
            device_id="unknown:device",
            name="temperature",
            value=23.45,
        )
        # Should not raise
        await server._on_telemetry(point)


class TestOpcUaDeviceOffline:
    @pytest.mark.asyncio
    async def test_device_offline_updates_status(self, opcua_stack):
        server, registry = opcua_stack

        dev = DiscoveredDevice(
            device_id="ble:AA:BB:CC",
            protocol=Protocol.BLE,
            protocol_address="AA:BB:CC",
        )
        await server._on_device_online(dev)
        await server._on_device_offline(dev)

        async with Client(url="opc.tcp://127.0.0.1:48400") as client:
            node = client.get_node("ns=2;s=Devices/ble:AA:BB:CC/Meta/status")
            val = await node.read_value()
            assert val == "offline"


class TestOpcUaServerLifecycle:
    @pytest.mark.asyncio
    async def test_is_running_property(self, tmp_path):
        reg_config = RegistryConfig(
            heartbeat_timeout_s=60.0,
            offline_after_s=120.0,
            persist_path=str(tmp_path / "reg.json"),
        )
        registry = DeviceRegistry(reg_config)
        opcua_config = OpcUaConfig(
            endpoint="opc.tcp://127.0.0.1:48401",
            security_mode="None",
            anonymous_allowed=True,
        )
        server = GatewayOpcUaServer(opcua_config, registry)
        assert not server.is_running

        await server.start()
        assert server.is_running

        await server.stop()
        assert not server.is_running
