from __future__ import annotations

import time
from typing import Any

import requests

HOOK_PORT = 47821


class HarnessHttpClient:
    """Client for the in-app TEST_HARNESS HTTP hook (HarnessHttpHost).

    The hook binds 127.0.0.1:47821 inside the app. The harness forwards that port
    via `adb forward` (Android) or `pymobiledevice3 usbmux forward` (iOS). All
    requests from the dev box go to http://127.0.0.1:<local_port>.
    """

    def __init__(self, local_port: int = HOOK_PORT, *, timeout: float = 5.0):
        self.base = f"http://127.0.0.1:{local_port}"
        self.timeout = timeout

    def wait_ready(self, *, timeout: float = 30.0) -> None:
        deadline = time.monotonic() + timeout
        last: Exception | None = None
        while time.monotonic() < deadline:
            try:
                r = requests.get(f"{self.base}/health", timeout=1.5)
                if r.ok and r.json().get("ok"):
                    return
            except Exception as e:
                last = e
            time.sleep(0.5)
        raise TimeoutError(f"in-app hook at {self.base}/health not ready: {last}")

    def health(self) -> dict[str, Any]:
        r = requests.get(f"{self.base}/health", timeout=self.timeout)
        r.raise_for_status()
        return r.json()

    def start_scenario(self, name: str, args: dict | None = None) -> dict[str, Any]:
        r = requests.post(f"{self.base}/scenario/{name}", json=args or {}, timeout=self.timeout)
        r.raise_for_status()
        return r.json()

    def state(self) -> dict[str, Any]:
        r = requests.get(f"{self.base}/state", timeout=self.timeout)
        r.raise_for_status()
        return r.json()

    def inject(self, kind: str, payload: dict) -> dict[str, Any]:
        r = requests.post(f"{self.base}/inject", json={"kind": kind, "payload": payload}, timeout=self.timeout)
        r.raise_for_status()
        return r.json()

    def logs(self, since: float | None = None) -> list[dict]:
        params = {"since": since} if since is not None else {}
        r = requests.get(f"{self.base}/logs", params=params, timeout=self.timeout)
        r.raise_for_status()
        return r.json().get("entries", [])
