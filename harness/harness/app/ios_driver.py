from __future__ import annotations

import shutil
import subprocess
import sys

from harness.app.builder import AppBuilder, AppName, BuildArtifact
from harness.app.driver import AppDriver, AppDriverError
from harness.app.hooks import HOOK_PORT, HarnessHttpClient
from harness.transports.idevice import IosTransport


class IosAppDriver(AppDriver):
    def __init__(self, transport: IosTransport, builder: AppBuilder, app: AppName = "maui"):
        if sys.platform != "darwin":
            raise AppDriverError("ios app driver requires macOS")
        self.transport = transport
        self.builder = builder
        self.app = app
        self._artifact: BuildArtifact | None = None
        self._hook_port: int | None = None

    def ensure_installed(self, *, force_reinstall: bool = False) -> None:
        if shutil.which("pymobiledevice3") is None:
            raise AppDriverError("pymobiledevice3 not installed")
        if self._artifact is None:
            self._artifact = self.builder.build(self.app, "ios")
        r = subprocess.run(
            ["pymobiledevice3", "apps", "install", str(self._artifact.path), "--udid", self.transport.device.udid],
            capture_output=True, text=True, check=False, timeout=300.0,
        )
        if r.returncode != 0:
            raise AppDriverError(f"ios install failed: {r.stderr or r.stdout}")

    def launch(self, scenario: str | None = None, extras: dict[str, str] | None = None) -> None:
        bundle_id = self._artifact.package_id if self._artifact else AppBuilder.spec(self.app, "ios").package_id
        args = ["pymobiledevice3", "apps", "launch", bundle_id, "--udid", self.transport.device.udid]
        env_extras = {"MIOT_TEST_MODE": "1"}
        if scenario:
            env_extras["MIOT_SCENARIO"] = scenario
        env_extras.update(extras or {})
        for k, v in env_extras.items():
            args.extend(["--env", f"{k}={v}"])
        r = subprocess.run(args, capture_output=True, text=True, check=False, timeout=30.0)
        if r.returncode != 0:
            raise AppDriverError(f"ios launch failed: {r.stderr or r.stdout}")
        self.transport.forward_port(HOOK_PORT, HOOK_PORT)
        self._hook_port = HOOK_PORT

    def stop(self) -> None:
        try:
            bundle_id = self._artifact.package_id if self._artifact else AppBuilder.spec(self.app, "ios").package_id
            subprocess.run(
                ["pymobiledevice3", "apps", "kill", bundle_id, "--udid", self.transport.device.udid],
                capture_output=True, text=True, check=False, timeout=10.0,
            )
        finally:
            if self._hook_port is not None:
                self.transport.unforward_port(self._hook_port)
                self._hook_port = None

    def hook(self) -> HarnessHttpClient:
        if self._hook_port is None:
            raise AppDriverError("app not launched yet")
        return HarnessHttpClient(self._hook_port)
