# Raspberry Pi Demos

This directory contains small Python utilities that complement the .NET MAUI demo app.

## BLE GATT Server

The `bluetoothle_demo.py` script exposes temperature, humidity and LED control over Bluetooth LE. It implements the same UUIDs that the MobileIoT app looks for and can run directly on a Raspberry Pi.

Run the script with Python 3 (and appropriate permissions):

```bash
python3 bluetoothle_demo.py
```

The script relies on the `bluezero`, `Adafruit_DHT` and `RPi.GPIO` packages. Refer to the source code for detailed explanations of the BLE service, characteristic formats and how to enable startup on boot.

## BLE Beacon Demo

The `beacon_demo.py` script turns the Raspberry Pi into an iBeacon transmitter so the MobileIoT app can detect it. The beacon uses the same base UUID as the other demos. See `BEACON_SETUP.md` for a full walkthrough on configuring the Pi and the expected advertisement format.
