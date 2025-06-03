# BLE Beacon Setup for .NET MAUI Demo (Raspberry Pi & .NET MAUI Integration)

## Expected iBeacon Advertisement Format for the Demo

The QiMata MobileIoT demo uses an **iBeacon**-style BLE advertisement for its beacon functionality. iBeacon packets contain a 128-bit UUID (often called the **proximity UUID** or “service” UUID in this context), plus 16-bit **Major** and **Minor** identifier values, and a 1-byte calibrated TX power value. This data is transmitted in the BLE advertising payload as manufacturer-specific data using Apple’s company ID (0x004C). In raw bytes, the iBeacon payload structure is:

* Company ID (Apple Inc.: **0x4C 00**, in little-endian)
* iBeacon indicator **0x02** and length **0x15** (these signify an iBeacon and that 21 bytes follow)
* **UUID**: 16 bytes unique identifier for the beacon (universally unique)
* **Major**: 2 bytes (application-specific group identifier)
* **Minor**: 2 bytes (application-specific subgroup identifier)
* **TX Power**: 1 byte (calibrated RSSI at 1 meter, used for distance approximation)

*Figure: BLE advertising packet structure for common beacon formats. The **iBeacon** format (top row, green) includes a 16-byte UUID, 2-byte Major, 2-byte Minor, and 1-byte TX power in the manufacturer data.*

For this demo, the **UUID** expected by the mobile app is `12345678-1234-1234-1234-1234567890AB`. (This UUID is defined in the Pi demo code and must match what the app looks for.) You can choose any 16-bit values for Major and Minor; for example, use **Major = 1** and **Minor = 2** as sample identifiers. The TX power byte is typically set to a default like **0xC5** (which corresponds to -59 dBm, a common calibration value). In summary, the beacon will broadcast an iBeacon advertisement with:

* **UUID:** `12345678-1234-1234-1234-1234567890AB` (128-bit)
* **Major:** `0x0001` (1)
* **Minor:** `0x0002` (2)
* **TX Power:** `0xC5` (=-59 dBm, example calibration)

These values will be embedded in the advertisement’s manufacturer-specific data (with Apple’s company ID) so that the .NET MAUI app can detect the beacon. The MAUI demo’s beacon scanner doesn’t explicitly filter by UUID in code (it simply logs any beacon advertisements it sees), but using the expected UUID and IDs helps identify your beacon in the app’s output.

## Raspberry Pi Configuration for BLE Beacon

To configure a Raspberry Pi as an iBeacon transmitter, follow these setup steps:

1. **Enable Bluetooth Hardware:** Ensure the Pi’s Bluetooth adapter is enabled and powered. On models with built-in Bluetooth (e.g. Pi 3/4), make sure nothing is blocking it (`sudo rfkill unblock bluetooth`) and bring up the interface (`sudo hciconfig hci0 up`).

2. **Install Required Packages:** You will need BlueZ (the Linux Bluetooth stack, usually pre-installed on Pi OS) and a Python library to interface with it. The QiMata project uses the `bluezero` Python library for BLE peripheral roles. Install BlueZero and any dependencies:

   ```bash
   sudo apt update
   sudo apt install python3-pip bluetooth libbluetooth-dev
   pip3 install bluezero
   ```

   *Note:* If you plan to use sensor/LED demos, also install `Adafruit_DHT` and `RPi.GPIO` as shown in the repository.

3. **Enable BLE Advertising Support:** Modern BlueZ versions (≥5.48) have stable support for BLE advertising via D-Bus. If you are on BlueZ 5.47 or older, you may need to start the Bluetooth service with the experimental flag to allow custom advertising. On recent Raspberry Pi OS releases (which use BlueZ 5.50+), advertising is supported by default. If needed, enable BlueZ experimental mode by editing `/lib/systemd/system/bluetooth.service` and adding `--experimental` to the `ExecStart` line, then reboot.

4. **Run as Root/Permissions:** Running BLE advertisements usually requires root or appropriate capabilities. It’s recommended to run the beacon script with sudo (or ensure your user has permissions to use Bluetooth D-Bus APIs). For example: `sudo python3 beacon.py`. This avoids permission errors when registering the advertisement.

With the environment prepared, we can now create the Python script to broadcast the iBeacon.

## Python Script to Broadcast the iBeacon

Below is a Python script that uses BlueZero to advertise an iBeacon with the specified parameters. This script sets up a non-connectable BLE broadcaster with the UUID, Major, Minor, and TX power matching the demo’s expected values:

```python
from bluezero import broadcaster
import uuid as uuidlib

# Define the iBeacon parameters (must match the demo expectations)
BEACON_UUID = uuidlib.UUID("12345678-1234-1234-1234-1234567890AB")
MAJOR = 1
MINOR = 2
TX_POWER = -59  # Measured RSSI at 1m, in dBm (use -59 dBm as a typical value)

# Convert parameters to bytes for advertisement
uuid_bytes = BEACON_UUID.bytes  # 16-byte UUID in big-endian byte order
major_bytes = MAJOR.to_bytes(2, byteorder='big')
minor_bytes = MINOR.to_bytes(2, byteorder='big')
# TX power byte: convert signed int to unsigned byte (0-255)
tx_power_byte = ((TX_POWER & 0xFF) if TX_POWER < 0 else TX_POWER).to_bytes(1, byteorder='big')

# Construct the manufacturer-specific payload for iBeacon
ibeacon_prefix = b'\x02\x15'  # iBeacon indicator: 0x02 (type), 0x15 (length)
ibeacon_data = ibeacon_prefix + uuid_bytes + major_bytes + minor_bytes + tx_power_byte

# Initialize the Beacon broadcaster (non-connectable advertisement)
beacon = broadcaster.Beacon()  # uses default Bluetooth adapter (hci0)

# Add Apple manufacturer ID (0x004C) and the iBeacon payload
beacon.add_manufacturer_data('4c00', ibeacon_data)

# Begin advertising the iBeacon
beacon.start_beacon()
print("iBeacon advertising started. UUID=%s, Major=%d, Minor=%d" % (BEACON_UUID, MAJOR, MINOR))

try:
    # Keep the program running to continue broadcasting
    while True:
        pass
except KeyboardInterrupt:
    # Stop advertising when script is interrupted
    beacon.stop_beacon()
    print("iBeacon advertising stopped.")
```

**How this works:** We use `bluezero.broadcaster.Beacon` to create a non-connectable advertising beacon. We then call `add_manufacturer_data()` with Apple’s company ID `0x004C` (represented as `'4c00'` in hex) and the raw iBeacon payload. The payload is composed of the standard iBeacon prefix `0x02 0x15`, followed by the 16-byte UUID, 2-byte Major, 2-byte Minor, and the 1-byte TX power. Finally, `start_beacon()` registers the advertisement with BlueZ and begins broadcasting. The script prints a confirmation and then enters an infinite loop to keep running (since the beacon advertising runs asynchronously in the background). Press **Ctrl+C** to terminate the script, which will call `stop_beacon()` to clean up.

**Advertisement Interval:** By default, BlueZ will advertise periodically (often around 100 ms interval by default for non-connectable beacons). This interval can be tuned via BlueZ configurations or HCI commands if needed, but the default is suitable for most beacon use-cases. In the beacon example above, we rely on BlueZ’s default advertising interval, which is typically on the order of 100 ms – making the beacon easily discoverable without excessive power usage.

## Verifying Beacon Parameters

Once the script is running on the Raspberry Pi, the device will continuously broadcast the iBeacon advertisement. You can use the .NET MAUI app or any BLE scanner to verify the beacon is advertising the correct values:

* On Android, use an app like “nRF Connect” or “BLE Scanner” to scan for BLE devices. You should see a device advertising manufacturer data for Apple (Company ID 0x004C). The raw advertisement will contain the demo UUID `12345678-1234-1234-1234-1234567890AB`, and the Major/Minor (`0x0001/0x0002`) in the payload. The MAUI demo app’s Beacon scanner page will list the advertisement as well (showing the device’s MAC, RSSI, and a hex preview of the data). The presence of `...-12-34-...-90-AB` in the hex preview confirms the UUID, and you may spot `00-01-00-02` corresponding to Major=1, Minor=2 in the bytes.

* On iOS, you might use an app like “Locate” or “Beacon Scanner” to detect iBeacons. The app should recognize an iBeacon with the specified UUID and show the major/minor values. (Note: iOS itself doesn’t show BLE adverts in the Bluetooth settings, you need a scanning app or the demo app.)

The .NET MAUI demo doesn’t need to connect to the beacon; it just **listens for the advertisement**. As long as the Pi is broadcasting with the correct format, the app should log the RSSI and data when the beacon is in range.

## Troubleshooting Tips

If the Raspberry Pi beacon is not working as expected, consider the following troubleshooting steps:

* **Check Python Errors:** If `beacon.start_beacon()` raises an exception (e.g., D-Bus permission or interface errors), ensure you are running the script with **sudo** or root privileges. Also verify that the Bluetooth service is running. If you see an error about `LEAdvertisingManager1` or “Not Supported”, it may indicate that BlueZ needs the experimental mode (for older versions) or that the adapter doesn’t support BLE advertising. Enabling the experimental flag (as described above) or upgrading BlueZ can resolve this.

* **Bluetooth Adapter Status:** Make sure the Bluetooth adapter (hci0) is powered on and not blocked. Run `hciconfig hci0` – it should show **UP RUNNING** and “LE” support. If it’s DOWN, use `sudo hciconfig hci0 up`. If `rfkill list` shows it blocked, unblocking is needed (`rfkill unblock bluetooth`). On headless setups, also ensure the **bluetooth service is enabled** (`sudo systemctl status bluetooth`) and active.

* **BlueZero / D-Bus Issues:** If the script seems to hang without advertising, or the beacon isn’t visible, the issue might be with BlueZ D-Bus advertisement registration. Ensure that no other program is already advertising with the same adapter. In some cases, running a BLE GATT server and a beacon on one adapter can conflict. You might stop other BLE services or combine them into one script if needed. Also, double-check that the Pi’s BlueZ version is new enough (5.48+). The QiMata demo’s instructions indicate the BlueZero-based BLE server is known to work on Raspberry Pi OS; the same environment should support the beacon as well.

* **Verifying the Broadcast:** Use a known scanner app to see if the beacon is actually advertising. If you don’t see the beacon on any scanner, try using `sudo hcitool lescan` (for scanning devices) or better `sudo btmgmt find`. Note that `hcitool` won’t directly show iBeacon payloads, but it will confirm that the device is advertising (you’d see an entry for a device with no name or “(unknown)” which is your beacon’s MAC). Using `sudo btmon` (Bluetooth monitor) is another advanced step – it can capture BLE packets and you can inspect if the advertisement contains the correct data bytes.

* **Correct Format:** Remember that iBeacon advertisements must include the correct prefix and length. In our code, we used the proper `0x02 0x15` prefix and Apple’s company ID. If you modify the UUID or other values, keep the overall payload length at 21 bytes after the 0x02 0x15. An incorrect length byte or missing prefix will result in the app not recognizing it as an iBeacon. The example code above handles this for you. (If you ever craft it manually via `hcitool` commands, be careful with byte ordering and counts.)

* **Range and RSSI:** If the app sees the beacon but RSSI is very low or intermittent, consider the distance and obstructions. The TX Power value in the packet doesn’t affect the transmission power; it’s only a calibration value. To improve range, ensure the Pi’s antenna (built-in or USB dongle) isn’t obstructed. In software, you can’t directly boost beyond the adapter’s capability, but you can set the advertisement interval slower to possibly slightly improve discoverability (not usually necessary).

By following the steps above, your Raspberry Pi should be successfully acting as a BLE iBeacon, broadcasting the expected UUID, Major, and Minor, so that the .NET MAUI demo app can detect and utilize the beacon signal. With the beacon advertising correctly, you can run the MobileIoT app’s beacon scanner page and observe the advertisements being received in real time, confirming the end-to-end functionality of the demo. Good luck!

**Sources:** The iBeacon format and parameters are based on the QiMata/mobileiot demo repository and Apple’s iBeacon specification. The Python setup and code leverage the BlueZ/BlueZero libraries as recommended in the project docs and are aligned with known examples for creating iBeacon transmitters on Raspberry Pi.
