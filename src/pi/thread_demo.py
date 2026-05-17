"""Thread Protocol demo bridge for Raspberry Pi."""

from __future__ import annotations

import asyncio
import os
from pathlib import Path

from aiohttp import web

from thread_demo_bridge import ThreadDemoConfig, create_app, start_coap_echo_server


def _load_env() -> None:
    """Load settings from thread_demo.env if it exists."""
    env_file = Path(__file__).with_name("thread_demo.env")
    if env_file.is_file():
        with env_file.open(encoding="utf-8") as handle:
            for line in handle:
                line = line.strip()
                if line and not line.startswith("#") and "=" in line:
                    key, _, value = line.partition("=")
                    os.environ.setdefault(key.strip(), value.strip())


async def main() -> None:
    _load_env()
    config = ThreadDemoConfig.from_env()

    try:
        await start_coap_echo_server(config.coap_port)
    except Exception as exc:
        print(f"Warning: CoAP server failed to start: {exc}")

    app = create_app(config)
    runner = web.AppRunner(app)
    await runner.setup()
    site = web.TCPSite(runner, config.http_host, config.http_port)
    await site.start()
    print(f"Thread bridge HTTP server listening on {config.http_host}:{config.http_port}")
    print("Endpoints:")
    print("  GET  /healthz")
    print("  GET  /thread/status")
    print("  POST /thread/ping")

    try:
        await asyncio.Event().wait()
    except KeyboardInterrupt:
        pass
    finally:
        await runner.cleanup()


if __name__ == "__main__":
    asyncio.run(main())
