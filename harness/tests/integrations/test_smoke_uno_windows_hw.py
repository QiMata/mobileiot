from __future__ import annotations

import pytest


@pytest.mark.hardware(roles=["windows"], capabilities=[])
def test_smoke_uno_windows_health(uno_windows_app_driver):
    uno_windows_app_driver.ensure_installed()
    uno_windows_app_driver.launch()
    hook = uno_windows_app_driver.hook()
    hook.wait_ready(timeout=30.0)
    assert hook.health()["ok"] is True
