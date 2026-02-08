"""Device registry — canonical state for all discovered devices."""

from __future__ import annotations

import asyncio
import json
import logging
import os
from datetime import datetime, timezone
from typing import Any, Awaitable, Callable

from gateway.models import (
    DeviceStatus,
    DiscoveredDevice,
    Protocol,
    TelemetryPoint,
)
from gateway.config import RegistryConfig

logger = logging.getLogger(__name__)

OnDeviceCallback = Callable[[DiscoveredDevice], Awaitable[None]]
OnTelemetryCallback = Callable[[TelemetryPoint], Awaitable[None]]


class DeviceRegistry:
    """Maintains the canonical state of all discovered devices.

    Responsibilities:
    - Merge protocol-specific discovery events into a unified identity
    - Track online / stale / offline status via heartbeat timeout
    - Persist state to JSON for restart resilience
    - Emit callbacks when devices come online, go offline, or report telemetry
    """

    def __init__(self, config: RegistryConfig) -> None:
        self._config = config
        self._devices: dict[str, DiscoveredDevice] = {}
        self._telemetry: dict[str, dict[str, TelemetryPoint]] = {}
        self._on_device_online: list[OnDeviceCallback] = []
        self._on_device_offline: list[OnDeviceCallback] = []
        self._on_telemetry: list[OnTelemetryCallback] = []
        self._heartbeat_task: asyncio.Task[None] | None = None

    # -- lifecycle --------------------------------------------------------

    async def start(self) -> None:
        """Load persisted state and start the heartbeat checker."""
        self._load_persisted()
        self._heartbeat_task = asyncio.create_task(
            self._heartbeat_loop(), name="registry-heartbeat"
        )

    async def stop(self) -> None:
        if self._heartbeat_task:
            self._heartbeat_task.cancel()
            try:
                await self._heartbeat_task
            except asyncio.CancelledError:
                pass
        await self.persist()

    # -- callback registration -------------------------------------------

    def on_device_online(self, callback: OnDeviceCallback) -> None:
        self._on_device_online.append(callback)

    def on_device_offline(self, callback: OnDeviceCallback) -> None:
        self._on_device_offline.append(callback)

    def on_telemetry(self, callback: OnTelemetryCallback) -> None:
        self._on_telemetry.append(callback)

    # -- device management -----------------------------------------------

    async def register_device(self, device: DiscoveredDevice) -> None:
        """Register or update a discovered device."""
        existing = self._devices.get(device.device_id)
        was_offline = existing is None or existing.status == DeviceStatus.OFFLINE

        device.status = DeviceStatus.ONLINE
        device.last_seen = datetime.now(timezone.utc)
        self._devices[device.device_id] = device

        if was_offline:
            logger.info(
                "Device online: %s (%s via %s)",
                device.device_id,
                device.display_name,
                device.protocol.value,
            )
            for cb in self._on_device_online:
                await cb(device)

    async def report_telemetry(self, point: TelemetryPoint) -> None:
        """Report a telemetry data point for a device."""
        if point.device_id in self._devices:
            self._devices[point.device_id].last_seen = datetime.now(timezone.utc)
            self._devices[point.device_id].status = DeviceStatus.ONLINE

        self._telemetry.setdefault(point.device_id, {})[point.name] = point

        for cb in self._on_telemetry:
            await cb(point)

    # -- queries ---------------------------------------------------------

    def get_device(self, device_id: str) -> DiscoveredDevice | None:
        return self._devices.get(device_id)

    def get_all_devices(self) -> list[DiscoveredDevice]:
        return list(self._devices.values())

    def get_latest_telemetry(
        self, device_id: str
    ) -> dict[str, TelemetryPoint]:
        return dict(self._telemetry.get(device_id, {}))

    def get_adapter_for_device(self, device_id: str) -> Protocol | None:
        dev = self._devices.get(device_id)
        return dev.protocol if dev else None

    # -- heartbeat -------------------------------------------------------

    async def _heartbeat_loop(self) -> None:
        """Periodically check for stale / offline devices."""
        while True:
            await asyncio.sleep(self._config.heartbeat_timeout_s / 2)
            now = datetime.now(timezone.utc)
            for dev in list(self._devices.values()):
                if dev.status == DeviceStatus.OFFLINE:
                    continue
                elapsed = (now - dev.last_seen).total_seconds()
                if elapsed > self._config.offline_after_s:
                    dev.status = DeviceStatus.OFFLINE
                    logger.warning(
                        "Device offline: %s (last seen %.0fs ago)",
                        dev.device_id,
                        elapsed,
                    )
                    for cb in self._on_device_offline:
                        await cb(dev)
                elif elapsed > self._config.heartbeat_timeout_s:
                    if dev.status != DeviceStatus.STALE:
                        dev.status = DeviceStatus.STALE
                        logger.info(
                            "Device stale: %s (last seen %.0fs ago)",
                            dev.device_id,
                            elapsed,
                        )

    # -- persistence -----------------------------------------------------

    async def persist(self) -> None:
        """Save registry state to disk."""
        try:
            os.makedirs(os.path.dirname(self._config.persist_path), exist_ok=True)
            data = {
                did: _serialize_device(d)
                for did, d in self._devices.items()
            }
            with open(self._config.persist_path, "w") as f:
                json.dump(data, f, indent=2)
        except OSError as exc:
            logger.error("Failed to persist registry: %s", exc)

    def _load_persisted(self) -> None:
        """Load previously persisted device state."""
        if not os.path.isfile(self._config.persist_path):
            return
        try:
            with open(self._config.persist_path) as f:
                data: dict[str, Any] = json.load(f)
            for did, raw in data.items():
                self._devices[did] = DiscoveredDevice(
                    device_id=raw["device_id"],
                    protocol=Protocol(raw["protocol"]),
                    protocol_address=raw["protocol_address"],
                    display_name=raw.get("display_name", ""),
                    metadata=raw.get("metadata", {}),
                    last_seen=datetime.fromisoformat(raw["last_seen"]),
                    status=DeviceStatus.OFFLINE,  # start as offline until re-discovered
                )
            logger.info("Loaded %d devices from persisted state", len(self._devices))
        except (OSError, json.JSONDecodeError, KeyError) as exc:
            logger.warning("Could not load persisted registry: %s", exc)


def _serialize_device(d: DiscoveredDevice) -> dict[str, Any]:
    return {
        "device_id": d.device_id,
        "protocol": d.protocol.value,
        "protocol_address": d.protocol_address,
        "display_name": d.display_name,
        "metadata": d.metadata,
        "last_seen": d.last_seen.isoformat(),
    }
