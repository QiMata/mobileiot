"""iBeacon broadcaster demo for the MobileIoT application."""

from __future__ import annotations

import argparse
import logging
import time
import uuid as uuidlib
from typing import Optional

from bluezero import broadcaster

DEFAULT_UUID = uuidlib.UUID("12345678-1234-1234-1234-1234567890AB")
DEFAULT_MAJOR = 1
DEFAULT_MINOR = 2
DEFAULT_TX_POWER = -59
DEFAULT_DHT_PIN = 4
DEFAULT_SENSOR_INTERVAL = 5.0

LOGGER = logging.getLogger(__name__)


def _build_ibeacon_payload(
    beacon_uuid: uuidlib.UUID,
    major: int,
    minor: int,
    tx_power: int,
) -> bytes:
    if not 0 <= major <= 0xFFFF:
        raise ValueError("Major value must be between 0 and 65535")
    if not 0 <= minor <= 0xFFFF:
        raise ValueError("Minor value must be between 0 and 65535")
    if not -128 <= tx_power <= 127:
        raise ValueError("TX power must be between -128 and 127 dBm")

    uuid_bytes = beacon_uuid.bytes
    major_bytes = major.to_bytes(2, byteorder="big")
    minor_bytes = minor.to_bytes(2, byteorder="big")
    tx_power_byte = (tx_power & 0xFF).to_bytes(1, byteorder="big")
    ibeacon_prefix = b"\x02\x15"
    return ibeacon_prefix + uuid_bytes + major_bytes + minor_bytes + tx_power_byte


def start_beacon(
    *,
    beacon_uuid: uuidlib.UUID,
    major: int,
    minor: int,
    tx_power: int,
    adapter: Optional[str],
) -> broadcaster.Beacon:
    ibeacon_payload = _build_ibeacon_payload(beacon_uuid, major, minor, tx_power)
    beacon = broadcaster.Beacon(adapter=adapter) if adapter else broadcaster.Beacon()
    beacon.add_manufacturer_data("4c00", ibeacon_payload)
    beacon.start_beacon()
    LOGGER.info(
        "iBeacon advertising started (UUID=%s Major=%d Minor=%d TX=%d dBm)",
        beacon_uuid,
        major,
        minor,
        tx_power,
    )
    return beacon


def run_event_loop(beacon: broadcaster.Beacon, *, duration: Optional[float]) -> None:
    start_time = time.monotonic()
    try:
        while True:
            if duration is not None and time.monotonic() - start_time >= duration:
                LOGGER.info("Requested duration reached; stopping beacon broadcast")
                break
            time.sleep(1.0)
    except KeyboardInterrupt:
        LOGGER.info("Keyboard interrupt received; stopping beacon broadcast")
    finally:
        beacon.stop_beacon()
        LOGGER.info("Beacon advertising stopped")


def _read_dht22(pin: int) -> tuple[float, float]:
    try:
        import Adafruit_DHT  # type: ignore
    except ImportError as exc:
        raise RuntimeError("sensor mode requires Adafruit_DHT to be installed") from exc

    humidity, temperature = Adafruit_DHT.read_retry(Adafruit_DHT.DHT22, pin)
    if humidity is None or temperature is None:
        raise RuntimeError("DHT22 read returned None")
    return float(temperature), float(humidity)


def _to_uint16(value: float) -> int:
    return max(0, min(0xFFFF, int(round(value))))


def run_sensor_loop(
    *,
    beacon_uuid: uuidlib.UUID,
    tx_power: int,
    adapter: Optional[str],
    dht_pin: int,
    update_interval: float,
    duration: Optional[float],
) -> None:
    beacon: Optional[broadcaster.Beacon] = None
    start_time = time.monotonic()

    try:
        while True:
            if duration is not None and time.monotonic() - start_time >= duration:
                LOGGER.info("Requested duration reached; stopping beacon broadcast")
                break

            temp_c, humidity = _read_dht22(dht_pin)
            major = _to_uint16(temp_c * 100)
            minor = _to_uint16(humidity * 100)

            if beacon is not None:
                beacon.stop_beacon()

            beacon = start_beacon(
                beacon_uuid=beacon_uuid,
                major=major,
                minor=minor,
                tx_power=tx_power,
                adapter=adapter,
            )
            LOGGER.info(
                "Sensor update: temp=%.2fC (major=%d), humidity=%.2f%% (minor=%d)",
                temp_c,
                major,
                humidity,
                minor,
            )
            time.sleep(update_interval)
    except KeyboardInterrupt:
        LOGGER.info("Keyboard interrupt received; stopping beacon broadcast")
    finally:
        if beacon is not None:
            beacon.stop_beacon()
        LOGGER.info("Beacon advertising stopped")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Broadcast an iBeacon advertisement")
    parser.add_argument(
        "--uuid",
        default=str(DEFAULT_UUID),
        help="UUID to broadcast (defaults to the MobileIoT demo UUID)",
    )
    parser.add_argument("--major", type=int, default=DEFAULT_MAJOR, help="Beacon major value (0-65535)")
    parser.add_argument("--minor", type=int, default=DEFAULT_MINOR, help="Beacon minor value (0-65535)")
    parser.add_argument(
        "--tx-power",
        type=int,
        default=DEFAULT_TX_POWER,
        help="Calibrated RSSI at 1m in dBm (range -128 to 127)",
    )
    parser.add_argument(
        "--adapter",
        help="Bluetooth adapter to use (for example hci0). Defaults to the primary adapter.",
    )
    parser.add_argument(
        "--duration",
        type=float,
        help="Optional duration in seconds to advertise before stopping automatically",
    )
    parser.add_argument(
        "--sensor-major-minor",
        action="store_true",
        help="Encode DHT22 temperature/humidity into major/minor and refresh periodically",
    )
    parser.add_argument(
        "--dht-pin",
        type=int,
        default=DEFAULT_DHT_PIN,
        help="BCM GPIO pin used for the DHT22 sensor when --sensor-major-minor is enabled",
    )
    parser.add_argument(
        "--update-interval",
        type=float,
        default=DEFAULT_SENSOR_INTERVAL,
        help="Sensor refresh interval in seconds when --sensor-major-minor is enabled",
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

    try:
        beacon_uuid = uuidlib.UUID(args.uuid)
    except ValueError as exc:
        raise SystemExit(f"Invalid UUID '{args.uuid}': {exc}")

    if args.sensor_major_minor:
        run_sensor_loop(
            beacon_uuid=beacon_uuid,
            tx_power=args.tx_power,
            adapter=args.adapter,
            dht_pin=args.dht_pin,
            update_interval=max(0.5, args.update_interval),
            duration=args.duration,
        )
        return

    beacon = start_beacon(
        beacon_uuid=beacon_uuid,
        major=args.major,
        minor=args.minor,
        tx_power=args.tx_power,
        adapter=args.adapter,
    )
    run_event_loop(beacon, duration=args.duration)


if __name__ == "__main__":
    main()
