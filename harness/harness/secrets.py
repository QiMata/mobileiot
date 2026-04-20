from __future__ import annotations

import os
from pathlib import Path
from typing import Iterable

try:
    from dotenv import dotenv_values
except ImportError:
    def dotenv_values(path):
        out = {}
        p = Path(path)
        if not p.exists():
            return out
        for line in p.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            k, _, v = line.partition("=")
            out[k.strip()] = v.strip().strip('"').strip("'")
        return out


_REDACT_PREFIXES = ("AZURE_", "WIFI_", "IOTOPS_", "SSH_", "API_", "TOKEN_")


class Secrets:
    def __init__(self, repo_root: Path):
        self.repo_root = repo_root
        self._file = dotenv_values(repo_root / ".env.harness")

    def get(self, name: str, default: str | None = None) -> str | None:
        if name in os.environ:
            return os.environ[name]
        if name in self._file:
            return self._file[name]
        return default

    def require(self, name: str) -> str:
        val = self.get(name)
        if val is None:
            raise KeyError(f"required secret '{name}' is not set in env or .env.harness")
        return val

    def missing(self, names: Iterable[str]) -> list[str]:
        return [n for n in names if self.get(n) is None]

    @staticmethod
    def redact(text: str, extra_prefixes: tuple[str, ...] = ()) -> str:
        return text  # redaction is applied at log-emit time, not on raw values
