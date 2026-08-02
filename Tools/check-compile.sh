#!/usr/bin/env bash
# Компіляційна перевірка без відкриття Unity.
#
# КРИТИЧНО: компілює КОЖНУ збірку окремо, в порядку залежностей, з її власним csc.rsp —
# рівно так, як це робить Unity. Монолітна компіляція «всіх .cs разом» дає хибне зелене:
# вона робить internal-члени видимими між збірками і застосовує nullable-контекст глобально,
# тому пропускає і CS1061 (internal з іншої збірки), і CS8632 (анотація без контексту).
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt")"
SCRIPTING="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/Resources/Scripting"
DOTNET="$SCRIPTING/DotNetSdk/dotnet"
CSC="$(ls "$SCRIPTING"/DotNetSdk/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | head -1)"
PKG="$ROOT/Library/ScriptAssemblies"
OUT="${TMPDIR:-/tmp}/inkflow-compile"

[[ -x "$DOTNET" && -n "$CSC" ]] || { echo "Не знайдено компілятор Unity ($UNITY_VERSION)"; exit 1; }
[[ -d "$PKG" ]] || { echo "Немає $PKG — відкрий проєкт в Unity хоча б раз"; exit 1; }

rm -rf "$OUT"; mkdir -p "$OUT"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

# NUnit лежить у PackageCache, а не в ScriptAssemblies — шлях містить хеш версії пакета.
NUNIT_DLL="$(find "$ROOT/Library/PackageCache" -name 'nunit.framework.dll' 2>/dev/null | head -1)"

# Шляхи проєкту містять пробіли, тож КОЖЕН аргумент у .rsp має бути в лапках,
# а списки — збиратись без word splitting. Саме на цьому падала перша версія скрипта.
BASE_REFS=()
for d in "$SCRIPTING/NetStandard/ref/2.1.0"/*.dll \
         "$SCRIPTING/NetStandard/compat/2.1.0/shims/netstandard"/*.dll \
         "$SCRIPTING/NetStandard/compat/2.1.0/shims/netfx"/*.dll \
         "$SCRIPTING/Managed/UnityEngine"/*.dll; do
  BASE_REFS+=("-r:\"$d\"")
done

FAILED=0

# build <ім'я збірки> <тека з кодом> [посилання...]
build() {
  local name="$1" dir="$2"; shift 2
  local rsp="$OUT/$name.rsp"

  # Файли з вкладених asmdef належать ІНШІЙ збірці — Unity їх сюди не включає.
  local prune=()
  while IFS= read -r nested; do
    local nested_dir; nested_dir="$(dirname "$nested")"
    [[ "$nested_dir" == "$ROOT/$dir" ]] && continue
    prune+=(-path "$nested_dir" -prune -o)
  done < <(find "$ROOT/$dir" -name '*.asmdef')

  # ${prune[@]+...} — щоб порожній масив не падав під set -u.
  local sources=()
  while IFS= read -r file; do sources+=("$file"); done \
    < <(find "$ROOT/$dir" ${prune[@]+"${prune[@]}"} -name '*.cs' -print)
  [[ ${#sources[@]} -gt 0 ]] || { echo "  · $name — немає .cs, пропущено"; return; }

  {
    printf '%s\n' "${BASE_REFS[@]}"
    for ref in "$@"; do
      if [[ -f "$OUT/$ref.dll" ]]; then echo "-r:\"$OUT/$ref.dll\""
      elif [[ -f "$PKG/$ref.dll" ]]; then echo "-r:\"$PKG/$ref.dll\""
      elif [[ -n "${NUNIT_DLL:-}" && "$ref" == "nunit.framework" ]]; then echo "-r:\"$NUNIT_DLL\""
      else echo "  !! немає посилання $ref" >&2; fi
    done
    # Власний csc.rsp збірки — саме він задає nullable-контекст.
    # Передаємо ДОСЛІВНО, без фільтрації: Unity не підтримує коментарі в csc.rsp
    # і тлумачить їхній текст як імена файлів (CS2001). Якщо тут «прибирати сміття»,
    # перевірка буде зеленою там, де редактор упаде.
    [[ -f "$ROOT/$dir/csc.rsp" ]] && cat "$ROOT/$dir/csc.rsp"
    printf '"%s"\n' "${sources[@]}"
  } > "$rsp"

  local log="$OUT/$name.log"
  # -nowarn:0169,0649 — те саме, що глушить сам Unity: поля [SerializeField] заповнює
  # десеріалізація, тож «never assigned» для них структурно хибне.
  if "$DOTNET" exec "$CSC" -nologo -noconfig -nostdlib -target:library \
      -nowarn:0169,0649 -define:UNITY_EDITOR -define:UNITY_INCLUDE_TESTS \
      -out:"$OUT/$name.dll" "@$rsp" > "$log" 2>&1; then
    local warns; warns="$(grep -c 'warning CS' "$log" || true)"
    if [[ "$warns" -gt 0 ]]; then
      echo "  ⚠ $name — 0 помилок, $warns попереджень"
      grep 'warning CS' "$log" | sed 's|^|      |' | sort -u | head -5
    else
      echo "  ✔ $name"
    fi
  else
    echo "  ✘ $name — ПОМИЛКИ КОМПІЛЯЦІЇ"
    grep 'error CS' "$log" | sed 's|^|      |' | sort -u | head -10
    FAILED=1
  fi
}

echo "Компіляція по збірках (як в Unity):"

build InkFlow.Core      Assets/_Scripts/Core
build InkFlow.Platform  Assets/_Scripts/Platform
build InkFlow.Meta      Assets/_Scripts/Meta      InkFlow.Core
build InkFlow.Gameplay  Assets/_Scripts/Gameplay  InkFlow.Core InkFlow.Platform \
      Unity.InputSystem Unity.TextMeshPro UnityEngine.UI
build InkFlow.UI        Assets/_Scripts/UI        InkFlow.Core InkFlow.Meta \
      UnityEngine.UI Unity.TextMeshPro
build InkFlow.App       Assets/_Scripts/App       InkFlow.Core InkFlow.Gameplay InkFlow.UI \
      InkFlow.Meta InkFlow.Platform Unity.Addressables Unity.ResourceManager
build InkFlow.Editor    Assets/_Scripts/Editor    InkFlow.Core InkFlow.Gameplay InkFlow.UI \
      InkFlow.Meta InkFlow.Platform InkFlow.App Unity.Addressables Unity.Addressables.Editor \
      Unity.ResourceManager Unity.InputSystem Unity.TextMeshPro UnityEngine.UI \
      Unity.RenderPipelines.Universal.Runtime Unity.RenderPipelines.Universal.2D.Runtime

# Тести — ОКРЕМІ збірки: лише так видно, чи справді відкрито internal-члени
# через InternalsVisibleTo (монолітна компіляція це питання приховує).
build InkFlow.Core.Tests Assets/Tests/EditMode InkFlow.Core nunit.framework
build InkFlow.Meta.Tests Assets/Tests/EditMode/Meta InkFlow.Core InkFlow.Meta nunit.framework

echo
echo "Результат: $([[ $FAILED -eq 0 ]] && echo 'усі збірки компілюються' || echo 'Є ПОМИЛКИ')"
exit $FAILED
