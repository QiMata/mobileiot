from __future__ import annotations

from pathlib import Path
from typing import Iterable, Optional

from harness.inventory.discovery import probe_live
from harness.inventory.loader import find_default_inventory_paths, load_inventory
from harness.inventory.model import Device, DeviceKind, DeviceUnavailable


class Inventory:
    """Live, cached view of available devices."""

    def __init__(self, devices: list[Device]):
        self._devices = list(devices)
        self._probed = False

    @classmethod
    def from_repo(cls, repo_root: Path) -> "Inventory":
        primary, overrides = find_default_inventory_paths(repo_root)
        return cls(load_inventory(primary, overrides))

    def probe(self) -> "Inventory":
        probe_live(self._devices)
        self._probed = True
        return self

    @property
    def devices(self) -> list[Device]:
        return list(self._devices)

    def online(self) -> list[Device]:
        return [d for d in self._devices if d.online]

    def of_kind(self, kind: DeviceKind, *, online_only: bool = False) -> list[Device]:
        pool = self.online() if online_only else self._devices
        return [d for d in pool if d.kind == kind]

    def find(
        self,
        *,
        kind: DeviceKind | str | None = None,
        capabilities: Iterable[str] = (),
        online_only: bool = True,
    ) -> list[Device]:
        want_kind = DeviceKind(kind) if isinstance(kind, str) else kind
        caps = set(capabilities)
        pool = self.online() if online_only else self._devices
        return [d for d in pool if (want_kind is None or d.kind == want_kind) and caps.issubset(d.capabilities)]

    def require(
        self,
        *,
        role: str,
        kind: DeviceKind | str,
        capabilities: Iterable[str] = (),
    ) -> Device:
        matches = self.find(kind=kind, capabilities=capabilities, online_only=True)
        if not matches:
            reasons = self._unavailable_reasons(kind, capabilities)
            raise DeviceUnavailable(
                f"role '{role}' requires kind={kind} capabilities={list(capabilities)} — none online",
                role=role,
                reason=reasons,
            )
        return matches[0]

    def _unavailable_reasons(self, kind, capabilities) -> str:
        kind_enum = DeviceKind(kind) if isinstance(kind, str) else kind
        candidates = [d for d in self._devices if d.kind == kind_enum]
        if not candidates:
            return f"no {kind_enum.value} devices configured in devices.yaml"
        parts = []
        for d in candidates:
            if not d.online:
                parts.append(f"{d.id}: {d.reason_offline}")
            elif not set(capabilities).issubset(d.capabilities):
                missing = set(capabilities) - d.capabilities
                parts.append(f"{d.id}: missing capabilities {sorted(missing)}")
        return "; ".join(parts) if parts else "unknown"

    def summary(self) -> dict:
        by_kind: dict[str, dict[str, int]] = {}
        for d in self._devices:
            entry = by_kind.setdefault(d.kind.value, {"configured": 0, "online": 0})
            entry["configured"] += 1
            if d.online:
                entry["online"] += 1
        return {"probed": self._probed, "by_kind": by_kind, "total": len(self._devices)}
