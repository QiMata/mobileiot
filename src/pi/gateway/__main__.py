"""Allow running as: python -m gateway"""

import asyncio

from gateway.main import run

if __name__ == "__main__":
    asyncio.run(run())
