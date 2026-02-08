#!/usr/bin/env bash
# Full onboarding: create endpoint + register all discovered devices as assets.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
HEALTH_HOST="${HEALTH_HOST:-localhost}"
HEALTH_PORT="${HEALTH_PORT:-8081}"

echo "=== MobileIoT Azure IoT Operations Onboarding ==="
echo ""

# Step 1: Create the OPC UA endpoint
"${SCRIPT_DIR}/az_create_endpoint.sh"
echo ""

# Step 2: Get all online devices from gateway
echo "Fetching discovered devices..."
DEVICE_IDS=$(curl -sf "http://${HEALTH_HOST}:${HEALTH_PORT}/devices" | python3 -c "
import json, sys
for d in json.load(sys.stdin):
    if d['status'] == 'online':
        print(d['device_id'])
")

if [ -z "${DEVICE_IDS}" ]; then
    echo "No online devices found. Ensure the gateway is running and devices are connected."
    exit 0
fi

# Step 3: Create an asset for each device
COUNT=0
for DID in ${DEVICE_IDS}; do
    echo ""
    echo "--- Onboarding device: ${DID} ---"
    "${SCRIPT_DIR}/az_create_asset.sh" "${DID}"
    COUNT=$((COUNT + 1))
done

echo ""
echo "=== Onboarding complete: ${COUNT} device(s) registered ==="
