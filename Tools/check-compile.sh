#!/usr/bin/env bash
# Компіляційна перевірка без відкриття Unity.
#
# ПРИНЦИП: не відтворювати параметри компіляції вручну, а брати ВЛАСНІ response-файли Unity
# з Library/Bee/artifacts/*.dag/. У них уже точний набір посилань, define-ів і аналізаторів —
# рівно те, чим збирає редактор. Будь-яка ручна реконструкція рано чи пізно розходиться
# (різні reference-збірки → різні nullable-анотації → попередження, які бачить лише Unity).
#
# Кожна збірка компілюється ОКРЕМО: монолітна збірка «всіх .cs разом» робить internal-члени
# видимими між збірками й дає хибне зелене.
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt")"
SCRIPTING="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/Resources/Scripting"
DOTNET="$SCRIPTING/DotNetSdk/dotnet"
CSC="$(ls "$SCRIPTING"/DotNetSdk/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | head -1)"
OUT="${TMPDIR:-/tmp}/inkflow-compile"

[[ -x "$DOTNET" && -n "$CSC" ]] || { echo "Не знайдено компілятор Unity ($UNITY_VERSION)"; exit 1; }

# Найсвіжіша тека артефактів Bee — саме там лежать актуальні rsp.
DAG="$(ls -dt "$ROOT"/Library/Bee/artifacts/*.dag 2>/dev/null | head -1)"
if [[ -z "$DAG" ]]; then
  echo "Немає Library/Bee/artifacts/*.dag — відкрий проєкт в Unity хоча б раз," >&2
  echo "щоб він згенерував параметри компіляції." >&2
  exit 1
fi

ASSEMBLIES=(
  InkFlow.Core InkFlow.Platform InkFlow.Meta InkFlow.Gameplay
  InkFlow.UI InkFlow.App InkFlow.Editor
  InkFlow.Core.Tests InkFlow.Meta.Tests
)

rm -rf "$OUT"; mkdir -p "$OUT"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

echo "Компіляція по збірках (параметрами самого Unity, $(basename "$DAG")):"
FAILED=0
MISSING=0

for name in "${ASSEMBLIES[@]}"; do
  src_rsp="$DAG/$name.rsp"
  if [[ ! -f "$src_rsp" ]]; then
    echo "  ? $name — Unity ще не збирав цю збірку, пропущено"
    MISSING=1
    continue
  fi

  # Єдина правка: перенаправляємо вихід, щоб не чіпати артефакти редактора.
  rsp="$OUT/$name.rsp"
  sed -e "s|^-out:.*|-out:\"$OUT/$name.dll\"|" \
      -e "s|^-refout:.*|-refout:\"$OUT/$name.ref.dll\"|" \
      -e "s|^-doc:.*||" "$src_rsp" > "$rsp"

  log="$OUT/$name.log"
  # Компілюємо з кореня проєкту: шляхи в rsp відносні саме до нього.
  if (cd "$ROOT" && "$DOTNET" exec "$CSC" -nologo -noconfig "@$rsp") > "$log" 2>&1; then
    warns="$(grep -c 'warning CS' "$log" || true)"
    if [[ "$warns" -gt 0 ]]; then
      echo "  ⚠ $name — 0 помилок, $warns попереджень"
      grep 'warning CS' "$log" | sed 's|^|      |' | sort -u | head -8
      FAILED=1
    else
      echo "  ✔ $name"
    fi
  else
    echo "  ✘ $name — ПОМИЛКИ КОМПІЛЯЦІЇ"
    grep -E 'error CS' "$log" | sed 's|^|      |' | sort -u | head -10
    FAILED=1
  fi
done

echo
if [[ $MISSING -eq 1 ]]; then
  echo "Увага: частину збірок пропущено. Дай Unity перекомпілювати проєкт і запусти знову."
fi
echo "Результат: $([[ $FAILED -eq 0 ]] && echo 'чисто — 0 помилок, 0 попереджень' || echo 'Є ЗАУВАЖЕННЯ')"
exit $FAILED
