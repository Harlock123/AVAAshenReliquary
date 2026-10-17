#!/usr/bin/env bash
# Renders screenshots headlessly from the real window (tests/AshenReliquary.App.Tests/RenderTests.cs)
# with a fixed seed and frame-by-frame play, so no desktop is captured.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
out="${1:-$root/screenshots}"
mkdir -p "$out"
ASHEN_SHOTS="$out" dotnet test "$root/tests/AshenReliquary.App.Tests" --nologo -v quiet
ls -1 "$out"
