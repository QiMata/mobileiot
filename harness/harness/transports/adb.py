from __future__ import annotations

import shutil
import subprocess

from harness.inventory.model import Device
from harness.transports.base import Transport, TransportError, TransportResult


class AndroidTransport(Transport):
    kind = "adb"

    def __init__(self, device: Device):
        if not device.adb_serial:
            raise TransportError(f"device {device.id} has no adb_serial")
        if shutil.which("adb") is None:
            raise TransportError("`adb` not found on PATH (install Android Platform Tools)")
        self.device = device
        self._forwarded: set[int] = set()

    def _adb(self, *args: str, timeout: float = 30.0) -> TransportResult:
        cmd = ["adb", "-s", self.device.adb_serial, *args]
        proc = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout, check=False)
        return TransportResult(proc.returncode, proc.stdout or "", proc.stderr or "")

    def shell(self, cmd, *, timeout: float = 30.0) -> TransportResult:
        if isinstance(cmd, list):
            return self._adb("shell", *cmd, timeout=timeout)
        return self._adb("shell", cmd, timeout=timeout)

    def push(self, local: str, remote: str) -> None:
        r = self._adb("push", local, remote, timeout=120.0)
        if not r.ok:
            raise TransportError(f"adb push failed: {r.stderr}")

    def pull(self, remote: str, local: str) -> None:
        r = self._adb("pull", remote, local, timeout=120.0)
        if not r.ok:
            raise TransportError(f"adb pull failed: {r.stderr}")

    def forward_port(self, local_port: int, remote_port: int) -> None:
        r = self._adb("forward", f"tcp:{local_port}", f"tcp:{remote_port}")
        if not r.ok:
            raise TransportError(f"adb forward failed: {r.stderr}")
        self._forwarded.add(local_port)

    def unforward_port(self, local_port: int) -> None:
        self._adb("forward", "--remove", f"tcp:{local_port}")
        self._forwarded.discard(local_port)

    def install_apk(self, apk_path: str, *, reinstall: bool = True) -> None:
        args = ["install", "-r", apk_path] if reinstall else ["install", apk_path]
        r = self._adb(*args, timeout=300.0)
        if not r.ok:
            raise TransportError(f"adb install failed: {r.stderr or r.stdout}")

    def launch_activity(self, package: str, activity: str | None = None, extras: dict[str, str] | None = None) -> None:
        target = f"{package}/{activity}" if activity else package
        cmd = ["am", "start", "-n" if activity else "-p", target]
        for k, v in (extras or {}).items():
            cmd.extend(["--es", k, v])
        r = self.shell(cmd)
        if not r.ok:
            raise TransportError(f"am start failed: {r.stderr or r.stdout}")

    def stream_logs(self):
        proc = subprocess.Popen(
            ["adb", "-s", self.device.adb_serial, "logcat", "-v", "threadtime"],
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            bufsize=1,
        )
        try:
            assert proc.stdout is not None
            for line in proc.stdout:
                yield line.rstrip("\n")
        finally:
            proc.terminate()

    def close(self) -> None:
        for p in list(self._forwarded):
            self.unforward_port(p)
