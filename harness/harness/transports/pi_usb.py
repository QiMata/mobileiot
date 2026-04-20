from __future__ import annotations

import socket
import threading
from pathlib import Path
from typing import Optional

import paramiko

from harness.inventory.model import Device
from harness.transports.base import Transport, TransportError, TransportResult


class PiTransport(Transport):
    """SSH over the Pi's USB-gadget ethernet link (RNDIS/ECM)."""

    kind = "pi-ssh"

    def __init__(self, device: Device):
        if not device.usb_gadget_ip:
            raise TransportError(f"device {device.id} has no usb_gadget_ip")
        self.device = device
        self._client: Optional[paramiko.SSHClient] = None
        self._forward_threads: dict[int, "_ForwardServer"] = {}

    def _connect(self) -> paramiko.SSHClient:
        if self._client is not None:
            return self._client
        client = paramiko.SSHClient()
        client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
        connect_kwargs: dict = {
            "hostname": self.device.usb_gadget_ip,
            "username": self.device.ssh_user or "pi",
            "timeout": 10.0,
            "allow_agent": True,
            "look_for_keys": True,
        }
        if self.device.ssh_key_path:
            connect_kwargs["key_filename"] = self.device.ssh_key_path
        client.connect(**connect_kwargs)
        self._client = client
        return client

    def shell(self, cmd, *, timeout: float = 30.0) -> TransportResult:
        client = self._connect()
        cmd_str = cmd if isinstance(cmd, str) else " ".join(cmd)
        stdin, stdout, stderr = client.exec_command(cmd_str, timeout=timeout)
        out = stdout.read().decode("utf-8", errors="replace")
        err = stderr.read().decode("utf-8", errors="replace")
        rc = stdout.channel.recv_exit_status()
        return TransportResult(rc, out, err)

    def push(self, local: str, remote: str) -> None:
        client = self._connect()
        with client.open_sftp() as sftp:
            sftp.put(local, remote)

    def pull(self, remote: str, local: str) -> None:
        client = self._connect()
        with client.open_sftp() as sftp:
            sftp.get(remote, local)

    def forward_port(self, local_port: int, remote_port: int) -> None:
        client = self._connect()
        transport = client.get_transport()
        if transport is None:
            raise TransportError("ssh transport not established")
        server = _ForwardServer(local_port, "127.0.0.1", remote_port, transport)
        server.start()
        self._forward_threads[local_port] = server

    def unforward_port(self, local_port: int) -> None:
        server = self._forward_threads.pop(local_port, None)
        if server:
            server.stop()

    def stream_logs(self):
        client = self._connect()
        stdin, stdout, stderr = client.exec_command("journalctl -f -n 0", get_pty=True)
        try:
            for line in iter(stdout.readline, ""):
                yield line.rstrip("\n")
        finally:
            try:
                stdout.channel.close()
            except Exception:
                pass

    def close(self) -> None:
        for p in list(self._forward_threads):
            self.unforward_port(p)
        if self._client is not None:
            self._client.close()
            self._client = None


class _ForwardServer(threading.Thread):
    def __init__(self, local_port: int, remote_host: str, remote_port: int, ssh_transport: paramiko.Transport):
        super().__init__(daemon=True, name=f"pi-forward-{local_port}")
        self.local_port = local_port
        self.remote_host = remote_host
        self.remote_port = remote_port
        self.ssh_transport = ssh_transport
        self._sock: Optional[socket.socket] = None
        self._stop = threading.Event()

    def run(self) -> None:
        self._sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self._sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self._sock.bind(("127.0.0.1", self.local_port))
        self._sock.listen(5)
        self._sock.settimeout(1.0)
        while not self._stop.is_set():
            try:
                client_sock, _ = self._sock.accept()
            except socket.timeout:
                continue
            except OSError:
                return
            threading.Thread(target=self._handle, args=(client_sock,), daemon=True).start()

    def _handle(self, client_sock: socket.socket) -> None:
        try:
            chan = self.ssh_transport.open_channel(
                "direct-tcpip", (self.remote_host, self.remote_port), client_sock.getpeername()
            )
        except Exception:
            client_sock.close()
            return
        if chan is None:
            client_sock.close()
            return
        _pump(client_sock, chan)

    def stop(self) -> None:
        self._stop.set()
        if self._sock:
            try:
                self._sock.close()
            except OSError:
                pass


def _pump(sock_a: socket.socket, sock_b) -> None:
    import select
    try:
        while True:
            r, _, _ = select.select([sock_a, sock_b], [], [], 1.0)
            if sock_a in r:
                data = sock_a.recv(4096)
                if not data:
                    break
                sock_b.send(data)
            if sock_b in r:
                data = sock_b.recv(4096)
                if not data:
                    break
                sock_a.send(data)
    finally:
        try:
            sock_a.close()
        except Exception:
            pass
        try:
            sock_b.close()
        except Exception:
            pass
