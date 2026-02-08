"""YAML-backed configuration with strict validation."""

from __future__ import annotations

import os
from dataclasses import dataclass, field, fields
from typing import Any

import yaml


@dataclass
class CharacteristicConfig:
    uuid: str = ""
    name: str = ""
    datatype: str = "Int16"
    scale: float = 0.01
    unit: str = ""


@dataclass
class BleAdapterConfig:
    enabled: bool = True
    scan_interval_s: float = 10.0
    scan_duration_s: float = 5.0
    service_filter: list[str] = field(
        default_factory=lambda: ["12345678-1234-1234-1234-1234567890AB"]
    )
    name_filter: list[str] = field(default_factory=lambda: ["PiSensor"])
    read_interval_s: float = 2.0
    characteristics: list[CharacteristicConfig] = field(default_factory=list)
    write_allowlist: list[str] = field(
        default_factory=lambda: ["12345679-1234-1234-1234-1234567890AB"]
    )


@dataclass
class SerialAdapterConfig:
    enabled: bool = True
    port_patterns: list[str] = field(
        default_factory=lambda: ["/dev/ttyUSB*", "/dev/ttyACM*", "/dev/ttyGS*"]
    )
    baud_rate: int = 9600
    identify_timeout_s: float = 3.0
    poll_interval_s: float = 5.0
    commands: list[str] = field(
        default_factory=lambda: ["SENSOR_READ", "STATUS"]
    )
    write_allowlist: list[str] = field(
        default_factory=lambda: ["LED_ON", "LED_OFF", "GPIO_WRITE"]
    )


@dataclass
class WifiDirectAdapterConfig:
    enabled: bool = True
    listen_host: str = "127.0.0.1"
    listen_port: int = 8988
    hello_timeout_s: float = 10.0
    heartbeat_interval_s: float = 30.0
    telemetry_request_on_connect: bool = True
    reconnect_delay_s: float = 10.0


@dataclass
class OpcUaConfig:
    endpoint: str = "opc.tcp://0.0.0.0:4840"
    server_name: str = "MobileIoT Gateway"
    namespace_uri: str = "urn:qimata:mobileiot:gateway"
    security_policy: str = "Basic256Sha256"
    security_mode: str = "SignAndEncrypt"
    certificate_path: str = "/etc/mobileiot/opcua/certs/server_cert.pem"
    private_key_path: str = "/etc/mobileiot/opcua/private/server_key.pem"
    trust_path: str = "/etc/mobileiot/opcua/trust/"
    anonymous_allowed: bool = False


@dataclass
class RegistryConfig:
    heartbeat_timeout_s: float = 60.0
    offline_after_s: float = 120.0
    persist_path: str = "/var/lib/mobileiot/registry.json"


@dataclass
class HealthConfig:
    enabled: bool = True
    host: str = "0.0.0.0"
    port: int = 8081


@dataclass
class GatewayConfig:
    ble: BleAdapterConfig = field(default_factory=BleAdapterConfig)
    serial: SerialAdapterConfig = field(default_factory=SerialAdapterConfig)
    wifi_direct: WifiDirectAdapterConfig = field(default_factory=WifiDirectAdapterConfig)
    opcua: OpcUaConfig = field(default_factory=OpcUaConfig)
    registry: RegistryConfig = field(default_factory=RegistryConfig)
    health: HealthConfig = field(default_factory=HealthConfig)
    log_level: str = "INFO"


# Mapping of config section names to their dataclass types.
_SECTION_TYPES: dict[str, type] = {
    "ble": BleAdapterConfig,
    "serial": SerialAdapterConfig,
    "wifi_direct": WifiDirectAdapterConfig,
    "opcua": OpcUaConfig,
    "registry": RegistryConfig,
    "health": HealthConfig,
}


def load_config(path: str | None = None) -> GatewayConfig:
    """Load and validate gateway configuration from YAML.

    Falls back to all defaults if the file does not exist.
    """
    path = path or os.environ.get("GATEWAY_CONFIG", "gateway.yaml")
    if not os.path.isfile(path):
        return GatewayConfig()

    with open(path) as f:
        raw = yaml.safe_load(f) or {}

    config = _build_config(raw)
    validate(config)
    return config


def _build_config(raw: dict[str, Any]) -> GatewayConfig:
    """Recursively construct GatewayConfig from a raw YAML dict."""
    kwargs: dict[str, Any] = {}

    for key, value in raw.items():
        if key in _SECTION_TYPES and isinstance(value, dict):
            kwargs[key] = _build_dataclass(_SECTION_TYPES[key], value)
        else:
            kwargs[key] = value

    return GatewayConfig(**{k: v for k, v in kwargs.items() if k in {f.name for f in fields(GatewayConfig)}})


def _build_dataclass(cls: type, raw: dict[str, Any]) -> Any:
    """Build a dataclass instance from a dict, ignoring unknown keys."""
    valid_names = {f.name for f in fields(cls)}
    return cls(**{k: v for k, v in raw.items() if k in valid_names})


def validate(config: GatewayConfig) -> None:
    """Validate configuration values. Raises ``ValueError`` on invalid config."""
    if config.ble.scan_interval_s < 1.0:
        raise ValueError("ble.scan_interval_s must be >= 1.0")

    if config.ble.scan_duration_s < 0.5:
        raise ValueError("ble.scan_duration_s must be >= 0.5")

    if config.ble.read_interval_s < 0.5:
        raise ValueError("ble.read_interval_s must be >= 0.5")

    if config.serial.baud_rate not in (9600, 19200, 38400, 57600, 115200):
        raise ValueError(f"Unsupported serial baud rate: {config.serial.baud_rate}")

    if config.serial.poll_interval_s < 1.0:
        raise ValueError("serial.poll_interval_s must be >= 1.0")

    if config.opcua.security_mode not in ("None", "Sign", "SignAndEncrypt"):
        raise ValueError(f"Invalid OPC UA security mode: {config.opcua.security_mode}")

    if not config.opcua.anonymous_allowed and config.opcua.security_mode == "None":
        raise ValueError(
            "OPC UA security_mode 'None' with anonymous disabled is insecure "
            "and not supported"
        )

    if config.registry.heartbeat_timeout_s < 5.0:
        raise ValueError("registry.heartbeat_timeout_s must be >= 5.0")

    if config.registry.offline_after_s <= config.registry.heartbeat_timeout_s:
        raise ValueError(
            "registry.offline_after_s must be greater than heartbeat_timeout_s"
        )

    valid_levels = {"DEBUG", "INFO", "WARNING", "ERROR", "CRITICAL"}
    if config.log_level.upper() not in valid_levels:
        raise ValueError(f"Invalid log_level: {config.log_level}")
