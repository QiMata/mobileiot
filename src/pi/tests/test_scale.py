"""Scale and reliability tests."""

import asyncio
import json
import struct

import pytest

from gateway.config import OpcUaConfig, RegistryConfig
from gateway.models import (
    DiscoveredDevice,
    OpcUaDataType,
    Protocol,
    TelemetryPoint,
)
from gateway.opcua_server import GatewayOpcUaServer
from gateway.registry import DeviceRegistry

asyncua = pytest.importorskip("asyncua")
Client = asyncua.Client


class TestScaleDeviceRegistration:
    @pytest.mark.asyncio
    async def test_register_50_devices(self, tmp_path):
        """Registry should handle 50 concurrent devices without issues."""
        reg_config = RegistryConfig(
            heartbeat_timeout_s=60.0,
            offline_after_s=120.0,
            persist_path=str(tmp_path / "reg.json"),
        )
        registry = DeviceRegistry(reg_config)

        online_events: list[DiscoveredDevice] = []
        registry.on_device_online(lambda d: _async_append(online_events, d))

        for i in range(50):
            protocol = [Protocol.BLE, Protocol.USB_SERIAL, Protocol.WIFI_DIRECT][i % 3]
            dev = DiscoveredDevice(
                device_id=f"test:{protocol.value}:{i:03d}",
                protocol=protocol,
                protocol_address=f"addr-{i}",
                display_name=f"Device {i}",
            )
            await registry.register_device(dev)

        assert len(registry.get_all_devices()) == 50
        assert len(online_events) == 50

    @pytest.mark.asyncio
    async def test_50_devices_telemetry_throughput(self, tmp_path):
        """50 devices each reporting 4 telemetry points."""
        reg_config = RegistryConfig(
            heartbeat_timeout_s=60.0,
            offline_after_s=120.0,
            persist_path=str(tmp_path / "reg.json"),
        )
        registry = DeviceRegistry(reg_config)

        telemetry_count = 0

        async def count_telemetry(p):
            nonlocal telemetry_count
            telemetry_count += 1

        registry.on_telemetry(count_telemetry)

        # Register 50 devices
        for i in range(50):
            dev = DiscoveredDevice(
                device_id=f"scale:{i:03d}",
                protocol=Protocol.BLE,
                protocol_address=f"addr-{i}",
            )
            await registry.register_device(dev)

        # Each device reports 4 points
        for i in range(50):
            for name in ("temperature", "humidity", "cpu_temp", "uptime"):
                await registry.report_telemetry(
                    TelemetryPoint(
                        device_id=f"scale:{i:03d}",
                        name=name,
                        value=float(i),
                    )
                )

        assert telemetry_count == 200  # 50 * 4


class TestScaleOpcUa:
    @pytest.mark.asyncio
    async def test_50_devices_in_opcua(self, tmp_path):
        """OPC UA server should handle 50 device objects."""
        reg_config = RegistryConfig(
            heartbeat_timeout_s=60.0,
            offline_after_s=120.0,
            persist_path=str(tmp_path / "reg.json"),
        )
        registry = DeviceRegistry(reg_config)
        opcua_config = OpcUaConfig(
            endpoint="opc.tcp://127.0.0.1:48420",
            security_mode="None",
            anonymous_allowed=True,
        )
        server = GatewayOpcUaServer(opcua_config, registry)
        await server.start()

        # Register 50 devices and report telemetry
        for i in range(50):
            dev = DiscoveredDevice(
                device_id=f"scale:{i:03d}",
                protocol=Protocol.BLE,
                protocol_address=f"addr-{i}",
                display_name=f"Device {i}",
            )
            await server._on_device_online(dev)
            await server._on_telemetry(
                TelemetryPoint(
                    device_id=f"scale:{i:03d}",
                    name="temperature",
                    value=20.0 + i * 0.1,
                )
            )

        # Verify a sample of nodes
        async with Client(url="opc.tcp://127.0.0.1:48420") as client:
            for i in [0, 24, 49]:
                node = client.get_node(
                    f"ns=2;s=Devices/scale:{i:03d}/Telemetry/temperature"
                )
                val = await node.read_value()
                expected = 20.0 + i * 0.1
                assert abs(val - expected) < 0.01, f"Device {i}: {val} != {expected}"

        await server.stop()


class TestRestartResume:
    @pytest.mark.asyncio
    async def test_persist_and_reload_50_devices(self, tmp_path):
        """Registry should persist and reload 50 devices."""
        config = RegistryConfig(
            heartbeat_timeout_s=60.0,
            offline_after_s=120.0,
            persist_path=str(tmp_path / "registry.json"),
        )
        reg = DeviceRegistry(config)

        for i in range(50):
            await reg.register_device(
                DiscoveredDevice(
                    device_id=f"persist:{i:03d}",
                    protocol=Protocol.BLE,
                    protocol_address=f"addr-{i}",
                )
            )

        await reg.persist()

        # Reload
        reg2 = DeviceRegistry(config)
        reg2._load_persisted()
        assert len(reg2.get_all_devices()) == 50


async def _async_append(lst, item):
    lst.append(item)
