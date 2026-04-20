from __future__ import annotations

import hashlib
import shutil
import subprocess
from dataclasses import dataclass
from pathlib import Path
from typing import Literal, Optional

AppName = Literal["maui", "uno"]
Platform = Literal["android", "ios", "windows"]


@dataclass(frozen=True)
class BuildSpec:
    app: AppName
    platform: Platform
    csproj_rel: str
    tfm: str
    extension: str
    package_id: str
    publish: bool = False
    runtime: str | None = None
    exe_name: str | None = None


BUILD_SPECS: dict[tuple[AppName, Platform], BuildSpec] = {
    ("maui", "android"): BuildSpec(
        "maui",
        "android",
        "src/MobileIoT/QiMata.MobileIoT/QiMata.MobileIoT.csproj",
        "net8.0-android",
        ".apk",
        "com.qimata.mobileiot",
    ),
    ("maui", "ios"): BuildSpec(
        "maui",
        "ios",
        "src/MobileIoT/QiMata.MobileIoT/QiMata.MobileIoT.csproj",
        "net8.0-ios",
        ".ipa",
        "com.qimata.mobileiot",
    ),
    ("uno", "windows"): BuildSpec(
        "uno",
        "windows",
        "src/MobileIoT/QiMata.MobileIoT.Uno/QiMata.MobileIoT.Uno.csproj",
        "net9.0-windows10.0.19041",
        ".exe",
        "com.qimata.mobileiot.uno",
        publish=True,
        exe_name="QiMata.MobileIoT.Uno.exe",
    ),
    ("uno", "android"): BuildSpec(
        "uno",
        "android",
        "src/MobileIoT/QiMata.MobileIoT.Uno/QiMata.MobileIoT.Uno.csproj",
        "net9.0-android",
        ".apk",
        "com.qimata.mobileiot.uno",
    ),
    ("uno", "ios"): BuildSpec(
        "uno",
        "ios",
        "src/MobileIoT/QiMata.MobileIoT.Uno/QiMata.MobileIoT.Uno.csproj",
        "net9.0-ios",
        ".ipa",
        "com.qimata.mobileiot.uno",
    ),
}


@dataclass
class BuildArtifact:
    app: AppName
    platform: Platform
    path: Path
    package_id: str
    build_key: str
    support_dir: Path | None = None


class AppBuilder:
    """Builds demo apps in TestHarness configuration and caches artifacts by app/platform/git state."""

    def __init__(self, repo_root: Path):
        self.repo_root = repo_root
        self.cache_dir = repo_root / "harness" / "runs" / ".cache"
        self.cache_dir.mkdir(parents=True, exist_ok=True)

    @staticmethod
    def spec(app: AppName, platform: Platform) -> BuildSpec:
        try:
            return BUILD_SPECS[(app, platform)]
        except KeyError as exc:
            valid = ", ".join(f"{a}/{p}" for a, p in sorted(BUILD_SPECS))
            raise ValueError(f"unsupported app/platform '{app}/{platform}' (valid: {valid})") from exc

    @staticmethod
    def supported_pairs() -> list[tuple[AppName, Platform]]:
        return sorted(BUILD_SPECS)

    def cached_artifacts(self) -> list[dict[str, str]]:
        rows: list[dict[str, str]] = []
        for path in sorted(self.cache_dir.glob("*")):
            if path.is_file() and path.suffix in {".apk", ".ipa", ".exe"}:
                rows.append({"name": path.name, "path": str(path)})
            elif path.is_dir() and path.name.startswith("_publish-"):
                exe = next(path.glob("*.exe"), None)
                rows.append({"name": path.name, "path": str(exe or path)})
        return rows

    def _build_key(self) -> str:
        try:
            sha = subprocess.run(
                ["git", "rev-parse", "--short=12", "HEAD"],
                cwd=self.repo_root,
                capture_output=True,
                text=True,
                check=False,
                timeout=3.0,
            ).stdout.strip() or "nogit"
            dirty = subprocess.run(
                ["git", "status", "--porcelain"],
                cwd=self.repo_root,
                capture_output=True,
                text=True,
                check=False,
                timeout=3.0,
            ).stdout
            return f"{sha}-{hashlib.sha256(dirty.encode()).hexdigest()[:8]}"
        except Exception:
            return "nogit"

    def _cached(self, spec: BuildSpec, key: str) -> Optional[BuildArtifact]:
        prefix = f"{spec.app}-{spec.platform}-{key}"
        if spec.publish:
            support_dir = self.cache_dir / f"_publish-{prefix}"
            exe = support_dir / (spec.exe_name or "")
            if exe.exists():
                return BuildArtifact(spec.app, spec.platform, exe, spec.package_id, key, support_dir)
            return None

        path = self.cache_dir / f"{prefix}{spec.extension}"
        if path.exists():
            return BuildArtifact(spec.app, spec.platform, path, spec.package_id, key)
        return None

    def build(self, app: AppName, platform: Platform, *, force: bool = False) -> BuildArtifact:
        spec = self.spec(app, platform)
        csproj = self.repo_root / spec.csproj_rel
        if not csproj.exists():
            raise FileNotFoundError(f"csproj not found at {csproj}")

        key = self._build_key()
        if not force:
            cached = self._cached(spec, key)
            if cached:
                return cached

        return self._publish(spec, csproj, key) if spec.publish else self._build(spec, csproj, key)

    def _build(self, spec: BuildSpec, csproj: Path, key: str) -> BuildArtifact:
        out_dir = self.cache_dir / f"_build-{spec.app}-{spec.platform}"
        if out_dir.exists():
            shutil.rmtree(out_dir)
        out_dir.mkdir(parents=True, exist_ok=True)

        cmd = [
            "dotnet",
            "build",
            str(csproj),
            "-c",
            "TestHarness",
            "-f",
            spec.tfm,
            f"-p:OutputPath={out_dir}/",
        ]
        proc = subprocess.run(cmd, cwd=self.repo_root, check=False)
        if proc.returncode != 0:
            raise RuntimeError(f"dotnet build failed for {spec.app}/{spec.platform} (exit {proc.returncode})")

        candidates = list(out_dir.rglob(f"*{spec.extension}"))
        if not candidates:
            raise FileNotFoundError(f"no {spec.extension} produced under {out_dir}")

        chosen = max(candidates, key=lambda p: p.stat().st_size)
        final = self.cache_dir / f"{spec.app}-{spec.platform}-{key}{spec.extension}"
        shutil.copy2(chosen, final)
        return BuildArtifact(spec.app, spec.platform, final, spec.package_id, key)

    def _publish(self, spec: BuildSpec, csproj: Path, key: str) -> BuildArtifact:
        support_dir = self.cache_dir / f"_publish-{spec.app}-{spec.platform}-{key}"
        if support_dir.exists():
            shutil.rmtree(support_dir)
        support_dir.mkdir(parents=True, exist_ok=True)

        cmd = [
            "dotnet",
            "publish",
            str(csproj),
            "-c",
            "TestHarness",
            "-f",
            spec.tfm,
            "-o",
            str(support_dir),
            "/p:WindowsAppSDKSelfContained=true",
            "--self-contained",
            "false",
        ]
        if spec.runtime:
            cmd.extend(["-r", spec.runtime])

        proc = subprocess.run(cmd, cwd=self.repo_root, check=False)
        if proc.returncode != 0:
            raise RuntimeError(f"dotnet publish failed for {spec.app}/{spec.platform} (exit {proc.returncode})")

        exe = support_dir / (spec.exe_name or "")
        if not exe.exists():
            candidates = list(support_dir.rglob("*.exe"))
            if not candidates:
                raise FileNotFoundError(f"no exe produced under {support_dir}")
            exe = max(candidates, key=lambda p: p.stat().st_size)

        return BuildArtifact(spec.app, spec.platform, exe, spec.package_id, key, support_dir)
