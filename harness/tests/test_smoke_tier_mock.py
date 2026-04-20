"""Mock-tier smoke test — runs with zero hardware, proves the harness self-tests framework works."""
from __future__ import annotations

import pytest


@pytest.mark.mock
def test_harness_importable():
    import harness.cli  # noqa: F401
    import harness.inventory  # noqa: F401
    import harness.transports  # noqa: F401


@pytest.mark.mock
def test_cli_app_exists():
    from harness.cli import app
    assert app is not None
