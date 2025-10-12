"""Tests for the camera to Azure IoT Operations integration helpers."""

from __future__ import annotations

import base64
import asyncio

import camera_iot


class _DummyClient:
    def __init__(self) -> None:
        self.closed = False
        self.payloads: list[dict[str, object]] = []

    async def ingest(self, payload: dict[str, object]) -> None:
        self.payloads.append(payload)

    async def close(self) -> None:
        self.closed = True


def test_build_image_payload_serializes_bytes() -> None:
    payload = camera_iot.build_image_payload(
        b"\x01\x02",
        content_type="image/jpeg",
        stream_id="line-1",
        metadata={"site": "factory"},
    )

    assert payload["streamId"] == "line-1"
    assert payload["contentType"] == "image/jpeg"
    assert payload["metadata"] == {"site": "factory"}
    assert "captureTimestamp" in payload

    data = base64.b64decode(payload["data"])
    assert data == b"\x01\x02"


def test_send_frame_uses_provided_factories() -> None:
    asyncio.run(_run_send_frame_test())


async def _run_send_frame_test() -> None:
    dummy_client = _DummyClient()

    async def client_factory(ingest_url: str, api_key: str | None, api_key_header: str):
        assert ingest_url == "https://example.test/ingest"
        assert api_key == "token"
        assert api_key_header == "X-API-KEY"
        return dummy_client

    device = camera_iot.MediaConnectorCamera(
        ingest_url="https://example.test/ingest",
        stream_id="line-1",
        capture_interval=0.01,
        client_factory=client_factory,
        api_key="token",
        api_key_header="X-API-KEY",
        metadata={"site": "factory"},
    )

    device.capture_frame = lambda: b"test-bytes"  # type: ignore[assignment]

    await device.connect()
    await device.send_frame()
    await device.disconnect()

    assert dummy_client.closed is True
    assert len(dummy_client.payloads) == 1

    message_payload = dummy_client.payloads[0]
    assert message_payload["contentType"] == "image/jpeg"
    assert message_payload["streamId"] == "line-1"
    assert message_payload["metadata"] == {"site": "factory"}
    assert base64.b64decode(message_payload["data"]) == b"test-bytes"

