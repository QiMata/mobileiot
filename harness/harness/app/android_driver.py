from __future__ import annotations

from harness.app.builder import AppBuilder, AppName, BuildArtifact
from harness.app.driver import AppDriver, AppDriverError
from harness.app.hooks import HOOK_PORT, HarnessHttpClient
from harness.transports.adb import AndroidTransport


class AndroidAppDriver(AppDriver):
    def __init__(self, transport: AndroidTransport, builder: AppBuilder, app: AppName = "maui"):
        self.transport = transport
        self.builder = builder
        self.app = app
        self._artifact: BuildArtifact | None = None
        self._hook_port: int | None = None

    def ensure_installed(self, *, force_reinstall: bool = False) -> None:
        if self._artifact is None:
            self._artifact = self.builder.build(self.app, "android")
        if force_reinstall or not self._installed():
            self.transport.install_apk(str(self._artifact.path))

    def _installed(self) -> bool:
        package_id = self._artifact.package_id if self._artifact else AppBuilder.spec(self.app, "android").package_id
        r = self.transport.shell(["pm", "path", package_id])
        return r.ok and "package:" in r.stdout

    def launch(self, scenario: str | None = None, extras: dict[str, str] | None = None) -> None:
        e = dict(extras or {})
        e["MIOT_TEST_MODE"] = "1"
        if scenario:
            e["MIOT_SCENARIO"] = scenario
        package_id = self._artifact.package_id if self._artifact else AppBuilder.spec(self.app, "android").package_id
        self.transport.launch_activity(package_id, extras=e)
        port = HOOK_PORT
        self.transport.forward_port(port, HOOK_PORT)
        self._hook_port = port

    def stop(self) -> None:
        try:
            package_id = self._artifact.package_id if self._artifact else AppBuilder.spec(self.app, "android").package_id
            self.transport.shell(["am", "force-stop", package_id])
        finally:
            if self._hook_port is not None:
                self.transport.unforward_port(self._hook_port)
                self._hook_port = None

    def hook(self) -> HarnessHttpClient:
        if self._hook_port is None:
            raise AppDriverError("app not launched yet; call launch() first")
        return HarnessHttpClient(self._hook_port)
