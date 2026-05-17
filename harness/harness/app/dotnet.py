from __future__ import annotations

import subprocess
from pathlib import Path

from harness.app.build_specs import BuildSpec


def restore_android_dependencies(repo_root: Path, csproj: Path, tfm: str) -> None:
    shared_csprojs = [p for p in csproj.parent.parent.rglob("*.Shared.csproj")]
    for shared in shared_csprojs:
        rc = subprocess.run(
            ["dotnet", "restore", str(shared)],
            cwd=repo_root,
            check=False,
        ).returncode
        if rc != 0:
            raise RuntimeError(f"dotnet restore failed for {shared.name} (exit {rc})")

    rc = subprocess.run(
        [
            "dotnet",
            "restore",
            str(csproj),
            f"-p:TargetFrameworks={tfm}",
            "--no-dependencies",
        ],
        cwd=repo_root,
        check=False,
    ).returncode
    if rc != 0:
        raise RuntimeError(f"dotnet restore failed for {csproj.name} (exit {rc})")


def build_project(repo_root: Path, csproj: Path, spec: BuildSpec, out_dir: Path) -> None:
    android = spec.platform == "android"
    if android:
        # The MAUI csproj's TargetFrameworks is multi-target. On a Mac/Linux
        # dev box that doesn't have every workload (e.g. maui-maccatalyst)
        # we scope to a single TFM via `-p:TargetFrameworks=`. That property,
        # however, propagates to ProjectReferences during restore - which
        # corrupts the Shared project's assets if Shared is single-target.
        # So: restore Shared without the override, then Main with it,
        # then build with --no-restore.
        restore_android_dependencies(repo_root, csproj, spec.tfm)

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
    if android:
        # 1. Scope TargetFrameworks to the one we're building.
        # 2. Embed assemblies + disable Fast Deployment so the produced APK
        #    is self-contained and can be `adb install`-ed directly.
        cmd += [
            f"-p:TargetFrameworks={spec.tfm}",
            "-p:EmbedAssembliesIntoApk=true",
            "-p:AndroidPackageFormat=apk",
            "--no-restore",
        ]
    proc = subprocess.run(cmd, cwd=repo_root, check=False)
    if proc.returncode != 0:
        raise RuntimeError(f"dotnet build failed for {spec.app}/{spec.platform} (exit {proc.returncode})")


def publish_project(repo_root: Path, csproj: Path, spec: BuildSpec, support_dir: Path) -> None:
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

    proc = subprocess.run(cmd, cwd=repo_root, check=False)
    if proc.returncode != 0:
        raise RuntimeError(f"dotnet publish failed for {spec.app}/{spec.platform} (exit {proc.returncode})")


def choose_output(out_dir: Path, extension: str) -> Path:
    candidates = list(out_dir.rglob(f"*{extension}"))
    if not candidates:
        raise FileNotFoundError(f"no {extension} produced under {out_dir}")
    return max(candidates, key=lambda p: p.stat().st_size)


def choose_exe(support_dir: Path, exe_name: str | None) -> Path:
    exe = support_dir / (exe_name or "")
    if exe.exists():
        return exe
    candidates = list(support_dir.rglob("*.exe"))
    if not candidates:
        raise FileNotFoundError(f"no exe produced under {support_dir}")
    return max(candidates, key=lambda p: p.stat().st_size)
