"""Route OPC UA write-back commands to the correct protocol adapter."""

from __future__ import annotations

import logging
from typing import TYPE_CHECKING

from gateway.models import CommandRequest, CommandResponse, Protocol
from gateway.registry import DeviceRegistry

if TYPE_CHECKING:
    from gateway.adapters.base import DeviceAdapter

logger = logging.getLogger(__name__)


class CommandRouter:
    """Dispatches commands from the OPC UA server to the appropriate adapter."""

    def __init__(self, registry: DeviceRegistry) -> None:
        self._registry = registry
        self._adapters: dict[Protocol, DeviceAdapter] = {}

    def register_adapter(self, adapter: DeviceAdapter) -> None:
        self._adapters[adapter.protocol] = adapter

    async def dispatch(self, request: CommandRequest) -> CommandResponse:
        protocol = self._registry.get_adapter_for_device(request.device_id)
        if protocol is None:
            return CommandResponse(
                request_id=request.request_id,
                success=False,
                message=f"Unknown device: {request.device_id}",
            )

        adapter = self._adapters.get(protocol)
        if adapter is None:
            return CommandResponse(
                request_id=request.request_id,
                success=False,
                message=f"No adapter registered for protocol: {protocol.value}",
            )

        logger.info(
            "Routing command '%s' to %s via %s adapter",
            request.command,
            request.device_id,
            protocol.value,
        )
        return await adapter.execute_command(request)
