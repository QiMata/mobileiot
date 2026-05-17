"""Shared Thread bridge logic for the Raspberry Pi demo."""

from __future__ import annotations

import asyncio
import json
import os
import subprocess
import time
from dataclasses import dataclass
from datetime import datetime, timezone

from aiohttp import web


@dataclass(frozen=True)
class ThreadDemoConfig:
    http_host: str = "0.0.0.0"
    http_port: int = 8080
    coap_port: int = 5683
    default_target: str = ""

    @classmethod
    def from_env(cls) -> "ThreadDemoConfig":
        defaults = cls()
        return cls(
            http_host=os.environ.get("THREAD_HTTP_HOST", defaults.http_host),
            http_port=int(os.environ.get("THREAD_HTTP_PORT", str(defaults.http_port))),
            coap_port=int(os.environ.get("THREAD_COAP_PORT", str(defaults.coap_port))),
            default_target=os.environ.get("THREAD_DEFAULT_TARGET", defaults.default_target),
        )


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
        except FileNotFoundError as exc:
            raise RuntimeError("ot-ctl not found – is OTBR installed?") from exc
        except subprocess.TimeoutExpired as exc:
            raise RuntimeError("ot-ctl timed out") from exc

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
        timestamp = datetime.now(timezone.utc).isoformat()
        try:
            return {
                "ok": True,
                "role": cls.state(),
                "datasetHex": cls.dataset_active_hex(),
                "meshLocalAddresses": cls.ipaddr(),
                "rloc16": cls.rloc16(),
                "timestampUtc": timestamp,
            }
        except RuntimeError as exc:
            return {
                "ok": False,
                "role": "",
                "datasetHex": "",
                "meshLocalAddresses": [],
                "rloc16": "",
                "timestampUtc": timestamp,
                "error": str(exc),
            }


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


async def coap_ping(target: str, payload: str, timeout_s: float, coap_port: int) -> dict:
    """Send a CoAP POST to target's /echo and return the result."""
    import aiocoap

    uri = f"coap://[{target}]:{coap_port}/echo"
    request = aiocoap.Message(
        code=aiocoap.POST,
        uri=uri,
        payload=payload.encode("utf-8"),
    )

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


def create_app(config: ThreadDemoConfig | None = None) -> web.Application:
    """Create the aiohttp application with all routes."""
    app_config = config if config is not None else ThreadDemoConfig()
    app = web.Application()

    async def handle_healthz(_request):
        return web.json_response({"status": "ok"})

    async def handle_thread_status(_request):
        status = OtCtlDiagnostics.get_status()
        return web.json_response(status)

    async def handle_thread_ping(request):
        try:
            body = await request.json()
        except (json.JSONDecodeError, Exception):
            return web.json_response(
                {"ok": False, "error": "Invalid JSON body"},
                status=400,
            )

        target = body.get("target", app_config.default_target)
        payload = body.get("payload", "ping")
        timeout_ms = body.get("timeoutMs", 3000)

        if not target:
            return web.json_response(
                {"ok": False, "error": "Missing 'target' field"},
                status=400,
            )

        timeout_s = max(timeout_ms / 1000.0, 0.5)
        result = await coap_ping(target, payload, timeout_s, app_config.coap_port)
        return web.json_response(result)

    app.router.add_get("/healthz", handle_healthz)
    app.router.add_get("/thread/status", handle_thread_status)
    app.router.add_post("/thread/ping", handle_thread_ping)
    return app
