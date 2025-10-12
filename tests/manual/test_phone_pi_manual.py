import pytest

from .manual_test_helper import ManualStep, ManualTestPlan


@pytest.mark.manual
def test_pi_ble_sensor_handshake(manual_test):
    plan = ManualTestPlan(
        identifier="PI-BLE-01",
        title="BLE sensor telemetry handshake",
        objective="Verify the phone discovers the Pi GATT server and retrieves sensor readings.",
        steps=[
            ManualStep("Start `python bluetoothle_demo.py` on the Raspberry Pi in the src/pi directory."),
            ManualStep("Open the BLE Demo page on the phone and tap Connect."),
            ManualStep("Refresh the sensor values twice, waiting roughly five seconds between taps."),
            ManualStep("Observe the temperature and humidity values shown in the app."),
        ],
        expected_result=(
            "Connection status switches to Connected within 30 seconds, and stable non-zero temperature and humidity readings appear "
            "without error toasts or log entries."
        ),
        references=["tests/manual/phone_pi.md#pi-ble-01-–-ble-sensor-telemetry-handshake"],
    )
    manual_test.execute(plan)


@pytest.mark.manual
def test_pi_ble_led_control(manual_test):
    plan = ManualTestPlan(
        identifier="PI-BLE-02",
        title="BLE LED control loopback",
        objective="Ensure the phone toggles the Pi LED characteristic and both sides log the change.",
        steps=[
            ManualStep("Confirm the BLE demo from PI-BLE-01 is still running on the Pi."),
            ManualStep("Tap Toggle LED on the BLE Demo page five times, leaving a one-second gap between taps."),
            ManualStep("Watch the physical LED attached to GPIO17 while monitoring the on-screen LED status."),
            ManualStep("Check the Pi terminal for LED state log messages."),
        ],
        expected_result=(
            "The physical LED and app status text switch in sync for each tap, and the Pi console records matching 'LED turned ON/OFF' entries."
        ),
        references=["tests/manual/phone_pi.md#pi-ble-02-–-ble-led-control-loopback"],
    )
    manual_test.execute(plan)


@pytest.mark.manual
def test_pi_ble_reconnection(manual_test):
    plan = ManualTestPlan(
        identifier="PI-BLE-03",
        title="BLE reconnection and error recovery",
        objective="Validate disconnect handling and recovery after the GATT server restarts.",
        steps=[
            ManualStep("From the app, tap Disconnect."),
            ManualStep("Stop the BLE script on the Pi with Ctrl+C."),
            ManualStep("Attempt to reconnect from the phone and wait for the failure state."),
            ManualStep("Restart `python bluetoothle_demo.py` on the Pi."),
            ManualStep("Tap Connect again, then perform another sensor refresh."),
        ],
        expected_result=(
            "Status changes to Disconnected after the first step, the failed reconnect surfaces a connection failed message without crashes, and "
            "after restarting the script the app reconnects and resumes telemetry refresh successfully."
        ),
        references=["tests/manual/phone_pi.md#pi-ble-03-–-ble-reconnection--error-recovery"],
    )
    manual_test.execute(plan)


@pytest.mark.manual
def test_pi_beacon_detection(manual_test):
    plan = ManualTestPlan(
        identifier="PI-BEACON-01",
        title="Beacon advertisement detection",
        objective="Confirm iBeacon advertisements from the Pi appear in the phone scanner UI.",
        steps=[
            ManualStep("Stop the BLE GATT demo if it is still running."),
            ManualStep("Launch `python beacon_demo.py` on the Pi."),
            ManualStep("Open the Beacon Scanner page on the phone."),
            ManualStep("Wait up to one minute for the advertisement to appear, then walk about a meter away and back."),
        ],
        expected_result=(
            "The scanner lists the beacon UUID 12345678-1234-1234-1234-1234567890AB and RSSI updates reflect distance changes while the UI stays responsive."
        ),
        references=["tests/manual/phone_pi.md#pi-beacon-01-–-beacon-advertisement-detection"],
    )
    manual_test.execute(plan)


@pytest.mark.manual
def test_pi_usb_serial_led(manual_test):
    plan = ManualTestPlan(
        identifier="PI-SERIAL-01",
        title="USB serial LED toggling",
        objective="Validate CDC ACM messaging between the app and `serial_demo.py`.",
        steps=[
            ManualStep("Load the g_serial gadget on the Pi and start ./run_serial_demo.sh or python serial_demo.py --port /dev/ttyGS0."),
            ManualStep("Open the USB Serial page on the phone and tap Connect."),
            ManualStep("Tap Send Command three times with two-second pauses."),
            ManualStep("Observe the physical LED and app log entries."),
        ],
        expected_result=(
            "App log shows connection confirmation and alternating TX/RX entries, the Pi terminal prints matching receive/ACK lines, and the GPIO17 LED toggles."
        ),
        references=["tests/manual/phone_pi.md#pi-serial-01-–-usb-serial-led-toggling"],
    )
    manual_test.execute(plan)


@pytest.mark.manual
def test_pi_usb_bulk_ping(manual_test):
    plan = ManualTestPlan(
        identifier="PI-USB-01",
        title="USB bulk ping echo",
        objective="Ensure the USB bulk demo exchanges payloads using the g_zero gadget.",
        steps=[
            ManualStep("Load the g_zero kernel module on the Pi and keep the USB-C link connected."),
            ManualStep("Open the USB Bulk page on the phone."),
            ManualStep("Tap Connect, then Send Ping twice."),
            ManualStep("Review the log output."),
        ],
        expected_result=(
            "The app log indicates a successful connection followed by RX 64 bytes entries for each ping without requiring a Python helper script."
        ),
        references=["tests/manual/phone_pi.md#pi-usb-01-–-usb-bulk-ping-echo"],
    )
    manual_test.execute(plan)


@pytest.mark.manual
def test_pi_audio_modem(manual_test):
    plan = ManualTestPlan(
        identifier="PI-AUDIO-01",
        title="Audio jack telemetry ingest",
        objective="Verify the audio modem demo decodes telemetry from the Pi over the TRRS connection.",
        steps=[
            ManualStep("Connect the TRRS audio cable between the Pi and phone, ensuring phone microphone permission is granted."),
            ManualStep("Run python audio_demo.py on the Pi with other audio apps closed."),
            ManualStep("Open Audio Modem on the phone, tap Start, and monitor at least three telemetry updates."),
            ManualStep("Tap Stop in the app and terminate the Pi script with Ctrl+C."),
        ],
        expected_result=(
            "Status transitions from Idle to Listening… and displays periodic CPU temperature readings within a realistic range until Stop is pressed, after which updates halt and the Pi script exits cleanly."
        ),
        references=["tests/manual/phone_pi.md#pi-audio-01-–-audio-jack-telemetry-ingest"],
    )
    manual_test.execute(plan)
