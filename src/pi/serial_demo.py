import serial
import time
import RPi.GPIO as GPIO

LED_PIN = 17
GPIO.setmode(GPIO.BCM)
GPIO.setup(LED_PIN, GPIO.OUT, initial=GPIO.LOW)

ser = serial.Serial('/dev/ttyGS0', 9600, timeout=1)
print("Waiting for commands on /dev/ttyGS0...")

try:
    while True:
        if ser.in_waiting:
            line = ser.readline().decode('ascii', errors='ignore').strip()
            if not line:
                continue
            print(f"Received: {line}")
            if line == "LED_ON":
                GPIO.output(LED_PIN, GPIO.HIGH)
                ser.write(b"ACK: LED turned ON\n")
            elif line == "LED_OFF":
                GPIO.output(LED_PIN, GPIO.LOW)
                ser.write(b"ACK: LED turned OFF\n")
            else:
                ser.write(b"NACK: Unknown command\n")
        time.sleep(0.1)
finally:
    ser.close()
    GPIO.cleanup()
