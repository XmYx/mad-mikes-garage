#!/usr/bin/env python3
"""Build the whole HD parts + items pack: every family script in its own Blender process (parallel), then the coverage
table (COVERAGE.md), the export job list for tools/blender/hd/export/run_export.py (export_jobs.json) and one contact
sheet per group in hd_preview/{parts_all,items}/.

    python3 tools/blender/hd/parts_all/build_all.py [--jobs N] [family ...]
"""
import json
import os
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor

HERE = os.path.dirname(os.path.abspath(__file__))
HD = os.path.dirname(HERE)
BLENDER = os.environ.get("BLENDER", "/usr/bin/blender")
PREVIEW = "/home/magix/PycharmProjects/MadMaxUnity/hd_preview"
FAMILIES = [("parts_all", f) for f in ("wheels", "engines", "bumpers", "cargo", "lights_exhaust", "body_weapons", "machine_tools")] + \
           [("items", f) for f in ("tools", "food", "consumables", "clothing", "world_items")]
# atlas size per asset for the exporter (texels on screen: a part spans 0.3-2 m, an item 0.05-1 m)
BIG_PARTS = {"engines", "machine_tools"}


def run(job):
    sub, fam = job
    t0 = time.time()
    logs = os.path.join(HD, "..", "..", "..", "Logs", "hd_parts")
    os.makedirs(logs, exist_ok=True)
    log = os.path.abspath(os.path.join(logs, "%s.log" % fam))
    with open(log, "w") as fh:
        r = subprocess.run([BLENDER, "-b", "-P", os.path.join(HD, sub, fam + ".py")], stdout=fh, stderr=subprocess.STDOUT)
    return fam, r.returncode, time.time() - t0, log


def export_jobs():
    jobs = []
    for sub in ("parts_all", "items"):
        md = os.path.join(HD, sub, "manifest")
        for f in sorted(os.listdir(md)):
            fam = f[:-5]
            for s in json.load(open(os.path.join(md, f))):
                if "blend" not in s:
                    continue
                kind = "part" if s["kind"] == "part" else "prop"
                size = 1024 if fam in BIG_PARTS else 512 if sub == "parts_all" else 256
                jobs.append({"name": s["id"], "group": "cars", "blend": s["blend"],
                             "args": ["--root", s["id"], "--kind", kind, "--size", str(size)], "family": fam})
    json.dump(jobs, open(os.path.join(HERE, "export_jobs.json"), "w"), indent=1)
    return len(jobs)


def rebuild_tiles():
    """tiles.json from the manifests (parallel family runs would race on the shared file)."""
    for sub in ("parts_all", "items"):
        md = os.path.join(HD, sub, "manifest")
        tiles, order = {}, []
        for f in sorted(os.listdir(md)):
            for s in json.load(open(os.path.join(md, f))):
                t = s.get("tile")
                if not t:
                    continue
                if t not in tiles:
                    tiles[t] = []
                    order.append(t)
                tiles[t].append(s)
        out = [{"name": t, "label": ", ".join(s["label"] for s in tiles[t])[:300], "replaces": ", ".join(s["id"] for s in tiles[t])} for t in order]
        json.dump(out, open(os.path.join(PREVIEW, sub, "tiles.json"), "w"), indent=1)
        stale = [f for f in os.listdir(os.path.join(PREVIEW, sub)) if f.endswith(".png") and not f.startswith("sheet_")
                 and f.replace("_px.png", "").replace(".png", "") not in tiles]
        for f in stale:
            os.remove(os.path.join(PREVIEW, sub, f))


def sheets():
    sys.path.insert(0, HD)
    import contact_sheet as cs
    for sub, title in (("parts_all", "HD VEHICLE PARTS (all)"), ("items", "HD TOOLS, WEAPONS & ITEMS (all)")):
        tiles = cs.load_tiles(os.path.join(PREVIEW, sub))
        if tiles:
            im = cs.sheet("%s  (%d tiles)" % (title, len(tiles)), tiles)
            p = os.path.join(PREVIEW, sub, "sheet_%s.png" % sub)
            im.save(p)
            print("wrote", p, im.size)


def main():
    argv = sys.argv[1:]
    n = 4
    if "--jobs" in argv:
        n = int(argv[argv.index("--jobs") + 1])
        del argv[argv.index("--jobs"):argv.index("--jobs") + 2]
    fams = [j for j in FAMILIES if not argv or j[1] in argv]
    with ThreadPoolExecutor(n) as ex:
        for fam, rc, dt, log in ex.map(run, fams):
            print("%-16s %s %5.0fs  %s" % (fam, "ok" if rc == 0 else "FAILED rc=%d" % rc, dt, log))
    subprocess.run([sys.executable, os.path.join(HERE, "coverage.py")])
    print("export jobs:", export_jobs())
    rebuild_tiles()
    sheets()


if __name__ == "__main__":
    main()
