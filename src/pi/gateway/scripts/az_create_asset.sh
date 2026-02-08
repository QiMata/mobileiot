#!/usr/bin/env bash
# Create an Azure IoT Operations asset for a discovered gateway device.
# Usage: az_create_asset.sh <device_id>
set -euo pipefail

DEVICE_ID="${1:?Usage: az_create_asset.sh <device_id>}"

if [ -f /etc/mobileiot/gateway.env ]; then
    set -a
    # shellcheck disable=SC1091
    source /etc/mobileiot/gateway.env
    set +a
fi

: "${AIO_RESOURCE_GROUP:?Set AIO_RESOURCE_GROUP}"
: "${AIO_INSTANCE_NAME:?Set AIO_INSTANCE_NAME}"
: "${AIO_DEVICE_NAME:?Set AIO_DEVICE_NAME}"

ENDPOINT_NAME="${AIO_DEVICE_NAME}-ep"

# Fetch device info from the gateway health endpoint
HEALTH_HOST="${HEALTH_HOST:-localhost}"
HEALTH_PORT="${HEALTH_PORT:-8081}"

DEVICE_JSON=$(curl -sf "http://${HEALTH_HOST}:${HEALTH_PORT}/devices" | python3 -c "
import json, sys
devices = json.load(sys.stdin)
for d in devices:
    if d['device_id'] == sys.argv[1]:
        print(json.dumps(d))
        sys.exit(0)
print('', file=sys.stderr)
sys.exit(1)
" "${DEVICE_ID}") || { echo "Device '${DEVICE_ID}' not found in gateway registry."; exit 1; }

DISPLAY_NAME=$(echo "${DEVICE_JSON}" | python3 -c "import json,sys; print(json.load(sys.stdin)['display_name'])")
SAFE_NAME=$(echo "${DEVICE_ID}" | tr ':/' '-')

echo "Creating asset: ${SAFE_NAME} (${DISPLAY_NAME})"

az iot ops asset create \
    --name "${SAFE_NAME}" \
    -g "${AIO_RESOURCE_GROUP}" \
    --instance "${AIO_INSTANCE_NAME}" \
    --endpoint "${ENDPOINT_NAME}" \
    --description "Auto-registered: ${DISPLAY_NAME}" \
    --output table 2>/dev/null || echo "  Asset may already exist (idempotent)."

# Add standard telemetry data points
for POINT_NAME in temperature humidity cpu_temperature uptime_seconds; do
    NODE_ID="ns=2;s=Devices/${DEVICE_ID}/Telemetry/${POINT_NAME}"
    echo "  Adding data point: ${POINT_NAME} -> ${NODE_ID}"
    az iot ops asset data-point add \
        --asset "${SAFE_NAME}" \
        -g "${AIO_RESOURCE_GROUP}" \
        --instance "${AIO_INSTANCE_NAME}" \
        --data-source "${NODE_ID}" \
        --name "${POINT_NAME}" \
        --output none 2>/dev/null || echo "    (already exists)"
done

echo "Asset '${SAFE_NAME}' created with data points."
