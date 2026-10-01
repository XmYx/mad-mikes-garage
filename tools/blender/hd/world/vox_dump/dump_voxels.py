#!/usr/bin/env python3
"""Dump every voxel template the HD assets replace (furniture, world props, stalls, site props, animals) without the
Unity editor: copy the game's C# to a temp dir, patch the few native calls the voxel generators use (mesh upload,
ColorUtility, Mathf.PerlinNoise -> a managed Perlin), compile it as an exe with tools/compile_check.py's references,
run it, then write ../ref_index.json (id -> game dims in metres, voxel size, voxel bounds, material bytes, labels).

    python3 dump_voxels.py [checkout] [vox_out_dir]
Raw dumps (`<group>/<id>.vox.txt`: "x y z rrggbb mat label" per voxel) go to vox_out_dir (default: a temp dir)."""
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
CHECKOUT = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 and sys.argv[1] != "--index" else os.path.join(HERE, "../../../../.."))
sys.path.insert(0, os.path.join(CHECKOUT, "tools"))
import compile_check as cc  # noqa: E402


def patch(root):
    rt = os.path.join(root, "Assets/MadMax/Runtime")
    p = os.path.join(rt, "Voxel/VoxelMesher.cs")
    s = open(p).read()
    s = s.replace("public static Mesh Build(VoxelGrid grid, string name, float size = DefaultSize) => ToMesh(BuildData(grid, size), name);",
                  "public static System.Collections.Generic.List<(string, VoxelGrid, float)> Captured = new System.Collections.Generic.List<(string, VoxelGrid, float)>();\n"
                  "        public static Mesh Build(VoxelGrid grid, string name, float size = DefaultSize) { Captured.Add((name, grid.Clone(), size)); return null; }")
    open(p, "w").write(s)
    p = os.path.join(rt, "Voxel/VoxelGrid.cs")
    s = open(p).read()
    s = s.replace("readonly Dictionary<string, int> labels =", "public readonly Dictionary<string, int> labels =")
    s = s.replace("var g = new VoxelGrid { label = label, material = material };",
                  "var g = new VoxelGrid { label = label, material = material };\n            foreach (var kv in labels) g.labels[kv.Key] = kv.Value;")
    open(p, "w").write(s)
    p = os.path.join(rt, "Voxel/Pal.cs")
    s = open(p).read()
    s = re.sub(r'ColorUtility\.TryParseHtmlString\("#" \+ h, out var c\);\s*return c;',
               'return new Color32(System.Convert.ToByte(h.Substring(0, 2), 16), System.Convert.ToByte(h.Substring(2, 2), 16), '
               'System.Convert.ToByte(h.Substring(4, 2), 16), h.Length >= 8 ? System.Convert.ToByte(h.Substring(6, 2), 16) : (byte)255);', s)
    open(p, "w").write(s)
    for base, _, files in os.walk(os.path.join(root, "Assets")):
        for f in files:
            if f.endswith(".cs"):
                q = os.path.join(base, f)
                s = open(q).read()
                if "Mathf.PerlinNoise(" in s:
                    open(q, "w").write(s.replace("Mathf.PerlinNoise(", "MadMax.DumpShim.Perlin("))
    os.makedirs(os.path.join(rt, "_Dump"), exist_ok=True)
    shutil.copy(os.path.join(HERE, "DumpShim.cs.in"), os.path.join(rt, "_Dump/DumpShim.cs"))


def main():
    tmp = tempfile.mkdtemp(prefix="madmax_vox_")
    vox_out = os.path.abspath(sys.argv[2]) if len(sys.argv) > 2 else os.path.join(tmp, "vox")
    for base, dirs, files in os.walk(os.path.join(CHECKOUT, "Assets")):
        rel = os.path.relpath(base, CHECKOUT)
        for f in files:
            if f.endswith(".cs"):
                os.makedirs(os.path.join(tmp, rel), exist_ok=True)
                shutil.copy(os.path.join(base, f), os.path.join(tmp, rel, f))
    patch(tmp)
    orig = cc.subprocess.run

    def run(cmd, **kw):
        rsp = cmd[-1][1:]
        s = open(rsp).read().replace("-target:library", "-target:exe\n-main:MadMax.Dumper")
        open(rsp, "w").write(s)
        return orig(cmd, **kw)
    cc.subprocess.run = run
    out = os.path.join(tmp, "out")
    os.makedirs(out)
    errs = cc.build("Assembly-CSharp.rsp", tmp, False, [], os.path.join(out, "Dump.dll"))
    cc.subprocess.run = orig
    if errs:
        print("\n".join(errs[:30]))
        sys.exit(1)
    for line in open(os.path.join(out, "Dump.dll.rsp")):
        m = re.match(r'^-r:"(.*)"$', line.strip())
        if m and "TargetingPacks" not in m.group(1) and "/ref/" not in m.group(1) and os.path.exists(m.group(1)):
            dst = os.path.join(out, os.path.basename(m.group(1)))
            if not os.path.exists(dst):
                shutil.copy(m.group(1), dst)
    sa = os.path.join(cc.MAIN, "Library/ScriptAssemblies")
    for f in os.listdir(sa):
        if f.endswith(".dll") and not os.path.exists(os.path.join(out, f)):
            shutil.copy(os.path.join(sa, f), out)
    json.dump({"runtimeOptions": {"tfm": "net8.0", "framework": {"name": "Microsoft.NETCore.App", "version": "8.0.0"}, "rollForward": "Major"}},
              open(os.path.join(out, "Dump.runtimeconfig.json"), "w"))
    subprocess.run([cc.DOTNET, "exec", "Dump.dll", vox_out], cwd=out, check=True)
    index(vox_out)
    print("voxel dumps:", vox_out)


def index(vox):
    ref = {}
    for line in open(os.path.join(vox, "index.tsv")):
        if line.startswith("#"):
            continue
        f = line.rstrip("\n").split("\t")
        if len(f) < 8 or f[2] == "EMPTY":
            continue
        group, gid, n, size, bb, dims = f[:6]
        if group.startswith("animals/") or group == "factories":
            continue
        key = gid[len("Furniture_"):] if group == "furniture_meshes" and gid.startswith("Furniture_") else gid
        if group == "captured":
            key = gid.split(".", 1)[1]
        if key in ref and group != "furniture":
            continue
        lo, hi = [list(map(int, x.strip("[]").split(","))) for x in bb.split("..")]
        ref[key] = {"group": group, "voxels": int(n), "voxel": float(size), "lo": lo, "hi": hi,
                    "dims": [float(x) for x in dims.split("x")], "materials": f[6], "labels": f[7],
                    "info": f[8] if len(f) > 8 else ""}
    animals = [json.loads(l) for l in open(os.path.join(vox, "animals.jsonl"))]
    for a in animals:
        ref["animal:" + a["id"]] = a
    json.dump(ref, open(os.path.join(HERE, "..", "ref_index.json"), "w"), indent=0, sort_keys=True)
    print(len(ref), "entries in ref_index.json")


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "--index":
        index(sys.argv[2])
    else:
        main()
