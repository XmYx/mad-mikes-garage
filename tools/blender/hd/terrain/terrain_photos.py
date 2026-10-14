#!/usr/bin/env python3
"""Photo sources for the HD terrain detail maps: Krea-2 Turbo through the local ComfyUI (127.0.0.1:8188), lighting
flattened and made seamless. make_textures.py turns them into the game's detail albedo (around mid grey, the biome
colour comes from the vertex colour) and normal maps, high-passed so no blotch or tile repeat reads from above.

    python3 tools/blender/hd/terrain/terrain_photos.py [class ...]          # generate (cached raw reused)
    python3 tools/blender/hd/terrain/terrain_photos.py --regen [class ...]  # new images

Writes tools/blender/hd/textures/terrain/<class>.png (1024 px, sRGB; committed like the fabrics), originals cached in
~/.cache/madmax_terrain_raw."""
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "character"))
import fabric_textures as FT  # noqa: E402  (ComfyUI client, flatten, tileable)
from PIL import Image  # noqa: E402

OUT = os.path.join(os.path.dirname(HERE), "textures", "terrain")
RAW = os.path.expanduser("~/.cache/madmax_terrain_raw")
SIZE = 1024
STYLE = (", seamless ground texture photographed straight down from above, flat orthographic top view, even soft "
         "overcast light, no shadows of objects, no horizon, no sky, fills the whole frame edge to edge, uniform "
         "density, highly detailed photogrammetry ground scan")

# TerrainClass order (HDTerrain.Names)
PROMPTS = {
    "sand": "fine desert sand ground with small wind ripples, scattered tiny pebbles and grit, dry",
    "dirt": "dry packed brown dirt ground, small clods, fine cracks, scattered small stones and dry twig bits",
    "grass": "short dense meadow grass seen from above, many thin green and some dry yellow blades, small clover leaves",
    "rock": "weathered flat sandstone rock surface, uniform fine grain, many small evenly spread pits and hairline cracks, lichen specks, no large features",
    "gravel": "loose gravel ground of small rounded and angular stones, mixed grey and tan pebbles, tightly packed",
    "asphalt": "old weathered asphalt road surface, exposed grey aggregate stones in dark bitumen, fine hairline cracks",
    "concrete": "weathered concrete slab surface, fine sand grain, small air holes, faint stains, trowel marks",
    "mud": "wet brown mud ground, smooth puddled patches, squelched footprints and small ridges, glossy wet spots",
    "snow": "fresh packed snow ground, fine granular crust, soft small drifts, subtle sparkle",
}


def main(argv):
    regen = "--regen" in argv
    names = [a for a in argv if not a.startswith("--")] or list(PROMPTS)
    os.makedirs(OUT, exist_ok=True); os.makedirs(RAW, exist_ok=True)
    FT.STYLE = STYLE
    for n in names:
        t0 = time.time()
        raw = os.path.join(RAW, n + ".png")
        if os.path.exists(raw) and not regen:
            im = Image.open(raw).convert("RGB")
        else:
            im = FT.generate(PROMPTS[n], seed=7300 + list(PROMPTS).index(n))
            im.save(raw)
        im = FT.tileable(FT.flatten(im, 0.1)).resize((SIZE, SIZE), Image.LANCZOS)
        im.save(os.path.join(OUT, n + ".png"), optimize=True)
        print("[terrain-photo] %s %.1fs" % (n, time.time() - t0), flush=True)


if __name__ == "__main__":
    main(sys.argv[1:])
