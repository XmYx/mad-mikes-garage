#!/usr/bin/env python3
"""Map every Blender-made HD asset (catalog_dump.json, dumped from the editor's Resources/HDGen) plus the HD vehicles
into Prop Foundry sets, one per category, with a drafted prompt, real size (longest side of the HD bounds), mount and
a face budget. Flat / simple / architectural pieces are left out (EXCLUDE). Vehicle sets are created on hold: the
dashboard generates nothing for them until "approve" is pressed. Existing sets keep their edited prompts and seeds.

    python3 tools/visual/props/map_sets.py            # writes/merges <set>_set.json"""
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
STYLE = json.load(open(os.path.join(HERE, "street_set.json")))["style"]

EXCLUDE = re.compile(r"^(wall_|floor_|roof_|ramp_|doorway_|foundation_|fence_|plate_|city_wall|city_deck|city_railing|city_ramp|city_stairs|"
                     r"city_ladder|city_gangway|city_cabin|city_pod|city_banner|stairs$|ladder$|post$|curb$|guard_rail|garage_frame|gate_frame|"
                     r"jump_ramp|bridge_|sea_dome|sea_tunnel|shore_entrance|shutter|rig_frame|cable$|pipe$|drip_line|pond_liner|pool_tarp|"
                     r"field_bed|rug$|painting$|sign|flag$|light_switch|light_strip|barbed_wire|tripwire|landmine|spike_wall|sandbag_wall|"
                     r"hangar$|slipway$|garden_plot|"
                     r"BrickHouse|Farmhouse|Shop\d|Tower\d|Warehouse|Shack\d|GuardHut|Site_Bunker_(Wall|WallOuter|Doorway|Pillar|RampWall|Roof|Entry)|"
                     r"Site_Tunnel_(Wall|Arch|Portal)|Site_Airfield_Hangar|Fence|ChainLink|Hedge|Graffiti|Litter|Pond|Gen_|"
                     r"cloth_|pros_|book_|vhs_|bp_|seed_|coin_|limb_|keepsake)", re.I)

HINT = {
    "furn_household": "household furniture", "furn_lights": "light fitting", "furn_utility": "utility equipment",
    "furn_industry": "workshop machine, scrap-built", "furn_garden": "garden and farm equipment", "furn_defence": "defensive structure, scrap-built",
    "furn_decor": "decoration", "world_street": "street furniture", "world_nature": "desert wasteland plant",
    "world_rocks": "rock", "world_sites": "abandoned military site prop", "items_food": "food item",
    "items_supplies": "survival supply item", "items_story": "personal object", "tools": "hand tool or weapon",
    "parts_engines": "car engine part", "parts_body": "car body part, aftermarket", "parts_cargo": "vehicle cargo rack accessory",
    "parts_wheels": "vehicle wheel with tyre", "cars": "car, whole vehicle", "heavy": "heavy vehicle, whole vehicle",
}


def words(s):
    s = re.sub(r"\d+$", "", s).replace("_", " ")
    return re.sub(r"(?<=[a-z])(?=[A-Z])", " ", s).strip().lower()


def set_for(a):
    i, d, c = a["id"], a["domain"], a["cat"]
    if d == "Furniture":
        if i.startswith(("light_", "lamp", "city_lamp", "city_lantern", "city_floodlight", "city_searchlight", "city_string", "floodlight", "mine_lamp", "streetlamp", "porch_lights", "candles")):
            return "furn_lights"
        return {"Furniture": "furn_household", "Utility": "furn_utility", "Industry": "furn_industry", "Garden": "furn_garden",
                "Defence": "furn_defence", "Decor": "furn_decor", "Structure": "furn_household", "Vehicle": "parts_body"}.get(c)
    if d == "World":
        if re.match(r"(Tree|Pine|Palm|Bush|Fern|Cactus|ParkTree|Tumbleweed|Haystack|Log)", i):
            return "world_nature"
        if re.match(r"(Rock|Ore_|Rubble|Site_Tunnel_Boulders)", i):
            return "world_rocks"
        if i.startswith("Site_"):
            return "world_sites"
        return "world_street"
    if d == "Tool":
        return "tools"
    if d == "Item":
        if i.startswith(("food_", "drink_", "crop_")):
            return "items_food"
        if i.startswith(("story_", "relic_", "evidence_", "trophy_", "world_")):
            return "items_story"
        return "items_supplies"
    if d == "Part":
        if i.startswith(("engine_", "radiator_", "exhaust_")):
            return "parts_engines"
        if i.startswith("wheel_"):
            return "parts_wheels"
        if i.startswith("cargo_"):
            return "parts_cargo"
        return "parts_body"
    return None


def mount_of(i):
    if re.search(r"(light_wall|mirror|medicine_cabinet|wall_clock|trophy_mount|weapon_rack|alarm_bell)", i):
        return "wall"
    if re.search(r"(light_ceiling|light_pendant)", i):
        return "ceiling"
    return "floor"


def faces_of(size, s):
    if s in ("cars", "heavy"):
        return 16000
    return 1500 if size < 0.3 else 2500 if size < 0.8 else 4000 if size < 2 else 7000


def vehicles():
    out = []
    for grp in ("cars", "heavy"):
        root = os.path.join(REPO, "Assets", "MadMax", "Models", "HD", grp)
        for n in sorted(os.listdir(root)):
            j = os.path.join(root, n, n + ".hd.json")
            if not os.path.isfile(j):
                continue
            side = json.load(open(j))
            mn, mx = side.get("boundsMin", [0, 0, 0]), side.get("boundsMax", [1, 1, 1])
            out.append({"id": n, "domain": "Vehicle", "set": grp, "name": "", "desc": "", "size": [mx[k] - mn[k] for k in range(3)]})
    return out


def main():
    dump = json.load(open(os.path.join(HERE, "catalog_dump.json")))
    sets, seen = {}, set()
    for a in dump + vehicles():
        if EXCLUDE.search(a["id"]) or a["domain"] == "Animal":
            continue
        s = a.get("set") or set_for(a)
        if not s:
            continue
        pid = re.sub(r"[^a-z0-9_]", "_", re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "_", a["id"]).lower()).strip("_")
        if pid in seen:
            pid = s.split("_")[-1] + "_" + pid
        seen.add(pid)
        size = round(max(max(a["size"]), 0.05), 2)
        name = (a.get("name") or "").strip()
        label = name.lower() if name and a["domain"] != "Vehicle" else words(a["id"])
        prompt = "%s, %s, salvaged and weathered" % (label, HINT[s])
        sets.setdefault(s, []).append({"id": pid, "prompt": prompt, "size_m": size, "mount": mount_of(a["id"]),
                                       "faces": faces_of(size, s), "source": a["id"], "note": a.get("desc", "")[:160]})
    for s, props in sorted(sets.items()):
        path = os.path.join(HERE, s + "_set.json")
        old = json.load(open(path)) if os.path.exists(path) else None
        keep = {p["id"]: p for p in (old or {}).get("props", [])}
        ids = {q["id"] for q in props}
        merged = [keep.get(p["id"], p) for p in props] + [p for p in keep.values() if p["id"] not in ids]
        spec = {"set": s, "style": (old or {}).get("style", STYLE), "props": merged}
        if s in ("cars", "heavy"):
            spec["hold"] = (old or {}).get("hold", True)
            if not old:
                spec["style"] = STYLE.replace("single isolated object, ", "single isolated vehicle, side three-quarter view, ")
        json.dump(spec, open(path, "w"), indent=1)
        print("%-16s %3d%s" % (s, len(merged), "  (on hold)" if spec.get("hold") else ""))


if __name__ == "__main__":
    main()
