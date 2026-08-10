#!/usr/bin/env python3
"""
Перевіряє, що кожна подія, яку екран випромінює назовні, справді кудись веде.

Причина існування: після дев'яти екранів усі дев'ять наборів подій
(`BackRequested`, `PlayRequested`, `PaintRequested`…) були оголошені, задокументовані
— і не підписані ЖОДНОГО разу. Компілятор такого не бачить, тести теж: подія просто
нікуди не стріляє, і кнопка мовчить. Єдиний спосіб помітити — натиснути її руками
або перевірити зв'язок статично.

Правило: подія `public System.Action…` в `UI/**/*Screen.cs` мусить мати рівно одну
підписку `<щось>.<Ім'я> +=` в `AppRouter.cs`.
"""

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
UI = ROOT / "Assets/_Scripts/UI"
ROUTER = UI / "Common/AppRouter.cs"

EVENT = re.compile(r"public\s+(?:System\.)?Action(?:<[^>]*>)?\??\s+(\w+)\s*;")


def screen_events():
    """{файл: [події]} для всіх екранів."""
    found = {}
    for path in sorted(UI.rglob("*Screen.cs")):
        names = EVENT.findall(path.read_text(encoding="utf-8"))
        if names:
            found[path] = names
    return found


def main():
    if not ROUTER.exists():
        print(f"Навігація: НЕ знайдено {ROUTER.relative_to(ROOT)}")
        return 1

    router = ROUTER.read_text(encoding="utf-8")
    events = screen_events()

    dangling = []
    total = 0
    for path, names in events.items():
        for name in names:
            total += 1
            # Підписка виглядає як `screen.Name +=` — крапка обов'язкова,
            # інакше збіглося б із оголошенням у самому екрані.
            if not re.search(rf"\.{re.escape(name)}\s*\+=", router):
                dangling.append(f"{path.relative_to(ROOT)}  →  {name}")

    if dangling:
        print("Події екранів, які нікуди не ведуть:\n")
        for item in dangling:
            print(f"  ✗ {item}")
        print(f"\nЗнайдено: {len(dangling)} з {total}. "
              f"Підпиши їх в AppRouter або прибери подію.")
        return 1

    print(f"Навігація: чисто — усі {total} подій з {len(events)} екранів підв'язані.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
