#!/usr/bin/env python3
"""
Авторський генератор бібліотеки картинок Ink Flow (~32 px, колірні родини, кроки).

Джерело правди для гри — текстові файли в Assets/_Pictures/<тема>/<id>.txt
(формат у docs/pictures-format.md). Цей скрипт лише ВИРОБЛЯЄ їх із фігур нижче
(Tools/pictures/raster.py): кладе основний тон родини, домальовує світлу грань і тінь,
іскри, зовнішній контур, добирає крок під ціль рідкості й перевіряє правила.
Художник може правити .txt руками або дописувати нові — скрипт не потрібен для гри.

Кожна картинка: 4–6 родин (щонайменше три з них із затіненими фігурами — це дає
10–16 тонів), ~32 px по довшій стороні, контур домальовується сам.

Запуск: python3 Tools/pictures/author.py            (пише файли)
        python3 Tools/pictures/author.py --check    (лише перевіряє)
"""
import math as _m
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from raster import Pic, write_all  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "_Pictures")

PICTURES = []


def pic(pid, name, theme, rarity, w, h, outline="ink"):
    p = Pic(pid, name, theme, rarity, w, h, outline)
    PICTURES.append(p)
    return p


# ── спільні деталі ──

def eyes(p, family, pts, r=1.6, pupil=True, look=(0, 0)):
    """Білки очей — білий тон родини тіла, зіниці — контур."""
    for (x, y) in pts:
        p.circle(x, y, r, family, tone="white", shade=False)
        if pupil:
            p.ink([(int(round(x + look[0])), int(round(y + look[1])))])


def smile(p, x0, x1, y, depth=1):
    cells = []
    n = x1 - x0
    for i in range(n + 1):
        t = i / max(n, 1)
        cells.append((x0 + i, y + int(round(depth * (1 - (2 * t - 1) ** 2)))))
    p.ink(cells)


def cheeks(p, pts, rx=1.5, ry=1, family="blush"):
    for (x, y) in pts:
        p.ellipse(x, y, rx, ry, family, shade=False)


def ground(p, cx, y, rx, family="slate"):
    """Тінь-підставка під предметом: затінений еліпс у три ряди — окрема родина з трьома тонами."""
    p.ellipse(cx, y, rx, 1.6, family)


def sparkles(p, pts, family="cream"):
    p.dots(pts, family)


def star_points(cx, cy, r_out, r_in, n=5, rot=-_m.pi / 2):
    pts = []
    for i in range(2 * n):
        a = rot + i * _m.pi / n
        r = r_out if i % 2 == 0 else r_in
        pts.append((cx + r * _m.cos(a), cy + r * _m.sin(a)))
    return pts


# ═══════════════════════════ ТВАРИНИ ═══════════════════════════

p = pic("duck", "КАЧЕНЯ", "animals", "common", 32, 30)
ground(p, 16, 28, 13, "cyan")                              # калюжка
p.ellipse(15, 19, 12, 8, "lemon", spark=True)              # тіло
p.ellipse(9, 23, 5, 3, "yellow", spark=True)               # крило
p.circle(21, 9, 7, "lemon", spark=True)
p.poly([(26, 8), (32, 10), (26, 12)], "orange", spark=True)
eyes(p, "lemon", [(22, 8)], r=1.5)
p.rect(11, 26, 3, 2, "orange", shade=False)
p.rect(17, 26, 3, 2, "orange", shade=False)
cheeks(p, [(18, 12)])

p = pic("chick", "КУРЧА", "animals", "common", 30, 30)
ground(p, 15, 28, 12, "lime")
p.ellipse(15, 18, 11, 9, "yellow", spark=True)
p.circle(15, 9, 8, "yellow", spark=True)
p.ellipse(6, 19, 4, 5, "lemon", spark=True)
p.ellipse(24, 19, 4, 5, "lemon", spark=True)
p.poly([(13, 11), (17, 11), (15, 15)], "orange", spark=True)
eyes(p, "yellow", [(12, 8), (18, 8)], r=1.4)
p.rect(11, 26, 3, 2, "orange", shade=False)
p.rect(16, 26, 3, 2, "orange", shade=False)
p.dots([(14, 1), (15, 0), (16, 1)], "orange")
cheeks(p, [(10, 11), (20, 11)])

p = pic("frog", "ЖАБКА", "animals", "common", 32, 28)
p.ellipse(16, 26, 12, 1.8, "cyan")                          # латаття
p.ellipse(16, 15, 15, 9, "green", spark=True)
p.circle(8, 6, 5, "green", spark=True)
p.circle(24, 6, 5, "green", spark=True)
eyes(p, "green", [(8, 6), (24, 6)], r=2.6)
p.ellipse(16, 20, 10, 3.5, "lime", spark=True)
p.ellipse(4, 22, 3, 2, "green")
p.ellipse(28, 22, 3, 2, "green")
smile(p, 10, 22, 13, depth=2)
cheeks(p, [(6, 12), (26, 12)], family="pink")
p.circle(28, 3, 2.2, "yellow", spark=True)                 # мушка
p.ink_line(29, 1, 31, 0)

p = pic("pig", "ПОРОСЯ", "animals", "common", 30, 30)
ground(p, 15, 28, 12, "bark")                              # калюжа болота
p.ellipse(15, 15, 14, 12, "pink", spark=True)
p.poly([(2, 6), (7, 0), (10, 8)], "pink")
p.poly([(28, 6), (23, 0), (20, 8)], "pink")
p.ellipse(15, 19, 6, 4, "blush", spark=True)
p.ink([(13, 19), (17, 19)])
eyes(p, "pink", [(10, 11), (20, 11)], r=1.6)
p.rect(8, 26, 4, 2, "raspberry", shade=False)
p.rect(18, 26, 4, 2, "raspberry", shade=False)
p.poly([(24, 2), (30, 4), (26, 8)], "green", spark=True)   # листочок

p = pic("bunny", "ЗАЙЧИК", "animals", "common", 30, 32)
ground(p, 15, 30, 12, "lime")
p.ellipse(9, 8, 3.5, 8, "cream", spark=True)
p.ellipse(21, 8, 3.5, 8, "cream", spark=True)
p.ellipse(9, 8, 1.5, 5, "blush", shade=False)
p.ellipse(21, 8, 1.5, 5, "blush", shade=False)
p.ellipse(15, 22, 12, 9, "cream", spark=True)
eyes(p, "cream", [(11, 20), (19, 20)], r=1.6)
p.ellipse(15, 25, 2, 1.5, "pink", shade=False)
p.ink([(13, 27), (14, 28), (16, 28), (17, 27)])
cheeks(p, [(7, 25), (23, 25)], rx=2, ry=1.2)
p.ellipse(27, 24, 3, 3.5, "orange", spark=True)            # морквина
p.poly([(26, 18), (29, 21), (24, 21)], "green")

p = pic("fox", "ЛИСИЧКА", "animals", "uncommon", 32, 30)
ground(p, 16, 28, 13, "green")
p.poly([(1, 2), (9, 12), (2, 14)], "orange")
p.poly([(31, 2), (23, 12), (30, 14)], "orange")
p.ellipse(16, 17, 15, 10, "orange", spark=True)
p.poly([(4, 18), (16, 27), (28, 18), (16, 16)], "cream", spark=True)
p.ellipse(16, 23, 2.5, 1.8, "bark", shade=False)
eyes(p, "orange", [(11, 15), (21, 15)], r=1.5, look=(0.4, 0))
p.dots([(4, 5), (28, 5)], "cream")
p.ink([(14, 26), (15, 27), (17, 27), (18, 26)])
cheeks(p, [(7, 20), (25, 20)], family="pink")

p = pic("bear", "ВЕДМЕДИК", "animals", "uncommon", 30, 30)
ground(p, 15, 28, 12, "green")
p.circle(6, 6, 5, "bark", spark=True)
p.circle(24, 6, 5, "bark", spark=True)
p.circle(6, 6, 2.5, "caramel", shade=False)
p.circle(24, 6, 2.5, "caramel", shade=False)
p.ellipse(15, 16, 14, 11, "bark", spark=True)
p.ellipse(15, 21, 7, 5, "caramel", spark=True)
p.ink([(14, 19), (15, 19), (16, 19), (15, 20)])
eyes(p, "bark", [(10, 13), (20, 13)], r=1.5)
smile(p, 12, 18, 23, depth=1)
p.circle(27, 22, 3.5, "yellow", spark=True)                # горщик меду
p.rect(25, 18, 5, 2, "orange", shade=False)
cheeks(p, [(7, 17), (23, 17)])

p = pic("owl", "СОВЕНЯ", "animals", "rare", 30, 32)
p.rect(2, 28, 26, 3, "bark", r=1, spark=True)              # гілка
p.poly([(3, 0), (9, 8), (2, 10)], "slate")
p.poly([(27, 0), (21, 8), (28, 10)], "slate")
p.ellipse(15, 16, 14, 13, "slate", spark=True)
p.ellipse(15, 22, 9, 6, "caramel", spark=True)
p.ellipse(9, 12, 5.5, 5.5, "cream", shade=False)
p.ellipse(21, 12, 5.5, 5.5, "cream", shade=False)
eyes(p, "slate", [(9, 12), (21, 12)], r=3)
p.poly([(13, 15), (17, 15), (15, 19)], "orange", spark=True)
p.dots([(12, 21), (14, 23), (16, 21), (18, 23), (13, 25), (17, 25)], "caramel", tone="shadow")
p.rect(9, 28, 4, 2, "orange", shade=False)
p.rect(17, 28, 4, 2, "orange", shade=False)
p.dots([(4, 30), (26, 30)], "green")

p = pic("penguin", "ПІНГВІН", "animals", "common", 28, 32)
ground(p, 14, 30, 12, "ice")
p.ellipse(14, 17, 12, 12, "slate", spark=True)
p.ellipse(14, 20, 7.5, 8, "cream", spark=True)
p.ellipse(14, 8, 8, 7, "slate", spark=True)
p.ellipse(14, 9, 4, 3, "cream", shade=False)
eyes(p, "slate", [(11, 7), (17, 7)], r=1.3)
p.poly([(12, 10), (16, 10), (14, 13)], "orange", spark=True)
p.ellipse(3, 18, 2.5, 6, "slate")
p.ellipse(25, 18, 2.5, 6, "slate")
p.rect(8, 28, 5, 2, "orange", shade=False)
p.rect(15, 28, 5, 2, "orange", shade=False)
p.rect(5, 13, 18, 3, "red", spark=True)                    # шарфик
cheeks(p, [(9, 10), (19, 10)])

p = pic("panda", "ПАНДА", "animals", "uncommon", 30, 30)
ground(p, 15, 28, 12, "lime")
p.circle(6, 5, 4.5, "slate", spark=True)
p.circle(24, 5, 4.5, "slate", spark=True)
p.ellipse(15, 16, 14, 11, "cream", spark=True)
p.ellipse(10, 14, 4, 4.5, "slate", shade=False)
p.ellipse(20, 14, 4, 4.5, "slate", shade=False)
eyes(p, "slate", [(10, 14), (20, 14)], r=1.6)
p.ellipse(15, 20, 2, 1.5, "slate", shade=False)
smile(p, 12, 18, 23, depth=1)
cheeks(p, [(5, 19), (25, 19)], family="pink")
p.rect(26, 16, 3, 10, "green", spark=True)                 # бамбук
p.dots([(27, 19), (27, 23)], "green", tone="shadow")

p = pic("cow", "КОРІВКА", "animals", "rare", 32, 30)
ground(p, 16, 28, 14, "green")
p.ellipse(16, 17, 15, 11, "cream", spark=True)
p.ellipse(16, 23, 9, 5, "pink", spark=True)
p.ellipse(12, 23, 1.5, 1, "pink", tone="shadow", shade=False)
p.ellipse(20, 23, 1.5, 1, "pink", tone="shadow", shade=False)
p.ellipse(7, 12, 5, 4, "slate", shade=False)
p.ellipse(24, 10, 4, 3, "slate", shade=False)
p.ellipse(4, 8, 3, 2, "cream")
p.ellipse(28, 8, 3, 2, "cream")
p.poly([(8, 1), (10, 8), (5, 8)], "caramel", spark=True)
p.poly([(24, 1), (22, 8), (27, 8)], "caramel", spark=True)
eyes(p, "cream", [(11, 16), (21, 16)], r=1.6)
p.circle(16, 5, 2, "caramel", tone="light", shade=False)  # дзвіночок
p.dots([(4, 26), (28, 26)], "slate")

p = pic("hedgehog", "ЇЖАЧОК", "animals", "uncommon", 32, 28)
ground(p, 16, 26, 13, "green")
p.ellipse(19, 14, 12, 10, "bark", spark=True)
for i, (x, y) in enumerate([(9, 6), (13, 2), (18, 1), (23, 2), (27, 5), (30, 9), (31, 14)]):
    p.poly([(x - 2, y + 6), (x, y), (x + 2, y + 6)], "bark" if i % 2 else "caramel")
p.ellipse(9, 18, 8, 6, "peach", spark=True)
p.ellipse(2, 19, 2, 1.5, "bark", shade=False)
eyes(p, "peach", [(8, 16)], r=1.4)
p.rect(8, 23, 4, 3, "bark", shade=False)
p.rect(20, 23, 4, 3, "bark", shade=False)
cheeks(p, [(11, 20)], family="pink")
p.circle(22, 6, 2.6, "red", shade=False)                   # яблучко на голках
p.dots([(21, 5)], "red", tone="light")

p = pic("mouse", "МИШКА", "animals", "common", 32, 28)
ground(p, 16, 26, 13, "sand")
p.circle(7, 6, 5.5, "slate", spark=True)
p.circle(25, 6, 5.5, "slate", spark=True)
p.circle(7, 6, 3, "pink", shade=False)
p.circle(25, 6, 3, "pink", shade=False)
p.ellipse(16, 15, 13, 10, "slate", spark=True)
p.ellipse(16, 20, 6, 4, "silver", spark=True)
eyes(p, "slate", [(11, 13), (21, 13)], r=1.5)
p.ellipse(16, 19, 2, 1.5, "pink", shade=False)
p.line(3, 22, 9, 21, "slate", tone="light", width=1)
p.line(23, 21, 29, 22, "slate", tone="light", width=1)
p.poly([(27, 22), (32, 18), (31, 25)], "yellow", spark=True)   # сир
p.dots([(29, 21)], "yellow", tone="shadow")


def cat_base(p, coat="orange", belly="cream"):
    ground(p, 15, 28, 12, "sand")
    p.poly([(1, 2), (10, 10), (2, 12)], coat)
    p.poly([(29, 2), (20, 10), (28, 12)], coat)
    p.poly([(3, 5), (8, 10), (4, 11)], "pink", shade=False)
    p.poly([(27, 5), (22, 10), (26, 11)], "pink", shade=False)
    p.ellipse(15, 17, 14, 10, coat, spark=True)
    p.ellipse(15, 22, 8, 5, belly, spark=True)
    eyes(p, coat, [(10, 15), (20, 15)], r=1.8)
    p.ellipse(15, 20, 2, 1.4, "pink", shade=False)
    p.ink([(13, 23), (14, 24), (16, 24), (17, 23)])
    p.line(2, 21, 7, 20, coat, tone="light")
    p.line(23, 20, 28, 21, coat, tone="light")


p = pic("cat", "КОТИК", "animals", "common", 30, 30)
cat_base(p)
p.circle(27, 24, 2.6, "red", spark=True)                   # клубок
p.dots([(6, 4), (24, 4)], "orange", tone="light")

p = pic("cat_bow", "КОТИК ІЗ БАНТОМ", "animals", "uncommon", 30, 30)
cat_base(p)
p.poly([(20, 1), (28, 0), (27, 8), (22, 6)], "red", spark=True)
p.poly([(28, 0), (32, 3), (30, 9), (27, 8)], "red")
p.circle(27, 5, 1.6, "raspberry", shade=False)

p = pic("cat_hat", "КОТИК У КАПЕЛЮСІ", "animals", "uncommon", 30, 32)
ground(p, 15, 30, 12, "sand")
p.poly([(1, 6), (10, 14), (2, 16)], "orange")
p.poly([(29, 6), (20, 14), (28, 16)], "orange")
p.ellipse(15, 21, 14, 10, "orange", spark=True)
p.ellipse(15, 26, 8, 4.5, "cream", spark=True)
eyes(p, "orange", [(10, 19), (20, 19)], r=1.8)
p.ellipse(15, 24, 2, 1.4, "pink", shade=False)
p.ink([(13, 27), (14, 28), (16, 28), (17, 27)])
p.rect(3, 10, 24, 3, "violet", r=1, spark=True)
p.rect(8, 1, 14, 10, "violet", r=2, spark=True)
p.rect(8, 8, 14, 2, "magenta", shade=False)

p = pic("cat_glasses", "КОТИК В ОКУЛЯРАХ", "animals", "rare", 30, 30)
cat_base(p, coat="slate", belly="silver")
p.circle(10, 15, 3.8, "cyan", tone="light", shade=False)
p.circle(20, 15, 3.8, "cyan", tone="light", shade=False)
p.circle(10, 15, 2.6, "cyan", spark=True)
p.circle(20, 15, 2.6, "cyan", spark=True)
p.ink([(13, 15), (14, 15), (15, 15), (16, 15), (17, 15)])
p.ink([(9, 14), (19, 14)])
p.rect(11, 25, 8, 2, "pink", shade=False)                  # краватка-метелик
p.circle(15, 26, 1.4, "pink", tone="shadow", shade=False)

p = pic("hamster", "ХОМ'ЯЧОК", "animals", "common", 30, 28)
ground(p, 15, 26, 12, "sand")
p.circle(6, 6, 4, "caramel", spark=True)
p.circle(24, 6, 4, "caramel", spark=True)
p.ellipse(15, 15, 14, 10.5, "caramel", spark=True)
p.ellipse(8, 18, 6, 5, "cream", spark=True)
p.ellipse(22, 18, 6, 5, "cream", spark=True)
p.ellipse(15, 20, 4, 4, "cream", shade=False)
eyes(p, "caramel", [(10, 12), (20, 12)], r=1.5)
p.ellipse(15, 17, 2, 1.4, "pink", shade=False)
p.ink([(14, 21), (16, 21)])
p.circle(4, 22, 2.2, "yellow", spark=True)                 # зернятко
cheeks(p, [(3, 16), (27, 16)])

p = pic("sheep", "ОВЕЧКА", "animals", "common", 32, 28)
ground(p, 16, 26, 14, "green")
for (x, y) in [(6, 10), (12, 6), (19, 5), (26, 8), (28, 15), (23, 21), (14, 22), (7, 18)]:
    p.circle(x, y, 5, "cream", spark=(x == 12))
p.ellipse(17, 14, 10, 8, "cream")
p.ellipse(6, 15, 5.5, 5, "slate", spark=True)
p.ellipse(2, 12, 2, 1.5, "slate")
eyes(p, "slate", [(5, 14)], r=1.4)
p.rect(11, 22, 3, 4, "slate", shade=False)
p.rect(21, 22, 3, 4, "slate", shade=False)
cheeks(p, [(8, 17)], family="pink")
p.circle(28, 24, 2, "yellow", spark=True)                  # квіточка

p = pic("bat", "КАЖАНЧИК", "animals", "uncommon", 32, 26)
p.circle(26, 4, 3.5, "yellow", spark=True)                 # місяць
p.poly([(0, 8), (10, 14), (12, 22), (7, 17), (3, 21), (2, 14)], "violet", spark=True)
p.poly([(32, 8), (22, 14), (20, 22), (25, 17), (29, 21), (30, 14)], "violet", spark=True)
p.ellipse(16, 15, 8, 7, "periwinkle", spark=True)
p.poly([(9, 4), (13, 10), (8, 11)], "periwinkle")
p.poly([(23, 4), (19, 10), (24, 11)], "periwinkle")
eyes(p, "periwinkle", [(13, 14), (19, 14)], r=1.6)
p.dots([(14, 18), (18, 18)], "periwinkle", tone="white")
p.ink([(15, 18), (16, 18), (17, 18)])
cheeks(p, [(10, 17), (22, 17)], family="pink")
ground(p, 16, 24, 8, "slate")

p = pic("koala", "КОАЛА", "animals", "uncommon", 32, 28)
p.rect(14, 22, 4, 6, "bark", spark=True)                   # гілка
p.circle(5, 8, 5.5, "slate", spark=True)
p.circle(27, 8, 5.5, "slate", spark=True)
p.circle(5, 8, 3, "pink", shade=False)
p.circle(27, 8, 3, "pink", shade=False)
p.ellipse(16, 14, 13, 11, "slate", spark=True)
p.ellipse(16, 17, 3.5, 4.5, "bark", spark=True)
eyes(p, "slate", [(10, 12), (22, 12)], r=1.5)
p.ellipse(16, 23, 5, 2, "silver", shade=False)
cheeks(p, [(7, 18), (25, 18)], family="pink")
p.ellipse(27, 24, 4, 2, "green", spark=True)               # листя евкаліпта
p.ellipse(5, 24, 4, 2, "green", spark=True)

p = pic("unicorn", "ЄДИНОРІГ", "animals", "epic", 32, 32)
p.poly([(13, 0), (16, 12), (10, 12)], "yellow", spark=True)
p.ink_line(12, 4, 14, 3)
p.ink_line(12, 8, 15, 7)
p.ellipse(16, 20, 13, 11, "cream", spark=True)
p.ellipse(16, 25, 7, 4.5, "pink", shade=False)
p.poly([(2, 10), (9, 5), (11, 15), (3, 20), (1, 14)], "magenta", spark=True)
p.poly([(4, 18), (9, 15), (12, 24), (4, 27)], "violet", spark=True)
p.poly([(3, 24), (9, 22), (10, 30), (4, 31)], "blue", shade=False)
p.poly([(26, 6), (31, 2), (28, 14)], "cream")
eyes(p, "cream", [(20, 18)], r=2)
p.ink([(13, 26), (19, 26)])
cheeks(p, [(24, 23)], rx=2, ry=1.2, family="pink")
p.dots([(27, 20), (29, 26), (26, 29)], "cream", tone="white")

p = pic("dragon", "ДРАКОНЧИК", "animals", "legendary", 40, 34)
ground(p, 22, 32, 14, "slate")
p.poly([(2, 30), (12, 26), (16, 22), (12, 32), (4, 32)], "green")
p.ellipse(20, 22, 13, 10, "green", spark=True)
p.ellipse(20, 26, 8, 5, "lime", spark=True)
p.ellipse(28, 12, 10, 8, "green", spark=True)
p.ellipse(33, 15, 5, 3, "lime")
for (x, y) in [(10, 12), (15, 9), (21, 9), (26, 4)]:
    p.poly([(x - 3, y + 6), (x, y), (x + 3, y + 6)], "red", spark=(x == 15))
p.poly([(3, 12), (13, 6), (12, 20)], "teal", spark=True)
p.poly([(3, 12), (9, 20), (12, 20)], "teal", tone="light", shade=False)
eyes(p, "green", [(30, 11)], r=2)
p.ink([(36, 15), (37, 16)])
p.dots([(38, 12), (38, 10), (39, 11)], "orange")
p.rect(12, 30, 5, 3, "green", shade=False)
p.rect(22, 30, 5, 3, "green", shade=False)
p.dots([(13, 32), (15, 32), (23, 32), (25, 32)], "lime", tone="light")

# ═══════════════════════════ КОМАШКИ ═══════════════════════════

p = pic("bee", "БДЖІЛКА", "bugs", "common", 30, 26)
p.circle(26, 22, 3.5, "pink", spark=True)                  # квітка
p.circle(26, 22, 1.4, "yellow", shade=False)
p.ellipse(10, 6, 6, 4, "ice", spark=True)
p.ellipse(20, 6, 6, 4, "ice", spark=True)
p.ellipse(15, 15, 13, 8, "yellow", spark=True)
p.rect(8, 8, 3, 15, "slate", shade=False)
p.rect(15, 8, 3, 15, "slate", shade=False)
p.rect(22, 9, 3, 13, "slate", shade=False)
p.circle(4, 14, 5, "yellow", spark=True)
eyes(p, "yellow", [(3, 13)], r=1.4)
p.ink([(29, 15), (28, 16)])
p.ink_line(2, 8, 4, 10)
p.ink_line(7, 7, 6, 10)
smile(p, 2, 5, 17, depth=1)

p = pic("ladybug", "СОНЕЧКО-ЖУЧОК", "bugs", "common", 28, 28)
p.ellipse(14, 26, 12, 1.8, "green")                        # листок
p.ellipse(14, 15, 13, 10, "red", spark=True)
p.circle(14, 5, 5, "slate", spark=True)
p.ink_line(14, 9, 14, 24)
for (x, y) in [(7, 12), (20, 12), (9, 20), (19, 20), (14, 16)]:
    p.circle(x, y, 2, "slate", shade=False)
eyes(p, "slate", [(12, 5), (16, 5)], r=1.2)
p.ink_line(10, 1, 12, 3)
p.ink_line(18, 1, 16, 3)
p.dots([(4, 9), (24, 9)], "red", tone="white")
p.circle(3, 23, 2, "yellow", spark=True)

p = pic("beetle", "ЖУК-ОЛЕНЬ", "bugs", "rare", 30, 32)
ground(p, 15, 30, 12, "green")
p.poly([(8, 0), (12, 10), (7, 12), (5, 4)], "bark", spark=True)
p.poly([(22, 0), (18, 10), (23, 12), (25, 4)], "bark", spark=True)
p.dots([(6, 3), (24, 3), (9, 6), (21, 6)], "bark", tone="light")
p.ellipse(15, 13, 6, 4, "slate", spark=True)
p.ellipse(15, 23, 11, 8, "caramel", spark=True)
p.ink_line(15, 16, 15, 30)
p.ellipse(9, 21, 3, 4, "orange", shade=False)              # відблиск надкрил
p.ellipse(21, 21, 3, 4, "orange", shade=False)
for (x, y) in [(3, 18), (23, 18), (2, 24), (24, 24)]:
    p.rect(x, y, 4, 2, "slate", shade=False)
eyes(p, "slate", [(12, 13), (18, 13)], r=1.2)

p = pic("butterfly", "МЕТЕЛИК", "bugs", "uncommon", 32, 26)
p.ellipse(8, 8, 8, 7, "magenta", spark=True)
p.ellipse(24, 8, 8, 7, "magenta", spark=True)
p.ellipse(9, 19, 6, 5, "violet", spark=True)
p.ellipse(23, 19, 6, 5, "violet", spark=True)
p.circle(8, 8, 3, "yellow", spark=True)
p.circle(24, 8, 3, "yellow", spark=True)
p.circle(9, 19, 2, "cyan", shade=False)
p.circle(23, 19, 2, "cyan", shade=False)
p.rect(15, 5, 3, 18, "slate", r=1, spark=True)
p.ink_line(13, 1, 15, 4)
p.ink_line(19, 1, 17, 4)
p.dots([(16, 7), (16, 8)], "slate", tone="white")

p = pic("snail", "РАВЛИК", "bugs", "uncommon", 32, 26)
ground(p, 16, 24, 14, "green")
p.ellipse(11, 20, 10, 3.5, "lime", spark=True)
p.ellipse(25, 16, 6, 4, "lime")
p.circle(26, 8, 3.5, "lime", spark=True)
p.ink_line(27, 2, 26, 5)
p.ink_line(31, 2, 29, 5)
p.dots([(27, 1), (31, 1)], "lime")
eyes(p, "lime", [(27, 8)], r=1.2)
p.circle(11, 11, 9, "orange", spark=True)
p.circle(11, 11, 6, "peach", spark=True)
p.circle(11, 11, 3, "orange", shade=False)
p.ink([(11, 11)])
cheeks(p, [(29, 10)], family="pink")

# ═══════════════════════════ ДИНОЗАВРИКИ ═══════════════════════════

def dino(p, body, belly, spikes, plate="sand"):
    ground(p, 16, 30, 13, plate)
    p.poly([(0, 24), (8, 22), (10, 18), (7, 30), (1, 30)], body)
    p.ellipse(14, 22, 10, 8, body, spark=True)
    p.ellipse(16, 25, 6, 4, belly, spark=True)
    p.ellipse(24, 11, 8, 7, body, spark=True)
    for (x, y) in [(8, 10), (12, 7), (17, 6), (22, 3)]:
        p.poly([(x - 2, y + 5), (x, y), (x + 2, y + 5)], spikes, spark=(x == 12))
    eyes(p, body, [(26, 10)], r=1.6)
    p.ink([(30, 14), (31, 14)])
    p.rect(9, 28, 4, 3, body, shade=False)
    p.rect(19, 28, 4, 3, body, shade=False)


p = pic("dino_green", "ДИНОЗАВРИК", "dinos", "common", 32, 32)
dino(p, "green", "lime", "orange")
cheeks(p, [(21, 13)], family="pink")

p = pic("dino_blue", "СИНЬОЗАВР", "dinos", "common", 32, 32)
dino(p, "blue", "ice", "cyan")
cheeks(p, [(21, 13)], family="pink")

p = pic("stego", "СТЕГОЗАВРИК", "dinos", "uncommon", 32, 28)
ground(p, 16, 26, 14, "green")
p.poly([(0, 20), (8, 18), (6, 25)], "olive")
p.ellipse(16, 18, 13, 7, "olive", spark=True)
for (x, y) in [(6, 9), (12, 5), (19, 4), (25, 7)]:
    p.poly([(x - 3, y + 8), (x, y), (x + 3, y + 8)], "orange", spark=(x == 12))
p.ellipse(28, 16, 5, 4, "olive", spark=True)
eyes(p, "olive", [(29, 15)], r=1.3)
p.rect(8, 23, 4, 3, "olive", shade=False)
p.rect(20, 23, 4, 3, "olive", shade=False)
p.ellipse(16, 21, 7, 3, "lime", spark=True)
cheeks(p, [(26, 18)], family="pink")

p = pic("trice", "ТРИЦЕРАТОПСИК", "dinos", "rare", 32, 30)
ground(p, 16, 28, 14, "sand")
p.poly([(0, 20), (8, 18), (6, 26)], "teal")
p.ellipse(13, 19, 11, 8, "teal", spark=True)
p.ellipse(24, 14, 9, 8, "teal", spark=True)
p.ellipse(21, 10, 8, 7, "seafoam", spark=True)
p.poly([(16, 2), (19, 10), (14, 10)], "cream", spark=True)
p.poly([(26, 2), (28, 10), (23, 10)], "cream", spark=True)
p.poly([(31, 12), (32, 15), (28, 15)], "cream")
eyes(p, "teal", [(26, 14)], r=1.5)
p.ink([(30, 18), (31, 18)])
p.rect(6, 25, 4, 3, "teal", shade=False)
p.rect(17, 25, 4, 3, "teal", shade=False)
cheeks(p, [(23, 18)], family="pink")

p = pic("dino_egg", "ЯЙЦЕ ДИНОЗАВРА", "dinos", "common", 28, 32)
p.ellipse(14, 29, 12, 2.5, "lime", spark=True)             # гніздо
p.ellipse(14, 15, 12, 14, "mint", spark=True)
for (x, y) in [(7, 9), (18, 6), (20, 16), (8, 20), (14, 25)]:
    p.circle(x, y, 2.5, "green", spark=(x == 7))
p.dots([(5, 14), (14, 12), (21, 22)], "green", tone="light")
p.dots([(2, 28), (26, 28), (6, 27), (22, 27)], "olive")
p.rect(2, 30, 24, 2, "bark", shade=False)

p = pic("dino_hatch", "ВИЛУПОК", "dinos", "uncommon", 30, 32)
ground(p, 15, 30, 12, "lime")
p.poly([(3, 14), (27, 14), (25, 29), (5, 29)], "cream", spark=True)
p.poly([(3, 14), (7, 10), (11, 14), (15, 10), (19, 14), (23, 10), (27, 14)], "cream")
p.circle(15, 10, 8, "green", spark=True)
eyes(p, "green", [(12, 9), (18, 9)], r=1.5)
p.ink([(14, 13), (15, 13), (16, 13)])
p.poly([(4, 2), (7, 6), (2, 6)], "cream", spark=True)
p.ellipse(15, 22, 6, 3, "mint", spark=True)
cheeks(p, [(9, 12), (21, 12)], rx=1.4, family="pink")

p = pic("rex", "ТИРАНОЗАВР", "dinos", "epic", 32, 32)
ground(p, 16, 30, 13, "sand")
p.poly([(0, 24), (9, 21), (11, 17), (9, 30), (2, 30)], "olive")
p.ellipse(15, 21, 9, 8, "olive", spark=True)
p.ellipse(16, 25, 5, 3.5, "lime", spark=True)
p.ellipse(24, 10, 8, 7, "olive", spark=True)
p.poly([(24, 12), (32, 12), (32, 16), (26, 16)], "olive")
p.dots([(27, 13), (29, 13), (31, 13)], "cream")
p.ink_line(25, 14, 31, 14)
eyes(p, "olive", [(24, 8)], r=1.8, look=(0.5, 0))
p.rect(19, 17, 4, 2, "olive", shade=False)
p.rect(8, 28, 5, 3, "olive", shade=False)
p.rect(18, 28, 5, 3, "olive", shade=False)
for (x, y) in [(8, 11), (13, 8), (18, 7)]:
    p.poly([(x - 2, y + 5), (x, y), (x + 2, y + 5)], "orange", spark=(x == 13))
cheeks(p, [(21, 12)], rx=1.4, family="pink")

# ═══════════════════════════ ПРИВИДИ (серія) ═══════════════════════════

def ghost_body(p, coat="cream", top=13, look=(0, 0), shy=False, cx=14):
    p.ellipse(cx, top, 12, 12, coat, spark=True)
    p.rect(cx - 12, top, 24, 12, coat)
    b = top + 12
    p.poly([(cx - 12, b), (cx - 6, b - 4), (cx, b), (cx + 6, b - 4), (cx + 12, b), (cx + 12, b + 5), (cx - 12, b + 5)], coat)
    if shy:
        p.ink([(cx - 5, top - 1), (cx - 4, top), (cx - 3, top - 1), (cx + 3, top - 1), (cx + 4, top), (cx + 5, top - 1)])
    else:
        eyes(p, coat, [(cx - 4, top - 1), (cx + 4, top - 1)], r=2, look=look)
    p.ink([(cx - 1, top + 5), (cx, top + 6), (cx + 1, top + 5)])
    cheeks(p, [(cx - 7, top + 4), (cx + 7, top + 4)], rx=1.6, family="pink")


p = pic("ghost", "ПРИВИДИК", "ghosts", "common", 28, 30)
p.ellipse(14, 28, 9, 1.6, "ice")                           # сяйво під привидом
ghost_body(p)
p.circle(24, 4, 2.4, "yellow", spark=True)                 # вогники
p.circle(3, 8, 2.2, "yellow", spark=True)
p.circle(25, 24, 1.8, "cyan", spark=True)
p.dots([(2, 20)], "ice", tone="light")

p = pic("ghost_shy", "СОРОМ'ЯЗЛИВИЙ ПРИВИД", "ghosts", "common", 28, 30)
p.ellipse(14, 28, 9, 1.6, "ice")
ghost_body(p, shy=True)
p.ellipse(9, 16, 3, 2, "blush", spark=True)
p.ellipse(19, 16, 3, 2, "blush", spark=True)
p.circle(4, 4, 2, "yellow", spark=True)
p.circle(25, 6, 2, "yellow", spark=True)

p = pic("ghost_hat", "ПРИВИД У КАПЕЛЮСІ", "ghosts", "uncommon", 28, 32)
ghost_body(p, top=15)
p.rect(2, 8, 24, 3, "bark", r=1, spark=True)
p.rect(7, 0, 14, 9, "bark", r=2, spark=True)
p.rect(7, 6, 14, 2, "red", shade=False)
p.circle(9, 7, 1.4, "yellow", shade=False)                 # пір'їнка-значок

p = pic("ghost_pirate", "ПРИВИД-ПІРАТ", "ghosts", "uncommon", 28, 30)
p.ellipse(14, 28, 9, 1.6, "ice")
ghost_body(p, look=(0.4, 0))
p.poly([(0, 8), (14, 0), (28, 8), (27, 10), (1, 10)], "red", spark=True)
p.circle(18, 12, 3, "slate", spark=True)
p.ink_line(8, 8, 26, 10)
p.dots([(4, 4), (9, 3), (14, 2), (19, 3), (24, 4)], "red", tone="light")
p.circle(3, 15, 1.5, "yellow", shade=False)

p = pic("ghost_crown", "ПРИВИД-КОРОЛЬ", "ghosts", "rare", 28, 32)
ghost_body(p, top=15)
p.poly([(5, 8), (8, 1), (11, 6), (14, 0), (17, 6), (20, 1), (23, 8)], "yellow", spark=True)
p.rect(5, 7, 18, 3, "yellow")
p.dots([(8, 8), (14, 8), (20, 8)], "red")
p.dots([(11, 8), (17, 8)], "cyan")
p.ellipse(14, 30, 9, 1.6, "ice")
p.circle(14, 24, 2, "red", spark=True)                     # брошка

p = pic("ghost_wizard", "ПРИВИД-ЧАРІВНИК", "ghosts", "rare", 28, 32)
ghost_body(p, top=17)
p.poly([(2, 11), (14, 0), (26, 11)], "violet", spark=True)
p.rect(1, 10, 26, 3, "violet")
p.dots([(12, 5), (16, 8), (10, 8)], "yellow")
p.poly(star_points(4, 26, 3, 1.2), "yellow", spark=True)
p.dots([(25, 22), (26, 26)], "yellow", tone="light")
p.ellipse(14, 30, 9, 1.6, "ice")

p = pic("ghost_love", "ЗАКОХАНИЙ ПРИВИД", "ghosts", "uncommon", 28, 30)
p.ellipse(14, 28, 9, 1.6, "ice")
ghost_body(p)
p.circle(24, 26, 2, "yellow", spark=True)                  # свічка
p.circle(9, 12, 2.2, "red", shade=False)
p.circle(11, 12, 2.2, "red", shade=False)
p.poly([(7, 13), (13, 13), (10, 16)], "red", shade=False)
p.circle(17, 12, 2.2, "red", shade=False)
p.circle(19, 12, 2.2, "red", shade=False)
p.poly([(15, 13), (21, 13), (18, 16)], "red", shade=False)
p.circle(25, 5, 2.8, "red", spark=True)                    # сердечко збоку
p.circle(3, 4, 2, "red", spark=True)

p = pic("ghost_royal", "ПРИВИД-ІМПЕРАТОР", "ghosts", "epic", 32, 32)
ghost_body(p, cx=16, top=16)
p.poly([(0, 20), (5, 12), (6, 32), (0, 32)], "red", spark=True)
p.poly([(32, 20), (27, 12), (26, 32), (32, 32)], "red", spark=True)
p.dots([(2, 24), (3, 29), (29, 24), (30, 29)], "cream")
p.poly([(6, 9), (9, 1), (12, 7), (16, 0), (20, 7), (23, 1), (26, 9)], "yellow", spark=True)
p.rect(6, 8, 20, 3, "yellow")
p.dots([(9, 9), (16, 9), (23, 9)], "red")
p.dots([(12, 9), (20, 9)], "cyan")
p.circle(16, 26, 2.2, "cyan", spark=True)

# ═══════════════════════════ ЇЖА ═══════════════════════════

p = pic("apple", "ЯБЛУКО", "food", "common", 30, 30)
ground(p, 15, 28, 11, "sand")
p.circle(10, 17, 8.5, "red", spark=True)
p.circle(20, 17, 8.5, "red", spark=True)
p.ellipse(15, 20, 12, 8, "red")
p.rect(14, 3, 2, 7, "bark", spark=True)
p.ellipse(20, 5, 5, 2.5, "green", spark=True)
p.dots([(7, 12), (8, 11)], "red", tone="white")

p = pic("cherry", "ЧЕРЕШНІ", "food", "common", 30, 30)
ground(p, 15, 28, 11, "sand")
p.ink_line(9, 18, 15, 2)
p.ink_line(21, 18, 16, 2)
p.ellipse(16, 3, 4, 2, "green", spark=True)
p.circle(9, 21, 6.5, "red", spark=True)
p.circle(21, 21, 6.5, "red", spark=True)
p.dots([(6, 18), (18, 18)], "red", tone="white")
p.ellipse(9, 25, 3, 1.2, "raspberry", spark=True)
p.ellipse(21, 25, 3, 1.2, "raspberry", spark=True)

p = pic("strawberry", "ПОЛУНИЦЯ", "food", "common", 28, 32)
ground(p, 14, 30, 11, "sand")
p.poly([(2, 10), (26, 10), (19, 27), (14, 30), (9, 27)], "red", spark=True)
p.ellipse(14, 12, 12, 5, "red")
for (x, y) in [(9, 15), (15, 14), (20, 16), (11, 21), (17, 21), (14, 26)]:
    p.dots([(x, y)], "yellow")
p.poly([(4, 8), (14, 10), (24, 8), (19, 4), (14, 7), (9, 4)], "green", spark=True)
p.rect(13, 1, 2, 5, "green", shade=False)
p.circle(25, 26, 2.2, "cream", spark=True)                 # крапля вершків

p = pic("banana", "БАНАН", "food", "common", 32, 28)
ground(p, 16, 26, 13, "sand")
p.poly([(2, 4), (8, 10), (16, 18), (26, 20), (30, 16), (26, 22), (14, 22), (6, 16), (1, 8)], "yellow", spark=True)
p.poly([(6, 8), (12, 14), (20, 19), (26, 20), (24, 17), (14, 16), (8, 10)], "lemon", spark=True)
p.rect(1, 3, 3, 3, "bark", spark=True)
p.rect(28, 15, 3, 2, "bark", shade=False)
p.dots([(4, 7), (5, 8)], "yellow", tone="white")

p = pic("watermelon", "КАВУН", "food", "uncommon", 32, 24)
ground(p, 16, 22, 10, "sand")
p.poly([(0, 6), (32, 6), (16, 22)], "green", spark=True)
p.poly([(3, 7), (29, 7), (16, 19)], "lime", spark=True)
p.poly([(5, 8), (27, 8), (16, 17)], "red", spark=True)
p.dots([(10, 10), (16, 10), (22, 10), (13, 13), (19, 13), (16, 15)], "slate")
p.rect(0, 5, 32, 2, "green", tone="shadow", shade=False)

p = pic("donut", "ПОНЧИК", "food", "uncommon", 30, 30)
ground(p, 15, 28, 12, "sand")
p.ellipse(15, 15, 14, 12, "caramel", spark=True)
p.ellipse(15, 14, 13, 9, "pink", spark=True)
p.ellipse(15, 15, 4, 3.5, "caramel", tone="shadow", shade=False)
p.ellipse(15, 15, 3, 2.5, "sand", tone="shadow", shade=False)
for (x, y) in [(7, 10), (12, 8), (19, 8), (24, 11), (9, 18), (21, 19)]:
    p.dots([(x, y)], "cyan" if x % 2 else "yellow")
p.dots([(6, 8)], "pink", tone="white")

p = pic("icecream", "МОРОЗИВО", "food", "uncommon", 26, 32)
p.poly([(2, 16), (24, 16), (13, 32)], "caramel", spark=True)
for y in range(18, 30, 3):
    p.ink_line(5 + (y - 18) // 3 * 2, y, 21 - (y - 18) // 3 * 2, y)
p.circle(13, 9, 8, "pink", spark=True)
p.circle(7, 13, 4, "pink")
p.circle(19, 13, 4, "pink")
p.circle(13, 6, 4, "cream", spark=True)
p.circle(13, 2, 1.8, "red", spark=True)
p.dots([(8, 10), (17, 8), (12, 13)], "cyan")

p = pic("pizza", "ПІЦА", "food", "rare", 32, 30)
ground(p, 16, 28, 8, "sand")
p.poly([(0, 0), (32, 0), (16, 28)], "caramel", spark=True)
p.poly([(2, 3), (30, 3), (16, 26)], "orange", spark=True)
p.poly([(4, 4), (28, 4), (16, 23)], "yellow", spark=True)
for (x, y) in [(9, 8), (19, 7), (14, 13), (22, 13), (16, 19)]:
    p.circle(x, y, 2.4, "red", shade=False)
p.dots([(8, 7), (18, 6)], "red", tone="light")
p.dots([(7, 12), (24, 9), (11, 17), (19, 16)], "green")
p.rect(0, 0, 32, 2, "caramel", tone="shadow", shade=False)

p = pic("burger", "БУРГЕР", "food", "epic", 32, 30)
p.ellipse(16, 8, 15, 7, "caramel", spark=True)
p.dots([(8, 5), (14, 3), (20, 4), (25, 7), (11, 8)], "cream")
p.rect(1, 12, 30, 3, "green", spark=True)
p.rect(2, 15, 28, 3, "yellow", spark=True)
p.rect(2, 18, 28, 4, "bark", spark=True)
p.rect(3, 22, 26, 2, "red", shade=False)
p.ellipse(16, 26, 14, 4, "caramel", spark=True)
p.dots([(6, 13), (16, 13), (26, 13)], "green", tone="shadow")

p = pic("sushi", "СУШІ", "food", "uncommon", 30, 24)
ground(p, 15, 22, 13, "slate")
p.ellipse(15, 15, 13, 6, "cream", spark=True)
p.dots([(4, 13), (9, 17), (15, 12), (21, 17), (26, 13), (12, 19), (19, 19)], "cream", tone="shadow")
p.ellipse(15, 8, 12, 5, "red", spark=True)
p.line(5, 7, 26, 7, "red", tone="light")
p.line(7, 10, 24, 10, "red", tone="light")
p.rect(13, 3, 4, 16, "green", spark=True)
p.dots([(3, 20), (27, 20)], "lime")

p = pic("taco", "ТАКО", "food", "rare", 32, 26)
ground(p, 16, 24, 13, "sand")
p.ellipse(16, 14, 15, 9, "yellow", spark=True)
p.poly([(3, 12), (29, 12), (26, 4), (6, 4)], "green", spark=True)
p.rect(6, 8, 20, 3, "bark", spark=True)
p.dots([(8, 6), (16, 6), (24, 6)], "red")
p.dots([(11, 7), (19, 7)], "cream")
p.poly([(1, 14), (31, 14), (16, 23)], "yellow", tone="shadow", shade=False)
p.poly([(3, 14), (29, 14), (16, 21)], "yellow", shade=False)

p = pic("egg", "ЯЄЧНЯ", "food", "common", 30, 28)
p.rect(2, 24, 26, 3, "slate", r=1, spark=True)             # сковорідка
p.rect(0, 25, 4, 1, "bark", shade=False)
p.poly([(4, 6), (14, 2), (24, 4), (29, 12), (26, 22), (14, 25), (4, 22), (1, 14)], "cream", spark=True)
p.circle(15, 13, 6, "yellow", spark=True)
p.dots([(5, 8), (25, 20)], "cream", tone="white")
p.dots([(20, 9), (8, 18)], "green")

# ═══════════════════════════ СОЛОДОЩІ ═══════════════════════════

p = pic("candy", "ЦУКЕРКА", "sweets", "common", 32, 22)
ground(p, 16, 20, 12, "sand")
p.poly([(0, 4), (7, 8), (7, 12), (0, 16), (2, 10)], "raspberry", spark=True)
p.poly([(32, 4), (25, 8), (25, 12), (32, 16), (30, 10)], "raspberry", spark=True)
p.ellipse(16, 10, 10, 8, "pink", spark=True)
p.line(10, 4, 8, 16, "cyan", width=2)
p.line(16, 3, 14, 17, "cyan", width=2)
p.line(22, 4, 20, 16, "cyan", width=2)
p.dots([(9, 5), (15, 4)], "pink", tone="white")

p = pic("lollipop", "ЛЬОДЯНИК", "sweets", "common", 26, 32)
p.rect(12, 18, 2, 14, "cream", shade=False)
p.circle(13, 10, 10, "red", spark=True)
p.circle(13, 10, 7, "cream", spark=True)
p.circle(13, 10, 4.5, "red", spark=True)
p.circle(13, 10, 2, "cream", shade=False)
p.dots([(7, 5), (8, 4)], "red", tone="white")
p.poly([(9, 20), (17, 20), (13, 24)], "cyan", spark=True)
p.circle(13, 22, 1.4, "yellow", shade=False)

p = pic("cupcake", "КАПКЕЙК", "sweets", "uncommon", 28, 32)
ground(p, 14, 30, 11, "sand")
p.poly([(3, 16), (25, 16), (22, 29), (6, 29)], "caramel", spark=True)
for x in range(6, 24, 4):
    p.line(x, 17, x - 1, 28, "caramel", tone="shadow", shade=False)
p.circle(14, 10, 9, "pink", spark=True)
p.circle(8, 14, 4.5, "pink")
p.circle(20, 14, 4.5, "pink")
p.circle(14, 5, 4, "cream", spark=True)
p.circle(14, 2, 1.8, "red", shade=False)
p.dots([(9, 9), (18, 8), (12, 13), (17, 13)], "cyan")

p = pic("chocolate", "ШОКОЛАДКА", "sweets", "common", 30, 26)
ground(p, 15, 24, 13, "sand")
p.rect(1, 1, 28, 22, "bark", spark=True)
for y in (2, 9, 16):
    for x in (2, 9, 16, 23):
        p.rect(x, y, 6, 6, "bark", r=1, spark=(x == 2 and y == 2))
p.rect(16, 1, 13, 22, "silver", spark=True)
p.line(18, 3, 27, 21, "silver", tone="light")
p.rect(16, 1, 13, 3, "red", spark=True)

p = pic("cake", "ТОРТ", "sweets", "epic", 32, 32)
p.ellipse(16, 28, 15, 3.5, "silver", spark=True)
p.rect(3, 15, 26, 11, "peach", spark=True)
p.rect(3, 15, 26, 3, "pink", spark=True)
p.dots([(5, 18), (10, 18), (16, 18), (22, 18), (27, 18)], "pink", tone="shadow")
p.rect(8, 6, 16, 9, "peach", spark=True)
p.rect(8, 6, 16, 3, "pink")
p.dots([(9, 9), (14, 9), (19, 9), (23, 9)], "pink", tone="shadow")
p.rect(15, 0, 2, 6, "cyan", shade=False)
p.dots([(15, 0)], "yellow")
p.dots([(16, 1)], "yellow", tone="shadow")
for (x, y) in [(6, 22), (12, 20), (20, 20), (26, 22), (11, 12), (21, 12)]:
    p.circle(x, y, 1.8, "red", spark=(x == 6))
p.dots([(4, 16), (9, 7)], "pink", tone="white")

# ═══════════════════════════ МОРСЬКІ МЕШКАНЦІ ═══════════════════════════

p = pic("fish", "РИБКА", "sea", "common", 32, 24)
p.poly([(24, 11), (32, 3), (31, 19)], "orange", spark=True)
p.ellipse(14, 11, 12, 8, "orange", spark=True)
p.poly([(10, 4), (16, 0), (18, 6)], "yellow", spark=True)
p.poly([(9, 17), (15, 22), (17, 16)], "yellow")
p.ellipse(9, 11, 3, 4, "cream", spark=True)
eyes(p, "orange", [(6, 10)], r=1.6, look=(-0.4, 0))
p.ink([(3, 13), (4, 14)])
p.circle(3, 3, 1.6, "cyan", spark=True)                    # бульбашки
p.circle(6, 0, 1.2, "cyan", shade=False)
p.dots([(20, 8), (18, 13)], "orange", tone="light")

p = pic("crab", "КРАБИК", "sea", "common", 32, 28)
ground(p, 16, 26, 14, "sand")
p.ellipse(16, 15, 12, 8, "red", spark=True)
p.circle(4, 10, 3.5, "orange", spark=True)
p.circle(28, 10, 3.5, "orange", spark=True)
p.ink([(3, 8), (29, 8)])
p.ink_line(7, 12, 10, 14)
p.ink_line(25, 12, 22, 14)
for (x, y) in [(6, 20), (9, 23), (23, 23), (26, 20)]:
    p.rect(x, y, 3, 2, "orange", shade=False)
p.ink_line(11, 6, 12, 9)
p.ink_line(21, 6, 20, 9)
eyes(p, "red", [(12, 6), (20, 6)], r=1.6)
smile(p, 13, 19, 17, depth=1)
p.ellipse(16, 20, 5, 2, "peach", spark=True)

p = pic("octopus", "ВОСЬМИНІЖКА", "sea", "uncommon", 32, 30)
p.ellipse(16, 12, 12, 11, "violet", spark=True)
for i, x in enumerate([3, 8, 13, 19, 24, 29]):
    p.ellipse(x, 24 + (i % 2) * 2, 3, 5, "violet")
    p.dots([(x, 27 + (i % 2) * 2)], "pink")
eyes(p, "violet", [(11, 12), (21, 12)], r=2)
p.ink([(15, 18), (16, 19), (17, 18)])
cheeks(p, [(7, 16), (25, 16)], rx=1.8, ry=1.2, family="pink")
p.circle(3, 6, 2, "cyan", spark=True)
p.circle(29, 4, 1.6, "cyan", spark=True)
p.circle(16, 5, 2.2, "magenta", spark=True)                # плямка

p = pic("jellyfish", "МЕДУЗКА", "sea", "uncommon", 30, 30)
p.ellipse(15, 10, 13, 9, "cyan", spark=True)
p.ellipse(15, 12, 10, 5, "ice", spark=True)
for x in (4, 9, 15, 21, 26):
    p.line(x, 18, x + (1 if x < 15 else -1), 29, "cyan", tone="light", shade=False)
    p.dots([(x, 24), (x + (1 if x < 15 else -1), 29)], "magenta")
eyes(p, "cyan", [(11, 10), (19, 10)], r=1.6)
p.ink([(14, 14), (15, 15), (16, 14)])
cheeks(p, [(7, 13), (23, 13)], family="pink")
p.circle(2, 24, 1.6, "periwinkle", spark=True)
p.circle(28, 22, 2, "periwinkle", spark=True)

p = pic("whale", "КИТ", "sea", "rare", 32, 26)
p.ellipse(16, 24, 14, 1.8, "cyan")                         # хвиля
p.ellipse(14, 14, 13, 8, "blue", spark=True)
p.poly([(24, 12), (32, 6), (32, 18), (26, 16)], "blue", spark=True)
p.ellipse(14, 17, 9, 4, "ice", spark=True)
eyes(p, "blue", [(6, 12)], r=1.6, look=(-0.4, 0))
p.ink([(3, 16), (4, 17), (5, 17)])
p.line(12, 5, 12, 1, "cyan", tone="light", shade=False)
p.line(12, 4, 9, 1, "cyan", tone="light", shade=False)
p.line(12, 4, 15, 1, "cyan", tone="light", shade=False)
p.dots([(9, 0), (15, 0), (12, 0)], "cyan")
cheeks(p, [(8, 15)], family="pink")

p = pic("turtle", "ЧЕРЕПАШКА", "sea", "uncommon", 32, 26)
ground(p, 14, 24, 12, "sand")
p.ellipse(14, 12, 11, 8, "green", spark=True)
for (x, y) in [(14, 8), (9, 13), (19, 13), (14, 16)]:
    p.ellipse(x, y, 3.5, 2.5, "olive", spark=(x == 14 and y == 8))
p.ellipse(27, 14, 5, 4, "lime", spark=True)
eyes(p, "lime", [(28, 13)], r=1.4)
p.ellipse(5, 20, 3.5, 2, "lime")
p.ellipse(22, 20, 3.5, 2, "lime")
p.ellipse(1, 12, 2, 1.5, "lime")
smile(p, 27, 30, 16, depth=1)
cheeks(p, [(30, 16)], family="pink")

p = pic("seahorse", "МОРСЬКИЙ КОНИК", "sea", "rare", 24, 34)
p.ellipse(12, 8, 7, 6, "orange", spark=True)
p.poly([(2, 7), (7, 5), (7, 9)], "orange")
p.poly([(10, 12), (18, 14), (16, 22), (8, 24)], "orange")
p.ellipse(11, 24, 6, 6, "orange", spark=True)
p.poly([(5, 26), (10, 30), (12, 34), (4, 33)], "orange")
p.poly([(15, 2), (21, 4), (19, 8), (21, 12), (17, 12)], "yellow", spark=True)
p.poly([(17, 16), (22, 14), (21, 22)], "yellow", spark=True)
eyes(p, "orange", [(12, 7)], r=1.5)
p.dots([(11, 15), (13, 19), (11, 23), (9, 27)], "cream")
cheeks(p, [(14, 10)], rx=1.4, family="pink")
p.circle(21, 28, 1.8, "cyan", spark=True)
p.circle(2, 18, 1.5, "cyan", spark=True)

p = pic("shell", "МУШЛЯ", "sea", "common", 30, 28)
ground(p, 15, 26, 13, "sand")
p.poly([(15, 24), (1, 12), (4, 4), (15, 0), (26, 4), (29, 12)], "peach", spark=True)
for (x0, y0) in [(4, 4), (9, 1), (15, 0), (21, 1), (26, 4)]:
    p.ink_line(15, 22, x0, y0)
p.ellipse(15, 10, 6, 3, "blush", spark=True)
p.ellipse(15, 23, 5, 2, "caramel", spark=True)
p.circle(26, 22, 2, "cyan", spark=True)

p = pic("axolotl", "АКСОЛОТЛЬ", "sea", "epic", 32, 28)
p.ellipse(16, 26, 13, 1.8, "cyan")
p.poly([(0, 14), (8, 12), (10, 18), (2, 22)], "pink")
p.ellipse(15, 15, 10, 7, "pink", spark=True)
p.circle(23, 12, 8, "pink", spark=True)
for (x, y) in [(17, 3), (20, 2), (28, 3), (31, 6), (16, 21), (30, 20)]:
    p.line(x, y, x + (1 if x > 23 else -1), y + 4, "magenta", width=2, shade=False)
    p.dots([(x, y)], "magenta", tone="light")
eyes(p, "pink", [(21, 11), (27, 11)], r=1.5)
smile(p, 22, 27, 16, depth=1)
cheeks(p, [(19, 15), (29, 15)], rx=1.8, ry=1.2, family="blush")
p.rect(8, 20, 4, 3, "pink", shade=False)
p.rect(17, 20, 4, 3, "pink", shade=False)
p.ellipse(11, 12, 3, 2, "peach", spark=True)               # плямка

# ═══════════════════════════ КОСМОС ═══════════════════════════

p = pic("rocket", "РАКЕТА", "space", "common", 26, 32)
p.poly([(5, 22), (21, 22), (18, 30), (8, 30)], "orange", spark=True)
p.poly([(8, 28), (18, 28), (13, 32)], "yellow", spark=True)
p.poly([(3, 28), (9, 16), (9, 26)], "red", spark=True)
p.poly([(23, 28), (17, 16), (17, 26)], "red", spark=True)
p.rect(8, 8, 10, 16, "cream", r=2, spark=True)
p.poly([(8, 9), (13, 0), (18, 9)], "red")
p.circle(13, 14, 3, "cyan", spark=True)
p.ink_ellipse(13, 14, 3.8, 3.8)
p.circle(13, 14, 2.8, "cyan", spark=True)
p.rect(9, 20, 8, 2, "red", shade=False)
p.dots([(1, 4), (24, 6), (2, 20)], "cream")

p = pic("planet", "ПЛАНЕТА", "space", "common", 32, 28)
p.poly([(0, 15), (32, 9), (32, 13), (0, 19)], "peach", spark=True)
p.circle(16, 14, 10, "teal", spark=True)
p.ellipse(11, 10, 3.5, 2.5, "green", spark=True)
p.ellipse(20, 16, 4, 3, "green", spark=True)
p.poly([(0, 16), (11, 14), (11, 17), (0, 20)], "peach", spark=True)
p.poly([(21, 13), (32, 10), (32, 14), (21, 16)], "peach")
p.circle(28, 3, 2, "yellow", spark=True)                   # місяць
p.dots([(2, 3), (29, 25), (4, 25)], "cream")

p = pic("star", "ЗІРОЧКА", "space", "common", 30, 30)
p.poly(star_points(15, 15.5, 15, 6.5), "yellow", spark=True)
eyes(p, "yellow", [(12, 14), (18, 14)], r=1.5)
smile(p, 13, 17, 19, depth=1)
cheeks(p, [(9, 17), (21, 17)], family="orange")
p.circle(4, 4, 3, "cyan", spark=True)
p.circle(26, 26, 3, "cyan", spark=True)
p.circle(27, 3, 1.8, "cream", spark=True)
p.circle(3, 27, 1.8, "cream", spark=True)

p = pic("moon", "МІСЯЦЬ", "space", "common", 28, 30)
p.circle(13, 15, 13, "lemon", spark=True)
p.circle(21, 12, 11, "periwinkle", spark=True)             # нічне небо в серпі
p.ellipse(8, 10, 2, 1.5, "yellow", spark=True)
p.ellipse(6, 18, 2.5, 2, "yellow", spark=True)
p.ellipse(11, 25, 2, 1.5, "yellow", shade=False)
eyes(p, "lemon", [(7, 14)], r=1.4)
smile(p, 5, 9, 20, depth=1)
p.poly(star_points(23, 4, 2.5, 1), "cream", shade=False)
p.dots([(26, 24), (24, 28), (27, 14)], "cream")
cheeks(p, [(4, 22)], family="pink")

p = pic("comet", "КОМЕТА", "space", "uncommon", 32, 20)
p.poly([(0, 9), (2, 7), (24, 3), (24, 17), (2, 12)], "slate")
p.poly([(4, 9), (24, 5), (24, 15), (4, 11)], "blue")
p.poly([(9, 9.5), (24, 7), (24, 13), (9, 10.5)], "yellow")
p.circle(24, 10, 6.5, "yellow", spark=True)
p.circle(25, 10, 4, "lemon", spark=True)
p.dots([(3, 5), (6, 15), (1, 14)], "slate", tone="white")

p = pic("ufo", "ЛЕТЮЧА ТАРІЛКА", "space", "uncommon", 32, 24)
p.ellipse(16, 8, 8, 6, "cyan", spark=True)
p.circle(16, 9, 3.5, "green", spark=True)
eyes(p, "green", [(15, 8), (17, 8)], r=1)
p.ellipse(16, 13, 15, 4, "silver", spark=True)
p.ellipse(16, 15, 11, 3, "slate", spark=True)
p.dots([(6, 15), (11, 16), (16, 16), (21, 16), (26, 15)], "yellow")
p.poly([(9, 18), (23, 18), (26, 24), (6, 24)], "yellow", tone="light", shade=False)
p.dots([(2, 3), (29, 2), (3, 22)], "cream")

p = pic("satellite", "СУПУТНИК", "space", "rare", 32, 26)
p.rect(0, 8, 9, 10, "blue", spark=True)
p.rect(23, 8, 9, 10, "blue", spark=True)
for x in (2, 5, 25, 28):
    p.line(x, 9, x, 17, "blue", tone="shadow", shade=False)
p.rect(9, 12, 3, 2, "silver", shade=False)
p.rect(20, 12, 3, 2, "silver", shade=False)
p.rect(12, 7, 8, 12, "silver", r=1, spark=True)
p.circle(16, 13, 2.5, "cyan", spark=True)
p.line(16, 7, 16, 2, "silver", tone="shadow", shade=False)
p.circle(16, 2, 1.8, "red", shade=False)
p.poly([(12, 19), (20, 19), (18, 24), (14, 24)], "slate", spark=True)
p.dots([(2, 3), (30, 24), (29, 2)], "cream")

p = pic("astronaut", "КОСМОНАВТ", "space", "rare", 28, 32)
p.circle(14, 9, 8, "cream", spark=True)
p.circle(14, 10, 5.5, "blue", tone="shadow", shade=False)
p.circle(14, 10, 4.5, "ice", spark=True)
p.rect(6, 17, 16, 12, "cream", r=2, spark=True)
p.rect(2, 18, 4, 8, "cream")
p.rect(22, 18, 4, 8, "cream")
p.rect(10, 20, 8, 5, "slate", spark=True)
p.dots([(11, 21), (14, 21)], "red")
p.dots([(16, 21)], "red", tone="light")
p.rect(7, 29, 5, 3, "slate", shade=False)
p.rect(16, 29, 5, 3, "slate", shade=False)
p.rect(2, 6, 3, 6, "orange", spark=True)
p.rect(23, 6, 3, 6, "orange", spark=True)
p.dots([(1, 1), (26, 2), (1, 30)], "cream", tone="white")

p = pic("galaxy", "ГАЛАКТИКА", "space", "legendary", 40, 34)
for i, (rx, ry, fam) in enumerate([(19, 14, "violet"), (15, 10, "periwinkle"), (10, 7, "magenta"), (6, 4, "pink")]):
    p.ellipse(20, 17, rx, ry, fam, spark=(i == 0))
p.circle(20, 17, 3, "cream", spark=True)
for (x, y) in [(6, 10), (9, 8), (13, 6), (27, 28), (31, 26), (34, 23)]:
    p.circle(x, y, 2.2, "periwinkle", tone="light", shade=False)
for (x, y) in [(3, 20), (36, 12), (10, 27), (30, 6)]:
    p.dots([(x, y), (x + 1, y)], "cream")
for (x, y) in [(2, 3), (37, 31), (38, 2), (1, 31), (20, 1)]:
    p.dots([(x, y)], "cream")
p.dots([(14, 12), (26, 22), (17, 22), (23, 12)], "pink", tone="white")
p.poly(star_points(5, 29, 3, 1.2), "cyan", shade=False)
p.poly(star_points(35, 5, 3, 1.2), "cyan", shade=False)

p = pic("blackhole", "ЧОРНА ДІРА", "space", "cosmic", 40, 40)
p.ellipse(20, 20, 19, 12, "orange", spark=True)
p.ellipse(20, 20, 16, 9, "yellow", spark=True)
p.ellipse(20, 20, 13, 7, "yellow", tone="light", shade=False)
p.circle(20, 20, 8, "violet", spark=True)
p.circle(20, 20, 6.5, "slate", tone="shadow", shade=False)
p.ink_ellipse(20, 20, 5.5, 5.5)
p.ellipse(20, 20, 18, 3, "orange", tone="light", shade=False)
p.poly([(2, 20), (20, 17), (20, 23)], "yellow", shade=False)
p.poly([(38, 20), (20, 17), (20, 23)], "yellow", shade=False)
p.circle(20, 20, 8, "violet", spark=True)
p.circle(20, 20, 6.5, "slate", tone="shadow", shade=False)
p.ink_ellipse(20, 20, 5.5, 5.5)
p.poly([(20, 0), (24, 8), (20, 10), (16, 8)], "cyan", spark=True)
p.poly([(20, 40), (24, 32), (20, 30), (16, 32)], "cyan", spark=True)
for (x, y) in [(3, 4), (36, 5), (5, 35), (35, 36), (9, 12), (31, 29)]:
    p.dots([(x, y)], "cyan", tone="white")
p.dots([(6, 20), (34, 20), (20, 4), (20, 36)], "cyan", tone="white")
p.poly(star_points(34, 8, 3, 1.2), "slate", spark=True)
p.poly(star_points(6, 32, 3, 1.2), "slate", spark=True)

p = pic("alien", "ІНОПЛАНЕТЯНИН", "space", "uncommon", 28, 32)
ground(p, 14, 30, 11, "violet")
p.ellipse(14, 11, 11, 10, "green", spark=True)
p.rect(9, 20, 10, 8, "green", r=2, spark=True)
p.rect(4, 21, 5, 5, "green")
p.rect(19, 21, 5, 5, "green")
eyes(p, "green", [(10, 11), (18, 11)], r=2.6, pupil=False)
p.ink([(10, 12), (18, 12), (9, 11), (19, 11)])
p.ink([(13, 16), (14, 17), (15, 16)])
p.ink_line(11, 3, 9, 0)
p.ink_line(17, 3, 19, 0)
p.circle(9, 0, 1.6, "lime", spark=True)
p.circle(19, 0, 1.6, "lime", spark=True)
p.rect(9, 28, 4, 2, "slate", spark=True)
p.rect(15, 28, 4, 2, "slate", spark=True)
cheeks(p, [(7, 15), (21, 15)], family="lime")

p = pic("telescope", "ТЕЛЕСКОП", "space", "common", 32, 30)
ground(p, 16, 28, 12, "sand")
p.poly([(4, 10), (26, 2), (28, 8), (6, 16)], "blue", spark=True)
p.rect(24, 1, 5, 8, "silver", r=1, spark=True)
p.rect(2, 10, 5, 7, "silver", r=1, spark=True)
p.rect(14, 14, 4, 6, "slate", spark=True)
p.line(16, 18, 6, 27, "slate", width=2, shade=False)
p.line(16, 18, 26, 27, "slate", width=2, shade=False)
p.line(16, 18, 16, 26, "slate", width=2, shade=False)
p.circle(30, 22, 1.6, "yellow", shade=False)
p.dots([(30, 3), (1, 24)], "cream")

# ═══════════════════════════ МОНСТРИКИ ═══════════════════════════

p = pic("slime", "СЛИМАЧОК", "monsters", "common", 32, 26)
ground(p, 16, 24, 14, "teal")
p.ellipse(16, 14, 15, 9, "lime", spark=True)
p.ellipse(10, 8, 6, 4, "lime")
p.ellipse(22, 21, 4, 2, "lime")
eyes(p, "lime", [(11, 12), (21, 12)], r=2)
p.ink([(14, 17), (15, 18), (16, 18), (17, 17)])
p.dots([(15, 18), (16, 18)], "lime", tone="white")
p.circle(26, 6, 2.2, "green", spark=True)                  # бульбашка слизу
p.circle(6, 4, 1.8, "green", spark=True)
cheeks(p, [(7, 16), (25, 16)], family="pink")

p = pic("cyclops", "ЦИКЛОПИК", "monsters", "common", 30, 32)
ground(p, 15, 30, 12, "sand")
p.ellipse(15, 16, 12, 12, "violet", spark=True)
p.circle(15, 13, 5.5, "violet", tone="white", shade=False)
p.circle(15, 13, 3, "cyan", spark=True)
p.ink([(15, 13), (15, 14), (14, 14)])
p.ink_line(7, 6, 10, 2)
p.ink_line(23, 6, 20, 2)
p.circle(10, 1, 1.6, "pink", spark=True)
p.circle(20, 1, 1.6, "pink", spark=True)
p.ink([(12, 22), (13, 23), (14, 23), (15, 23), (16, 23), (17, 23), (18, 22)])
p.dots([(13, 24), (17, 24)], "violet", tone="white")
p.rect(9, 27, 4, 3, "violet", shade=False)
p.rect(17, 27, 4, 3, "violet", shade=False)

p = pic("horned", "РОГАТИК", "monsters", "uncommon", 30, 32)
ground(p, 15, 30, 12, "slate")
p.poly([(2, 12), (6, 0), (11, 10)], "yellow", spark=True)
p.poly([(28, 12), (24, 0), (19, 10)], "yellow", spark=True)
p.ellipse(15, 18, 13, 11, "red", spark=True)
eyes(p, "red", [(10, 16), (20, 16)], r=2)
p.ink([(10, 24), (11, 25), (19, 25), (20, 24)])
p.ink_line(11, 25, 19, 25)
p.dots([(12, 26), (18, 26)], "red", tone="white")
p.rect(8, 28, 5, 2, "red", shade=False)
p.rect(17, 28, 5, 2, "red", shade=False)
cheeks(p, [(6, 20), (24, 20)], family="pink")
p.circle(15, 11, 2, "orange", spark=True)                  # ґудзик на лобі

p = pic("threeeyes", "ТРИОКИЙ", "monsters", "uncommon", 32, 28)
ground(p, 16, 26, 14, "sand")
p.ellipse(16, 14, 15, 11, "teal", spark=True)
eyes(p, "teal", [(8, 12), (16, 9), (24, 12)], r=2.2)
p.ink([(12, 19), (13, 20), (14, 20), (18, 20), (19, 20), (20, 19)])
p.ink_line(14, 20, 18, 20)
p.dots([(4, 4), (28, 4), (2, 22), (30, 22)], "teal", tone="light")
p.rect(9, 24, 5, 2, "teal", shade=False)
p.rect(18, 24, 5, 2, "teal", shade=False)
cheeks(p, [(6, 17), (26, 17)], family="pink")
p.circle(4, 5, 2, "yellow", spark=True)                    # антенка
p.ink_line(5, 7, 7, 10)

p = pic("fluffy", "ПУХНАСТИК", "monsters", "rare", 32, 30)
ground(p, 16, 28, 13, "sand")
for (x, y) in [(4, 12), (9, 5), (16, 3), (23, 5), (28, 12), (26, 21), (16, 25), (6, 21)]:
    p.circle(x, y, 5, "pink", spark=(x == 9))
p.ellipse(16, 14, 11, 9, "pink")
eyes(p, "pink", [(11, 12), (21, 12)], r=2)
p.ellipse(16, 17, 2, 1.4, "raspberry", shade=False)
smile(p, 13, 19, 20, depth=1)
p.ink_line(12, 2, 10, 0)
p.ink_line(20, 2, 22, 0)
p.circle(10, 0, 1.6, "yellow", spark=True)
p.circle(22, 0, 1.6, "yellow", spark=True)
p.rect(9, 26, 5, 2, "violet", spark=True)
p.rect(18, 26, 5, 2, "violet", spark=True)

p = pic("bigmouth", "ЗУБАСТИК", "monsters", "rare", 32, 28)
ground(p, 16, 26, 14, "sand")
p.ellipse(16, 13, 15, 12, "blue", spark=True)
p.ellipse(16, 16, 11, 6, "blue", tone="shadow", shade=False)
p.ellipse(16, 18, 9, 4, "raspberry", spark=True)
for x in (7, 11, 15, 19, 23):
    p.poly([(x, 11), (x + 3, 11), (x + 1.5, 15)], "cream", spark=(x == 7))
eyes(p, "blue", [(10, 6), (22, 6)], r=2)
p.ink_line(10, 1, 12, 3)
p.ink_line(22, 1, 20, 3)
p.rect(8, 24, 5, 2, "blue", shade=False)
p.rect(19, 24, 5, 2, "blue", shade=False)
cheeks(p, [(5, 10), (27, 10)], family="pink")

p = pic("kraken", "КРАКЕНЯ", "monsters", "legendary", 40, 36)
p.ellipse(20, 34, 18, 1.8, "cyan")
p.ellipse(20, 12, 14, 11, "violet", spark=True)
for i, (x, y) in enumerate([(3, 22), (9, 26), (15, 29), (25, 29), (31, 26), (37, 22)]):
    p.ellipse(x, y, 3, 6, "violet")
    p.circle(x, y + 3, 1.6, "pink", spark=(i == 0))
p.poly([(0, 28), (3, 16), (6, 30)], "violet")
p.poly([(40, 28), (37, 16), (34, 30)], "violet")
eyes(p, "violet", [(14, 11), (26, 11)], r=2.6)
p.ink([(17, 18), (18, 19), (22, 19), (23, 18)])
p.ink_line(18, 19, 22, 19)
p.dots([(19, 20), (21, 20)], "violet", tone="white")
p.poly([(12, 2), (16, 6), (10, 8)], "magenta", spark=True)
p.poly([(20, 0), (24, 5), (16, 5)], "magenta", spark=True)
p.poly([(28, 2), (24, 6), (30, 8)], "magenta", spark=True)
p.circle(4, 8, 2.4, "cyan", spark=True)
p.circle(36, 8, 2.4, "cyan", spark=True)
p.circle(20, 33, 3, "cyan", spark=True)
cheeks(p, [(9, 15), (31, 15)], rx=1.8, ry=1.2, family="pink")

# ═══════════════════════════ ПОГОДА ═══════════════════════════

p = pic("sun", "СОНЕЧКО", "weather", "common", 32, 32)
for i in range(8):
    a = i * _m.pi / 4
    x, y = 16 + 13 * _m.cos(a), 16 + 13 * _m.sin(a)
    p.poly([(16 + 9 * _m.cos(a - 0.3), 16 + 9 * _m.sin(a - 0.3)), (x + 3 * _m.cos(a), y + 3 * _m.sin(a)),
            (16 + 9 * _m.cos(a + 0.3), 16 + 9 * _m.sin(a + 0.3))], "orange", spark=(i == 0))
p.circle(16, 16, 9.5, "yellow", spark=True)
eyes(p, "yellow", [(13, 15), (19, 15)], r=1.5)
smile(p, 13, 19, 19, depth=1)
cheeks(p, [(10, 18), (22, 18)], family="pink")
p.circle(3, 27, 2.5, "cream", spark=True)                  # хмаринка
p.circle(6, 26, 2.5, "cream", spark=True)
p.circle(28, 3, 2, "cream", spark=True)

p = pic("cloud", "ХМАРКА", "weather", "common", 32, 24)
p.circle(9, 12, 6, "cream", spark=True)
p.circle(16, 8, 7.5, "cream", spark=True)
p.circle(24, 12, 6, "cream", spark=True)
p.rect(4, 12, 24, 6, "cream")
eyes(p, "cream", [(13, 11), (19, 11)], r=1.5)
smile(p, 14, 18, 15, depth=1)
cheeks(p, [(9, 14), (23, 14)], family="pink")
p.circle(3, 4, 2.4, "yellow", spark=True)                  # сонячний зайчик
p.ellipse(16, 21, 10, 1.6, "ice")
p.dots([(30, 4), (2, 20)], "cyan")

p = pic("raincloud", "ДОЩОВА ХМАРКА", "weather", "uncommon", 32, 28)
p.circle(9, 9, 6, "slate", spark=True)
p.circle(16, 6, 7, "slate", spark=True)
p.circle(24, 9, 6, "slate", spark=True)
p.rect(4, 9, 24, 6, "slate")
eyes(p, "slate", [(13, 8), (19, 8)], r=1.4)
p.ink([(14, 12), (15, 13), (16, 13), (17, 13), (18, 12)])
for (x, y) in [(7, 18), (13, 21), (19, 18), (25, 21), (10, 25), (22, 25)]:
    p.poly([(x, y - 2), (x + 2, y + 1), (x + 1, y + 3), (x - 1, y + 1)], "blue", spark=(x == 7))
p.circle(29, 3, 2, "yellow", spark=True)
p.ellipse(16, 27, 10, 1.4, "cyan", spark=True)             # калюжа

p = pic("rainbow", "ВЕСЕЛКА", "weather", "rare", 32, 22)
for r, fam, tone in [(16, "red", None), (13.5, "orange", None), (11, "yellow", None), (8.5, "green", None), (6, "blue", None), (3.5, "blue", 18)]:
    p.ellipse(16, 17, r, r, fam, tone=tone, shade=False)
p.circle(16, 17, 1.5, "cream", shade=False)
p.circle(5, 16, 4, "cream", spark=True)
p.circle(9, 14, 4, "cream", spark=True)
p.circle(27, 16, 4, "cream", spark=True)
p.circle(23, 14, 4, "cream", spark=True)
p.rect(1, 16, 12, 6, "cream")
p.rect(19, 16, 12, 6, "cream")
p.rect(0, 15, 32, 3, "red", tone="light", shade=False)     # блиск на дузі — ні, лише хмари
p.rect(1, 16, 12, 6, "cream", spark=True)
p.rect(19, 16, 12, 6, "cream", spark=True)
p.dots([(2, 3), (30, 3)], "cream")

p = pic("snowflake", "СНІЖИНКА", "weather", "uncommon", 30, 30)
for i in range(6):
    a = i * _m.pi / 3
    x1, y1 = 15 + 13 * _m.cos(a), 15 + 13 * _m.sin(a)
    p.line(15, 15, int(round(x1)), int(round(y1)), "ice", width=2)
    bx, by = 15 + 8 * _m.cos(a), 15 + 8 * _m.sin(a)
    for da in (-0.6, 0.6):
        p.line(int(round(bx)), int(round(by)), int(round(bx + 4 * _m.cos(a + da))), int(round(by + 4 * _m.sin(a + da))), "cyan", width=1, shade=False)
    p.circle(int(round(x1)), int(round(y1)), 1.6, "cyan", spark=True)
p.circle(15, 15, 3.5, "ice", spark=True)
p.circle(15, 15, 1.5, "cream", shade=False)
p.circle(3, 3, 1.6, "periwinkle", spark=True)
p.circle(27, 27, 1.6, "periwinkle", spark=True)
p.circle(27, 3, 1.2, "periwinkle", shade=False)

p = pic("lightning", "БЛИСКАВКА", "weather", "common", 26, 32)
p.circle(6, 4, 4, "slate", spark=True)                     # хмарка
p.circle(11, 2, 4.5, "slate", spark=True)
p.circle(16, 4, 4, "slate", spark=True)
p.rect(3, 4, 16, 3, "slate")
p.poly([(15, 6), (5, 20), (12, 20), (8, 32), (23, 15), (15, 15), (20, 6)], "yellow", spark=True)
p.poly([(14, 9), (9, 17), (13, 17), (11, 26), (19, 16), (14, 16), (17, 9)], "lemon", shade=False)
p.dots([(2, 12), (25, 28), (24, 6)], "cyan")
p.dots([(4, 26), (23, 10)], "cyan", tone="light")

p = pic("tornado", "ТОРНАДО", "weather", "rare", 32, 32)
ground(p, 16, 30, 13, "bark")
for i, (y, rx) in enumerate([(4, 15), (10, 12), (16, 9), (22, 6), (26, 4), (29, 2)]):
    p.ellipse(16 + (i % 2) * 2 - 1, y, rx, 3.2, "slate" if i % 2 == 0 else "silver", spark=(i == 0))
p.dots([(8, 4), (22, 10), (12, 16), (20, 22)], "slate", tone="light")
p.circle(3, 14, 2, "cream", spark=True)
p.rect(27, 20, 3, 2, "bark", shade=False)
p.circle(29, 6, 2, "green", spark=True)                    # кущик, що летить

# ═══════════════════════════ РОСЛИНИ ═══════════════════════════

p = pic("tree", "ДЕРЕВЦЕ", "plants", "common", 28, 32)
ground(p, 14, 30, 11, "lime")
p.rect(12, 20, 4, 11, "bark", spark=True)
p.line(13, 24, 9, 21, "bark", width=2, shade=False)
p.circle(14, 11, 11, "green", spark=True)
p.circle(6, 14, 6, "green")
p.circle(22, 14, 6, "green")
p.circle(10, 8, 4, "lime", spark=True)
p.dots([(18, 6), (20, 12), (8, 18), (17, 16)], "lime")
p.circle(12, 4, 1.6, "red", spark=True)
p.circle(22, 8, 1.6, "red", spark=True)

p = pic("flower", "КВІТКА", "plants", "common", 28, 32)
ground(p, 14, 30, 8, "lime")
for (x, y) in [(14, 4), (22, 9), (22, 17), (14, 22), (6, 17), (6, 9)]:
    p.circle(x, y, 5, "pink", spark=(x == 14 and y == 4))
p.circle(14, 13, 5, "yellow", spark=True)
p.dots([(12, 12), (16, 12), (14, 15)], "orange")
p.rect(13, 22, 2, 9, "green", shade=False)
p.ellipse(8, 27, 4, 2, "green", spark=True)
p.ellipse(20, 29, 4, 2, "green", spark=True)

p = pic("mushroom", "ГРИБОЧОК", "plants", "common", 30, 30)
ground(p, 15, 28, 12, "lime")
p.ellipse(15, 11, 14, 10, "red", spark=True)
p.rect(1, 11, 28, 4, "red")
for (x, y) in [(6, 8), (13, 5), (21, 7), (10, 13), (24, 13), (17, 12)]:
    p.circle(x, y, 2, "cream", spark=(x == 13))
p.rect(10, 15, 10, 13, "cream", r=2, spark=True)
p.dots([(12, 20), (15, 24)], "sand")
eyes(p, "cream", [(13, 19), (17, 19)], r=1.2)
smile(p, 13, 17, 23, depth=1)
cheeks(p, [(11, 22), (19, 22)], rx=1.2, family="pink")

p = pic("sprout", "ПАРОСТОК", "plants", "common", 26, 30)
p.rect(12, 12, 2, 12, "green", shade=False)
p.ellipse(7, 9, 6, 4, "green", spark=True)
p.ellipse(19, 9, 6, 4, "green", spark=True)
p.ellipse(13, 3, 3, 2.5, "lime", spark=True)
p.line(8, 9, 12, 12, "green", tone="shadow", shade=False)
p.line(18, 9, 14, 12, "green", tone="shadow", shade=False)
p.rect(4, 22, 18, 8, "caramel", r=2, spark=True)
p.rect(3, 22, 20, 3, "orange", spark=True)
p.ellipse(13, 24, 6, 1.5, "bark", shade=False)
p.circle(23, 5, 1.6, "cyan", shade=False)                  # крапля
p.circle(2, 14, 1.4, "cyan", shade=False)

p = pic("clover", "КОНЮШИНА", "plants", "uncommon", 28, 32)
ground(p, 14, 30, 10, "sand")
for (x, y) in [(8, 8), (20, 8), (8, 18), (20, 18)]:
    p.circle(x, y, 6, "green", spark=(x == 8 and y == 8))
    p.circle(x, y, 3, "lime", spark=(x == 8 and y == 8))
p.ink([(14, 13), (14, 12), (14, 14), (13, 13), (15, 13)])
p.line(14, 18, 12, 29, "green", width=2, shade=False)
p.circle(25, 4, 1.8, "yellow", spark=True)
p.circle(3, 26, 1.8, "yellow", spark=True)

p = pic("sunflower", "СОНЯШНИК", "plants", "uncommon", 30, 32)
ground(p, 15, 30, 8, "lime")
for i in range(12):
    a = i * _m.pi / 6
    p.ellipse(15 + 10 * _m.cos(a), 12 + 10 * _m.sin(a), 4.2, 2.6, "yellow", spark=(i == 0))
p.circle(15, 12, 6.5, "bark", spark=True)
for (x, y) in [(13, 10), (17, 10), (12, 14), (16, 14), (15, 12)]:
    p.dots([(x, y)], "caramel")
p.rect(14, 22, 2, 9, "green", shade=False)
p.ellipse(9, 27, 5, 2.5, "green", spark=True)
p.ellipse(21, 29, 5, 2.5, "green", spark=True)

p = pic("tulip", "ТЮЛЬПАН", "plants", "common", 26, 32)
ground(p, 13, 30, 9, "sand")
p.poly([(3, 4), (8, 0), (13, 5), (18, 0), (23, 4), (21, 14), (13, 18), (5, 14)], "pink", spark=True)
p.poly([(9, 3), (13, 8), (17, 3), (15, 13), (11, 13)], "raspberry", spark=True)
p.rect(12, 18, 2, 12, "green", shade=False)
p.poly([(2, 20), (11, 24), (11, 30), (2, 26)], "green", spark=True)
p.poly([(24, 22), (15, 26), (15, 30), (24, 28)], "green", spark=True)
p.circle(2, 4, 1.6, "yellow", spark=True)

p = pic("bonsai", "БОНСАЙ", "plants", "rare", 32, 30)
ground(p, 16, 29, 14, "sand")
p.rect(4, 22, 24, 7, "caramel", r=2, spark=True)
p.rect(3, 22, 26, 3, "caramel", tone="light", shade=False)
p.rect(14, 12, 3, 10, "bark", spark=True)
p.line(15, 14, 8, 9, "bark", width=2, shade=False)
p.line(15, 14, 23, 8, "bark", width=2, shade=False)
p.ellipse(8, 6, 7, 4, "green", spark=True)
p.ellipse(23, 5, 8, 4, "green", spark=True)
p.ellipse(15, 3, 6, 3, "green", spark=True)
p.dots([(6, 5), (21, 3), (14, 2), (25, 6)], "lime")


def cactus(p, top=4):
    ground(p, 16, 30, 12, "sand")
    p.rect(12, top, 8, 22 - (top - 4), "green", r=3, spark=True)
    p.rect(3, top + 6, 5, 10, "green", r=2, spark=True)
    p.rect(7, top + 12, 6, 3, "green")
    p.rect(24, top + 4, 5, 8, "green", r=2, spark=True)
    p.rect(19, top + 9, 6, 3, "green")
    for (x, y) in [(13, top + 4), (17, top + 8), (14, top + 12), (18, top + 16), (4, top + 9), (26, top + 7)]:
        p.dots([(x, y)], "lime")
    p.rect(7, 26, 18, 5, "caramel", r=2, spark=True)
    p.rect(6, 26, 20, 2, "caramel", tone="light", shade=False)
    eyes(p, "green", [(14, top + 9), (18, top + 9)], r=1.2)
    smile(p, 14, 18, top + 13, depth=1)


p = pic("cactus", "КАКТУС", "plants", "common", 32, 32)
cactus(p)

p = pic("cactus_flower", "КАКТУС У ЦВІТУ", "plants", "uncommon", 32, 32)
cactus(p)
p.circle(16, 3, 3.5, "pink", spark=True)
p.circle(13, 5, 2.5, "pink")
p.circle(19, 5, 2.5, "pink")
p.circle(16, 4, 1.5, "pink", tone="white", shade=False)

p = pic("cactus_hat", "КАКТУС У СОМБРЕРО", "plants", "rare", 32, 32)
cactus(p, top=8)
p.ellipse(16, 8, 14, 2.5, "yellow", spark=True)
p.rect(10, 1, 12, 8, "yellow", r=2, spark=True)
p.rect(10, 6, 12, 2, "red", shade=False)
p.dots([(3, 8), (29, 8)], "red")

# ═══════════════════════════ ПРЕДМЕТИ ═══════════════════════════

p = pic("book", "КНИЖКА", "things", "common", 30, 28)
ground(p, 15, 26, 13, "sand")
p.rect(1, 2, 28, 22, "red", r=2, spark=True)
p.rect(1, 2, 4, 22, "raspberry", spark=True)
p.rect(7, 5, 19, 16, "cream", r=1, spark=True)
for y in (9, 12, 15, 18):
    p.line(10, y, 22, y, "cream", tone="shadow", shade=False)
p.rect(25, 3, 3, 20, "sand", spark=True)
p.dots([(3, 4), (3, 21)], "yellow")
p.rect(20, 0, 2, 5, "cyan", shade=False)                   # закладка

p = pic("heart", "СЕРДЕЧКО", "things", "common", 30, 30)
ground(p, 15, 28, 10, "sand")
p.circle(8, 8, 7.5, "red", spark=True)
p.circle(22, 8, 7.5, "red", spark=True)
p.poly([(1, 10), (29, 10), (15, 27)], "red")
p.rect(6, 8, 18, 6, "red")
p.dots([(5, 5), (6, 4)], "red", tone="white")
p.ellipse(15, 15, 4, 2.5, "raspberry", spark=True)
p.circle(3, 21, 1.8, "pink", spark=True)
p.circle(27, 21, 1.8, "pink", spark=True)

p = pic("key", "КЛЮЧИК", "things", "common", 32, 18)
ground(p, 16, 16, 13, "sand")
p.circle(7, 8, 6.5, "yellow", spark=True)
p.circle(7, 8, 3, "yellow", tone="shadow", shade=False)
p.circle(7, 8, 2, "slate", spark=True)
p.rect(13, 6, 18, 4, "yellow", spark=True)
p.rect(23, 10, 3, 4, "orange", spark=True)
p.rect(28, 10, 3, 5, "orange", spark=True)
p.dots([(4, 4), (14, 5)], "yellow", tone="white")
p.dots([(2, 2), (30, 1)], "cream")

p = pic("lamp", "ЛАМПОЧКА", "things", "common", 28, 32)
p.circle(14, 11, 11, "yellow", spark=True)
p.circle(14, 10, 7, "lemon", spark=True)
p.ink_line(11, 8, 14, 13)
p.ink_line(17, 8, 14, 13)
p.rect(9, 21, 10, 3, "orange", spark=True)
p.rect(8, 24, 12, 6, "silver", r=1, spark=True)
for y in (25, 27, 29):
    p.line(9, y, 19, y, "silver", tone="shadow", shade=False)
p.rect(11, 30, 6, 2, "slate", shade=False)
p.dots([(1, 5), (27, 5), (2, 16), (26, 16)], "yellow", tone="light")

p = pic("gift", "ПОДАРУНОК", "things", "uncommon", 30, 32)
ground(p, 15, 30, 12, "sand")
p.rect(3, 12, 24, 17, "cyan", r=1, spark=True)
p.rect(2, 11, 26, 4, "cyan", tone="light", shade=False)
p.rect(13, 11, 4, 18, "yellow", spark=True)
p.rect(2, 19, 26, 3, "yellow", spark=True)
p.circle(10, 7, 3.5, "orange", spark=True)
p.circle(20, 7, 3.5, "orange", spark=True)
p.circle(15, 9, 2, "yellow", shade=False)
p.dots([(5, 24), (23, 16), (8, 27)], "cyan", tone="white")

p = pic("potion", "ЗІЛЛЯ", "things", "uncommon", 26, 32)
ground(p, 13, 30, 10, "sand")
p.rect(10, 0, 6, 4, "caramel", spark=True)
p.rect(9, 4, 8, 6, "ice", tone="light", shade=False)
p.circle(13, 19, 11, "ice", spark=True)
p.circle(13, 20, 9.5, "violet", spark=True)
p.ellipse(13, 23, 7, 5, "magenta", shade=False)
p.dots([(8, 17), (15, 14), (18, 19), (11, 25)], "violet", tone="white")
p.dots([(12, 3), (14, 6)], "cyan", tone="light")

p = pic("robot", "РОБОТИК", "things", "uncommon", 28, 32)
ground(p, 14, 31, 11, "sand")
p.line(14, 4, 14, 1, "silver", tone="shadow", shade=False)
p.circle(14, 1, 1.6, "red", spark=True)
p.rect(4, 4, 20, 12, "silver", r=2, spark=True)
p.rect(7, 7, 14, 6, "slate", spark=True)
p.dots([(10, 9), (11, 10), (17, 9), (18, 10)], "cyan")
p.rect(11, 11, 6, 1, "cyan", shade=False)
p.rect(6, 17, 16, 10, "silver", r=1, spark=True)
p.rect(9, 19, 10, 4, "slate", spark=True)
p.dots([(10, 20), (13, 20), (16, 20)], "yellow")
p.dots([(11, 22), (15, 22)], "red")
p.rect(1, 18, 4, 7, "slate", spark=True)
p.rect(23, 18, 4, 7, "slate", spark=True)
p.rect(7, 27, 5, 4, "slate", shade=False)
p.rect(16, 27, 5, 4, "slate", shade=False)

p = pic("hourglass", "ПІСОЧНИЙ ГОДИННИК", "things", "rare", 28, 32)
ground(p, 14, 31, 11, "slate")
p.rect(3, 0, 22, 3, "bark", r=1, spark=True)
p.rect(3, 28, 22, 3, "bark", r=1, spark=True)
p.poly([(5, 3), (23, 3), (15, 16), (13, 16)], "ice", spark=True)
p.poly([(13, 16), (15, 16), (23, 28), (5, 28)], "ice", spark=True)
p.poly([(8, 5), (20, 5), (14, 13)], "sand", spark=True)
p.poly([(13, 16), (15, 16), (14, 22)], "sand", shade=False)
p.poly([(7, 27), (21, 27), (14, 21)], "sand", spark=True)
p.rect(4, 3, 2, 25, "caramel", spark=True)
p.rect(22, 3, 2, 25, "caramel", spark=True)

p = pic("diamond", "ДІАМАНТ", "things", "epic", 32, 28)
ground(p, 16, 26, 10, "slate")
p.poly([(0, 9), (7, 0), (25, 0), (32, 9), (16, 26)], "cyan", spark=True)
p.poly([(7, 0), (25, 0), (24, 8), (8, 8)], "ice", spark=True)
p.poly([(0, 9), (32, 9), (16, 26)], "cyan")
p.poly([(8, 9), (24, 9), (16, 24)], "blue", spark=True)
p.ink_line(8, 8, 16, 25)
p.ink_line(24, 8, 16, 25)
p.ink_line(0, 9, 32, 9)
p.dots([(4, 4), (28, 4), (12, 12), (20, 12)], "ice", tone="white")
p.poly(star_points(3, 22, 2.5, 1), "cream", shade=False)
p.poly(star_points(29, 22, 2.5, 1), "cream", shade=False)

p = pic("crystal", "КРИСТАЛ", "things", "legendary", 32, 40)
p.rect(6, 37, 20, 3, "slate", r=1, spark=True)
p.poly([(8, 38), (24, 38), (26, 30), (18, 4), (14, 4), (6, 30)], "violet", spark=True)
p.poly([(14, 4), (18, 4), (20, 24), (12, 24)], "magenta", tone="light", shade=False)
p.poly([(0, 34), (8, 20), (12, 30), (8, 38)], "periwinkle", spark=True)
p.poly([(32, 34), (24, 20), (20, 30), (24, 38)], "periwinkle", spark=True)
p.poly([(16, 0), (20, 8), (12, 8)], "magenta", spark=True)
p.ink_line(16, 6, 16, 36)
p.dots([(10, 14), (22, 18), (14, 30), (4, 28), (28, 28)], "violet", tone="white")
p.poly(star_points(3, 8, 2.5, 1), "cream", shade=False)
p.poly(star_points(29, 12, 2.5, 1), "cream", shade=False)
p.circle(29, 24, 1.6, "cyan", spark=True)
p.circle(3, 20, 1.6, "cyan", spark=True)

p = pic("gem_cosmic", "СЕРЦЕ ГАЛАКТИКИ", "things", "cosmic", 40, 36)
p.circle(12, 12, 10, "magenta", spark=True)
p.circle(28, 12, 10, "magenta", spark=True)
p.poly([(2, 16), (38, 16), (20, 36)], "magenta")
p.rect(8, 12, 24, 8, "magenta")
p.circle(12, 12, 6, "violet", spark=True)
p.circle(28, 12, 6, "violet", spark=True)
p.poly([(8, 18), (32, 18), (20, 32)], "violet", shade=False)
p.circle(20, 18, 8, "periwinkle", spark=True)
p.circle(20, 18, 4.5, "cyan", spark=True)
p.circle(20, 18, 2, "cream", shade=False)
for (x, y) in [(6, 8), (34, 8), (12, 26), (28, 26), (20, 8)]:
    p.dots([(x, y), (x + 1, y)], "magenta", tone="white")
for (x, y) in [(1, 30), (39, 30), (2, 2), (38, 2), (20, 0)]:
    p.dots([(x, y)], "cream")
p.poly(star_points(35, 26, 3, 1.2), "yellow", spark=True)
p.poly(star_points(5, 26, 3, 1.2), "yellow", spark=True)
p.dots([(16, 14), (24, 14), (20, 24)], "cyan", tone="light")

# ═══════════════════════════ ЗАПУСК ═══════════════════════════

def main():
    check_only = "--check" in sys.argv
    ids = [p.id for p in PICTURES]
    dup = {i for i in ids if ids.count(i) > 1}
    if dup:
        print("Дубльовані id:", sorted(dup))
        return 1
    if check_only:
        import tempfile
        out = tempfile.mkdtemp()
    else:
        for folder, _, names in os.walk(OUT):
            for n in names:
                if n.endswith(".txt"):
                    os.remove(os.path.join(folder, n))
        out = OUT
    written, errors, report = write_all(PICTURES, out)
    by_rarity = {}
    for rarity, theme, pid, info in report:
        by_rarity.setdefault(rarity, []).append(info)
    for rarity in ["common", "uncommon", "rare", "epic", "legendary", "cosmic"]:
        infos = by_rarity.get(rarity, [])
        if not infos:
            continue
        steps = sorted(i["steps"] for i in infos)
        tones = sorted(i["tones"] for i in infos)
        fams = sorted(i["families"] for i in infos)
        print(f"{rarity:10} n={len(infos):3}  кроків {steps[0]}–{steps[-1]} (мед {steps[len(steps) // 2]})  тонів {tones[0]}–{tones[-1]}  родин {fams[0]}–{fams[-1]}")
    if errors:
        print("\n".join(errors))
        print(f"\nПОМИЛОК: {len(errors)}")
        return 1
    print(f"{written} картинок → {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
