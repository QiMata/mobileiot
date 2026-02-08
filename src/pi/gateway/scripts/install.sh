#!/usr/bin/env bash
# Install the MobileIoT OPC UA Gateway.
# Run as root or with sudo.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
GATEWAY_DIR="$(dirname "$SCRIPT_DIR")"
PI_DIR="$(dirname "$GATEWAY_DIR")"

echo "=== MobileIoT OPC UA Gateway Installer ==="

# Create virtual environment if not present
if [ ! -d "$PI_DIR/.venv" ]; then
    echo "Creating Python virtual environment..."
    python3 -m venv "$PI_DIR/.venv"
fi

echo "Installing Python dependencies..."
"$PI_DIR/.venv/bin/pip" install --quiet -r "$GATEWAY_DIR/requirements.txt"

# Create system directories
echo "Creating system directories..."
sudo mkdir -p /etc/mobileiot/opcua/{certs,private,trust}
sudo mkdir -p /var/lib/mobileiot
sudo chown -R pi:pi /etc/mobileiot /var/lib/mobileiot

# Copy example configs if not present
if [ ! -f /etc/mobileiot/gateway.yaml ]; then
    sudo cp "$GATEWAY_DIR/gateway.yaml.example" /etc/mobileiot/gateway.yaml
    echo "Copied example config to /etc/mobileiot/gateway.yaml"
fi

if [ ! -f /etc/mobileiot/gateway.env ]; then
    sudo cp "$GATEWAY_DIR/gateway.env.example" /etc/mobileiot/gateway.env
    echo "Copied example env to /etc/mobileiot/gateway.env"
fi

# Install systemd unit
echo "Installing systemd service..."
sudo cp "$GATEWAY_DIR/gateway.service" /etc/systemd/system/mobileiot-gateway.service
sudo systemctl daemon-reload

echo ""
echo "Installation complete."
echo ""
echo "Next steps:"
echo "  1. Edit /etc/mobileiot/gateway.yaml with your settings"
echo "  2. Edit /etc/mobileiot/gateway.env with Azure IoT Operations settings"
echo "  3. Generate certificates:"
echo "     sudo $PI_DIR/.venv/bin/python -m gateway.pki.generate_ca"
echo "     sudo $PI_DIR/.venv/bin/python -m gateway.pki.generate_server_cert"
echo "  4. Start the gateway:"
echo "     sudo systemctl enable --now mobileiot-gateway"
