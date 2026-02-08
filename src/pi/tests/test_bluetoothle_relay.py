"""Tests for the BLE telemetry retransmission integration."""

from __future__ import annotations

import importlib
import sys
import types


def _install_stubs() -> None:
    # Adafruit_DHT stub
    adafruit = types.ModuleType("Adafruit_DHT")
    adafruit.DHT22 = object()

    def read_retry(sensor, pin):  # pragma: no cover - fallback path
        return 40.0, 20.0

    adafruit.read_retry = read_retry
    sys.modules["Adafruit_DHT"] = adafruit

    # RPi.GPIO stub
    gpio = types.ModuleType("RPi.GPIO")
    gpio.BCM = 1
    gpio.OUT = 0
    gpio.LOW = 0
    gpio.HIGH = 1

    def noop(*args, **kwargs):
        return None

    gpio.setmode = noop
    gpio.setup = noop
    gpio.output = noop
    gpio.cleanup = noop
    sys.modules["RPi.GPIO"] = gpio

    rpi_module = types.ModuleType("RPi")
    rpi_module.GPIO = gpio
    sys.modules["RPi"] = rpi_module

    # bluezero stub
    bluezero = types.ModuleType("bluezero")

    adapter_module = types.ModuleType("bluezero.adapter")

    class _Adapter:
        @staticmethod
        def available():
            class _Instance:
                address = "00:00:00:00:00:00"

            return [_Instance()]

    adapter_module.Adapter = _Adapter

    peripheral_module = types.ModuleType("bluezero.peripheral")

    class _Peripheral:
        def __init__(self, *args, **kwargs):
            self.added_services: list[dict[str, object]] = []

        def add_service(self, **kwargs):
            self.added_services.append({"service": kwargs})

        def add_characteristic(self, **kwargs):
            self.added_services.append({"characteristic": kwargs})

    peripheral_module.Peripheral = _Peripheral

    bluezero.adapter = adapter_module
    bluezero.peripheral = peripheral_module

    sys.modules["bluezero"] = bluezero
    sys.modules["bluezero.adapter"] = adapter_module
    sys.modules["bluezero.peripheral"] = peripheral_module


class _RelayRecorder:
    def __init__(self) -> None:
        self.payloads: list[dict[str, object]] = []

    def emit(self, payload: dict[str, object]) -> None:
        self.payloads.append(payload)


def test_temperature_and_humidity_callbacks_emit_payloads(monkeypatch) -> None:
    _install_stubs()
    module = importlib.import_module("bluetoothle_demo")
    importlib.reload(module)

    relay = _RelayRecorder()
    module.set_telemetry_relay(relay)  # type: ignore[attr-defined]

    monkeypatch.setattr(module, "_read_dht22", lambda now=None: (22.25, 48.5))

    module.temperature_read_callback()
    module.humidity_read_callback()

    assert len(relay.payloads) == 2
    temp_payload = relay.payloads[0]
    humidity_payload = relay.payloads[1]

    assert temp_payload["measurementType"] == "temperature"
    assert temp_payload["unit"] == "C"
    assert humidity_payload["measurementType"] == "humidity"
    assert humidity_payload["unit"] == "%"
    assert temp_payload["source"] == "bluetoothle_demo"
    assert humidity_payload["source"] == "bluetoothle_demo"
