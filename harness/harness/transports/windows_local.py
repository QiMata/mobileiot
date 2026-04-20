from __future__ import annotations

import os
import shutil
import subprocess
from pathlib import Path
from typing import Iterator

from harness.transports.base import Transport, TransportResult


class WindowsLocalTransport(Transport):
    kind = "windows-local"

    def __init__(self, device=None):
        self.device = device
        self._artifact = None
        self._process: subprocess.Popen[str] | None = None

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

    def install(self, artifact) -> None:
        self._artifact = artifact

    def launch(self, env: dict[str, str] | None = None) -> None:
        if self._artifact is None:
            raise RuntimeError("no artifact installed")
        cwd = Path(self._artifact.support_dir or self._artifact.path.parent)
        proc_env = {**os.environ, **(env or {})}
        args = [str(self._artifact.path)]
        if proc_env.get("MIOT_TEST_MODE"):
            args.append("--miot-test-mode")
        self._process = subprocess.Popen(
            args,
            cwd=str(cwd),
            env=proc_env,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            bufsize=1,
        )

    def stop(self) -> None:
        proc = self._process
        self._process = None
        if proc is None or proc.poll() is not None:
            return
        proc.terminate()
        try:
            proc.wait(timeout=5.0)
        except subprocess.TimeoutExpired:
            proc.kill()
            proc.wait(timeout=5.0)

    def forward_port(self, local_port: int, remote_port: int) -> None:
        if local_port != remote_port:
            raise NotImplementedError("windows-local transport cannot remap ports")

    def unforward_port(self, local_port: int) -> None:
        return

    def stream_logs(self) -> Iterator[str]:
        proc = self._process
        if proc is None or proc.stdout is None:
            return iter(())
        return (line.rstrip("\n") for line in proc.stdout)

    def close(self) -> None:
        self.stop()
