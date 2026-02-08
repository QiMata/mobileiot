# MobileIoT

This repository contains small .NET and Python demos for Raspberry Pi.

The `src/MobileIoT` folder holds the .NET MAUI application, while `src/pi` includes Python utilities that run directly on the Pi hardware.

## BLE GATT Demo

The Python script at `src/pi/bluetoothle_demo.py` implements a Bluetooth Low Energy GATT server exposing temperature, humidity and LED control. It matches the UUIDs that the MobileIoT app expects:

- **Service UUID:** `12345678-1234-1234-1234-1234567890AB`
- **Temperature Characteristic:** UUID `00002A6E-0000-1000-8000-00805F9B34FB`
- **Humidity Characteristic:** UUID `00002A6F-0000-1000-8000-00805F9B34FB`
- **LED Characteristic:** UUID `12345679-1234-1234-1234-1234567890AB`

See `src/pi/README.md` or the script itself for full details and instructions.

## BLE Beacon Demo

A Raspberry Pi can also act as a simple iBeacon for the .NET MAUI app. The Python script at `src/pi/beacon_demo.py` uses BlueZero to broadcast an iBeacon advertisement with the same UUID used elsewhere in the project. See `src/pi/BEACON_SETUP.md` for setup instructions.

## USB Serial & Device Demos

The Pi can also communicate with the MAUI app over USB. To enable this,
configure the Pi for **USB gadget mode** (see `src/pi/README.md` for the
exact steps). Two demos are provided:

1. **USB Serial (CDC ACM)** – load the `g_serial` driver and run
   `src/pi/serial_demo.py`. The MAUI app alternates between the commands
   `"LED_ON"` and `"LED_OFF"` which toggle an LED on GPIO17 and reply with
   an acknowledgment.
2. **USB Bulk Ping** – load the `g_zero` gadget. It echoes any bulk data
   from the host so the MAUI app’s ping function receives the bytes back
   immediately.

The helper script `src/pi/run_serial_demo.sh` loads `g_serial` if needed
and launches the Python demo.

## Thread Protocol Demo

The Thread demo shows how the MAUI app can interact with a Thread mesh
network through a Raspberry Pi running OpenThread Border Router (OTBR).

**Hybrid mock/live mode** – the app starts in mock mode with deterministic
synthetic data so it can be used immediately on any platform. Toggle the
"Use Live Bridge" switch and enter the Pi's bridge URL to query real Thread
status via `ot-ctl` and perform CoAP echo pings to mesh nodes.

### App side

The core logic lives in `QiMata.MobileIoT.ThreadDemoCore`, a plain `net8.0`
class library with no MAUI dependencies. The MAUI app references it and
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
dotnet test QiMata.MobileIoT.ThreadDemoCore.Tests

# Python (from repo root)
python -m pytest src/pi/tests -q
```
