# Raspberry Pi IoT Demos

This README summarizes the Raspberry Pi demo scripts used with the QiMata MobileIoT app.

## BLE GATT Server Demo (Sensor and LED)

**Purpose:** `bluetoothle_demo.py` turns the Pi into a BLE GATT peripheral that exposes:
- Temperature characteristic (`00002A6E-0000-1000-8000-00805F9B34FB`)
- Humidity characteristic (`00002A6F-0000-1000-8000-00805F9B34FB`)
- LED write characteristic (`12345679-1234-1234-1234-1234567890AB`)

The service UUID is `12345678-1234-1234-1234-1234567890AB`.

**Hardware:** DHT22 on GPIO4 and LED on GPIO17.

**Run:**
```bash
python3 bluetoothle_demo.py --device-name PiSensor
```

### Forward BLE telemetry to Azure

Optional arguments:
- `--azure-iot-hub-connection-string`
- `--azure-iot-ops-ingest-url`
- `--azure-iot-ops-api-key`
- `--azure-iot-ops-api-key-header`

Example:
```bash
python3 bluetoothle_demo.py \
  --azure-iot-hub-connection-string "$IOTHUB_DEVICE_CONNECTION_STRING" \
  --azure-iot-ops-ingest-url "https://edge-host/api/data" \
  --azure-iot-ops-api-key "$IOTOPS_API_KEY"
```

## BLE Beacon Demo (iBeacon)

**Purpose:** `beacon_demo.py` broadcasts iBeacon advertisements for proximity detection.

**Run (fixed major/minor):**
```bash
sudo python3 beacon_demo.py \
  --uuid 12345678-1234-1234-1234-1234567890AB \
  --major 1 \
  --minor 2 \
  --tx-power -59 \
  --adapter hci0
```

**Optional sensor mode:** encode DHT22 readings into major/minor.
```bash
sudo python3 beacon_demo.py --sensor-major-minor --dht-pin 4 --update-interval 5
```

See `BEACON_SETUP.md` for environment setup and troubleshooting.

## USB Serial Demo (CDC ACM)

**Purpose:** `serial_demo.py` listens on `/dev/ttyGS0` and handles commands from the app.

Supported commands:
- `LED_ON`, `LED_OFF`
- `GPIO_READ <pin>`
- `GPIO_WRITE <pin> <0|1>`
- `SENSOR_READ`
- `STATUS`

**Run:**
```bash
sudo ./run_serial_demo.sh
```

## USB Bulk Ping Demo (g_zero)

**Purpose:** Echo raw USB bulk data back to the host.

**Run:**
```bash
sudo ./run_usb_bulk_echo.sh
```

## Thread Protocol Demo (OTBR + CoAP Bridge)

**Purpose:** `thread_demo.py` bridges Thread network info to the app over HTTP and supports CoAP echo pings.

**Prerequisites:**
1. Install and configure OTBR on the Pi.
2. Install Python dependencies:
```bash
pip install aiocoap aiohttp
```

**Run:**
```bash
cp thread_demo.env.example thread_demo.env
python3 thread_demo.py
```

HTTP endpoints:
- `GET /healthz`
- `GET /thread/status`
- `POST /thread/ping`

Example:
```bash
curl -X POST http://raspberrypi.local:8080/thread/ping \
  -H "Content-Type: application/json" \
  -d '{"target":"fd00::1","payload":"hello","timeoutMs":3000}'
```

## Audio Jack Telemetry Demo

**Purpose:** `audio_demo.py` sends telemetry as FSK audio using `minimodem`.

**Hardware:** Pi audio out -> phone mic input through a proper TRRS conditioning circuit.

**Run:**
```bash
python3 audio_demo.py --sensor cpu-temp --interval 2
python3 audio_demo.py --sensor dht22 --dht22-pin 4
python3 audio_demo.py --sensor json-file --input-path ./telemetry.json --interval 10
```

Install dependency:
```bash
sudo apt-get install minimodem
```

## Camera to Azure IoT Operations Edge Demo

**Purpose:** `camera_iot.py` packages camera frames and uploads them to Media Connector ingest.

**Run:**
```bash
export IOTOPS_MEDIA_CONNECTOR_INGEST_URL="https://<edge-fqdn>/media/ingest"
export IOTOPS_MEDIA_CONNECTOR_STREAM_ID="<stream-id>"
export IOTOPS_MEDIA_CONNECTOR_API_KEY="<api-key-or-token>"
export IOTOPS_MEDIA_CONNECTOR_API_KEY_HEADER="Authorization"
python3 camera_iot.py
```

Implement `MediaConnectorCamera.capture_frame` for your camera hardware.

## Tests

From repo root:
```bash
python -m pytest src/pi/tests -q
```

These demos cover BLE sensor/actuator control, iBeacon presence, USB serial and bulk communication, Thread mesh bridging, audio transport, and camera ingestion for edge workflows.
