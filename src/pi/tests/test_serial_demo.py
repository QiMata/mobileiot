import types
import sys
import unittest
from itertools import islice
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

# Provide lightweight stubs for Raspberry Pi specific modules before importing the module under test.
_outputs: list[tuple[int, int]] = []


def _record_output(pin, state):
    _outputs.append((pin, state))


_gpio_stub = types.SimpleNamespace(
    BCM=11,
    OUT=0,
    LOW=0,
    HIGH=1,
    setmode=lambda *args, **kwargs: None,
    setup=lambda *args, **kwargs: None,
    output=_record_output,
    cleanup=lambda: None,
)


sys.modules.setdefault("RPi", types.ModuleType("RPi"))
sys.modules["RPi"].GPIO = _gpio_stub
sys.modules["RPi.GPIO"] = _gpio_stub


class _FakeSerial:
    def __init__(self, lines: list[bytes] | None = None, *, raise_on_read: Exception | None = None):
        self._lines = lines or []
        self._raise = raise_on_read
        self.written: list[bytes] = []
        self.closed = False
        self.in_waiting = len(self._lines)

    def readline(self):
        if self._raise:
            raise self._raise
        if not self._lines:
            self.in_waiting = 0
            return b""
        value = self._lines.pop(0)
        self.in_waiting = len(self._lines)
        return value

    def write(self, payload: bytes):
        self.written.append(payload)

    def close(self):
        self.closed = True


_serial_instances: list[_FakeSerial] = []


def _serial_factory(*args, **kwargs):
    conn = _FakeSerial()
    _serial_instances.append(conn)
    return conn


serial_module = types.ModuleType("serial")
serial_module.SerialException = Exception
serial_module.SerialBase = object
serial_module.Serial = _serial_factory
serial_module.Serial.__annotations__ = {}
sys.modules["serial"] = serial_module

import serial_demo  # noqa: E402


class SerialDemoTests(unittest.TestCase):
    def setUp(self) -> None:
        _outputs.clear()
        _serial_instances.clear()

    def test_handle_command_led_on(self):
        demo = serial_demo.SerialDemo(port="/dev/null", baud_rate=9600)
        response = demo.handle_command("LED_ON")
        self.assertEqual(response, b"ACK: LED turned ON\n")
        self.assertIn((serial_demo.LED_PIN, _gpio_stub.HIGH), _outputs)

    def test_handle_command_unknown(self):
        demo = serial_demo.SerialDemo(port="/dev/null", baud_rate=9600)
        response = demo.handle_command("BOGUS")
        self.assertEqual(response, b"NACK: Unknown command\n")
        self.assertEqual(_outputs, [])

    def test_serial_connection_closes_port(self):
        with serial_demo.serial_connection("/dev/null", 9600):
            self.assertEqual(len(_serial_instances), 1)
            self.assertFalse(_serial_instances[0].closed)

        self.assertTrue(_serial_instances[0].closed)

    def test_read_serial_lines_yields_decoded_text(self):
        fake = _FakeSerial(lines=[b"LED_ON\n", b"LED_OFF\n"])
        iterator = serial_demo.read_serial_lines(fake, poll_interval=0.0)
        lines = list(islice(iterator, 2))
        self.assertEqual(lines, ["LED_ON", "LED_OFF"])

    def test_read_serial_lines_handles_unicode_errors(self):
        fake = _FakeSerial(lines=[b"\xff\xff\xff"], raise_on_read=None)
        with mock.patch("serial_demo.time.sleep", side_effect=RuntimeError("stop")):
            with self.assertLogs(serial_demo.LOGGER, level="WARNING") as captured:
                iterator = serial_demo.read_serial_lines(fake, poll_interval=0.0)
                with self.assertRaises(RuntimeError):
                    next(iterator)
        self.assertIn("Discarding undecodable payload", " ".join(captured.output))

    def test_read_serial_lines_handles_serial_exception(self):
        fake = _FakeSerial(lines=[b"LED_ON\n"], raise_on_read=serial_module.SerialException("boom"))
        with self.assertLogs(serial_demo.LOGGER, level="ERROR") as captured:
            iterator = serial_demo.read_serial_lines(fake, poll_interval=0.0)
            list(islice(iterator, 1))
        self.assertIn("Failed reading from serial connection", " ".join(captured.output))


if __name__ == "__main__":
    unittest.main()
