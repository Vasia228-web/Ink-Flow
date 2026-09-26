"""
Ink Flow · P2Watercolor — turns any pixel-art PNG into two textures for the picture canvas:
  <name>_watercolor.png  the finished picture, painted in watercolour on warm paper
  <name>_sketch.png      the same picture as a pencil sketch (what the player sees before painting)
In the game, a mask (one texel per art pixel) blends sketch -> watercolour as zones get painted.

Usage:
  pip install pillow numpy scipy
  python watercolorize.py comet.png                 # 24 px per art pixel
  python watercolorize.py comet.png --px 32 --seed 7 --out build/

Transparent pixels of the source = empty paper. Each opaque colour is one paint zone.
"""
import argparse, os
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter, map_coordinates

PAPER = np.array([0xEC, 0xE2, 0xCC], float) / 255
PENCIL = np.array([0x5D, 0x56, 0x70], float) / 255


def smooth_noise(shape, sigma, rng):
    n = gaussian_filter(rng.standard_normal(shape), sigma, mode="wrap")
    return n / (np.abs(n).max() + 1e-9)


def paper(h, w, rng):
    grain = gaussian_filter(rng.standard_normal((h, w)), 0.7)
    grain = grain / (np.abs(grain).max() + 1e-9)
    img = np.ones((h, w, 3)) * PAPER
    return np.clip(img * (1 - 0.06 * grain[..., None]), 0, 1)


def displace(mask, dx, dy):
    h, w = mask.shape
    yy, xx = np.mgrid[0:h, 0:w].astype(float)
    return map_coordinates(mask, [yy + dy, xx + dx], order=1, mode="constant")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("src")
    ap.add_argument("--px", type=int, default=24, help="output pixels per art pixel")
    ap.add_argument("--seed", type=int, default=9)
    ap.add_argument("--out", default=".")
    a = ap.parse_args()
    rng = np.random.default_rng(a.seed)

    art = np.array(Image.open(a.src).convert("RGBA"))
    ah, aw = art.shape[:2]
    p = a.px
    H, W = (ah + 2) * p, (aw + 2) * p                     # one art-pixel margin of paper around the picture

    def up(m):                                              # art grid -> output grid (nearest), with margin
        big = np.kron(m.astype(float), np.ones((p, p)))
        out = np.zeros((H, W)); out[p:p + ah * p, p:p + aw * p] = big
        return out

    dx = smooth_noise((H, W), p * 0.9, rng) * p * 0.2      # wobbly paint edges
    dy = smooth_noise((H, W), p * 0.9, rng) * p * 0.2
    fine = smooth_noise((H, W), 1.2, rng)                   # pigment granulation

    wc = paper(H, W, rng)
    sk = paper(H, W, rng)

    opaque = art[..., 3] > 127
    colours = {tuple(c) for c in art[..., :3][opaque]}
    # paint light colours first, dark outlines last (like a real painter)
    for c in sorted(colours, key=lambda c: -sum(int(v) for v in c)):
        m = up(opaque & np.all(art[..., :3] == c, axis=-1))
        m = gaussian_filter(np.clip(displace(m, dx, dy), 0, 1), 0.8)
        col = np.array(c, float) / 255
        alpha = 0.82 * m * (1 - 0.10 * fine)
        wc = wc * (1 - alpha[..., None]) + col * alpha[..., None]
        edge = np.clip(m - gaussian_filter(m, 1.6), 0, 1)   # pigment pools at the edge
        wc = wc * (1 - 0.45 * edge[..., None])

    # pencil sketch: lines wherever two neighbouring art pixels differ
    key = np.where(opaque, art[..., 0].astype(int) * 65536 + art[..., 1].astype(int) * 256 + art[..., 2], -1)
    lines = np.zeros((H, W))
    for r in range(-1, ah + 1):
        for cc in range(-1, aw + 1):
            k = key[r, cc] if 0 <= r < ah and 0 <= cc < aw else -1
            kr = key[r, cc + 1] if 0 <= r < ah and 0 <= cc + 1 < aw else -1
            kd = key[r + 1, cc] if 0 <= r + 1 < ah and 0 <= cc < aw else -1
            x, y = (cc + 2) * p, (r + 1) * p
            if k != kr:
                lines[max(y, 0):y + p, x - 1:x + 1] = 1
            x, y = (cc + 1) * p, (r + 2) * p
            if k != kd:
                lines[y - 1:y + 1, max(x, 0):x + p] = 1
    lines = gaussian_filter(np.clip(displace(lines, dx * .3, dy * .3), 0, 1), 0.5) * 0.45
    sk = sk * (1 - lines[..., None]) + PENCIL * lines[..., None]

    os.makedirs(a.out, exist_ok=True)
    name = os.path.splitext(os.path.basename(a.src))[0]
    Image.fromarray((np.clip(wc, 0, 1) * 255).astype(np.uint8)).save(os.path.join(a.out, f"{name}_watercolor.png"))
    Image.fromarray((np.clip(sk, 0, 1) * 255).astype(np.uint8)).save(os.path.join(a.out, f"{name}_sketch.png"))
    print(f"{name}: {W}x{H} -> {name}_watercolor.png, {name}_sketch.png")


if __name__ == "__main__":
    main()
