"""
Читання й запис PNG без залежностей (лише zlib): авторський скрипт картинок,
контактний аркуш і підготовка спрайтів K1Candy ділять цей модуль.
Пікселі — список рядків, рядок — bytes довжиною width×channels.
"""
import struct
import zlib


def read_png(path):
    """→ (width, height, channels, rows[bytes]). Підтримує 8-бітні RGB/RGBA/сірі."""
    d = open(path, "rb").read()
    assert d[:8] == b"\x89PNG\r\n\x1a\n", path
    pos, idat, w, h, ct, bd = 8, b"", 0, 0, 0, 8
    while pos < len(d):
        ln = struct.unpack(">I", d[pos:pos + 4])[0]
        tag, body = d[pos + 4:pos + 8], d[pos + 8:pos + 8 + ln]
        pos += 12 + ln
        if tag == b"IHDR":
            w, h, bd, ct = struct.unpack(">IIBB", body[:10])
        elif tag == b"IDAT":
            idat += body
    assert bd == 8, f"{path}: лише 8 біт на канал"
    ch = {6: 4, 2: 3, 0: 1, 4: 2}[ct]
    raw, stride = zlib.decompress(idat), w * ch
    rows, prev, p = [], bytearray(stride), 0
    for _ in range(h):
        f, line, p = raw[p], bytearray(raw[p + 1:p + 1 + stride]), p + 1 + stride
        for i in range(stride):
            a = line[i - ch] if i >= ch else 0
            b = prev[i]
            c = prev[i - ch] if i >= ch else 0
            if f == 1:
                line[i] = (line[i] + a) & 255
            elif f == 2:
                line[i] = (line[i] + b) & 255
            elif f == 3:
                line[i] = (line[i] + (a + b) // 2) & 255
            elif f == 4:
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                line[i] = (line[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append(bytes(line))
        prev = line
    return w, h, ch, rows


def write_png(path, width, height, rows, channels=4):
    """rows — список bytes довжиною width×channels; channels 4 (RGBA) або 3 (RGB)."""
    ct = {4: 6, 3: 2, 1: 0}[channels]
    raw = bytearray()
    for y in range(height):
        raw.append(0)
        raw.extend(rows[y])

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, ct, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def rgba_grid(width, height, fill=(0, 0, 0, 0)):
    """Двовимірний список пікселів (r,g,b,a) для зручного малювання; у PNG — через flatten."""
    return [[fill] * width for _ in range(height)]


def flatten(grid):
    return [bytes(v for px in row for v in px) for row in grid]
