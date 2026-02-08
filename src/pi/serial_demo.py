import serial
import time
import os
import RPi.GPIO as GPIO
import Adafruit_DHT

LED_PIN = 17
DHT_SENSOR = Adafruit_DHT.DHT22
DHT_PIN = 4
VALID_PINS = {4, 17, 18, 22, 23, 24, 25, 27}

GPIO.setmode(GPIO.BCM)
GPIO.setup(LED_PIN, GPIO.OUT, initial=GPIO.LOW)
_output_pins = {LED_PIN}

ser = serial.Serial('/dev/ttyGS0', 9600, timeout=1)
print("Waiting for commands on /dev/ttyGS0...")


def read_system_status():
    uptime = 0
    cpu_temp = 0.0
    mem_free = 0
    try:
        with open('/proc/uptime') as f:
            uptime = int(float(f.read().split()[0]))
    except Exception:
        pass
    try:
        with open('/sys/class/thermal/thermal_zone0/temp') as f:
            cpu_temp = int(f.read().strip()) / 1000.0
    except Exception:
        pass
    try:
        with open('/proc/meminfo') as f:
            for line in f:
                if line.startswith('MemFree:'):
                    mem_free = int(line.split()[1])
                    break
    except Exception:
        pass
    return uptime, cpu_temp, mem_free


def process_command(line):
    if not line:
        return
    print(f"Received: {line}")
    parts = line.split()
    cmd = parts[0]

    if cmd == "LED_ON":
        GPIO.output(LED_PIN, GPIO.HIGH)
        ser.write(b"ACK: LED turned ON\n")
    elif cmd == "LED_OFF":
        GPIO.output(LED_PIN, GPIO.LOW)
        ser.write(b"ACK: LED turned OFF\n")
    elif cmd == "GPIO_READ" and len(parts) == 2:
        try:
            pin = int(parts[1])
        except ValueError:
            ser.write(f"NACK: Invalid pin {parts[1]}\n".encode())
            return
        if pin not in VALID_PINS:
            ser.write(f"NACK: Invalid pin {pin}\n".encode())
            return
        if pin not in _output_pins:
            GPIO.setup(pin, GPIO.IN)
        val = GPIO.input(pin)
        ser.write(f"GPIO:{pin}={val}\n".encode())
    elif cmd == "GPIO_WRITE" and len(parts) == 3:
        try:
            pin = int(parts[1])
            val = int(parts[2])
        except ValueError:
            ser.write(b"NACK: Invalid arguments\n")
            return
        if pin not in VALID_PINS:
            ser.write(f"NACK: Invalid pin {pin}\n".encode())
            return
        if val not in (0, 1):
            ser.write(f"NACK: Invalid value {val}\n".encode())
            return
        if pin not in _output_pins:
            GPIO.setup(pin, GPIO.OUT)
            _output_pins.add(pin)
        GPIO.output(pin, val)
        ser.write(f"ACK: GPIO {pin} set to {val}\n".encode())
    elif cmd == "SENSOR_READ":
        humidity, temperature = Adafruit_DHT.read_retry(DHT_SENSOR, DHT_PIN)
        if temperature is None:
            temperature = 0.0
        if humidity is None:
            humidity = 0.0
        ser.write(f"SENSOR:temp={temperature:.1f},hum={humidity:.1f}\n".encode())
    elif cmd == "STATUS":
        uptime, cpu_temp, mem_free = read_system_status()
        ser.write(f"STATUS:uptime={uptime},cpu_temp={cpu_temp:.1f},mem_free={mem_free}\n".encode())
    else:
        ser.write(b"NACK: Unknown command\n")


buf = b""

try:
    while True:
        if ser.in_waiting:
            buf += ser.read(ser.in_waiting)
            # Process complete lines (delimited by \n)
            while b"\n" in buf:
                line, buf = buf.split(b"\n", 1)
                process_command(line.decode('ascii', errors='ignore').strip())
        elif buf:
            # No more data arriving and buffer has content — treat as complete command
            line = buf.decode('ascii', errors='ignore').strip()
            buf = b""
            if line:
                process_command(line)
        time.sleep(0.05)
finally:
    ser.close()
    GPIO.cleanup()
