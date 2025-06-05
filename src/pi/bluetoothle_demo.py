import time
import Adafruit_DHT  # Library for DHT22 sensor (ensure it's installed)
import RPi.GPIO as GPIO  # GPIO library for LED control

# BlueZero library for BLE peripheral (install via pip if needed)
from bluezero import adapter, peripheral, async_tools

# BLE UUIDs (must match the mobile app expectations)
SERVICE_UUID    = "12345678-1234-1234-1234-1234567890AB"
TEMP_CHAR_UUID  = "00002A6E-0000-1000-8000-00805F9B34FB"  # Temperature (16-bit, °C*100)
HUM_CHAR_UUID   = "00002A6F-0000-1000-8000-00805F9B34FB"  # Humidity (16-bit, %*100)
LED_CHAR_UUID   = "12345679-1234-1234-1234-1234567890AB"  # LED control (1 byte)

# Hardware setup: define sensor and LED pins
DHT_SENSOR = Adafruit_DHT.DHT22
DHT_PIN    = 4    # BCM GPIO4 for DHT22 data line (with 10k pull-up to 3.3V)
LED_PIN    = 17   # BCM GPIO17 for LED (wired with a resistor to ground)

# Initialize GPIO for LED
GPIO.setmode(GPIO.BCM)
GPIO.setup(LED_PIN, GPIO.OUT, initial=GPIO.LOW)

def read_dht22():
    """Read temperature and humidity from DHT22 sensor."""
    humidity, temperature = Adafruit_DHT.read_retry(DHT_SENSOR, DHT_PIN)
    return temperature, humidity

def temp_read_callback():
    """Return temperature in 2-byte little-endian format (Int16)."""
    temp, hum = read_dht22()
    # Convert float degrees C to hundredths and pack into 2 bytes
    int_temp = int(temp * 100)  # e.g., 23.45°C -> 2345
    # Convert to bytes (signed 16-bit little-endian)
    temp_bytes = int_temp.to_bytes(2, byteorder='little', signed=True)
    return [temp_bytes[0], temp_bytes[1]]

def hum_read_callback():
    """Return humidity in 2-byte little-endian format (UInt16)."""
    temp, hum = read_dht22()
    int_hum = int(hum * 100)  # e.g., 55.7% -> 5570
    # Convert to unsigned 16-bit little-endian bytes
    hum_bytes = int_hum.to_bytes(2, byteorder='little', signed=False)
    return [hum_bytes[0], hum_bytes[1]]

def led_write_callback(value):
    """Handle write to LED characteristic (turn LED on/off)."""
    GPIO.setup(LED_PIN, GPIO.OUT, initial=GPIO.LOW)
    if value and value[0] == 0x01:  # value is a list of bytes
        GPIO.output(LED_PIN, GPIO.HIGH)   # LED ON
    else:
        GPIO.output(LED_PIN, GPIO.LOW)    # LED OFF

if __name__ == "__main__":
    # Get the Bluetooth adapter address (hci0)
    adapter_addr = list(adapter.Adapter.available())[0].address
    # Create BLE Peripheral with a given name (advertised over BLE)
    ble_periph = peripheral.Peripheral(adapter_addr, local_name="PiSensor", appearance=0x0340)
    # Define the custom service and its characteristics:
    ble_periph.add_service(srv_id=1, uuid=SERVICE_UUID, primary=True)
    # Temperature characteristic (read + notify)
    ble_periph.add_characteristic(srv_id=1, chr_id=1, uuid=TEMP_CHAR_UUID,
                                  value=[0x00, 0x00],  # initial 2-byte value
                                  notifying=False,
                                  flags=['read'],
                                  read_callback=temp_read_callback,
                                  write_callback=None,
                                  notify_callback=None)
    # Humidity characteristic (read + notify)
    ble_periph.add_characteristic(srv_id=1, chr_id=2, uuid=HUM_CHAR_UUID,
                                  value=[0x00, 0x00],
                                  notifying=False,
                                  flags=['read'],
                                  read_callback=hum_read_callback,
                                  write_callback=None,
                                  notify_callback=None)
    # LED characteristic (read + write)
    ble_periph.add_characteristic(srv_id=1, chr_id=3, uuid=LED_CHAR_UUID,
                                  value=[0x00],  # LED off initially
                                  notifying=False,
                                  flags=['write'],
                                  read_callback=None,
                                  write_callback=led_write_callback)

    # Publish the service and begin advertising
    ble_periph.publish()
    print("BLE GATT server started (Advertising as 'PiSensor'). Press Ctrl+C to exit.")
    try:
        # Keep the main thread alive to allow background BLE events
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        print("Stopping GATT server...")
        try:
            ble_periph.quit()  # preferred (≥0.6)
        except AttributeError:
            ble_periph.unpublish()  # fallback for older Bluezero
        GPIO.cleanup()      # Clean up GPIO on exit
