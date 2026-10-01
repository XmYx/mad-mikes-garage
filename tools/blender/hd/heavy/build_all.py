"""Build every heavy-vehicle model (.blend) and tile in parallel, then write hd_preview/heavy/tiles.json and stats.json.
Run with plain python3: python3 build_all.py [-j 6]"""
import json
import os
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = "/home/magix/PycharmProjects/MadMaxUnity/hd_preview/heavy"
JOBS = [
    ("trucks.py", "Hauler"), ("trucks.py", "Semi"), ("trucks.py", "Bus"), ("trucks.py", "Ambulance"), ("trucks.py", "APC"),
    ("trailers.py", "Tanker"), ("trailers.py", "TankerSmall"), ("trailers.py", "BoxTrailer"), ("trailers.py", "CargoTrailer"),
    ("trailers.py", "CarTrailer"), ("trailers.py", "CarTrailerDouble"),
    ("machines.py", "Excavator"), ("machines.py", "Backhoe"), ("machines.py", "Bulldozer"), ("machines.py", "DumpTruck"),
    ("machines.py", "Paver"), ("machines.py", "Roller"),
    ("farm.py", "Tractor"), ("farm.py", "Tractor_implements"),
]
EXTRA = {  # extra tiles rendered by a job: name -> (label, replaces)
    "Hauler_cutaway": ("Hauler cutaway (living module)", "Hauler walk-in interior: InteriorDesign furniture + cab"),
    "Bus_cutaway": ("Bus cutaway (living area)", "Bus walk-in interior: InteriorDesign furniture + cab"),
    "Excavator_posed": ("Excavator segments posed", "tool_excavator_arm segments boom/stick/bucket + slew"),
}


def run(job):
    script, name = job
    r = subprocess.run(["blender", "-b", "-P", os.path.join(HERE, script), "--", name], capture_output=True, text=True, cwd=HERE)
    ok = r.returncode == 0 and "Traceback" not in r.stderr + r.stdout
    print(("OK  " if ok else "FAIL"), name, flush=True)
    if not ok:
        print(r.stdout[-2000:], r.stderr[-2000:])
    return ok


def main():
    j = int(sys.argv[sys.argv.index("-j") + 1]) if "-j" in sys.argv else 6
    with ThreadPoolExecutor(j) as ex:
        results = list(ex.map(run, JOBS))
    tiles, stats = [], []
    for (_, name) in JOBS:
        p = os.path.join(OUT, "_stats_" + name + ".json")
        if not os.path.exists(p):
            continue
        s = json.load(open(p))
        stats.append(s)
        tiles.append({"name": name, "label": s["label"], "replaces": s["replaces"]})
        for ex_name, (label, rep) in EXTRA.items():
            if ex_name.startswith(name + "_") and os.path.exists(os.path.join(OUT, ex_name + ".png")):
                tiles.append({"name": ex_name, "label": label, "replaces": rep})
    json.dump(tiles, open(os.path.join(OUT, "tiles.json"), "w"), indent=1)
    json.dump(stats, open(os.path.join(OUT, "stats.json"), "w"), indent=1)
    for p in os.listdir(OUT):
        if p.startswith("_stats_"):
            os.remove(os.path.join(OUT, p))
    print("tiles:", len(tiles), "failed:", results.count(False))


if __name__ == "__main__":
    main()
