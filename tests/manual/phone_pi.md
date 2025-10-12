# Phone ↔ Raspberry Pi Manual Tests

These tests validate the MAUI application when paired with a Raspberry Pi that
runs the demo scripts located in `src/pi`. Execute the tests in order so that
shared setup (Bluetooth pairing, USB gadget mode, wiring) stays intact.

## Hardware and software prerequisites

- One Android phone with the MobileIoT app installed from the current branch
  build. (iOS works for BLE/NFC but USB gadget and audio modem steps are
  Android-specific.)
- One Raspberry Pi 4 or newer with Raspberry Pi OS Bookworm or Bullseye.
- Pi accessories:
  - DHT22 sensor wired to GPIO4 for the BLE demo.
  - Single LED with resistor on GPIO17 for BLE and serial demos.
  - TRRS audio cable connecting the Pi headphone jack to the phone microphone
    input for the audio telemetry demo.
  - USB-C data cable for gadget-mode serial/bulk demos.
- Pi configured with:
  - Bluetooth and Wi-Fi enabled.
  - `bluezero`, `Adafruit_DHT`, `RPi.GPIO`, and `minimodem` installed
    (`pip install -r src/pi/requirements.txt`).
  - Serial gadget support enabled (`dtoverlay=dwc2,dr_mode=peripheral` and
    modules `libcomposite`, `g_serial`, `g_zero` available).
- Ensure both devices use the same time zone to simplify telemetry correlation.

## Common setup checklist

1. Update the repository on the Pi and this workstation to the same commit.
2. Copy the contents of `src/pi` to the Raspberry Pi (or pull from Git).
3. On the Pi, create a Python virtual environment and install requirements:
   ```bash
   cd ~/mobileiot/src/pi
   python3 -m venv .venv
   source .venv/bin/activate
   pip install -r requirements.txt
   ```
4. Confirm Bluetooth is enabled (`sudo systemctl status bluetooth`).
5. Verify the DHT22 sensor and LED are wired correctly and the TRRS cable is
   unplugged until the audio test to avoid feedback.
6. Deploy the MobileIoT app build to the phone and grant requested permissions
   (location, microphone, nearby devices, NFC, camera).

## Test cases

### PI-BLE-01 – BLE sensor telemetry handshake

**Objective:** Validate that the MAUI app discovers the Pi BLE peripheral and
reads temperature/humidity values from `bluetoothle_demo.py`.

**Prerequisites:**
- Pi shell with virtual environment activated.
- Phone and Pi within 2 meters.

**Steps:**
1. On the Pi, start the BLE demo:
   ```bash
   python bluetoothle_demo.py
   ```
2. On the phone, open the **BLE Demo** page and tap **Connect**.
3. Once connected, tap **Refresh Sensor** twice, five seconds apart.
4. Observe the temperature and humidity values displayed on screen.

**Expected results:**
- The connection status changes to “Connected” within 30 seconds.
- Temperature and humidity fields update with non-zero values that are stable
  between reads (differences < 1°C and < 5% RH unless the environment changes).
- No error toast or log entries appear.

**Cleanup:** Leave the BLE demo running for the next test.

### PI-BLE-02 – BLE LED control loopback

**Objective:** Ensure the app toggles the Pi LED characteristic and the Pi
responds appropriately.

**Prerequisites:**
- PI-BLE-01 completed successfully with the BLE demo still running.
- Physical view of the LED connected to GPIO17.

**Steps:**
1. On the phone, tap **Toggle LED** repeatedly five times, waiting one second
   between taps.
2. Watch the physical LED and the on-screen LED status text.
3. Use the Pi terminal to confirm log output indicates LED changes.

**Expected results:**
- The LED turns on and off in sync with each tap.
- The app button text alternates between “Turn LED On/Off” and status label
  mirrors the LED state.
- The Pi console logs `LED turned ON`/`LED turned OFF` messages.

### PI-BLE-03 – BLE reconnection & error recovery

**Objective:** Verify the app handles disconnections and connection failures.

**Prerequisites:**
- BLE demo is still running from prior tests.

**Steps:**
1. On the phone, tap **Disconnect**.
2. Stop the BLE script with `Ctrl+C`.
3. Attempt to reconnect from the app (tap **Connect**). Wait for the failure.
4. Restart the BLE script on the Pi.
5. Tap **Connect** again and confirm reconnection.
6. Trigger one more sensor read to ensure telemetry resumes.

**Expected results:**
- After step 1 the status changes to “Disconnected”.
- Step 3 shows “Connection failed” within 30 seconds and no crash occurs.
- After restarting the script, the app reconnects and sensor refresh succeeds.

### PI-BEACON-01 – Beacon advertisement detection

**Objective:** Confirm the app discovers iBeacon advertisements emitted by the
Pi running `beacon_demo.py`.

**Prerequisites:**
- BLE stack idle (stop the GATT demo).

**Steps:**
1. On the Pi, run `python beacon_demo.py`.
2. On the phone, open **Beacon Scanner**.
3. Wait up to one minute for the device list to populate.
4. Move one meter away and back to observe RSSI changes.

**Expected results:**
- A device entry with UUID `12345678-1234-1234-1234-1234567890AB` appears.
- RSSI values update as you change distance, roughly matching proximity.
- The app UI stays responsive while the Pi console logs remain quiet (beacon
  script prints start message only).

### PI-SERIAL-01 – USB serial LED toggling

**Objective:** Validate USB CDC ACM communication between the phone and
`serial_demo.py`.

**Prerequisites:**
- Pi configured for gadget mode with `g_serial` loaded (`sudo modprobe g_serial`).
- USB-C cable connected between devices.

**Steps:**
1. On the Pi, run `./run_serial_demo.sh` (or activate venv and run
   `python serial_demo.py --port /dev/ttyGS0`).
2. On the phone, open the **USB Serial** page and tap **Connect**.
3. Tap **Send Command** three times, pausing two seconds between taps.
4. Watch the LED on GPIO17 and the on-screen log.

**Expected results:**
- App log shows “Connected” then alternating `TX: LED_ON/LED_OFF` entries.
- Pi console prints matching “Received” lines and ACK responses.
- Physical LED toggles accordingly.

### PI-USB-01 – USB bulk ping echo

**Objective:** Exercise the bulk transfer ping using the Linux `g_zero`
gadget that echoes payloads in hardware.

**Prerequisites:**
- `g_zero` module loaded on the Pi (`sudo modprobe g_zero`).
- Devices still connected over USB-C.

**Steps:**
1. On the phone, open the **USB Bulk** page.
2. Tap **Connect** to list devices and establish the session.
3. Tap **Send Ping** twice.
4. Observe the log output.

**Expected results:**
- The app log displays “Connected to …” followed by `RX 64 bytes` entries for
  each ping.
- No Python script is required; the Pi does not need additional terminals for
  this test.

### PI-AUDIO-01 – Audio jack telemetry ingest

**Objective:** Verify that the phone decodes temperature telemetry transmitted
through the Pi audio demo.

**Prerequisites:**
- TRRS cable connected from Pi audio output to phone microphone input.
- Phone volume at 70% and microphone permissions granted.

**Steps:**
1. On the Pi, stop other audio applications and run `python audio_demo.py`.
2. On the phone, open **Audio Modem** and tap **Start**.
3. Monitor the status label for at least three telemetry updates.
4. Tap **Stop** and then terminate the Pi script with `Ctrl+C`.

**Expected results:**
- The status label transitions from “Idle” → “Listening…” → telemetry readings
  like `42.1` every ~5 seconds.
- Values match the Pi CPU temperature reported in the terminal (if printed) or
  stay within plausible range (30–70°C).
- Stopping the app halts updates and the Pi script exits cleanly on `Ctrl+C`.

## Post-test cleanup

- Stop all Python scripts and unload gadget modules (`sudo modprobe -r g_serial
  g_zero`).
- Disconnect peripherals, restore normal Pi audio routing, and document any
  deviations encountered during the run.
