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

## USB Serial Demo

To control the Pi over USB as a virtual COM port, enable USB gadget mode
and load the `g_serial` module. Once `/dev/ttyGS0` appears, run
`serial_demo.py`:

```bash
sudo ./run_serial_demo.sh
```

The MAUI app sends `LED_ON` or `LED_OFF` commands which toggle an LED on
GPIO17 and receive an acknowledgment string.

## USB Device-Mode Ping Demo

For a simple USB bulk transfer demo, load the `g_zero` gadget instead of
`g_serial`:

```bash
sudo modprobe g_zero
```

No additional script is required – any data written to the bulk OUT
endpoint is echoed back to the host so the app’s ping feature sees a
response.
