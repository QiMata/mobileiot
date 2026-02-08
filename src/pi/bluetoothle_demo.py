import time
import threading
import Adafruit_DHT  # Library for DHT22 sensor (ensure it's installed)
import RPi.GPIO as GPIO  # GPIO library for LED control

# BlueZero library for BLE peripheral (install via pip if needed)
from bluezero import adapter, peripheral

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

# Notification state
_notify_temp = False
_notify_hum = False

def read_dht22():
    """Read temperature and humidity from DHT22 sensor."""
    humidity, temperature = Adafruit_DHT.read_retry(DHT_SENSOR, DHT_PIN)
    if temperature is None:
        temperature = 0.0
    if humidity is None:
        humidity = 0.0
    return temperature, humidity

def temp_read_callback():
    """Return temperature in 2-byte little-endian format (Int16)."""
    temp, hum = read_dht22()
    int_temp = int(temp * 100)
    temp_bytes = int_temp.to_bytes(2, byteorder='little', signed=True)
    return [temp_bytes[0], temp_bytes[1]]

def hum_read_callback():
    """Return humidity in 2-byte little-endian format (Int16)."""
    temp, hum = read_dht22()
    int_hum = int(hum * 100)
    hum_bytes = int_hum.to_bytes(2, byteorder='little', signed=True)
    return [hum_bytes[0], hum_bytes[1]]

def led_write_callback(value):
    """Handle write to LED characteristic (turn LED on/off)."""
    if value and value[0] == 0x01:
        GPIO.output(LED_PIN, GPIO.HIGH)
    else:
        GPIO.output(LED_PIN, GPIO.LOW)

def temp_notify_callback(notifying, characteristic):
    """Called when a client subscribes/unsubscribes to temperature notifications."""
    global _notify_temp
    _notify_temp = notifying

def hum_notify_callback(notifying, characteristic):
    """Called when a client subscribes/unsubscribes to humidity notifications."""
    global _notify_hum
    _notify_hum = notifying

def notification_loop(periph):
    """Background thread that pushes sensor data every 2s when notifications are active."""
    while True:
        if _notify_temp or _notify_hum:
            temp, hum = read_dht22()
            if _notify_temp:
                int_temp = int(temp * 100)
                periph.update_value(srv_id=1, chr_id=1,
                    value=list(int_temp.to_bytes(2, 'little', signed=True)))
            if _notify_hum:
                int_hum = int(hum * 100)
                periph.update_value(srv_id=1, chr_id=2,
                    value=list(int_hum.to_bytes(2, 'little', signed=True)))
        time.sleep(2)

if __name__ == "__main__":
    adapter_addr = list(adapter.Adapter.available())[0].address
    ble_periph = peripheral.Peripheral(adapter_addr, local_name="PiSensor", appearance=0x0340)
    ble_periph.add_service(srv_id=1, uuid=SERVICE_UUID, primary=True)
    # Temperature characteristic (read + notify)
    ble_periph.add_characteristic(srv_id=1, chr_id=1, uuid=TEMP_CHAR_UUID,
                                  value=[0x00, 0x00],
                                  notifying=False,
                                  flags=['read', 'notify'],
                                  read_callback=temp_read_callback,
                                  write_callback=None,
                                  notify_callback=temp_notify_callback)
    # Humidity characteristic (read + notify)
    ble_periph.add_characteristic(srv_id=1, chr_id=2, uuid=HUM_CHAR_UUID,
                                  value=[0x00, 0x00],
                                  notifying=False,
                                  flags=['read', 'notify'],
                                  read_callback=hum_read_callback,
                                  write_callback=None,
                                  notify_callback=hum_notify_callback)
    # LED characteristic (write)
    ble_periph.add_characteristic(srv_id=1, chr_id=3, uuid=LED_CHAR_UUID,
                                  value=[0x00],
                                  notifying=False,
                                  flags=['write'],
                                  read_callback=None,
                                  write_callback=led_write_callback)

    # Start notification background thread
    t = threading.Thread(target=notification_loop, args=(ble_periph,), daemon=True)
    t.start()

    ble_periph.publish()
    print("BLE GATT server started (Advertising as 'PiSensor'). Press Ctrl+C to exit.")
    print("Notifications enabled for Temperature and Humidity (2s interval when subscribed).")
    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        print("Stopping GATT server...")
        try:
            ble_periph.quit()
        except AttributeError:
            ble_periph.unpublish()
        GPIO.cleanup()
