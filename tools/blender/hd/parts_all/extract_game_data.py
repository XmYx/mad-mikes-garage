"""Extract the game-side sizes the HD parts must match (plain python, no Blender).

Reads Assets/MadMax/Generated/Meshes/Part_*.asset (local AABB of every part mesh and of every segment mesh, in game
metres, segment meshes relative to their pivot), the part prefabs (segment pivots / parents) and the Make(...) calls in
Runtime/Designs (category, mass, size class). Writes game_parts.json next to this script.

    python3 tools/blender/hd/parts_all/extract_game_data.py
"""
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
MAD = os.path.join(ROOT, "Assets", "MadMax")
MESHES = os.path.join(MAD, "Generated", "Meshes")
PREFABS = os.path.join(MAD, "Generated", "Prefabs", "Parts")
DESIGNS = os.path.join(MAD, "Runtime", "Designs")

VEC = re.compile(r"\{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}")


def aabb(path):
    t = open(path).read()
    m = re.search(r"m_LocalAABB:\s*\n\s*m_Center: (\{[^}]*\})\s*\n\s*m_Extent: (\{[^}]*\})", t)
    c = [float(v) for v in VEC.match(m.group(1)).groups()]
    e = [float(v) for v in VEC.match(m.group(2)).groups()]
    return [round(c[i] - e[i], 4) for i in range(3)], [round(c[i] + e[i], 4) for i in range(3)]


def prefab_tree(path):
    t = open(path).read()
    names, trs = {}, []
    for d in re.split(r"^--- ", t, flags=re.M):
        m = re.match(r"!u!(\d+) &(\d+)", d)
        if not m:
            continue
        k, i = m.groups()
        if k == "1":
            names[i] = re.search(r"m_Name: (.*)", d).group(1).strip()
        elif k == "4":
            go = re.search(r"m_GameObject: \{fileID: (\d+)", d).group(1)
            pos = [float(v) for v in VEC.search(re.search(r"m_LocalPosition: (\{.*?\})", d).group(1)).groups()]
            par = re.search(r"m_Father: \{fileID: (\d+)", d).group(1)
            trs.append((i, go, pos, par))
    tid = {i: names[go] for i, go, _, _ in trs}
    out = {}
    for i, go, pos, par in trs:
        if par != "0":
            out[names[go]] = {"parent": tid.get(par), "pivot": [round(v, 4) for v in pos]}
    return out


def makes():
    """key -> (category, mass, size) from Make("key", PartCategory.X, g, mass, size...)."""
    res = {}
    pat = re.compile(r'Make\("([a-z0-9_]+)",\s*PartCategory\.(\w+),\s*\w+,\s*([\d.]+)f?(?:,\s*(\d+))?')
    for f in os.listdir(DESIGNS):
        if f.endswith(".cs"):
            for m in pat.finditer(open(os.path.join(DESIGNS, f)).read()):
                res[m.group(1)] = {"category": m.group(2), "mass": float(m.group(3)), "size": int(m.group(4) or 1)}
    return res


def main():
    mk = makes()
    parts = {}
    for f in sorted(os.listdir(PREFABS)):
        if not f.endswith(".prefab"):
            continue
        key = f[:-7]
        segs = prefab_tree(os.path.join(PREFABS, f))
        entry = {"segments": {}}
        mp = os.path.join(MESHES, "Part_%s.asset" % key)
        if os.path.exists(mp):
            entry["bounds"] = aabb(mp)
        for s, info in segs.items():
            sp = os.path.join(MESHES, "Part_%s_%s.asset" % (key, s))
            if os.path.exists(sp):
                info["bounds"] = aabb(sp)
            entry["segments"][s] = info
        entry.update(mk.get(key, {}))
        parts[key] = entry
    json.dump(parts, open(os.path.join(HERE, "game_parts.json"), "w"), indent=1, sort_keys=True)
    print(len(parts), "parts")


if __name__ == "__main__":
    main()
