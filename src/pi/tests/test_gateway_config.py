"""Tests for gateway.config — validation and YAML loading."""

import pytest

from gateway.config import (
    BleAdapterConfig,
    GatewayConfig,
    OpcUaConfig,
    RegistryConfig,
    SerialAdapterConfig,
    load_config,
    validate,
)


class TestConfigDefaults:
    def test_default_config_is_valid(self):
        config = GatewayConfig()
        validate(config)

    def test_default_opcua_endpoint(self):
        config = GatewayConfig()
        assert config.opcua.endpoint == "opc.tcp://0.0.0.0:4840"

    def test_default_ble_service_filter(self):
        config = GatewayConfig()
        assert "12345678-1234-1234-1234-1234567890AB" in config.ble.service_filter

    def test_default_serial_allowlist(self):
        config = GatewayConfig()
        assert "LED_ON" in config.serial.write_allowlist


class TestConfigValidation:
    def test_invalid_baud_rate_raises(self):
        config = GatewayConfig()
        config.serial.baud_rate = 12345
        with pytest.raises(ValueError, match="baud rate"):
            validate(config)

    def test_insecure_config_raises(self):
        config = GatewayConfig()
        config.opcua.security_mode = "None"
        config.opcua.anonymous_allowed = False
        with pytest.raises(ValueError, match="insecure"):
            validate(config)

    def test_scan_interval_too_low_raises(self):
        config = GatewayConfig()
        config.ble.scan_interval_s = 0.1
        with pytest.raises(ValueError, match="scan_interval_s"):
            validate(config)

    def test_scan_duration_too_low_raises(self):
        config = GatewayConfig()
        config.ble.scan_duration_s = 0.1
        with pytest.raises(ValueError, match="scan_duration_s"):
            validate(config)

    def test_read_interval_too_low_raises(self):
        config = GatewayConfig()
        config.ble.read_interval_s = 0.1
        with pytest.raises(ValueError, match="read_interval_s"):
            validate(config)

    def test_poll_interval_too_low_raises(self):
        config = GatewayConfig()
        config.serial.poll_interval_s = 0.1
        with pytest.raises(ValueError, match="poll_interval_s"):
            validate(config)

    def test_invalid_security_mode_raises(self):
        config = GatewayConfig()
        config.opcua.security_mode = "FooBar"
        with pytest.raises(ValueError, match="security mode"):
            validate(config)

    def test_heartbeat_too_low_raises(self):
        config = GatewayConfig()
        config.registry.heartbeat_timeout_s = 1.0
        with pytest.raises(ValueError, match="heartbeat_timeout_s"):
            validate(config)

    def test_offline_must_exceed_heartbeat(self):
        config = GatewayConfig()
        config.registry.heartbeat_timeout_s = 60.0
        config.registry.offline_after_s = 30.0
        with pytest.raises(ValueError, match="offline_after_s"):
            validate(config)

    def test_invalid_log_level_raises(self):
        config = GatewayConfig()
        config.log_level = "VERBOSE"
        with pytest.raises(ValueError, match="log_level"):
            validate(config)

    def test_valid_security_none_with_anonymous(self):
        config = GatewayConfig()
        config.opcua.security_mode = "None"
        config.opcua.anonymous_allowed = True
        validate(config)  # should not raise


class TestConfigLoading:
    def test_load_missing_file_returns_defaults(self, tmp_path):
        config = load_config(str(tmp_path / "nonexistent.yaml"))
        assert config.opcua.endpoint == "opc.tcp://0.0.0.0:4840"

    def test_load_yaml_overrides_defaults(self, tmp_path):
        yaml_path = tmp_path / "test.yaml"
        yaml_path.write_text(
            "opcua:\n  endpoint: opc.tcp://0.0.0.0:5840\n"
        )
        config = load_config(str(yaml_path))
        assert config.opcua.endpoint == "opc.tcp://0.0.0.0:5840"

    def test_load_yaml_preserves_non_overridden(self, tmp_path):
        yaml_path = tmp_path / "test.yaml"
        yaml_path.write_text("log_level: DEBUG\n")
        config = load_config(str(yaml_path))
        assert config.log_level == "DEBUG"
        assert config.opcua.endpoint == "opc.tcp://0.0.0.0:4840"

    def test_load_yaml_nested_section(self, tmp_path):
        yaml_path = tmp_path / "test.yaml"
        yaml_path.write_text(
            "ble:\n  scan_interval_s: 30.0\n  name_filter:\n    - MyDevice\n"
        )
        config = load_config(str(yaml_path))
        assert config.ble.scan_interval_s == 30.0
        assert config.ble.name_filter == ["MyDevice"]

    def test_load_empty_yaml_returns_defaults(self, tmp_path):
        yaml_path = tmp_path / "empty.yaml"
        yaml_path.write_text("")
        config = load_config(str(yaml_path))
        assert config.opcua.endpoint == "opc.tcp://0.0.0.0:4840"
