#!/usr/bin/env python3
"""HD HUD font atlas: every glyph of PixelCanvas's 3x5 font, rendered from DejaVu Sans Condensed Bold into cells of the
3.6 x 5 logical box the HD text overlay draws (World: Rendering/HudTextGraphic.cs; same advance and layout as the pixel
font). White RGB, coverage in alpha, 16 glyphs per row in CHARS order.

    python3 tools/hud_font.py [out.png]      # default Assets/MadMax/Resources/HDFont/hud_font.png

DejaVu fonts: Bitstream Vera / DejaVu licence (free to embed and redistribute, see CREDITS)."""
import os
import sys
from PIL import Image, ImageDraw, ImageFont

CHARS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ .:/-%<>[]+!#='(),?&`"
CW, CH, PER_ROW = 54, 75, 16
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSansCondensed-Bold.ttf"


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "..", "Assets", "MadMax", "Resources", "HDFont", "hud_font.png")
    rows = (len(CHARS) + PER_ROW - 1) // PER_ROW
    img = Image.new("RGBA", (1024, 512), (255, 255, 255, 0))
    font = ImageFont.truetype(FONT, 88)
    cap = font.getbbox("H")                                    # cap height box
    cap_h = cap[3] - cap[1]
    for i, ch in enumerate(CHARS):
        if ch == " ":
            continue
        g = Image.new("L", (200, 200), 0)
        d = ImageDraw.Draw(g)
        d.text((40, 40 - cap[1]), ch, font=font, fill=255)
        bb = g.getbbox()
        if not bb:
            continue
        # scale so the cap height fills the cell (5 logical rows); punctuation keeps its place on that scale
        k = (CH - 6) / cap_h
        w = bb[2] - bb[0]
        if w * k > CW - 4:
            kx = (CW - 4) / w
        else:
            kx = k
        sub = g.crop((bb[0], 40, bb[2], 40 + int(cap_h * 1.25)))
        sw, sh = max(1, int(round(w * kx))), max(1, int(round(sub.height * k)))
        sub = sub.resize((sw, sh), Image.LANCZOS)
        x0 = (i % PER_ROW) * CW + (CW - sw) // 2
        y0 = (i // PER_ROW) * CH + 3
        cell = Image.new("RGBA", (sw, sh), (255, 255, 255, 0))
        cell.putalpha(sub)
        img.alpha_composite(cell, (x0, y0))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    img.save(out)
    print("[hud-font] %d glyphs, %d rows -> %s" % (len(CHARS), rows, out))


if __name__ == "__main__":
    main()
