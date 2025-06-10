# Raspberry Pi IoT Demos

This README summarizes the Raspberry Pi demonstration scripts included in the QiMata **MobileIoT** repository. These Python-based demos illustrate how a Raspberry Pi can be integrated into IoT workflows alongside a .NET MAUI mobile application. Each demo highlights a different use case – from using the Pi as a Bluetooth Low Energy sensor device to leveraging it as a USB-connected gadget – and shows how the Pi interacts with sensors or peripherals and communicates over common IoT interfaces (BLE, USB, etc). Developers can use these examples to understand how to connect a Raspberry Pi to other devices or apps via short-range wireless protocols (like Bluetooth) or direct wired connections (like USB). *(Note: While not explicitly covered here, similar principles apply if integrating a Pi using Wi-Fi networking or NFC interfaces in IoT scenarios.)*

## BLE GATT Server Demo (Sensor & LED via Bluetooth LE)

**Purpose:** This demo turns a Raspberry Pi into a Bluetooth Low Energy **GATT server** that exposes environmental sensor data and allows basic device control. It runs the `bluetoothle_demo.py` script, which advertises a custom BLE service containing a temperature sensor reading, a humidity sensor reading, and an LED toggle control. This mirrors the functionality expected by the companion MobileIoT MAUI app, using the same BLE UUIDs that the app is configured to look for.

**Hardware:** The setup involves a DHT22 temperature/humidity sensor and a simple LED indicator attached to the Raspberry Pi’s GPIO pins (sensor data line on **GPIO4**, LED on **GPIO17**). The Pi reads the DHT22 sensor to obtain temperature (°C) and humidity (%), and it controls the LED via a GPIO output.

**How it Works:** The Python script uses the BlueZ/BlueZero library to create a BLE peripheral named "PiSensor" with one custom service (UUID `12345678-1234-1234-1234-1234567890AB`) and three GATT characteristics:

* **Service UUID:** `12345678-1234-1234-1234-1234567890AB` (custom service for sensor and LED data)
* **Temperature Characteristic:** UUID `00002A6E-0000-1000-8000-00805F9B34FB` – Current temperature reading (16-bit value, in hundredths of a degree Celsius)
* **Humidity Characteristic:** UUID `00002A6F-0000-1000-8000-00805F9B34FB` – Current relative humidity reading (16-bit value, in hundredths of a percent)
* **LED Characteristic:** UUID `12345679-1234-1234-1234-1234567890AB` – LED state control (1 byte: write `0x01` or `0x00` to turn the Pi’s LED on or off)

When the BLE GATT server is running, a mobile app or other BLE central device can connect to the Pi and interact with these characteristics. The app can **read** or subscribe to notifications from the Temperature and Humidity characteristics to get sensor measurements, and it can **write** to the LED characteristic to remotely toggle the Pi’s LED. Under the hood, the script reads the DHT22 sensor on-demand for updates and uses callbacks to update the characteristic values. For example, when a write of `0x01` is received on the LED characteristic, the Pi sets the GPIO17 high to turn on the LED (and `0x00` turns it off). This demo illustrates a classic IoT pattern: a Raspberry Pi acting as a BLE-based environmental sensor and actuator module, communicating wirelessly with an application over GATT.

## BLE Beacon Demo (iBeacon Advertisement)

**Purpose:** This demo showcases how a Raspberry Pi can operate as a Bluetooth LE **beacon**. Using the `beacon_demo.py` script, the Pi advertises itself as an **iBeacon**, allowing the MobileIoT app (or any BLE scanner) to detect its presence and approximate proximity. Unlike the GATT server demo, the beacon is a one-way broadcast – there is no connection or data exchange beyond the advertisement itself.

**Hardware:** No external sensors are required for this demo – it only uses the Pi’s built-in Bluetooth radio. (Ensure the Pi’s Bluetooth is enabled and not blocked by rfkill.)

**How it Works:** The script uses BlueZ/BlueZero in broadcaster mode to send out a manufacturer-specific BLE advertisement that follows the iBeacon format. It broadcasts a 128-bit UUID as the beacon identifier (in this project, the UUID `12345678-1234-1234-1234-1234567890AB` is used to tie into the app’s expectations) along with a 16-bit Major ID and 16-bit Minor ID (the demo uses Major = 1 and Minor = 2 as sample values). The advertisement also includes a Tx power calibration byte (set to –59 dBm, a typical value for 1m reference signal strength). The Pi transmits this iBeacon packet non-stop as a non-connectable BLE advertisement.

When the MobileIoT app is scanning, it will detect the Pi’s beacon signal (identified by the matching UUID) and can use it to trigger proximity-based logic or simply log the beacon’s presence. This demo demonstrates how a Raspberry Pi can serve as a BLE beacon for use cases like indoor positioning, asset tracking, or presence detection in IoT workflows. It shows how to programmatically configure beacon advertisements on the Pi using standard BLE parameters.

## USB Serial Demo (Virtual COM Port Control)

**Purpose:** This demo turns the Raspberry Pi into a USB **CDC ACM device** (virtual serial COM port) to enable direct wired communication with a host (such as an Android phone or PC). It allows a connected host application to send text commands to the Pi and receive responses, simulating a simple control interface over USB. In the MobileIoT app, this is used to toggle an LED on the Pi by sending commands over a USB cable instead of wirelessly.

**Hardware:** In addition to the Pi itself, this demo uses a single LED attached to **GPIO17** on the Pi (the same LED setup as the BLE demo). To use this demo, the Raspberry Pi must support **USB gadget mode** (for example, a Raspberry Pi Zero or Zero W can be configured to act as a USB device). You’ll also need a USB connection between the Pi and the host device (e.g. a USB OTG cable to connect the Pi Zero’s USB port to a phone, or a direct USB link to a computer).

**How it Works:** First, the Pi needs to be configured in gadget mode as a USB serial device. This involves loading the Linux **g\_serial** module, which creates a `/dev/ttyGS0` device on the Pi that represents the USB serial link. Once the **g\_serial** gadget is active (and the Pi is plugged into the host), running the `serial_demo.py` script will listen on `/dev/ttyGS0` for incoming data.

On the host side, the Pi will enumerate as a serial COM port. The MobileIoT app (or any terminal program on a PC) can open this serial port and send simple text commands. In this demo, two commands are recognized: `"LED_ON"` and `"LED_OFF"`. When the Pi receives one of these commands via the USB serial connection, it sets its GPIO17 LED on or off accordingly, and sends back a confirmation message (e.g. **ACK: LED turned ON** or **ACK: LED turned OFF**). If an unrecognized string is sent, the Pi responds with a negative acknowledgment (**NACK: Unknown command**). The `serial_demo.py` script implements this loop, continuously reading from the serial port and handling incoming commands. This demo illustrates a tethered IoT device scenario: the Raspberry Pi behaves like a USB-enabled microcontroller, providing a simple control interface to the host. It demonstrates how to use USB CDC communication for device control and how a Pi can emulate a serial device for direct integration with other systems.

*(Note: To simplify running this demo, a helper script `run_serial_demo.sh` is provided to load the gadget driver and launch the Python program in one step.)*

## USB Bulk Ping Demo (USB Echo Gadget)

**Purpose:** This demo uses the Raspberry Pi as a generic USB device that echoes data back to the host over a bulk endpoint, showcasing a simple **USB bulk transfer** communication. It is used in the MobileIoT app’s “ping” test feature, where the app sends data to the Pi and expects the exact data echoed back immediately.

**Hardware:** No additional hardware is needed beyond the Raspberry Pi and a USB connection to the host. As with the serial demo, the Pi must be in USB gadget mode (here using a different gadget driver).

**How it Works:** Instead of a custom script, this demo leverages the built-in Linux `g_zero` gadget module, which implements a basic USB device that has bulk IN/OUT endpoints and automatically echoes any received data. To set up the demo, the `g_zero` module is loaded on the Pi (in place of `g_serial`) to configure the Pi as a USB “loopback” device. Once the Pi is plugged into the host and the gadget is active, the MobileIoT app can send a stream of bytes to the Pi’s bulk OUT endpoint and the `g_zero` gadget will echo those bytes back on the bulk IN endpoint. The app’s ping function simply writes data and verifies that the response matches, confirming the communication.

Because the echo functionality is handled in the Pi’s kernel module, **no separate Python script is required for this demo** – the Pi effectively acts as a USB echo device as soon as `g_zero` is enabled. This demo demonstrates how a Raspberry Pi can emulate a custom USB data interface, useful for high-throughput data exchange or testing USB communications. It highlights an IoT integration scenario where a device communicates over USB at the raw data level (bulk endpoints) rather than through a higher-level protocol.

## Audio Jack Telemetry Demo

**Purpose:** This experimental demo sends sensor data through the Pi’s audio output so a mobile device can receive it via the headphone microphone pin. It illustrates how an audio cable can serve as a low-speed data link when no network or USB connection is available.

**Hardware:** A Raspberry Pi with a 3.5mm audio jack and a TRRS cable to connect to the phone’s headset jack. A small resistor divider and capacitor should condition the Pi’s line-out level for the phone’s mic input (see `docs/audio_jack_demo.md`). Optionally attach a sensor such as a DHT22 to provide real data.

**How it Works:** The script `audio_demo.py` reads a value (for example the CPU temperature) and transmits it as FSK tones using the Linux `minimodem` tool. The mobile app records from the mic and decodes the tones back into text. Each reading is sent every few seconds so the app can display the latest value.

Run the demo with:

```bash
python3 audio_demo.py
```

Ensure `minimodem` is installed (`sudo apt-get install minimodem`) and the Pi audio output volume is set low enough not to clip the phone input.

Overall, these Raspberry Pi demos cover a range of IoT interaction models: **wireless sensor/actuator control via BLE**, **proximity detection via BLE beacon**, and **wired command/data exchange via USB**. Developers can refer to these examples as a guide for integrating Raspberry Pi hardware with mobile or other client applications, adapting the concepts to fit their specific IoT workflows and communication needs. Each demo provides a template for connecting physical hardware (sensors, LEDs, etc.) with software, using industry-standard protocols and Raspberry Pi’s capabilities to bridge the physical and digital worlds in an IoT solution.
