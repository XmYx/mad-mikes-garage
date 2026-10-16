#!/usr/bin/env python3
"""Swap table for the game: every prop of a published Prop Foundry set that was mapped from an existing HD asset
(`source` in <set>_set.json, map_sets.py) stands in for that asset at runtime (Runtime/Rendering/HDGenSwap.cs).

    python3 tools/visual/props/swaps.py            # writes Assets/MadMax/Resources/HDGenSwaps.txt (Domain|source|Gen_id)

A set counts as published when its blend tools/blender/hd/world/props_gen_<set>.blend exists and the prop's export
Models/HD/world/Gen_<id> does. `noswap: true` on a prop keeps its source: a prop swaps only where the in-game lineup showed it clearly beats the
hand-built model (curated 2026-10-15: furniture, world props, food and story items; see the set JSONs). Vehicle sets (cars, heavy) are not props: their models go through car_conform.py."""
import glob
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
OUT = os.path.join(REPO, "Assets", "MadMax", "Resources", "HDGenSwaps.txt")
# cars / heavy go through car_conform.py; tools and vehicle parts are thin, mechanical shapes TRELLIS reconstructs as
# lumps (tyres shredded, engines blobs, hammers bricks: in-game lineup 2026-10-15) — exported, never swapped
# items_supplies: mottled blobs and wrong shapes (animals as bags, bottles as lumps) — none beat the hand-built ones
SKIP = ("cars", "heavy", "tools", "parts_body", "parts_cargo", "parts_engines", "parts_wheels", "items_supplies")


def main():
    domain = {a["id"]: a["domain"] for a in json.load(open(os.path.join(HERE, "catalog_dump.json")))}
    lines, per = [], {}
    for path in sorted(glob.glob(os.path.join(HERE, "*_set.json"))):
        spec = json.load(open(path))
        s = spec["set"]
        if s in SKIP or not os.path.exists(os.path.join(REPO, "tools", "blender", "hd", "world", "props_gen_%s.blend" % s)):
            continue
        for p in spec["props"]:
            src, gid = p.get("source"), "Gen_" + p["id"]
            if not src or src not in domain or p.get("noswap"):
                continue
            if not os.path.exists(os.path.join(REPO, "Assets", "MadMax", "Models", "HD", "world", gid, gid + ".hd.json")):
                continue
            lines.append("%s|%s|%s" % (domain[src], src, gid))
            per[s] = per.get(s, 0) + 1
    with open(OUT, "w") as f:
        f.write("\n".join(lines) + "\n")
    print("%d swaps -> %s %s" % (len(lines), OUT, per))


if __name__ == "__main__":
    main()
