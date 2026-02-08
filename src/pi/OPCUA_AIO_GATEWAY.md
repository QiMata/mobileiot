# OPC UA Gateway for Azure IoT Operations

A Python gateway that discovers BLE, USB serial, and WiFi Direct devices on Raspberry Pi, normalizes their telemetry into a single OPC UA server, and onboards them to Azure IoT Operations.

## Architecture

```
[bluetoothle_demo.py]     [serial_demo.py]     [wifidirect_demo.py]
 (BLE Peripheral)          (/dev/ttyGS0)        (TCP :8988)
       |                        |                      |
       v                        v                      v
 +-----------+           +------------+          +-------------+
 | BLE       |           | USB Serial |          | WiFi Direct |
 | Adapter   |           | Adapter    |          | Adapter     |
 +-----------+           +------------+          +-------------+
       |                        |                      |
       +--- TelemetryPoint -----+-------- events ------+
       v                                               v
 +-------------------+     +---------------------+
 |   Registry        | <-> | Command Router      |
 +-------------------+     +---------------------+
       |
       v
 +-------------------+
 |  OPC UA Server    |   opc.tcp://0.0.0.0:4840
 |  (asyncua)        |   ns=2;s=Devices/...
 +-------------------+
       |
       v
 Azure IoT Operations OPC UA Connector
```

## Quick Start

### 1. Install

```bash
cd src/pi
sudo bash gateway/scripts/install.sh
```

### 2. Generate Certificates

```bash
sudo .venv/bin/python -m gateway.pki.generate_ca
sudo .venv/bin/python -m gateway.pki.generate_server_cert
```

### 3. Configure

Edit `/etc/mobileiot/gateway.yaml` — see `gateway/gateway.yaml.example` for all options.

### 4. Start

```bash
sudo systemctl enable --now mobileiot-gateway
```

### 5. Verify

```bash
bash gateway/scripts/health_check.sh
```

### 6. Onboard to Azure IoT Operations

```bash
# Edit /etc/mobileiot/gateway.env with your Azure settings
bash gateway/scripts/az_onboard.sh
```

## OPC UA Address Space

Each discovered device gets a node tree:

```
Objects/
  Devices/
    ble:AA:BB:CC:DD:EE:FF/
      Meta/
        protocol     = "ble"
        address      = "AA:BB:CC:DD:EE:FF"
        status       = "online"
        display_name = "PiSensor"
      Telemetry/
        temperature  = 23.45    (ns=2;s=Devices/ble:AA:BB:CC:DD:EE:FF/Telemetry/temperature)
        humidity     = 55.70
      Commands/
        LED_ON
        LED_OFF
    serial:1234:5678:001/
      Meta/...
      Telemetry/
        temperature, humidity, uptime, cpu_temp, mem_free
      Commands/
        LED_ON, LED_OFF, GPIO_WRITE
```

## Protocol Adapters

### BLE (Central Role)
- Scans for peripherals matching configured service UUIDs and names
- Connects via `bleak` and reads GATT characteristics
- Default: service `12345678-1234-1234-1234-1234567890AB`, temp `00002A6E`, humidity `00002A6F`
- LED writes via characteristic `12345679-...` (allowlist enforced)

### USB Serial
- Discovers `/dev/ttyUSB*`, `/dev/ttyACM*` ports
- Sends `SENSOR_READ`, `STATUS` commands at configured interval
- Parses `SENSOR:key=value` and `STATUS:key=value` responses
- Write allowlist: `LED_ON`, `LED_OFF`, `GPIO_WRITE`

### WiFi Direct
- TCP client connecting to port 8988
- 4-byte big-endian length prefix + UTF-8 JSON framing
- Requests telemetry streaming on connect
- Parses `temp`, `hum`, `cpu_temp`, `uptime` fields

## Security

- OPC UA: `Basic256Sha256` / `SignAndEncrypt` by default
- Anonymous connections disabled
- Trust-list validation for client certificates
- Command write-back enforced through per-adapter allowlists
- Private keys stored with restricted permissions (`0o600`)

### Certificate Management

```bash
# Initial setup
sudo python -m gateway.pki.generate_ca
sudo python -m gateway.pki.generate_server_cert

# Rotation (safe reload via SIGHUP)
sudo python -m gateway.pki.rotate_cert
```

Certificate files:
- `/etc/mobileiot/opcua/certs/ca_cert.pem`
- `/etc/mobileiot/opcua/certs/server_cert.pem`
- `/etc/mobileiot/opcua/private/ca_key.pem`
- `/etc/mobileiot/opcua/private/server_key.pem`
- `/etc/mobileiot/opcua/trust/` — trusted client CA certificates

## Configuration

See `gateway/gateway.yaml.example` for the full schema. Key sections:

| Section | Description |
|---------|-------------|
| `ble` | BLE scan intervals, service/name filters, write allowlist |
| `serial` | Port patterns, baud rate, poll interval, command allowlist |
| `wifi_direct` | TCP host/port, reconnect delay |
| `opcua` | Endpoint, security policy, certificate paths |
| `registry` | Heartbeat/offline timeouts, persistence path |
| `health` | HTTP health endpoint host/port |

## Azure IoT Operations Onboarding

### Automated

```bash
bash gateway/scripts/az_onboard.sh
```

This:
1. Creates an OPC UA asset endpoint pointing at the gateway
2. Fetches all online devices from the health endpoint
3. Creates an asset per device with telemetry data points

### Manual

```bash
# Create endpoint
bash gateway/scripts/az_create_endpoint.sh

# Create asset for a specific device
bash gateway/scripts/az_create_asset.sh "ble:AA:BB:CC:DD:EE:FF"
```

## Health Endpoint

`GET http://localhost:8081/healthz`
```json
{
  "status": "ok",
  "opcua_running": true,
  "devices_total": 5,
  "devices_online": 3
}
```

`GET http://localhost:8081/devices`
```json
[
  {
    "device_id": "ble:AA:BB:CC:DD:EE:FF",
    "protocol": "ble",
    "status": "online",
    "display_name": "PiSensor",
    "last_seen": "2025-01-15T10:30:00+00:00"
  }
]
```

## Running Tests

```bash
cd src/pi

# Unit tests (no hardware required)
python -m pytest tests/test_gateway_*.py tests/test_adapter_*.py tests/test_command_router.py -v

# OPC UA tests (requires asyncua)
python -m pytest tests/test_opcua_server.py -v

# Integration tests
python -m pytest tests/test_integration.py -v

# Scale tests
python -m pytest tests/test_scale.py -v

# All tests
python -m pytest tests/ -v
```

## Systemd Service

```bash
# Status
sudo systemctl status mobileiot-gateway

# Logs
sudo journalctl -u mobileiot-gateway -f

# Restart
sudo systemctl restart mobileiot-gateway
```

## Troubleshooting

**No BLE devices found**: Ensure `bluetoothle_demo.py` is running and advertising. Check `bluetoothctl scan on` to verify BLE radio.

**Serial port permission denied**: Add the `pi` user to the `dialout` group: `sudo usermod -aG dialout pi`

**WiFi Direct connection refused**: Ensure `wifidirect_demo.py` is running on port 8988. Check `listen_host` and `listen_port` in config.

**OPC UA client cannot connect**: Verify certificates are generated and paths in `gateway.yaml` are correct. For testing, set `security_mode: "None"` and `anonymous_allowed: true`.
