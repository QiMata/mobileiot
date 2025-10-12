import pytest

from .manual_test_helper import ManualStep, ManualTestPlan


@pytest.mark.manual
def test_phone_wifi_direct_ping(manual_test):
    plan = ManualTestPlan(
        identifier="PHONE-P2P-01",
        title="Wi-Fi Direct discovery and ping",
        objective="Confirm Phone A discovers Phone B via Wi-Fi Direct and successfully exchanges a ping.",
        steps=[
            ManualStep("Open Wi-Fi Direct settings on Phone B and leave the device discoverable."),
            ManualStep("On Phone A, open the Wi-Fi Direct demo page and trigger discovery."),
            ManualStep("Select Phone B from the peer list and initiate the connection."),
            ManualStep("After the connection completes, send a ping from Phone A."),
            ManualStep("Stop discovery to release the session."),
        ],
        expected_result=(
            "Phone B appears within 30 seconds, the connection succeeds without PIN prompts, the ping reports bytes sent/received, and stopping discovery closes the session cleanly."
        ),
        references=["tests/manual/two_phones.md#phone-p2p-01-–-wi-fi-direct-discovery-and-ping"],
    )
    manual_test.execute(plan)


@pytest.mark.manual
def test_phone_peer_message_delivery(manual_test):
    plan = ManualTestPlan(
        identifier="PHONE-P2P-02",
        title="Peer message delivery",
        objective="Verify text messages flow across an established Wi-Fi Direct link.",
        steps=[
            ManualStep("Ensure the Wi-Fi Direct connection from PHONE-P2P-01 remains active."),
            ManualStep("Send a short text such as 'mobileiot test' from Phone A using the SendToPeer command."),
            ManualStep("Confirm the message arrives on Phone B via logs or UI, then optionally send a reply."),
        ],
        expected_result=(
            "Message contents arrive exactly once without truncation, the link stays active for additional interactions, and empty messages are ignored without crashing."
        ),
        references=["tests/manual/two_phones.md#phone-p2p-02-–-peer-message-delivery"],
    )
    manual_test.execute(plan)


@pytest.mark.manual
def test_phone_nfc_tag_round_trip(manual_test):
    plan = ManualTestPlan(
        identifier="PHONE-NFC-01",
        title="NFC tag read/write round-trip",
        objective="Ensure NFC text data can be written from Phone A and read back successfully.",
        steps=[
            ManualStep("On Phone A, open the NFC page and enter 'Hello MobileIoT' as the write payload."),
            ManualStep("Tap Write to Tag and present a blank tag or Phone B in tag emulation mode until the write completes."),
            ManualStep("Clear the input box on Phone A."),
            ManualStep("Start scanning and present the programmed tag again."),
        ],
        expected_result=(
            "Write completes without errors, scanning shows 'Hello MobileIoT' in the Last Tag Content label, and duplicate logs only appear when a new tag is detected."
        ),
        references=["tests/manual/two_phones.md#phone-nfc-01-–-nfc-tag-readwrite-round-trip"],
    )
    manual_test.execute(plan)


@pytest.mark.manual
def test_phone_nfc_peer_handover(manual_test):
    plan = ManualTestPlan(
        identifier="PHONE-NFC-02",
        title="NFC peer-to-peer handover",
        objective="Validate the NFC peer-to-peer page pushes the default payload between devices.",
        steps=[
            ManualStep("Open the NFC Peer-to-Peer page on both phones."),
            ManualStep("On Phone A, tap the prompt to initiate the exchange."),
            ManualStep("Touch the phones back-to-back until haptic feedback indicates an NDEF push."),
            ManualStep("Accept the received payload on Phone B and exit the page on both devices."),
        ],
        expected_result=(
            "Phone B opens the Hello World payload in its default viewer, Phone A navigates back without error, and repeated exchanges continue working without an app restart."
        ),
        references=["tests/manual/two_phones.md#phone-nfc-02-–-nfc-peer-to-peer-handover"],
    )
    manual_test.execute(plan)


@pytest.mark.manual
def test_phone_beacon_detection(manual_test):
    plan = ManualTestPlan(
        identifier="PHONE-BEACON-01",
        title="Beacon detection via companion device",
        objective="Check that Phone A detects advertisements broadcast from Phone B's beacon simulator.",
        steps=[
            ManualStep("Configure Phone B's beacon simulator with UUID 12345678-1234-1234-1234-1234567890AB, major 1, minor 2, TX power -59."),
            ManualStep("Start the broadcast on Phone B."),
            ManualStep("Open the BLE Beacons page on Phone A and wait for the entry to appear."),
            ManualStep("Change the distance between devices to observe RSSI updates, then stop the broadcast."),
        ],
        expected_result=(
            "Phone A lists the configured beacon with live RSSI updates and removes the entry within a minute after the broadcast stops without leaving stale records."
        ),
        references=["tests/manual/two_phones.md#phone-beacon-01-–-beacon-detection-via-companion-device"],
    )
    manual_test.execute(plan)


@pytest.mark.manual
def test_phone_qr_scan_interop(manual_test):
    plan = ManualTestPlan(
        identifier="PHONE-VISION-01",
        title="QR scan interoperability smoke test",
        objective="Ensure the QR scanner reads codes generated on another device and the camera workflow remains stable afterward.",
        steps=[
            ManualStep("Generate a QR code on Phone B containing https://aka.ms/mobileiot."),
            ManualStep("On Phone A, open the Computer Vision page and start the QR scan."),
            ManualStep("Align the camera to read the QR code, then capture a photo to validate classification still works."),
        ],
        expected_result=(
            "QR result updates with the exact URL, the app continues functioning when switching back to photo capture, and classification results populate as expected."
        ),
        references=["tests/manual/two_phones.md#phone-vision-01-–-qr-scan-interoperability-smoke-test"],
    )
    manual_test.execute(plan)
