"""Shared wiring helpers for the Raspberry Pi BLE demo."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Callable, Dict, List, Sequence

import RPi.GPIO as GPIO
from bluezero import adapter, peripheral

from azure_retransmit import (
    AzureIoTHubPublisher,
    IoTOpsEdgePublisher,
    TelemetryPublisher,
)

BLE_CONFIG_PATH = Path(__file__).resolve().parents[1] / "shared" / "ble_constants.json"
DEFAULT_DEVICE_NAME = "PiSensor"
LED_PIN = 17


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
        # Cleanup is best-effort because the demo may be interrupted mid-init.
        pass


def build_publishers_from_args(args: argparse.Namespace) -> List[TelemetryPublisher]:
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


def build_peripheral(
    constants: Dict[str, str],
    *,
    device_name: str,
    temperature_read_callback: Callable[[], Sequence[int]],
    humidity_read_callback: Callable[[], Sequence[int]],
    led_write_callback: Callable[[Sequence[int]], None],
    temp_notify_callback: Callable[[bool, object | None], None],
    hum_notify_callback: Callable[[bool, object | None], None],
) -> peripheral.Peripheral:
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
        flags=["read", "notify"],
        read_callback=temperature_read_callback,
        write_callback=None,
        notify_callback=temp_notify_callback,
    )
    ble_periph.add_characteristic(
        srv_id=1,
        chr_id=2,
        uuid=constants["humidityCharacteristicUuid"],
        value=[0x00, 0x00],
        notifying=False,
        flags=["read", "notify"],
        read_callback=humidity_read_callback,
        write_callback=None,
        notify_callback=hum_notify_callback,
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
