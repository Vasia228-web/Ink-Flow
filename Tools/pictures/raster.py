"""
Растеризатор авторських картинок Ink Flow (~32 px, K1Candy-сумісні тони).

Картинка описується фігурами (еліпс, прямокутник, многокутник, лінія, крапка) з родиною
на кожну. Скрипт кладе основний тон родини, сам домальовує світлу грань (зліва зверху)
і тінь (справа знизу), іскри, зовнішній контур; обрізає порожні поля; добирає розмір
кроку під ціль рідкості; перевіряє правила формату (docs/pictures-format.md) і пише
текстовий файл, який читає PixelPicture.Parse.

Координати: x вправо, y вниз, у пікселях картинки; фігури малюються в порядку виклику
(пізніша поверх). Літери кольорів у файлі призначаються автоматично.
"""
import math
import os

# ── Майстер-палітра (дзеркало Core/Config/MasterPalette.cs) ──

FILLS = {
    "white": 4, "cream": 5, "silver": 6, "slate": 7, "red": 8, "orange": 9, "yellow": 10,
    "lemon": 11, "lime": 12, "green": 13, "teal": 14, "cyan": 15, "blue": 16, "periwinkle": 17,
    "violet": 18, "magenta": 19, "pink": 20, "peach": 21, "caramel": 22, "bark": 23, "sand": 24,
    "ice": 25, "mint": 26, "blush": 27, "olive": 28, "raspberry": 29, "seafoam": 30,
}
OUTLINES = {"ink": 1, "cocoa": 2, "navy": 3}
FIRST_TONE = 31
WHITE = 4

HEX = {
    0: None, 1: "#1B1730", 2: "#3A2C2C", 3: "#26305A", 4: "#FFFFFF", 5: "#F3ECDD", 6: "#C8CCD8",
    7: "#8E93A8", 8: "#FF5E6E", 9: "#FF8A3D", 10: "#FFC145", 11: "#FFEE7A", 12: "#B6E24F", 13: "#4ED37A",
    14: "#2EC4A6", 15: "#5FE1E8", 16: "#5AA7FF", 17: "#7D7CFF", 18: "#AE7BFF", 19: "#F075E6", 20: "#FF9FCB",
    21: "#FFB98B", 22: "#DDA25F", 23: "#B07A4E", 24: "#E8D7B0", 25: "#9FDBFF", 26: "#C3F7D6", 27: "#FFC8D8",
    28: "#B8B534", 29: "#E2308F", 30: "#79E8A8",
    31: "#FFFFFF", 32: "#C9CCD8", 33: "#FFFBF4", 34: "#C7BFA6", 35: "#E7E9EF", 36: "#8A8DA0", 37: "#B4B8C6",
    38: "#55576F", 39: "#FFB2B9", 40: "#A8273C", 41: "#FFC7A2", 42: "#B4601A", 43: "#FFE1A6", 44: "#B98A18",
    45: "#FFF7BF", 46: "#BDB244", 47: "#E2FAAC", 48: "#6F9C24", 49: "#A3EABB", 50: "#268F55", 51: "#8ADACA",
    52: "#12857B", 53: "#B7FBFF", 54: "#2F8C9E", 55: "#B0D5FF", 56: "#2A5AB0", 57: "#C1C0FF", 58: "#4F42B0",
    59: "#D8C0FF", 60: "#6742B0", 61: "#FFC0FA", 62: "#A33EA1", 63: "#FFD1E6", 64: "#B05C88", 65: "#FFDDC7",
    66: "#B47F4E", 67: "#F4D5B2", 68: "#997232", 69: "#C5A890", 70: "#74522A", 71: "#FFF6E1", 72: "#A5996F",
    73: "#D1EEFF", 74: "#5C86AE", 75: "#E5FFEF", 76: "#79A991", 77: "#FFE5EC", 78: "#B07C8E", 79: "#CDCC87",
    80: "#767D18", 81: "#FA9BCE", 82: "#9C1262", 83: "#C4FFDD", 84: "#449F75",
}


def light_of(fill):
    tone = FIRST_TONE + 2 * (fill - 4)
    return fill if HEX[tone] == HEX[fill] else tone


def shadow_of(fill):
    return FIRST_TONE + 2 * (fill - 4) + 1


# Таблиця рідкості (дзеркало BalanceData): сітка файлу, тони, родини, кроки.
GRID = {"common": 34, "uncommon": 34, "rare": 34, "epic": 34, "legendary": 42, "cosmic": 42}
TONES = {"common": (10, 16), "uncommon": (10, 16), "rare": (11, 16), "epic": (12, 16), "legendary": (13, 18), "cosmic": (14, 18)}
FAMILIES = (4, 6)
STEPS = {"common": 60, "uncommon": 70, "rare": 85, "epic": 110, "legendary": 150, "cosmic": 240}
STEP_TOLERANCE = 0.35
MIN_SIDE = 12
MIN_LONG = 24

LETTERS = "ABCDEFGHIJLMNOPQRSTUVWXYZabcdefghijlmnopqrstuvwxyz0123456789"


class Pic:
    """Одна картинка: полотно w×h, список фігур, потім build() → текст файлу."""

    def __init__(self, pid, name, theme, rarity, w, h, outline="ink"):
        self.id, self.name, self.theme, self.rarity = pid, name, theme, rarity
        self.w, self.h = w, h
        self.outline = OUTLINES[outline]
        # на піксель: (tone, family, shape_id) або None; контур: (outline, 0, -1)
        self.px = [[None] * w for _ in range(h)]
        self.shapes = 0
        self.masks = {}      # shape_id → set((x,y))
        self.spec = {}       # shape_id → dict(family, tone, shade, spark)

    # ── фігури ──

    def _paint(self, cells, family, tone=None, shade=True, spark=False, outline=False):
        sid = self.shapes
        self.shapes += 1
        mask = set()
        if outline:
            fam, tn = 0, self.outline
        else:
            fam = FILLS[family]
            tn = {None: fam, "base": fam, "light": light_of(fam), "shadow": shadow_of(fam), "white": WHITE}.get(tone, tone)
        for (x, y) in cells:
            if 0 <= x < self.w and 0 <= y < self.h:
                self.px[y][x] = (tn, fam, sid)
                mask.add((x, y))
        self.masks[sid] = mask
        self.spec[sid] = dict(family=fam, tone=tn, shade=shade and not outline and tone is None, spark=spark, outline=outline)
        return sid

    def ellipse(self, cx, cy, rx, ry, family, **kw):
        cells = []
        for y in range(int(math.floor(cy - ry - 1)), int(math.ceil(cy + ry + 1)) + 1):
            for x in range(int(math.floor(cx - rx - 1)), int(math.ceil(cx + rx + 1)) + 1):
                dx = (x + 0.5 - cx) / max(rx, 0.01)
                dy = (y + 0.5 - cy) / max(ry, 0.01)
                if dx * dx + dy * dy <= 1.0:
                    cells.append((x, y))
        return self._paint(cells, family, **kw)

    def circle(self, cx, cy, r, family, **kw):
        return self.ellipse(cx, cy, r, r, family, **kw)

    def rect(self, x, y, w, h, family, r=0, **kw):
        cells = []
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                if r > 0:
                    # закруглені кути: точка має бути в межах прямокутника без кутів або в кутовому колі
                    ex = min(xx - x, x + w - 1 - xx)
                    ey = min(yy - y, y + h - 1 - yy)
                    if ex < r and ey < r:
                        dx, dy = r - 0.5 - ex, r - 0.5 - ey
                        if dx * dx + dy * dy > (r - 0.25) ** 2:
                            continue
                cells.append((xx, yy))
        return self._paint(cells, family, **kw)

    def poly(self, points, family, **kw):
        """Заповнений многокутник (парність), координати — кути пікселів."""
        cells = []
        ys = [p[1] for p in points]
        xs = [p[0] for p in points]
        for y in range(int(math.floor(min(ys))), int(math.ceil(max(ys))) + 1):
            cy = y + 0.5
            for x in range(int(math.floor(min(xs))), int(math.ceil(max(xs))) + 1):
                if _inside(points, x + 0.5, cy):
                    cells.append((x, y))
        return self._paint(cells, family, **kw)

    def line(self, x0, y0, x1, y1, family, width=1, **kw):
        cells = set()
        n = max(abs(x1 - x0), abs(y1 - y0), 1)
        for i in range(int(n) + 1):
            t = i / n
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            for dy in range(-(width // 2), width - width // 2):
                for dx in range(-(width // 2), width - width // 2):
                    cells.add((int(round(x)) + dx, int(round(y)) + dy))
        return self._paint(sorted(cells), family, **kw)

    def dot(self, x, y, family, **kw):
        kw.setdefault("shade", False)
        return self._paint([(x, y)], family, **kw)

    def dots(self, cells, family, **kw):
        kw.setdefault("shade", False)
        return self._paint(cells, family, **kw)

    def ink(self, cells):
        """Контурні пікселі всередині малюнка (зіниці, рот, шви) — видно від початку."""
        return self._paint(cells, None, outline=True)

    def ink_line(self, x0, y0, x1, y1, width=1):
        cells = set()
        n = max(abs(x1 - x0), abs(y1 - y0), 1)
        for i in range(int(n) + 1):
            t = i / n
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            for dy in range(-(width // 2), width - width // 2):
                for dx in range(-(width // 2), width - width // 2):
                    cells.add((int(round(x)) + dx, int(round(y)) + dy))
        return self._paint(sorted(cells), None, outline=True)

    def ink_ellipse(self, cx, cy, rx, ry):
        cells = []
        for y in range(int(math.floor(cy - ry - 1)), int(math.ceil(cy + ry + 1)) + 1):
            for x in range(int(math.floor(cx - rx - 1)), int(math.ceil(cx + rx + 1)) + 1):
                dx = (x + 0.5 - cx) / max(rx, 0.01)
                dy = (y + 0.5 - cy) / max(ry, 0.01)
                if dx * dx + dy * dy <= 1.0:
                    cells.append((x, y))
        return self._paint(cells, None, outline=True)

    # ── збірка ──

    def _top(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h and self.px[y][x] is not None:
            return self.px[y][x][2]
        return None

    def _shade(self):
        for sid, mask in self.masks.items():
            sp = self.spec[sid]
            if not sp["shade"] or sp["family"] == 0:
                continue
            fam = sp["family"]
            lt, sh = light_of(fam), shadow_of(fam)
            for (x, y) in mask:
                if self._top(x, y) != sid:
                    continue
                up = self._top(x, y - 1) != sid
                left = self._top(x - 1, y) != sid
                down = self._top(x, y + 1) != sid
                right = self._top(x + 1, y) != sid
                exposed_light = up or left
                exposed_dark = down or right
                if exposed_light and not exposed_dark:
                    self.px[y][x] = (lt, fam, sid)
                elif exposed_dark and not exposed_light:
                    self.px[y][x] = (sh, fam, sid)
            if sp["spark"]:
                xs = [c[0] for c in mask]
                ys = [c[1] for c in mask]
                cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
                rx, ry = (max(xs) - min(xs)) / 2, (max(ys) - min(ys)) / 2
                sx, sy = int(round(cx - rx * 0.45)), int(round(cy - ry * 0.5))
                for (x, y) in ((sx, sy), (sx + 1, sy)):
                    if (x, y) in mask and self._top(x, y) == sid:
                        self.px[y][x] = (WHITE, fam, sid)

    def _outline(self):
        grid = [[None] * (self.w + 2) for _ in range(self.h + 2)]
        for y in range(self.h):
            for x in range(self.w):
                grid[y + 1][x + 1] = self.px[y][x]
        for y in range(self.h + 2):
            for x in range(self.w + 2):
                if grid[y][x] is not None:
                    continue
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        ny, nx = y + dy, x + dx
                        if 0 <= ny < self.h + 2 and 0 <= nx < self.w + 2 and grid[ny][nx] is not None and grid[ny][nx][1] != 0:
                            grid[y][x] = (self.outline, 0, -1)
        return grid

    def build(self, errors):
        self._shade()
        grid = self._outline()
        # обрізати порожні поля
        rows = [r for r in grid if any(c is not None for c in r)]
        if not rows:
            errors.append(f"{self.id}: порожня картинка")
            return None
        left = min(next(i for i, c in enumerate(r) if c is not None) for r in rows if any(c is not None for c in r))
        right = max(max(i for i, c in enumerate(r) if c is not None) for r in rows)
        rows = [r[left:right + 1] for r in rows]
        h, w = len(rows), len(rows[0])

        # літери: контур → K/k/n; пари (тон, родина) → з пулу
        outline_letter = {1: "K", 2: "k", 3: "n"}[self.outline]
        pairs = {}
        fam_pixels = {}
        fill_pixels = 0
        for r in rows:
            for c in r:
                if c is None or c[1] == 0:
                    continue
                key = (c[0], c[1])
                pairs.setdefault(key, 0)
                pairs[key] += 1
                fam_pixels[c[1]] = fam_pixels.get(c[1], 0) + 1
                fill_pixels += 1
        families = sorted(fam_pixels, key=lambda f: -fam_pixels[f])
        letters = {}
        pool = iter(LETTERS)
        for fam in families:
            letters[(fam, fam)] = next(pool)  # основний тон — першим
        for key in sorted(pairs, key=lambda k: (families.index(k[1]), k[0] != k[1], k[0])):
            if key not in letters:
                letters[key] = next(pool)

        # правила
        size = GRID[self.rarity]
        if max(w, h) > size:
            errors.append(f"{self.id}: {w}x{h} більший за сітку {size} ({self.rarity})")
        if max(w, h) < MIN_LONG + 2 or min(w, h) < MIN_SIDE:
            errors.append(f"{self.id}: {w}x{h} замалий (довша ≥ {MIN_LONG + 2}, менша ≥ {MIN_SIDE})")
        tones = len({k[0] for k in pairs})
        lo, hi = TONES[self.rarity]
        if not (lo <= tones <= hi):
            errors.append(f"{self.id}: {tones} тонів, а {self.rarity} просить {lo}–{hi}")
        if not (FAMILIES[0] <= len(families) <= FAMILIES[1]):
            errors.append(f"{self.id}: {len(families)} родин, треба {FAMILIES[0]}–{FAMILIES[1]}: {families}")
        outline_count = sum(1 for r in rows for c in r if c is not None and c[1] == 0)
        if fill_pixels <= outline_count // 2:
            errors.append(f"{self.id}: контуру ({outline_count}) забагато проти заливки ({fill_pixels})")

        # сітка символів
        text_rows = []
        for r in rows:
            line = []
            for c in r:
                if c is None:
                    line.append(".")
                elif c[1] == 0:
                    line.append(outline_letter)
                else:
                    line.append(letters[(c[0], c[1])])
            text_rows.append("".join(line))

        # крок під ціль рідкості — перевіряємо тим самим алгоритмом, що й Core
        target = STEPS[self.rarity]
        step = max(1, round(fill_pixels / target))
        tone_of = {v: k[0] for k, v in letters.items()}
        fam_of = {v: k[1] for k, v in letters.items()}
        steps = count_steps(text_rows, fam_of, outline_letter, step)
        for _ in range(12):
            if steps > target * (1 + STEP_TOLERANCE * 0.6) and step < 64:
                step += 1
            elif steps < target * (1 - STEP_TOLERANCE * 0.6) and step > 1:
                step -= 1
            else:
                break
            steps = count_steps(text_rows, fam_of, outline_letter, step)
        if not (target * (1 - STEP_TOLERANCE) <= steps <= target * (1 + STEP_TOLERANCE)):
            errors.append(f"{self.id}: {steps} кроків (крок {step}, {fill_pixels} px), ціль {target} ± {int(STEP_TOLERANCE * 100)}%")

        colors = " ".join(f"{letters[k]}={k[0]}" for k in sorted(letters, key=lambda k: LETTERS.index(letters[k]) if letters[k] in LETTERS else -1))
        colors = f"{outline_letter}={self.outline} " + colors
        fam_lines = []
        for fam in families:
            head = letters[(fam, fam)]
            members = "".join(letters[k] for k in sorted(letters, key=lambda k: LETTERS.index(letters[k])) if k[1] == fam)
            fam_lines.append(f"{head}={members}")
        text = [
            f"id: {self.id}", f"name: {self.name}", f"theme: {self.theme}", f"rarity: {self.rarity}",
            f"step: {step}", f"colors: {colors}", f"outline: {outline_letter}", f"families: {' '.join(fam_lines)}", "grid:",
        ] + text_rows
        return "\n".join(text) + "\n", dict(w=w, h=h, tones=tones, families=len(families), steps=steps, step=step, fill=fill_pixels)


def _inside(points, x, y):
    inside = False
    n = len(points)
    for i in range(n):
        x0, y0 = points[i]
        x1, y1 = points[(i + 1) % n]
        if (y0 > y) != (y1 > y):
            xi = x0 + (y - y0) * (x1 - x0) / (y1 - y0)
            if x < xi:
                inside = not inside
    return inside


# ── Дзеркало PixelPicture.BuildRevealOrder + BuildSteps (Core) для добору кроку ──

def count_steps(rows, fam_of, outline_letter, step):
    h, w = len(rows), len(rows[0])
    fam = [[0] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            ch = rows[y][x]
            if ch != "." and ch != outline_letter:
                fam[y][x] = fam_of[ch]
    fill = [(x, y) for y in range(h) for x in range(w) if fam[y][x]]
    if not fill:
        return 0

    def neighbours(x, y):
        for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and fam[ny][nx]:
                yield nx, ny

    # порядок проявлення
    visited = set()
    order = []
    center = (w - 1) / 2
    seed = min(fill, key=lambda p: (h - 1 - p[1]) * 1000 + int(abs(p[0] - center) * 10))
    from collections import deque
    while len(order) < len(fill):
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

    # області
    region = {}
    rid = 0
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

    open_ = {}
    steps = 0
    last_region = -1
    for p in order:
        if region[p] != last_region:
            open_ = {}
            last_region = region[p]
        f = fam[p[1]][p[0]]
        if f not in open_:
            open_[f] = 0
            steps += 1
        open_[f] += 1
        if open_[f] >= step:
            del open_[f]
    return steps


def write_all(pictures, out_root):
    errors = []
    report = []
    written = 0
    for p in pictures:
        result = p.build(errors)
        if result is None:
            continue
        text, info = result
        folder = os.path.join(out_root, p.theme)
        os.makedirs(folder, exist_ok=True)
        with open(os.path.join(folder, f"{p.id}.txt"), "w", encoding="utf-8") as f:
            f.write(text)
        written += 1
        report.append((p.rarity, p.theme, p.id, info))
    return written, errors, report
