"""Coverage of the HD parts / items pack against the game ids (plain python, no Blender).

Game universe:
  parts  every prefab in Assets/MadMax/Generated/Prefabs/Parts (game_parts.json)
  items  every quoted id with an item prefix in Assets/MadMax/Runtime (tools, food, drink, ammo, media, ...), every
         ClothingLibrary garment as cloth_<id>
HD side: the manifests written by the family scripts (parts_all/manifest/*.json, items/manifest/*.json).
Writes parts_all/COVERAGE.md and prints the gaps.

    python3 tools/blender/hd/parts_all/coverage.py
"""
import glob
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
HD = os.path.dirname(HERE)
ROOT = os.path.abspath(os.path.join(HD, "..", "..", ".."))
RUNTIME = os.path.join(ROOT, "Assets", "MadMax", "Runtime")

PREFIX = r"(food|drink|seed|sapling|book|vhs|ammo|throw|bait|trophy|dye|bp|use|kit|crop|med|animal|coin|misc|relic|farm|vet|keepsake|evidence|story|tool)"
# ids that look like items but are not (with the reason)
NOT_ITEMS = {
    "tool_pipe": "prefix test (ItemCatalog: StartsWith(\"tool_pipe\")) / stale test candidate, not an item",
    "tool_bolt": "prefix test (BagLibrary: StartsWith(\"tool_bolt\")), not an item",
    "tool_rear": "socket name (backhoe rear tool socket)",
    "food_jar_": "prefix test in ItemModels", "food_can_": "prefix test (StartsWith)", "food_pickle": "prefix test in ItemModels", "food_fish": "prefix test in ItemModels",
    "food_price_spread": "acceptance metric name", "story_start": "story system flag", "story_lines": "acceptance metric name",
    "story_cast_in_town": "acceptance metric name", "animal_treatment": "story 'needs' system key (S12), not an item",
    "trophy_mount": "furniture piece (FurnitureLibrary), not an item",
}
PANEL_RE = re.compile(r"_(door|door_rear|rear_door|hood|deck)$")
HEAVY_TOOLS = {"tool_excavator_arm": "heavy/machines.py (Excavator)", "tool_backhoe_loader": "heavy/machines.py (Backhoe)",
               "tool_hoe_arm": "heavy/machines.py (Backhoe)", "tool_dozer_blade": "heavy/machines.py (Bulldozer)",
               "tool_dump_bed": "heavy/machines.py (DumpTruck)", "tool_paver_screed": "heavy/machines.py (Paver)",
               "tool_plough": "heavy/farm.py (Tractor)", "tool_seeder": "heavy/farm.py (implements)", "tool_sprayer": "heavy/farm.py (implements)",
               "tool_harvester": "heavy/farm.py (implements)"}
HEAVY_PANELS = ("hauler_", "bus_", "semi_", "amb_", "apc_")
SHARED = {"kit_": "world_kit (WorldItemModels.Kit: every build kit shares the flat-pack crate)"}


def game_items():
    ids = set()
    for f in glob.glob(os.path.join(RUNTIME, "**", "*.cs"), recursive=True):
        for m in re.finditer(r'"(%s_[a-z0-9_]+)"' % PREFIX, open(f, errors="ignore").read()):
            ids.add(m.group(1))
    for f in glob.glob(os.path.join(RUNTIME, "Game", "Character", "ClothingLibrary*.cs")):
        t = open(f).read()
        for m in re.finditer(r'Def\("([a-z0-9_]+)",\s*"[^"]+",\s*ClothingSlot\.', t):
            ids.add("cloth_" + m.group(1))
        if "Def(BagLibrary.Brace" in t:
            ids.add("cloth_back_brace")
    return ids


def manifests():
    out = {}
    for d in (os.path.join(HD, "parts_all", "manifest"), os.path.join(HD, "items", "manifest")):
        for f in sorted(glob.glob(os.path.join(d, "*.json"))):
            for s in json.load(open(f)):
                out[s["id"]] = s
    return out


def main():
    parts = json.load(open(os.path.join(HERE, "game_parts.json")))
    hd = manifests()
    items = game_items()
    machine_parts = {k for k in items if k in parts}          # tool_plough etc. are parts, not hand tools
    items -= machine_parts
    rows_p, rows_i, gaps = [], [], []
    for k in sorted(parts):
        if k in hd:
            s = hd[k]
            rows_p.append((k, s.get("blend", "?"), "%s tris, tile %s%s" % (s.get("tris"), s.get("tile"), ", segments " + "/".join(s["segments"]) if s.get("segments") else "")))
        elif k in HEAVY_TOOLS:
            rows_p.append((k, "tools/blender/hd/" + HEAVY_TOOLS[k].split(" ")[0], "heavy group: modelled on its machine, origin at the tool socket"))
        elif PANEL_RE.search(k):
            grp = "heavy/trucks.py" if k.startswith(HEAVY_PANELS) else "cars/body.py (+ cars/refbuild.py)"
            rows_p.append((k, "tools/blender/hd/" + grp, "vehicle panel cut from the HD body (Door/Hood objects with socket props)"))
        else:
            rows_p.append((k, "-", "MISSING"))
            gaps.append(k)
    for k in sorted(items):
        if k in hd:
            s = hd[k]
            rows_i.append((k, s.get("blend", "?"), "%s, %s tris, tile %s" % (s.get("family"), s.get("tris"), s.get("tile"))))
        elif k in NOT_ITEMS:
            rows_i.append((k, "-", "not an item: " + NOT_ITEMS[k]))
        elif any(k.startswith(p) for p in SHARED):
            p = next(p for p in SHARED if k.startswith(p))
            rows_i.append((k, "items/blend/world_kit.blend", "shared: " + SHARED[p]))
        else:
            rows_i.append((k, "-", "MISSING"))
            gaps.append(k)
    extra = sorted(set(hd) - set(parts) - items)
    n_hd_p = sum(1 for k in parts if k in hd)
    n_hd_i = sum(1 for k in items if k in hd)
    lines = ["# HD parts & items coverage", "",
             "Generated by `python3 tools/blender/hd/parts_all/coverage.py` from the game data (Generated/Prefabs/Parts, item ids in "
             "Runtime/*.cs, ClothingLibrary) and the family manifests. Paths are relative to `tools/blender/hd/`; .blend files are "
             "gitignored and rebuilt by the family scripts (`blender -b -P <family>.py`).", "",
             "| | game ids | modelled here | elsewhere / shared / not an item | missing |", "|---|---|---|---|---|",
             "| vehicle parts | %d | %d | %d | %d |" % (len(parts), n_hd_p, len(parts) - n_hd_p - sum(1 for g in gaps if g in parts), sum(1 for g in gaps if g in parts)),
             "| items (tools, food, clothing, ...) | %d | %d | %d | %d |" % (len(items), n_hd_i, len(items) - n_hd_i - sum(1 for g in gaps if g in items), sum(1 for g in gaps if g in items)),
             "| extra HD models (world stand-ins) | | %d | | |" % len(extra), "",
             "## Vehicle parts", "", "| part key | asset | notes |", "|---|---|---|"]
    lines += ["| `%s` | %s | %s |" % r for r in rows_p]
    lines += ["", "## Items", "", "| item id | asset | notes |", "|---|---|---|"]
    lines += ["| `%s` | %s | %s |" % r for r in rows_i]
    lines += ["", "## Extra world models", "", "| id | asset | notes |", "|---|---|---|"]
    lines += ["| `%s` | %s | %s |" % (k, hd[k].get("blend"), hd[k].get("label")) for k in extra]
    open(os.path.join(HERE, "COVERAGE.md"), "w").write("\n".join(lines) + "\n")
    print("parts %d/%d here, items %d/%d here, extra %d, gaps %d: %s" % (n_hd_p, len(parts), n_hd_i, len(items), len(extra), len(gaps), gaps))


if __name__ == "__main__":
    main()
