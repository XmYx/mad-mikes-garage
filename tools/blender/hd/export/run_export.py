#!/usr/bin/env python3
"""Batch runner for export_hd.py: one Blender process per job, then a size report.

    python3 tools/blender/hd/export/run_export.py [--src DIR] [--out DIR] [--jobs N] [--list] [name ...]

--src   folder holding the built .blend files (tools/blender/hd/<group>/...; gitignored, made by the group build
        scripts). Default: $HD_BLEND_ROOT, else this checkout's tools/blender/hd.
--out   Unity folder for the results (default <repo>/Assets/MadMax/Models/HD); assets land in <out>/<group>/<Asset>/.
name    job names from JOBS (default: the slice set). "all" = every job.
Writes tools/blender/hd/export/hd_report.json; Blender logs go to <repo>/Logs/hd_export/.
(bytes per file, triangles per LOD); prints a table.
"""
import json
import os
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor

HERE = os.path.dirname(os.path.abspath(__file__))
HD = os.path.dirname(HERE)
REPO = os.path.abspath(os.path.join(HD, "..", "..", ".."))
BLENDER = os.environ.get("BLENDER", "/usr/bin/blender")

# name: (group, blend relative to --src, extra args)
JOBS = {
    "Sedan": ("cars", "cars/blend/Sedan.blend", []),
    "Pickup": ("cars", "cars/blend/Pickup.blend", []),
    "Fiat126p": ("cars", "cars/blend/Fiat126p.blend", []),
    "DumpTruck": ("heavy", "heavy/DumpTruck.blend", []),
    "Tractor_implements": ("heavy", "heavy/Tractor_implements.blend", []),   # seeder / harvester / sprayer / plough parts (HDVehicleBuilder.SaveGenericParts)
    "BrickHouse": ("misc", "misc/prop_brickhouse.blend", ["--root", "BrickHouse", "--kind", "prop"]),
    "CharacterMale": ("character", "character/hd_characters.blend", ["--collection", "base_male", "--name", "CharacterMale"]),
    "CharacterMale_Starter": ("character", "character/hd_characters.blend", ["--collection", "outfit_starter", "--name", "CharacterMale_Starter"]),
}
SLICE = ["Sedan", "Pickup", "Fiat126p", "DumpTruck", "BrickHouse", "CharacterMale", "CharacterMale_Starter"]


def add_roster(src):
    """Every built .blend becomes a job (cars/heavy by file name, misc files export each top-level object)."""
    for group, sub in (("cars", "cars/blend"), ("heavy", "heavy"), ("misc", "misc")):
        d = os.path.join(src, sub)
        if not os.path.isdir(d):
            continue
        for f in sorted(os.listdir(d)):
            if not f.endswith(".blend") or f.startswith("_"):
                continue
            name = f[:-6]
            if name in JOBS or name.endswith(("_exploded", "_wear", "_implements")):
                continue
            extra = []
            if group == "misc":
                kind = "part" if name.startswith("parts_") else "vehicle" if name.split("_")[0] in ("bike", "boat", "air") else "prop"
                extra = ["--kind", kind] + (["--size", "512"] if kind == "part" else ["--size", "2048"] if kind == "vehicle" else [])
            JOBS[name] = (group, os.path.join(sub, f), extra)


def run(name, src, out):
    group, rel, extra = JOBS[name]
    blend = os.path.join(src, rel)
    if not os.path.exists(blend):
        return name, False, "missing " + blend, 0.0
    t0 = time.time()
    cmd = [BLENDER, "-b", blend, "--python-exit-code", "1", "-P", os.path.join(HERE, "export_hd.py"), "--",
           "--group", group, "--out", os.path.join(out, group)] + extra
    p = subprocess.run(cmd, capture_output=True, text=True)
    logf = os.path.join(REPO, "Logs", "hd_export", name + ".log")
    os.makedirs(os.path.dirname(logf), exist_ok=True)
    with open(logf, "w") as f:
        f.write(p.stdout + "\n" + p.stderr)
    ok = p.returncode == 0
    tail = [ln for ln in p.stdout.splitlines() if ln.startswith("[hd-export]")][-1:] if ok else (p.stdout + p.stderr).splitlines()[-15:]
    return name, ok, "\n".join(tail), time.time() - t0


def report(out):
    rows = []
    for group in sorted(os.listdir(out)):
        gdir = os.path.join(out, group)
        if not os.path.isdir(gdir) or group.startswith("_"):
            continue
        for asset in sorted(os.listdir(gdir)):
            sc = os.path.join(gdir, asset, asset + ".hd.json")
            if not os.path.exists(sc):
                continue
            s = json.load(open(sc))
            files = s.get("files", [])
            fbx = sum(f["bytes"] for f in files if f["file"].endswith(".fbx"))
            tex = sum(f["bytes"] for f in files if f["file"].endswith(".png"))
            rows.append({"group": group, "asset": asset, "fbx": fbx, "png": tex, "tris": s.get("tris"),
                         "atlases": {a["name"]: a["size"] for a in s["atlases"]}})
    json.dump(rows, open(os.path.join(HERE, "hd_report.json"), "w"), indent=1)
    print("%-10s %-24s %9s %9s  %s" % ("group", "asset", "fbx KB", "png KB", "tris lod0/1/2"))
    for r in rows:
        t = r["tris"] or [0, 0, 0]
        print("%-10s %-24s %9.0f %9.0f  %s/%s/%s" % (r["group"], r["asset"], r["fbx"] / 1024, r["png"] / 1024, t[0], t[1], t[2]))
    print("total %.1f MB" % (sum(r["fbx"] + r["png"] for r in rows) / 1e6))


def main():
    a = sys.argv[1:]
    src = os.environ.get("HD_BLEND_ROOT", HD)
    out = os.path.join(REPO, "Assets", "MadMax", "Models", "HD")
    jobs = 2
    names = []
    i = 0
    while i < len(a):
        if a[i] == "--src":
            src = a[i + 1]; i += 1
        elif a[i] == "--out":
            out = a[i + 1]; i += 1
        elif a[i] == "--jobs":
            jobs = int(a[i + 1]); i += 1
        elif a[i] == "--list":
            add_roster(src)
            print("\n".join(sorted(JOBS)))
            return
        elif a[i] == "--report":
            report(out)
            return
        else:
            names.append(a[i])
        i += 1
    if names == ["all"]:
        add_roster(src)
        names = sorted(JOBS)
    elif not names:
        names = SLICE
    else:
        add_roster(src)
    os.makedirs(out, exist_ok=True)
    with ThreadPoolExecutor(max_workers=jobs) as ex:
        for name, ok, msg, dt in ex.map(lambda n: run(n, src, out), names):
            print("%s %-24s %6.1fs  %s" % ("OK " if ok else "ERR", name, dt, msg), flush=True)
    report(out)


if __name__ == "__main__":
    main()
