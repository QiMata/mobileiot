from __future__ import annotations

import sys
from dataclasses import dataclass, field
from importlib import metadata
from typing import Callable, Optional


@dataclass
class HardwareRoles:
    """Declares which device kinds (phone/peer) the hardware-tier tests for this integration need.

    Keys are free-form role names used inside tests (e.g. "phone_a", "phone_b", "pi").
    Values are device kinds ("android", "ios", "pi").
    """
    roles: dict[str, str] = field(default_factory=dict)
    apps: dict[str, set[str]] = field(default_factory=dict)


@dataclass
class IntegrationPlugin:
    """Declarative metadata for one integration. No test logic here.

    Discovery: plugins register via the `mobileiot_harness.integrations` entry point in pyproject.toml.
    Tests themselves live under `src/pi/tests/`, `src/MobileIoT/QiMata.MobileIoT.Tests/`, or
    `harness/tests/integrations/`, keyed by this plugin's `name`.
    """
    name: str
    display_name: str
    required_capabilities: set[str] = field(default_factory=set)
    hardware_roles: HardwareRoles = field(default_factory=HardwareRoles)
    mock_service_factory_ref: Optional[str] = None
    description: str = ""
    tags: set[str] = field(default_factory=set)

    def eligible(self, inventory, app: str | None = None) -> tuple[bool, str]:
        """Return (eligible_for_hardware_tier, reason_if_not)."""
        from harness.inventory.model import DeviceKind
        missing: list[str] = []
        for role, kind in self.hardware_roles.roles.items():
            if app is not None:
                supported_apps = self.hardware_roles.apps.get(role, {"maui", "uno"})
                if app not in supported_apps:
                    missing.append(f"{role}: app '{app}' not supported")
                    continue
            try:
                kind_enum = DeviceKind(kind)
            except ValueError:
                missing.append(f"{role}: unknown kind '{kind}'")
                continue
            matches = inventory.find(kind=kind_enum, capabilities=self.required_capabilities, online_only=True)
            if not matches:
                reason = inventory._unavailable_reasons(kind_enum, self.required_capabilities)
                missing.append(f"{role}: {reason}")
        if missing:
            return False, "; ".join(missing)
        return True, ""


def load_all_plugins() -> list[IntegrationPlugin]:
    """Discover every registered IntegrationPlugin via entry points."""
    plugins: list[IntegrationPlugin] = []
    try:
        eps = metadata.entry_points(group="mobileiot_harness.integrations")
    except TypeError:
        eps = metadata.entry_points().get("mobileiot_harness.integrations", [])
    for ep in eps:
        obj = ep.load()
        if callable(obj):
            obj = obj()
        if isinstance(obj, IntegrationPlugin):
            plugins.append(obj)
    return plugins
