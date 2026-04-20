from __future__ import annotations

from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Iterator


class TransportError(RuntimeError):
    pass


@dataclass
class TransportResult:
    returncode: int
    stdout: str
    stderr: str

    @property
    def ok(self) -> bool:
        return self.returncode == 0


class Transport(ABC):
    """Abstraction over how the harness reaches a device (adb, usbmux, ssh, local)."""

    kind: str = "abstract"

    @abstractmethod
    def shell(self, cmd: str | list[str], *, timeout: float = 30.0) -> TransportResult: ...

    @abstractmethod
    def push(self, local: str, remote: str) -> None: ...

    @abstractmethod
    def pull(self, remote: str, local: str) -> None: ...

    @abstractmethod
    def forward_port(self, local_port: int, remote_port: int) -> None: ...

    @abstractmethod
    def unforward_port(self, local_port: int) -> None: ...

    def stream_logs(self) -> Iterator[str]:
        """Default: no streaming. Concrete transports override with adb logcat, idevicesyslog, journalctl, etc."""
        return iter(())

    def close(self) -> None:
        pass

    def __enter__(self) -> "Transport":
        return self

    def __exit__(self, *exc) -> None:
        self.close()
