from __future__ import annotations

import json
import sys
from typing import Any, Iterable

from rich.console import Console
from rich.table import Table

_console = Console()
_json_mode = False


def set_json_mode(enabled: bool) -> None:
    global _json_mode
    _json_mode = enabled


def emit_event(event: str, **fields: Any) -> None:
    """Emit a single structured event. In --json mode prints one JSON object per line to stdout."""
    payload = {"event": event, **fields}
    if _json_mode:
        sys.stdout.write(json.dumps(payload) + "\n")
        sys.stdout.flush()
    else:
        _console.log(f"[cyan]{event}[/cyan] {fields}")


def render_inventory_table(devices: Iterable) -> None:
    if _json_mode:
        emit_event(
            "inventory",
            devices=[
                {
                    "id": d.id,
                    "kind": d.kind.value,
                    "online": d.online,
                    "reason_offline": d.reason_offline,
                    "capabilities": sorted(d.capabilities),
                }
                for d in devices
            ],
        )
        return
    table = Table(title="Inventory", show_header=True, header_style="bold")
    table.add_column("ID")
    table.add_column("Kind")
    table.add_column("Status")
    table.add_column("Capabilities")
    table.add_column("Reason (if offline)")
    for d in devices:
        status = "[green]online[/green]" if d.online else "[red]offline[/red]"
        table.add_row(d.id, d.kind.value, status, ", ".join(sorted(d.capabilities)), d.reason_offline or "")
    _console.print(table)
