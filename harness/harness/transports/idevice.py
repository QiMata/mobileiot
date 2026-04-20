from __future__ import annotations

import shutil
import subprocess
import sys

from harness.inventory.model import Device
from harness.transports.base import Transport, TransportError, TransportResult


class IosTransport(Transport):
    """iOS transport. Constructible on any OS; actual operations require macOS + pymobiledevice3."""

    kind = "idevice"

    def __init__(self, device: Device):
        self.device = device
        if not device.udid:
            raise TransportError(f"device {device.id} has no udid")
        self._mac = sys.platform == "darwin"
        self._tool = shutil.which("pymobiledevice3") if self._mac else None
        self._port_forward_procs: dict[int, subprocess.Popen] = {}

    def _require_mac(self) -> None:
        if not self._mac:
            raise TransportError("ios transport requires macOS")
        if self._tool is None:
            raise TransportError("pymobiledevice3 not installed (pip install pymobiledevice3)")

    def shell(self, cmd, *, timeout: float = 30.0) -> TransportResult:
        raise TransportError("ios does not support arbitrary shell; use app-side /scenario HTTP hook instead")

    def push(self, local: str, remote: str) -> None:
        self._require_mac()
        r = subprocess.run(
            ["pymobiledevice3", "afc", "push", local, remote, "--udid", self.device.udid],
            capture_output=True, text=True, check=False, timeout=120.0,
        )
        if r.returncode != 0:
            raise TransportError(f"afc push failed: {r.stderr}")

    def pull(self, remote: str, local: str) -> None:
        self._require_mac()
        r = subprocess.run(
            ["pymobiledevice3", "afc", "pull", remote, local, "--udid", self.device.udid],
            capture_output=True, text=True, check=False, timeout=120.0,
        )
        if r.returncode != 0:
            raise TransportError(f"afc pull failed: {r.stderr}")

    def forward_port(self, local_port: int, remote_port: int) -> None:
        self._require_mac()
        proc = subprocess.Popen(
            ["pymobiledevice3", "usbmux", "forward", str(local_port), str(remote_port), "--udid", self.device.udid],
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
        )
        self._port_forward_procs[local_port] = proc

    def unforward_port(self, local_port: int) -> None:
        proc = self._port_forward_procs.pop(local_port, None)
        if proc:
            proc.terminate()
            try:
                proc.wait(timeout=5.0)
            except subprocess.TimeoutExpired:
                proc.kill()

    def stream_logs(self):
        if not self._mac:
            return iter(())
        proc = subprocess.Popen(
            ["pymobiledevice3", "syslog", "--udid", self.device.udid],
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, bufsize=1,
        )
        try:
            assert proc.stdout is not None
            for line in proc.stdout:
                yield line.rstrip("\n")
        finally:
            proc.terminate()

    def close(self) -> None:
        for p in list(self._port_forward_procs):
            self.unforward_port(p)
