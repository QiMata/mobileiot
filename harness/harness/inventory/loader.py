from __future__ import annotations

from pathlib import Path
from typing import Iterable

import yaml

from harness.inventory.model import Device, DeviceKind


def _parse_device(raw: dict) -> Device:
    kind = DeviceKind(raw["kind"])
    caps = set(raw.get("capabilities", []))
    labels = dict(raw.get("labels", {}))
    return Device(
        id=raw["id"],
        kind=kind,
        capabilities=caps,
        labels=labels,
        adb_serial=raw.get("adb_serial"),
        udid=raw.get("udid"),
        usb_gadget_ip=raw.get("usb_gadget_ip"),
        ssh_user=raw.get("ssh_user"),
        ssh_key_path=raw.get("ssh_key_path"),
    )


def _read_yaml(path: Path) -> dict:
    if not path.exists():
        return {}
    with path.open("r", encoding="utf-8") as f:
        return yaml.safe_load(f) or {}


def load_inventory(
    primary: Path,
    overrides: Path | None = None,
) -> list[Device]:
    """Merge devices.yaml with optional devices.local.yaml.

    Overrides match by `id` and replace the device entry entirely.
    """
    base = _read_yaml(primary)
    over = _read_yaml(overrides) if overrides else {}

    by_id: dict[str, dict] = {d["id"]: d for d in base.get("devices", [])}
    for d in over.get("devices", []):
        by_id[d["id"]] = d

    return [_parse_device(d) for d in by_id.values()]


def find_default_inventory_paths(repo_root: Path) -> tuple[Path, Path]:
    return repo_root / "devices.yaml", repo_root / "devices.local.yaml"


def iter_by_kind(devices: Iterable[Device], kind: DeviceKind) -> list[Device]:
    return [d for d in devices if d.kind == kind]
