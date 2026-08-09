#!/usr/bin/env python3
"""
Шукає щокадрові дотики до UGUI-графіки.

Навіщо. Якщо анімація щокадру пише в Image.color, sizeDelta, anchoredPosition,
sprite чи кличе SetVerticesDirty, графіка проситься на перебудову. Коли це
збігається з проходом канваса, Unity кидає «Trying to add X for graphic rebuild
while we are already inside a graphic rebuild loop» — сотнями за кадр.

Правило: у Update/LateUpdate і в тілах твін-корутин можна чіпати лише
localPosition, localScale, localRotation і CanvasRenderer (SetColor/SetAlpha) —
вони не бруднять графіку.

SpriteRenderer не рахуємо: він живе поза канвасом, і його color безпечний.

Запуск: python3 Tools/check-ui-animation.py
"""
import re
import sys
import pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
SCRIPTS = ROOT / "Assets" / "_Scripts"

DANGER = [
    (r"\.color\s*=", "color"),
    (r"\.sizeDelta\s*=", "sizeDelta"),
    (r"\.anchoredPosition\s*=", "anchoredPosition"),
    (r"\.anchorM(?:in|ax)\s*=", "anchorMin/Max"),
    (r"\.offsetM(?:in|ax)\s*=", "offsetMin/Max"),
    (r"\.sprite\s*=", "sprite"),
    (r"\.fillAmount\s*=", "fillAmount"),
    (r"\.pixelsPerUnitMultiplier\s*=", "pixelsPerUnitMultiplier"),
    (r"SetAllDirty\(", "SetAllDirty"),
    (r"SetVerticesDirty\(", "SetVerticesDirty"),
    (r"SetLayoutDirty\(", "SetLayoutDirty"),
    (r"SetMaterialDirty\(", "SetMaterialDirty"),
]

# Будь-який метод, що повертає IEnumerator, — потенційна щокадрова петля:
# всередині майже напевно є `yield return null`. Спершу тут стояв список суфіксів
# (Routine/Tween/Anim), і корутини з іншими іменами — PlayMerge, PlayReject —
# перевірку просто обходили. Ім'я не є ознакою; тип повернення є.
PER_FRAME = re.compile(
    r"void\s+(?:Update|LateUpdate|FixedUpdate)\s*\(\s*\)"
    r"|IEnumerator\s+\w+\s*\("
)


def method_body(src: str, start: int):
    """Повертає тіло методу від першої { і його зсув у файлі."""
    try:
        open_at = src.index("{", start)
    except ValueError:
        return "", start
    depth = 0
    for i in range(open_at, len(src)):
        if src[i] == "{":
            depth += 1
        elif src[i] == "}":
            depth -= 1
            if depth == 0:
                return src[open_at:i + 1], open_at
    return src[open_at:], open_at


def sprite_renderer_fields(src: str):
    """Імена полів типу SpriteRenderer — вони поза канвасом і безпечні."""
    return set(re.findall(r"SpriteRenderer\s+(\w+)\s*;", src))


def main() -> int:
    findings = []

    for path in sorted(SCRIPTS.rglob("*.cs")):
        if f"{path.sep if hasattr(path, 'sep') else '/'}Editor/" in str(path).replace("\\", "/"):
            continue
        if "/Editor/" in str(path).replace("\\", "/"):
            continue

        src = path.read_text(encoding="utf-8")
        safe = sprite_renderer_fields(src)

        for match in PER_FRAME.finditer(src):
            body, offset = method_body(src, match.end())
            for pattern, label in DANGER:
                for hit in re.finditer(pattern, body):
                    # Ліворуч від крапки — приймач. Якщо це SpriteRenderer, пропускаємо.
                    prefix = body[max(0, hit.start() - 40):hit.start()]
                    receiver = re.search(r"(\w+)\s*$", prefix)
                    if receiver and receiver.group(1) in safe:
                        continue

                    line = src[:offset + hit.start()].count("\n") + 1
                    rel = path.relative_to(ROOT)
                    findings.append(f"  ✗ {rel}:{line}  {label}  у {match.group(0).strip()}")

    if findings:
        print("Щокадрові дотики до UGUI-графіки:\n")
        print("\n".join(findings))
        print(f"\nЗнайдено: {len(findings)}. Переведи на localPosition/localScale/CanvasRenderer.")
        return 1

    print("Анімація UI: чисто — жодного щокадрового дотику до графіки.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
