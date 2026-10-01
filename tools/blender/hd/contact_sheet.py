#!/usr/bin/env python3
"""Compose the HD asset-pack preview: every tile from hd_preview/<group>/tiles.json as
[detailed | pixel mode] pairs with a label, grouped, one PNG per group plus one overview.

  python3 tools/blender/hd/contact_sheet.py [preview_dir] [out_dir]
"""
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

GROUPS = [("character", "CHARACTER"), ("cars", "ROAD CARS"), ("heavy", "HEAVY VEHICLES & MACHINES"),
          ("misc", "BIKES, AIRCRAFT, BOATS, PARTS & WORLD OBJECTS")]
TILE = 256          # each half of a pair
COLS = 4            # pairs per row
PAD = 12
LABEL = 22
BG = (34, 30, 28)
INK = (238, 234, 220)
MUTED = (160, 150, 135)


def font(size, bold=False):
    name = "DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf"
    try:
        return ImageFont.truetype("/usr/share/fonts/truetype/dejavu/" + name, size)
    except OSError:
        return ImageFont.load_default()


def load_tiles(group_dir):
    path = os.path.join(group_dir, "tiles.json")
    if os.path.exists(path):
        tiles = json.load(open(path))
    else:  # fall back to whatever pairs exist
        tiles = [{"name": f[:-4], "label": f[:-4], "replaces": ""} for f in sorted(os.listdir(group_dir))
                 if f.endswith(".png") and not f.endswith("_px.png")]
    out = []
    for t in tiles:
        hi = os.path.join(group_dir, t["name"] + ".png")
        px = os.path.join(group_dir, t["name"] + "_px.png")
        if os.path.exists(hi):
            out.append((t, hi, px if os.path.exists(px) else None))
    return out


def sheet(title, tiles, cols=COLS):
    rows = (len(tiles) + cols - 1) // cols
    cw = TILE * 2 + PAD
    ch = TILE + LABEL * 2 + PAD
    W = PAD + cols * (cw + PAD)
    H = 60 + rows * ch + PAD
    im = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(im)
    d.text((PAD, 16), title, fill=INK, font=font(26, True))
    d.text((W - 420, 24), "left: detailed (vector)   right: pixel mode", fill=MUTED, font=font(14))
    for i, (t, hi, px) in enumerate(tiles):
        x = PAD + (i % cols) * (cw + PAD)
        y = 60 + (i // cols) * ch
        im.paste(Image.open(hi).convert("RGB").resize((TILE, TILE), Image.LANCZOS), (x, y))
        if px:
            im.paste(Image.open(px).convert("RGB").resize((TILE, TILE), Image.NEAREST), (x + TILE + PAD, y))
        d.text((x, y + TILE + 3), t.get("label", t["name"])[:46], fill=INK, font=font(15, True))
        rep = t.get("replaces", "")
        if rep:
            d.text((x, y + TILE + 3 + LABEL), ("replaces " + rep)[:60], fill=MUTED, font=font(12))
    return im


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    prev = sys.argv[1] if len(sys.argv) > 1 else os.path.abspath(os.path.join(here, "../../../../../hd_preview"))
    out = sys.argv[2] if len(sys.argv) > 2 else prev
    sheets = []
    for key, title in GROUPS:
        gd = os.path.join(prev, key)
        if not os.path.isdir(gd):
            continue
        tiles = load_tiles(gd)
        if not tiles:
            continue
        im = sheet(f"{title}  ({len(tiles)})", tiles)
        p = os.path.join(out, f"sheet_{key}.png")
        im.save(p)
        sheets.append((p, im))
        print("wrote", p, im.size, len(tiles), "tiles")
    if sheets:  # overview: groups stacked, scaled to a common width
        w = max(im.size[0] for _, im in sheets)
        scaled = [im.resize((w, int(im.size[1] * w / im.size[0]))) if im.size[0] != w else im for _, im in sheets]
        total = Image.new("RGB", (w, sum(s.size[1] for s in scaled)), BG)
        y = 0
        for s in scaled:
            total.paste(s, (0, y)); y += s.size[1]
        p = os.path.join(out, "sheet_all.png")
        total.save(p)
        print("wrote", p, total.size)


if __name__ == "__main__":
    main()
