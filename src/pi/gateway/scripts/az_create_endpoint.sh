#!/usr/bin/env bash
# Create an Azure IoT Operations asset endpoint for the gateway OPC UA server.
# Requires: az CLI with iot-ops extension, /etc/mobileiot/gateway.env
set -euo pipefail

if [ -f /etc/mobileiot/gateway.env ]; then
    set -a
    # shellcheck disable=SC1091
    source /etc/mobileiot/gateway.env
    set +a
fi

: "${AIO_RESOURCE_GROUP:?Set AIO_RESOURCE_GROUP in /etc/mobileiot/gateway.env}"
: "${AIO_INSTANCE_NAME:?Set AIO_INSTANCE_NAME in /etc/mobileiot/gateway.env}"
: "${AIO_DEVICE_NAME:?Set AIO_DEVICE_NAME in /etc/mobileiot/gateway.env}"

PI_HOST="${PI_HOSTNAME:-$(hostname -I | awk '{print $1}')}"
ENDPOINT_ADDR="opc.tcp://${PI_HOST}:4840"
ENDPOINT_NAME="${AIO_DEVICE_NAME}-ep"

echo "Creating asset endpoint: ${ENDPOINT_NAME}"
echo "  OPC UA address: ${ENDPOINT_ADDR}"
echo "  Resource group: ${AIO_RESOURCE_GROUP}"
echo "  AIO instance:   ${AIO_INSTANCE_NAME}"

az iot ops asset endpoint create opcua \
    --name "${ENDPOINT_NAME}" \
    -g "${AIO_RESOURCE_GROUP}" \
    --instance "${AIO_INSTANCE_NAME}" \
    --target-address "${ENDPOINT_ADDR}" \
    --output table

echo ""
echo "Endpoint created: ${ENDPOINT_ADDR}"
echo "Use az_create_asset.sh to register individual devices as assets."
