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
