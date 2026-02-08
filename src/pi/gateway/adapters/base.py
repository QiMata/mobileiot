"""Abstract base class for protocol-specific device adapters."""

from __future__ import annotations

import abc
import asyncio
import logging
from typing import TYPE_CHECKING

from gateway.models import (
    CommandRequest,
    CommandResponse,
    DiscoveredDevice,
    Protocol,
    TelemetryPoint,
)

if TYPE_CHECKING:
    from gateway.command_router import CommandRouter
    from gateway.registry import DeviceRegistry

logger = logging.getLogger(__name__)


class DeviceAdapter(abc.ABC):
    """Base class that all protocol adapters must implement."""

    def __init__(
        self, registry: DeviceRegistry, router: CommandRouter
    ) -> None:
        self._registry = registry
        self._router = router
        self._running = False
        self._tasks: list[asyncio.Task] = []  # type: ignore[type-arg]

    @property
    @abc.abstractmethod
    def protocol(self) -> Protocol:
        """The protocol this adapter handles."""
        ...

    @abc.abstractmethod
    async def start(self) -> None:
        """Start the adapter (begin discovery / listening)."""
        ...

    @abc.abstractmethod
    async def stop(self) -> None:
        """Stop the adapter and clean up resources."""
        ...

    @abc.abstractmethod
    async def discover(self) -> list[DiscoveredDevice]:
        """Perform one discovery cycle and return newly found devices."""
        ...

    @abc.abstractmethod
    async def read_points(self, device_id: str) -> list[TelemetryPoint]:
        """Read current telemetry from a specific device."""
        ...

    @abc.abstractmethod
    async def execute_command(
        self, request: CommandRequest
    ) -> CommandResponse:
        """Execute a write-back command on a device."""
        ...

    # -- task helpers ----------------------------------------------------

    def _create_task(self, coro, name: str = "") -> asyncio.Task:  # type: ignore[type-arg]
        task = asyncio.create_task(coro, name=name)
        self._tasks.append(task)
        task.add_done_callback(lambda t: self._tasks.remove(t) if t in self._tasks else None)
        return task

    async def _cancel_tasks(self) -> None:
        for task in list(self._tasks):
            task.cancel()
        await asyncio.gather(*self._tasks, return_exceptions=True)
        self._tasks.clear()
