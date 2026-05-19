"""USB serial adapter — discovers serial ports and polls telemetry.

Designed to consume the ``serial_demo.py`` peripheral which accepts
newline-terminated text commands and returns ACK/NACK responses.
"""

from __future__ import annotations

import asyncio
import glob
import logging
from typing import Any

import serial_asyncio  # type: ignore[import-untyped]
from serial.tools import list_ports  # type: ignore[import-untyped]

from gateway.adapters.base import DeviceAdapter
from gateway.command_router import CommandRouter
from gateway.config import SerialAdapterConfig
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


class SerialConnection:
    """Manages a single serial port with async line-based I/O."""

    def __init__(self, port: str, baud_rate: int) -> None:
        self.port = port
        self.baud_rate = baud_rate
        self._reader: asyncio.StreamReader | None = None
        self._writer: asyncio.StreamWriter | None = None
        self._lock = asyncio.Lock()

    async def open(self) -> None:
        self._reader, self._writer = await serial_asyncio.open_serial_connection(
            url=self.port, baudrate=self.baud_rate
        )

    async def close(self) -> None:
        if self._writer:
            self._writer.close()

    async def send_command(self, cmd: str, timeout: float = 3.0) -> str | None:
        """Send a command and wait for one response line."""
        if not self._writer or not self._reader:
            return None
        async with self._lock:
            self._writer.write(f"{cmd}\n".encode("ascii"))
            await self._writer.drain()
            try:
                line = await asyncio.wait_for(
                    self._reader.readline(), timeout=timeout
                )
                return line.decode("ascii", errors="ignore").strip()
            except asyncio.TimeoutError:
                return None


class UsbSerialAdapter(DeviceAdapter):
    def __init__(
        self,
        config: SerialAdapterConfig,
        registry: DeviceRegistry,
        router: CommandRouter,
    ) -> None:
        super().__init__(registry, router)
        self._config = config
        self._connections: dict[str, SerialConnection] = {}

    @property
    def protocol(self) -> Protocol:
        return Protocol.USB_SERIAL

    async def start(self) -> None:
        self._running = True
        self._create_task(self._discovery_loop(), name="serial-discovery")
        self._create_task(self._poll_loop(), name="serial-poll")

    async def stop(self) -> None:
        self._running = False
        await self._cancel_tasks()
        for conn in self._connections.values():
            await conn.close()
        self._connections.clear()

    # -- discovery -------------------------------------------------------

    async def _discovery_loop(self) -> None:
        while self._running:
            try:
                devices = await self.discover()
                for dev in devices:
                    await self._registry.register_device(dev)
            except asyncio.CancelledError:
                return
            except Exception as exc:
                logger.error("Serial discovery error: %s", exc)
            await asyncio.sleep(self._config.identify_timeout_s * 2)

    async def discover(self) -> list[DiscoveredDevice]:
        ports_found: set[str] = set()
        for pattern in self._config.port_patterns:
            ports_found.update(glob.glob(pattern))

        discovered: list[DiscoveredDevice] = []
        for port_path in sorted(ports_found):
            port_info = None
            for info in list_ports.comports():
                if info.device == port_path:
                    port_info = info
                    break

            vid = port_info.vid if port_info and port_info.vid else 0
            pid = port_info.pid if port_info and port_info.pid else 0
            serial_num = (
                port_info.serial_number
                if port_info and port_info.serial_number
                else ""
            )

            # Some Pi USB-serial adapters omit a stable serial number; fall back to the kernel
            # port path so the gateway still has a unique device id (port path is stable per
            # physical USB port + cable, which is acceptable for fixed-deployment demos).
            if serial_num:
                device_id = f"serial:{vid:04x}:{pid:04x}:{serial_num}"
            else:
                device_id = f"serial:{vid:04x}:{pid:04x}:{port_path}"

            if device_id not in self._connections:
                conn = SerialConnection(port_path, self._config.baud_rate)
                try:
                    await conn.open()
                    self._connections[device_id] = conn
                except Exception as exc:
                    logger.warning("Cannot open %s: %s", port_path, exc)
                    continue

            discovered.append(
                DiscoveredDevice(
                    device_id=device_id,
                    protocol=Protocol.USB_SERIAL,
                    protocol_address=port_path,
                    display_name=f"Serial {port_path}",
                    metadata={"vid": vid, "pid": pid, "serial": serial_num},
                )
            )
        return discovered

    # -- telemetry -------------------------------------------------------

    async def _poll_loop(self) -> None:
        while self._running:
            for device_id in list(self._connections):
                try:
                    points = await self.read_points(device_id)
                    for p in points:
                        await self._registry.report_telemetry(p)
                except asyncio.CancelledError:
                    return
                except Exception as exc:
                    logger.warning("Serial poll error for %s: %s", device_id, exc)
            await asyncio.sleep(self._config.poll_interval_s)

    async def read_points(self, device_id: str) -> list[TelemetryPoint]:
        conn = self._connections.get(device_id)
        if not conn:
            return []

        points: list[TelemetryPoint] = []

        # SENSOR_READ -> SENSOR:temp=23.4,hum=55.7
        response = await conn.send_command("SENSOR_READ")
        if response and response.startswith("SENSOR:"):
            points.extend(
                _parse_kv_response(
                    device_id, response[len("SENSOR:") :], {"temp": "temperature", "hum": "humidity"}
                )
            )

        # STATUS -> STATUS:uptime=1234,cpu_temp=45.2,mem_free=123456
        response = await conn.send_command("STATUS")
        if response and response.startswith("STATUS:"):
            points.extend(
                _parse_kv_response(device_id, response[len("STATUS:") :])
            )

        return points

    # -- commands --------------------------------------------------------

    async def execute_command(
        self, request: CommandRequest
    ) -> CommandResponse:
        conn = self._connections.get(request.device_id)
        if not conn:
            return CommandResponse(
                request_id=request.request_id,
                success=False,
                message="Device not connected",
            )

        # Build the command string
        if request.command == "GPIO_WRITE":
            pin = request.args.get("pin", "")
            value = request.args.get("value", "")
            cmd = f"GPIO_WRITE {pin} {value}"
        else:
            cmd = request.command

        # Allowlist check
        base_cmd = cmd.split()[0]
        if base_cmd not in self._config.write_allowlist:
            return CommandResponse(
                request_id=request.request_id,
                success=False,
                message=f"Command '{base_cmd}' not in write allowlist",
            )

        response = await conn.send_command(cmd)
        if response and response.startswith("ACK"):
            return CommandResponse(
                request_id=request.request_id,
                success=True,
                message=response,
            )
        return CommandResponse(
            request_id=request.request_id,
            success=False,
            message=response or "No response from device",
        )


def _parse_kv_response(
    device_id: str,
    payload: str,
    name_map: dict[str, str] | None = None,
) -> list[TelemetryPoint]:
    """Parse ``key=value,key=value`` into TelemetryPoints."""
    points: list[TelemetryPoint] = []
    for pair in payload.split(","):
        kv = pair.split("=", 1)
        if len(kv) != 2:
            continue
        try:
            val = float(kv[1])
        except ValueError:
            continue
        name = (name_map or {}).get(kv[0], kv[0])
        points.append(
            TelemetryPoint(
                device_id=device_id,
                name=name,
                value=val,
                datatype=OpcUaDataType.DOUBLE,
            )
        )
    return points
