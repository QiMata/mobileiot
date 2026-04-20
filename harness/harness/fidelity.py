from __future__ import annotations

from enum import Enum
from typing import Iterable

import pytest


class FidelityTier(str, Enum):
    MOCK = "mock"
    HARDWARE = "hardware"


def requires_hardware(
    *,
    roles: Iterable[str],
    capabilities: Iterable[str] = (),
):
    """Decorator/marker that skips a test when the live inventory cannot satisfy the roles.

    `roles` is a list of device-kind strings ("android", "ios", "pi") that MUST be online.
    `capabilities` is a flat list of tags required on at least one matched device per role.
    Skipping produces a structured reason so the JSON reporter can distinguish missing-hw from real failures.
    """
    role_list = list(roles)
    cap_list = list(capabilities)

    def _decorator(fn):
        return pytest.mark.hardware(roles=role_list, capabilities=cap_list)(fn)

    return _decorator


def evaluate_hardware_requirement(inventory, roles: list[str], capabilities: list[str]) -> tuple[bool, str]:
    """Return (ok, reason_if_skipped). Used by fixtures and by the skip hook in conftest."""
    from harness.inventory.model import DeviceKind
    missing: list[str] = []
    for role in roles:
        try:
            kind = DeviceKind(role)
        except ValueError:
            missing.append(f"{role}: unknown kind")
            continue
        matches = inventory.find(kind=kind, capabilities=capabilities, online_only=True)
        if not matches:
            reason = inventory._unavailable_reasons(kind, capabilities)
            missing.append(f"{role}: {reason}")
    if missing:
        return False, "; ".join(missing)
    return True, ""
