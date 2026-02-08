#!/usr/bin/env bash
# Start the MobileIoT OPC UA Gateway service.
set -euo pipefail

sudo systemctl enable --now mobileiot-gateway
echo "Gateway service started."
sudo systemctl status mobileiot-gateway --no-pager
