"""Tests for gateway.adapters.wifidirect — WiFi Direct TCP adapter."""

import asyncio
import json
import struct

import pytest

from gateway.adapters.wifidirect import WifiDirectAdapter, WifiDirectFraming
from gateway.command_router import CommandRouter
from gateway.config import RegistryConfig, WifiDirectAdapterConfig
from gateway.models import CommandRequest, Protocol, TelemetryPoint
from gateway.registry import DeviceRegistry


class TestWifiDirectFraming:
    def test_encode_produces_length_prefix(self):
        msg = {"type": "start_telemetry"}
        frame = WifiDirectFraming.encode(msg)
        length = struct.unpack(">I", frame[:4])[0]
        payload = json.loads(frame[4:].decode("utf-8"))
        assert payload["type"] == "start_telemetry"
        assert length == len(frame) - 4

    @pytest.mark.asyncio
    async def test_decode_reads_framed_message(self):
        msg = {"type": "telemetry", "temp": 23.4, "hum": 55.7}
        frame = WifiDirectFraming.encode(msg)
        reader = asyncio.StreamReader()
        reader.feed_data(frame)
        result = await WifiDirectFraming.decode(reader)
        assert result["type"] == "telemetry"
        assert result["temp"] == 23.4
        assert result["hum"] == 55.7

    def test_encode_decode_roundtrip(self):
        original = {"type": "file_list", "path": "/home/pi/data"}
        frame = WifiDirectFraming.encode(original)
        length = struct.unpack(">I", frame[:4])[0]
        payload = json.loads(frame[4 : 4 + length].decode("utf-8"))
        assert payload == original

    @pytest.mark.asyncio
    async def test_decode_oversized_raises(self):
        # Craft a frame claiming to be 20MB
        header = struct.pack(">I", 20 * 1024 * 1024)
        reader = asyncio.StreamReader()
        reader.feed_data(header)
        with pytest.raises(ValueError, match="too large"):
            await WifiDirectFraming.decode(reader)


class TestWifiDirectIntegration:
    @pytest.mark.asyncio
    async def test_adapter_receives_telemetry_from_mock_server(self, tmp_path):
        """Spin up a mock TCP server and verify the adapter ingests telemetry."""
        reg = DeviceRegistry(
            RegistryConfig(
                heartbeat_timeout_s=10.0,
                offline_after_s=20.0,
                persist_path=str(tmp_path / "r.json"),
            )
        )
        router = CommandRouter(reg)

        telemetry_received = asyncio.Event()
        received_points: list[TelemetryPoint] = []

        original_report = reg.report_telemetry

        async def track_telemetry(point):
            await original_report(point)
            received_points.append(point)
            if len(received_points) >= 4:  # temp, hum, cpu_temp, uptime
                telemetry_received.set()

        reg.report_telemetry = track_telemetry  # type: ignore[assignment]

        async def mock_server_handler(reader, writer):
            # Wait for client to send start_telemetry
            header = await reader.readexactly(4)
            length = struct.unpack(">I", header)[0]
            await reader.readexactly(length)  # consume the request

            # Send one telemetry frame
            msg = {
                "type": "telemetry",
                "ts": 1700000000,
                "temp": 25.0,
                "hum": 60.0,
                "cpu_temp": 40.0,
                "uptime": 500,
            }
            writer.write(WifiDirectFraming.encode(msg))
            await writer.drain()
            # Keep connection open briefly
            await asyncio.sleep(2)
            writer.close()

        server = await asyncio.start_server(mock_server_handler, "127.0.0.1", 0)
        port = server.sockets[0].getsockname()[1]

        config = WifiDirectAdapterConfig(
            listen_host="127.0.0.1",
            listen_port=port,
            telemetry_request_on_connect=True,
            reconnect_delay_s=1.0,
        )
        adapter = WifiDirectAdapter(config, reg, router)
        await adapter.start()

        try:
            await asyncio.wait_for(telemetry_received.wait(), timeout=5.0)
        finally:
            await adapter.stop()
            server.close()
            await server.wait_closed()

        names = {p.name for p in received_points}
        assert "temperature" in names
        assert "humidity" in names
        assert "cpu_temperature" in names
        assert "uptime_seconds" in names

    @pytest.mark.asyncio
    async def test_adapter_handles_connection_refused(self, tmp_path):
        """Adapter should not crash when server is unavailable."""
        reg = DeviceRegistry(
            RegistryConfig(
                heartbeat_timeout_s=10.0,
                offline_after_s=20.0,
                persist_path=str(tmp_path / "r.json"),
            )
        )
        router = CommandRouter(reg)
        config = WifiDirectAdapterConfig(
            listen_host="127.0.0.1",
            listen_port=19999,  # nothing listening here
            reconnect_delay_s=0.5,
        )
        adapter = WifiDirectAdapter(config, reg, router)
        await adapter.start()
        await asyncio.sleep(1.0)  # let it attempt a connection
        await adapter.stop()  # should not raise

    @pytest.mark.asyncio
    async def test_execute_command_no_peer(self, tmp_path):
        reg = DeviceRegistry(
            RegistryConfig(
                heartbeat_timeout_s=10.0,
                offline_after_s=20.0,
                persist_path=str(tmp_path / "r.json"),
            )
        )
        router = CommandRouter(reg)
        config = WifiDirectAdapterConfig()
        adapter = WifiDirectAdapter(config, reg, router)

        req = CommandRequest(device_id="wifidirect:missing", command="LED_ON")
        resp = await adapter.execute_command(req)
        assert not resp.success
        assert "not connected" in resp.message.lower()
