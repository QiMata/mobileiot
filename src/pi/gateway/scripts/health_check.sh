#!/usr/bin/env bash
# Check the gateway health endpoint.
set -euo pipefail

HOST="${1:-localhost}"
PORT="${2:-8081}"

echo "Checking gateway health at ${HOST}:${PORT}..."
curl -sf "http://${HOST}:${PORT}/healthz" | python3 -m json.tool

echo ""
echo "Registered devices:"
curl -sf "http://${HOST}:${PORT}/devices" | python3 -m json.tool
