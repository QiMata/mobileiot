"""Top-level pytest config for the harness.

Imports fixtures from harness.fixtures so tests can use `android_phone`, `ios_phone`, `pi`, etc.
Also re-exports the Pi-side gateway fixtures from src/pi/tests so harness tests can compose them.
"""
from __future__ import annotations

import sys
from pathlib import Path

import pytest

from harness.fixtures import *  # noqa: F401,F403
from harness.fidelity import evaluate_hardware_requirement

_HERE = Path(__file__).resolve().parent
_REPO = _HERE.parent
_PI_TESTS = _REPO / "src" / "pi" / "tests"
_PI_ROOT = _REPO / "src" / "pi"
if _PI_TESTS.exists() and str(_PI_ROOT) not in sys.path:
    sys.path.insert(0, str(_PI_ROOT))


def pytest_addoption(parser):
    parser.addoption("--miot-app", action="store", default="maui", choices=("maui", "uno", "both"))


def pytest_generate_tests(metafunc):
    if "active_app" not in metafunc.fixturenames:
        return
    selected = metafunc.config.getoption("--miot-app")
    apps = ("maui", "uno") if selected == "both" else (selected,)
    metafunc.parametrize("active_app", apps, ids=[f"app={app}" for app in apps], scope="session")


def pytest_collection_modifyitems(config, items):
    """Auto-skip hardware-tier tests when the inventory can't satisfy their requirements."""
    inv = None
    for item in items:
        mark = item.get_closest_marker("hardware")
        if mark is None:
            continue
        if inv is None:
            from harness.inventory import Inventory
            inv = Inventory.from_repo(_REPO).probe()
        roles = mark.kwargs.get("roles", [])
        caps = mark.kwargs.get("capabilities", [])
        ok, reason = evaluate_hardware_requirement(inv, roles, caps)
        if not ok:
            item.add_marker(pytest.mark.skip(reason=f"hardware unavailable: {reason}"))


def pytest_configure(config):
    config.addinivalue_line("markers", "mock: mock-tier test (no hardware required)")
    config.addinivalue_line("markers", "hardware(roles, capabilities): requires physical devices")
