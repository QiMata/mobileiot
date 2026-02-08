"""Shared test fixtures for the gateway test suite."""

from __future__ import annotations

import os
import sys

import pytest

# Ensure the gateway package is importable from the tests directory.
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))


@pytest.fixture
def tmp_registry_path(tmp_path):
    return str(tmp_path / "registry.json")


@pytest.fixture
def default_gateway_config(tmp_path):
    from gateway.config import GatewayConfig, HealthConfig, OpcUaConfig, RegistryConfig

    return GatewayConfig(
        opcua=OpcUaConfig(
            endpoint="opc.tcp://127.0.0.1:48400",
            security_mode="None",
            anonymous_allowed=True,
        ),
        registry=RegistryConfig(
            persist_path=str(tmp_path / "registry.json"),
            heartbeat_timeout_s=10.0,
            offline_after_s=20.0,
        ),
        health=HealthConfig(enabled=False),
    )
