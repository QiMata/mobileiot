"""Tests for the Azure telemetry retransmission helpers."""

from __future__ import annotations

import json
from typing import Dict

import azure_retransmit


def test_build_sensor_payload_includes_metadata() -> None:
    payload = azure_retransmit.build_sensor_payload(
        "temperature",
        21.5,
        "C",
        source="bluetoothle_demo",
        metadata={"site": "factory"},
    )

    assert payload["measurementType"] == "temperature"
    assert payload["value"] == 21.5
    assert payload["unit"] == "C"
    assert payload["source"] == "bluetoothle_demo"
    assert payload["metadata"] == {"site": "factory"}
    assert "timestamp" in payload


class _RecorderPublisher:
    def __init__(self) -> None:
        self.payloads: list[Dict[str, object]] = []
        self.closed = False

    def publish(self, payload: Dict[str, object]) -> None:
        self.payloads.append(payload)

    def close(self) -> None:
        self.closed = True


def test_background_relay_flushes_payloads_and_closes_publishers() -> None:
    publisher = _RecorderPublisher()

    with azure_retransmit.BackgroundTelemetryRelay([publisher]) as relay:
        relay.emit({"value": 1})
        relay.emit({"value": 2})

    assert publisher.payloads == [{"value": 1}, {"value": 2}]
    assert publisher.closed is True


class _DummyHandle:
    def __init__(self) -> None:
        self.messages: list[Dict[str, object]] = []
        self.closed = False

    def send(self, payload: Dict[str, object]) -> None:
        self.messages.append(payload)

    def close(self) -> None:
        self.closed = True


def test_azure_iot_hub_publisher_uses_factory() -> None:
    handle = _DummyHandle()

    publisher = azure_retransmit.AzureIoTHubPublisher(
        "HostName=test.azure-devices.net;DeviceId=device;SharedAccessKey=abc",
        client_factory=lambda conn: handle,
    )

    publisher.publish({"value": 42})
    publisher.close()

    assert handle.messages == [{"value": 42}]
    assert handle.closed is True


class _DummyResponse:
    def __init__(self) -> None:
        self.status = 204

    def __enter__(self) -> "_DummyResponse":
        return self

    def __exit__(self, exc_type, exc, tb) -> None:
        return None

    def getcode(self) -> int:
        return self.status

    def read(self) -> bytes:
        return b""


class _DummyOpener:
    def __init__(self) -> None:
        self.requests: list = []

    def open(self, request, timeout: float):
        self.requests.append((request, timeout))
        return _DummyResponse()


def test_iot_ops_edge_publisher_posts_json_payload() -> None:
    opener = _DummyOpener()
    publisher = azure_retransmit.IoTOpsEdgePublisher(
        "https://example.test/ingest",
        api_key="token",
        api_key_header="X-API-KEY",
        opener=opener,
    )

    publisher.publish({"value": 99})

    assert len(opener.requests) == 1
    request, timeout = opener.requests[0]
    assert request.get_full_url() == "https://example.test/ingest"
    assert timeout == 10.0
    headers = {name.lower(): value for name, value in request.header_items()}
    assert headers["content-type"] == "application/json"
    assert headers["x-api-key"] == "token"
    assert json.loads(request.data.decode("utf-8")) == {"value": 99}
