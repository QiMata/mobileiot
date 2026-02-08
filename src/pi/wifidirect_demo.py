"""Wi-Fi Direct (P2P) telemetry + file transfer server for Raspberry Pi.

After a Wi-Fi Direct group is formed between the mobile device and the Pi,
the mobile app connects to the group owner on TCP port 8988. This script
serves JSON telemetry streams and file transfer requests.

Protocol: 4-byte big-endian length prefix + UTF-8 JSON payload.

Message types (by "type" field):
  start_telemetry  (mobile->Pi)  - Start streaming sensor data
  stop_telemetry   (mobile->Pi)  - Stop streaming
  telemetry        (Pi->mobile)  - Sensor data payload (every 2s)
  file_list        (mobile->Pi)  - Request directory listing
  file_list_response (Pi->mobile) - Directory listing response
  file_download    (mobile->Pi)  - Request file contents
  file_data        (Pi->mobile)  - File contents (base64)

Prerequisites
-------------
1. Enable Wi-Fi Direct on the Pi:
       sudo wpa_cli -i wlan0 p2p_find
       sudo wpa_cli -i wlan0 p2p_connect <mobile-mac> pbc

2. Run this script after the P2P group is established:
       python3 wifidirect_demo.py
"""

import socket
import threading
import json
import struct
import time
import os
import base64
import Adafruit_DHT

PORT = 8988
DHT_SENSOR = Adafruit_DHT.DHT22
DHT_PIN = 4
SHARE_DIR = "/home/pi/data"


def recv_exact(conn, n):
    """Receive exactly n bytes from socket."""
    buf = b""
    while len(buf) < n:
        chunk = conn.recv(n - len(buf))
        if not chunk:
            return None
        buf += chunk
    return buf


def send_msg(conn, obj):
    """Send a length-prefixed JSON message."""
    payload = json.dumps(obj).encode('utf-8')
    conn.sendall(struct.pack('>I', len(payload)) + payload)


def recv_msg(conn):
    """Receive a length-prefixed JSON message. Returns dict or None."""
    header = recv_exact(conn, 4)
    if not header:
        return None
    length = struct.unpack('>I', header)[0]
    if length > 10 * 1024 * 1024:  # 10MB sanity limit
        return None
    data = recv_exact(conn, length)
    if not data:
        return None
    return json.loads(data.decode('utf-8'))


def read_system_status():
    """Read system uptime and CPU temperature."""
    uptime = 0
    cpu_temp = 0.0
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
    return uptime, cpu_temp


def telemetry_thread(conn, stop_event):
    """Send telemetry data every 2 seconds until stopped."""
    while not stop_event.is_set():
        try:
            humidity, temperature = Adafruit_DHT.read_retry(DHT_SENSOR, DHT_PIN)
            if temperature is None:
                temperature = 0.0
            if humidity is None:
                humidity = 0.0
            uptime, cpu_temp = read_system_status()

            msg = {
                "type": "telemetry",
                "ts": time.time(),
                "temp": round(temperature, 2),
                "hum": round(humidity, 2),
                "cpu_temp": round(cpu_temp, 1),
                "uptime": uptime
            }
            send_msg(conn, msg)
        except (BrokenPipeError, ConnectionResetError, OSError):
            break
        stop_event.wait(2)


def handle_file_list(conn, msg):
    """Handle file_list request."""
    path = msg.get("path", SHARE_DIR)
    if not os.path.isdir(path):
        path = SHARE_DIR
    files = []
    try:
        os.makedirs(path, exist_ok=True)
        for name in os.listdir(path):
            full = os.path.join(path, name)
            if os.path.isfile(full):
                files.append({"name": name, "size": os.path.getsize(full)})
    except OSError:
        pass
    send_msg(conn, {"type": "file_list_response", "files": files})


def handle_file_download(conn, msg):
    """Handle file_download request."""
    path = msg.get("path", "")
    # Security: only allow files under SHARE_DIR
    full = os.path.realpath(os.path.join(SHARE_DIR, os.path.basename(path)))
    if not full.startswith(os.path.realpath(SHARE_DIR)):
        send_msg(conn, {"type": "file_data", "name": "", "size": 0, "data": ""})
        return
    if not os.path.isfile(full):
        send_msg(conn, {"type": "file_data", "name": "", "size": 0, "data": ""})
        return
    with open(full, "rb") as f:
        content = f.read()
    send_msg(conn, {
        "type": "file_data",
        "name": os.path.basename(full),
        "size": len(content),
        "data": base64.b64encode(content).decode('ascii')
    })


def handle_client(conn, addr):
    """Handle a single connected peer."""
    print(f"Peer connected: {addr}")
    streaming = False
    stop_event = threading.Event()
    tel_thread = None

    try:
        while True:
            msg = recv_msg(conn)
            if msg is None:
                break

            msg_type = msg.get("type", "")
            print(f"Received: {msg_type} from {addr}")

            if msg_type == "start_telemetry":
                if not streaming:
                    streaming = True
                    stop_event.clear()
                    tel_thread = threading.Thread(
                        target=telemetry_thread, args=(conn, stop_event), daemon=True)
                    tel_thread.start()

            elif msg_type == "stop_telemetry":
                if streaming:
                    stop_event.set()
                    streaming = False

            elif msg_type == "file_list":
                handle_file_list(conn, msg)

            elif msg_type == "file_download":
                handle_file_download(conn, msg)

    except (ConnectionResetError, BrokenPipeError):
        pass
    finally:
        stop_event.set()
        conn.close()
        print(f"Peer disconnected: {addr}")


def main():
    os.makedirs(SHARE_DIR, exist_ok=True)
    srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind(("0.0.0.0", PORT))
    srv.listen(1)
    print(f"Wi-Fi Direct telemetry server listening on port {PORT}...")
    print(f"Serving files from: {SHARE_DIR}")

    try:
        while True:
            conn, addr = srv.accept()
            t = threading.Thread(target=handle_client, args=(conn, addr), daemon=True)
            t.start()
    except KeyboardInterrupt:
        print("Shutting down...")
    finally:
        srv.close()


if __name__ == "__main__":
    main()
