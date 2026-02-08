"""Async orchestrator — starts all gateway components and handles shutdown."""

from __future__ import annotations

import asyncio
import logging
import signal
import sys

from gateway.config import load_config
from gateway.registry import DeviceRegistry
from gateway.opcua_server import GatewayOpcUaServer
from gateway.command_router import CommandRouter
from gateway.health import HealthServer
from gateway.adapters.ble import BleAdapter
from gateway.adapters.usb_serial import UsbSerialAdapter
from gateway.adapters.wifidirect import WifiDirectAdapter

logger = logging.getLogger("gateway")


async def run(config_path: str | None = None) -> None:
    config = load_config(config_path)
    logging.basicConfig(
        level=getattr(logging, config.log_level.upper(), logging.INFO),
        format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    )

    registry = DeviceRegistry(config.registry)
    await registry.start()

    opcua = GatewayOpcUaServer(config.opcua, registry)
    router = CommandRouter(registry)
    health = HealthServer(config.health, registry, opcua)

    adapters = []
    if config.ble.enabled:
        adapters.append(BleAdapter(config.ble, registry, router))
    if config.serial.enabled:
        adapters.append(UsbSerialAdapter(config.serial, registry, router))
    if config.wifi_direct.enabled:
        adapters.append(WifiDirectAdapter(config.wifi_direct, registry, router))

    for adapter in adapters:
        router.register_adapter(adapter)

    await opcua.start()

    if config.health.enabled:
        await health.start()

    for adapter in adapters:
        await adapter.start()

    # Graceful shutdown on SIGTERM / SIGINT
    stop_event = asyncio.Event()

    def _signal_handler() -> None:
        logger.info("Shutdown signal received")
        stop_event.set()

    loop = asyncio.get_running_loop()
    if sys.platform != "win32":
        for sig in (signal.SIGTERM, signal.SIGINT):
            loop.add_signal_handler(sig, _signal_handler)
    else:
        # Windows: SIGINT handled by KeyboardInterrupt
        pass

    logger.info("Gateway running. Press Ctrl+C to stop.")
    try:
        await stop_event.wait()
    except (KeyboardInterrupt, asyncio.CancelledError):
        pass

    # Shutdown in reverse order
    logger.info("Shutting down gateway...")
    for adapter in reversed(adapters):
        await adapter.stop()
    if config.health.enabled:
        await health.stop()
    await opcua.stop()
    await registry.stop()
    logger.info("Gateway stopped.")
