import subprocess
import time


def read_cpu_temp():
    """Read the Pi CPU temperature in Celsius."""
    with open('/sys/class/thermal/thermal_zone0/temp') as f:
        return float(f.read().strip()) / 1000.0


def transmit(value: float):
    """Transmit a numeric value as FSK tones via minimodem."""
    msg = f"{value:.1f}\n"
    subprocess.run(['minimodem', '--tx', '--quiet', '1200'], input=msg.encode(), check=False)


def main():
    print("Audio telemetry started. Press Ctrl+C to stop.")
    try:
        while True:
            temp = read_cpu_temp()
            transmit(temp)
            time.sleep(5)
    except KeyboardInterrupt:
        pass


if __name__ == '__main__':
    main()
