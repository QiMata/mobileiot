"""HTTP health and device-list endpoints."""

from __future__ import annotations

import logging
from typing import TYPE_CHECKING

from aiohttp import web

from gateway.config import HealthConfig
from gateway.registry import DeviceRegistry

if TYPE_CHECKING:
    from gateway.opcua_server import GatewayOpcUaServer

logger = logging.getLogger(__name__)


class HealthServer:
    def __init__(
        self,
        config: HealthConfig,
        registry: DeviceRegistry,
        opcua_server: GatewayOpcUaServer,
    ) -> None:
        self._config = config
        self._registry = registry
        self._opcua = opcua_server
        self._runner: web.AppRunner | None = None

    async def start(self) -> None:
        app = web.Application()
        app.router.add_get("/healthz", self._handle_health)
        app.router.add_get("/devices", self._handle_devices)
        self._runner = web.AppRunner(app)
        await self._runner.setup()
        site = web.TCPSite(self._runner, self._config.host, self._config.port)
        await site.start()
        logger.info(
            "Health server listening on %s:%d", self._config.host, self._config.port
        )

    async def stop(self) -> None:
        if self._runner:
            await self._runner.cleanup()

    async def _handle_health(self, _request: web.Request) -> web.Response:
        devices = self._registry.get_all_devices()
        online = sum(1 for d in devices if d.status.value == "online")
        return web.json_response(
            {
                "status": "ok",
                "opcua_running": self._opcua.is_running,
                "devices_total": len(devices),
                "devices_online": online,
            }
        )

    async def _handle_devices(self, _request: web.Request) -> web.Response:
        devices = self._registry.get_all_devices()
        return web.json_response(
            [
                {
                    "device_id": d.device_id,
                    "protocol": d.protocol.value,
                    "status": d.status.value,
                    "display_name": d.display_name,
                    "last_seen": d.last_seen.isoformat(),
                }
                for d in devices
            ]
        )
