#!/usr/bin/env bash
# Качає Nunito з Google Fonts у Assets/_Fonts.
#
# Nunito, а не Baloo 2 з макета: у Baloo 2 немає кирилиці, а весь інтерфейс
# гри українською. Nunito близька за характером (округла, геометрична,
# важкі накреслення) і має повні 220 кириличних гліфів.
#
# Після цього: Unity → Ink Flow → Setup → Generate Font Assets.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/Assets/_Fonts"
mkdir -p "$OUT"

CSS="$(mktemp)"
trap 'rm -f "$CSS"' EXIT

# Старий User-Agent змушує Google віддати TTF замість woff2.
curl -sSL --max-time 30 -A "Mozilla/5.0" \
  "https://fonts.googleapis.com/css2?family=Nunito:wght@400;700;800&display=swap" -o "$CSS"

fetch() {
  local weight="$1" name="$2"
  local url
  url="$(awk "/font-weight: $weight;/{f=1} f && /\.ttf/{print; exit}" "$CSS" \
        | grep -oE 'https://[^)]+\.ttf')"
  [[ -n "$url" ]] || { echo "Не знайдено URL для ваги $weight"; exit 1; }
  curl -sSL --max-time 60 "$url" -o "$OUT/Nunito-$name.ttf"
  echo "  ✔ Nunito-$name.ttf ($(wc -c < "$OUT/Nunito-$name.ttf") байт)"
}

echo "Качаю Nunito → $OUT"
fetch 400 Regular
fetch 700 Bold
fetch 800 ExtraBold

echo "Готово. Далі в Unity: Ink Flow → Setup → Generate Font Assets"
