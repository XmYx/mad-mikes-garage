"""Unattended acceptance run (roadmap 26, Q1): launches a player build on a disposable profile, supervises it,
and exits with the run's result. No input is needed from launch to the final report.

    python3 tools/acceptance/run.py [--suite fast|full|catalogue|vehicles] [--scenario <id prefixes>] [--skip <id prefixes>] [--quick] [--headless]
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
    ap.add_argument("--scenario", help="id prefixes, comma-separated")
    ap.add_argument("--skip", help="leave out ids starting with these prefixes (comma-separated)")
    ap.add_argument("--quick", action="store_true", help="reuse the world between standard-rule scenarios (faster, less isolated)")
    ap.add_argument("--player", default=os.path.join(ROOT, "Builds", "Linux", "MadMikesGarage.x86_64"))
    ap.add_argument("--timeout", type=float, default=1800)
    ap.add_argument("--headless", action="store_true", help="-batchmode -nographics (no GPU; screenshots are blank)")
    ap.add_argument("--keep", action="store_true", help="keep the disposable profile folder")
    ap.add_argument("--startup", type=float, default=300, help="seconds the player may take to reach the runner (failed startup = exit 3)")
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
    if a.skip:
        cmd += ["-skip", a.skip]
    if a.quick:
        cmd += ["-reuse"]
    if a.headless:
        cmd += ["-batchmode", "-nographics"]
    print("[acceptance]", " ".join(cmd), flush=True)
    env = dict(os.environ)
    try:
        env["MADMAX_COMMIT"] = subprocess.check_output(["git", "-C", ROOT, "rev-parse", "HEAD"], text=True, stderr=subprocess.DEVNULL).strip()
    except (OSError, subprocess.CalledProcessError):
        pass
    # exact replay: the same suite / filter on a fresh disposable profile
    replay = [sys.executable, os.path.abspath(__file__), "--suite", a.suite] + (["--scenario", a.scenario] if a.scenario else []) + \
             (["--skip", a.skip] if a.skip else []) + (["--quick"] if a.quick else []) + (["--headless"] if a.headless else []) + \
             ["--player", a.player, "--timeout", str(int(a.timeout))]
    with open(os.path.join(results, "replay.sh"), "w") as fh:
        fh.write("#!/bin/sh\n# commit " + env.get("MADMAX_COMMIT", "unknown") + "\n" + " ".join(replay) + "\n")
    t0 = time.time()
    log = os.path.join(results, "player.log")
    proc = subprocess.Popen(cmd, stdin=subprocess.DEVNULL, env=env)
    code, started = None, False
    try:
        while code is None:
            try:
                code = proc.wait(timeout=2)
            except subprocess.TimeoutExpired:
                el = time.time() - t0
                if not started and os.path.exists(log):
                    with open(log, encoding="utf-8", errors="replace") as fh:
                        started = "[acceptance]" in fh.read()
                if not started and el > a.startup:
                    print(f"[acceptance] STARTUP: the runner never reported within {a.startup:.0f} s", file=sys.stderr)
                    proc.kill(); proc.wait(); code = 3
                elif el > a.timeout + 180:
                    print(f"[acceptance] TIMEOUT: killed the player after {el:.0f} s", file=sys.stderr)
                    proc.kill(); proc.wait(); code = 3
    finally:
        if proc.poll() is None:
            proc.kill()
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
    print(f"[acceptance] replay: {os.path.join(results, 'replay.sh')}")
    expected = r["exit"]
    if code not in (expected, 0) and code != 3:
        print(f"[acceptance] note: player exit {code} differs from the report's {expected}", file=sys.stderr)
    return expected if code != 3 else 3


sys.exit(main())
