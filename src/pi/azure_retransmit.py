"""Helpers for relaying Bluetooth sensor data to Azure IoT services."""

from __future__ import annotations

import json
import logging
import queue
import threading
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Callable, Dict, Optional, Protocol, Sequence

import urllib.error
import urllib.request

LOGGER = logging.getLogger(__name__)


class TelemetryPublisher(Protocol):
    """Protocol describing an object that can publish telemetry payloads."""

    def publish(self, payload: Dict[str, object]) -> None:  # pragma: no cover - structural typing hook
        ...

    def close(self) -> None:  # pragma: no cover - optional hook
        ...


@dataclass
class _IoTHubClientHandle:
    """Minimal wrapper for the Azure IoT Hub device client."""

    send: Callable[[Dict[str, object]], None]
    close: Callable[[], None]


class AzureIoTHubPublisher:
    """Publish telemetry payloads to Azure IoT Hub."""

    def __init__(
        self,
        connection_string: str,
        *,
        client_factory: Optional[Callable[[str], _IoTHubClientHandle]] = None,
    ) -> None:
        self._connection_string = connection_string
        self._client_factory = client_factory or self._default_client_factory
        self._handle: Optional[_IoTHubClientHandle] = None
        self._lock = threading.Lock()

    def publish(self, payload: Dict[str, object]) -> None:
        handle = self._ensure_handle()
        handle.send(payload)

    def close(self) -> None:
        with self._lock:
            if self._handle is None:
                return
            try:
                self._handle.close()
            finally:
                self._handle = None

    def _ensure_handle(self) -> _IoTHubClientHandle:
        with self._lock:
            if self._handle is None:
                self._handle = self._client_factory(self._connection_string)
            return self._handle

    @staticmethod
    def _default_client_factory(connection_string: str) -> _IoTHubClientHandle:
        try:
            from azure.iot.device import IoTHubDeviceClient, Message
        except ImportError as exc:  # pragma: no cover - executed only when dependency is missing.
            raise RuntimeError(
                "The 'azure-iot-device' package is required to send telemetry to Azure IoT Hub."
            ) from exc

        client = IoTHubDeviceClient.create_from_connection_string(connection_string)
        client.connect()

        def _send(payload: Dict[str, object]) -> None:
            message = Message(json.dumps(payload))
            client.send_message(message)

        def _close() -> None:
            try:
                client.disconnect()
            except Exception:  # pragma: no cover - defensive cleanup
                LOGGER.exception("Failed to disconnect IoT Hub client cleanly")

        return _IoTHubClientHandle(send=_send, close=_close)


class IoTOpsEdgePublisher:
    """Publish telemetry payloads to Azure IoT Operations Edge ingest endpoints."""

    def __init__(
        self,
        ingest_url: str,
        *,
        api_key: Optional[str] = None,
        api_key_header: str = "Authorization",
        opener: Optional[urllib.request.OpenerDirector] = None,
        timeout: float = 10.0,
    ) -> None:
        self._ingest_url = ingest_url
        self._timeout = timeout
        self._opener = opener or urllib.request.build_opener()
        headers = {"Content-Type": "application/json"}
        if api_key:
            headers[api_key_header] = api_key
        self._headers = headers

    def publish(self, payload: Dict[str, object]) -> None:
        data = json.dumps(payload).encode("utf-8")
        request = urllib.request.Request(
            self._ingest_url,
            data=data,
            headers=self._headers,
            method="POST",
        )

        try:
            with self._opener.open(request, timeout=self._timeout) as response:
                status = getattr(response, "status", None)
                if status is None:
                    status = response.getcode()
                if status >= 400:
                    body = response.read().decode("utf-8", "ignore")
                    raise RuntimeError(
                        f"Azure IoT Operations Edge ingest failed with status {status}: {body.strip()}"
                    )
        except urllib.error.HTTPError as exc:
            body = exc.read().decode("utf-8", "ignore")
            raise RuntimeError(
                f"Azure IoT Operations Edge ingest failed with status {exc.code}: {body.strip()}"
            ) from exc

    def close(self) -> None:
        # urllib openers do not require explicit teardown but the hook is provided for parity.
        return None


class BackgroundTelemetryRelay:
    """Queue telemetry payloads and deliver them to the configured publishers."""

    def __init__(
        self,
        publishers: Sequence[TelemetryPublisher],
        *,
        max_queue_size: int = 100,
    ) -> None:
        if not publishers:
            raise ValueError("At least one telemetry publisher must be provided")

        self._publishers: tuple[TelemetryPublisher, ...] = tuple(publishers)
        self._queue: "queue.Queue[Optional[Dict[str, object]]]" = queue.Queue(max_queue_size)
        self._stop_event = threading.Event()
        self._thread = threading.Thread(target=self._worker, name="TelemetryRelay", daemon=True)
        self._thread.start()

    def __enter__(self) -> "BackgroundTelemetryRelay":
        return self

    def __exit__(self, exc_type, exc, tb) -> None:
        self.close()

    def emit(self, payload: Dict[str, object]) -> None:
        if self._stop_event.is_set():
            raise RuntimeError("Telemetry relay has been closed")

        try:
            self._queue.put_nowait(payload)
        except queue.Full:
            LOGGER.warning("Telemetry relay queue is full; dropping payload")

    def close(self) -> None:
        if self._stop_event.is_set():
            return

        self._stop_event.set()
        self._queue.put(None)
        self._thread.join(timeout=5.0)

        # Flush remaining messages synchronously to avoid data loss.
        while True:
            try:
                payload = self._queue.get_nowait()
            except queue.Empty:
                break

            if payload is None:
                continue
            self._dispatch(payload)

        for publisher in self._publishers:
            close = getattr(publisher, "close", None)
            if callable(close):
                try:
                    close()
                except Exception:  # pragma: no cover - defensive cleanup
                    LOGGER.exception("Telemetry publisher close() raised an exception")

    def _worker(self) -> None:
        while True:
            payload = self._queue.get()
            if payload is None:
                break
            self._dispatch(payload)

    def _dispatch(self, payload: Dict[str, object]) -> None:
        for publisher in self._publishers:
            try:
                publisher.publish(payload)
            except Exception:  # pragma: no cover - ensure telemetry path never crashes the worker
                LOGGER.exception("Telemetry publisher raised an exception")


def build_sensor_payload(
    measurement_type: str,
    value: float,
    unit: str,
    *,
    source: str,
    timestamp: Optional[datetime] = None,
    metadata: Optional[Dict[str, object]] = None,
) -> Dict[str, object]:
    """Create a normalized telemetry payload for sensor readings."""

    if timestamp is None:
        timestamp = datetime.now(timezone.utc)

    payload: Dict[str, object] = {
        "measurementType": measurement_type,
        "value": float(value),
        "unit": unit,
        "timestamp": timestamp.isoformat(),
        "source": source,
    }
    if metadata:
        payload["metadata"] = metadata
    return payload


__all__ = [
    "AzureIoTHubPublisher",
    "BackgroundTelemetryRelay",
    "IoTOpsEdgePublisher",
    "TelemetryPublisher",
    "build_sensor_payload",
]
