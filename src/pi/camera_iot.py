"""Camera to Azure IoT Operations Edge Media Connector integration helpers.

This module provides a ready-made workflow that captures frames from a
camera and forwards them to the Azure IoT Operations Edge Media Connector
ingest endpoint.  The Media Connector is responsible for taking camera
frames that originate on the edge and routing them to downstream Azure
services defined by a media pipeline.  The capture routine itself remains
hardware-specific, so this module intentionally leaves a TODO stub that can
be replaced with code for the target camera.  Everything around that –
payload formatting, ingest client lifecycle, and capture cadence – is
implemented here so the final integration only needs to provide the
camera-specific capture routine.
"""

from __future__ import annotations

import asyncio
import base64
import logging
import os
from dataclasses import dataclass, field
from datetime import datetime, timezone
from typing import Awaitable, Callable, Dict, Optional, Protocol

LOGGER = logging.getLogger(__name__)

__all__ = ["MediaConnectorCamera", "build_image_payload"]


class MediaIngestClient(Protocol):
    """Protocol describing the minimal ingest client used by the uploader."""

    async def ingest(self, payload: Dict[str, object]) -> None:  # pragma: no cover - interface definition only
        ...

    async def close(self) -> None:  # pragma: no cover - interface definition only
        ...


ClientFactory = Callable[[str, Optional[str], str], Awaitable[MediaIngestClient]]


class _AioHttpMediaIngestClient:
    """Default implementation that posts payloads to the Media Connector ingest URL."""

    def __init__(self, session, ingest_url: str, api_key: Optional[str], api_key_header: str) -> None:
        self._session = session
        self._ingest_url = ingest_url
        headers = {"Content-Type": "application/json"}
        if api_key:
            headers[api_key_header] = api_key
        self._headers = headers

    async def ingest(self, payload: Dict[str, object]) -> None:
        async with self._session.post(self._ingest_url, json=payload, headers=self._headers) as response:
            if response.status >= 400:
                text = await response.text()
                raise RuntimeError(
                    f"Media Connector ingest failed with status {response.status}: {text.strip()}"
                )

    async def close(self) -> None:
        await self._session.close()


async def _default_client_factory(
    ingest_url: str, api_key: Optional[str], api_key_header: str
) -> MediaIngestClient:
    try:
        import aiohttp
    except ImportError as exc:  # pragma: no cover - executed only when dependency is missing.
        raise RuntimeError("The 'aiohttp' package is required to send frames to IoT Operations.") from exc

    session = aiohttp.ClientSession()
    return _AioHttpMediaIngestClient(session, ingest_url, api_key, api_key_header)


def build_image_payload(
    image_bytes: bytes,
    *,
    content_type: str,
    stream_id: str,
    metadata: Optional[Dict[str, object]] = None,
) -> Dict[str, object]:
    """Serialize a camera frame for transport to the Media Connector ingest endpoint."""

    if not isinstance(image_bytes, (bytes, bytearray)):
        raise TypeError("image_bytes must be raw bytes")

    encoded = base64.b64encode(image_bytes).decode("ascii")
    timestamp = datetime.now(timezone.utc).isoformat()

    payload: Dict[str, object] = {
        "streamId": stream_id,
        "contentType": content_type,
        "captureTimestamp": timestamp,
        "data": encoded,
    }
    if metadata:
        payload["metadata"] = metadata
    return payload


@dataclass
class MediaConnectorCamera:
    """Handle camera frame capture and uploading to Azure IoT Operations Edge."""

    ingest_url: str
    stream_id: str
    capture_interval: float = 30.0
    content_type: str = "image/jpeg"
    api_key: Optional[str] = None
    api_key_header: str = "Authorization"
    metadata: Optional[Dict[str, object]] = None
    client_factory: Optional[ClientFactory] = None
    _client: Optional[MediaIngestClient] = field(init=False, default=None)

    async def connect(self) -> None:
        """Create the ingest client used to upload frames."""

        factory = self.client_factory or _default_client_factory
        self._client = await factory(self.ingest_url, self.api_key, self.api_key_header)
        LOGGER.info("Prepared Media Connector ingest client targeting %s", self.ingest_url)

    async def disconnect(self) -> None:
        if self._client is None:
            return

        await self._client.close()
        LOGGER.info("Closed Media Connector ingest client")

    async def send_frame(self) -> None:
        if self._client is None:
            raise RuntimeError("Media Connector ingest client is not connected")

        frame_bytes = await asyncio.to_thread(self.capture_frame)
        payload = build_image_payload(
            frame_bytes,
            content_type=self.content_type,
            stream_id=self.stream_id,
            metadata=self.metadata,
        )
        await self._client.ingest(payload)
        LOGGER.info(
            "Uploaded camera frame to Azure IoT Operations Edge (%s bytes)",
            len(frame_bytes),
        )

    async def run(self, *, iterations: Optional[int] = None) -> None:
        """Continuously capture and send frames until cancelled.

        Args:
            iterations: Optional number of frames to send before exiting. ``None``
                (default) means run indefinitely.
        """

        count = 0
        while iterations is None or count < iterations:
            await self.send_frame()
            count += 1
            await asyncio.sleep(self.capture_interval)

    def capture_frame(self) -> bytes:
        """Capture a frame from the attached camera.

        This method intentionally raises ``NotImplementedError`` because the
        integration is hardware-specific.  Replace the body with code that
        captures an image from the camera you are using (for example via
        OpenCV, libcamera, or vendor SDK APIs).
        """

        raise NotImplementedError(
            "TODO: integrate with the target camera hardware to capture a frame"
        )


async def run_camera_upload_loop(*, capture_interval: float = 30.0) -> None:
    """Entry point used by CLI scripts.

    The Media Connector ingest configuration is read from the environment:

    * ``IOTOPS_MEDIA_CONNECTOR_INGEST_URL`` – the absolute URL for the ingest
      endpoint that receives frames.
    * ``IOTOPS_MEDIA_CONNECTOR_STREAM_ID`` – the logical stream identifier the
      payload should reference.
    * ``IOTOPS_MEDIA_CONNECTOR_API_KEY`` – optional API key to authorize with the
      Media Connector endpoint.
    * ``IOTOPS_MEDIA_CONNECTOR_API_KEY_HEADER`` – optional header name to use for
      the API key (defaults to ``Authorization``).
    """

    ingest_url = os.getenv("IOTOPS_MEDIA_CONNECTOR_INGEST_URL")
    stream_id = os.getenv("IOTOPS_MEDIA_CONNECTOR_STREAM_ID")
    api_key = os.getenv("IOTOPS_MEDIA_CONNECTOR_API_KEY")
    api_key_header = os.getenv("IOTOPS_MEDIA_CONNECTOR_API_KEY_HEADER", "Authorization")

    if not ingest_url:
        raise RuntimeError(
            "Set the IOTOPS_MEDIA_CONNECTOR_INGEST_URL environment variable before running the camera uploader"
        )

    if not stream_id:
        raise RuntimeError(
            "Set the IOTOPS_MEDIA_CONNECTOR_STREAM_ID environment variable before running the camera uploader"
        )

    device = MediaConnectorCamera(
        ingest_url=ingest_url,
        stream_id=stream_id,
        api_key=api_key,
        api_key_header=api_key_header,
        capture_interval=capture_interval,
    )

    await device.connect()
    try:
        await device.run()
    finally:
        await device.disconnect()


def main() -> None:
    """Blocking entry point for command line usage."""

    interval = float(os.getenv("CAMERA_CAPTURE_INTERVAL", "30"))
    asyncio.run(run_camera_upload_loop(capture_interval=interval))


if __name__ == "__main__":  # pragma: no cover - manual execution helper
    logging.basicConfig(level=logging.INFO)
    main()

