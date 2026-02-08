"""Tests for gateway.command_router — command dispatch."""

import pytest
from unittest.mock import AsyncMock

from gateway.command_router import CommandRouter
from gateway.config import RegistryConfig
from gateway.models import (
    CommandRequest,
    CommandResponse,
    DiscoveredDevice,
    Protocol,
)
from gateway.registry import DeviceRegistry


@pytest.fixture
def router_stack(tmp_path):
    reg_config = RegistryConfig(
        heartbeat_timeout_s=10.0,
        offline_after_s=20.0,
        persist_path=str(tmp_path / "reg.json"),
    )
    registry = DeviceRegistry(reg_config)
    router = CommandRouter(registry)
    return router, registry


class TestCommandRouter:
    @pytest.mark.asyncio
    async def test_dispatch_to_correct_adapter(self, router_stack):
        router, registry = router_stack

        # Register a BLE device
        dev = DiscoveredDevice(
            device_id="ble:AA:BB:CC",
            protocol=Protocol.BLE,
            protocol_address="AA:BB:CC",
        )
        await registry.register_device(dev)

        # Create mock adapter
        mock_adapter = AsyncMock()
        mock_adapter.protocol = Protocol.BLE
        mock_adapter.execute_command = AsyncMock(
            return_value=CommandResponse(request_id="r1", success=True, message="ok")
        )
        router.register_adapter(mock_adapter)

        req = CommandRequest(device_id="ble:AA:BB:CC", command="LED_ON", request_id="r1")
        resp = await router.dispatch(req)
        assert resp.success
        mock_adapter.execute_command.assert_called_once_with(req)

    @pytest.mark.asyncio
    async def test_dispatch_unknown_device(self, router_stack):
        router, _ = router_stack

        req = CommandRequest(device_id="ble:UNKNOWN", command="LED_ON")
        resp = await router.dispatch(req)
        assert not resp.success
        assert "Unknown device" in resp.message

    @pytest.mark.asyncio
    async def test_dispatch_no_adapter_for_protocol(self, router_stack):
        router, registry = router_stack

        dev = DiscoveredDevice(
            device_id="serial:test",
            protocol=Protocol.USB_SERIAL,
            protocol_address="/dev/ttyUSB0",
        )
        await registry.register_device(dev)

        # No adapter registered for USB_SERIAL
        req = CommandRequest(device_id="serial:test", command="LED_ON")
        resp = await router.dispatch(req)
        assert not resp.success
        assert "No adapter" in resp.message

    @pytest.mark.asyncio
    async def test_dispatch_routes_to_serial_adapter(self, router_stack):
        router, registry = router_stack

        dev = DiscoveredDevice(
            device_id="serial:test",
            protocol=Protocol.USB_SERIAL,
            protocol_address="/dev/ttyUSB0",
        )
        await registry.register_device(dev)

        mock_adapter = AsyncMock()
        mock_adapter.protocol = Protocol.USB_SERIAL
        mock_adapter.execute_command = AsyncMock(
            return_value=CommandResponse(request_id="r2", success=True, message="done")
        )
        router.register_adapter(mock_adapter)

        req = CommandRequest(device_id="serial:test", command="STATUS", request_id="r2")
        resp = await router.dispatch(req)
        assert resp.success
