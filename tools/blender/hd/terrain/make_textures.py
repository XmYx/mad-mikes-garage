#!/usr/bin/env python3
"""HD terrain detail textures (+ the road layers R<k>_*, see roads()): tileable 1024 px albedo-detail (RGB around mid grey, multiplied by 2x the terrain's
vertex colour in HDLit _TERRAIN; A = smoothness) and normal maps for the ground classes DeformableTerrain splats
(order = TerrainClass): sand, dirt, grass, rock, gravel, asphalt, concrete, mud, snow.

    python3 tools/blender/hd/terrain/make_textures.py [out_dir] [--size 1024] [--sheet preview.png]

Default out_dir: Assets/MadMax/Resources/HDTerrain (gitignored like the HD pack; tools/release.sh renders it).
Everything is periodic (FFT-filtered noise, Voronoi on a wrapped jittered grid), so the maps tile seamlessly.
Where a photo source exists (tools/blender/hd/textures/terrain/<class>.png, from terrain_photos.py: Krea-2 via
ComfyUI) it replaces the procedural detail: its luminance high-passed (no blotches or tile repeat from above) around mid
grey with part of its colour variation, normals from the same height."""
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


PHOTOS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "textures", "terrain")
# class -> (detail contrast as luminance std, colour variation kept, high-pass radius as a share of the tile)
PHOTO_LOOK = {"sand": (0.07, 0.5, 0.08), "dirt": (0.09, 0.6, 0.1), "grass": (0.1, 0.8, 0.1), "rock": (0.08, 0.5, 0.06),
              "gravel": (0.11, 0.7, 0.1), "asphalt": (0.08, 0.4, 0.06), "concrete": (0.05, 0.4, 0.06),
              "mud": (0.08, 0.5, 0.1), "snow": (0.04, 0.3, 0.1)}


def blur_periodic(a, sigma):
    """Gaussian blur of a tileable 2D field (FFT, wraps)."""
    n = a.shape[0]
    f = np.fft.fftfreq(n)
    fx, fy = np.meshgrid(f, f)
    g = np.exp(-2 * (np.pi * sigma) ** 2 * (fx * fx + fy * fy))
    return np.real(np.fft.ifft2(np.fft.fft2(a) * g))


def photo(name, n):
    """-> (rgb detail around 0.5, height) from the photo source, or None."""
    src = os.path.join(PHOTOS, name + ".png")
    if not os.path.exists(src):
        return None
    rgb = np.asarray(Image.open(src).convert("RGB").resize((n, n), Image.LANCZOS), np.float64) / 255.0
    sd, keep, hp = PHOTO_LOOK.get(name, (0.08, 0.5, 0.08))
    lum = rgb @ np.array([0.2126, 0.7152, 0.0722])
    high = lum - blur_periodic(lum, n * hp)
    high /= high.std() + 1e-9
    chroma = rgb / np.maximum(lum[..., None], 1e-3)
    chroma = chroma / chroma.reshape(-1, 3).mean(0)
    chroma = chroma / np.maximum(blur_periodic(chroma.mean(-1), n * hp)[..., None], 1e-3)   # no colour blotches either
    tint = 1.0 + (chroma - 1.0) * keep
    detail = (0.5 + np.clip(high, -3.5, 3.5) * sd)[..., None] * tint
    return np.clip(detail, 0, 1), high


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


# ------------------------------------------------------------------ roads (HDLit _TERRAIN street layers)
# Road layers (order = World/HDTerrain.RoadNames): tiles are detail around mid grey (HDLit multiplies them into its
# own tarmac / concrete / granite colours; A = smoothness), "paint" keeps the paint's coverage in A, decals keep their
# own colour (sRGB values, linear import: HDLit converts) with the cut-out mask in A.
ROADS = ["asphalt_fresh", "asphalt_worn", "asphalt_cracked", "asphalt_patched", "sidewalk", "kerb", "paint",
         "manhole", "drain", "oil", "pothole"]
ROAD_PHOTOS = os.path.join(os.path.dirname(PHOTOS), "roads")
# tile -> (detail contrast, colour kept, high-pass radius share, smoothness, normal strength)
ROAD_LOOK = {"asphalt_fresh": (0.07, 0.2, 0.08, 0.3, 1.6), "asphalt_worn": (0.09, 0.25, 0.08, 0.26, 2.0),
             "asphalt_cracked": (0.1, 0.2, 0.25, 0.24, 2.6), "asphalt_patched": (0.09, 0.2, 0.3, 0.26, 1.8),
             "sidewalk": (0.05, 0.3, 0.1, 0.2, 1.2), "kerb": (0.08, 0.3, 0.06, 0.25, 1.4)}


def road_tile(name, n, seed):
    """-> (rgba, height, normal strength) for a tiling road layer, from its photo or procedurally."""
    sd, keep, hp, smooth, strength = ROAD_LOOK[name]
    src = os.path.join(ROAD_PHOTOS, name + ".png")
    if os.path.exists(src):
        rgb = np.asarray(Image.open(src).convert("RGB").resize((n, n), Image.LANCZOS), np.float64) / 255.0
        lum = rgb @ np.array([0.2126, 0.7152, 0.0722])
        high = lum - blur_periodic(lum, n * hp)
        high /= high.std() + 1e-9
        chroma = rgb / np.maximum(lum[..., None], 1e-3)
        chroma = chroma / chroma.reshape(-1, 3).mean(0)
        detail = (0.5 + np.clip(high, -3.5, 3.5) * sd)[..., None] * (1.0 + (chroma - 1.0) * keep)
        gloss = smooth + np.clip(-high, 0, 3) * 0.05 * (name.startswith("asphalt"))       # sealed cracks, tar: glossier
        return np.concatenate([np.clip(detail, 0, 1), np.clip(gloss, 0, 1)[..., None]], -1), high, strength
    lum, sm, h, st, tint = make("concrete" if name in ("sidewalk", "kerb") else "asphalt", n, seed)
    rgb = np.clip((0.5 + (lum - 0.5) * 1.5)[..., None] * tint, 0, 1)
    return np.concatenate([rgb, np.full((n, n, 1), smooth)], -1), h, st


def road_paint(n, seed):
    src = os.path.join(ROAD_PHOTOS, "paint.png")
    if os.path.exists(src):
        rgb = np.asarray(Image.open(src).convert("RGB").resize((n, n), Image.LANCZOS), np.float64) / 255.0
        lum = rgb @ np.array([0.2126, 0.7152, 0.0722])
        cover = np.clip((lum - 0.45) / 0.2, 0, 1)
    else:
        cover = np.clip(fbm(n, 1.2, seed, 4, 300) * 0.6 + 1.2, 0, 1)
        lum = 0.6 + cover * 0.3
    detail = 0.5 + (lum - lum.mean()) * 0.5
    rgba = np.stack([detail, detail, detail, cover], -1)
    return np.clip(rgba, 0, 1), cover - 0.5, 1.0


def road_decal(name, n, seed):
    """-> (rgba: sRGB colour + cut-out mask, height, strength). Photo cut out against its border colour, else a
    simple procedural stand-in."""
    y, x = (np.mgrid[0:n, 0:n] + 0.5) / n - 0.5
    r = np.sqrt(x * x + y * y)
    src = os.path.join(ROAD_PHOTOS, name + ".png")
    if os.path.exists(src):
        rgb = np.asarray(Image.open(src).convert("RGB").resize((n, n), Image.LANCZOS), np.float64) / 255.0
        lum = rgb @ np.array([0.2126, 0.7152, 0.0722])
        edge = np.concatenate([rgb[:n // 16].reshape(-1, 3), rgb[-n // 16:].reshape(-1, 3), rgb[:, :n // 16].reshape(-1, 3), rgb[:, -n // 16:].reshape(-1, 3)])
        diff = np.sqrt(((rgb - np.median(edge, 0)) ** 2).sum(-1))
        if name == "manhole":
            prof = [diff[(r > a) & (r < a + 0.01)].mean() for a in np.arange(0.3, 0.5, 0.01)]
            rad = 0.3 + 0.01 * (int(np.argmax(np.array(prof) < 0.5 * prof[0])) or 17)
            mask = np.clip((rad - r) * n / 3.0, 0, 1)
        elif name == "drain":
            m = diff > 0.12
            ys, xs = np.nonzero(m[n // 8: -n // 8, n // 8: -n // 8])
            x0, x1 = np.percentile(xs, [1, 99]) + n // 8; y0, y1 = np.percentile(ys, [1, 99]) + n // 8
            px, py = (np.mgrid[0:n, 0:n][1]), (np.mgrid[0:n, 0:n][0])
            ex = np.minimum(px - x0, x1 - px); ey = np.minimum(py - y0, y1 - py)
            mask = np.clip(np.minimum(ex, ey) / 3.0, 0, 1)
        elif name == "oil":                                                     # opacity by how much darker than the asphalt
            el = np.median(edge @ np.array([0.2126, 0.7152, 0.0722]))
            mask = np.clip((el - blur_periodic(lum, n * 0.004) - 0.04) / (el * 0.45), 0, 1) * np.clip((0.47 - r) * 20, 0, 1)
        else:
            m = blur_periodic(diff, n * 0.01) > 0.1
            try:
                from scipy import ndimage
                m = ndimage.binary_fill_holes(m)
                lab, k = ndimage.label(m)
                if k > 1:
                    sizes = ndimage.sum(m, lab, range(1, k + 1))
                    m = lab == (1 + int(np.argmax(sizes)))
            except ImportError:
                pass
            mask = np.clip(blur_periodic(m.astype(float), n * 0.006), 0, 1)
            mask *= np.clip((0.5 - r) * 20, 0, 1)
        high = lum - blur_periodic(lum, n * 0.05)
        return np.concatenate([rgb, mask[..., None]], -1), high / (high.std() + 1e-9), 2.5 if name != "oil" else 0.2
    ring = np.clip((0.42 - r) * n / 3, 0, 1)
    if name == "manhole":
        grid = ((np.floor((x + 0.5) * 20) + np.floor((y + 0.5) * 20)) % 2) * 0.06
        c = 0.2 + grid - np.clip(1 - np.abs(r - 0.4) * n / 6, 0, 1) * 0.08
        return np.stack([c * 1.05, c, c * 0.95, ring], -1), c, 2.0
    if name == "drain":
        box = np.clip((0.3 - np.maximum(np.abs(x), np.abs(y) * 1.4)) * n / 3, 0, 1)
        slot = (np.abs(((x + 0.5) * 14) % 1 - 0.5) < 0.25) & (np.abs(y) < 0.17)
        c = np.where(slot, 0.05, 0.24)
        return np.stack([c * 1.1, c, c * 0.9, box], -1), c, 2.5
    blob = np.clip((0.3 + fbm(n, 2.2, seed, 1, 8) * 0.06 - r) * n / 6, 0, 1)
    c = np.full((n, n), 0.08 if name == "oil" else 0.22) + fbm(n, 0.8, seed + 1, 20) * 0.02
    return np.stack([c, c, c * 1.05, blob], -1), c, 1.0


def roads(out, n, sheet):
    for k, name in enumerate(ROADS):
        seed = 3000 + k * 31
        if name in ROAD_LOOK: rgba, h, strength = road_tile(name, n, seed)
        elif name == "paint": rgba, h, strength = road_paint(n, seed)
        else: rgba, h, strength = road_decal(name, n, seed)
        Image.fromarray((np.clip(rgba, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(out, "R%d_%s_A.png" % (k, name)))
        nm = normal_map((h - h.mean()) / (h.std() + 1e-9) * (n / 1024.0), strength * 0.18)
        Image.fromarray((nm * 255 + 0.5).astype(np.uint8), "RGB").save(os.path.join(out, "R%d_%s_N.png" % (k, name)))
        print("[hd-roads] %s" % name, flush=True)
    if sheet:
        t = 256
        img = Image.new("RGB", (t * len(ROADS), t * 2))
        for k, name in enumerate(ROADS):
            a = Image.open(os.path.join(out, "R%d_%s_A.png" % (k, name))).convert("RGBA").resize((t, t))
            bg = Image.new("RGBA", (t, t), (255, 0, 255, 255)); bg.alpha_composite(a)
            img.paste(bg.convert("RGB"), (k * t, 0))
            img.paste(Image.open(os.path.join(out, "R%d_%s_N.png" % (k, name))).resize((t, t)), (k * t, t))
        root, ext = os.path.splitext(sheet)
        img.save(root + "_roads" + ext)


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
        ph = photo(name, n)
        if ph is not None:
            rgb, h = ph
            strength *= 0.8
        a = np.full((n, n, 1), smooth)
        Image.fromarray((np.concatenate([rgb, a], -1) * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(out, "T%d_%s_A.png" % (k, name)))
        nm = normal_map((h - h.mean()) / (h.std() + 1e-9) * (n / 1024.0), strength * 0.18)
        Image.fromarray((nm * 255 + 0.5).astype(np.uint8), "RGB").save(os.path.join(out, "T%d_%s_N.png" % (k, name)))
        tiles.append((name, rgb, nm))
        print("[hd-terrain] %s" % name, flush=True)
    roads(out, n, sheet)
    if sheet:
        t = 256
        img = Image.new("RGB", (t * len(tiles), t * 2))
        for k, (name, rgb, nm) in enumerate(tiles):
            a = Image.fromarray((np.clip(rgb * 2 * np.array([0.55, 0.42, 0.3]), 0, 1) * 255).astype(np.uint8)).resize((t, t))
            img.paste(a, (k * t, 0)); img.paste(Image.fromarray((nm * 255).astype(np.uint8)).resize((t, t)), (k * t, t))
        img.save(sheet)


if __name__ == "__main__":
    main()
