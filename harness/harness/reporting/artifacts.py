from __future__ import annotations

import datetime as dt
import json
import os
import subprocess
from pathlib import Path
from typing import Any


def _git_sha(repo_root: Path) -> str:
    try:
        r = subprocess.run(
            ["git", "rev-parse", "--short=12", "HEAD"],
            cwd=repo_root, capture_output=True, text=True, check=False, timeout=3.0,
        )
        return r.stdout.strip() or "unknown"
    except Exception:
        return "unknown"


class ArtifactSink:
    def __init__(self, repo_root: Path, run_id: str | None = None):
        self.repo_root = repo_root
        sha = _git_sha(repo_root)
        iso = dt.datetime.now().strftime("%Y%m%dT%H%M%S")
        self.run_id = run_id or f"{iso}-{sha}"
        self.root = repo_root / "harness" / "runs" / self.run_id
        self.root.mkdir(parents=True, exist_ok=True)

    def device_dir(self, device_id: str) -> Path:
        p = self.root / device_id
        p.mkdir(parents=True, exist_ok=True)
        return p

    def write_summary(self, summary: dict[str, Any]) -> Path:
        path = self.root / "summary.json"
        path.write_text(json.dumps(summary, indent=2), encoding="utf-8")
        return path

    def append_jsonl(self, name: str, obj: dict[str, Any]) -> None:
        path = self.root / name
        with path.open("a", encoding="utf-8") as f:
            f.write(json.dumps(obj) + "\n")
