"""Tests for the Thread demo bridge and ot-ctl parser.

Run with:
    python -m pytest src/pi/tests -q
"""

from unittest.mock import MagicMock, patch

import pytest

import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from thread_demo_bridge import (
    OtCtlDiagnostics,
    create_app,
)


# ---------------------------------------------------------------------------
# ot-ctl parser tests
# ---------------------------------------------------------------------------

class TestOtCtlDiagnostics:
    """Test the ot-ctl output parsing logic."""

    @patch("thread_demo_bridge.subprocess.run")
    def test_state_returns_role(self, mock_run):
        mock_run.return_value = MagicMock(stdout="leader\n", returncode=0)
        assert OtCtlDiagnostics.state() == "leader"

    @patch("thread_demo_bridge.subprocess.run")
    def test_ipaddr_returns_list(self, mock_run):
        mock_run.return_value = MagicMock(
            stdout="fddead:00be:ef00:0:0:ff:fe00:fc00\nfddead:00be:ef00:0:a8cd:baff:dead:beef\n",
            returncode=0,
        )
        addrs = OtCtlDiagnostics.ipaddr()
        assert len(addrs) == 2
        assert "fddead:00be:ef00:0:0:ff:fe00:fc00" in addrs

    @patch("thread_demo_bridge.subprocess.run")
    def test_rloc16_returns_hex(self, mock_run):
        mock_run.return_value = MagicMock(stdout="0400\n", returncode=0)
        assert OtCtlDiagnostics.rloc16() == "0400"

    @patch("thread_demo_bridge.subprocess.run")
    def test_dataset_active_hex(self, mock_run):
        mock_run.return_value = MagicMock(stdout="0e0800000000000100\n", returncode=0)
        assert OtCtlDiagnostics.dataset_active_hex() == "0e0800000000000100"

    @patch("thread_demo_bridge.subprocess.run")
    def test_get_status_success(self, mock_run):
        # Each call to _run returns different output based on args
        def side_effect(args, **kwargs):
            cmd = " ".join(args)
            if "state" in cmd:
                return MagicMock(stdout="router\n")
            elif "ipaddr" in cmd:
                return MagicMock(stdout="fd00::1\nfd00::2\n")
            elif "rloc16" in cmd:
                return MagicMock(stdout="1800\n")
            elif "dataset" in cmd:
                return MagicMock(stdout="abcdef\n")
            return MagicMock(stdout="\n")

        mock_run.side_effect = side_effect

        status = OtCtlDiagnostics.get_status()
        assert status["ok"] is True
        assert status["role"] == "router"
        assert len(status["meshLocalAddresses"]) == 2
        assert status["rloc16"] == "1800"

    @patch("thread_demo_bridge.subprocess.run")
    def test_get_status_otctl_missing(self, mock_run):
        mock_run.side_effect = FileNotFoundError("ot-ctl not found")

        status = OtCtlDiagnostics.get_status()
        assert status["ok"] is False
        assert "error" in status

    @patch("thread_demo_bridge.subprocess.run")
    def test_get_status_otctl_timeout(self, mock_run):
        import subprocess
        mock_run.side_effect = subprocess.TimeoutExpired(cmd="ot-ctl", timeout=5)

        status = OtCtlDiagnostics.get_status()
        assert status["ok"] is False
        assert "error" in status


# ---------------------------------------------------------------------------
# HTTP endpoint tests (aiohttp test client)
# ---------------------------------------------------------------------------

@pytest.fixture
def app():
    return create_app()


@pytest.fixture
def client(aiohttp_client, app):
    return aiohttp_client(app)


class TestHealthz:
    @pytest.mark.asyncio
    async def test_healthz(self, aiohttp_client):
        app = create_app()
        client = await aiohttp_client(app)
        resp = await client.get("/healthz")
        assert resp.status == 200
        data = await resp.json()
        assert data["status"] == "ok"


class TestThreadStatus:
    @pytest.mark.asyncio
    @patch.object(OtCtlDiagnostics, "get_status")
    async def test_status_success(self, mock_status, aiohttp_client):
        mock_status.return_value = {
            "ok": True,
            "role": "leader",
            "datasetHex": "abc",
            "meshLocalAddresses": ["fd00::1"],
            "rloc16": "0400",
            "timestampUtc": "2025-01-01T00:00:00Z",
        }
        app = create_app()
        client = await aiohttp_client(app)
        resp = await client.get("/thread/status")
        assert resp.status == 200
        data = await resp.json()
        assert data["ok"] is True
        assert data["role"] == "leader"

    @pytest.mark.asyncio
    @patch.object(OtCtlDiagnostics, "get_status")
    async def test_status_error(self, mock_status, aiohttp_client):
        mock_status.return_value = {
            "ok": False,
            "role": "",
            "datasetHex": "",
            "meshLocalAddresses": [],
            "rloc16": "",
            "timestampUtc": "2025-01-01T00:00:00Z",
            "error": "ot-ctl not found",
        }
        app = create_app()
        client = await aiohttp_client(app)
        resp = await client.get("/thread/status")
        assert resp.status == 200
        data = await resp.json()
        assert data["ok"] is False
        assert "error" in data


class TestThreadPing:
    @pytest.mark.asyncio
    @patch("thread_demo_bridge.coap_ping")
    async def test_ping_success(self, mock_coap, aiohttp_client):
        mock_coap.return_value = {
            "ok": True,
            "payload": "hello",
            "response": "ECHO: hello",
            "rttMs": 15.2,
            "error": None,
        }
        app = create_app()
        client = await aiohttp_client(app)
        resp = await client.post(
            "/thread/ping",
            json={"target": "fd00::1", "payload": "hello", "timeoutMs": 3000},
        )
        assert resp.status == 200
        data = await resp.json()
        assert data["ok"] is True
        assert data["response"] == "ECHO: hello"
        assert data["rttMs"] > 0

    @pytest.mark.asyncio
    @patch("thread_demo_bridge.coap_ping")
    async def test_ping_timeout(self, mock_coap, aiohttp_client):
        mock_coap.return_value = {
            "ok": False,
            "payload": "hello",
            "response": "",
            "rttMs": 3001.0,
            "error": "CoAP request timed out",
        }
        app = create_app()
        client = await aiohttp_client(app)
        resp = await client.post(
            "/thread/ping",
            json={"target": "fd00::1", "payload": "hello", "timeoutMs": 3000},
        )
        assert resp.status == 200
        data = await resp.json()
        assert data["ok"] is False
        assert "timed out" in data["error"]

    @pytest.mark.asyncio
    async def test_ping_missing_target(self, aiohttp_client):
        app = create_app()
        client = await aiohttp_client(app)
        resp = await client.post(
            "/thread/ping",
            json={"payload": "hello"},
        )
        assert resp.status == 400
        data = await resp.json()
        assert data["ok"] is False
        assert "target" in data["error"].lower()

    @pytest.mark.asyncio
    async def test_ping_bad_json(self, aiohttp_client):
        app = create_app()
        client = await aiohttp_client(app)
        resp = await client.post(
            "/thread/ping",
            data="not json",
            headers={"Content-Type": "application/json"},
        )
        assert resp.status == 400
