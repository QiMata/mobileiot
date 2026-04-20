from __future__ import annotations

import shutil
import socket
import subprocess
import sys
from typing import Iterable

from harness.inventory.model import Device, DeviceKind


def _run(cmd: list[str], timeout: float = 5.0) -> tuple[int, str]:
    try:
        proc = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            timeout=timeout,
            check=False,
        )
        return proc.returncode, (proc.stdout or "") + (proc.stderr or "")
    except (FileNotFoundError, subprocess.TimeoutExpired) as e:
        return 127, str(e)


def list_adb_serials() -> set[str]:
    if shutil.which("adb") is None:
        return set()
    rc, out = _run(["adb", "devices"])
    if rc != 0:
        return set()
    serials: set[str] = set()
    for line in out.splitlines()[1:]:
        parts = line.split()
        if len(parts) >= 2 and parts[1] == "device":
            serials.add(parts[0])
    return serials


def list_ios_udids() -> set[str]:
    """List iOS devices. Returns empty set on non-Mac hosts or when tools unavailable."""
    if sys.platform != "darwin":
        return set()
    for cmd in (["pymobiledevice3", "usbmux", "list", "--no-color"], ["idevice_id", "-l"]):
        if shutil.which(cmd[0]) is None:
            continue
        rc, out = _run(cmd)
        if rc != 0:
            continue
        udids: set[str] = set()
        for line in out.splitlines():
            line = line.strip()
            if not line or line.startswith("["):
                continue
            token = line.split()[0].strip('",:')
            if len(token) >= 24 and all(c in "0123456789abcdefABCDEF-" for c in token):
                udids.add(token)
        if udids:
            return udids
    return set()


def probe_pi_ssh(ip: str, port: int = 22, timeout: float = 1.5) -> bool:
    try:
        with socket.create_connection((ip, port), timeout=timeout):
            return True
    except OSError:
        return False


def probe_live(devices: Iterable[Device]) -> list[Device]:
    """Mutates and returns the same device objects with online/reason_offline set."""
    adb_serials = list_adb_serials()
    ios_udids = list_ios_udids()
    ios_available = sys.platform == "darwin"

    for d in devices:
        if d.kind == DeviceKind.ANDROID:
            if not d.adb_serial:
                d.online, d.reason_offline = False, "device has no adb_serial configured"
            elif d.adb_serial in adb_serials:
                d.online, d.reason_offline = True, None
            else:
                d.online, d.reason_offline = False, f"adb serial {d.adb_serial} not detected"
        elif d.kind == DeviceKind.IOS:
            if not ios_available:
                d.online, d.reason_offline = False, "ios-requires-macos"
            elif not d.udid:
                d.online, d.reason_offline = False, "device has no udid configured"
            elif d.udid in ios_udids:
                d.online, d.reason_offline = True, None
            else:
                d.online, d.reason_offline = False, f"udid {d.udid} not detected via usbmux"
        elif d.kind == DeviceKind.PI:
            if not d.usb_gadget_ip:
                d.online, d.reason_offline = False, "pi has no usb_gadget_ip configured"
            elif probe_pi_ssh(d.usb_gadget_ip):
                d.online, d.reason_offline = True, None
            else:
                d.online, d.reason_offline = False, f"ssh {d.usb_gadget_ip}:22 unreachable"
        elif d.kind == DeviceKind.LOCAL:
            d.online, d.reason_offline = True, None
        elif d.kind == DeviceKind.WINDOWS:
            if sys.platform == "win32":
                d.online, d.reason_offline = True, None
            else:
                d.online, d.reason_offline = False, "windows-requires-windows-host"
    return list(devices)
