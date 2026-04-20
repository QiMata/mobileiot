from __future__ import annotations

from abc import ABC, abstractmethod

from harness.app.hooks import HarnessHttpClient


class AppDriverError(RuntimeError):
    pass


class AppDriver(ABC):
    """Contract for install/launch/stop the MAUI app on a target device and reach its test hook."""

    @abstractmethod
    def ensure_installed(self, *, force_reinstall: bool = False) -> None: ...

    @abstractmethod
    def launch(self, scenario: str | None = None, extras: dict[str, str] | None = None) -> None: ...

    @abstractmethod
    def stop(self) -> None: ...

    @abstractmethod
    def hook(self) -> HarnessHttpClient: ...

    def __enter__(self) -> "AppDriver":
        return self

    def __exit__(self, *exc) -> None:
        try:
            self.stop()
        except Exception:
            pass
