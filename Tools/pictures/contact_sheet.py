#!/usr/bin/env python3
"""
Контактний аркуш бібліотеки картинок: усі Assets/_Pictures/**/*.txt однією PNG-сіткою,
кожна картинка двічі — готова й у стані «24 з 36» (дві третини кроків, як бачить її
гравець посеред забігу). Стиль — W4DarkCanvas (docs/StyleRef/W4DarkCanvas/): темне полотно
з плетінням, світлі контури незафарбованих зон, м'яке гало фарби, рівні квадратні пікселі
зі світлом зверху й тінню знизу. Та сама математика, що в шейдері InkFlow/DarkCanvas і
Core PictureCanvas (контур проявляється разом із сусідньою фарбою). Рамка плитки — колір рідкості.
Чистий Python (zlib), без залежностей. Вихід: docs/pictures-contact-sheet.png
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from pngio import write_png  # noqa: E402
from raster import HEX  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Assets", "_Pictures")
OUT = os.path.join(ROOT, "docs", "pictures-contact-sheet.png")

FRAME = {"common": "#B8BCC8", "uncommon": "#4ED37A", "rare": "#5AA7FF", "epic": "#AE7BFF", "legendary": "#FFC145", "cosmic": "#F075E6"}
ORDER = ["common", "uncommon", "rare", "epic", "legendary", "cosmic"]
BG = (20, 17, 42)

# W4DarkCanvas — ті самі числа, що токени DesignSystem (docs/StyleRef/W4DarkCanvas/SPEC.md).
CANVAS = (0x40 / 255, 0x3C / 255, 0x78 / 255)
LINE = (0xDF / 255, 0xE0 / 255, 0xF5 / 255)
MARGIN = 2            # порожнє полотно навколо арту, пікселі арту
WEAVE_ALPHA = 0.045
WEAVE_ON_PAINT = 0.6
LIGHT_TOP = 0.16
SHADE_BOTTOM = 0.16
HALO_SIGMA = 1.4      # пікселі арту
HALO_ALPHA = 0.45
HALO_GAIN = 1.6
SKETCH_ALPHA = 0.35
BORDER_ALPHA = 0.35


def hex_rgb(h):
    if isinstance(h, tuple):
        return h
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)


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


def gaussian_kernel(sigma):
    r = int(math.ceil(sigma * 3))
    k = [math.exp(-(i * i) / (2 * sigma * sigma)) for i in range(-r, r + 1)]
    total = sum(k)
    return r, [v / total for v in k]


def coverage_of(rows, colors, fam_of, outline, painted):
    """Покриття пікселів арту: заливка — чи зафарбована; контур — разом із сусідньою фарбою (Core PictureCanvas.Coverage)."""
    h, w = len(rows), max(len(r) for r in rows)
    fill = [[False] * w for _ in range(h)]
    cov = [[0.0] * w for _ in range(h)]
    complete = True
    for y in range(h):
        for x in range(w):
            ch = rows[y][x] if x < len(rows[y]) else "."
            if ch != "." and ch != outline:
                fill[y][x] = True
                v = 1.0 if painted is None or (x, y) in painted else 0.0
                cov[y][x] = v
                if v < 1.0:
                    complete = False

    def is_outline(x, y):
        ch = rows[y][x] if x < len(rows[y]) else "."
        return ch != "." and not fill[y][x]

    for pass_ in range(2):
        prev = [r[:] for r in cov]
        for y in range(h):
            for x in range(w):
                if not is_outline(x, y):
                    continue
                if complete:
                    cov[y][x] = 1.0
                    continue
                best = cov[y][x]
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        nx, ny = x + dx, y + dy
                        if (dx or dy) and 0 <= nx < w and 0 <= ny < h:
                            if pass_ == 0 and not fill[ny][nx]:
                                continue
                            best = max(best, prev[ny][nx])
                cov[y][x] = best
    return cov


def halo_of(rows, colors, cov, w, h):
    """Гало на сітці полотна (арт + поле): розмите премультипліковане покриття (Core PictureCanvas.Halo)."""
    cw, ch = w + 2 * MARGIN, h + 2 * MARGIN
    grid = [[(0.0, 0.0, 0.0, 0.0)] * cw for _ in range(ch)]
    for y in range(h):
        for x in range(w):
            a = cov[y][x]
            if a <= 0:
                continue
            c = hex_rgb(HEX[colors[rows[y][x]]])
            grid[y + MARGIN][x + MARGIN] = (c[0] / 255 * a, c[1] / 255 * a, c[2] / 255 * a, a)
    r, kernel = gaussian_kernel(HALO_SIGMA)

    def blur(src, horizontal):
        out = [[None] * cw for _ in range(ch)]
        for y in range(ch):
            for x in range(cw):
                acc = [0.0, 0.0, 0.0, 0.0]
                for t in range(-r, r + 1):
                    sx, sy = (x + t, y) if horizontal else (x, y + t)
                    if 0 <= sx < cw and 0 <= sy < ch:
                        v = src[sy][sx]
                        wgt = kernel[t + r]
                        for i in range(4):
                            acc[i] += v[i] * wgt
                out[y][x] = tuple(acc)
        return out

    grid = blur(blur(grid, True), False)
    halo = [[None] * cw for _ in range(ch)]
    for y in range(ch):
        for x in range(cw):
            cr, cg, cb, a = grid[y][x]
            halo[y][x] = (0, 0, 0, 0) if a <= 1e-6 else (cr / a, cg / a, cb / a, min(HALO_ALPHA, a * HALO_ALPHA * HALO_GAIN))
    return halo


def main():
    files = []
    for folder, _, names in os.walk(SRC):
        for n in sorted(names):
            if n.endswith(".txt"):
                files.append(os.path.join(folder, n))
    pics = [parse(f) for f in files]
    pics.sort(key=lambda p: (ORDER.index(p[0].get("rarity", "common")), p[0].get("theme", ""), p[0].get("id", "")))

    cell = 6                               # px на піксель арту на аркуші
    tile = (42 + 2 * MARGIN) * cell + 8    # плитка: до 42×42 пікселів арту + поле полотна + рамка
    pad = 10
    cols = 6           # три картинки в ряд, кожна — готова + 24/36
    width = cols * (tile + pad) + pad
    rows_n = (len(pics) * 2 + cols - 1) // cols
    height = rows_n * (tile + pad) + pad
    pixels = [BG] * (width * height)
    weave_pitch = max(2, int(round(cell * 0.25)))

    def put(x, y, c):
        if 0 <= x < width and 0 <= y < height:
            pixels[y * width + x] = c

    def draw(slot, meta, colors, fam_of, rows, painted):
        cx = pad + (slot % cols) * (tile + pad)
        cy = pad + (slot // cols) * (tile + pad)
        frame = hex_rgb(FRAME.get(meta.get("rarity", "common"), "#FFFFFF"))[:3]
        for x in range(tile):
            for t in range(3):
                put(cx + x, cy + t, frame)
                put(cx + x, cy + tile - 1 - t, frame)
        for y in range(tile):
            for t in range(3):
                put(cx + t, cy + y, frame)
                put(cx + tile - 1 - t, cy + y, frame)

        outline = meta["outline"]
        h, w = len(rows), max(len(r) for r in rows)
        cov = coverage_of(rows, colors, fam_of, outline, painted)
        halo = halo_of(rows, colors, cov, w, h)

        def zone(x, y):
            if x < 0 or y < 0 or x >= w or y >= h:
                return None
            ch_ = rows[y][x] if x < len(rows[y]) else "."
            if ch_ == ".":
                return None
            return "outline" if ch_ == outline else fam_of.get(ch_, 0)

        cw, chh = (w + 2 * MARGIN) * cell, (h + 2 * MARGIN) * cell
        ox = cx + (tile - cw) // 2
        oy = cy + (tile - chh) // 2
        radius = cell  # кут полотна — 1 піксель арту
        for Y in range(chh):
            ay = (Y + 0.5) / cell - MARGIN
            cyl = int(math.floor(ay))
            fy = ay - cyl
            t_art = min(max(ay / h, 0.0), 1.0)
            light = LIGHT_TOP * (1 - t_art / 0.5) if t_art < 0.5 else 0.0
            shade = SHADE_BOTTOM * (t_art - 0.5) / 0.5 if t_art > 0.5 else 0.0
            row_on = 1.0 if (Y % weave_pitch) < weave_pitch / 2 else 0.0
            for X in range(cw):
                # кути полотна: за дугою — тло аркуша
                dx = max(radius - X - 0.5, X + 0.5 - (cw - radius), 0)
                dy = max(radius - Y - 0.5, Y + 0.5 - (chh - radius), 0)
                if dx > 0 and dy > 0 and dx * dx + dy * dy > radius * radius:
                    continue
                col_on = (1.0 if (X % weave_pitch) < weave_pitch / 2 else 0.0) * 0.7
                wv = min(row_on + col_on, 1.7) / 1.7
                col = [min(1.0, CANVAS[i] + WEAVE_ALPHA * wv) for i in range(3)]

                ax = (X + 0.5) / cell - MARGIN
                cxl = int(math.floor(ax))
                fx = ax - cxl
                here = zone(cxl, cyl)
                # світлі контури між зонами й по силуету: 1 px на межі клітинки
                line = False
                if X % cell == 0 and zone(cxl - 1, cyl) != here:
                    line = True
                if Y % cell == 0 and zone(cxl, cyl - 1) != here:
                    line = True
                if line:
                    col = [col[i] * (1 - SKETCH_ALPHA) + LINE[i] * SKETCH_ALPHA for i in range(3)]

                # гало — білінійно з сітки полотна
                gx = (X + 0.5) / cell - 0.5
                gy = (Y + 0.5) / cell - 0.5
                x0, y0 = int(math.floor(gx)), int(math.floor(gy))
                tx, ty = gx - x0, gy - y0
                acc = [0.0, 0.0, 0.0, 0.0]
                for (sx, sy, wgt) in ((x0, y0, (1 - tx) * (1 - ty)), (x0 + 1, y0, tx * (1 - ty)),
                                      (x0, y0 + 1, (1 - tx) * ty), (x0 + 1, y0 + 1, tx * ty)):
                    sx = min(max(sx, 0), w + 2 * MARGIN - 1)
                    sy = min(max(sy, 0), h + 2 * MARGIN - 1)
                    v = halo[sy][sx]
                    for i in range(4):
                        acc[i] += v[i] * wgt
                if acc[3] > 0:
                    col = [col[i] * (1 - acc[3]) + acc[i] * acc[3] for i in range(3)]

                # фарба: піксель заповнює клітинку цілком
                if 0 <= cxl < w and 0 <= cyl < h and cov[cyl][cxl] >= 1.0:
                    c = hex_rgb(HEX[colors[rows[cyl][cxl]]])
                    paint = [(c[i] / 255) * (1 - light) + light for i in range(3)]
                    paint = [min(1.0, v * (1 - shade) + WEAVE_ALPHA * WEAVE_ON_PAINT * wv) for v in paint]
                    col = paint

                # тонка темна рамка полотна
                if X == 0 or Y == 0 or X == cw - 1 or Y == chh - 1:
                    col = [v * (1 - BORDER_ALPHA) for v in col]
                put(ox + X, oy + Y, tuple(int(round(min(max(v, 0.0), 1.0) * 255)) for v in col))

    for i, (meta, colors, fam_of, rows) in enumerate(pics):
        draw(i * 2, meta, colors, fam_of, rows, None)
        step = int(meta.get("step", "1"))
        steps = steps_order(rows, fam_of, meta["outline"], step)
        painted = set()
        for s_ in steps[: (len(steps) * 2) // 3]:
            painted.update(s_)
        draw(i * 2 + 1, meta, colors, fam_of, rows, painted)

    write_png(OUT, width, height, [bytes(v for px in pixels[y * width:(y + 1) * width] for v in px + (255,)) for y in range(height)])
    print(f"{len(pics)} картинок × 2 → {OUT} ({width}×{height})")


if __name__ == "__main__":
    main()
