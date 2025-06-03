"""Placeholder for the USB bulk demo.

The Ping example in the MobileIoT app uses the Linux ``g_zero`` gadget,
which echoes any data sent to it. Because the gadget handles the echo in
the kernel, no Python code is required on the Pi. Load ``g_zero`` with
``sudo modprobe g_zero`` and the MAUI app will be able to send and
receive bytes on the bulk endpoints.
"""

