from __future__ import annotations

import shutil
from pathlib import Path

from harness.app.build_cache import (
    BuildArtifact,
    build_key as cache_build_key,
    cached_artifact,
    cached_artifacts as list_cached_artifacts,
    ensure_cache_dir,
    reset_dir,
)
from harness.app.build_specs import AppName, BUILD_SPECS, BuildSpec, Platform
from harness.app.dotnet import build_project, choose_exe, choose_output, publish_project


class AppBuilder:
    """Builds demo apps in TestHarness configuration and caches artifacts by app/platform/git state."""

    def __init__(self, repo_root: Path):
        self.repo_root = repo_root
        self.cache_dir = ensure_cache_dir(repo_root)

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
        return list_cached_artifacts(self.cache_dir)

    def _build_key(self) -> str:
        return cache_build_key(self.repo_root)

    def build(self, app: AppName, platform: Platform, *, force: bool = False) -> BuildArtifact:
        spec = self.spec(app, platform)
        csproj = self.repo_root / spec.csproj_rel
        if not csproj.exists():
            raise FileNotFoundError(f"csproj not found at {csproj}")

        key = self._build_key()
        if not force:
            cached = cached_artifact(self.cache_dir, spec, key)
            if cached:
                return cached

        return self._publish(spec, csproj, key) if spec.publish else self._build(spec, csproj, key)

    def _build(self, spec: BuildSpec, csproj: Path, key: str) -> BuildArtifact:
        out_dir = self.cache_dir / f"_build-{spec.app}-{spec.platform}"
        reset_dir(out_dir)
        build_project(self.repo_root, csproj, spec, out_dir)
        chosen = choose_output(out_dir, spec.extension)
        final = self.cache_dir / f"{spec.app}-{spec.platform}-{key}{spec.extension}"
        shutil.copy2(chosen, final)
        return BuildArtifact(spec.app, spec.platform, final, spec.package_id, key)

    def _publish(self, spec: BuildSpec, csproj: Path, key: str) -> BuildArtifact:
        support_dir = self.cache_dir / f"_publish-{spec.app}-{spec.platform}-{key}"
        reset_dir(support_dir)
        publish_project(self.repo_root, csproj, spec, support_dir)
        exe = choose_exe(support_dir, spec.exe_name)
        return BuildArtifact(spec.app, spec.platform, exe, spec.package_id, key, support_dir)
