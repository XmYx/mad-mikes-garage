#!/usr/bin/env python3
"""Batch runner for export_hd.py: every HD source .blend -> Models/HD/<group>/<Asset>/, parallel Blender processes,
resumable, with a size report and the asset index the game builders read.

    python3 tools/blender/hd/export/run_export.py [--out DIR] [--jobs N] [--list] [--report] [--force] [filter ...]

Sources: the .blend files committed under tools/blender/hd of this checkout (the HD binaries in Models/HD are not
in git: tools/release.sh renders them from these at release time); override per set with HD_SRC_VEHICLES /
HD_SRC_PARTS / HD_SRC_WORLD / HD_SRC_CHARACTER (a tools/blender/hd folder):
    vehicles   cars/blend/*.blend (cars), heavy/*.blend (heavy), misc/{bike,boat,air}_*.blend (misc vehicles)
    parts      parts_all/blend + items/blend, jobs from parts_all/export_jobs.json (parts, items)
    world      world/*.blend (world: buildings, props, sites, vegetation), furniture/*.blend, animals/*.blend
    character  character/blend/wardrobe_<M|M2|F|F2>.blend (build_wardrobe.py) -> Character_<KEY>
--out      Unity folder (default <this checkout>/Assets/MadMax/Models/HD).
filter     job names or prefixes (e.g. "cars/Sedan", "world/", "items/food_"); default: all jobs.
--force    re-export even when up to date (default: skip jobs whose state records the same .blend content hash and
           export_hd.EXPORTER_VERSION, and inside a job assets whose sidecar is newer than the source).
--check    exit 1 when any job is not up to date (nothing exported).
Writes <out>/hd_index.json (every exported asset), tools/blender/hd/export/hd_report.json, appends to the log file
($HD_EXPORT_LOG, default <repo>/Logs/hd_export.log), per-job Blender output in <repo>/Logs/hd_export/, job state in
<repo>/Logs/hd_export_state/.
"""
import glob
import json
import os
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor, as_completed

HERE = os.path.dirname(os.path.abspath(__file__))
HD = os.path.dirname(HERE)
REPO = os.path.abspath(os.path.join(HD, "..", "..", ".."))
BLENDER = os.environ.get("BLENDER", "/usr/bin/blender")
EXPORTER = os.path.join(HERE, "export_hd.py")
SRC = {
    "vehicles": os.environ.get("HD_SRC_VEHICLES", HD),
    "parts": os.environ.get("HD_SRC_PARTS", HD),
    "world": os.environ.get("HD_SRC_WORLD", HD),
    "character": os.environ.get("HD_SRC_CHARACTER", HD),
}
LOG = os.environ.get("HD_EXPORT_LOG", REPO + "/Logs/hd_export.log")
LOGDIR = REPO + "/Logs/hd_export"
STATE = REPO + "/Logs/hd_export_state"
CAP = {"furniture": 512, "items": 512, "parts": 512, "vegetation": 256}


def log(msg):
    os.makedirs(os.path.dirname(LOG), exist_ok=True)
    line = time.strftime("%Y-%m-%d %H:%M:%S ") + msg
    print(line, flush=True)
    with open(LOG, "a") as f:
        f.write(line + "\n")


def jobs():
    """name -> (group, blend path, extra args)."""
    J = {}
    v = SRC["vehicles"]
    for f in sorted(glob.glob(v + "/cars/blend/*.blend")):
        n = os.path.basename(f)[:-6]
        if not n.endswith(("_exploded", "_wear")):
            J["cars/" + n] = ("cars", f, [])
    for f in sorted(glob.glob(v + "/heavy/*.blend")):
        J["heavy/" + os.path.basename(f)[:-6]] = ("heavy", f, [])
    # misc: only the vehicles; its preview props / furniture / parts are superseded by world/, furniture/, parts_all/
    for f in sorted(glob.glob(v + "/misc/*.blend")):
        n = os.path.basename(f)[:-6]
        if n.split("_")[0] in ("bike", "boat", "air"):
            J["misc/" + n] = ("misc", f, ["--kind", "vehicle", "--size", "2048"])
    p = SRC["parts"]
    ej = os.path.join(p, "parts_all", "export_jobs.json")
    if not os.path.exists(ej):
        ej = os.path.join(HD, "parts_all", "export_jobs.json")
    for e in json.load(open(ej)) if os.path.exists(ej) else []:
        group = "items" if e["blend"].startswith("items/") else "parts"
        a = list(e["args"])
        if "--kind" in a:
            a[a.index("--kind") + 1] = "item" if group == "items" else "part"
        J[group + "/" + e["name"]] = (group, os.path.join(p, e["blend"]), a + ["--max-size", str(CAP[group])])
    w = SRC["world"]
    for f in sorted(glob.glob(w + "/world/*.blend")):
        n = os.path.basename(f)[:-6]
        J["world/" + n] = ("world", f, ["--max-size", str(CAP["vegetation"])] if n.startswith("vegetation") else [])
    for f in sorted(glob.glob(w + "/furniture/*.blend")):
        J["furniture/" + os.path.basename(f)[:-6]] = ("furniture", f, ["--max-size", str(CAP["furniture"])])
    for f in sorted(glob.glob(w + "/animals/*.blend")):
        J["animals/" + os.path.basename(f)[:-6]] = ("animals", f, [])
    for f in sorted(glob.glob(SRC["character"] + "/character/blend/wardrobe_*.blend")):
        k = os.path.basename(f)[len("wardrobe_"):-6]
        J["character/Character_" + k] = ("character", f, ["--collection", "wardrobe_" + k, "--name", "Character_" + k])
    return J


def state_file(name):
    return os.path.join(STATE, name.replace("/", "__") + ".ok")


def blend_hash(blend):
    import hashlib
    h = hashlib.sha1()
    with open(blend, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def up_to_date(name, blend):
    """Same exporter version and the same .blend content as the last good export (a fresh clone's file times say
    nothing); state files without a hash fall back to their time."""
    sf = state_file(name)
    if not os.path.exists(sf):
        return False
    with open(sf) as f:
        lines = f.read().splitlines()
    if not lines or lines[0].strip() != "v%d" % exporter_version():
        return False                              # exporter changes that need a re-export bump EXPORTER_VERSION
    if len(lines) > 1 and lines[1].startswith("sha1:"):
        return lines[1][5:] == blend_hash(blend)
    return os.path.getmtime(sf) > os.path.getmtime(blend)


def exporter_version():
    import re
    m = re.search(r"^EXPORTER_VERSION = (\d+)", open(EXPORTER).read(), re.M)
    return int(m.group(1)) if m else 0


def run(name, job, out, force):
    group, blend, extra = job
    if not os.path.exists(blend):
        return name, "missing", 0.0, "missing " + blend
    if not force and up_to_date(name, blend):
        return name, "skip", 0.0, ""
    t0 = time.time()
    cmd = [BLENDER, "-b", blend, "--python-exit-code", "1", "-P", EXPORTER, "--", "--group", group,
           "--out", os.path.join(out, group)] + extra + ([] if force else ["--skip-fresh"])
    p = subprocess.run(cmd, capture_output=True, text=True)
    os.makedirs(LOGDIR, exist_ok=True)
    with open(os.path.join(LOGDIR, name.replace("/", "__") + ".log"), "w") as f:
        f.write(p.stdout + "\n" + p.stderr)
    lines = [ln for ln in p.stdout.splitlines() if ln.startswith("[hd-export]")]
    done = [ln for ln in lines if " objects, tris " in ln or ": up to date" in ln]
    if p.returncode == 0:
        os.makedirs(STATE, exist_ok=True)
        with open(state_file(name), "w") as f:
            f.write("v%d\nsha1:%s\n" % (exporter_version(), blend_hash(blend)) + "\n".join(done))
        return name, "ok", time.time() - t0, "%d assets" % len(done)
    tail = "\n".join([ln for ln in lines if "FAILED" in ln or "failed" in ln][-5:] + (p.stdout + p.stderr).splitlines()[-12:])
    return name, "fail", time.time() - t0, tail


def index(out):
    """hd_index.json: every exported asset for the game builders."""
    rows = []
    for sc in sorted(glob.glob(os.path.join(out, "*", "*", "*.hd.json"))):
        try:
            s = json.load(open(sc))
        except ValueError:
            continue
        if s.get("exporter", 0) < 2:
            continue                                  # stale export of an older exporter / retired asset
        d = os.path.dirname(sc)
        rel = os.path.relpath(d, os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(out))))).replace(os.sep, "/")
        files = s.get("files", [])
        rows.append({"group": os.path.basename(os.path.dirname(d)), "id": s.get("gameId") or s["asset"], "asset": s["asset"],
                     "kind": s.get("kind", ""), "path": rel, "sidecar": rel + "/" + os.path.basename(sc),
                     "fbx": rel + "/" + s.get("fbx", s["asset"] + ".fbx"), "gameScale": s.get("gameScale", 1.0),
                     "rigged": bool(s.get("bones")), "tris": s.get("tris", [0, 0, 0]),
                     "bytes": sum(f["bytes"] for f in files)})
    json.dump({"format": 1, "generated": time.strftime("%Y-%m-%d %H:%M:%S"), "root": "Assets/MadMax/Models/HD",
               "assets": rows}, open(os.path.join(out, "hd_index.json"), "w"), indent=1)
    return rows


def report(rows):
    by = {}
    for r in rows:
        g = by.setdefault(r["group"], [0, 0, 0])
        g[0] += 1
        g[1] += r["bytes"]
        g[2] += r["tris"][0] if r["tris"] else 0
    json.dump(rows, open(os.path.join(HERE, "hd_report.json"), "w"), indent=1)
    log("%-10s %6s %10s %12s" % ("group", "assets", "MB", "tris lod0"))
    for g, (n, b, t) in sorted(by.items()):
        log("%-10s %6d %10.1f %12d" % (g, n, b / 1e6, t))
    log("total %d assets, %.1f MB" % (len(rows), sum(r["bytes"] for r in rows) / 1e6))


def main():
    a = sys.argv[1:]
    out = os.path.join(REPO, "Assets", "MadMax", "Models", "HD")
    n_jobs, force, check, filt = 6, False, False, []
    i = 0
    while i < len(a):
        if a[i] == "--out":
            out = os.path.abspath(a[i + 1]); i += 1
        elif a[i] == "--jobs":
            n_jobs = int(a[i + 1]); i += 1
        elif a[i] == "--force":
            force = True
        elif a[i] == "--check":
            check = True
        elif a[i] == "--list":
            for k, v in jobs().items():
                print(k, v[1], " ".join(v[2]))
            return
        elif a[i] == "--report":
            report(index(out))
            return
        else:
            filt.append(a[i])
        i += 1
    J = jobs()
    names = [n for n in J if not filt or any(n == f or n.startswith(f) for f in filt)]
    names.sort(key=lambda n: -os.path.getsize(J[n][1]) if os.path.exists(J[n][1]) else 0)   # big files first
    if check:
        stale = [n for n in names if not os.path.exists(J[n][1]) or not up_to_date(n, J[n][1])]
        print("%d of %d HD jobs stale%s" % (len(stale), len(names), (": " + ", ".join(stale[:20])) if stale else ""))
        sys.exit(1 if stale else 0)
    os.makedirs(out, exist_ok=True)
    t0 = time.time()
    log("export start: %d jobs, %d parallel -> %s" % (len(names), n_jobs, out))
    stats = {"ok": 0, "skip": 0, "fail": 0, "missing": 0}
    failed = []
    with ThreadPoolExecutor(max_workers=n_jobs) as ex:
        futs = [ex.submit(run, n, J[n], out, force) for n in names]
        for k, f in enumerate(as_completed(futs), 1):
            name, st, dt, msg = f.result()
            stats[st] += 1
            if st != "skip":
                log("[%d/%d] %-4s %-48s %6.1fs %s" % (k, len(names), st.upper(), name, dt, msg if st != "fail" else ""))
            if st in ("fail", "missing"):
                failed.append(name)
                log("  " + msg.replace("\n", "\n  "))
    rows = index(out)
    report(rows)
    log("export done in %.1f min: %s; failed: %s" % ((time.time() - t0) / 60, stats, failed or "none"))
    if failed:
        sys.exit(1)


if __name__ == "__main__":
    main()
