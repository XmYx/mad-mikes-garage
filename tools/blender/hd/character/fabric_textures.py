#!/usr/bin/env python3
"""Tileable fabric textures for the HD wardrobe: Krea-2 Turbo through the local ComfyUI (127.0.0.1:8188), made seamless,
plus a normal map per albedo (height from luminance, high-passed, wrapped Sobel). materials.make() samples them
(box projection over the rest pose) and keeps the game colour as the mean.

    python3 tools/blender/hd/character/fabric_textures.py [name ...]          # generate (default: all missing)
    python3 tools/blender/hd/character/fabric_textures.py --force [name ...]  # re-process (cached raw) / --regen: new

    python3 tools/blender/hd/character/fabric_textures.py --normals           # only rebuild the *_n.png

Writes tools/blender/hd/textures/fabric/<name>.png (albedo, sRGB, SIZE px) and <name>_n.png (OpenGL tangent normal)
and stats.json (mean linear colour per albedo, read by materials.py). Sources copied from modelbench/lib/texgen.py."""
import json
import os
import sys
import time
import urllib.request

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

COMFY = "http://127.0.0.1:8188"
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "textures", "fabric")
RAW = os.path.expanduser("~/.cache/madmax_fabric_raw")    # generated originals (not committed), for re-processing
NO_FLATTEN = {"grime_mask"}
SIZE = 512                     # stored size (garment atlases are 256-1024 and the tile repeats; keeps the set small)
STYLE = (", flat orthographic texture photograph shot straight on, even soft overcast lighting, no perspective, "
         "no folds, no shadows cast across it, fills the whole frame edge to edge, highly detailed material scan")

# name -> (prompt, normal strength, high-pass radius px at 1024)
TEXTURES = {
    "denim_blue": ("worn indigo blue denim fabric, fine diagonal twill weave, slight fading and white weft flecks", 2.2, 24),
    "denim_faded": ("old heavily faded light blue denim fabric, diagonal twill weave, worn soft, pale streaks", 2.2, 24),
    "denim_olive": ("worn olive green cotton drill fabric, diagonal twill weave, faded, dusty", 2.2, 24),
    "canvas_sand": ("worn sand beige heavy cotton canvas fabric, coarse plain weave, faded, faint dirt", 2.6, 24),
    "canvas_olive": ("worn olive drab military cotton canvas fabric, coarse plain weave, faded, dusty", 2.6, 24),
    "canvas_grey": ("worn grey heavy duck canvas fabric, coarse plain weave, faded, oil smudges", 2.6, 24),
    "canvas_patched": ("worn tan canvas fabric with sewn on darker canvas patches and visible stitched seams, coarse "
                       "weave, mended, dusty", 3.0, 32),
    "knit_sweater": ("chunky hand knitted wool sweater fabric, rows of stockinette knit stitches, soft fuzzy yarn, "
                     "slightly pilled", 3.2, 32),
    "knit_beanie": ("fine ribbed knit wool fabric, vertical ribs, soft yarn, slightly fuzzy", 3.0, 24),
    "jersey": ("plain cotton jersey t-shirt fabric, fine knit, soft, slightly worn and pilled, faint stains", 1.6, 20),
    "fleece": ("soft fleece fabric, fuzzy pile, light matted patches", 1.8, 24),
    "leather_brown": ("brown cowhide leather surface, natural grain and pores, scuffs, creases, worn lighter edges", 2.4, 28),
    "leather_black": ("black leather jacket surface, fine pebbled grain, creases, scuffed matte patches", 2.4, 28),
    "leather_worn": ("very old worn brown leather, cracked dry surface, scratches, faded patches", 3.0, 32),
    "leather_waxed": ("waxed oilskin duster coat leather, dark tan, mottled oily sheen, crinkled creases, worn", 2.4, 32),
    "suede": ("brown suede leather, soft napped surface, brushed marks", 1.6, 20),
    "rubber": ("matte black rubber surface, fine texture, small scuffs and dust", 1.4, 20),
    "kevlar": ("navy blue woven ballistic nylon fabric, tight basket weave clearly visible, slightly worn", 2.4, 20),
    "cordura": ("rugged nylon cordura backpack fabric, dense square weave, slightly worn", 2.2, 20),
    "webbing": ("nylon webbing strap material, close horizontal ribbed weave, worn fuzzy", 2.4, 20),
    "check_shemagh": ("cotton shemagh scarf fabric, cream with black houndstooth check pattern, soft weave, worn", 1.6, 20),
    "stripe_poncho": ("woven wool blanket poncho fabric, horizontal stripes of rust red, ochre and dark brown, coarse "
                      "handwoven texture", 2.4, 28),
    "hazmat_vinyl": ("yellow rubberised pvc hazmat suit material, slightly wrinkled, glossy, scuffs and dirt", 1.6, 32),
    "felt": ("brown wool felt hat material, dense matted fibres, slightly fuzzy, faint stains", 1.6, 24),
    "straw": ("flat panel of tightly woven natural straw, small regular basket weave of thin straw strips, sun bleached, "
              "uniform", 2.8, 20),
    "fur": ("synthetic fur trim, dense light grey brown pile, tufts", 3.0, 32),
    "quilted": ("quilted nylon parka lining, diamond stitched padded pattern, olive", 3.0, 48),
    "wool_coarse": ("coarse woven grey wool blanket fabric, rough fibres, felted patches", 2.4, 24),
    "grime_mask": ("abstract grunge texture of dirt, dust, sweat and oil stains on plain white cloth, soft blotches, "
                   "mostly light", 0.0, 0),
    "seam_strip": ("close up of rows of parallel topstitching seams on worn tan canvas, double stitched thread lines, "
                   "seam allowances", 3.0, 24),
}


def prompt_api(text, seed, w=1024, h=1024):
    return {
        "10": {"class_type": "UNETLoader", "inputs": {"unet_name": "krea2_turbo_fp8_scaled.safetensors", "weight_dtype": "default"}},
        "11": {"class_type": "CLIPLoader", "inputs": {"clip_name": "qwen3vl_4b_fp8_scaled.safetensors", "type": "krea2", "device": "default"}},
        "12": {"class_type": "VAELoader", "inputs": {"vae_name": "qwen_image_vae.safetensors"}},
        "6": {"class_type": "CLIPTextEncode", "inputs": {"text": text, "clip": ["11", 0]}},
        "13": {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": ["6", 0]}},
        "5": {"class_type": "EmptyLatentImage", "inputs": {"width": w, "height": h, "batch_size": 1}},
        "3": {"class_type": "KSampler", "inputs": {"seed": seed, "steps": 8, "cfg": 1.0, "sampler_name": "euler",
              "scheduler": "simple", "denoise": 1.0, "model": ["10", 0], "positive": ["6", 0], "negative": ["13", 0],
              "latent_image": ["5", 0]}},
        "8": {"class_type": "VAEDecode", "inputs": {"samples": ["3", 0], "vae": ["12", 0]}},
        "9": {"class_type": "SaveImage", "inputs": {"filename_prefix": "madmax_fabric", "images": ["8", 0]}},
    }


def _post(path, data):
    req = urllib.request.Request(COMFY + path, json.dumps(data).encode(), {"Content-Type": "application/json"})
    return json.loads(urllib.request.urlopen(req, timeout=30).read())


def _get(path):
    return urllib.request.urlopen(COMFY + path, timeout=30).read()


def tileable(im, band=0.18):
    """Seamless by offset: roll half a tile (edges become continuous), then cover the new centre seams with a
    feathered cross of the original image."""
    w, h = im.size
    rolled = Image.new("RGB", (w, h))
    rolled.paste(im.crop((w // 2, h // 2, w, h)), (0, 0))
    rolled.paste(im.crop((0, h // 2, w // 2, h)), (w - w // 2, 0))
    rolled.paste(im.crop((w // 2, 0, w, h // 2)), (0, h - h // 2))
    rolled.paste(im.crop((0, 0, w // 2, h // 2)), (w - w // 2, h - h // 2))
    mask = Image.new("L", (w, h), 0)
    bw, bh = int(w * band), int(h * band)
    d = ImageDraw.Draw(mask)
    d.rectangle((w // 2 - bw // 2, 0, w // 2 + bw // 2, h), fill=255)
    d.rectangle((0, h // 2 - bh // 2, w, h // 2 + bh // 2), fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(bw / 3))
    return Image.composite(im, rolled, mask)


def flatten(im, radius=0.12):
    """Divide out the photo's lighting falloff (vignette, gradients) so the tile carries no 2x2 pattern once tiled."""
    a = np.asarray(im, np.float32)
    low = np.asarray(im.filter(ImageFilter.GaussianBlur(im.size[0] * radius)), np.float32)
    out = a / np.maximum(low, 4.0) * a.reshape(-1, 3).mean(0)
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))


def generate(text, seed):
    pid = _post("/prompt", {"prompt": prompt_api(text + STYLE, seed)})["prompt_id"]
    while True:
        h = json.loads(_get(f"/history/{pid}"))
        if pid in h:
            break
        time.sleep(0.5)
    st = h[pid].get("status", {})
    if st.get("status_str") == "error":
        raise RuntimeError(json.dumps(st)[:800])
    img = next(i for o in h[pid]["outputs"].values() for i in o.get("images", []))
    raw = _get(f"/view?filename={img['filename']}&subfolder={img['subfolder']}&type={img['type']}")
    from io import BytesIO
    return Image.open(BytesIO(raw)).convert("RGB")


def _blur_wrap(a, r):
    """Gaussian blur with wrap-around (tileable)."""
    if r <= 0:
        return a
    k = np.arange(-3 * r, 3 * r + 1)
    g = np.exp(-0.5 * (k / r) ** 2)
    g /= g.sum()
    out = a
    for ax in (0, 1):
        acc = np.zeros_like(out)
        for o, wgt in zip(k, g):
            acc += wgt * np.roll(out, o, axis=ax)
        out = acc
    return out


def normal_map(albedo, strength, hp):
    """OpenGL tangent normal (+Y up) from the luminance, high-passed so lighting gradients don't tilt the surface."""
    a = np.asarray(albedo, np.float32) / 255.0
    lum = a @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    n = lum.shape[0]
    s = n / 1024.0
    h = lum - _blur_wrap(lum, max(1, int(hp * s))) if hp else lum
    h = h / (np.abs(h).mean() * 4 + 1e-4)
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5
    k = strength * 0.5
    nx, ny, nz = -dx * k, dy * k, np.ones_like(h)                 # image rows go down: +Y up flips dy
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    rgb = np.dstack([nx / ln, ny / ln, nz / ln]) * 0.5 + 0.5
    return Image.fromarray(np.clip(rgb * 255 + 0.5, 0, 255).astype(np.uint8))


def lin(c):
    c = np.asarray(c, np.float32) / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def write_stats():
    stats = {}
    for f in sorted(os.listdir(OUT)):
        if f.endswith(".png") and not f.endswith("_n.png"):
            a = lin(np.asarray(Image.open(os.path.join(OUT, f)).convert("RGB")))
            stats[f[:-4]] = [round(float(v), 5) for v in a.reshape(-1, 3).mean(0)]
    json.dump(stats, open(os.path.join(OUT, "stats.json"), "w"), indent=1)


def make_normals(names):
    for n in names:
        src = os.path.join(OUT, n + ".png")
        if not os.path.exists(src) or TEXTURES[n][1] <= 0:
            continue
        normal_map(Image.open(src).convert("RGB"), TEXTURES[n][1], TEXTURES[n][2]).save(os.path.join(OUT, n + "_n.png"), optimize=True)


def main(argv):
    force = "--force" in argv
    only_normals = "--normals" in argv
    names = [a for a in argv if not a.startswith("--")] or list(TEXTURES)
    os.makedirs(OUT, exist_ok=True)
    if not only_normals:
        for i, n in enumerate(names):
            dst = os.path.join(OUT, n + ".png")
            if os.path.exists(dst) and not force:
                continue
            t0 = time.time()
            raw = os.path.join(RAW, n + ".png")
            if os.path.exists(raw) and "--regen" not in argv:
                im = Image.open(raw).convert("RGB")
            else:
                im = generate(TEXTURES[n][0], seed=4100 + list(TEXTURES).index(n))
                os.makedirs(RAW, exist_ok=True)
                im.save(raw)
            if n not in NO_FLATTEN:
                im = flatten(im)
            im = tileable(im).resize((SIZE, SIZE), Image.LANCZOS)
            im.save(dst, optimize=True)
            print(n, "%.1fs" % (time.time() - t0), flush=True)
    make_normals(names)
    write_stats()


if __name__ == "__main__":
    main(sys.argv[1:])
