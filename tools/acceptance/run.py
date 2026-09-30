"""Unattended acceptance run (roadmap 26, Q1): launches a player build on a disposable profile, supervises it,
and exits with the run's result. No input is needed from launch to the final report.

    python3 tools/acceptance/run.py [--suite fast|full|catalogue|vehicles] [--scenario <id prefix>]
                                    [--player Builds/Linux/MadMikesGarage.x86_64] [--timeout 1800] [--keep]

Exit codes: 0 all passed, 1 a scenario failed, 2 only blocked scenarios, 3 hang/crash/timeout (no trustworthy
result), 4 infrastructure (no player build). Results land in Temp/acceptance/<timestamp>/ (results.json,
junit.xml, coverage.json, player.log, failure screenshots). Build the player first with MadMax > Build Linux Player.
"""
import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--suite", default="fast")
    ap.add_argument("--scenario")
    ap.add_argument("--player", default=os.path.join(ROOT, "Builds", "Linux", "MadMikesGarage.x86_64"))
    ap.add_argument("--timeout", type=float, default=1800)
    ap.add_argument("--keep", action="store_true", help="keep the disposable profile folder")
    a = ap.parse_args()

    if not os.path.isfile(a.player):
        print(f"[acceptance] INFRASTRUCTURE: no player at {a.player}. Build it with MadMax > Build Linux Player.", file=sys.stderr)
        return 4
    stamp = time.strftime("%Y%m%d-%H%M%S")
    results = os.path.join(ROOT, "Temp", "acceptance", stamp)
    os.makedirs(results, exist_ok=True)
    profile = tempfile.mkdtemp(prefix="madmax-acceptance-")
    cmd = [a.player, "-acceptance", a.suite, "-results", results, "-profiledir", profile, "-runtimeout", str(int(a.timeout)),
           "-mute", "-no-intro", "-logFile", os.path.join(results, "player.log"), "-screen-fullscreen", "0", "-screen-width", "1280", "-screen-height", "720"]
    if a.scenario:
        cmd += ["-scenario", a.scenario]
    print("[acceptance]", " ".join(cmd), flush=True)
    t0 = time.time()
    proc = subprocess.Popen(cmd, stdin=subprocess.DEVNULL)
    try:
        code = proc.wait(timeout=a.timeout + 180)
    except subprocess.TimeoutExpired:
        proc.kill()
        proc.wait()
        print(f"[acceptance] TIMEOUT: killed the player after {time.time() - t0:.0f} s", file=sys.stderr)
        code = 3
    finally:
        if not a.keep:
            shutil.rmtree(profile, ignore_errors=True)

    report = os.path.join(results, "results.json")
    if not os.path.exists(report):
        print(f"[acceptance] no results.json (player exit {code}): treating as a crash", file=sys.stderr)
        return 3
    r = json.load(open(report, encoding="utf-8"))
    c = r["counts"]
    print(f"[acceptance] {r['suite']}: {c['pass']} passed, {c['fail']} failed, {c['blocked']} blocked in {time.time() - t0:.0f} s -> {results}")
    for s in r["scenarios"]:
        if s["outcome"] != "PASS":
            print(f"  {s['outcome']:8} {s['id']}: {s['reason']}")
    cov = os.path.join(results, "coverage.json")
    if os.path.exists(cov):
        k = json.load(open(cov, encoding="utf-8"))["counts"]
        print(f"[acceptance] coverage: {k['covered']} of {k['features']} features covered, {k['gap']} explicit gaps, {k['failing_or_blocked']} failing/blocked")
    expected = r["exit"]
    if code not in (expected, 0) and code != 3:
        print(f"[acceptance] note: player exit {code} differs from the report's {expected}", file=sys.stderr)
    return expected if code != 3 else 3


sys.exit(main())
