from __future__ import annotations

from pathlib import Path


def find_repo_root(start: Path | None = None) -> Path:
    here = (start or Path(__file__)).resolve()
    for parent in [here, *here.parents]:
        if (parent / "src" / "MobileIoT").exists() and (parent / "harness").exists():
            return parent
    raise RuntimeError("cannot locate MobileIoT repo root from harness")
