from __future__ import annotations

import sys
from pathlib import Path

import typer

from harness.config import find_repo_root
from harness.app.builder import AppBuilder, BUILD_SPECS
from harness.inventory import Inventory
from harness.integrations import load_all_plugins
from harness.reporting.console import emit_event, render_inventory_table, set_json_mode
from harness.secrets import Secrets

app = typer.Typer(help="MobileIoT test harness", no_args_is_help=True)


def _repo_root() -> Path:
    return find_repo_root()


@app.command()
def doctor(json_out: bool = typer.Option(False, "--json", help="Emit JSON events to stdout")):
    """Probe the inventory, list missing secrets, and print the integration eligibility matrix."""
    set_json_mode(json_out)
    root = _repo_root()
    inv = Inventory.from_repo(root).probe()
    render_inventory_table(inv.devices)
    builder = AppBuilder(root)

    plugins = load_all_plugins()
    matrix = []
    for p in plugins:
        ok, reason = p.eligible(inv)
        matrix.append({"integration": p.name, "hardware_tier_eligible": ok, "reason": reason})
        emit_event("eligibility", integration=p.name, hardware=ok, reason=reason)

    secrets = Secrets(root)
    missing_keys = secrets.missing(["AZURE_IOT_CONNSTR", "WIFI_SSID", "WIFI_PSK", "PI_SSH_KEY_PATH"])
    emit_event("secrets", missing=missing_keys)

    emit_event(
        "summary",
        kind="doctor",
        inventory=inv.summary(),
        integrations_total=len(plugins),
        integrations_hw_eligible=sum(1 for m in matrix if m["hardware_tier_eligible"]),
        missing_secrets=missing_keys,
        cached_builds=builder.cached_artifacts(),
    )


@app.command()
def run(
    integration: str = typer.Option(None, "--integration", "-i", help="Run only this integration by name"),
    tier: str = typer.Option("auto", "--tier", "-t", help="mock | hardware | auto"),
    app_variant: str = typer.Option("maui", "--app", help="maui | uno | both"),
    json_out: bool = typer.Option(False, "--json", help="Stream JSON events to stdout"),
    extra: list[str] = typer.Argument(None, help="Extra arguments forwarded to pytest"),
):
    """Run harness tests. `auto` runs mock-tier always + hardware-tier for online devices."""
    import pytest
    set_json_mode(json_out)
    root = _repo_root()
    app_norm = app_variant.lower()
    if app_norm not in ("maui", "uno", "both"):
        typer.echo(f"unknown app '{app_variant}'; use maui | uno | both", err=True)
        raise typer.Exit(2)
    args: list[str] = []
    test_root = root / "harness" / "tests"
    args.append(str(test_root))
    tier_norm = tier.lower()
    if tier_norm == "mock":
        args += ["-m", "mock"]
    elif tier_norm == "hardware":
        args += ["-m", "hardware"]
    elif tier_norm != "auto":
        typer.echo(f"unknown tier '{tier}'; use mock | hardware | auto", err=True)
        raise typer.Exit(2)
    if integration:
        args += ["-k", integration]
    args += [f"--miot-app={app_norm}"]
    if json_out:
        args += ["-q", "--no-header"]
    if extra:
        args += list(extra)
    emit_event("run_start", tier=tier_norm, integration=integration, app=app_norm, pytest_args=args)
    rc = pytest.main(args)
    emit_event("summary", kind="run", tier=tier_norm, integration=integration, app=app_norm, exit_code=int(rc))
    raise typer.Exit(int(rc))


@app.command()
def build(
    platform: str = typer.Option("android", "--platform", "-p", help="android | ios | windows"),
    app_variant: str = typer.Option("maui", "--app", help="maui | uno"),
):
    """Force-rebuild the TestHarness APK/IPA and print the cached path."""
    app_norm = app_variant.lower()
    platform_norm = platform.lower()
    if app_norm not in ("maui", "uno"):
        typer.echo(f"unknown app '{app_variant}'", err=True)
        raise typer.Exit(2)
    if (app_norm, platform_norm) not in BUILD_SPECS:
        valid = ", ".join(f"{a}/{p}" for a, p in sorted(BUILD_SPECS))
        typer.echo(f"unsupported app/platform '{app_norm}/{platform_norm}' (valid: {valid})", err=True)
        raise typer.Exit(2)
    if platform_norm == "ios" and sys.platform != "darwin":
        typer.echo("ios build requires macOS", err=True)
        raise typer.Exit(2)
    if platform_norm == "windows" and sys.platform != "win32":
        typer.echo("windows build requires Windows", err=True)
        raise typer.Exit(2)
    root = _repo_root()
    builder = AppBuilder(root)
    art = builder.build(app_norm, platform_norm, force=True)  # type: ignore[arg-type]
    typer.echo(str(art.path))


@app.command()
def repl():
    """Drop into an interactive Python REPL with inventory/transport helpers preloaded."""
    import code
    root = _repo_root()
    inv = Inventory.from_repo(root).probe()
    ns = {"repo_root": root, "inventory": inv}
    banner = (
        "MobileIoT harness REPL\n"
        f"repo_root = {root}\n"
        f"inventory = {inv.summary()}\n"
        "from harness.transports import transport_for\n"
    )
    code.interact(banner=banner, local=ns)


if __name__ == "__main__":
    app()
