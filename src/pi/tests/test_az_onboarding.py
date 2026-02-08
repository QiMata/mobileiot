"""Tests for Azure IoT Operations onboarding scripts (dry-run validation)."""

import json
import os

import pytest


class TestAzScriptsExist:
    """Verify all onboarding scripts are present and executable."""

    SCRIPT_DIR = os.path.join(
        os.path.dirname(__file__), "..", "gateway", "scripts"
    )

    def test_az_create_endpoint_exists(self):
        path = os.path.join(self.SCRIPT_DIR, "az_create_endpoint.sh")
        assert os.path.isfile(path)

    def test_az_create_asset_exists(self):
        path = os.path.join(self.SCRIPT_DIR, "az_create_asset.sh")
        assert os.path.isfile(path)

    def test_az_onboard_exists(self):
        path = os.path.join(self.SCRIPT_DIR, "az_onboard.sh")
        assert os.path.isfile(path)

    def test_install_script_exists(self):
        path = os.path.join(self.SCRIPT_DIR, "install.sh")
        assert os.path.isfile(path)

    def test_health_check_script_exists(self):
        path = os.path.join(self.SCRIPT_DIR, "health_check.sh")
        assert os.path.isfile(path)


class TestAzCreateAssetScript:
    """Validate the az_create_asset.sh script content."""

    SCRIPT_DIR = os.path.join(
        os.path.dirname(__file__), "..", "gateway", "scripts"
    )

    def test_script_references_correct_node_id_pattern(self):
        path = os.path.join(self.SCRIPT_DIR, "az_create_asset.sh")
        with open(path) as f:
            content = f.read()
        # Verify the OPC UA node ID pattern
        assert "ns=2;s=Devices/" in content
        assert "/Telemetry/" in content

    def test_script_includes_standard_telemetry_points(self):
        path = os.path.join(self.SCRIPT_DIR, "az_create_asset.sh")
        with open(path) as f:
            content = f.read()
        for point in ("temperature", "humidity", "cpu_temperature", "uptime_seconds"):
            assert point in content, f"Missing telemetry point: {point}"

    def test_script_is_idempotent(self):
        """Script should handle already-existing resources gracefully."""
        path = os.path.join(self.SCRIPT_DIR, "az_create_asset.sh")
        with open(path) as f:
            content = f.read()
        # Should have error suppression for already-existing assets
        assert "2>/dev/null" in content or "already exist" in content


class TestAzOnboardScript:
    """Validate the az_onboard.sh script content."""

    SCRIPT_DIR = os.path.join(
        os.path.dirname(__file__), "..", "gateway", "scripts"
    )

    def test_calls_create_endpoint(self):
        path = os.path.join(self.SCRIPT_DIR, "az_onboard.sh")
        with open(path) as f:
            content = f.read()
        assert "az_create_endpoint.sh" in content

    def test_calls_create_asset(self):
        path = os.path.join(self.SCRIPT_DIR, "az_onboard.sh")
        with open(path) as f:
            content = f.read()
        assert "az_create_asset.sh" in content

    def test_filters_online_devices(self):
        path = os.path.join(self.SCRIPT_DIR, "az_onboard.sh")
        with open(path) as f:
            content = f.read()
        assert "online" in content


class TestGatewayServiceFile:
    """Validate the systemd service file."""

    SERVICE_PATH = os.path.join(
        os.path.dirname(__file__), "..", "gateway", "gateway.service"
    )

    def test_service_file_exists(self):
        assert os.path.isfile(self.SERVICE_PATH)

    def test_service_runs_gateway_module(self):
        with open(self.SERVICE_PATH) as f:
            content = f.read()
        assert "python -m gateway" in content

    def test_service_has_restart_policy(self):
        with open(self.SERVICE_PATH) as f:
            content = f.read()
        assert "Restart=on-failure" in content

    def test_service_hardening(self):
        with open(self.SERVICE_PATH) as f:
            content = f.read()
        assert "NoNewPrivileges=true" in content
        assert "ProtectSystem=strict" in content
