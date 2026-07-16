#!/usr/bin/env bash
# Headless-прогін чистої Core-логіки + Edit Mode тестів без відкриття Unity.
# Використовує dotnet SDK, що йде в комплекті з Unity Editor — нічого ставити не треба.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt")"
DOTNET="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet"

if [[ ! -x "$DOTNET" ]]; then
  # Fallback: системний dotnet, якщо Unity-версія не знайдена.
  DOTNET="$(command -v dotnet || true)"
  [[ -n "$DOTNET" ]] || { echo "dotnet SDK не знайдено (ні в Unity, ні в системі)"; exit 1; }
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
exec "$DOTNET" run --project "$ROOT/Tools/CoreTestRunner" -c Release --verbosity quiet "$@"
