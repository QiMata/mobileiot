"""NFC provisioning receiver for Raspberry Pi with PN532 HAT.

Listens for NDEF text records containing JSON provisioning data.
When a provisioning payload is received, applies Wi-Fi and device config.

Expected NDEF text record JSON format:
{
    "type": "provision",
    "wifi_ssid": "MyNetwork",
    "wifi_pass": "MyPassword",
    "wifi_security": "WPA2",
    "device_name": "PiSensor-Kitchen",
    "mqtt_broker": "192.168.1.100",
    "mqtt_port": 1883
}

Prerequisites:
  pip install ndeflib adafruit-circuitpython-pn532 adafruit-blinka
  Enable I2C: sudo raspi-config -> Interface Options -> I2C

Wiring: PN532 connected via I2C (SDA=GPIO2, SCL=GPIO3)

Run:
  sudo python3 nfc_provision_demo.py
"""

import json
import time
import subprocess
import os

import board
import busio
import adafruit_pn532.i2c as pn532_i2c
import ndef

# Initialize I2C and PN532
i2c = busio.I2C(board.SCL, board.SDA)
pn = pn532_i2c.PN532_I2C(i2c, debug=False)
ic, ver, rev, support = pn.firmware_version
print(f"PN532 firmware: {ver}.{rev}")
pn.SAM_configuration()

print("NFC Provisioning receiver ready. Tap phone/tag...")


def apply_wifi_config(ssid, password, security="WPA2"):
    """Write wpa_supplicant configuration."""
    key_mgmt = "WPA-PSK"
    if security == "Open":
        key_mgmt = "NONE"

    config = f"""ctrl_interface=DIR=/var/run/wpa_supplicant GROUP=netdev
update_config=1
country=US

network={{
    ssid="{ssid}"
    {"psk=\"" + password + "\"" if key_mgmt != "NONE" else "key_mgmt=NONE"}
    key_mgmt={key_mgmt}
}}
"""
    with open("/etc/wpa_supplicant/wpa_supplicant.conf", "w") as f:
        f.write(config)
    subprocess.run(["wpa_cli", "-i", "wlan0", "reconfigure"], check=True)
    print(f"Wi-Fi configured for SSID: {ssid} ({security})")


def apply_hostname(name):
    """Set the Pi hostname."""
    subprocess.run(["hostnamectl", "set-hostname", name], check=True)
    print(f"Hostname set to: {name}")


def process_provision(data):
    """Parse and apply provisioning JSON."""
    try:
        config = json.loads(data)
    except json.JSONDecodeError as e:
        print(f"Invalid JSON: {e}")
        return False

    if config.get("type") != "provision":
        print(f"Unknown config type: {config.get('type')}")
        return False

    applied = []

    if "wifi_ssid" in config and "wifi_pass" in config:
        apply_wifi_config(
            config["wifi_ssid"],
            config["wifi_pass"],
            config.get("wifi_security", "WPA2"))
        applied.append("wifi")

    if "device_name" in config:
        apply_hostname(config["device_name"])
        applied.append("hostname")

    if "mqtt_broker" in config:
        # Save MQTT config to a JSON file for other services to read
        mqtt_config = {
            "broker": config["mqtt_broker"],
            "port": config.get("mqtt_port", 1883)
        }
        os.makedirs("/home/pi/data", exist_ok=True)
        with open("/home/pi/data/mqtt_config.json", "w") as f:
            json.dump(mqtt_config, f, indent=2)
        print(f"MQTT config saved: {mqtt_config}")
        applied.append("mqtt")

    print(f"Provisioning applied: {', '.join(applied)}")
    return True


while True:
    uid = pn.read_passive_target(timeout=0.5)
    if uid is None:
        continue

    print(f"Tag detected: {uid.hex()}")

    try:
        # Read NDEF data from NTAG/Mifare Ultralight (pages 4-39)
        raw = b""
        for page in range(4, 40):
            data = pn.ntag2xx_read_block(page)
            if data is None:
                break
            raw += bytes(data)

        # Parse NDEF records from raw bytes
        records = list(ndef.message_decoder(raw))
        for record in records:
            if hasattr(record, 'text'):
                print(f"NDEF text: {record.text}")
                process_provision(record.text)
                break
        else:
            print("No NDEF text record found on tag")

    except Exception as e:
        print(f"Error reading tag: {e}")

    time.sleep(2)  # Debounce - don't re-read the same tag immediately
