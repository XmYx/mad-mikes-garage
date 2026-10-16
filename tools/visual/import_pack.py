#!/usr/bin/env python3
"""Bring the generated HD pack of the sister project (CyberNinja, Krea-2 Turbo, same pipeline) into this game.

    python3 tools/visual/import_pack.py [--src <HDPack dir>]

* Decals (graffiti, stickers, notices, rugs, wall hangings, old-world brand posters / signs / billboards / logos) ->
  Assets/MadMax/Resources/Decals/<id>.png (RGBA, longest side 512) + catalog.json {decals: [{id, kind, w, h, tex}]},
  w/h in metres (real sizes by kind, aspect from the manifest). WallDecals places them.
* Runtime finishes (RUNTIME_SETS) also -> Assets/MadMax/Resources/Interior/<id>_v<k>_{albedo,normal} (InteriorFinish).
* Interior surfaces (floor tile, terrazzo, plaster, wood, carpet, cream panel, brick, concrete, steel, grating ...) ->
  tools/blender/hd/textures/interior/<id>_v<k>_{albedo,normal,rough}.<ext> at 1024 for the HD building bakes
  (buildings.py), with interior.json {surfaces: [{id, tile_m, variants}]}.
Nothing in the source is modified."""
import json
import os
import shutil
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = "/home/magix/PycharmProjects/CyberNinja/CyberNinja/Assets/StreamingAssets/HDPack"
DECALS = os.path.join(ROOT, "Assets/MadMax/Resources/Decals")
INTERIOR = os.path.join(ROOT, "tools/blender/hd/textures/interior")
RUNTIME = os.path.join(ROOT, "Assets/MadMax/Resources/Interior")
# finishes InteriorFinish lays at runtime (floors, wall panels): albedo + normal at 1024
RUNTIME_SETS = ["floor_tile", "terrazzo", "wood", "carpet", "concrete_deck", "plaster", "cream_panel", "brick", "grating"]

# height in metres by kind (width from the manifest aspect = w / h)
HEIGHT = {"decal": 1.0, "rug": 2.0, "poster": 0.9, "billboard": 2.4, "sign": 0.5, "logo": 0.6}
HEIGHT_BY_SET = {"sticker": 0.14, "notice": 0.3, "graffiti": 1.3, "rug_square": 1.6, "rug_runner": 2.2, "wall_hanging": 1.2}
SURFACES = ["floor_tile", "terrazzo", "plaster", "wood", "carpet", "cream_panel", "brick", "concrete_pale", "concrete_deck",
            "steel_worn", "grating", "painted_metal", "rust_plate", "corrugated", "cardboard", "hazard_stripes", "rubber", "canvas"]
SKIP_DECAL_SETS = ("_kiosk", "_label")


def fit(im, size):
    w, h = im.size
    s = size / max(w, h)
    return im.resize((max(1, round(w * s)), max(1, round(h * s))), Image.LANCZOS) if s < 1 else im


def main(argv):
    src = argv[argv.index("--src") + 1] if "--src" in argv else SRC
    man = json.load(open(os.path.join(src, "manifest.json")))
    os.makedirs(DECALS, exist_ok=True)
    os.makedirs(INTERIOR, exist_ok=True)
    decals, surfaces = [], []
    for s in man["sets"]:
        sid, kind = s["id"], s["kind"]
        if s["category"] in ("decorations", "brands") and not sid.endswith(SKIP_DECAL_SETS):
            for k, v in enumerate(s["variants"]):
                name = "%s_v%d" % (sid, k)
                im = Image.open(os.path.join(src, v["albedo"])).convert("RGBA")
                im = fit(im, 512)
                im.save(os.path.join(DECALS, name + ".png"), optimize=True)
                h = HEIGHT_BY_SET.get(sid, HEIGHT.get(kind, 1.0))
                a = s.get("aspect") or im.size[0] / im.size[1]
                decals.append({"id": name, "set": sid, "kind": kind, "brand": s.get("brand", ""), "w": round(h * a, 3), "h": h, "tex": "Decals/" + name})
        elif s["category"] == "surfaces" and sid in SURFACES:
            vs = []
            for k, v in enumerate(s["variants"]):
                out = {}
                for ch in ("albedo", "normal", "rough"):
                    if ch not in v:
                        continue
                    p = os.path.join(src, v[ch])
                    ext = os.path.splitext(p)[1]
                    dst = os.path.join(INTERIOR, "%s_v%d_%s%s" % (sid, k, ch, ext))
                    im = Image.open(p)
                    if max(im.size) > 1024:
                        fit(im, 1024).save(dst, quality=92)
                    else:
                        shutil.copyfile(p, dst)
                    out[ch] = os.path.basename(dst)
                    if sid in RUNTIME_SETS and ch in ("albedo", "normal"):
                        os.makedirs(RUNTIME, exist_ok=True)
                        shutil.copyfile(dst, os.path.join(RUNTIME, os.path.basename(dst)))
                vs.append(out)
            surfaces.append({"id": sid, "tile_m": s.get("tileMeters", 2.0), "variants": vs})
    json.dump({"source": "CyberNinja HD pack (Krea-2 Turbo)", "decals": decals}, open(os.path.join(DECALS, "catalog.json"), "w"), indent=1)
    json.dump({"source": "CyberNinja HD pack (Krea-2 Turbo)", "surfaces": surfaces}, open(os.path.join(INTERIOR, "interior.json"), "w"), indent=1)
    print("[import_pack] %d decals, %d surface sets" % (len(decals), len(surfaces)))


if __name__ == "__main__":
    main(sys.argv[1:])
