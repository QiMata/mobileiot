# MobileIoT

This repository contains small .NET and Python demos for Raspberry Pi.

The `src/MobileIoT` folder holds the .NET MAUI application, while `src/pi` includes Python utilities that run directly on the Pi hardware.

## BLE GATT Demo

The Python script at `src/pi/bluetoothle_demo.py` implements a Bluetooth Low Energy GATT server exposing temperature, humidity and LED control. It matches the UUIDs that the MobileIoT app expects:

- **Service UUID:** `12345678-1234-1234-1234-1234567890AB`
- **Temperature Characteristic:** UUID `00002A6E-0000-1000-8000-00805F9B34FB`
- **Humidity Characteristic:** UUID `00002A6F-0000-1000-8000-00805F9B34FB`
- **LED Characteristic:** UUID `12345679-1234-1234-1234-1234567890AB`

Run the demo with:

```bash
python3 bluetoothle_demo.py --azure-iot-hub-connection-string "<connection>"
```

Telemetry forwarding to Azure IoT Hub or Azure IoT Operations Edge is optional; provide the relevant `--azure-*` arguments to enable it. See `src/pi/README.md` for a table of command-line options.

## BLE Beacon Demo

A Raspberry Pi can also act as a simple iBeacon for the .NET MAUI app. The Python script at `src/pi/beacon_demo.py` uses BlueZero to broadcast an iBeacon advertisement with the same UUID used elsewhere in the project. The script accepts parameters for the UUID, major/minor identifiers, adapter selection, and optional runtime duration so you can tailor presence-detection experiments:

```bash
sudo python3 beacon_demo.py --uuid 12345678-1234-1234-1234-1234567890AB --major 100 --minor 1 --duration 60
```

See `src/pi/BEACON_SETUP.md` for setup instructions and additional context.

## USB Serial & Device Demos

The Pi can also communicate with the MAUI app over USB. To enable this,
configure the Pi for **USB gadget mode** (see `src/pi/README.md` for the
exact steps). Two demos are provided:

1. **USB Serial (CDC ACM)** - load the `g_serial` driver and run
   `src/pi/serial_demo.py`. The MAUI app alternates between the commands
   `"LED_ON"` and `"LED_OFF"` which toggle an LED on GPIO17 and reply with
   an acknowledgment. Use `run_serial_demo.sh` to automatically load the
   gadget driver and forward any command-line arguments to the Python script.
2. **USB Bulk Ping** - load the `g_zero` gadget. It echoes any bulk data
   from the host so the MAUI app's ping function receives the bytes back
   immediately. The helper `run_usb_bulk_echo.sh` swaps out `g_serial`
   if necessary and activates `g_zero` in one step.

## Thread Protocol Demo

The Thread demo shows how the MAUI app can interact with a Thread mesh
network through a Raspberry Pi running OpenThread Border Router (OTBR).

**Hybrid mock/live mode** - the app starts in mock mode with deterministic
synthetic data so it can be used immediately on any platform. Toggle the
"Use Live Bridge" switch and enter the Pi's bridge URL to query real Thread
status via `ot-ctl` and perform CoAP echo pings to mesh nodes.

### App side

The shared Thread logic lives in the `QiMata.MobileIoT.Shared.Thread`
namespace under the `QiMata.MobileIoT.Shared` `net8.0` library, with no MAUI
dependencies. The MAUI app references it and
provides `ThreadPage.xaml` with controls for status refresh, CoAP ping, and
a scrollable log capped at 200 entries.

### Pi side

`src/pi/thread_demo.py` runs an HTTP bridge on port 8080 and a CoAP echo
server on port 5683. Endpoints:

| Method | Path              | Description                   |
|--------|-------------------|-------------------------------|
| GET    | `/healthz`        | Health check                  |
| GET    | `/thread/status`  | Thread network status         |
| POST   | `/thread/ping`    | CoAP echo ping to a mesh node |

Prerequisites: OTBR installed and a Thread network commissioned. See
`src/pi/README.md` for setup and run commands.

### Running tests

```bash
# .NET (from src/MobileIoT/)
dotnet test QiMata.MobileIoT.Shared.Thread.Tests

# Python (from repo root)
python -m pytest src/pi/tests -q
```

## Audio Jack Telemetry

An additional demo shows how to transmit sensor data through the Pi's audio jack. See
`docs/audio_jack_demo.md` and the script `src/pi/audio_demo.py` for details on
connecting a TRRS cable and using the `minimodem` tool to send readings as audio
tones. The MobileIoT app has an "Audio Jack" page that listens on the microphone
and displays the decoded messages. Run the demo with, for example:

```bash
python3 audio_demo.py --sensor dht22 --dht22-pin 4 --interval 3
```

The script can stream CPU temperature, DHT22 readings, or values from a JSON file by
supplying the appropriate `--sensor` option. Each reading is formatted as
comma-separated key/value pairs before being handed to `minimodem` for FSK
modulation. Ensure `minimodem` is installed (`sudo apt-get install minimodem`) and
the Pi audio output volume is set low enough not to clip the phone input.
