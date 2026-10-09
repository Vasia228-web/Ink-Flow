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
    """
    Рядки, що справді потрапляють у TMP: аргумент Label(...), присвоєння .text, а також вирази, з яких
    текст збирається далі — повернення `=> "…"` і гілки тернара `? "…" : "…"` (підказки ніка й підпис
    вітрини саме такі; перша редакція чекера їх не бачила, і «чисто» для них було випадковим).
    """
    label_arg = re.compile(r'Label\(\s*[^,]+,\s*"[^"]*",\s*("(?:[^"\\]|\\.)*")')
    text_assign = re.compile(r'\.text\s*=\s*(\$?"(?:[^"\\]|\\.)*")')
    # В екранах UI майже кожен рядковий літерал — напис (назви об'єктів і спрайтів — ASCII, їх перевірка
    # не чіпає), тож там беремо ВСІ літерали: вирази-повернення, обидві гілки тернара на одному рядку,
    # кортежі у switch-виразах. Патерни вище лишаються для збирачів і решти коду.
    any_literal = re.compile(r'(\$?"(?:[^"\\]|\\.)*")')

    for path in sorted(SCRIPTS.rglob("*.cs")):
        source = path.read_text(encoding="utf-8")
        # Вирази й гілки тернара — лише в екранах UI: у збирачах, конфігах і бутстрапі такі рядки —
        # логи й підказки інспектора, а в TMP потрапляє лише те, що передано в Label(...).
        in_ui = "/UI/" in path.as_posix()
        for line_no, line in enumerate(source.splitlines(), 1):
            stripped = line.strip()
            # Логи, винятки, коментарі й хвости склеєних рядків (+ "…") у TMP не потрапляють.
            if ("Debug.Log" in stripped or "Exception(" in stripped or stripped.startswith("//")
                    or stripped.startswith('"') or stripped.startswith('+ "')
                    or "Tooltip(" in stripped or "Header(" in stripped):
                continue
            patterns = (any_literal,) if in_ui else (label_arg, text_assign)
            for pattern in patterns:
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
