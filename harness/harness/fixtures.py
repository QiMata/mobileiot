from __future__ import annotations

from pathlib import Path

import pytest

from harness.inventory import Inventory
from harness.inventory.model import DeviceKind, DeviceUnavailable
from harness.reporting.artifacts import ArtifactSink
from harness.secrets import Secrets
from harness.transports import transport_for
from harness.app.builder import AppBuilder
from harness.app.android_driver import AndroidAppDriver
from harness.app.ios_driver import IosAppDriver
from harness.app.windows_driver import WindowsAppDriver


def _repo_root() -> Path:
    here = Path(__file__).resolve()
    for parent in [here, *here.parents]:
        if (parent / "src" / "MobileIoT").exists() and (parent / "harness").exists():
            return parent
    return here.parents[2]


@pytest.fixture(scope="session")
def repo_root() -> Path:
    return _repo_root()


@pytest.fixture(scope="session")
def inventory(repo_root) -> Inventory:
    inv = Inventory.from_repo(repo_root).probe()
    return inv


@pytest.fixture(scope="session")
def secrets(repo_root) -> Secrets:
    return Secrets(repo_root)


@pytest.fixture(scope="session")
def artifacts(repo_root) -> ArtifactSink:
    return ArtifactSink(repo_root)


@pytest.fixture(scope="session")
def active_app(request) -> str:
    return getattr(request, "param", request.config.getoption("--miot-app"))


@pytest.fixture(scope="session")
def app_builder(repo_root) -> AppBuilder:
    return AppBuilder(repo_root)


def _require(inventory: Inventory, role: str, kind: DeviceKind, capabilities=()):
    try:
        return inventory.require(role=role, kind=kind, capabilities=capabilities)
    except DeviceUnavailable as e:
        pytest.skip(f"[{role}] {e}: {e.reason}", allow_module_level=False)


@pytest.fixture
def android_phone(inventory):
    device = _require(inventory, "android", DeviceKind.ANDROID)
    t = transport_for(device)
    try:
        yield device, t
    finally:
        t.close()


@pytest.fixture
def android_phones(inventory):
    devs = inventory.find(kind=DeviceKind.ANDROID, online_only=True)
    if len(devs) < 2:
        pytest.skip(f"test requires 2+ Android phones online (found {len(devs)})")
    transports = [transport_for(d) for d in devs]
    try:
        yield list(zip(devs, transports))
    finally:
        for t in transports:
            t.close()


@pytest.fixture
def ios_phone(inventory):
    device = _require(inventory, "ios", DeviceKind.IOS)
    t = transport_for(device)
    try:
        yield device, t
    finally:
        t.close()


@pytest.fixture
def windows_local(inventory):
    device = _require(inventory, "windows", DeviceKind.WINDOWS)
    t = transport_for(device)
    try:
        yield device, t
    finally:
        t.close()


@pytest.fixture
def android_app_driver(android_phone, app_builder, active_app):
    _, transport = android_phone
    driver = AndroidAppDriver(transport, app_builder, active_app)
    try:
        yield driver
    finally:
        driver.stop()


@pytest.fixture
def ios_app_driver(ios_phone, app_builder, active_app):
    _, transport = ios_phone
    driver = IosAppDriver(transport, app_builder, active_app)
    try:
        yield driver
    finally:
        driver.stop()


@pytest.fixture
def uno_windows_app_driver(windows_local, app_builder, active_app):
    if active_app == "maui":
        pytest.skip("maui/windows is not a supported app/platform pair")
    _, transport = windows_local
    driver = WindowsAppDriver(transport, app_builder)
    try:
        yield driver
    finally:
        driver.stop()


@pytest.fixture
def pi(inventory):
    device = _require(inventory, "pi", DeviceKind.PI)
    t = transport_for(device)
    try:
        yield device, t
    finally:
        t.close()
