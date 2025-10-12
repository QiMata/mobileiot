"""Raspberry Pi BLE peripheral demo for the DHT22 sensor and LED control."""

from __future__ import annotations

import argparse
import json
import logging
import time
from pathlib import Path
from typing import Dict, List, Optional, Sequence, Tuple

import Adafruit_DHT  # Library for DHT22 sensor (ensure it's installed)
import RPi.GPIO as GPIO  # GPIO library for LED control
from bluezero import adapter, peripheral
from azure_retransmit import (
    AzureIoTHubPublisher,
    BackgroundTelemetryRelay,
    IoTOpsEdgePublisher,
    TelemetryPublisher,
    build_sensor_payload,
)

LOGGER = logging.getLogger(__name__)

BLE_CONFIG_PATH = Path(__file__).resolve().parents[1] / "shared" / "ble_constants.json"
DEFAULT_DEVICE_NAME = "PiSensor"

TELEMETRY_SOURCE = "bluetoothle_demo"
_TELEMETRY_RELAY: Optional[BackgroundTelemetryRelay] = None

DHT_SENSOR = Adafruit_DHT.DHT22
DHT_PIN = 4
LED_PIN = 17

_SENSOR_SAMPLE_INTERVAL = 2.0
_last_temp_c = 0.0
_last_humidity = 0.0
_last_sample_ts = 0.0


def set_telemetry_relay(relay: Optional[BackgroundTelemetryRelay]) -> None:
    """Configure the telemetry relay used to forward sensor data to Azure services."""

    global _TELEMETRY_RELAY
    _TELEMETRY_RELAY = relay


def _emit_sensor_telemetry(measurement: str, value: float, unit: str) -> None:
    if _TELEMETRY_RELAY is None:
        return

    payload = build_sensor_payload(
        measurement,
        value,
        unit,
        source=TELEMETRY_SOURCE,
        metadata={"transport": "bluetooth-le"},
    )
    try:
        _TELEMETRY_RELAY.emit(payload)
    except RuntimeError:
        LOGGER.debug("Telemetry relay is closed; dropping %s payload", measurement)


class SensorReadError(RuntimeError):
    """Raised when the DHT22 sensor fails to provide a reading."""


def load_ble_constants(path: Path = BLE_CONFIG_PATH) -> Dict[str, str]:
    if not path.exists():
        raise FileNotFoundError(f"BLE constants file missing at {path}")

    with path.open("r", encoding="utf-8") as handle:
        data = json.load(handle)

    required = {
        "serviceUuid",
        "temperatureCharacteristicUuid",
        "humidityCharacteristicUuid",
        "ledCharacteristicUuid",
    }
    missing = sorted(required - data.keys())
    if missing:
        raise KeyError(f"BLE constants file missing keys: {', '.join(missing)}")

    return {key: str(value) for key, value in data.items()}


def configure_gpio() -> None:
    GPIO.setmode(GPIO.BCM)
    GPIO.setup(LED_PIN, GPIO.OUT, initial=GPIO.LOW)


def cleanup_gpio() -> None:
    try:
        GPIO.cleanup()
    except RuntimeError:
        LOGGER.debug("GPIO cleanup failed; hardware may already be released")


def _read_dht22(now: float | None = None) -> Tuple[float, float]:
    global _last_temp_c, _last_humidity, _last_sample_ts

    timestamp = now if now is not None else time.time()
    if timestamp - _last_sample_ts < _SENSOR_SAMPLE_INTERVAL:
        return _last_temp_c, _last_humidity

    humidity, temperature = Adafruit_DHT.read_retry(DHT_SENSOR, DHT_PIN)
    if humidity is None or temperature is None:
        raise SensorReadError("DHT22 read returned None")

    _last_temp_c = float(temperature)
    _last_humidity = float(humidity)
    _last_sample_ts = timestamp
    return _last_temp_c, _last_humidity


def _int16_bytes(value: int) -> List[int]:
    return list(int(value).to_bytes(2, byteorder="little", signed=True))


def _uint16_bytes(value: int) -> List[int]:
    return list(int(value).to_bytes(2, byteorder="little", signed=False))


def temperature_read_callback() -> List[int]:
    try:
        temp_c, _ = _read_dht22()
    except SensorReadError as exc:
        LOGGER.warning("Falling back to last known temperature: %s", exc)
        temp_c = _last_temp_c

    _emit_sensor_telemetry("temperature", temp_c, "C")
    encoded = _int16_bytes(int(round(temp_c * 100)))
    return encoded


def humidity_read_callback() -> List[int]:
    try:
        _, humidity = _read_dht22()
    except SensorReadError as exc:
        LOGGER.warning("Falling back to last known humidity: %s", exc)
        humidity = _last_humidity

    _emit_sensor_telemetry("humidity", humidity, "%")
    encoded = _uint16_bytes(int(round(humidity * 100)))
    return encoded


def led_write_callback(value: Sequence[int]) -> None:
    if value and value[0] == 0x01:
        GPIO.output(LED_PIN, GPIO.HIGH)
        LOGGER.info("LED turned ON")
    else:
        GPIO.output(LED_PIN, GPIO.LOW)
        LOGGER.info("LED turned OFF")


def _build_publishers_from_args(args: argparse.Namespace) -> List[TelemetryPublisher]:
    publishers: List[TelemetryPublisher] = []

    if args.azure_iot_hub_connection_string:
        publishers.append(AzureIoTHubPublisher(args.azure_iot_hub_connection_string))

    if args.azure_iot_ops_ingest_url:
        publishers.append(
            IoTOpsEdgePublisher(
                args.azure_iot_ops_ingest_url,
                api_key=args.azure_iot_ops_api_key,
                api_key_header=args.azure_iot_ops_api_key_header,
            )
        )

    return publishers


def build_peripheral(constants: Dict[str, str], *, device_name: str) -> peripheral.Peripheral:
    adapter_address = next(iter(adapter.Adapter.available())).address
    ble_periph = peripheral.Peripheral(
        adapter_address,
        local_name=device_name,
        appearance=0x0340,
    )

    ble_periph.add_service(srv_id=1, uuid=constants["serviceUuid"], primary=True)
    ble_periph.add_characteristic(
        srv_id=1,
        chr_id=1,
        uuid=constants["temperatureCharacteristicUuid"],
        value=[0x00, 0x00],
        notifying=False,
        flags=["read"],
        read_callback=temperature_read_callback,
        write_callback=None,
        notify_callback=None,
    )
    ble_periph.add_characteristic(
        srv_id=1,
        chr_id=2,
        uuid=constants["humidityCharacteristicUuid"],
        value=[0x00, 0x00],
        notifying=False,
        flags=["read"],
        read_callback=humidity_read_callback,
        write_callback=None,
        notify_callback=None,
    )
    ble_periph.add_characteristic(
        srv_id=1,
        chr_id=3,
        uuid=constants["ledCharacteristicUuid"],
        value=[0x00],
        notifying=False,
        flags=["write"],
        read_callback=None,
        write_callback=lambda data: led_write_callback(data),
    )

    return ble_periph


def run_event_loop(periph: peripheral.Peripheral, *, device_name: str) -> None:
    periph.publish()
    LOGGER.info("BLE GATT server started (Advertising as '%s'). Press Ctrl+C to exit.", device_name)
    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        LOGGER.info("Stopping GATT server...")
    finally:
        try:
            periph.quit()
        except AttributeError:
            periph.unpublish()


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="BLE GATT sensor hub demo")
    parser.add_argument("--device-name", default=DEFAULT_DEVICE_NAME, help="Bluetooth device name to advertise")
    parser.add_argument(
        "--azure-iot-hub-connection-string",
        help="Connection string for Azure IoT Hub telemetry forwarding",
    )
    parser.add_argument(
        "--azure-iot-ops-ingest-url",
        help="Azure IoT Operations Edge ingest endpoint for telemetry forwarding",
    )
    parser.add_argument(
        "--azure-iot-ops-api-key",
        help="API key or token used when publishing to Azure IoT Operations Edge",
    )
    parser.add_argument(
        "--azure-iot-ops-api-key-header",
        default="Authorization",
        help="HTTP header name used for the IoT Operations Edge API key",
    )
    parser.add_argument(
        "--telemetry-queue-size",
        type=int,
        default=100,
        help="Maximum number of telemetry payloads buffered for Azure forwarding",
    )
    parser.add_argument(
        "--log-level",
        default="INFO",
        choices=["CRITICAL", "ERROR", "WARNING", "INFO", "DEBUG"],
        help="Logging verbosity",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    log_level = getattr(logging, args.log_level)
    logging.basicConfig(level=log_level, format="%(asctime)s %(levelname)s %(name)s: %(message)s")

    constants = load_ble_constants()
    configure_gpio()

    telemetry_relay: Optional[BackgroundTelemetryRelay] = None
    publishers = _build_publishers_from_args(args)
    if publishers:
        telemetry_relay = BackgroundTelemetryRelay(publishers, max_queue_size=args.telemetry_queue_size)
        set_telemetry_relay(telemetry_relay)
        LOGGER.info("Azure telemetry forwarding enabled to %d target(s)", len(publishers))
    else:
        LOGGER.info("Azure telemetry forwarding disabled")

    try:
        ble_periph = build_peripheral(constants, device_name=args.device_name)
        run_event_loop(ble_periph, device_name=args.device_name)
    finally:
        if telemetry_relay is not None:
            telemetry_relay.close()
        set_telemetry_relay(None)
        cleanup_gpio()


if __name__ == "__main__":
    main()
