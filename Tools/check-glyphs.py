#!/usr/bin/env python3
"""
Шукає символи в написах, яких немає у згенерованому шрифті.

Навіщо. TMP не падає на відсутньому гліфі — він мовчки підставляє порожній
квадрат і пише попередження в консоль. Помітно це лише очима на екрані, і вже
чотири рази ловилось саме так: ★, ✓, ↺, ✎.

Набір гліфів читається з `GenerateFontAsset.BuildCharacterSet()` — не
дублюється тут, тож розійтись вони не можуть.

Правило, за яким вирішується доля символу:
  • ТЕКСТ (пунктуація, знаки: ‹ › № − «») — додати гліф у шрифт;
  • КАРТИНКА (★ ✓ ↺ ✎ 🔒) — намалювати спрайтом у GenerateUISprites.

Запуск: python3 Tools/check-glyphs.py
"""
import re
import sys
import pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
FONT_BUILDER = ROOT / "Assets" / "_Scripts" / "Editor" / "GenerateFontAsset.cs"
SCRIPTS = ROOT / "Assets" / "_Scripts"


def font_charset() -> set:
    """Збирає набір гліфів так само, як це робить BuildCharacterSet()."""
    if not FONT_BUILDER.exists():
        print(f"Немає {FONT_BUILDER} — перевірити нема з чим.", file=sys.stderr)
        sys.exit(2)

    source = FONT_BUILDER.read_text(encoding="utf-8")
    chars = set()

    for lo, hi in re.findall(r"AddRange\(sb,\s*0x([0-9A-Fa-f]+),\s*0x([0-9A-Fa-f]+)\)", source):
        for code in range(int(lo, 16), int(hi, 16) + 1):
            chars.add(chr(code))

    # Без префікса sb: виклики бувають ланцюжком — sb.Append('‹').Append('›').
    for ch in re.findall(r"\.Append\('(.)'\)", source):
        chars.add(ch)

    # Керівні символи в написах трапляються законно.
    chars.update("\n\t")
    return chars


def ui_strings():
    """Рядки, що справді потрапляють у TMP: аргумент Label(...) і присвоєння .text."""
    label_arg = re.compile(r'Label\(\s*[^,]+,\s*"[^"]*",\s*("(?:[^"\\]|\\.)*")')
    text_assign = re.compile(r'\.text\s*=\s*(\$?"(?:[^"\\]|\\.)*")')

    for path in sorted(SCRIPTS.rglob("*.cs")):
        source = path.read_text(encoding="utf-8")
        for line_no, line in enumerate(source.splitlines(), 1):
            for pattern in (label_arg, text_assign):
                for match in pattern.finditer(line):
                    yield path, line_no, match.group(1)


def main() -> int:
    charset = font_charset()
    findings = []

    for path, line_no, literal in ui_strings():
        # Теги TMP — не текст: <sprite name="star">, <b>, <s>.
        text = re.sub(r"<[^>]+>", "", literal)
        for ch in text:
            if ch in charset or ch in '"${}\\':
                continue
            rel = path.relative_to(ROOT)
            findings.append((f"{rel}:{line_no}", ch))

    if not findings:
        print("Гліфи: чисто — усі символи написів є в атласі шрифту.")
        return 0

    print("Символи, яких немає у згенерованому шрифті:\n")
    seen = set()
    for where, ch in findings:
        key = (where, ch)
        if key in seen:
            continue
        seen.add(key)
        print(f"  ✗ {where}  «{ch}»  U+{ord(ch):04X}")

    print("\nТекстовий знак — додай у GenerateFontAsset.BuildCharacterSet().")
    print("Піктограма — намалюй спрайтом у GenerateUISprites і встав як Image.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
