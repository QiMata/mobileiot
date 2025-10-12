"""Transmit telemetry over the Raspberry Pi audio jack using minimodem."""

from __future__ import annotations

import argparse
import json
import logging
import subprocess
import time
from pathlib import Path
from typing import Callable

LOGGER = logging.getLogger(__name__)

try:  # Optional dependency used only when the DHT22 sensor is selected.
    import Adafruit_DHT  # type: ignore
except ImportError:  # pragma: no cover - handled at runtime when the sensor is requested
    Adafruit_DHT = None

DEFAULT_BAUD = 1200
DEFAULT_INTERVAL = 5.0
DEFAULT_DHT_PIN = 4

SensorReader = Callable[[argparse.Namespace | None], dict[str, float]]


def read_cpu_temp(_: argparse.Namespace | None = None) -> dict[str, float]:
    with open("/sys/class/thermal/thermal_zone0/temp", "r", encoding="utf-8") as handle:
        raw = handle.read().strip()
    return {"cpuTempC": float(raw) / 1000.0}


def read_dht22(args: argparse.Namespace) -> dict[str, float]:
    if Adafruit_DHT is None:
        raise RuntimeError("Adafruit_DHT must be installed to use the DHT22 sensor mode")

    humidity, temperature = Adafruit_DHT.read_retry(Adafruit_DHT.DHT22, args.dht22_pin)
    if humidity is None or temperature is None:
        raise RuntimeError("DHT22 sensor returned no data")
    return {"tempC": float(temperature), "humidityPercent": float(humidity)}


def read_json_file(args: argparse.Namespace) -> dict[str, float]:
    path = Path(args.input_path)
    payload = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(payload, dict):
        raise RuntimeError("Input file must contain a JSON object")
    numeric_payload: dict[str, float] = {}
    for key, value in payload.items():
        try:
            numeric_payload[str(key)] = float(value)
        except (TypeError, ValueError) as exc:
            raise RuntimeError(f"Value for '{key}' is not numeric: {value!r}") from exc
    return numeric_payload


SENSOR_READERS: dict[str, SensorReader] = {
    "cpu-temp": read_cpu_temp,
    "dht22": read_dht22,
    "json-file": read_json_file,
}


def transmit(message: object, *, baud: int = DEFAULT_BAUD) -> None:
    if isinstance(message, str):
        text = message
    elif isinstance(message, (int, float)):
        text = f"{float(message):.1f}"
    else:
        text = str(message)

    LOGGER.debug("Transmitting payload via minimodem: %s", text)
    process = subprocess.run(
        ["minimodem", "--tx", "--quiet", str(baud)],
        input=(text + "\n").encode("utf-8"),
        check=False,
    )
    if process.returncode not in (0, None):
        LOGGER.warning("minimodem exited with status code %s", process.returncode)


def format_payload(readings: dict[str, float]) -> str:
    parts = [f"{key}={value:.2f}" for key, value in sorted(readings.items())]
    return ",".join(parts)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Send telemetry samples over audio using minimodem")
    parser.add_argument(
        "--sensor",
        choices=sorted(SENSOR_READERS.keys()),
        default="cpu-temp",
        help="Sensor source used to populate telemetry values",
    )
    parser.add_argument(
        "--interval",
        type=float,
        default=DEFAULT_INTERVAL,
        help="Number of seconds between transmissions",
    )
    parser.add_argument(
        "--baud",
        type=int,
        default=DEFAULT_BAUD,
        help="FSK baud rate used when invoking minimodem",
    )
    parser.add_argument(
        "--dht22-pin",
        type=int,
        default=DEFAULT_DHT_PIN,
        help="BCM pin used for the DHT22 data line when --sensor=dht22",
    )
    parser.add_argument(
        "--input-path",
        type=str,
        help="Path to a JSON file containing numeric fields when --sensor=json-file",
    )
    parser.add_argument(
        "--log-level",
        default="INFO",
        choices=["CRITICAL", "ERROR", "WARNING", "INFO", "DEBUG"],
        help="Logging verbosity",
    )
    return parser.parse_args()


def resolve_reader(args: argparse.Namespace) -> SensorReader:
    reader = SENSOR_READERS[args.sensor]
    if args.sensor == "json-file" and not args.input_path:
        raise SystemExit("--input-path must be provided when --sensor=json-file")
    return reader


def main() -> None:
    args = parse_args()
    log_level = getattr(logging, args.log_level)
    logging.basicConfig(level=log_level, format="%(asctime)s %(levelname)s %(name)s: %(message)s")
    reader = resolve_reader(args)

    LOGGER.info(
        "Starting audio telemetry loop (sensor=%s, interval=%ss, baud=%s)",
        args.sensor,
        args.interval,
        args.baud,
    )

    try:
        while True:
            readings = reader(args)
            message = format_payload(readings)
            LOGGER.info("Transmitting telemetry payload: %s", message)
            transmit(message, baud=args.baud)
            time.sleep(args.interval)
    except KeyboardInterrupt:
        LOGGER.info("Stopping audio telemetry")


if __name__ == "__main__":
    main()
