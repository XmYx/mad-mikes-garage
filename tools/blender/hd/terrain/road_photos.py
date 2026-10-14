#!/usr/bin/env python3
"""Photo sources for the HD road surfaces and decals: Krea-2 Turbo through the local ComfyUI (127.0.0.1:8188).
Tiles (asphalt fresh / worn / cracked / patched, sidewalk concrete, kerb granite, worn road paint) are flattened and
made seamless like the terrain photos; decals (manhole cover, drain grate, oil stain, pothole, sealed crack) are
centred top-down shots that make_textures.py cuts out with a mask. make_textures.py turns both into the road layers
of Resources/HDTerrain (R<k>_<name>_A/_N, World/HDTerrain loads them as the road arrays HDLit _TERRAIN samples).

    python3 tools/blender/hd/terrain/road_photos.py [name ...]          # generate (cached raw reused)
    python3 tools/blender/hd/terrain/road_photos.py --regen [name ...]  # new images

Writes tools/blender/hd/textures/roads/<name>.png (1024 px, sRGB; committed like the terrain photos), originals
cached in ~/.cache/madmax_road_raw."""
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "character"))
import fabric_textures as FT  # noqa: E402  (ComfyUI client, flatten, tileable)
from PIL import Image  # noqa: E402

OUT = os.path.join(os.path.dirname(HERE), "textures", "roads")
RAW = os.path.expanduser("~/.cache/madmax_road_raw")
SIZE = 1024
TILE_STYLE = (", seamless road surface texture photographed straight down from above, flat orthographic top view, even "
              "soft overcast light, no shadows, no horizon, no vehicles, no markings, fills the whole frame edge to edge, "
              "uniform density, highly detailed photogrammetry scan")
DECAL_STYLE = (", photographed straight down from above, flat orthographic top view, centred in the frame, even soft "
               "overcast light, no shadows, no perspective, highly detailed photogrammetry scan")

# name -> (prompt, decal); order = the road layer index (World/HDTerrain.RoadNames, make_textures.ROADS)
PROMPTS = {
    "asphalt_fresh": ("fresh dark city street asphalt, fine black bitumen with small evenly spread grey aggregate chips, "
                      "smooth and even", False),
    "asphalt_worn": ("worn grey city street asphalt, light grey aggregate stones exposed and polished by tyres, faint "
                     "dust in the pores, even wear", False),
    "asphalt_cracked": ("old grey asphalt road with a network of thin cracks, several cracks sealed with glossy black tar "
                        "lines, grey aggregate, a little dust", False),
    "asphalt_patched": ("grey asphalt road with darker rectangular repair patches and tar sealed seams around them, "
                        "mixed grey tones, aggregate stones", False),
    "sidewalk": ("smooth light grey concrete pavement surface, fine sand grain, tiny pits, faint rain stains, no joints",
                 False),
    "kerb": ("light grey granite stone surface, speckled black and white grains, fine flamed finish, even", False),
    "paint": ("worn white thermoplastic road marking paint on asphalt, close up, the white paint chipped and cracked, "
              "small flecks of dark asphalt showing through, mostly white", False),
    "manhole": ("a single round cast iron manhole cover with a raised grid pattern, dark grey iron with rust at the rim, "
                "set flush in dark grey asphalt, the cover fills two thirds of the frame", True),
    "drain": ("a single rectangular cast iron storm drain grate with parallel slots, set in a light concrete gutter, "
              "dark gaps between the bars, rust stains, the grate fills two thirds of the frame", True),
    "oil": ("a dark oil stain on grey asphalt, irregular soft edged blot with darker centre and a faint rainbow sheen, "
            "the stain fills half the frame, plain asphalt around it", True),
    "pothole": ("a single pothole in grey asphalt road, broken jagged edges, loose gravel and dust inside, dark damp "
                "bottom, the pothole fills half the frame, intact asphalt around it", True),
}


def main(argv):
    regen = "--regen" in argv
    names = [a for a in argv if not a.startswith("--")] or list(PROMPTS)
    os.makedirs(OUT, exist_ok=True); os.makedirs(RAW, exist_ok=True)
    for n in names:
        t0 = time.time()
        prompt, decal = PROMPTS[n]
        raw = os.path.join(RAW, n + ".png")
        if os.path.exists(raw) and not regen:
            im = Image.open(raw).convert("RGB")
        else:
            FT.STYLE = DECAL_STYLE if decal else TILE_STYLE
            im = FT.generate(prompt, seed=8100 + list(PROMPTS).index(n))
            im.save(raw)
        if not decal:
            im = FT.tileable(FT.flatten(im, 0.1))
        im.resize((SIZE, SIZE), Image.LANCZOS).save(os.path.join(OUT, n + ".png"), optimize=True)
        print("[road-photo] %s %.1fs" % (n, time.time() - t0), flush=True)


if __name__ == "__main__":
    main(sys.argv[1:])
