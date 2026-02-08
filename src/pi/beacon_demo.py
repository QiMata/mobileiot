import time
import uuid as uuidlib
from bluezero import broadcaster
import Adafruit_DHT

# iBeacon parameters
BEACON_UUID = uuidlib.UUID("12345678-1234-1234-1234-1234567890AB")
TX_POWER = -59  # Measured RSSI at 1m, in dBm

# DHT22 sensor setup
DHT_SENSOR = Adafruit_DHT.DHT22
DHT_PIN = 4
UPDATE_INTERVAL = 5  # seconds between beacon updates


def read_dht22():
    """Read temperature and humidity from DHT22 sensor."""
    humidity, temperature = Adafruit_DHT.read_retry(DHT_SENSOR, DHT_PIN)
    if temperature is None:
        temperature = 0.0
    if humidity is None:
        humidity = 0.0
    return temperature, humidity


def build_ibeacon_data(temp, hum):
    """Build iBeacon manufacturer data with sensor values encoded in major/minor.

    Major = int16(temp * 100) big-endian  (e.g., 23.45C -> 2345)
    Minor = uint16(hum * 100) big-endian  (e.g., 55.7% -> 5570)
    """
    uuid_bytes = BEACON_UUID.bytes
    major_bytes = int(temp * 100).to_bytes(2, byteorder='big', signed=True)
    minor_bytes = int(hum * 100).to_bytes(2, byteorder='big')
    tx_byte = (TX_POWER & 0xFF).to_bytes(1, byteorder='big')
    return b'\x02\x15' + uuid_bytes + major_bytes + minor_bytes + tx_byte


print("Telemetry beacon starting (UUID=%s)..." % BEACON_UUID)
print("Major=temp*100, Minor=hum*100. Update interval=%ds" % UPDATE_INTERVAL)

beacon = None
try:
    while True:
        temp, hum = read_dht22()
        data = build_ibeacon_data(temp, hum)

        # Stop previous beacon if running
        if beacon is not None:
            beacon.stop_beacon()

        beacon = broadcaster.Beacon()
        beacon.add_manufacturer_data('4c00', data)
        beacon.start_beacon()
        print(f"Advertising: temp={temp:.1f}C (major={int(temp*100)}), "
              f"hum={hum:.1f}% (minor={int(hum*100)})")
        time.sleep(UPDATE_INTERVAL)
except KeyboardInterrupt:
    if beacon is not None:
        beacon.stop_beacon()
    print("Telemetry beacon stopped.")
