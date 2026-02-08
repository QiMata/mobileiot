"""USB serial LED control demo for Raspberry Pi."""

from __future__ import annotations

import argparse
import logging
import time
from contextlib import contextmanager
from typing import Iterator

import Adafruit_DHT
import RPi.GPIO as GPIO
import serial

LOGGER = logging.getLogger(__name__)

DEFAULT_PORT = "/dev/ttyGS0"
DEFAULT_BAUD = 9600
LED_PIN = 17
DHT_SENSOR = Adafruit_DHT.DHT22
DHT_PIN = 4
VALID_PINS = {4, 17, 18, 22, 23, 24, 25, 27}


def read_system_status() -> tuple[int, float, int]:
    uptime = 0
    cpu_temp = 0.0
    mem_free = 0

    try:
        with open("/proc/uptime", "r", encoding="utf-8") as handle:
            uptime = int(float(handle.read().split()[0]))
    except Exception:
        pass

    try:
        with open("/sys/class/thermal/thermal_zone0/temp", "r", encoding="utf-8") as handle:
            cpu_temp = int(handle.read().strip()) / 1000.0
    except Exception:
        pass

    try:
        with open("/proc/meminfo", "r", encoding="utf-8") as handle:
            for line in handle:
                if line.startswith("MemFree:"):
                    mem_free = int(line.split()[1])
                    break
    except Exception:
        pass

    return uptime, cpu_temp, mem_free


class SerialDemo:
    def __init__(
        self,
        port: str,
        baud_rate: int,
        led_pin: int = LED_PIN,
        poll_interval: float = 0.1,
    ) -> None:
        self._port = port
        self._baud_rate = baud_rate
        self._led_pin = led_pin
        self._poll_interval = poll_interval
        self._output_pins = {self._led_pin}

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
        parts = command.strip().split()
        if not parts:
            return None

        cmd = parts[0].upper()
        if cmd == "LED_ON":
            GPIO.output(self._led_pin, GPIO.HIGH)
            return b"ACK: LED turned ON\n"
        if cmd == "LED_OFF":
            GPIO.output(self._led_pin, GPIO.LOW)
            return b"ACK: LED turned OFF\n"

        if cmd == "GPIO_READ" and len(parts) == 2:
            return self._handle_gpio_read(parts[1])
        if cmd == "GPIO_WRITE" and len(parts) == 3:
            return self._handle_gpio_write(parts[1], parts[2])
        if cmd == "SENSOR_READ":
            return self._handle_sensor_read()
        if cmd == "STATUS":
            return self._handle_status()

        LOGGER.warning("Unknown command: %s", command)
        return b"NACK: Unknown command\n"

    def _handle_gpio_read(self, pin_token: str) -> bytes:
        try:
            pin = int(pin_token)
        except ValueError:
            return f"NACK: Invalid pin {pin_token}\n".encode()

        if pin not in VALID_PINS:
            return f"NACK: Invalid pin {pin}\n".encode()

        if pin not in self._output_pins:
            GPIO.setup(pin, getattr(GPIO, "IN", GPIO.OUT))

        if not hasattr(GPIO, "input"):
            return b"NACK: GPIO read not supported\n"

        value = GPIO.input(pin)
        return f"GPIO:{pin}={value}\n".encode()

    def _handle_gpio_write(self, pin_token: str, value_token: str) -> bytes:
        try:
            pin = int(pin_token)
            value = int(value_token)
        except ValueError:
            return b"NACK: Invalid arguments\n"

        if pin not in VALID_PINS:
            return f"NACK: Invalid pin {pin}\n".encode()
        if value not in (0, 1):
            return f"NACK: Invalid value {value}\n".encode()

        if pin not in self._output_pins:
            GPIO.setup(pin, GPIO.OUT)
            self._output_pins.add(pin)

        GPIO.output(pin, value)
        return f"ACK: GPIO {pin} set to {value}\n".encode()

    def _handle_sensor_read(self) -> bytes:
        humidity, temperature = Adafruit_DHT.read_retry(DHT_SENSOR, DHT_PIN)
        if temperature is None:
            temperature = 0.0
        if humidity is None:
            humidity = 0.0
        return f"SENSOR:temp={temperature:.1f},hum={humidity:.1f}\n".encode()

    def _handle_status(self) -> bytes:
        uptime, cpu_temp, mem_free = read_system_status()
        return f"STATUS:uptime={uptime},cpu_temp={cpu_temp:.1f},mem_free={mem_free}\n".encode()


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
