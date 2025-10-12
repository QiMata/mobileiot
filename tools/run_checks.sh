#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"

pushd "$repo_root" >/dev/null

echo "Running Python unit tests..."
PYTHONPATH="src/pi" python -m unittest discover -s src/pi/tests

echo "Running .NET unit tests..."
dotnet test src/MobileIoT/QiMata.MobileIoT.Tests/QiMata.MobileIoT.Tests.csproj

popd >/dev/null
