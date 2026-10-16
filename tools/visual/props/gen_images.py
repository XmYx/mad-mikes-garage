#!/usr/bin/env python3
"""Step 2 of the prop workflow (tools/visual/WORKFLOW.md): Krea-2 Turbo pictures of each prop of a set, one isolated
object on white, through the local ComfyUI (127.0.0.1:8188).

    python3 tools/visual/props/gen_images.py street_set.json [ids...] [--regen] [--seeds=N]

Writes tools/visual/props/<set>/images/<id>.png (1024², the first seed) and <id>_s<k>.png for extra seeds, which the
TRELLIS batch picks from."""
import json
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "..", "blender", "hd", "character"))
import fabric_textures as FT  # noqa: E402  (ComfyUI client: prompt_api, generate)


def main(argv):
    regen = "--regen" in argv
    seeds = next((int(a.split("=")[1]) for a in argv if a.startswith("--seeds=")), 2)
    args = [a for a in argv if not a.startswith("--")]
    spec = json.load(open(os.path.join(HERE, args[0])))
    only = set(args[1:])
    out = os.path.join(HERE, spec["set"], "images")
    os.makedirs(out, exist_ok=True)
    FT.STYLE = spec["style"]
    for i, p in enumerate(spec["props"]):
        if only and p["id"] not in only:
            continue
        for k in range(seeds):
            name = p["id"] + (".png" if k == 0 else "_s%d.png" % k)
            dst = os.path.join(out, name)
            if os.path.exists(dst) and not regen:
                continue
            t0 = time.time()
            im = FT.generate(p["prompt"], seed=9100 + i * 31 + k * 7)
            im.save(dst)
            print("[props] %s %.1fs" % (name, time.time() - t0), flush=True)


if __name__ == "__main__":
    main(sys.argv[1:])
