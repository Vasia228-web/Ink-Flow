#!/usr/bin/env python3
"""
Контактний аркуш бібліотеки картинок: усі Assets/_Pictures/**/*.txt однією PNG-сіткою,
кожна картинка двічі — готова й у стані «24 з 36» (дві третини кроків, як бачить її
гравець посеред забігу: контур і намальовані кроки, решта — ескіз). Рамка — колір рідкості.
Чистий Python (zlib), без залежностей. Вихід: docs/pictures-contact-sheet.png
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from pngio import write_png  # noqa: E402
from raster import HEX, count_steps  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Assets", "_Pictures")
OUT = os.path.join(ROOT, "docs", "pictures-contact-sheet.png")

FRAME = {"common": "#B8BCC8", "uncommon": "#4ED37A", "rare": "#5AA7FF", "epic": "#AE7BFF", "legendary": "#FFC145", "cosmic": "#F075E6"}
ORDER = ["common", "uncommon", "rare", "epic", "legendary", "cosmic"]
BG = (20, 17, 42, 255)
PAPER = (236, 226, 204, 255)
SKETCH = (93, 86, 112, 255)


def hex_rgb(h):
    if isinstance(h, tuple):
        return h
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)


def blend(a, b, t):
    return tuple(int(round(a[i] * (1 - t) + b[i] * t)) for i in range(3)) + (255,)


def parse(path):
    meta, rows, in_grid = {}, [], False
    with open(path, encoding="utf-8") as f:
        for raw in f:
            line = raw.rstrip("\n")
            if in_grid:
                if line.strip() and not line.startswith("#"):
                    rows.append(line.strip())
                continue
            t = line.strip()
            if not t or t.startswith("#"):
                continue
            k, v = t.split(":", 1)
            k, v = k.strip(), v.strip()
            if k == "grid":
                in_grid = True
            else:
                meta[k] = v
    colors = {}
    for pair in meta.get("colors", "").split():
        colors[pair[0]] = int(pair[2:])
    fam_of = {}
    for pair in meta.get("families", "").split():
        head, members = pair.split("=")
        for ch in members:
            fam_of[ch] = colors[head]
    for ch in colors:
        fam_of.setdefault(ch, colors[ch])
    return meta, colors, fam_of, rows


def steps_order(rows, fam_of, outline, step):
    """Порядок кроків (список списків пікселів) — дзеркало PixelPicture.BuildSteps."""
    from collections import deque
    h, w = len(rows), len(rows[0])
    fam = [[0] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            ch = rows[y][x]
            if ch != "." and ch != outline:
                fam[y][x] = fam_of[ch]
    fill = [(x, y) for y in range(h) for x in range(w) if fam[y][x]]

    def neighbours(x, y):
        for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and fam[ny][nx]:
                yield nx, ny

    visited, order = set(), []
    center = (w - 1) / 2
    seed = min(fill, key=lambda p: (h - 1 - p[1]) * 1000 + int(abs(p[0] - center) * 10)) if fill else None
    while fill and len(order) < len(fill):
        if order:
            best, bd = None, 10 ** 9
            for p in fill:
                if p in visited:
                    continue
                for q in order:
                    d = abs(p[0] - q[0]) + abs(p[1] - q[1])
                    if d < bd:
                        bd, best = d, p
            seed = best
        visited.add(seed)
        dq = deque([seed])
        while dq:
            p = dq.popleft()
            order.append(p)
            for q in neighbours(*p):
                if q not in visited:
                    visited.add(q)
                    dq.append(q)
    region, rid = {}, 0
    for p in fill:
        if p in region:
            continue
        region[p] = rid
        dq = deque([p])
        while dq:
            c = dq.popleft()
            for q in neighbours(*c):
                if q not in region:
                    region[q] = rid
                    dq.append(q)
        rid += 1
    steps, open_, last = [], {}, -1
    for p in order:
        if region[p] != last:
            open_, last = {}, region[p]
        f = fam[p[1]][p[0]]
        if f not in open_:
            open_[f] = len(steps)
            steps.append([])
        steps[open_[f]].append(p)
        if len(steps[open_[f]]) >= step:
            del open_[f]
    return steps


def main():
    files = []
    for folder, _, names in os.walk(SRC):
        for n in sorted(names):
            if n.endswith(".txt"):
                files.append(os.path.join(folder, n))
    pics = [parse(f) for f in files]
    pics.sort(key=lambda p: (ORDER.index(p[0].get("rarity", "common")), p[0].get("theme", ""), p[0].get("id", "")))

    cell = 6           # px на піксель картинки на аркуші
    tile = 42 * cell   # плитка: до 42×42 пікселів
    pad = 10
    cols = 6           # три картинки в ряд, кожна — готова + 24/36
    width = cols * (tile + pad) + pad
    rows_n = (len(pics) * 2 + cols - 1) // cols
    height = rows_n * (tile + pad) + pad
    pixels = [BG] * (width * height)

    def put(x, y, c):
        if 0 <= x < width and 0 <= y < height:
            pixels[y * width + x] = c

    def draw(slot, meta, colors, fam_of, rows, painted):
        cx = pad + (slot % cols) * (tile + pad)
        cy = pad + (slot // cols) * (tile + pad)
        frame = hex_rgb(FRAME.get(meta.get("rarity", "common"), "#FFFFFF"))
        for x in range(tile):
            for t in range(3):
                put(cx + x, cy + t, frame)
                put(cx + x, cy + tile - 1 - t, frame)
        for y in range(tile):
            for t in range(3):
                put(cx + t, cy + y, frame)
                put(cx + tile - 1 - t, cy + y, frame)
        for y in range(4, tile - 4):
            for x in range(4, tile - 4):
                put(cx + x, cy + y, PAPER)
        h, w = len(rows), max(len(r) for r in rows)
        scale = max(1, min(cell, (tile - 12) // max(w, h)))
        ox = cx + (tile - w * scale) // 2
        oy = cy + (tile - h * scale) // 2
        outline = meta["outline"]
        for y, r in enumerate(rows):
            for x, ch in enumerate(r):
                if ch == ".":
                    continue
                is_outline = ch == outline
                if is_outline or painted is None or (x, y) in painted:
                    c = hex_rgb(HEX[colors[ch]])
                    c = blend(PAPER, c, 0.86)
                else:
                    # незафарбовано: олівцевий ескіз — лише межі родин і силуету (тони однієї родини не ріжуть)
                    fam = fam_of.get(ch, 0)
                    edge = False
                    for dx, dy in ((1, 0), (0, 1), (-1, 0), (0, -1)):
                        nx, ny = x + dx, y + dy
                        other = rows[ny][nx] if (0 <= ny < h and 0 <= nx < w) else "."
                        other_fam = 0 if other == "." or other == outline else fam_of.get(other, 0)
                        if other_fam != fam:
                            edge = True
                            break
                    c = blend(PAPER, SKETCH, 0.4) if edge else PAPER
                for dy in range(scale):
                    for dx in range(scale):
                        put(ox + x * scale + dx, oy + y * scale + dy, c)

    for i, (meta, colors, fam_of, rows) in enumerate(pics):
        draw(i * 2, meta, colors, fam_of, rows, None)
        step = int(meta.get("step", "1"))
        steps = steps_order(rows, fam_of, meta["outline"], step)
        painted = set()
        for s in steps[: (len(steps) * 2) // 3]:
            painted.update(s)
        draw(i * 2 + 1, meta, colors, fam_of, rows, painted)

    write_png(OUT, width, height, [bytes(v for px in pixels[y * width:(y + 1) * width] for v in px) for y in range(height)])
    print(f"{len(pics)} картинок × 2 → {OUT} ({width}×{height})")


if __name__ == "__main__":
    main()
