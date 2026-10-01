#!/usr/bin/env python3
"""Harness self-test (roadmap 26 Q1): the acceptance runner must turn a false assertion, a missing prerequisite,
an exception (thrown or logged), a blown budget and a stuck scenario into bounded, nonzero, explained results,
and a stalled main thread into exit 3.

  python3 tools/acceptance/selftest.py                 # run the "harness" suite on the player build (run.py)
  python3 tools/acceptance/selftest.py --hang          # also the 150 s main-thread stall (expects exit 3)
  python3 tools/acceptance/selftest.py --results DIR   # only check an existing results folder (e.g. an editor run)
"""
import argparse
import glob
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))

# id -> (outcome, text the reason must contain)
EXPECT = {
    "harness.pass": ("PASS", None),
    "harness.fail": ("FAIL", "deliberately false"),
    "harness.block": ("BLOCKED", "deliberately missing"),
    "harness.throw": ("FAIL", "threw"),
    "harness.budget": ("FAIL", "within budget"),
    "harness.timeout": ("FAIL", "timeout"),
    "harness.logged_exception": ("FAIL", "unhandled exception"),
}


def check(results_dir):
    path = os.path.join(results_dir, "results.json")
    if not os.path.exists(path):
        print(f"[selftest] FAIL: no results.json in {results_dir}")
        return False
    r = json.load(open(path, encoding="utf-8"))
    got = {s["id"]: s for s in r["scenarios"]}
    ok = True
    for sid, (outcome, needle) in EXPECT.items():
        s = got.get(sid)
        if s is None:
            print(f"[selftest] FAIL {sid}: missing from the report"); ok = False; continue
        reason = s.get("reason") or ""
        good = s["outcome"] == outcome and (needle is None or needle in reason)
        good = good and len(s.get("step_times", [])) == len(s.get("log", []))
        print(f"[selftest] {'ok  ' if good else 'FAIL'} {sid}: {s['outcome']} ({reason or 'no reason'})")
        ok &= good
    if r.get("exit") != 1:
        print(f"[selftest] FAIL: the report's exit is {r.get('exit')}, expected 1 (failures present)"); ok = False
    if not os.path.exists(os.path.join(results_dir, "junit.xml")):
        print("[selftest] FAIL: no junit.xml"); ok = False
    return ok


def run(suite, timeout):
    before = set(glob.glob(os.path.join(ROOT, "Temp", "acceptance", "*")))
    code = subprocess.call([sys.executable, os.path.join(HERE, "run.py"), "--suite", suite, "--timeout", str(timeout)])
    new = sorted(set(glob.glob(os.path.join(ROOT, "Temp", "acceptance", "*"))) - before)
    return code, (new[-1] if new else None)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--results")
    ap.add_argument("--hang", action="store_true")
    a = ap.parse_args()
    if a.results:
        return 0 if check(a.results) else 1
    code, folder = run("harness", 300)
    if code == 4:
        print("[selftest] no player build: build it with MadMax > Build Linux Player"); return 4
    ok = code == 1 and folder is not None and check(folder)
    if code != 1:
        print(f"[selftest] FAIL: run.py exit {code}, expected 1")
    if a.hang:
        hcode, _ = run("harness_hang", 120)
        print(f"[selftest] {'ok  ' if hcode == 3 else 'FAIL'} harness.hang: exit {hcode} (expected 3)")
        ok &= hcode == 3
    print("[selftest]", "PASSED" if ok else "FAILED")
    return 0 if ok else 1


sys.exit(main())
