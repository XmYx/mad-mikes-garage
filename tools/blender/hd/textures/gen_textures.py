#!/usr/bin/env python3
"""Photographic material textures for the HD world / furniture sets, made with Krea-2 Turbo through the local ComfyUI
(127.0.0.1:8188, the generator of modelbench/lib/texgen.py) and turned seamlessly tileable.

    python3 gen_textures.py [name ...] [--seed N] [--size 1024] [--force]

Writes textures/world/<name>.png (albedo, tileable) and <name>_n.png (tangent normal from luminance, OpenGL +Y).
Prompts live in TEXTURES (name -> (prompt, physical size in metres the tile covers)); texmat.py reads SCALE from
the generated `world/index.json`. Fabric for the wardrobe has its own generator (textures/fabric/)."""
import json
import os
import sys
import time
import urllib.request

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

COMFY = "http://127.0.0.1:8188"
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "world")
STYLE = (", flat orthographic texture photograph shot straight on, even overcast lighting, no perspective, no shadows "
         "cast across it, fills the whole frame edge to edge, highly detailed, seamless material scan")

# name: (prompt, metres covered by one tile, normal strength)
TEXTURES = {
    "pine_raw": ("rough sawn pine planks laid side by side horizontally, pale yellow-brown wood grain, knots, saw marks, "
                 "thin dark gaps between the boards", 1.2, 2.0),
    "pine_painted": ("old horizontal pine boards painted faded sage green, paint cracked and flaking at the edges "
                     "showing grey weathered wood underneath, thin gaps between boards", 1.2, 2.0),
    "pine_worn": ("weathered grey silvery barn wood planks, horizontal, deep grain, cracks, rusty nail heads, sun "
                  "bleached and worn", 1.2, 2.5),
    "oak": ("close up of smooth varnished dark oak furniture wood, straight horizontal grain, warm honey brown, "
            "subtle scratches and wear, satin finish", 0.6, 1.0),
    "plywood": ("sheet of plywood, light birch face veneer with wavy grain, a few stains and pencil marks", 1.0, 1.0),
    "osb": ("oriented strand board OSB panel, compressed wood chips and flakes, beige and brown", 0.8, 1.5),
    "wallpaper_floral": ("faded vintage 1950s farmhouse wallpaper, small repeating rose bouquet pattern on cream, "
                         "pale green leaves, slightly yellowed, faint water stains", 0.8, 0.4),
    "wallpaper_stripe": ("old vintage wallpaper with vertical stripes, dusty rose and cream with a thin gold line "
                         "between stripes, slightly faded, a few stains", 0.8, 0.4),
    "linoleum": ("worn vintage kitchen linoleum floor with a checkerboard pattern of faded red and cream squares, "
                 "scuffs, scratches", 1.0, 0.6),
    "floorboards": ("old wooden floor boards seen from above, long horizontal warm brown pine planks of different "
                    "tones, scuffed varnish, nail heads, gaps", 1.6, 1.8),
    "rug_woven": ("top view of a handwoven wool rug, persian style pattern, deep red, navy blue and cream geometric "
                  "medallions and borders, worn soft pile", 1.2, 1.2),
    "wool_blanket": ("thick wool blanket fabric, red and black buffalo check plaid, fuzzy woolen texture, close up", 0.5, 1.4),
    "quilt": ("handmade patchwork quilt, small square patches of faded calico prints in blue, red, yellow and green, "
              "visible quilting stitches, cotton fabric", 0.8, 1.2),
    "cushion": ("upholstery fabric, mustard yellow and brown woven tweed with a small herringbone pattern, close up", 0.4, 1.2),
    "ticking": ("mattress ticking fabric, cream cotton with narrow vertical navy blue stripes, slightly stained, close up", 0.5, 0.8),
    "curtain": ("thin cotton curtain fabric, faded blue and white gingham check, soft weave, close up", 0.4, 0.6),
    "tin_rusty": ("rusted corrugated iron sheet, vertical ribs, orange and brown rust over remnants of grey "
                  "galvanising, streaks, holes from nails", 1.2, 2.5),
    "tin_painted": ("corrugated metal sheet painted faded red, vertical ribs, paint peeling showing rust and "
                    "galvanised grey, dust", 1.2, 2.5),
    "tin_flat_rust": ("close up of a flat galvanised steel sheet, dull grey zinc with spreading patches of orange and brown "
                      "rust, thin rust streaks running down, scratches, no ribs, no seams", 2.2, 1.2),
    "tin_flat_red": ("close up of a flat steel sheet painted faded barn red, paint peeling and chalky, rust showing through "
                     "in spots and streaks, no ribs, no seams", 2.2, 1.0),
    "brick_red": ("old red brick wall, running bond, light grey mortar joints, some chipped bricks, varied red and "
                  "orange tones", 1.2, 2.5),
    "brick_dark": ("dark fired clinker brick wall, running bond, purple brown and black bricks, dark mortar, soot", 1.2, 2.5),
    "brick_white": ("whitewashed brick wall, running bond, flaking white lime wash showing red brick underneath in "
                    "patches", 1.2, 2.5),
    "plaster_cracked": ("old cream painted plaster wall, network of hairline cracks, small chips showing grey under "
                        "layer, faint water stains", 1.5, 1.2),
    "concrete": ("weathered poured concrete wall, grey, small air holes, formwork board lines, water stains", 3.0, 1.5),
    "burlap": ("burlap sacking jute fabric, coarse open weave, tan brown, close up", 0.4, 2.0),
    "chicken_wire": ("galvanised chicken wire hexagonal mesh in front of a plain dark background, flat on", 0.4, 2.0),
    "steel_painted": ("sheet steel painted dull olive green, scratches showing bare metal, small rust spots at the "
                      "edges, scuffed and dusty", 1.0, 1.0),
    "enamel": ("close up of a flat cream white vitreous enamel sheet, uniform colour, tiny black chips and pinholes "
               "scattered evenly, faint grime speckles, no edges, no seams", 0.6, 0.6),
    "cast_iron": ("close up of flat dark grey cast iron, uniform fine granular sand-cast texture, small pits, faint "
                  "rust speckles evenly spread, no edges", 0.4, 1.8),
    "tiles_ceramic": ("small square white glazed ceramic kitchen tiles, grey grout lines, slightly dirty and "
                      "crazed glaze", 0.6, 1.5),
    "hay": ("loose dry golden straw hay, dense pile of stalks, top view", 0.6, 2.0),
    "dirt_floor": ("packed earth floor, dry brown dirt with small pebbles, footprints, straw bits, top view", 1.6, 2.0),
    "book_spines": ("a row of old hardcover book spines on a shelf seen straight on, many colours, faded cloth "
                    "bindings, gold lettering, different heights", 0.8, 0.8),
    "labels": ("a strip of vintage paper labels for jars and tins, handwritten and printed old labels side by side, "
               "faded colours, flat", 0.6, 0.3),
    "rug_rag": ("braided rag rug, top view, coiled braids of faded multicoloured cloth strips, blue red and beige", 1.0, 1.6),
    "doormat": ("coir doormat bristles, top view, coarse brown coconut fibre", 0.5, 2.2),
    "painting": ("small naive oil painting of a farmhouse in a green valley at sunset, visible brush strokes, canvas "
                 "texture, slightly cracked varnish", 0.5, 0.3),
    "clock_face": ("vintage wall clock face, cream dial with black roman numerals around the edge, centered, flat on, "
                   "aged paper, perfectly circular dial filling the frame", 0.3, 0.2),
}

NOT_TILED = {"painting", "clock_face", "labels", "book_spines", "rug_woven"}   # pictures: no tile-seam blending
ROTATE = {"rug_woven"}                     # runner-shaped pictures: long axis along u (x)
# small-scale / small-area surfaces are stored at 512 (normal 256) to keep the committed set small
SMALL = {"painting", "clock_face", "labels", "book_spines", "cushion", "curtain", "ticking", "chicken_wire", "enamel",
         "cast_iron", "burlap", "doormat", "wool_blanket", "osb", "plywood", "steel_painted", "hay"}


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
        "9": {"class_type": "SaveImage", "inputs": {"filename_prefix": "madmax_worldtex", "images": ["8", 0]}},
    }


def _post(path, data):
    req = urllib.request.Request(COMFY + path, json.dumps(data).encode(), {"Content-Type": "application/json"})
    return json.loads(urllib.request.urlopen(req, timeout=30).read())


def _get(path):
    return urllib.request.urlopen(COMFY + path, timeout=60).read()


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


def normal_from(im, strength=1.5, size=512):
    """Tangent-space normal (OpenGL, +Y up) from luminance, high-passed so broad shading is not read as slope;
    wraps at the edges (the albedo is tileable)."""
    g = np.asarray(im.convert("L").resize((size, size), Image.LANCZOS), dtype=np.float32) / 255.0
    low = np.asarray(Image.fromarray((g * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(size / 24)),
                     dtype=np.float32) / 255.0
    h = g - low
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5
    s = strength * size / 64.0
    n = np.stack([-dx * s, dy * s, np.ones_like(h)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return Image.fromarray(((n * 0.5 + 0.5) * 255).clip(0, 255).astype(np.uint8), "RGB")


def generate(name, seed=7, size=1024):
    text, _metres, strength = TEXTURES[name]
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
    tmp = os.path.join(OUT, "." + name + "_raw.png")
    with open(tmp, "wb") as fh:
        fh.write(raw)
    im = Image.open(tmp).convert("RGB")
    os.remove(tmp)
    if name not in NOT_TILED:
        im = tileable(im)
    if name in ROTATE:
        im = im.transpose(Image.Transpose.ROTATE_90)
    if name in SMALL:
        size = min(size, 512)
    if im.size[0] != size:
        im = im.resize((size, size), Image.LANCZOS)
    out = os.path.join(OUT, name + ".png")
    im.save(out, optimize=True)
    normal_from(im, strength, size // 2).save(os.path.join(OUT, name + "_n.png"), optimize=True)
    return out


# Regular patterns break under the seam blend of tileable(): their geometry is drawn exactly here (a whole number of
# repeats per tile) and only the wear / weave comes from a generated photo (its high-passed luminance).
PATTERNS = {
    # name: (grain source texture, grain strength)
    "linoleum": ("concrete", 0.55),
    "ticking": ("cushion", 0.35),
    "tiles_ceramic": ("plaster_cracked", 0.35),
    "curtain": ("burlap", 0.3),
    "wallpaper_stripe": ("plaster_cracked", 0.3),
}


def _srgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def pattern(name, size=1024):
    import random
    r = random.Random(name)
    im = Image.new("RGB", (size, size))
    d = ImageDraw.Draw(im)
    if name == "linoleum":                       # 8 x 8 checker, faded red / cream, slight per-square tone
        n = 8
        q = size // n
        for i in range(n):
            for j in range(n):
                base = _srgb("a8392c") if (i + j) % 2 else _srgb("e2d6bc")
                k = r.uniform(0.92, 1.05)
                d.rectangle((i * q, j * q, (i + 1) * q - 1, (j + 1) * q - 1), fill=tuple(int(c * k) for c in base))
    elif name == "ticking":                      # cream with narrow navy stripes (16 repeats)
        d.rectangle((0, 0, size, size), fill=_srgb("e8e0cc"))
        q = size // 16
        for i in range(16):
            x = i * q
            d.rectangle((x + q // 2 - q // 8, 0, x + q // 2 + q // 8, size), fill=_srgb("2c3a5c"))
            d.rectangle((x + q // 2 + q // 4, 0, x + q // 2 + q // 4 + max(1, q // 24), size), fill=_srgb("2c3a5c"))
    elif name == "tiles_ceramic":                # 10 x 10 white glazed tiles, grey grout
        d.rectangle((0, 0, size, size), fill=_srgb("8a8680"))
        n = 10
        q = size // n
        g = max(2, q // 16)
        for i in range(n):
            for j in range(n):
                k = r.uniform(0.95, 1.02)
                d.rectangle((i * q + g, j * q + g, (i + 1) * q - g, (j + 1) * q - g), fill=tuple(int(c * k) for c in _srgb("ece9e2")))
    elif name == "curtain":                      # blue gingham, 12 repeats
        n = 12
        q = size // n
        blue, white = _srgb("4a6ea8"), _srgb("eeeee8")
        a = Image.new("L", (size, size), 0)
        da = ImageDraw.Draw(a)
        for i in range(n):
            da.rectangle((i * q, 0, i * q + q // 2 - 1, size), fill=128)
        b = Image.new("L", (size, size), 0)
        db = ImageDraw.Draw(b)
        for j in range(n):
            db.rectangle((0, j * q, size, j * q + q // 2 - 1), fill=128)
        t = np.asarray(a, np.float32) / 255 + np.asarray(b, np.float32) / 255
        col = np.array(white, np.float32)[None, None] * (1 - t[..., None]) + np.array(blue, np.float32)[None, None] * t[..., None]
        im = Image.fromarray(col.clip(0, 255).astype(np.uint8))
    elif name == "wallpaper_stripe":             # dusty rose / cream stripes with a thin gold line, 8 repeats
        d.rectangle((0, 0, size, size), fill=_srgb("e6d8c4"))
        q = size // 8
        for i in range(8):
            d.rectangle((i * q, 0, i * q + q // 2, size), fill=_srgb("c4949a"))
            d.rectangle((i * q + q // 2 + q // 8, 0, i * q + q // 2 + q // 8 + max(2, q // 40), size), fill=_srgb("b8964c"))
    src, k = PATTERNS[name]
    g = np.asarray(Image.open(os.path.join(OUT, src + ".png")).convert("L").resize((size, size), Image.LANCZOS), np.float32) / 255
    low = np.asarray(Image.fromarray((g * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(size / 16)), np.float32) / 255
    detail = 1.0 + (g - low) * 2.0 * k
    # a few soft stains / fading
    st = np.asarray(Image.open(os.path.join(OUT, src + ".png")).convert("L").resize((size // 16, size // 16)).resize((size, size), Image.BICUBIC), np.float32) / 255
    fade = 1.0 - (st - st.mean()) * 0.25
    out = np.asarray(im, np.float32) * (detail * fade)[..., None]
    im = Image.fromarray(out.clip(0, 255).astype(np.uint8))
    im.save(os.path.join(OUT, name + ".png"), optimize=True)
    normal_from(im, TEXTURES[name][2], size // 2).save(os.path.join(OUT, name + "_n.png"), optimize=True)
    return os.path.join(OUT, name + ".png")


def write_index():
    idx = {n: {"metres": m, "normal": s, "tiled": n not in NOT_TILED} for n, (_, m, s) in TEXTURES.items()}
    json.dump(idx, open(os.path.join(OUT, "index.json"), "w"), indent=1, sort_keys=True)


if __name__ == "__main__":
    args = sys.argv[1:]
    seed, size, force = 100, 1024, "--force" in args
    if "--seed" in args:
        seed = int(args[args.index("--seed") + 1])
    if "--size" in args:
        size = int(args[args.index("--size") + 1])
    names = [a for a in args if not a.startswith("--") and a in TEXTURES] or list(TEXTURES)
    os.makedirs(OUT, exist_ok=True)
    write_index()
    for i, n in enumerate(names):
        if not force and os.path.exists(os.path.join(OUT, n + ".png")):
            continue
        t0 = time.time()
        if n in PATTERNS:
            print(n, pattern(n, 512 if n in SMALL else size), "(pattern)", flush=True)
            continue
        print(n, generate(n, seed=seed + sorted(TEXTURES).index(n), size=size), "%.1fs" % (time.time() - t0), flush=True)
