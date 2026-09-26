"""
Ink Flow · W4DarkCanvas — bakes a pixel-art picture into the layers of the "dark canvas" style.
Pixels stay perfectly square: nearest-neighbour upscale, no distortion.

Outputs (all the same size, transparent background except the canvas):
  <name>_canvas.png   dark canvas with weave (background of the picture area)
  <name>_sketch.png   thin light outlines of every colour zone (what the player sees before painting)
  <name>_paint.png    the finished picture: crisp pixels + soft top light / bottom shade + faint weave
  <name>_halo.png     soft glow of the picture colours (sprite halo instead of Post-process Bloom)
  <name>_preview_empty.png / _preview_done.png   the layers already stacked, for checking by eye

Usage:
  pip install pillow numpy scipy
  python darkcanvas.py rogatyk.png                # 16 px per art pixel
  python darkcanvas.py rogatyk.png --px 20 --margin 2 --out Baked/
"""
import argparse, os
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter

CANVAS = (0x40, 0x3C, 0x78)
LINE = (0xDF, 0xE0, 0xF5)

CFG = dict(
    weave_pitch=0.25,      # weave cell, in art pixels
    weave_alpha=0.045,     # weave lines on the canvas
    weave_on_paint=0.6,    # how much of the weave shows through the paint
    light_top=0.16,        # white at the top of the picture
    shade_bottom=0.16,     # black at the bottom
    halo_sigma=1.4,        # halo blur, in art pixels
    halo_alpha=0.45,
    sketch_alpha=0.35,
    sketch_width=0.07,     # outline width, in art pixels (min 1 px)
)


def weave(h, w, pitch):
    """lighter threads: horizontal half-rows + vertical half-columns"""
    yy, xx = np.mgrid[0:h, 0:w]
    rows = ((yy % pitch) < pitch / 2).astype(float)
    cols = ((xx % pitch) < pitch / 2).astype(float) * 0.7
    return np.clip(rows + cols, 0, 1.7) / 1.7


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("src")
    ap.add_argument("--px", type=int, default=16, help="output pixels per art pixel")
    ap.add_argument("--margin", type=int, default=2, help="empty canvas around the art, in art pixels")
    ap.add_argument("--out", default=".")
    a = ap.parse_args()
    p, m = a.px, a.margin

    art = np.array(Image.open(a.src).convert("RGBA"))
    ah, aw = art.shape[:2]
    H, W = (ah + 2 * m) * p, (aw + 2 * m) * p
    oy, ox = m * p, m * p

    def up(x):
        big = np.kron(x, np.ones((p, p) + ((1,) * (x.ndim - 2))))
        out = np.zeros((H, W) + x.shape[2:], float)
        out[oy:oy + ah * p, ox:ox + aw * p] = big
        return out

    opaque = art[..., 3] > 127
    rgb = up(art[..., :3].astype(float) / 255)
    alpha = up(opaque.astype(float))
    wv = weave(H, W, max(2, int(round(p * CFG["weave_pitch"]))))

    # --- canvas
    canvas = np.ones((H, W, 3)) * np.array(CANVAS) / 255
    canvas = np.clip(canvas + CFG["weave_alpha"] * wv[..., None], 0, 1)

    # --- paint: crisp pixels, light from the top, faint weave
    yy = np.mgrid[0:H, 0:W][0]
    t = np.clip((yy - oy) / (ah * p), 0, 1)
    light = np.where(t < 0.5, CFG["light_top"] * (1 - t / 0.5), 0)
    shade = np.where(t > 0.5, CFG["shade_bottom"] * (t - 0.5) / 0.5, 0)
    paint = rgb * (1 - light[..., None]) + light[..., None]
    paint = paint * (1 - shade[..., None])
    paint = np.clip(paint + CFG["weave_alpha"] * CFG["weave_on_paint"] * wv[..., None], 0, 1)

    # --- halo
    s = CFG["halo_sigma"] * p
    ha = gaussian_filter(alpha, s)
    hc = np.stack([gaussian_filter(rgb[..., i] * alpha, s) for i in range(3)], -1) / np.maximum(ha, 1e-6)[..., None]
    halo_a = np.clip(ha * CFG["halo_alpha"] * 1.6, 0, CFG["halo_alpha"])

    # --- sketch: lines between different colours (and between art and empty)
    key = np.where(opaque, art[..., 0].astype(np.int64) * 65536 + art[..., 1].astype(np.int64) * 256 + art[..., 2], -1)
    pad = np.pad(key, 1, constant_values=-1)
    lw = max(1, int(round(p * CFG["sketch_width"])))
    lines = np.zeros((H, W))
    for r in range(-1, ah):
        for c in range(-1, aw):
            k = pad[r + 1, c + 1]
            if k != pad[r + 1, c + 2] and (r >= 0):      # vertical edge on the right of (r, c)
                x = ox + (c + 1) * p
                lines[oy + r * p: oy + (r + 1) * p, max(0, x - lw // 2 - 1): x + (lw + 1) // 2] = 1
            if k != pad[r + 2, c + 1] and (c >= 0):      # horizontal edge under (r, c)
                y = oy + (r + 1) * p
                lines[max(0, y - lw // 2 - 1): y + (lw + 1) // 2, ox + c * p: ox + (c + 1) * p] = 1
    sketch_a = lines * CFG["sketch_alpha"]

    def save(rgb_, a_, name):
        img = np.dstack([np.clip(rgb_, 0, 1), np.clip(a_, 0, 1)])
        Image.fromarray((img * 255).round().astype(np.uint8), "RGBA").save(os.path.join(a.out, name))

    def over(base, rgb_, a_):
        return base * (1 - a_[..., None]) + rgb_ * a_[..., None]

    os.makedirs(a.out, exist_ok=True)
    n = os.path.splitext(os.path.basename(a.src))[0]
    save(canvas, np.ones((H, W)), f"{n}_canvas.png")
    save(np.ones((H, W, 3)) * np.array(LINE) / 255, sketch_a, f"{n}_sketch.png")
    save(paint, alpha, f"{n}_paint.png")
    save(hc, halo_a, f"{n}_halo.png")
    empty = over(canvas, np.ones((H, W, 3)) * np.array(LINE) / 255, sketch_a)
    done = over(over(canvas, hc, halo_a), paint, alpha)
    Image.fromarray((np.clip(empty, 0, 1) * 255).astype(np.uint8)).save(os.path.join(a.out, f"{n}_preview_empty.png"))
    Image.fromarray((np.clip(done, 0, 1) * 255).astype(np.uint8)).save(os.path.join(a.out, f"{n}_preview_done.png"))
    print(f"{n}: {W}x{H}, {int(opaque.sum())} painted pixels")


if __name__ == "__main__":
    main()
