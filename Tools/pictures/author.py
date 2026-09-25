#!/usr/bin/env python3
"""
Авторський генератор бібліотеки картинок Ink Flow.

Джерело правди для гри — текстові файли в Assets/_Pictures/<тема>/<id>.txt
(формат у docs/pictures-format.md). Цей скрипт лише ВИРОБЛЯЄ їх із компактних
малюнків нижче: додає зовнішній контур навколо заливки, центрує на квадратній
сітці рідкості й перевіряє кількість кольорів. Художник може правити .txt руками
або дописувати нові — скрипт не потрібен для гри.

Літери кольорів — глобальна легенда майстер-палітри (Core/Config/MasterPalette.cs).
"""
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "_Pictures")

# Літера → індекс майстер-палітри. K — контур (чорнило), k — контур какао, n — контур глибокий синій.
LEGEND = {
    "K": 1, "k": 2, "n": 3,
    "W": 4, "C": 5, "S": 6, "G": 7, "R": 8, "O": 9, "A": 10, "L": 11, "M": 12, "E": 13,
    "T": 14, "Y": 15, "B": 16, "V": 17, "P": 18, "F": 19, "I": 20, "H": 21, "D": 22,
    "N": 23, "Z": 24, "X": 25, "Q": 26, "U": 27, "J": 28, "1": 29, "2": 30,
}
OUTLINES = {"K", "k", "n"}
# Найбільша сторона файлу = сітка з таблиці §6 + контурне кільце (по 1 px з кожного боку).
CANVAS = {"common": 14, "uncommon": 14, "rare": 16, "epic": 18, "legendary": 20, "cosmic": 22}
MIN_SIDE = {"common": 6, "uncommon": 8, "rare": 10, "epic": 12, "legendary": 14, "cosmic": 16}
COLORS = {"common": (2, 3), "uncommon": (3, 3), "rare": (3, 4), "epic": (4, 5), "legendary": (5, 6), "cosmic": (6, 8)}

PICTURES = []


def pic(pid, name, theme, rarity, rows, outline="K"):
    PICTURES.append(dict(id=pid, name=name, theme=theme, rarity=rarity, rows=rows, outline=outline))


def overlay(rows, edits):
    """Накладає правки (x, y, символ) на копію рядків; символ '.' стирає."""
    grid = [list(r) for r in rows]
    for x, y, ch in edits:
        while y >= len(grid):
            grid.append(["."] * len(grid[0]))
        while x >= len(grid[y]):
            for g in grid:
                g.append(".")
        grid[y][x] = ch
    return ["".join(g) for g in grid]


def pad_rows(rows):
    width = max(len(r) for r in rows)
    return [r.ljust(width, ".") for r in rows]


def auto_outline(rows, outline):
    """Зовнішній контур: порожня клітинка, що має 8-сусіда з заливкою, стає контуром."""
    rows = pad_rows(rows)
    h, w = len(rows), len(rows[0])
    big = ["." * (w + 2)] + ["." + r + "." for r in rows] + ["." * (w + 2)]
    grid = [list(r) for r in big]
    out = [row[:] for row in grid]
    for y in range(h + 2):
        for x in range(w + 2):
            if grid[y][x] != ".":
                continue
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < h + 2 and 0 <= nx < w + 2 and grid[ny][nx] not in (".",) and grid[ny][nx] not in OUTLINES:
                        out[y][x] = outline
    return ["".join(r) for r in out]


def trim(rows):
    rows = [r for r in rows if set(r) != {"."}]
    if not rows:
        return rows
    left = min(len(r) - len(r.lstrip(".")) for r in rows)
    right = min(len(r) - len(r.rstrip(".")) for r in rows)
    return [r[left:len(r) - right] for r in rows]


ERRORS = []


def build(p):
    rows = trim(auto_outline(p["rows"], p["outline"]))
    h, w = len(rows), len(rows[0])
    size = CANVAS[p["rarity"]]
    if max(w, h) > size:
        ERRORS.append(f"{p['id']}: малюнок {w}x{h} більший за сітку {size} ({p['rarity']})")
    if max(w, h) < MIN_SIDE[p["rarity"]]:
        ERRORS.append(f"{p['id']}: малюнок {w}x{h} замалий для {p['rarity']} (мінімум {MIN_SIDE[p['rarity']]})")
    used = sorted({ch for r in rows for ch in r if ch != "."}, key=lambda c: LEGEND[c])
    fills = [c for c in used if c not in OUTLINES]
    outlines = [c for c in used if c in OUTLINES]
    if outlines != [p["outline"]]:
        ERRORS.append(f"{p['id']}: контур має бути один ({p['outline']}), а є {outlines}")
    lo, hi = COLORS[p["rarity"]]
    if not (lo <= len(fills) <= hi):
        ERRORS.append(f"{p['id']}: {len(fills)} кольорів заливки, а {p['rarity']} просить {lo}–{hi}: {fills}")
    colors = " ".join(f"{c}={LEGEND[c]}" for c in used)
    text = [
        f"id: {p['id']}",
        f"name: {p['name']}",
        f"theme: {p['theme']}",
        f"rarity: {p['rarity']}",
        f"colors: {colors}",
        f"outline: {p['outline']}",
        "grid:",
    ] + rows
    return "\n".join(text) + "\n"


# ─────────────────────────── ТВАРИНИ ───────────────────────────

pic("duck", "КАЧЕНЯ", "animals", "common", [
    "....LLL...",
    "...LLLLL..",
    "...LLKLOO.",
    "...LLLLL..",
    "LLLLLLL...",
    "LLLLLLLL..",
    ".LLLLLLL..",
    "..LLLLL...",
    "....OO....",
])

pic("chick", "КУРЧА", "animals", "common", [
    "..LLLL..",
    ".LLLLLL.",
    "LLKLLKLL",
    "LLLOOLLL",
    "LLLLLLLL",
    ".LLLLLL.",
    "..LLLL..",
    "..O..O..",
])

pic("frog", "ЖАБКА", "animals", "common", [
    ".EE....EE.",
    ".EWK...EWK",
    "EEEEEEEEEE",
    "EEEEEEEEEE",
    "EEKKKKKKEE",
    "EEEEEEEEEE",
    ".EEEEEEEE.",
    "EE......EE",
])

pic("pig", "ПОРОСЯ", "animals", "common", [
    "II......II",
    "IIIIIIIIII",
    "IIKIIIIKII",
    "IIIIIIIIII",
    "IIIUUUUIII",
    "IIIUKUKUII",
    "IIIUUUUIII",
    ".IIIIIIII.",
    "..II..II..",
])

pic("bunny", "ЗАЙЧИК", "animals", "common", [
    "..WW....WW..",
    "..WUW..WUW..",
    "..WUW..WUW..",
    "..WWW..WWW..",
    ".WWWWWWWWWW.",
    "WWWKWWWWKWWW",
    "WWWWWWWWWWWW",
    "WWWWWUUWWWWW",
    ".WWWWWWWWWW.",
    "..WWWWWWWW..",
])

pic("fox", "ЛИСИЧКА", "animals", "uncommon", [
    "OO........OO",
    "OOO......OOO",
    "OOOO....OOOO",
    "OOOOOOOOOOOO",
    "OOKOOOOOOKOO",
    "OOOOOOOOOOOO",
    "OWUWWOOWWUWO",
    ".WWWWWWWWWW.",
    "..WWWWKWWW..",
    "....WWWW....",
])

pic("bear", "ВЕДМЕДИК", "animals", "uncommon", [
    ".NN......NN.",
    "NDDN....NDDN",
    "NNNNNNNNNNNN",
    "NNNNNNNNNNNN",
    "NNKNNNNNNKNN",
    "NNUNDDDDNUNN",
    "NNNDDKKDDNNN",
    "NNNDDDDDDNNN",
    ".NNNNNNNNNN.",
    "..NNNNNNNN..",
])

pic("owl", "СОВЕНЯ", "animals", "rare", [
    "..NN......NN..",
    "..NNN....NNN..",
    ".NNNNNNNNNNNN.",
    ".NNWWWNNWWWNN.",
    ".NWWKWWWWKWWN.",
    ".NWWWWNONWWWN.",
    ".NNWWWNNNWWWN.",
    ".NNNNDDDDNNNN.",
    ".NNNDDDDDDNNN.",
    ".NNNDDDDDDNNN.",
    "..NNNDDDDNNN..",
    "...NNNNNNNN...",
    "....OO..OO....",
])

pic("penguin", "ПІНГВІН", "animals", "common", [
    "...GGGG...",
    "..GGGGGG..",
    ".GGWKGWKG.",
    ".GGGOOGGG.",
    ".GGWWWWGG.",
    "GGWWWWWWGG",
    "GGWWWWWWGG",
    "GGWWWWWWGG",
    ".GWWWWWWG.",
    "..OO..OO..",
])

pic("panda", "ПАНДА", "animals", "uncommon", [
    ".GG......GG.",
    "GGGWWWWWWGGG",
    ".WWWWWWWWWW.",
    ".WGGWWWWGGW.",
    ".WGKGWWGKGW.",
    ".WUWWWWWWUW.",
    ".WWWWKKWWWW.",
    "..WWWWWWWW..",
    "..GGWWWWGG..",
    ".GGGGWWGGGG.",
])

pic("cow", "КОРІВКА", "animals", "rare", [
    "..GG......GG..",
    ".GGWWWWWWWWGG.",
    "..WWWKWWKWWW..",
    "..WWWWWWWWWW..",
    "..WWWWWWWWWW..",
    ".WWWIIIIIIWWW.",
    ".WWIIKIIKIIWW.",
    ".WWIIIIIIIIWW.",
    "..WWWIIIIWWW..",
    "..WWKKWWKKWW..",
    "..WWWKWWKWWW..",
    "...WWWWWWWW...",
])

pic("hedgehog", "ЇЖАЧОК", "animals", "uncommon", [
    "....NNNNNN..",
    "..NNNNNNNNN.",
    ".NNNNNNNNNNN",
    "NNNNNNNNNNNN",
    "NNNNNNNNNDDD",
    "NNNNNNNNDDKD",
    "NNNNNNNNDUDK",
    ".NNNNNNNNDDD",
    "..DD...DD...",
])

pic("mouse", "МИШКА", "animals", "common", [
    ".GG....GG.",
    "GIIG..GIIG",
    "GGGGGGGGGG",
    ".GGGGGGGG.",
    ".GKGGGGKG.",
    ".GGGGGGGG.",
    ".GGGGIGGG.",
    "..GGGGGG..",
    "...GGGG...",
])

# Серія котиків від одного шаблону.
CAT = [
    ".OO......OO.",
    ".OOO....OOO.",
    ".OOOOOOOOOO.",
    ".OOOOOOOOOO.",
    ".OKOOOOOOKO.",
    ".OOOOIIOOOO.",
    "OOOOOIKIOOOO",
    "OOOOOOOOOOOO",
    ".OOOOOOOOOO.",
    "..OOOOOOOO..",
]
pic("cat", "КОТИК", "animals", "common", CAT)
pic("cat_bow", "КОТИК ІЗ БАНТОМ", "animals", "uncommon", overlay(CAT, [
    (8, 0, "R"), (9, 0, "R"), (10, 0, "R"), (8, 1, "R"), (10, 1, "R"), (9, 1, "K"),
]))
pic("cat_hat", "КОТИК У КАПЕЛЮСІ", "animals", "uncommon", overlay(
    ["............", "............"] + CAT, [
        (3, 0, "V"), (4, 0, "V"), (5, 0, "V"), (6, 0, "V"), (7, 0, "V"), (8, 0, "V"),
        (3, 1, "V"), (4, 1, "V"), (5, 1, "V"), (6, 1, "V"), (7, 1, "V"), (8, 1, "V"),
        (1, 2, "V"), (2, 2, "V"), (3, 2, "V"), (4, 2, "V"), (5, 2, "V"), (6, 2, "V"), (7, 2, "V"), (8, 2, "V"), (9, 2, "V"), (10, 2, "V"),
    ]))
pic("cat_glasses", "КОТИК В ОКУЛЯРАХ", "animals", "rare", overlay(CAT, [
    (0, 4, "K"), (1, 4, "Y"), (2, 4, "Y"), (3, 4, "K"), (4, 4, "K"), (5, 4, "K"),
    (6, 4, "K"), (7, 4, "K"), (8, 4, "Y"), (9, 4, "Y"), (10, 4, "K"), (11, 4, "K"),
    (1, 5, "Y"), (2, 5, "Y"), (8, 5, "Y"), (9, 5, "Y"),
]))

# ─────────────────────────── РОСЛИНИ ───────────────────────────

pic("tree", "ДЕРЕВЦЕ", "plants", "common", [
    "....EEEE....",
    "..EEEEEEEE..",
    ".EEEEEEEEEE.",
    ".EEEEEEEEEE.",
    "..EEEEEEEE..",
    "....EEEE....",
    ".....NN.....",
    ".....NN.....",
    ".....NN.....",
    "...NNNNNN...",
])

pic("flower", "КВІТКА", "plants", "common", [
    "...RR..RR...",
    "..RRRRRRRR..",
    "..RRRAARRR..",
    "..RRRAARRR..",
    "..RRRRRRRR..",
    "...RR..RR...",
    ".....EE.....",
    "..EE.EE.....",
    "...EEEE.EE..",
    ".....EEEE...",
    ".....EE.....",
])

pic("mushroom", "ГРИБОЧОК", "plants", "common", [
    "...RRRRRR...",
    ".RRRWWRRRRR.",
    "RRRRWWRRWWRR",
    "RRRRRRRRWWRR",
    "RRWWRRRRRRRR",
    ".RRWWRRRRRR.",
    "....CCCC....",
    "....CKKC....",
    "....CCCC....",
    "...CCCCCC...",
])

pic("sprout", "ПАРОСТОК", "plants", "common", [
    ".EE....EE.",
    "EEEE..EEEE",
    ".EEEEEEEE.",
    "...EEEE...",
    "....EE....",
    "....EE....",
    "..NNNNNN..",
    "..NNNNNN..",
    "...NNNN...",
])

pic("clover", "КОНЮШИНА", "plants", "uncommon", [
    "..EEE..EEE..",
    ".EEEEEEEEEE.",
    ".EEEMEEMEEE.",
    "..EEEEEEEE..",
    "EEEEEEEEEEEE",
    "EEEMEEEEMEEE",
    ".EEEEEEEEEE.",
    "..EEE..EEE..",
    "......NN....",
    ".....NN.....",
])

pic("sunflower", "СОНЯШНИК", "plants", "uncommon", [
    "...AA..AA...",
    ".AAAAAAAAAA.",
    "..AANNNNAA..",
    ".AANNNNNNAA.",
    ".AANNNNNNAA.",
    "..AANNNNAA..",
    ".AAAAAAAAAA.",
    "...AA..AA...",
    ".....EE.....",
    "..EEEEE.EE..",
    ".....EEEE...",
])

pic("tulip", "ТЮЛЬПАН", "plants", "common", [
    ".II.II.II.",
    ".IIIIIIII.",
    ".IIIIIIII.",
    "..IIIIII..",
    "....EE....",
    "....EE....",
    ".EE.EE.EE.",
    "..EEEEEE..",
    "....EE....",
])

pic("bonsai", "БОНСАЙ", "plants", "rare", [
    "..EEEE....EEE.",
    ".EEEEEEE.EEEEE",
    ".EEEEEEEEEEEE.",
    "...EEEEEEEEE..",
    ".......NN.....",
    "......NN......",
    "......NN......",
    "..DDDDDDDDDD..",
    ".DDDDDDDDDDDD.",
    ".DDDDDDDDDDDD.",
    "..DDDDDDDDDD..",
], outline="k")

# Серія кактусів.
CACTUS = [
    "....EE....",
    "....EE....",
    "EE..EE..EE",
    "EE..EE..EE",
    "EEEEEEEEEE",
    "..EEEEEE..",
    "....EE....",
    "....EE....",
    "..DDDDDD..",
    ".DDDDDDDD.",
]
pic("cactus", "КАКТУС", "plants", "common", CACTUS)
pic("cactus_flower", "КАКТУС У ЦВІТУ", "plants", "uncommon", overlay(CACTUS, [
    (3, 0, "I"), (4, 0, "I"), (5, 0, "I"), (6, 0, "I"), (4, 1, "I"), (5, 1, "I"), (0, 2, "I"), (9, 2, "I"),
]))
pic("cactus_hat", "КАКТУС У СОМБРЕРО", "plants", "rare", overlay(
    ["..........", "..........", ".........."] + CACTUS, [
        (3, 0, "A"), (4, 0, "A"), (5, 0, "A"), (6, 0, "A"),
        (3, 1, "R"), (4, 1, "R"), (5, 1, "R"), (6, 1, "R"),
        (0, 2, "A"), (1, 2, "A"), (2, 2, "A"), (3, 2, "A"), (4, 2, "A"), (5, 2, "A"), (6, 2, "A"), (7, 2, "A"), (8, 2, "A"), (9, 2, "A"),
    ]))

# ─────────────────────────── ДИНОЗАВРИКИ ───────────────────────────

pic("dino_green", "ДИНОЗАВРИК", "dinos", "common", [
    "......EEEE..",
    "......EKEEUE",
    "......EEEEE.",
    "......EEE...",
    "EE...EEEE...",
    ".EE.EEEEE...",
    "..EEMMEEE...",
    "...EMMEEE...",
    "....EEEEE...",
    "....EE.EE...",
])

pic("dino_blue", "СИНЬОЗАВР", "dinos", "common", [
    ".BBBB.......",
    "BBBBKB......",
    "BBBBBB......",
    "..BBB.......",
    "..BBBB....BB",
    "..BBBBB..BB.",
    "..BBXXBBBB..",
    "..BBXXBBB...",
    "..BBBBBB....",
    "..BB.BB.....",
])

pic("stego", "СТЕГОЗАВРИК", "dinos", "uncommon", [
    "...O..O..O..",
    "..OOOOOOOOO.",
    ".EEEEEEEEEEE",
    "EEEEEEEEEEEE",
    "EKEEEEEEEEEE",
    "EEMMMMMMEEE.",
    ".EEEEEEEEE..",
    "..EE...EE...",
])

pic("trice", "ТРИЦЕРАТОПСИК", "dinos", "rare", [
    "..W......W....",
    ".WWI....IWW...",
    "IIIIIIIIIII...",
    "IIIIIIIIIIII..",
    "IIIkIIIIIIIII.",
    "IIIIIIIUIIIIII",
    ".IIIIIIIIIIIII",
    "..IIIIIIIIIII.",
    "..III..III....",
    "..III..III....",
], outline="k")

pic("dino_egg", "ЯЙЦЕ ДИНОЗАВРА", "dinos", "common", [
    "...CCCC...",
    "..CCCCCC..",
    ".CCEECCCC.",
    ".CCCCCEEC.",
    "CCCCCCCCCC",
    "CCEECCCCCC",
    "CCCCCCEECC",
    ".CCCCCCCC.",
    "..CCCCCC..",
])

pic("dino_hatch", "ВИЛУПОК", "dinos", "uncommon", [
    "....EEEE....",
    "...EEKEUE...",
    "...EEEEEE...",
    "....EEE.....",
    ".CC.EEE.CC..",
    "CCCCEEECCCC.",
    "CCCCCCCCCCC.",
    "CCCCCCCCCCC.",
    ".CCCCCCCCC..",
    "..CCCCCCC...",
])

pic("rex", "ТИРАНОЗАВР", "dinos", "epic", [
    "......EEEEEE....",
    "......EEKEEEEE..",
    "......EEEEEEEE..",
    "......EEEWWWWE..",
    "......EEEEEE....",
    "......EEEEE.....",
    "EE...EEEEEEE....",
    ".EE.EEEEEMMEE...",
    "..EEEEEEEMMEEEE.",
    "...EEEEEEMMEE...",
    "....EEEEEEEE....",
    ".....EEEEEEE....",
    ".....EEEEEEE....",
    ".....EEE.EEE....",
    "....DDD..DDD....",
])

# ─────────────────────────── ПРИВИДИ (серія) ───────────────────────────

GHOST = [
    "...WWWW...",
    "..WWWWWW..",
    ".WWWWWWWW.",
    ".WWKWWKWW.",
    ".WUWWWWUW.",
    ".WWWWWWWW.",
    ".WWWWWWWW.",
    ".WWWWWWWW.",
    ".WW.WW.WW.",
]
pic("ghost", "ПРИВИДИК", "ghosts", "common", GHOST)
pic("ghost_shy", "СОРОМ'ЯЗЛИВИЙ ПРИВИД", "ghosts", "common", overlay(GHOST, [(4, 3, "."), (5, 3, "."), (3, 3, "K"), (6, 3, "K"), (4, 6, "K"), (5, 6, "K")]))
pic("ghost_hat", "ПРИВИД У КАПЕЛЮСІ", "ghosts", "uncommon", overlay(
    ["..........", ".........."] + GHOST, [
        (3, 0, "K"), (4, 0, "K"), (5, 0, "K"), (6, 0, "K"),
        (3, 1, "K"), (4, 1, "K"), (5, 1, "K"), (6, 1, "K"),
        (1, 2, "K"), (2, 2, "K"), (3, 2, "R"), (4, 2, "R"), (5, 2, "R"), (6, 2, "R"), (7, 2, "K"), (8, 2, "K"),
    ]))
pic("ghost_pirate", "ПРИВИД-ПІРАТ", "ghosts", "uncommon", overlay(GHOST, [
    (2, 3, "K"), (1, 3, "K"), (3, 3, "K"), (2, 2, "K"),
    (1, 1, "R"), (2, 1, "R"), (3, 1, "R"), (4, 1, "R"), (5, 1, "R"), (6, 1, "R"), (7, 1, "R"), (8, 1, "R"),
    (2, 0, "R"), (3, 0, "R"), (4, 0, "R"), (5, 0, "R"), (6, 0, "R"), (7, 0, "R"),
]))
pic("ghost_crown", "ПРИВИД-КОРОЛЬ", "ghosts", "rare", overlay(
    ["..........", ".........."] + GHOST, [
        (2, 0, "A"), (4, 0, "A"), (7, 0, "A"), (2, 1, "A"), (3, 1, "A"), (4, 1, "A"), (5, 1, "A"), (6, 1, "A"), (7, 1, "A"),
        (3, 1, "R"), (6, 1, "R"),
    ]))
pic("ghost_wizard", "ПРИВИД-ЧАРІВНИК", "ghosts", "rare", overlay(
    ["..........", "..........", ".........."] + GHOST, [
        (4, 0, "P"), (5, 0, "P"), (3, 1, "P"), (4, 1, "P"), (5, 1, "P"), (6, 1, "P"),
        (2, 2, "P"), (3, 2, "P"), (4, 2, "A"), (5, 2, "P"), (6, 2, "P"), (7, 2, "P"),
        (1, 3, "P"), (2, 3, "P"), (3, 3, "P"), (4, 3, "P"), (5, 3, "P"), (6, 3, "P"), (7, 3, "P"), (8, 3, "P"),
    ]))
pic("ghost_love", "ЗАКОХАНИЙ ПРИВИД", "ghosts", "uncommon", overlay(GHOST, [
    (2, 3, "R"), (3, 3, "R"), (6, 3, "R"), (7, 3, "R"), (3, 5, "K"), (4, 5, "K"), (5, 5, "K"), (6, 5, "K"),
]))
pic("ghost_royal", "ПРИВИД-ІМПЕРАТОР", "ghosts", "epic", overlay(
    ["..............", "..............", ".............."] + [".." + r + ".." for r in GHOST] + ["..............", ".............."], [
        (3, 0, "A"), (7, 0, "A"), (11, 0, "A"), (3, 1, "A"), (4, 1, "A"), (5, 1, "R"), (6, 1, "A"), (7, 1, "A"), (8, 1, "A"), (9, 1, "R"), (10, 1, "A"), (11, 1, "A"),
        (3, 2, "A"), (4, 2, "A"), (5, 2, "A"), (6, 2, "A"), (7, 2, "A"), (8, 2, "A"), (9, 2, "A"), (10, 2, "A"), (11, 2, "A"),
        (1, 6, "P"), (2, 6, "P"), (12, 6, "P"), (13, 6, "P"), (1, 7, "P"), (2, 7, "P"), (12, 7, "P"), (13, 7, "P"),
        (1, 8, "P"), (2, 8, "P"), (12, 8, "P"), (13, 8, "P"), (1, 9, "P"), (2, 9, "P"), (12, 9, "P"), (13, 9, "P"),
        (5, 9, "A"), (6, 9, "A"), (7, 9, "A"), (8, 9, "A"),
    ]))

# ─────────────────────────── ЇЖА ───────────────────────────

pic("apple", "ЯБЛУКО", "food", "common", [
    ".....NN...",
    "....NN.EE.",
    "..RRRRRRR.",
    ".RRRRRRRRR",
    "RRRRRRRRRR",
    "RRRRRRRRRR",
    "RRRRRRRRRR",
    ".RRRRRRRR.",
    "..RRR.RRR.",
])

pic("cherry", "ЧЕРЕШНІ", "food", "common", [
    ".....E....",
    "....EE....",
    "...EE.E...",
    "..EE..EE..",
    ".RR....RR.",
    "RRRR..RRRR",
    "RWRR..RWRR",
    "RRRR..RRRR",
    ".RR....RR.",
])

pic("strawberry", "ПОЛУНИЦЯ", "food", "common", [
    "....EEEE....",
    "..EEEEEEEE..",
    "...RRRRRR...",
    "..RRRLRRRR..",
    "..RRRRRRLR..",
    "..RLRRRRRR..",
    "..RRRRLRRR..",
    "...RRRRRR...",
    "....RRRR....",
    ".....RR.....",
])

pic("banana", "БАНАН", "food", "common", [
    ".........N",
    "........LL",
    ".......LLL",
    "......LLL.",
    ".....LLL..",
    "...LLLL...",
    "LLLLLL....",
    ".LLLL.....",
])

pic("watermelon", "КАВУН", "food", "uncommon", [
    "EEEEEEEEEEEE",
    ".EWWWWWWWWE.",
    ".ERRRRRRRRE.",
    "..RRKRRRKR..",
    "..RRRRKRRR..",
    "...RRRRRR...",
    "....RRRR....",
    ".....RR.....",
])

pic("donut", "ПОНЧИК", "food", "uncommon", [
    "...IIIIII...",
    ".IIIIIIIIII.",
    ".IILIIIILII.",
    "IIII.DDD.III",
    "IIII.DDD.III",
    "IILI.DDD.ILI",
    ".IIIIIIIIII.",
    "..DDDDDDDD..",
    "...DDDDDD...",
])

pic("icecream", "МОРОЗИВО", "food", "uncommon", [
    "....IIII....",
    "...IIIIII...",
    "..IIIIIIII..",
    "..WWWWWWWW..",
    "..WWWWWWWW..",
    "...DDDDDD...",
    "...DDDDDD...",
    "....DDDD....",
    "....DDDD....",
    ".....DD.....",
])

pic("pizza", "ПІЦА", "food", "rare", [
    "......AA......",
    ".....AAAA.....",
    "....AARRAA....",
    "....AAAAAA....",
    "...AARRAAAA...",
    "...AAAAAAAA...",
    "..AAAAAARRAA..",
    "..AARRAAAAAA..",
    ".AAAAAAAAAAAA.",
    ".DDDDDDDDDDDD.",
    "DDDDDDDDDDDDDD",
], outline="k")

pic("burger", "БУРГЕР", "food", "epic", [
    "...DDDDDDDD...",
    "..DDDDDDDDDD..",
    ".DDDDDDDDDDDD.",
    ".EEEEEEEEEEEE.",
    "..RRRRRRRRRR..",
    ".NNNNNNNNNNNN.",
    ".AAAAAAAAAAAA.",
    "..DDDDDDDDDD..",
    "...DDDDDDDD...",
], outline="k")

pic("sushi", "СУШІ", "food", "uncommon", [
    "..RROORRR...",
    ".RRRRRRRRR..",
    "RRRRRRRRRRRR",
    "WWWWWWWWWWWW",
    "WWWWWWWWWWWW",
    ".WWWWWWWWWW.",
    "..KKKKKKKK..",
])

pic("taco", "ТАКО", "food", "rare", [
    "....EE..EE....",
    "..EERRREERRE..",
    ".ERRRRRRRRRRE.",
    "AAAAAAAAAAAAAA",
    "AAAAAAAAAAAAAA",
    ".AAAAAAAAAAAA.",
    "..AAAAAAAAAA..",
    "...AAAAAAAA...",
    "....AAAAAA....",
    ".....AAAA.....",
], outline="k")

pic("egg", "ЯЄЧНЯ", "food", "common", [
    "...WWWW...",
    ".WWWWWWWW.",
    "WWWAAAWWWW",
    "WWAAAAAWWW",
    "WWAAAAAWWW",
    "WWWAAAWWWW",
    ".WWWWWWWW.",
    "...WWWW...",
])

# ─────────────────────────── СОЛОДОЩІ ───────────────────────────

pic("candy", "ЦУКЕРКА", "sweets", "common", [
    "II......II",
    "III....III",
    "IIIIIIIIII",
    "IIWIIWIIII",
    "IIIIWIIWII",
    "IIIIIIIIII",
    "III....III",
    "II......II",
])

pic("lollipop", "ЛЬОДЯНИК", "sweets", "common", [
    "..RRRRRR..",
    ".RRWWRRRR.",
    "RRWRRRRWRR",
    "RRRRRWWRRR",
    "RRRWWRRRRR",
    ".RRRRRRRR.",
    "..RRRRRR..",
    "....WW....",
    "....WW....",
    "....WW....",
])

pic("cupcake", "КАПКЕЙК", "sweets", "uncommon", [
    ".....RR.....",
    "...IIIIII...",
    "..IIIIIIII..",
    ".IIIIIIIIII.",
    ".IIIIIIIIII.",
    "..DDDDDDDD..",
    "..DDDDDDDD..",
    "...DDDDDD...",
    "...DDDDDD...",
], outline="k")

pic("chocolate", "ШОКОЛАДКА", "sweets", "common", [
    "NNNNNNNNNN",
    "NDDNDDNDDN",
    "NDDNDDNDDN",
    "NNNNNNNNNN",
    "NDDNDDNDDN",
    "NDDNDDNDDN",
    "NNNNNNNNNN",
], outline="k")

pic("cake", "ТОРТ", "sweets", "epic", [
    ".......AA.......",
    ".......AA.......",
    "....IIIIIIII....",
    "...IIIIIIIIII...",
    "...WWWWWWWWWW...",
    "...RRRRRRRRRR...",
    ".IIIIIIIIIIIIII.",
    ".IIIIIIIIIIIIII.",
    ".WWWWWWWWWWWWWW.",
    ".RRRRRRRRRRRRRR.",
    "IIIIIIIIIIIIIIII",
    "IIIIIIIIIIIIIIII",
    "WWWWWWWWWWWWWWWW",
    "DDDDDDDDDDDDDDDD",
], outline="k")

# ─────────────────────────── МОРСЬКІ МЕШКАНЦІ ───────────────────────────

pic("fish", "РИБКА", "sea", "common", [
    "....BBBB....",
    "..BBBBBBB...",
    "BBBBBKBBBBB.",
    ".BBXXXBBBBBB",
    "BBBBBBBBBBB.",
    "..BBBBBBB...",
    "....BBBB....",
])

pic("crab", "КРАБИК", "sea", "common", [
    "RR......RR",
    "RRR....RRR",
    ".RRRRRRRR.",
    "RRWKRRKWRR",
    "RRRRRRRRRR",
    ".RRRRRRRR.",
    "R.RRRRRR.R",
    ".RR....RR.",
])

pic("octopus", "ВОСЬМИНІЖКА", "sea", "uncommon", [
    "...PPPPPP...",
    "..PPPPPPPP..",
    ".PPWKPPWKPP.",
    ".PPPPPPPPPP.",
    ".PPPPUUPPPP.",
    "..PPPPPPPP..",
    ".PPPPPPPPPP.",
    "PPPPPPPPPPPP",
    "PP.PP..PP.PP",
    "P..P....P..P",
])

pic("jellyfish", "МЕДУЗКА", "sea", "uncommon", [
    "...FFFFFF...",
    ".FFFFFFFFFF.",
    "FFFWKFFWKFFF",
    "FFIFFFFFFIFF",
    ".F.F.FF.F.F.",
    ".F.F.FF.F.F.",
    "...F.FF.F...",
    "...F....F...",
])

pic("whale", "КИТ", "sea", "rare", [
    "......XX......",
    ".....X.X......",
    "..BBBBBBBB....",
    ".BBKBBBBBBBB..",
    "BBBBBBBBBBBB.B",
    "BBBBBBBBBBBBBB",
    "BWWWWWWWWBBBBB",
    ".WWWWWWWWBBB..",
    "..WWWWWWWB....",
])

pic("turtle", "ЧЕРЕПАШКА", "sea", "uncommon", [
    "....EEEEEE..",
    "..EEEEEEEEEE",
    ".EEEEEEEEEEE",
    "MMEEEEEEEEE.",
    "MKMUEEEEEEE.",
    "MMMMMMMMMMMM",
    "..MM.MMM.MM.",
])

pic("seahorse", "МОРСЬКИЙ КОНИК", "sea", "rare", [
    "...AAAA...",
    "..AWKAAOO.",
    "..AAAAA...",
    "...AAAA...",
    "...AAOOA..",
    "....AAAA..",
    "...AAAAA..",
    "..AAAAAA..",
    "..AAAAA...",
    "...AAAA...",
    "....AAAA..",
    "...AAAA...",
    "..AAAA....",
])

pic("shell", "МУШЛЯ", "sea", "common", [
    "....IIII....",
    "..IIIIIIII..",
    ".IIWIIWIIWI.",
    ".IIIIIIIIII.",
    "IIWIIWIIWIII",
    ".IIIIIIIIII.",
    "..IIIIIIII..",
    "....IIII....",
])

pic("axolotl", "АКСОЛОТЛЬ", "sea", "epic", [
    "..F..IIIIII..F..",
    ".FF.IIIIIIII.FF.",
    "F..IIIIIIIIII..F",
    "...IWKIIIIKWI...",
    "...IIIIIIIIII...",
    "...IIIUUUUIII...",
    "....IIIIIIII....",
    "....IIIIIIIIIII.",
    "....IIIIIIIIIIII",
    "....IIIIIIIIIII.",
    "....II.II.II....",
])

# ─────────────────────────── КОСМОС ───────────────────────────

pic("rocket", "РАКЕТА", "space", "common", [
    "....RR....",
    "...RRRR...",
    "...SSSS...",
    "...SKKS...",
    "...SKKS...",
    "...SSSS...",
    "..RSSSSR..",
    ".RRSSSSRR.",
    ".R.SSSS.R.",
    "...OOOO...",
    "....OO....",
])

pic("planet", "ПЛАНЕТА", "space", "common", [
    "...BBBB...",
    "..BBEEBB..",
    ".BBBBBEBB.",
    "ABBBBBBBBA",
    ".AAAAAAAA.",
    ".BBBBBBBB.",
    "..BEEBBB..",
    "...BBBB...",
])

pic("star", "ЗІРОЧКА", "space", "common", [
    "....LA....",
    "....AA....",
    "...AAAA...",
    "AAAAAAAAAA",
    ".AAAAAAAA.",
    "..AAAAAA..",
    "..AAAAAA..",
    ".AAA..AAA.",
    "AA......AA",
])

pic("moon", "МІСЯЦЬ", "space", "common", [
    "....LLLL..",
    "...LLL....",
    "..LAL.....",
    ".LLL......",
    ".LLA......",
    ".LLL......",
    "..LLL.....",
    "...LLL....",
    "....LLLL..",
])

pic("comet", "КОМЕТА", "space", "uncommon", [
    "........AA..",
    "......ALLLAA",
    "....OOALLLAA",
    "..OOOOAAAAAA",
    "OOOO..AAAA..",
    "OO..........",
])

pic("ufo", "ЛЕТЮЧА ТАРІЛКА", "space", "uncommon", [
    "....YYYY....",
    "...YYEEYY...",
    "...YYEEYY...",
    "SSSSSSSSSSSS",
    "SSSSSSSSSSSS",
    ".SSSSSSSSSS.",
    "..Y..YY..Y..",
])

pic("satellite", "СУПУТНИК", "space", "rare", [
    "BBB...SS...BBB",
    "BBB...SS...BBB",
    "BBB.SSSSSS.BBB",
    "BBBSSSSSSSSBBB",
    "BBB.SSAASS.BBB",
    "BBB.SSSSSS.BBB",
    "BBB...SS...BBB",
    "......SS......",
    ".....SSSS.....",
])

pic("astronaut", "КОСМОНАВТ", "space", "rare", [
    "....WWWW....",
    "...WWWWWW...",
    "..WWnnnnWW..",
    "..WWnnnnWW..",
    "..WWWWWWWW..",
    ".WWWWWWWWWW.",
    "WWWWRRRRWWWW",
    "WW.WWWWWW.WW",
    "...SSSSSS...",
    "...SS..SS...",
], outline="n")

pic("galaxy", "ГАЛАКТИКА", "space", "legendary", [
    "......PPPP........",
    "....PPPPPPPP......",
    "...PPPVVVVPPP.....",
    "..PPVVVVVVVVPP....",
    "..PVVVBBBBVVVP....",
    ".PPVVBBAABBVVPP...",
    ".PVVVBAAAABVVVP...",
    ".PVVVBAAAABVVVP...",
    ".PPVVBBAABBVVPP...",
    "..PVVVBBBBVVVP....",
    "..PPVVVVVVVVPP....",
    "...PPPVVVVPPP.....",
    "....PPPPPPPP......",
    "......PPPP......W.",
    "W..............WWW",
    "WW..............W.",
], outline="n")

pic("blackhole", "ЧОРНА ДІРА", "space", "cosmic", [
    ".....PPPPPPPP.....",
    "...PPPPPPPPPPPP...",
    "..PPPVVVVVVVVPPP..",
    ".PPVVVVFFFFVVVVPP.",
    ".PVVVFFFOOOFFFVVP.",
    "PPVVFFOOOOOOOFFVVP",
    "PVVFFOOAAAAAOOFFVP",
    "PVVFOOAAAAAAAOOFVP",
    "PVVFOOAAnnnAAOOFVP",
    "PVVFOOAAnnnAAOOFVP",
    "PVVFOOAAAAAAAOOFVP",
    "PVVFFOOAAAAAOOFFVP",
    "PPVVFFOOOOOOOFFVVP",
    ".PVVVFFFOOOFFFVVP.",
    ".PPVVVVFFFFVVVVPP.",
    "..PPPVVVVVVVVPPP..",
    "...PPPPPPPPPPPP...",
    ".....PPPPPPPP.....",
    "S..S...S...S...S.S",
    ".S.....S.......S..",
], outline="n")

# ─────────────────────────── МОНСТРИКИ ───────────────────────────

pic("slime", "СЛИМАЧОК", "monsters", "common", [
    "....EEEE....",
    "..EMMEEEEE..",
    ".EEEEEEEEEE.",
    ".EWKEEEWKEE.",
    ".EEEEEEEEEE.",
    "EEEEEEEEEEEE",
    "EEEEEEEEEEEE",
    ".EEEEEEEEEE.",
])

pic("cyclops", "ЦИКЛОПИК", "monsters", "common", [
    "...PPPPPP...",
    "..PPPPPPPP..",
    ".PPPPPPPPPP.",
    ".PPPWWWWPPP.",
    ".PPWWKKWWPP.",
    ".PPPWWWWPPP.",
    ".PPPPPPPPPP.",
    ".PPPPPPPPPP.",
    "..PPPPPPPP..",
    "..PP.PP.PP..",
])

pic("horned", "РОГАТИК", "monsters", "uncommon", [
    "A..........A",
    "AA........AA",
    ".RRRRRRRRRR.",
    "RRRRRRRRRRRR",
    "RRKRRRRRRKRR",
    "RRRRRRRRRRRR",
    "RRRRWWWWRRRR",
    "RRRRWKKWRRRR",
    ".RRRRRRRRRR.",
    "..RR....RR..",
])

pic("threeeyes", "ТРИОКИЙ", "monsters", "uncommon", [
    "....TTTT....",
    "..TTTTTTTT..",
    ".TTTTTTTTTT.",
    ".TWKTTWKTWK.",
    ".TUTTTTTTUT.",
    ".TTTTTTTTTT.",
    "TTTTTTTTTTTT",
    ".TT.TTTT.TT.",
    "..T......T..",
])

pic("fluffy", "ПУХНАСТИК", "monsters", "rare", [
    "..II..II..II..",
    ".IIIIIIIIIIII.",
    "IIIIIIIIIIIIII",
    "IIWWIIIIIIWWII",
    "IIWKIIIIIIKWII",
    "IIIIIIIIIIIIII",
    "IIIIIUUUUIIIII",
    ".IIIIIIIIIIII.",
    "..IIIIIIIIII..",
    ".II.II..II.II.",
])

pic("bigmouth", "ЗУБАСТИК", "monsters", "rare", [
    "...BBBBBBBB...",
    "..BBBBBBBBBB..",
    ".BBKBBBBBBKBB.",
    ".BBBBBBBBBBBB.",
    ".BBRRRRRRRRBB.",
    ".BBRWRWRWRWBB.",
    ".BBRRRRRRRRBB.",
    "..BBBBBBBBBB..",
    "..B.BB..BB.B..",
])

pic("kraken", "КРАКЕНЯ", "monsters", "legendary", [
    ".......PPPPPP.....",
    ".....PPFPPPPFPP...",
    "....PPPPPPPPPPPP..",
    "....PPWnPPPPWnPP..",
    "....PPPPPPPPPPPP..",
    "....PPPPPRRRPPPP..",
    ".....PPPPPPPPPP...",
    "...PPPPPPPPPPPPPP.",
    "..PPP.PPP.PPP.PPP.",
    ".PPP..PP..PP..PPP.",
    "PPP...PP..PP...PPP",
    "PP...PPP..PPP...PP",
    "V...PPP....PPP...V",
    "....VV......VV....",
    "...VV........VV...",
], outline="n")

# ─────────────────────────── ПОГОДА ───────────────────────────

pic("sun", "СОНЕЧКО", "weather", "common", [
    "....A..A....",
    ".A..AAAA..A.",
    "..AAAAAAAA..",
    "..AAAAAAAA..",
    "AAAAKAAKAAAA",
    "AAUAAAAAAUAA",
    "..AAAAAAAA..",
    "..AAAAAAAA..",
    ".A..AAAA..A.",
    "....A..A....",
])

pic("cloud", "ХМАРКА", "weather", "common", [
    "....WWWW....",
    "..WWWWWWWW..",
    ".WWWWWWWWWW.",
    "WWWWKWWKWWWW",
    "WWWWWWWWWWWW",
    ".WXXXXXXXXW.",
])

pic("raincloud", "ДОЩОВА ХМАРКА", "weather", "uncommon", [
    "....GGGG....",
    "..GWWGGGGG..",
    ".GGGGGGGGGG.",
    "GGGGGGGGGGGG",
    ".GGGGGGGGGG.",
    "..B..B..B...",
    "...B..B..B..",
])

pic("rainbow", "ВЕСЕЛКА", "weather", "rare", [
    "....RRRRRR....",
    "..RRAAAAAARR..",
    ".RAAEEEEEEAAR.",
    "RAEEBBBBBBEEAR",
    "RAEB......BEAR",
    "RAEB......BEAR",
])

pic("snowflake", "СНІЖИНКА", "weather", "uncommon", [
    ".....BB.....",
    "..X..XX..X..",
    "...X.XX.X...",
    "....XXXX....",
    "BXXXXWWXXXXB",
    "....XXXX....",
    "...X.XX.X...",
    "..X..XX..X..",
    ".....BB.....",
], outline="n")

pic("lightning", "БЛИСКАВКА", "weather", "common", [
    "....LLLL",
    "...LLLL.",
    "..LLLL..",
    ".LAAALL.",
    "...LLL..",
    "..LLL...",
    ".LLL....",
    "LL......",
])

pic("tornado", "ТОРНАДО", "weather", "rare", [
    "GGSSGGGGGGSSGG",
    ".GGGGGGGGGGGG.",
    "..GGWWGGGGGG..",
    "...GGGGGGGG...",
    "....GGGGGG....",
    ".....GGGG.....",
    "....GGGG......",
    "...GGGG.......",
    "....GGG.......",
    "....GG........",
    ".....G........",
])

# ─────────────────────────── ПРЕДМЕТИ ───────────────────────────

pic("heart", "СЕРДЕЧКО", "things", "common", [
    ".RRR..RRR.",
    "RRRRRRRRRR",
    "RWRRRRRRRR",
    "RRRRRRRRRR",
    ".RRRRRRRR.",
    "..RRRRRR..",
    "...RRRR...",
    "....RR....",
])

pic("key", "КЛЮЧИК", "things", "common", [
    "AAAA......",
    "A..AAAAAAA",
    "A..AAAAAAA",
    "AAAA...D.D",
])

pic("gift", "ПОДАРУНОК", "things", "uncommon", [
    "...RR.RR....",
    "..R.RAR.R...",
    "BBBBBRRBBBBB",
    "BBBBBRRBBBBB",
    "BBBBBRRBBBBB",
    "BBBBBRRBBBBB",
    "BBBBBRRBBBBB",
    "BBBBBRRBBBBB",
])

pic("potion", "ЗІЛЛЯ", "things", "uncommon", [
    "....DDDD....",
    "....SSSS....",
    "....SSSS....",
    "...SSSSSS...",
    "..SSPPPPSS..",
    ".SPPPPPPPPS.",
    ".SPPSPPPPPS.",
    ".SPPPPPPPPS.",
    "..SPPPPPPS..",
    "...SSSSSS...",
])

pic("book", "КНИЖКА", "things", "common", [
    "BBBBBBBBBB",
    "BCCCCCCCBB",
    "BCCCCCCCBB",
    "BCCCCCCCBB",
    "BCCCCCCCBB",
    "BCCCCCCCBB",
    "BBBBBBBBBB",
])

pic("lamp", "ЛАМПОЧКА", "things", "common", [
    "...LLLL...",
    "..LLLLLL..",
    ".LLLLLLLL.",
    ".LLLLLLLL.",
    ".LLLLLLLL.",
    "..LLLLLL..",
    "...LLLL...",
    "...GGGG...",
    "...GGGG...",
    "....GG....",
])

pic("hourglass", "ПІСОЧНИЙ ГОДИННИК", "things", "rare", [
    "DDDDDDDDDD",
    ".WZZZZZZW.",
    ".ZZZZZZZZ.",
    "..ZZZZZZ..",
    "...ZZZZ...",
    "....ZZ....",
    "...Z..Z...",
    "..Z....Z..",
    ".Z.ZZZZ.Z.",
    ".ZZZZZZZZ.",
    "DDDDDDDDDD",
], outline="k")

pic("diamond", "ДІАМАНТ", "things", "epic", [
    "....YYYYYYYY....",
    "..YYXXYYYYXXYY..",
    ".YYXXXXYYXXXXYY.",
    "YYYYYYYYYYYYYYYY",
    ".YYYYYYYYYYYYYY.",
    "..YYYWWYYYYYYY..",
    "...YYYWWYYYYY...",
    "....YYBBBBYY....",
    ".....YBBBBY.....",
    "......YYYY......",
    ".......YY.......",
], outline="n")

pic("crystal", "КРИСТАЛ", "things", "legendary", [
    "........P.........",
    ".......PPP........",
    "......PPFPP.......",
    ".....PPFFFPP......",
    "....PPFFFFFPP.....",
    "...PPFIWWIFFPP....",
    "...PPFIWWIFFPP....",
    "..PPFFFFFFFFFPP...",
    "..PPFFFFFFFFFPP...",
    "..PPPFFFFFFFPPP...",
    "...PPPFFFFFPPP....",
    "....PPPFFFPPP.....",
    ".....PPPFPPP......",
    "......PPPPP.......",
    ".....SSSSSSS......",
    "....SSSSSSSSS.....",
], outline="n")

pic("gem_cosmic", "СЕРЦЕ ГАЛАКТИКИ", "things", "cosmic", [
    "......PPPPPP......",
    "....PPFFFFFFPP....",
    "...PFFYYYYYYFFP...",
    "..PFYYWWWWWWYYFP..",
    ".PFYWWAAAAAAWWYFP.",
    ".PFYWAARRRRAAWYFP.",
    "PFYWAARRnnRRAAWYFP",
    "PFYWAARnnnnRAAWYFP",
    "PFYWAARnnnnRAAWYFP",
    "PFYWAARRnnRRAAWYFP",
    ".PFYWAARRRRAAWYFP.",
    ".PFYWWAAAAAAWWYFP.",
    "..PFYYWWWWWWYYFP..",
    "...PFFYYYYYYFFP...",
    "....PPFFFFFFPP....",
    "......PPPPPP......",
], outline="n")

# ─────────────────────────── КОМАШКИ ───────────────────────────

pic("bee", "БДЖІЛКА", "bugs", "common", [
    "..XX..XX..",
    ".XXXXXXXX.",
    "AAKAAAAAAA",
    "AAAAAAAAAA",
    "AAAAKKKKAA",
    "AAAAAAAAAA",
    ".AAAKKKKA.",
    "..AAAAAA..",
    "....K.....",
])

pic("ladybug", "СОНЕЧКО-ЖУЧОК", "bugs", "common", [
    "...KWWK...",
    "..RRRRRR..",
    ".RRKRRKRR.",
    "RRRRKKRRRR",
    "RKRRKKRRKR",
    "RRRRKKRRRR",
    ".RRKKKKRR.",
    "..RRRRRR..",
])

pic("butterfly", "МЕТЕЛИК", "bugs", "uncommon", [
    "PPP..K..PPP",
    "PPPP.K.PPPP",
    "PIPPPKPPPIP",
    "PPAPPKPPAPP",
    ".PPPPKPPPP.",
    ".PPIPKPIPP.",
    "..PPPKPPP..",
    "...PPKPP...",
])

pic("snail", "РАВЛИК", "bugs", "uncommon", [
    "........E.E",
    "..OOOO..EEE",
    ".OOOOOO.EE.",
    "OOOAAOOOEE.",
    "OOAAAAOOEE.",
    "OOOAAOOEEE.",
    "EEEEEEEEEE.",
    ".EEEEEEEE..",
])

pic("beetle", "ЖУК-ОЛЕНЬ", "bugs", "rare", [
    ".G..........G.",
    "..G........G..",
    "...GGNNNNGG...",
    "....NWWNNN....",
    "...NNNNNNNN...",
    "..NNDDNNDDNN..",
    "..NNNNNNNNNN..",
    "G.NNNNNNNNNN.G",
    ".GNNNNNNNNNNG.",
    "..NNNNNNNNNN..",
    "G..NNNNNNNN..G",
    ".G..NNNNNN..G.",
])

# ─────────────────────────── додаткові тварини (звичайні) ───────────────────────────

pic("hamster", "ХОМ'ЯЧОК", "animals", "common", [
    ".DD......DD.",
    "DDDDDDDDDDDD",
    "DDKDDDDDDKDD",
    "DDDDDDDDDDDD",
    "DDDDDIIDDDDD",
    "DDDDDDDDDDDD",
    ".DDDDDDDDDD.",
    "..DDDDDDDD..",
])

pic("koala", "КОАЛА", "animals", "uncommon", [
    "GG........GG",
    "GWWG....GWWG",
    "GGGGGGGGGGGG",
    "GGGGGGGGGGGG",
    ".GGKGGGGKGG.",
    ".GUGGKKGGUG.",
    ".GGGGKKGGGG.",
    "..GGGGGGGG..",
    "...GGGGGG...",
])

pic("sheep", "ОВЕЧКА", "animals", "common", [
    ".WW.WWWW.WW.",
    "WWWWWWWWWWWW",
    "WWWWWWWWWWWW",
    "WW.GGGGGG.WW",
    "WWGGKGGKGGWW",
    "WWGUGGGGUGWW",
    ".WWGGGGGGWW.",
    "..GG.GG.GG..",
])

pic("bat", "КАЖАНЧИК", "animals", "uncommon", [
    "I....PP....I",
    "PP...PP...PP",
    "PPP.PPPP.PPP",
    "PPPPWnWnPPPP",
    "PPPPPPPPPPPP",
    ".PP.PWWP.PP.",
    "....PPPP....",
], outline="n")

pic("dragon", "ДРАКОНЧИК", "animals", "legendary", [
    "......O....O......",
    "......EEEEEE......",
    ".....EEKEEEEEE....",
    "....EEEEEEEEEE....",
    "...RREEEWWWWEE....",
    "..RRREEEEEEE......",
    ".RRRREEEEEE.......",
    "RRRRREEEEEEE......",
    "RRRRREEEAAEEEE....",
    ".RRRREEEAAEEEEE...",
    "..RRREEEAAEEEEEE..",
    "...RREEEAAEEEEEEE.",
    "....EEEEEEEEEEEEEE",
    "....EEEEEEEE...EEE",
    ".....EEE.EEE......",
    "....DDD..DDD......",
])

pic("unicorn", "ЄДИНОРІГ", "animals", "epic", [
    "......A.........",
    "......AA........",
    "....WWWWW.......",
    "...WWWKWWWW.....",
    "..WWWWWWWWWWW...",
    ".PIPWWWWWWWWWWW.",
    "PIPIWWWWWWWWWWWW",
    ".PIPWWWWWWWWWWW.",
    "..PIWWWWWWWWWW..",
    "...WWWWWWWWWW...",
    "...WWW....WWW...",
    "...WWW....WWW...",
    "...PPP....PPP...",
])

# ─────────────────────────── ще космос і предмети для повноти тем ───────────────────────────

pic("alien", "ІНОПЛАНЕТЯНИН", "space", "uncommon", [
    "...EEEEEE...",
    "..EMMEEEEE..",
    ".EEWKEEEWKE.",
    ".EEKKEEEKKE.",
    ".EEEEEEEEEE.",
    "..EEEKKEEE..",
    "...EEEEEE...",
    "....E..E....",
    "...EE..EE...",
])

pic("telescope", "ТЕЛЕСКОП", "space", "common", [
    ".........SS",
    ".......SSSS",
    ".....SSSSS.",
    "...SSSSS...",
    ".SSSSS.....",
    "SSSS.......",
    "SS.GG......",
    "..GGGG.....",
    ".GG..GG....",
])

pic("robot", "РОБОТИК", "things", "uncommon", [
    ".....A......",
    "....SSSS....",
    "...SSSSSS...",
    "...SYKSYKS..",
    "...SSSSSS...",
    "...SSKKKS...",
    "SS.SSSSSS.SS",
    "SSSSSSSSSSSS",
    "...SSSSSS...",
    "...SS..SS...",
])


def main():
    seen = set()
    written = 0
    for p in PICTURES:
        if p["id"] in seen:
            raise SystemExit(f"дубль id {p['id']}")
        seen.add(p["id"])
        text = build(p)
        folder = os.path.join(OUT, p["theme"])
        os.makedirs(folder, exist_ok=True)
        with open(os.path.join(folder, p["id"] + ".txt"), "w", encoding="utf-8") as f:
            f.write(text)
        written += 1
    if ERRORS:
        print("\n".join(ERRORS))
        raise SystemExit(f"{len(ERRORS)} помилок")
    by_rarity = {}
    by_theme = {}
    for p in PICTURES:
        by_rarity[p["rarity"]] = by_rarity.get(p["rarity"], 0) + 1
        by_theme[p["theme"]] = by_theme.get(p["theme"], 0) + 1
    print(f"записано {written} картинок у {OUT}")
    print("рідкість:", by_rarity)
    print("теми:", by_theme)


if __name__ == "__main__":
    main()
