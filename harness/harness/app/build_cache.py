from __future__ import annotations

import hashlib
import subprocess
from dataclasses import dataclass
from pathlib import Path

from harness.app.build_specs import AppName, BuildSpec, Platform


@dataclass
class BuildArtifact:
    app: AppName
    platform: Platform
    path: Path
    package_id: str
    build_key: str
    support_dir: Path | None = None


def cache_dir(repo_root: Path) -> Path:
    return repo_root / "harness" / "runs" / ".cache"


def ensure_cache_dir(repo_root: Path) -> Path:
    path = cache_dir(repo_root)
    path.mkdir(parents=True, exist_ok=True)
    return path


def reset_dir(path: Path) -> None:
    if path.exists():
        import shutil

        shutil.rmtree(path)
    path.mkdir(parents=True, exist_ok=True)


def build_key(repo_root: Path) -> str:
    try:
        sha = subprocess.run(
            ["git", "rev-parse", "--short=12", "HEAD"],
            cwd=repo_root,
            capture_output=True,
            text=True,
            check=False,
            timeout=10.0,
        ).stdout.strip() or "nogit"
        dirty = subprocess.run(
            ["git", "status", "--porcelain"],
            cwd=repo_root,
            capture_output=True,
            text=True,
            check=False,
            timeout=30.0,
        ).stdout
        return f"{sha}-{hashlib.sha256(dirty.encode()).hexdigest()[:8]}"
    except Exception:
        return "nogit"


def cached_artifacts(cache_dir: Path) -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    for path in sorted(cache_dir.glob("*")):
        if path.is_file() and path.suffix in {".apk", ".ipa", ".exe"}:
            rows.append({"name": path.name, "path": str(path)})
        elif path.is_dir() and path.name.startswith("_publish-"):
            exe = next(path.glob("*.exe"), None)
            rows.append({"name": path.name, "path": str(exe or path)})
    return rows


def cached_artifact(cache_dir: Path, spec: BuildSpec, key: str) -> BuildArtifact | None:
    prefix = f"{spec.app}-{spec.platform}-{key}"
    if spec.publish:
        support_dir = cache_dir / f"_publish-{prefix}"
        exe = support_dir / (spec.exe_name or "")
        if exe.exists():
            return BuildArtifact(spec.app, spec.platform, exe, spec.package_id, key, support_dir)
        return None

    path = cache_dir / f"{prefix}{spec.extension}"
    if path.exists():
        return BuildArtifact(spec.app, spec.platform, path, spec.package_id, key)
    return None
