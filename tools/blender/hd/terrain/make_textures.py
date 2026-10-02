#!/usr/bin/env python3
"""HD terrain detail textures: tileable 1024 px albedo-detail (RGB around mid grey, multiplied by 2x the terrain's
vertex colour in HDLit _TERRAIN; A = smoothness) and normal maps for the ground classes DeformableTerrain splats
(order = TerrainClass): sand, dirt, grass, rock, gravel, asphalt, concrete, mud, snow.

    python3 tools/blender/hd/terrain/make_textures.py [out_dir] [--size 1024] [--sheet preview.png]

Default out_dir: Assets/MadMax/Resources/HDTerrain (gitignored like the HD pack; tools/release.sh renders it).
Everything is periodic (FFT-filtered noise, Voronoi on a wrapped jittered grid), so the maps tile seamlessly."""
import os
import sys
import numpy as np
from PIL import Image

CLASSES = ["sand", "dirt", "grass", "rock", "gravel", "asphalt", "concrete", "mud", "snow"]


def fbm(n, beta, seed, lo=1.0, hi=None, aniso=None):
    """Periodic noise with a 1/f^beta spectrum between frequencies lo..hi (cycles per tile), unit variance."""
    r = np.random.default_rng(seed)
    f = np.fft.fftfreq(n) * n
    fx, fy = np.meshgrid(f, f)
    if aniso is not None:                           # (angle, stretch): elongated features
        a, s = aniso
        u = fx * np.cos(a) + fy * np.sin(a); v = -fx * np.sin(a) + fy * np.cos(a)
        fr = np.sqrt((u * s) ** 2 + (v / s) ** 2)
    else:
        fr = np.sqrt(fx * fx + fy * fy)
    amp = np.where(fr >= lo, 1.0 / np.maximum(fr, 1e-6) ** beta, 0.0)
    if hi is not None:
        amp *= np.exp(-(fr / hi) ** 2)
    spec = (r.standard_normal((n, n)) + 1j * r.standard_normal((n, n))) * amp
    img = np.real(np.fft.ifft2(spec))
    return (img - img.mean()) / (img.std() + 1e-9)


def voronoi(n, cells, seed, jitter=0.9):
    """Periodic F1, F2 (in cell units) and the index of the nearest feature point."""
    r = np.random.default_rng(seed)
    pts = r.random((cells, cells, 2)) * jitter + (1 - jitter) / 2
    ids = r.permutation(cells * cells).reshape(cells, cells)
    y, x = np.mgrid[0:n, 0:n] * (cells / n)
    cx, cy = np.floor(x).astype(int), np.floor(y).astype(int)
    f1 = np.full((n, n), 9.0); f2 = np.full((n, n), 9.0); idx = np.zeros((n, n), int)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            gx, gy = cx + dx, cy + dy
            wx, wy = gx % cells, gy % cells
            px = gx + pts[wy, wx, 0]; py = gy + pts[wy, wx, 1]
            d = np.sqrt((x - px) ** 2 + (y - py) ** 2)
            closer = d < f1
            f2 = np.where(closer, f1, np.minimum(f2, d))
            idx = np.where(closer, ids[wy, wx], idx)
            f1 = np.where(closer, d, f1)
    return f1, f2, idx


def norm01(a):
    return (a - a.min()) / (a.max() - a.min() + 1e-9)


def normal_map(h, strength):
    """Tangent-space normal (x right, y up the texture) from a periodic height field."""
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5 * strength
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5 * strength
    nz = np.ones_like(h)
    l = np.sqrt(dx * dx + dy * dy + nz)
    n = np.stack([-dx / l, dy / l, nz / l], -1)        # rows run down the image: +v is up
    return (n * 0.5 + 0.5)


def make(name, n, seed):
    """-> (rgb detail around 0.5, smoothness, height, normal strength)."""
    s = seed
    tint = np.ones((n, n, 3))
    if name == "sand":
        warp = fbm(n, 2.0, s, 1, 12) * 0.08
        y, x = np.mgrid[0:n, 0:n] / n
        rip = np.sin(2 * np.pi * (x * 14 + y * 3 + warp * 6 + fbm(n, 2.5, s + 1, 1, 6) * 0.12))
        grain = fbm(n, 0.3, s + 2, 60)
        h = rip * 0.6 + fbm(n, 2.2, s + 3, 1, 30) * 0.5 + grain * 0.15
        lum = 0.5 + rip * 0.035 + grain * 0.05 + fbm(n, 2.0, s + 4, 1, 20) * 0.04
        specks = (fbm(n, 0.2, s + 5, 120) > 2.2)
        lum = np.where(specks, lum * 0.72, lum)
        return lum, 0.08, h, 2.0, tint
    if name == "dirt":
        f1, f2, idx = voronoi(n, 46, s)
        pebble = np.clip(1 - f1 / 0.32, 0, 1) * ((idx % 7) == 0)
        clods = fbm(n, 1.6, s + 1, 2, 120)
        cr = np.clip(1 - (f2 - f1) / 0.05, 0, 1) * (fbm(n, 2, s + 9, 1, 10) > 0.3)
        h = clods * 0.5 + pebble * 1.6 - cr * 0.6 + fbm(n, 0.4, s + 2, 80) * 0.15
        lum = 0.5 + clods * 0.06 + pebble * 0.14 - cr * 0.12 + fbm(n, 0.4, s + 3, 80) * 0.04
        tint[..., 0] += pebble * 0.05; tint[..., 2] += pebble * 0.08
        return lum, 0.1, h, 3.0, tint
    if name == "grass":
        blades = sum(fbm(n, 0.6, s + k, 20, 240, aniso=(k * 1.1, 4.0)) for k in range(4)) / 2.0
        clump = fbm(n, 2.0, s + 9, 2, 24)
        gaps = np.clip(-blades - 0.8, 0, 3)
        h = blades * 0.7 + clump * 0.6
        lum = 0.5 + blades * 0.075 + clump * 0.05 - gaps * 0.06
        dry = norm01(fbm(n, 2.2, s + 10, 1, 10))
        tint[..., 0] += (dry - 0.5) * 0.14; tint[..., 1] += blades * 0.02; tint[..., 2] -= (dry - 0.5) * 0.08
        return lum, 0.12, h, 2.5, tint
    if name == "rock":
        f1, f2, idx = voronoi(n, 9, s)
        crack = np.clip(1 - (f2 - f1) / 0.035, 0, 1)
        strata = np.sin(2 * np.pi * (np.mgrid[0:n, 0:n][0] / n * 7 + fbm(n, 2.2, s + 1, 1, 8) * 0.25))
        rough = fbm(n, 1.2, s + 2, 2, 200)
        plate = (idx % 5) / 4.0 - 0.5
        h = rough * 0.6 + strata * 0.3 - crack * 1.6 + plate * 0.4
        lum = 0.5 + rough * 0.06 + strata * 0.04 - crack * 0.18 + plate * 0.08
        return lum, 0.22, h, 4.0, tint
    if name == "gravel":
        f1, f2, idx = voronoi(n, 58, s, 0.85)
        dome = np.sqrt(np.clip(1 - (f1 / 0.55) ** 2, 0, 1))
        gap = np.clip(1 - (f2 - f1) / 0.12, 0, 1)
        stone = ((idx * 7919) % 97) / 96.0 - 0.5
        h = dome * 1.2 - gap * 0.6 + fbm(n, 0.5, s + 1, 60) * 0.1
        lum = 0.5 + stone * 0.22 + dome * 0.06 - gap * 0.18 + fbm(n, 0.4, s + 2, 100) * 0.03
        tint[..., 0] += stone * 0.06; tint[..., 2] -= stone * 0.05
        return lum, 0.2, h, 3.5, tint
    if name == "asphalt":
        agg = fbm(n, 0.1, s, 150)
        light = np.clip(agg - 1.6, 0, 2); dark = np.clip(-agg - 1.4, 0, 2)
        stain = fbm(n, 2.4, s + 1, 1, 10)
        h = fbm(n, 0.5, s + 2, 80) * 0.4 + light * 0.4 - dark * 0.3
        lum = 0.5 + light * 0.22 - dark * 0.15 + stain * 0.04 + fbm(n, 0.6, s + 3, 40) * 0.03
        return lum, 0.28, h, 2.0, tint
    if name == "concrete":
        pores = np.clip(-fbm(n, 0.1, s, 160) - 2.4, 0, 2)
        stain = fbm(n, 2.5, s + 1, 1, 8)
        trowel = fbm(n, 1.2, s + 2, 6, 120, aniso=(0.3, 2.0))
        h = trowel * 0.25 - pores * 0.8
        lum = 0.5 + stain * 0.045 + trowel * 0.012 - pores * 0.2 + fbm(n, 0.6, s + 3, 60) * 0.025
        return lum, 0.22, h, 1.5, tint
    if name == "mud":
        base = fbm(n, 2.3, s, 1, 40)
        wet = norm01(fbm(n, 2.6, s + 1, 1, 12))
        tread = np.sin(2 * np.pi * (np.mgrid[0:n, 0:n][1] / n * 22)) * (np.abs(np.mgrid[0:n, 0:n][0] / n - 0.5) < 0.12) * 0.6
        h = base * 0.6 - wet * 0.8 + tread * 0.4 + fbm(n, 0.8, s + 2, 50) * 0.1
        lum = 0.5 + base * 0.05 - wet * 0.07 + fbm(n, 0.6, s + 3, 60) * 0.02
        return lum, 0.35, h, 2.0, tint
    if name == "snow":
        drift = fbm(n, 2.4, s, 1, 16)
        crust = fbm(n, 1.0, s + 1, 10, 200)
        sparkle = fbm(n, 0.0, s + 2, 200) > 2.8
        h = drift * 0.8 + crust * 0.15
        lum = 0.5 + drift * 0.025 + crust * 0.015 + sparkle * 0.08
        tint[..., 2] += 0.02
        return lum, 0.38, h, 1.2, tint
    raise ValueError(name)


def main():
    args = sys.argv[1:]
    here = os.path.dirname(os.path.abspath(__file__))
    repo = os.path.abspath(os.path.join(here, "..", "..", "..", ".."))
    out = os.path.join(repo, "Assets", "MadMax", "Resources", "HDTerrain")
    n, sheet = 1024, None
    i = 0
    while i < len(args):
        if args[i] == "--size": n = int(args[i + 1]); i += 1
        elif args[i] == "--sheet": sheet = args[i + 1]; i += 1
        else: out = args[i]
        i += 1
    os.makedirs(out, exist_ok=True)
    tiles = []
    for k, name in enumerate(CLASSES):
        lum, smooth, h, strength, tint = make(name, n, 1000 + k * 17)
        lum = 0.5 + (lum - 0.5) * 1.5                                    # read at a distance over the vertex tint
        rgb = np.clip(lum[..., None] * tint, 0, 1)
        a = np.full((n, n, 1), smooth)
        Image.fromarray((np.concatenate([rgb, a], -1) * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(out, "T%d_%s_A.png" % (k, name)))
        nm = normal_map((h - h.mean()) / (h.std() + 1e-9) * (n / 1024.0), strength * 0.18)
        Image.fromarray((nm * 255 + 0.5).astype(np.uint8), "RGB").save(os.path.join(out, "T%d_%s_N.png" % (k, name)))
        tiles.append((name, rgb, nm))
        print("[hd-terrain] %s" % name, flush=True)
    if sheet:
        t = 256
        img = Image.new("RGB", (t * len(tiles), t * 2))
        for k, (name, rgb, nm) in enumerate(tiles):
            a = Image.fromarray((np.clip(rgb * 2 * np.array([0.55, 0.42, 0.3]), 0, 1) * 255).astype(np.uint8)).resize((t, t))
            img.paste(a, (k * t, 0)); img.paste(Image.fromarray((nm * 255).astype(np.uint8)).resize((t, t)), (k * t, t))
        img.save(sheet)


if __name__ == "__main__":
    main()
