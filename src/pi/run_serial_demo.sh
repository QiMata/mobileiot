#!/bin/bash
# Simple helper to run the USB serial LED demo.
# Loads the g_serial module if needed and launches the Python script.

if ! grep -q '^g_serial ' /proc/modules; then
    sudo modprobe g_serial
fi

sudo python3 "$(dirname "$0")/serial_demo.py"
