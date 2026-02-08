import json
import sys
import tempfile
import types
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

_gpio_events: list[tuple[int, int]] = []


def _gpio_output(pin: int, state: int):
    _gpio_events.append((pin, state))


def _gpio_cleanup():
    _gpio_events.append(("cleanup", None))


GPIO_stub = types.SimpleNamespace(
    BCM=11,
    OUT=0,
    LOW=0,
    HIGH=1,
    setmode=lambda *args, **kwargs: None,
    setup=lambda *args, **kwargs: None,
    output=_gpio_output,
    cleanup=_gpio_cleanup,
)


class _DhtStub:
    def __init__(self) -> None:
        self.value = (55.0, 21.5)

    def read_retry(self, sensor, pin):
        return self.value


dht_stub = _DhtStub()
Adafruit_DHT_stub = types.SimpleNamespace(DHT22=22, read_retry=dht_stub.read_retry)


class _Adapter:
    def __init__(self):
        self.address = "AA:BB:CC:DD:EE:FF"

    @staticmethod
    def available():
        return [_Adapter()]


class _Characteristic:
    def __init__(self, **kwargs):
        self.kwargs = kwargs


class _Service:
    def __init__(self, **kwargs):
        self.characteristics = []
        self.kwargs = kwargs


class _Peripheral:
    def __init__(self, *args, **kwargs):
        self.services = []
        self.args = args
        self.kwargs = kwargs

    def add_service(self, **kwargs):
        self.services.append(_Service(**kwargs))

    def add_characteristic(self, **kwargs):
        self.services[-1].characteristics.append(_Characteristic(**kwargs))

    def publish(self):
        pass

    def quit(self):
        pass

    def unpublish(self):
        pass


bluezero_pkg = types.ModuleType("bluezero")
adapter_mod = types.ModuleType("bluezero.adapter")
peripheral_mod = types.ModuleType("bluezero.peripheral")
adapter_mod.Adapter = _Adapter
peripheral_mod.Peripheral = _Peripheral

sys.modules.setdefault("bluezero", bluezero_pkg)
sys.modules["bluezero.adapter"] = adapter_mod
sys.modules["bluezero.peripheral"] = peripheral_mod

sys.modules.setdefault("Adafruit_DHT", Adafruit_DHT_stub)
sys.modules.setdefault("RPi", types.ModuleType("RPi"))
sys.modules["RPi"].GPIO = GPIO_stub
sys.modules["RPi.GPIO"] = GPIO_stub

import bluetoothle_demo  # noqa: E402


class BluetoothLeDemoTests(unittest.TestCase):
    def setUp(self) -> None:
        bluetoothle_demo._last_temp_c = 0.0
        bluetoothle_demo._last_humidity = 0.0
        bluetoothle_demo._last_sample_ts = -10.0
        dht_stub.value = (55.0, 21.5)
        _gpio_events.clear()

    def test_load_ble_constants_requires_keys(self):
        with tempfile.NamedTemporaryFile("w", delete=False) as handle:
            json.dump({"serviceUuid": "abc"}, handle)
            temp_path = Path(handle.name)

        with self.assertRaises(KeyError):
            bluetoothle_demo.load_ble_constants(temp_path)

        temp_path.unlink()

    def test_load_ble_constants_returns_strings(self):
        payload = {
            "serviceUuid": "123",
            "temperatureCharacteristicUuid": "456",
            "humidityCharacteristicUuid": "789",
            "ledCharacteristicUuid": "abc",
        }
        with tempfile.NamedTemporaryFile("w", delete=False) as handle:
            json.dump(payload, handle)
            path = Path(handle.name)

        constants = bluetoothle_demo.load_ble_constants(path)

        self.assertEqual(constants["serviceUuid"], "123")
        path.unlink()

    def test_read_dht22_uses_cache_within_interval(self):
        first = bluetoothle_demo._read_dht22(now=0.0)
        self.assertEqual(first, (21.5, 55.0))

        dht_stub.value = (40.0, 10.0)
        cached = bluetoothle_demo._read_dht22(now=1.0)
        self.assertEqual(cached, first)

        refreshed = bluetoothle_demo._read_dht22(now=5.0)
        self.assertEqual(refreshed, (10.0, 40.0))

    def test_temperature_callback_falls_back_to_last_value(self):
        bluetoothle_demo._last_temp_c = 22.0
        bluetoothle_demo._last_sample_ts = 0.0
        dht_stub.value = (None, None)

        encoded = bluetoothle_demo.temperature_read_callback()
        self.assertEqual(encoded, [int(22.0 * 100) & 0xFF, int(22.0 * 100) >> 8])

    def test_humidity_callback_falls_back_to_last_value(self):
        bluetoothle_demo._last_humidity = 48.5
        bluetoothle_demo._last_sample_ts = 0.0
        dht_stub.value = (None, None)

        encoded = bluetoothle_demo.humidity_read_callback()
        expected = int(round(48.5 * 100))
        self.assertEqual(encoded, [expected & 0xFF, expected >> 8])

    def test_led_write_callback_updates_gpio(self):
        bluetoothle_demo.led_write_callback([0x01])
        bluetoothle_demo.led_write_callback([0x00])

        self.assertIn((bluetoothle_demo.LED_PIN, GPIO_stub.HIGH), _gpio_events)
        self.assertIn((bluetoothle_demo.LED_PIN, GPIO_stub.LOW), _gpio_events)


if __name__ == "__main__":
    unittest.main()
