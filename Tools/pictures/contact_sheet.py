#!/usr/bin/env python3
"""
Контактний аркуш бібліотеки картинок: усі Assets/_Pictures/**/*.txt однією PNG-сіткою
з рамками рідкості, для перегляду оком. Чистий Python (zlib), без залежностей.
Вихід: docs/pictures-contact-sheet.png
"""
import os
import struct
import zlib

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Assets", "_Pictures")
OUT = os.path.join(ROOT, "docs", "pictures-contact-sheet.png")

PALETTE = {
    0: (0, 0, 0, 0), 1: "#1B1730", 2: "#3A2C2C", 3: "#26305A", 4: "#FFFFFF", 5: "#F3ECDD", 6: "#C8CCD8",
    7: "#8E93A8", 8: "#FF5E6E", 9: "#FF8A3D", 10: "#FFC145", 11: "#FFEE7A", 12: "#B6E24F", 13: "#4ED37A",
    14: "#2EC4A6", 15: "#5FE1E8", 16: "#5AA7FF", 17: "#7D7CFF", 18: "#AE7BFF", 19: "#F075E6", 20: "#FF9FCB",
    21: "#FFB98B", 22: "#DDA25F", 23: "#B07A4E", 24: "#E8D7B0", 25: "#9FDBFF", 26: "#C3F7D6", 27: "#FFC8D8",
    28: "#B8B534", 29: "#E2308F", 30: "#79E8A8",
}
FRAME = {"common": "#B8BCC8", "uncommon": "#4ED37A", "rare": "#5AA7FF", "epic": "#AE7BFF", "legendary": "#FFC145", "cosmic": "#F075E6"}
ORDER = ["common", "uncommon", "rare", "epic", "legendary", "cosmic"]
BG = (20, 17, 42, 255)


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
    return meta, colors, rows


def write_png(path, width, height, pixels):
    raw = bytearray()
    for y in range(height):
        raw.append(0)
        for x in range(width):
            raw.extend(pixels[y * width + x])
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def main():
    files = []
    for folder, _, names in os.walk(SRC):
        for n in sorted(names):
            if n.endswith(".txt"):
                files.append(os.path.join(folder, n))
    pics = [parse(f) for f in files]
    pics.sort(key=lambda p: (ORDER.index(p[0].get("rarity", "common")), p[0].get("theme", ""), p[0].get("id", "")))

    cell = 24          # px на піксель картинки на аркуші
    tile = 22 * cell   # плитка: до 22×22 пікселів
    pad = 12
    cols = 8
    rows_n = (len(pics) + cols - 1) // cols
    width = cols * (tile + pad) + pad
    height = rows_n * (tile + pad) + pad
    pixels = [BG] * (width * height)

    def put(x, y, c):
        if 0 <= x < width and 0 <= y < height:
            pixels[y * width + x] = c

    for i, (meta, colors, rows) in enumerate(pics):
        cx = pad + (i % cols) * (tile + pad)
        cy = pad + (i // cols) * (tile + pad)
        frame = hex_rgb(FRAME.get(meta.get("rarity", "common"), "#FFFFFF"))
        for x in range(tile):
            for t in range(3):
                put(cx + x, cy + t, frame); put(cx + x, cy + tile - 1 - t, frame)
        for y in range(tile):
            for t in range(3):
                put(cx + t, cy + y, frame); put(cx + tile - 1 - t, cy + y, frame)
        h, w = len(rows), max(len(r) for r in rows)
        scale = max(1, min(cell, (tile - 12) // max(w, h)))
        ox = cx + (tile - w * scale) // 2
        oy = cy + (tile - h * scale) // 2
        for y, r in enumerate(rows):
            for x, ch in enumerate(r):
                if ch == ".":
                    continue
                c = hex_rgb(PALETTE[colors[ch]])
                for dy in range(scale):
                    for dx in range(scale):
                        put(ox + x * scale + dx, oy + y * scale + dy, c)

    write_png(OUT, width, height, pixels)
    print(f"{len(pics)} картинок → {OUT} ({width}×{height})")


if __name__ == "__main__":
    main()
