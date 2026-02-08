"""OPC UA server exposing discovered devices and telemetry via asyncua."""

from __future__ import annotations

import logging
import os

from asyncua import Server, ua

from gateway.config import OpcUaConfig
from gateway.models import (
    DeviceStatus,
    DiscoveredDevice,
    OpcUaDataType,
    TelemetryPoint,
)
from gateway.registry import DeviceRegistry

logger = logging.getLogger(__name__)

DATATYPE_MAP: dict[OpcUaDataType, ua.VariantType] = {
    OpcUaDataType.FLOAT: ua.VariantType.Float,
    OpcUaDataType.DOUBLE: ua.VariantType.Double,
    OpcUaDataType.INT16: ua.VariantType.Int16,
    OpcUaDataType.INT32: ua.VariantType.Int32,
    OpcUaDataType.STRING: ua.VariantType.String,
    OpcUaDataType.BOOLEAN: ua.VariantType.Boolean,
}


class GatewayOpcUaServer:
    """OPC UA server with dynamic per-device node management.

    NodeId convention::

        ns=<idx>;s=Devices/<device_id>/Telemetry/<point_name>
        ns=<idx>;s=Devices/<device_id>/Commands/<command_name>
        ns=<idx>;s=Devices/<device_id>/Meta/<field_name>
    """

    def __init__(self, config: OpcUaConfig, registry: DeviceRegistry) -> None:
        self._config = config
        self._registry = registry
        self._server: Server | None = None
        self._ns_idx: int = 0
        self._devices_folder: ua.Node | None = None
        self._device_nodes: dict[str, ua.NodeId] = {}
        self._telemetry_nodes: dict[str, ua.NodeId] = {}

    @property
    def is_running(self) -> bool:
        return self._server is not None

    # -- lifecycle -------------------------------------------------------

    async def start(self) -> None:
        self._server = Server()
        await self._server.init()
        self._server.set_endpoint(self._config.endpoint)
        self._server.set_server_name(self._config.server_name)

        await self._configure_security()

        self._ns_idx = await self._server.register_namespace(
            self._config.namespace_uri
        )

        objects = self._server.nodes.objects
        self._devices_folder = await objects.add_folder(
            self._ns_idx, "Devices"
        )

        # Wire up registry callbacks
        self._registry.on_device_online(self._on_device_online)
        self._registry.on_device_offline(self._on_device_offline)
        self._registry.on_telemetry(self._on_telemetry)

        await self._server.start()
        logger.info("OPC UA server started at %s", self._config.endpoint)

    async def stop(self) -> None:
        if self._server:
            await self._server.stop()
            self._server = None
            logger.info("OPC UA server stopped")

    # -- security --------------------------------------------------------

    async def _configure_security(self) -> None:
        assert self._server is not None

        if self._config.security_mode == "None":
            self._server.set_security_policy(
                [ua.SecurityPolicyType.NoSecurity]
            )
            return

        cert_path = self._config.certificate_path
        key_path = self._config.private_key_path

        if os.path.isfile(cert_path) and os.path.isfile(key_path):
            await self._server.load_certificate(cert_path)
            await self._server.load_private_key(key_path)

            if self._config.security_mode == "SignAndEncrypt":
                policy = ua.SecurityPolicyType.Basic256Sha256_SignAndEncrypt
            else:
                policy = ua.SecurityPolicyType.Basic256Sha256_Sign

            self._server.set_security_policy([policy])
            logger.info(
                "OPC UA security: %s / %s",
                self._config.security_policy,
                self._config.security_mode,
            )
        else:
            logger.warning(
                "Certificate or key not found (%s / %s). "
                "Falling back to NoSecurity.",
                cert_path,
                key_path,
            )
            self._server.set_security_policy(
                [ua.SecurityPolicyType.NoSecurity]
            )

    # -- registry callbacks ----------------------------------------------

    async def _on_device_online(self, device: DiscoveredDevice) -> None:
        """Create OPC UA nodes for a newly discovered device."""
        if device.device_id in self._device_nodes:
            # Already registered — update status node
            meta_key = f"{device.device_id}/status"
            if meta_key in self._telemetry_nodes:
                node = self._server.get_node(self._telemetry_nodes[meta_key])
                await node.write_value("online", ua.VariantType.String)
            return

        assert self._devices_folder is not None

        device_node = await self._devices_folder.add_object(
            ua.NodeId(f"Devices/{device.device_id}", self._ns_idx),
            device.display_name or device.device_id,
        )
        self._device_nodes[device.device_id] = device_node.nodeid

        # -- Meta folder --
        meta_folder = await device_node.add_folder(
            ua.NodeId(f"Devices/{device.device_id}/Meta", self._ns_idx),
            "Meta",
        )
        for field_name, value in [
            ("protocol", device.protocol.value),
            ("address", device.protocol_address),
            ("status", "online"),
            ("display_name", device.display_name),
        ]:
            nid = ua.NodeId(
                f"Devices/{device.device_id}/Meta/{field_name}", self._ns_idx
            )
            var = await meta_folder.add_variable(
                nid, field_name, value, ua.VariantType.String
            )
            self._telemetry_nodes[f"{device.device_id}/{field_name}"] = var.nodeid

        # -- Telemetry folder --
        tel_folder = await device_node.add_folder(
            ua.NodeId(
                f"Devices/{device.device_id}/Telemetry", self._ns_idx
            ),
            "Telemetry",
        )
        self._device_nodes[f"{device.device_id}/Telemetry"] = tel_folder.nodeid

        # -- Commands folder --
        cmd_folder = await device_node.add_folder(
            ua.NodeId(
                f"Devices/{device.device_id}/Commands", self._ns_idx
            ),
            "Commands",
        )
        self._device_nodes[f"{device.device_id}/Commands"] = cmd_folder.nodeid

        logger.info("OPC UA nodes created for device: %s", device.device_id)

    async def _on_device_offline(self, device: DiscoveredDevice) -> None:
        """Mark a device as offline in OPC UA."""
        meta_key = f"{device.device_id}/status"
        if meta_key in self._telemetry_nodes and self._server:
            node = self._server.get_node(self._telemetry_nodes[meta_key])
            await node.write_value("offline", ua.VariantType.String)

    async def _on_telemetry(self, point: TelemetryPoint) -> None:
        """Update or create OPC UA variable for a telemetry point."""
        if self._server is None:
            return

        key = f"{point.device_id}/{point.name}"
        variant_type = DATATYPE_MAP.get(point.datatype, ua.VariantType.Double)

        if key in self._telemetry_nodes:
            node = self._server.get_node(self._telemetry_nodes[key])
            await node.write_value(point.value, variant_type)
        else:
            tel_folder_key = f"{point.device_id}/Telemetry"
            if tel_folder_key not in self._device_nodes:
                return
            tel_folder = self._server.get_node(
                self._device_nodes[tel_folder_key]
            )
            nid = ua.NodeId(
                f"Devices/{point.device_id}/Telemetry/{point.name}",
                self._ns_idx,
            )
            var = await tel_folder.add_variable(
                nid, point.name, point.value, variant_type
            )
            await var.set_writable(False)
            self._telemetry_nodes[key] = var.nodeid
            logger.debug("Created OPC UA telemetry variable: %s", nid)
