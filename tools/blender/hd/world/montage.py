#!/usr/bin/env python3
"""Labelled montage of PNG images: python3 montage.py <out.png> <cols> <tile_px> img1.png img2.png ..."""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

out, cols, tile = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
files = sys.argv[4:]
rows = (len(files) + cols - 1) // cols
LAB = 16
sheet = Image.new("RGB", (cols * tile, rows * (tile + LAB)), (34, 30, 28))
d = ImageDraw.Draw(sheet)
try:
    f = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 11)
except OSError:
    f = ImageFont.load_default()
files = [f for f in files if os.path.exists(f)]
for i, p in enumerate(files):
    im = Image.open(p).convert("RGB").resize((tile, tile), Image.LANCZOS)
    x, y = (i % cols) * tile, (i // cols) * (tile + LAB)
    sheet.paste(im, (x, y))
    d.text((x + 3, y + tile + 1), os.path.basename(p)[:-4].split("__")[-1][:tile // 6], fill=(238, 234, 220), font=f)
sheet.save(out)
