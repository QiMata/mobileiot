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
