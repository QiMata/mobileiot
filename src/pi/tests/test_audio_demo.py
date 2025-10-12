import unittest
from unittest import mock
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import audio_demo


class AudioDemoTests(unittest.TestCase):
    def test_read_cpu_temp(self):
        with mock.patch("builtins.open", mock.mock_open(read_data="45000")):
            value = audio_demo.read_cpu_temp()
        self.assertEqual(value, 45.0)

    def test_transmit_invokes_minimodem(self):
        with mock.patch("subprocess.run") as run_mock:
            audio_demo.transmit(12.34)
        run_mock.assert_called_once()
        args, kwargs = run_mock.call_args
        self.assertEqual(args[0], ["minimodem", "--tx", "--quiet", "1200"])
        self.assertEqual(kwargs["input"], b"12.3\n")


if __name__ == "__main__":
    unittest.main()
