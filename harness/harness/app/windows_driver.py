from __future__ import annotations

from harness.app.builder import AppBuilder, BuildArtifact
from harness.app.driver import AppDriver, AppDriverError
from harness.app.hooks import HOOK_PORT, HarnessHttpClient
from harness.transports.windows_local import WindowsLocalTransport


class WindowsAppDriver(AppDriver):
    def __init__(self, transport: WindowsLocalTransport, builder: AppBuilder):
        self.transport = transport
        self.builder = builder
        self._artifact: BuildArtifact | None = None
        self._hook_port: int | None = None

    def ensure_installed(self, *, force_reinstall: bool = False) -> None:
        if self._artifact is None or force_reinstall:
            self._artifact = self.builder.build("uno", "windows", force=force_reinstall)
        self.transport.install(self._artifact)

    def launch(self, scenario: str | None = None, extras: dict[str, str] | None = None) -> None:
        env = dict(extras or {})
        env["MIOT_TEST_MODE"] = "1"
        if scenario:
            env["MIOT_SCENARIO"] = scenario
        self.transport.launch(env=env)
        self.transport.forward_port(HOOK_PORT, HOOK_PORT)
        self._hook_port = HOOK_PORT

    def stop(self) -> None:
        try:
            self.transport.stop()
        finally:
            if self._hook_port is not None:
                self.transport.unforward_port(self._hook_port)
                self._hook_port = None

    def hook(self) -> HarnessHttpClient:
        if self._hook_port is None:
            raise AppDriverError("app not launched yet")
        return HarnessHttpClient(self._hook_port)
