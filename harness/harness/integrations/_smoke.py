"""Smoke integration — exercises the harness framework without touching any real protocol.

Used by `python -m harness run --integration _smoke` to validate that device discovery,
app build, install, launch, port-forwarding, and hook health-check all work end-to-end.
"""
from harness.integrations.base import HardwareRoles, IntegrationPlugin


plugin = IntegrationPlugin(
    name="_smoke",
    display_name="Harness Smoke",
    required_capabilities=set(),
    hardware_roles=HardwareRoles(),
    description="Validates the harness skeleton itself: build, install, launch, /health round-trip.",
    tags={"internal"},
)
