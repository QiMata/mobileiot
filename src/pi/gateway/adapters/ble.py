"""BLE central adapter — scans, connects, and reads from BLE peripherals.

Designed to consume the ``bluetoothle_demo.py`` peripheral which advertises as
"PiSensor" with service UUID ``12345678-1234-1234-1234-1234567890AB``.
"""

from __future__ import annotations

import asyncio
import logging
import struct

from bleak import BleakClient, BleakScanner, BLEDevice
from bleak.exc import BleakError

from gateway.adapters.base import DeviceAdapter
from gateway.command_router import CommandRouter
from gateway.config import BleAdapterConfig
from gateway.models import (
    CommandRequest,
    CommandResponse,
    DiscoveredDevice,
    OpcUaDataType,
    Protocol,
    TelemetryPoint,
)
from gateway.registry import DeviceRegistry

logger = logging.getLogger(__name__)

# UUIDs matching bluetoothle_demo.py
SERVICE_UUID = "12345678-1234-1234-1234-1234567890ab"
TEMP_CHAR_UUID = "00002a6e-0000-1000-8000-00805f9b34fb"
HUM_CHAR_UUID = "00002a6f-0000-1000-8000-00805f9b34fb"
LED_CHAR_UUID = "12345679-1234-1234-1234-1234567890ab"


class BleAdapter(DeviceAdapter):
    def __init__(
        self,
        config: BleAdapterConfig,
        registry: DeviceRegistry,
        router: CommandRouter,
    ) -> None:
        super().__init__(registry, router)
        self._config = config
        self._clients: dict[str, BleakClient] = {}
        self._ble_devices: dict[str, BLEDevice] = {}

    @property
    def protocol(self) -> Protocol:
        return Protocol.BLE

    async def start(self) -> None:
        self._running = True
        self._create_task(self._scan_loop(), name="ble-scan-loop")
        self._create_task(self._read_loop(), name="ble-read-loop")

    async def stop(self) -> None:
        self._running = False
        await self._cancel_tasks()
        for client in list(self._clients.values()):
            try:
                await client.disconnect()
            except BleakError:
                pass
        self._clients.clear()

    # -- discovery -------------------------------------------------------

    async def _scan_loop(self) -> None:
        while self._running:
            try:
                devices = await self.discover()
                for dev in devices:
                    await self._registry.register_device(dev)
                    await self._ensure_connected(dev.device_id)
            except asyncio.CancelledError:
                return
            except Exception as exc:
                logger.error("BLE scan error: %s", exc)
            await asyncio.sleep(self._config.scan_interval_s)

    async def discover(self) -> list[DiscoveredDevice]:
        service_uuids = (
            [u.lower() for u in self._config.service_filter]
            if self._config.service_filter
            else None
        )
        devices = await BleakScanner.discover(
            timeout=self._config.scan_duration_s,
            service_uuids=service_uuids,
        )
        discovered: list[DiscoveredDevice] = []
        for d in devices:
            if self._config.name_filter and d.name not in self._config.name_filter:
                continue
            device_id = f"ble:{d.address}"
            self._ble_devices[device_id] = d
            discovered.append(
                DiscoveredDevice(
                    device_id=device_id,
                    protocol=Protocol.BLE,
                    protocol_address=d.address,
                    display_name=d.name or d.address,
                    metadata={"rssi": d.rssi},
                )
            )
        return discovered

    # -- connection management -------------------------------------------

    async def _ensure_connected(self, device_id: str) -> None:
        if device_id in self._clients and self._clients[device_id].is_connected:
            return
        ble_device = self._ble_devices.get(device_id)
        if not ble_device:
            return
        try:
            client = BleakClient(
                ble_device,
                disconnected_callback=lambda _c: self._on_disconnect(device_id),
            )
            await asyncio.wait_for(client.connect(), timeout=10.0)
            self._clients[device_id] = client
            logger.info(
                "BLE connected to %s (%s)", device_id, ble_device.name
            )
        except (BleakError, asyncio.TimeoutError) as exc:
            logger.warning("BLE connect failed for %s: %s", device_id, exc)

    def _on_disconnect(self, device_id: str) -> None:
        logger.info("BLE disconnected: %s", device_id)
        self._clients.pop(device_id, None)

    # -- telemetry -------------------------------------------------------

    async def _read_loop(self) -> None:
        while self._running:
            for device_id, client in list(self._clients.items()):
                if not client.is_connected:
                    continue
                try:
                    points = await self.read_points(device_id)
                    for p in points:
                        await self._registry.report_telemetry(p)
                except BleakError as exc:
                    logger.warning("BLE read error for %s: %s", device_id, exc)
                except asyncio.CancelledError:
                    return
            await asyncio.sleep(self._config.read_interval_s)

    async def read_points(self, device_id: str) -> list[TelemetryPoint]:
        client = self._clients.get(device_id)
        if not client or not client.is_connected:
            return []

        points: list[TelemetryPoint] = []

        # Temperature: Int16 little-endian, value / 100.
        # Scale factor (× 100) matches the firmware encoder in src/pi/bluetoothle_demo.py
        # — keep both sides in sync if you change precision.
        try:
            raw = await client.read_gatt_char(TEMP_CHAR_UUID)
            temp_int = struct.unpack("<h", raw)[0]
            points.append(
                TelemetryPoint(
                    device_id=device_id,
                    name="temperature",
                    value=temp_int / 100.0,
                    datatype=OpcUaDataType.DOUBLE,
                )
            )
        except BleakError as exc:
            logger.debug("Could not read temp from %s: %s", device_id, exc)

        # Humidity: Int16 little-endian, value / 100.
        # Scale factor (× 100) matches the firmware encoder in src/pi/bluetoothle_demo.py.
        try:
            raw = await client.read_gatt_char(HUM_CHAR_UUID)
            hum_int = struct.unpack("<h", raw)[0]
            points.append(
                TelemetryPoint(
                    device_id=device_id,
                    name="humidity",
                    value=hum_int / 100.0,
                    datatype=OpcUaDataType.DOUBLE,
                )
            )
        except BleakError as exc:
            logger.debug("Could not read hum from %s: %s", device_id, exc)

        return points

    # -- commands --------------------------------------------------------

    async def execute_command(
        self, request: CommandRequest
    ) -> CommandResponse:
        client = self._clients.get(request.device_id)
        if not client or not client.is_connected:
            return CommandResponse(
                request_id=request.request_id,
                success=False,
                message="Device not connected",
            )

        if request.command not in ("LED_ON", "LED_OFF"):
            return CommandResponse(
                request_id=request.request_id,
                success=False,
                message=f"Command not in write allowlist: {request.command}",
            )

        try:
            value = bytes([0x01]) if request.command == "LED_ON" else bytes([0x00])
            await client.write_gatt_char(LED_CHAR_UUID, value)
            return CommandResponse(
                request_id=request.request_id,
                success=True,
                message=f"BLE {request.command} executed",
            )
        except BleakError as exc:
            return CommandResponse(
                request_id=request.request_id,
                success=False,
                message=str(exc),
            )
