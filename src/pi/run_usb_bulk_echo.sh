#!/bin/bash
# Enable the g_zero USB bulk echo gadget for the MobileIoT ping demo.

set -euo pipefail

if [[ $EUID -ne 0 ]]; then
    echo "This script must be run as root" >&2
    exit 1
fi

if lsmod | grep -q '^g_serial '; then
    echo "Unloading conflicting g_serial gadget" >&2
    modprobe -r g_serial
fi

if lsmod | grep -q '^g_zero '; then
    echo "g_zero gadget already loaded" >&2
else
    modprobe g_zero
    echo "g_zero gadget loaded"
fi

echo "USB bulk echo gadget is active. Connect the Pi to the host device to begin testing."
