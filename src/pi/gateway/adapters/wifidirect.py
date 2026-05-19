"""WiFi Direct adapter — TCP client consuming the wifidirect_demo.py server.

Uses 4-byte big-endian length-prefixed UTF-8 JSON framing matching the
existing ``wifidirect_demo.py`` protocol.
"""

from __future__ import annotations

import asyncio
import json
import logging
import struct

from gateway.adapters.base import DeviceAdapter
from gateway.command_router import CommandRouter
from gateway.config import WifiDirectAdapterConfig
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


class WifiDirectFraming:
    """Encode / decode the 4-byte big-endian length-prefixed JSON protocol."""

    @staticmethod
    def encode(obj: dict) -> bytes:
        payload = json.dumps(obj).encode("utf-8")
        return struct.pack(">I", len(payload)) + payload

    @staticmethod
    async def decode(reader: asyncio.StreamReader) -> dict | None:
        header = await reader.readexactly(4)
        length = struct.unpack(">I", header)[0]
        # Sanity cap: reject oversized frames before allocation to avoid OOM on a Pi 3.
        # The framing protocol allows up to 4 GiB length-prefixed payloads in theory;
        # 10 MB is well above any legitimate JSON message we send today.
        if length > 10 * 1024 * 1024:  # 10 MB sanity limit
            raise ValueError(f"WiFi Direct frame too large: {length}")
        data = await reader.readexactly(length)
        return json.loads(data.decode("utf-8"))


class WifiDirectPeer:
    """A single TCP connection to a WiFi Direct demo server."""

    def __init__(
        self,
        device_id: str,
        reader: asyncio.StreamReader,
        writer: asyncio.StreamWriter,
    ) -> None:
        self.device_id = device_id
        self._reader = reader
        self._writer = writer
        self._lock = asyncio.Lock()

    async def send(self, msg: dict) -> None:
        async with self._lock:
            self._writer.write(WifiDirectFraming.encode(msg))
            await self._writer.drain()

    async def recv(self) -> dict | None:
        try:
            return await WifiDirectFraming.decode(self._reader)
        except (asyncio.IncompleteReadError, ConnectionError):
            return None

    async def close(self) -> None:
        self._writer.close()


class WifiDirectAdapter(DeviceAdapter):
    def __init__(
        self,
        config: WifiDirectAdapterConfig,
        registry: DeviceRegistry,
        router: CommandRouter,
    ) -> None:
        super().__init__(registry, router)
        self._config = config
        self._peers: dict[str, WifiDirectPeer] = {}

    @property
    def protocol(self) -> Protocol:
        return Protocol.WIFI_DIRECT

    async def start(self) -> None:
        self._running = True
        self._create_task(
            self._connect_loop(), name="wifidirect-connect"
        )

    async def stop(self) -> None:
        self._running = False
        await self._cancel_tasks()
        for peer in self._peers.values():
            await peer.close()
        self._peers.clear()

    # -- connection management -------------------------------------------

    async def _connect_loop(self) -> None:
        """Connect to the WiFi Direct demo TCP server as a client."""
        while self._running:
            try:
                host = self._config.listen_host
                port = self._config.listen_port
                reader, writer = await asyncio.open_connection(host, port)

                device_id = f"wifidirect:{host}:{port}"
                peer = WifiDirectPeer(device_id, reader, writer)
                self._peers[device_id] = peer

                await self._registry.register_device(
                    DiscoveredDevice(
                        device_id=device_id,
                        protocol=Protocol.WIFI_DIRECT,
                        protocol_address=f"{host}:{port}",
                        display_name="WiFi Direct Demo",
                    )
                )

                if self._config.telemetry_request_on_connect:
                    await peer.send({"type": "start_telemetry"})

                await self._receive_loop(peer)

            except asyncio.CancelledError:
                return
            except (ConnectionError, OSError) as exc:
                logger.warning(
                    "WiFi Direct connection failed: %s. Retrying in %.0fs.",
                    exc,
                    self._config.reconnect_delay_s,
                )

            await asyncio.sleep(self._config.reconnect_delay_s)

    async def _receive_loop(self, peer: WifiDirectPeer) -> None:
        """Process incoming messages from the WiFi Direct demo."""
        while self._running:
            msg = await peer.recv()
            if msg is None:
                logger.info(
                    "WiFi Direct peer disconnected: %s", peer.device_id
                )
                self._peers.pop(peer.device_id, None)
                break

            msg_type = msg.get("type", "")
            if msg_type == "telemetry":
                await self._handle_telemetry(peer.device_id, msg)
            else:
                logger.debug("WiFi Direct received type=%s", msg_type)

    async def _handle_telemetry(
        self, device_id: str, msg: dict
    ) -> None:
        """Parse telemetry from wifidirect_demo.py format."""
        field_map: dict[str, tuple[str, OpcUaDataType]] = {
            "temp": ("temperature", OpcUaDataType.DOUBLE),
            "hum": ("humidity", OpcUaDataType.DOUBLE),
            "cpu_temp": ("cpu_temperature", OpcUaDataType.DOUBLE),
            "uptime": ("uptime_seconds", OpcUaDataType.INT32),
        }
        for raw_name, (point_name, dtype) in field_map.items():
            if raw_name in msg:
                await self._registry.report_telemetry(
                    TelemetryPoint(
                        device_id=device_id,
                        name=point_name,
                        value=msg[raw_name],
                        datatype=dtype,
                    )
                )

    # -- adapter interface -----------------------------------------------

    async def discover(self) -> list[DiscoveredDevice]:
        return [
            DiscoveredDevice(
                device_id=did,
                protocol=Protocol.WIFI_DIRECT,
                protocol_address=peer.device_id,
                display_name="WiFi Direct Peer",
            )
            for did, peer in self._peers.items()
        ]

    async def read_points(self, device_id: str) -> list[TelemetryPoint]:
        # WiFi Direct uses push-based telemetry; return latest from registry
        return list(self._registry.get_latest_telemetry(device_id).values())

    async def execute_command(
        self, request: CommandRequest
    ) -> CommandResponse:
        peer = self._peers.get(request.device_id)
        if not peer:
            return CommandResponse(
                request_id=request.request_id,
                success=False,
                message="Peer not connected",
            )
        try:
            await peer.send(
                {
                    "type": "command",
                    "command": request.command,
                    "args": request.args,
                }
            )
            return CommandResponse(
                request_id=request.request_id,
                success=True,
                message="Command sent",
            )
        except Exception as exc:
            return CommandResponse(
                request_id=request.request_id,
                success=False,
                message=str(exc),
            )
