"""Tests for gateway.registry — device tracking, heartbeat, persistence."""

import asyncio
import json

import pytest

from gateway.config import RegistryConfig
from gateway.models import (
    DeviceStatus,
    DiscoveredDevice,
    Protocol,
    TelemetryPoint,
)
from gateway.registry import DeviceRegistry


@pytest.fixture
def registry(tmp_path):
    config = RegistryConfig(
        heartbeat_timeout_s=5.0,
        offline_after_s=10.0,
        persist_path=str(tmp_path / "registry.json"),
    )
    return DeviceRegistry(config)


def _make_device(device_id="ble:AA:BB:CC", protocol=Protocol.BLE):
    return DiscoveredDevice(
        device_id=device_id,
        protocol=protocol,
        protocol_address=device_id.split(":", 1)[1] if ":" in device_id else "",
        display_name="TestDevice",
    )


class TestRegisterDevice:
    @pytest.mark.asyncio
    async def test_register_fires_online_callback(self, registry):
        events = []
        registry.on_device_online(lambda d: _async_append(events, d))

        await registry.register_device(_make_device())
        assert len(events) == 1
        assert events[0].device_id == "ble:AA:BB:CC"

    @pytest.mark.asyncio
    async def test_re_register_online_device_no_duplicate(self, registry):
        events = []
        registry.on_device_online(lambda d: _async_append(events, d))

        dev = _make_device()
        await registry.register_device(dev)
        await registry.register_device(dev)
        assert len(events) == 1

    @pytest.mark.asyncio
    async def test_re_register_offline_device_fires_again(self, registry):
        events = []
        registry.on_device_online(lambda d: _async_append(events, d))

        dev = _make_device()
        await registry.register_device(dev)
        dev.status = DeviceStatus.OFFLINE
        registry._devices[dev.device_id].status = DeviceStatus.OFFLINE
        await registry.register_device(dev)
        assert len(events) == 2

    @pytest.mark.asyncio
    async def test_get_device(self, registry):
        dev = _make_device()
        await registry.register_device(dev)
        result = registry.get_device("ble:AA:BB:CC")
        assert result is not None
        assert result.device_id == "ble:AA:BB:CC"

    @pytest.mark.asyncio
    async def test_get_all_devices(self, registry):
        await registry.register_device(_make_device("ble:11:22:33"))
        await registry.register_device(_make_device("ble:44:55:66"))
        assert len(registry.get_all_devices()) == 2


class TestTelemetry:
    @pytest.mark.asyncio
    async def test_report_telemetry_updates_last_seen(self, registry):
        dev = _make_device()
        await registry.register_device(dev)

        point = TelemetryPoint(device_id="ble:AA:BB:CC", name="temperature", value=23.5)
        await registry.report_telemetry(point)

        stored = registry.get_device("ble:AA:BB:CC")
        assert stored.status == DeviceStatus.ONLINE

    @pytest.mark.asyncio
    async def test_telemetry_callback_fires(self, registry):
        events = []
        registry.on_telemetry(lambda p: _async_append(events, p))

        dev = _make_device()
        await registry.register_device(dev)

        point = TelemetryPoint(device_id="ble:AA:BB:CC", name="temperature", value=23.5)
        await registry.report_telemetry(point)
        assert len(events) == 1
        assert events[0].name == "temperature"

    @pytest.mark.asyncio
    async def test_get_latest_telemetry(self, registry):
        dev = _make_device()
        await registry.register_device(dev)

        await registry.report_telemetry(
            TelemetryPoint(device_id="ble:AA:BB:CC", name="temperature", value=23.5)
        )
        await registry.report_telemetry(
            TelemetryPoint(device_id="ble:AA:BB:CC", name="humidity", value=55.0)
        )

        latest = registry.get_latest_telemetry("ble:AA:BB:CC")
        assert "temperature" in latest
        assert "humidity" in latest

    @pytest.mark.asyncio
    async def test_get_adapter_for_device(self, registry):
        dev = _make_device(protocol=Protocol.USB_SERIAL)
        dev.device_id = "serial:1234:5678:test"
        await registry.register_device(dev)

        assert registry.get_adapter_for_device("serial:1234:5678:test") == Protocol.USB_SERIAL
        assert registry.get_adapter_for_device("unknown") is None


class TestPersistence:
    @pytest.mark.asyncio
    async def test_persist_and_reload(self, tmp_path):
        config = RegistryConfig(
            heartbeat_timeout_s=10.0,
            offline_after_s=20.0,
            persist_path=str(tmp_path / "registry.json"),
        )
        reg = DeviceRegistry(config)

        dev = _make_device("ble:11:22:33")
        await reg.register_device(dev)
        await reg.persist()

        # Create new registry loading same file
        reg2 = DeviceRegistry(config)
        reg2._load_persisted()

        loaded = reg2.get_device("ble:11:22:33")
        assert loaded is not None
        assert loaded.protocol == Protocol.BLE
        # Loaded devices start as OFFLINE until re-discovered
        assert loaded.status == DeviceStatus.OFFLINE

    @pytest.mark.asyncio
    async def test_persist_creates_directory(self, tmp_path):
        path = str(tmp_path / "subdir" / "registry.json")
        config = RegistryConfig(
            heartbeat_timeout_s=10.0,
            offline_after_s=20.0,
            persist_path=path,
        )
        reg = DeviceRegistry(config)
        await reg.register_device(_make_device())
        await reg.persist()

        with open(path) as f:
            data = json.load(f)
        assert "ble:AA:BB:CC" in data

    @pytest.mark.asyncio
    async def test_load_missing_file_is_noop(self, tmp_path):
        config = RegistryConfig(
            heartbeat_timeout_s=10.0,
            offline_after_s=20.0,
            persist_path=str(tmp_path / "nonexistent.json"),
        )
        reg = DeviceRegistry(config)
        reg._load_persisted()
        assert len(reg.get_all_devices()) == 0


async def _async_append(lst, item):
    lst.append(item)
