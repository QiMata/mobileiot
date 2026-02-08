"""Thread Protocol demo bridge for Raspberry Pi.

Exposes Thread network diagnostics (via ot-ctl) and a CoAP echo endpoint
through a simple HTTP API that the MobileIoT MAUI app can consume.

Prerequisites
-------------
1. Install OpenThread Border Router (OTBR) and commission a Thread network.
2. Install Python dependencies:
       pip install aiocoap aiohttp

3. Run this script:
       python3 thread_demo.py

   Override defaults with environment variables or a thread_demo.env file:
       THREAD_HTTP_HOST=0.0.0.0
       THREAD_HTTP_PORT=8080
       THREAD_COAP_PORT=5683
       THREAD_DEFAULT_TARGET=  (optional mesh-local IPv6)

HTTP Endpoints
--------------
  GET  /healthz         - Health check
  GET  /thread/status   - Thread network status via ot-ctl
  POST /thread/ping     - CoAP echo ping to a Thread node
       body: { "target": "<ipv6>", "payload": "hello", "timeoutMs": 3000 }
"""

import asyncio
import json
import os
import subprocess
import time
from datetime import datetime, timezone

from aiohttp import web

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

def _load_env():
    """Load settings from thread_demo.env if it exists."""
    env_file = os.path.join(os.path.dirname(os.path.abspath(__file__)), "thread_demo.env")
    if os.path.isfile(env_file):
        with open(env_file) as f:
            for line in f:
                line = line.strip()
                if line and not line.startswith("#") and "=" in line:
                    key, _, value = line.partition("=")
                    os.environ.setdefault(key.strip(), value.strip())


_load_env()

HTTP_HOST = os.environ.get("THREAD_HTTP_HOST", "0.0.0.0")
HTTP_PORT = int(os.environ.get("THREAD_HTTP_PORT", "8080"))
COAP_PORT = int(os.environ.get("THREAD_COAP_PORT", "5683"))
DEFAULT_TARGET = os.environ.get("THREAD_DEFAULT_TARGET", "")

# ---------------------------------------------------------------------------
# ot-ctl diagnostics wrapper
# ---------------------------------------------------------------------------

class OtCtlDiagnostics:
    """Thin wrapper around ``ot-ctl`` CLI commands."""

    @staticmethod
    def _run(args: list[str]) -> str:
        """Run an ot-ctl command and return stdout."""
        try:
            result = subprocess.run(
                ["sudo", "ot-ctl"] + args,
                capture_output=True,
                text=True,
                timeout=5,
            )
            return result.stdout.strip()
        except FileNotFoundError:
            raise RuntimeError("ot-ctl not found – is OTBR installed?")
        except subprocess.TimeoutExpired:
            raise RuntimeError("ot-ctl timed out")

    @classmethod
    def state(cls) -> str:
        return cls._run(["state"])

    @classmethod
    def ipaddr(cls) -> list[str]:
        raw = cls._run(["ipaddr"])
        return [line.strip() for line in raw.splitlines() if line.strip()]

    @classmethod
    def rloc16(cls) -> str:
        return cls._run(["rloc16"])

    @classmethod
    def dataset_active_hex(cls) -> str:
        return cls._run(["dataset", "active", "-x"])

    @classmethod
    def get_status(cls) -> dict:
        """Return a full status snapshot as a dictionary."""
        try:
            return {
                "ok": True,
                "role": cls.state(),
                "datasetHex": cls.dataset_active_hex(),
                "meshLocalAddresses": cls.ipaddr(),
                "rloc16": cls.rloc16(),
                "timestampUtc": datetime.now(timezone.utc).isoformat(),
            }
        except RuntimeError as exc:
            return {
                "ok": False,
                "role": "",
                "datasetHex": "",
                "meshLocalAddresses": [],
                "rloc16": "",
                "timestampUtc": datetime.now(timezone.utc).isoformat(),
                "error": str(exc),
            }

# ---------------------------------------------------------------------------
# CoAP echo server
# ---------------------------------------------------------------------------

_coap_server_task = None


async def start_coap_echo_server(port: int):
    """Start an aiocoap server with a /echo resource."""
    import aiocoap
    import aiocoap.resource as resource

    class EchoResource(resource.Resource):
        async def render_post(self, request):
            payload = request.payload.decode("utf-8", errors="replace")
            response_payload = f"ECHO: {payload}"
            return aiocoap.Message(payload=response_payload.encode("utf-8"))

        async def render_get(self, request):
            return aiocoap.Message(payload=b"CoAP echo resource. POST a payload to echo it.")

    root = resource.Site()
    root.add_resource(["echo"], EchoResource())

    context = await aiocoap.Context.create_server_context(root, bind=("::", port))
    print(f"CoAP echo server listening on port {port}")
    return context


# ---------------------------------------------------------------------------
# CoAP ping helper
# ---------------------------------------------------------------------------

async def coap_ping(target: str, payload: str, timeout_s: float, coap_port: int) -> dict:
    """Send a CoAP POST to target's /echo and return the result."""
    import aiocoap

    uri = f"coap://[{target}]:{coap_port}/echo"
    request = aiocoap.Message(code=aiocoap.POST, uri=uri,
                              payload=payload.encode("utf-8"))

    start = time.monotonic()
    try:
        context = await aiocoap.Context.create_client_context()
        try:
            response = await asyncio.wait_for(
                context.request(request).response,
                timeout=timeout_s,
            )
            rtt = (time.monotonic() - start) * 1000
            return {
                "ok": True,
                "payload": payload,
                "response": response.payload.decode("utf-8", errors="replace"),
                "rttMs": round(rtt, 2),
                "error": None,
            }
        finally:
            await context.shutdown()
    except asyncio.TimeoutError:
        rtt = (time.monotonic() - start) * 1000
        return {
            "ok": False,
            "payload": payload,
            "response": "",
            "rttMs": round(rtt, 2),
            "error": "CoAP request timed out",
        }
    except Exception as exc:
        rtt = (time.monotonic() - start) * 1000
        return {
            "ok": False,
            "payload": payload,
            "response": "",
            "rttMs": round(rtt, 2),
            "error": str(exc),
        }


# ---------------------------------------------------------------------------
# HTTP bridge server (aiohttp)
# ---------------------------------------------------------------------------

async def handle_healthz(request):
    return web.json_response({"status": "ok"})


async def handle_thread_status(request):
    status = OtCtlDiagnostics.get_status()
    return web.json_response(status)


async def handle_thread_ping(request):
    try:
        body = await request.json()
    except (json.JSONDecodeError, Exception):
        return web.json_response(
            {"ok": False, "error": "Invalid JSON body"}, status=400
        )

    target = body.get("target", DEFAULT_TARGET)
    payload = body.get("payload", "ping")
    timeout_ms = body.get("timeoutMs", 3000)

    if not target:
        return web.json_response(
            {"ok": False, "error": "Missing 'target' field"}, status=400
        )

    timeout_s = max(timeout_ms / 1000.0, 0.5)
    result = await coap_ping(target, payload, timeout_s, COAP_PORT)
    return web.json_response(result)


def create_app() -> web.Application:
    """Create the aiohttp application with all routes."""
    app = web.Application()
    app.router.add_get("/healthz", handle_healthz)
    app.router.add_get("/thread/status", handle_thread_status)
    app.router.add_post("/thread/ping", handle_thread_ping)
    return app


async def main():
    # Start CoAP echo server
    try:
        await start_coap_echo_server(COAP_PORT)
    except Exception as exc:
        print(f"Warning: CoAP server failed to start: {exc}")

    # Start HTTP bridge
    app = create_app()
    runner = web.AppRunner(app)
    await runner.setup()
    site = web.TCPSite(runner, HTTP_HOST, HTTP_PORT)
    await site.start()
    print(f"Thread bridge HTTP server listening on {HTTP_HOST}:{HTTP_PORT}")
    print(f"Endpoints:")
    print(f"  GET  /healthz")
    print(f"  GET  /thread/status")
    print(f"  POST /thread/ping")

    # Run forever
    try:
        await asyncio.Event().wait()
    except KeyboardInterrupt:
        pass
    finally:
        await runner.cleanup()


if __name__ == "__main__":
    asyncio.run(main())
