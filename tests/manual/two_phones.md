# Phone ↔ Phone Manual Tests

These tests cover features that require two mobile devices. Designate one device
as **Phone A** (test subject) running the MobileIoT app build under test and the
other as **Phone B** (companion) that provides NFC tags, beacon broadcasts, or
peer connectivity.

## Hardware and software prerequisites

- Two Android phones with NFC, Wi-Fi Direct, Bluetooth LE, and camera support.
  (iOS can substitute for beacon and NFC tag scenarios if you install equivalent
  tooling.)
- MobileIoT app installed on both devices from the same build.
- Optional helper apps on Phone B:
  - *Beacon Simulator* (Android) or *Locate Beacon* (iOS) to broadcast custom
    iBeacon frames.
  - *NFC Tools* to emulate tags or to write text records to blank NFC cards.
- Stable Wi-Fi environment so Wi-Fi Direct negotiations complete without severe
  interference.

## Common setup checklist

1. Enable developer options on both phones and allow installing from unknown
   sources if side-loading the test build.
2. Sign into the same Wi-Fi network (for logging convenience) but disable
   regular Wi-Fi during Wi-Fi Direct tests so only the peer-to-peer link is
   active.
3. Ensure Bluetooth and NFC radios are enabled on both devices.
4. Launch the MobileIoT app on Phone A and verify the main menu loads.
5. Keep both phones unlocked for the duration of testing.

## Test cases

### PHONE-P2P-01 – Wi-Fi Direct discovery and ping

**Objective:** Confirm Phone A can discover Phone B over Wi-Fi Direct and
perform a ping using the P2P service.

**Prerequisites:**
- QA build that exposes the Wi-Fi Direct test harness (toolbar buttons or
  developer menu that map to `P2pViewModel` commands). If using the stock UI,
  attach a debugger and invoke commands via the Live Visual Tree or `dotnet
  watch` hot reload buttons that bind to `DiscoverCommand`, `SendPingCommand`,
  and `StopDiscoveryCommand`.

**Steps:**
1. On Phone B, open Android settings → **Wi-Fi** → **Wi-Fi Direct** and keep the
   dialog visible so the device advertises itself.
2. On Phone A, navigate to the **Wi-Fi Direct** demo page and trigger
   **Discover**.
3. Wait for Phone B’s device ID to appear in the discovered peers list.
4. Select the peer and trigger **Connect**.
5. After the connection completes, invoke **Send Ping**.
6. Stop discovery to tear down the session.

**Expected results:**
- Discovery lists Phone B within 30 seconds.
- Connection succeeds without prompting for a PIN.
- Ping command succeeds (status log or toast indicates bytes sent and received).
- Stopping discovery closes the session without crashing the app.

### PHONE-P2P-02 – Peer message delivery

**Objective:** Verify arbitrary messages can be exchanged once the Wi-Fi Direct
link is established.

**Prerequisites:**
- PHONE-P2P-01 completed with devices paired.
- Mechanism to send custom text (debug menu calling `SendToPeerCommand`).

**Steps:**
1. With the Wi-Fi Direct connection active, prepare a short text like
   “mobileiot test”.
2. Trigger the **SendToPeer** action with Phone B’s peer ID and the text.
3. Observe logs or on-screen status on Phone B’s test harness (or use a packet
   capture app) to confirm receipt.
4. Repeat with Phone B sending a message back if its build includes the harness.

**Expected results:**
- Message appears exactly once on the receiving side without truncation.
- Connection stays active after the message and additional pings still work.
- Sending empty text is ignored (no crash, optional warning log).

### PHONE-NFC-01 – NFC tag read/write round-trip

**Objective:** Ensure NFC text tags can be written and read between the two
phones using the MobileIoT NFC page.

**Prerequisites:**
- Blank NFC tag or Phone B with NFC Tools set to **Write** text records.

**Steps:**
1. On Phone A, open the **NFC** page and enter `Hello MobileIoT` in the write
   text box.
2. Tap **Write to Tag** and place Phone A against the NFC tag or Phone B in tag
   emulation mode until Android confirms the write.
3. Clear the input box on Phone A.
4. Tap **Start Scan** and present the programmed tag (or Phone B in read mode).

**Expected results:**
- Write action completes without error and the tag contains the new string.
- Scanning updates the “Last Tag Content” label with `Hello MobileIoT`.
- Repeated scans append to the log only when a new tag is detected (no duplicate
  spam when the devices remain in contact).

### PHONE-NFC-02 – NFC peer-to-peer handover

**Objective:** Validate the peer-to-peer push initiated from the NFC P2P page
broadcasts the default payload and causes navigation to exit gracefully.

**Prerequisites:**
- Both phones unlocked with NFC enabled and set to Android Beam / Quick Share or
  the platform equivalent.

**Steps:**
1. Open the **NFC Peer-to-Peer** page on both phones.
2. On Phone A, tap **Tap phones to exchange**.
3. Immediately bring the phones back-to-back until haptic feedback indicates an
   NDEF push.
4. Accept the prompt on Phone B to open the received data (usually a text share).
5. Tap **Back** on both devices to exit the page.

**Expected results:**
- The `Hello World` payload transfers to Phone B, launching the default viewer
  (e.g., text preview).
- Phone A returns to the previous page without errors after the exchange.
- Repeated exchanges continue to work without requiring an app restart.

### PHONE-BEACON-01 – Beacon detection via companion device

**Objective:** Verify the beacon scanner detects advertisements that originate
from another phone running a beacon simulator app.

**Prerequisites:**
- Beacon simulator on Phone B configured with UUID `12345678-1234-1234-1234-1234567890AB`,
  major `1`, minor `2`, TX power `-59`.

**Steps:**
1. Start the beacon broadcast on Phone B.
2. On Phone A, open **BLE Beacons**.
3. Wait for the device list to populate and locate Phone B’s advertisement.
4. Move Phone B farther away and return to observe RSSI updates.
5. Stop the broadcast on Phone B and confirm the entry eventually disappears.

**Expected results:**
- Phone A lists Phone B with the configured UUID and updates RSSI in real time.
- Entry disappears within one minute of stopping the broadcast.
- No stale ghost entries remain when the broadcast is off.

### PHONE-VISION-01 – QR scan interoperability smoke test

**Objective:** Ensure the QR scanner reads codes generated on a second phone.

**Prerequisites:**
- QR code generator app or website on Phone B.

**Steps:**
1. Generate a QR code on Phone B containing the text `https://aka.ms/mobileiot`.
2. On Phone A, open **Computer Vision** and tap **Scan QR Code**.
3. Align the camera with the QR code until a result is shown.
4. Tap **Capture Photo** and take a picture of any object to confirm image
   classification still works after scanning.

**Expected results:**
- QR result field on Phone A updates with the URL exactly.
- App does not crash or freeze when switching between QR scanning and photo
  capture.
- Optional classification result populates after the photo capture.

## Post-test cleanup

- Disable any beacon or NFC tag emulation apps running on Phone B.
- Re-enable regular Wi-Fi on both phones if it was disabled.
- Sign out of test builds or uninstall them if the devices are being handed off
  to other testers.
