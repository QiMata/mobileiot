"""USB serial LED control demo for Raspberry Pi."""

from __future__ import annotations

import argparse
import logging
import time
from contextlib import contextmanager
from typing import Iterator

import RPi.GPIO as GPIO
import serial

LOGGER = logging.getLogger(__name__)

DEFAULT_PORT = "/dev/ttyGS0"
DEFAULT_BAUD = 9600
LED_PIN = 17


class SerialDemo:
    def __init__(self, port: str, baud_rate: int, led_pin: int = LED_PIN, poll_interval: float = 0.1) -> None:
        self._port = port
        self._baud_rate = baud_rate
        self._led_pin = led_pin
        self._poll_interval = poll_interval

    def run(self) -> None:
        configure_gpio(self._led_pin)
        try:
            with serial_connection(self._port, self._baud_rate) as conn:
                LOGGER.info("Listening for commands on %s", self._port)
                for line in read_serial_lines(conn, self._poll_interval):
                    if not line:
                        continue
                    LOGGER.info("Received: %s", line)
                    response = self.handle_command(line)
                    if response:
                        conn.write(response)
        except serial.SerialException as exc:
            LOGGER.error("Serial port error: %s", exc)
        finally:
            cleanup_gpio()

    def handle_command(self, command: str) -> bytes | None:
        match command.upper():
            case "LED_ON":
                GPIO.output(self._led_pin, GPIO.HIGH)
                return b"ACK: LED turned ON\n"
            case "LED_OFF":
                GPIO.output(self._led_pin, GPIO.LOW)
                return b"ACK: LED turned OFF\n"
            case _:
                LOGGER.warning("Unknown command: %s", command)
                return b"NACK: Unknown command\n"


@contextmanager
def serial_connection(port: str, baud_rate: int, timeout: float = 1.0) -> Iterator[serial.SerialBase]:
    conn = serial.Serial(port, baud_rate, timeout=timeout)
    try:
        yield conn
    finally:
        conn.close()


def read_serial_lines(conn: serial.SerialBase, poll_interval: float) -> Iterator[str]:
    while True:
        if conn.in_waiting:
            try:
                raw = conn.readline()
            except serial.SerialException as exc:
                LOGGER.error("Failed reading from serial connection: %s", exc)
                break
            try:
                text = raw.decode("utf-8").strip()
            except UnicodeDecodeError:
                LOGGER.warning("Discarding undecodable payload: %s", raw)
                continue
            yield text
        else:
            time.sleep(poll_interval)


def configure_gpio(pin: int) -> None:
    GPIO.setmode(GPIO.BCM)
    GPIO.setup(pin, GPIO.OUT, initial=GPIO.LOW)


def cleanup_gpio() -> None:
    try:
        GPIO.cleanup()
    except RuntimeError:
        LOGGER.debug("GPIO cleanup attempted without setup")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="USB serial LED control demo")
    parser.add_argument("--port", default=DEFAULT_PORT, help="Serial port to monitor")
    parser.add_argument("--baud", type=int, default=DEFAULT_BAUD, help="Serial baud rate")
    parser.add_argument("--interval", type=float, default=0.1, help="Polling interval when idle")
    return parser.parse_args()


def main() -> None:
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s: %(message)s")
    args = parse_args()
    demo = SerialDemo(args.port, args.baud, poll_interval=args.interval)
    demo.run()


if __name__ == "__main__":
    main()
