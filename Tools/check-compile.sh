#!/usr/bin/env bash
# Компіляційна перевірка без відкриття Unity.
#
# ПРИНЦИП: параметри компіляції беремо з ВЛАСНИХ response-файлів Unity
# (Library/Bee/artifacts/*.dag/) — там точний набір посилань, define-ів і аналізаторів.
# Ручна реконструкція вже двічі давала хибне зелене (різні reference-збірки → різні
# nullable-анотації; монолітна збірка → невидимі помилки з internal).
#
# АЛЕ список ФАЙЛІВ у тих rsp — станом на останню компіляцію Unity. Тому джерела
# підставляємо поточні: інакше щойно доданий файл мовчки не перевіряється.
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt")"
SCRIPTING="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/Resources/Scripting"
DOTNET="$SCRIPTING/DotNetSdk/dotnet"
CSC="$(ls "$SCRIPTING"/DotNetSdk/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | head -1)"
OUT="${TMPDIR:-/tmp}/inkflow-compile"

[[ -x "$DOTNET" && -n "$CSC" ]] || { echo "Не знайдено компілятор Unity ($UNITY_VERSION)"; exit 1; }

DAG="$(ls -dt "$ROOT"/Library/Bee/artifacts/*.dag 2>/dev/null | head -1)"
if [[ -z "$DAG" ]]; then
  echo "Немає Library/Bee/artifacts/*.dag — відкрий проєкт в Unity хоча б раз." >&2
  exit 1
fi

# збірка → тека з кодом
ASSEMBLY_DIRS=(
  "InkFlow.Core:Assets/_Scripts/Core"
  "InkFlow.Platform:Assets/_Scripts/Platform"
  "InkFlow.Meta:Assets/_Scripts/Meta"
  "InkFlow.Gameplay:Assets/_Scripts/Gameplay"
  "InkFlow.UI:Assets/_Scripts/UI"
  "InkFlow.App:Assets/_Scripts/App"
  "InkFlow.Editor:Assets/_Scripts/Editor"
  "InkFlow.Core.Tests:Assets/Tests/EditMode"
  "InkFlow.Meta.Tests:Assets/Tests/EditMode/Meta"
)

rm -rf "$OUT"; mkdir -p "$OUT"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

echo "Компіляція по збірках (параметрами Unity з $(basename "$DAG"), джерела — поточні):"
FAILED=0
MISSING=0

for entry in "${ASSEMBLY_DIRS[@]}"; do
  name="${entry%%:*}"
  dir="${entry#*:}"
  src_rsp="$DAG/$name.rsp"

  if [[ ! -f "$src_rsp" ]]; then
    echo "  ? $name — Unity ще не збирав цю збірку, пропущено"
    MISSING=1
    continue
  fi

  # Файли з вкладених asmdef належать іншій збірці — Unity їх сюди не включає.
  prune=()
  while IFS= read -r nested; do
    nested_dir="$(dirname "$nested")"
    [[ "$nested_dir" == "$ROOT/$dir" ]] && continue
    prune+=(-path "$nested_dir" -prune -o)
  done < <(find "$ROOT/$dir" -name '*.asmdef' 2>/dev/null)

  sources=()
  while IFS= read -r file; do
    sources+=("${file#$ROOT/}")
  done < <(find "$ROOT/$dir" ${prune[@]+"${prune[@]}"} -name '*.cs' -print 2>/dev/null)

  if [[ ${#sources[@]} -eq 0 ]]; then
    echo "  · $name — немає .cs, пропущено"
    continue
  fi

  rsp="$OUT/$name.rsp"
  {
    # Лише прапорці Unity, без його списку файлів. Unity пише їх і через '-', і через '/'
    # (напр. /nowarn:0649) — фільтрувати треба обидва, інакше «загубляться» саме ті
    # придушення попереджень, які редактор застосовує до [SerializeField]-полів.
    grep -E '^[-/]' "$src_rsp" \
      | grep -vE '^[-/](out|refout|doc):'
    echo "-out:\"$OUT/$name.dll\""
    printf '"%s"\n' "${sources[@]}"
  } > "$rsp"

  log="$OUT/$name.log"
  if (cd "$ROOT" && "$DOTNET" exec "$CSC" -nologo -noconfig "@$rsp") > "$log" 2>&1; then
    warns="$(grep -c 'warning CS' "$log" || true)"
    if [[ "$warns" -gt 0 ]]; then
      echo "  ⚠ $name — 0 помилок, $warns попереджень"
      grep 'warning CS' "$log" | sed 's|^|      |' | sort -u | head -8
      FAILED=1
    else
      echo "  ✔ $name ($((${#sources[@]})) файлів)"
    fi
  else
    echo "  ✘ $name — ПОМИЛКИ КОМПІЛЯЦІЇ"
    grep -E 'error CS' "$log" | sed 's|^|      |' | sort -u | head -10
    FAILED=1
  fi
done

echo
[[ $MISSING -eq 1 ]] && echo "Увага: частину збірок пропущено — дай Unity перекомпілювати проєкт."
echo "Результат: $([[ $FAILED -eq 0 ]] && echo 'чисто — 0 помилок, 0 попереджень' || echo 'Є ЗАУВАЖЕННЯ')"
exit $FAILED
