"""USB bulk echo gadget for Raspberry Pi.

This script configures the Pi as a USB bulk device using Linux ConfigFS and
then echoes any data received on the OUT endpoint back through the IN
endpoint.  It replaces the need to manually ``modprobe g_zero``.

Prerequisites
-------------
* Raspberry Pi with USB gadget support (e.g. Pi Zero / Zero W / 4 in OTG mode).
* Must run as root (``sudo python3 usb_demo.py``).
* The ``libcomposite`` kernel module must be available::

      sudo modprobe libcomposite

The script creates a gadget named ``mobileiot`` under
``/sys/kernel/config/usb_gadget/``.  On exit (Ctrl-C) it tears the gadget
down automatically.
"""

import os
import sys
import signal

GADGET_BASE = "/sys/kernel/config/usb_gadget/mobileiot"
UDC_PATH = "/sys/class/udc"

# USB IDs (use Linux Foundation test range)
ID_VENDOR = "0x1d6b"
ID_PRODUCT = "0x0104"


def write_file(path: str, value: str):
    with open(path, "w") as f:
        f.write(value)


def read_file(path: str) -> str:
    with open(path, "r") as f:
        return f.read().strip()


def find_udc() -> str:
    """Return the name of the first available UDC controller."""
    entries = os.listdir(UDC_PATH)
    if not entries:
        raise RuntimeError("No UDC controllers found. Is this a gadget-capable Pi?")
    return entries[0]


def setup_gadget():
    """Create a minimal USB gadget with one bulk IN and one bulk OUT endpoint."""
    os.makedirs(GADGET_BASE, exist_ok=True)

    write_file(f"{GADGET_BASE}/idVendor", ID_VENDOR)
    write_file(f"{GADGET_BASE}/idProduct", ID_PRODUCT)
    write_file(f"{GADGET_BASE}/bcdUSB", "0x0200")
    write_file(f"{GADGET_BASE}/bcdDevice", "0x0100")

    # English strings
    strings = f"{GADGET_BASE}/strings/0x409"
    os.makedirs(strings, exist_ok=True)
    write_file(f"{strings}/manufacturer", "QiMata")
    write_file(f"{strings}/product", "MobileIoT USB Echo")
    write_file(f"{strings}/serialnumber", "000000000001")

    # Configuration
    config = f"{GADGET_BASE}/configs/c.1"
    os.makedirs(config, exist_ok=True)
    config_strings = f"{config}/strings/0x409"
    os.makedirs(config_strings, exist_ok=True)
    write_file(f"{config_strings}/configuration", "Bulk Echo")
    write_file(f"{config}/MaxPower", "120")

    # Use the loopback function (kernel built-in, echoes bulk data)
    func = f"{GADGET_BASE}/functions/Loopback.0"
    os.makedirs(func, exist_ok=True)

    # Link function into configuration
    link = f"{config}/Loopback.0"
    if not os.path.exists(link):
        os.symlink(func, link)

    # Bind to UDC
    udc = find_udc()
    write_file(f"{GADGET_BASE}/UDC", udc)
    print(f"USB gadget bound to UDC: {udc}")


def teardown_gadget():
    """Unbind and remove the gadget configuration."""
    try:
        write_file(f"{GADGET_BASE}/UDC", "")
    except (FileNotFoundError, OSError):
        pass

    config = f"{GADGET_BASE}/configs/c.1"
    link = f"{config}/Loopback.0"
    if os.path.islink(link):
        os.unlink(link)

    # Remove directories in reverse order
    for d in [
        f"{config}/strings/0x409",
        config,
        f"{GADGET_BASE}/functions/Loopback.0",
        f"{GADGET_BASE}/strings/0x409",
        GADGET_BASE,
    ]:
        try:
            os.rmdir(d)
        except (FileNotFoundError, OSError):
            pass

    print("USB gadget removed.")


def main():
    if os.geteuid() != 0:
        print("This script must be run as root (sudo).", file=sys.stderr)
        sys.exit(1)

    # Ensure libcomposite is loaded
    os.system("modprobe libcomposite")

    # Register cleanup on exit
    def cleanup(*_):
        teardown_gadget()
        sys.exit(0)

    signal.signal(signal.SIGINT, cleanup)
    signal.signal(signal.SIGTERM, cleanup)

    setup_gadget()
    print("USB bulk echo gadget active. The host can send/receive on bulk endpoints.")
    print("Press Ctrl+C to stop.")

    # Keep running until interrupted; the Loopback function handles
    # echo in the kernel so no userspace I/O loop is needed.
    try:
        signal.pause()
    except AttributeError:
        # signal.pause not available on all platforms; fallback
        import time
        while True:
            time.sleep(60)


if __name__ == "__main__":
    main()
