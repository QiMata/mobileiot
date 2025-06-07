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

## Audio Jack Telemetry

An additional demo shows how to transmit sensor data through the Pi’s audio jack. See
`docs/audio_jack_demo.md` and the script `src/pi/audio_demo.py` for details on
connecting a TRRS cable and using the `minimodem` tool to send readings as audio
tones. The MobileIoT app has an "Audio Jack" page that listens on the microphone
and displays the decoded messages.
