import types
import sys
import unittest

# Provide lightweight stubs for Raspberry Pi specific modules before importing the module under test.
_outputs = []

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

serial_module = types.ModuleType("serial")
serial_module.SerialException = Exception
serial_module.SerialBase = object
serial_module.Serial = lambda *args, **kwargs: None
serial_module.Serial.__annotations__ = {}
sys.modules["serial"] = serial_module

import serial_demo  # noqa: E402


class SerialDemoTests(unittest.TestCase):
    def setUp(self) -> None:
        _outputs.clear()

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


if __name__ == "__main__":
    unittest.main()
