from __future__ import annotations

import shutil
import subprocess
from pathlib import Path

from harness.transports.base import Transport, TransportResult


class LocalHostTransport(Transport):
    kind = "local"

    def shell(self, cmd, *, timeout: float = 30.0) -> TransportResult:
        if isinstance(cmd, list):
            proc = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout, check=False)
        else:
            proc = subprocess.run(cmd, shell=True, capture_output=True, text=True, timeout=timeout, check=False)
        return TransportResult(proc.returncode, proc.stdout or "", proc.stderr or "")

    def push(self, local: str, remote: str) -> None:
        shutil.copy2(local, remote)

    def pull(self, remote: str, local: str) -> None:
        shutil.copy2(remote, local)

    def forward_port(self, local_port: int, remote_port: int) -> None:
        if local_port != remote_port:
            raise NotImplementedError("local transport cannot remap ports")

    def unforward_port(self, local_port: int) -> None:
        return
