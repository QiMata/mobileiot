"""Rotate the server certificate with safe reload.

Steps:
    1. Generate new server cert signed by existing CA
    2. Atomic swap via rename
    3. Signal the gateway process to reload (SIGHUP)

Usage::

    sudo python -m gateway.pki.rotate_cert [--out-dir /etc/mobileiot/opcua]
"""

from __future__ import annotations

import argparse
import os
import signal
import subprocess

from gateway.pki.generate_server_cert import generate_server_cert

DEFAULT_OUT_DIR = "/etc/mobileiot/opcua"


def rotate(out_dir: str = DEFAULT_OUT_DIR, hostname: str | None = None) -> None:
    """Rotate the server certificate."""
    certs_dir = os.path.join(out_dir, "certs")
    private_dir = os.path.join(out_dir, "private")

    old_cert = os.path.join(certs_dir, "server_cert.pem")
    old_key = os.path.join(private_dir, "server_key.pem")
    backup_cert = os.path.join(certs_dir, "server_cert.pem.bak")
    backup_key = os.path.join(private_dir, "server_key.pem.bak")

    # Backup current certs
    if os.path.isfile(old_cert):
        os.rename(old_cert, backup_cert)
    if os.path.isfile(old_key):
        os.rename(old_key, backup_key)

    try:
        generate_server_cert(out_dir, hostname)
        print("Certificate rotated successfully.")
    except Exception:
        # Rollback on failure
        if os.path.isfile(backup_cert):
            os.rename(backup_cert, old_cert)
        if os.path.isfile(backup_key):
            os.rename(backup_key, old_key)
        raise

    # Clean up backups
    for path in (backup_cert, backup_key):
        if os.path.isfile(path):
            os.remove(path)

    # Signal the gateway to reload
    _signal_gateway()


def _signal_gateway() -> None:
    """Send SIGHUP to the gateway process for cert reload."""
    try:
        result = subprocess.run(
            ["systemctl", "show", "-p", "MainPID", "mobileiot-gateway"],
            capture_output=True,
            text=True,
            check=True,
        )
        pid_line = result.stdout.strip()
        pid = int(pid_line.split("=")[1])
        if pid > 0:
            os.kill(pid, signal.SIGHUP)
            print(f"Sent SIGHUP to gateway PID {pid}")
        else:
            print("Gateway service is not running.")
    except (subprocess.CalledProcessError, FileNotFoundError, ValueError):
        print("Could not signal gateway. Restart manually if needed.")


def main() -> None:
    parser = argparse.ArgumentParser(description="Rotate gateway server certificate")
    parser.add_argument(
        "--out-dir", default=DEFAULT_OUT_DIR, help="PKI directory"
    )
    parser.add_argument("--hostname", default=None, help="Server hostname")
    args = parser.parse_args()
    rotate(args.out_dir, args.hostname)


if __name__ == "__main__":
    main()
