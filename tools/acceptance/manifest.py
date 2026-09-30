"""Feature coverage manifest (roadmap 26, Q0).

Every bullet under README "What's in the game" and every vehicle group row is a feature. The manifest
(Assets/StreamingAssets/Acceptance/manifest.json) maps each to scenario ids (exact, or a prefix ending in '*')
or records why it is still a gap. The game reads the same file to write coverage.json after a run.

    python3 tools/acceptance/manifest.py --check    # exit 1 if a README feature is missing from the manifest
    python3 tools/acceptance/manifest.py --update   # add new README features as gaps, keep existing mappings
"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
README = os.path.join(ROOT, "README.md")
MANIFEST = os.path.join(ROOT, "Assets", "StreamingAssets", "Acceptance", "manifest.json")
GAP = "no acceptance scenario yet (roadmap 26 Q2-Q7)"


def slug(text):
    return re.sub(r"[^a-z0-9]+", "_", text.lower()).strip("_")[:48]


def readme_features():
    lines = open(README, encoding="utf-8").read().splitlines()
    feats, group, section = [], None, None
    for line in lines:
        if line.startswith("## "):
            section = line[3:].strip()
            continue
        if section == "What's in the game":
            if line.startswith("### "):
                group = line[4:].strip()
            elif line.startswith("- ") and group:
                text = line[2:].strip()
                feats.append({"group": group, "readme": text[:80]})
            elif group == "Radio" and line.strip() and not line.startswith("#"):
                if not any(f["group"] == "Radio" for f in feats):
                    feats.append({"group": "Radio", "readme": line.strip()[:80]})
        elif section == "Vehicles" and line.startswith("| ") and not line.startswith("| Group") and not line.startswith("|---"):
            cells = [c.strip() for c in line.strip("|").split("|")]
            feats.append({"group": "Vehicles", "readme": (cells[0] + ": " + cells[1])[:80]})
    for f in feats:
        f["id"] = slug(f["group"]) + "." + slug(f["readme"])[:40]
    return feats


def load():
    if not os.path.exists(MANIFEST):
        return {"version": 1, "features": []}
    return json.load(open(MANIFEST, encoding="utf-8"))


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else "--check"
    m = load()
    known = {f["readme"]: f for f in m["features"]}
    missing = [f for f in readme_features() if f["readme"] not in known]
    if mode == "--update":
        for f in missing:
            m["features"].append({"id": f["id"], "group": f["group"], "readme": f["readme"], "scenarios": [], "gap": GAP})
        os.makedirs(os.path.dirname(MANIFEST), exist_ok=True)
        json.dump(m, open(MANIFEST, "w", encoding="utf-8"), indent=2, ensure_ascii=False)
        open(MANIFEST, "a").write("\n")
        print(f"added {len(missing)} features, {len(m['features'])} total")
        return 0
    feats = m["features"]
    covered = sum(1 for f in feats if f.get("scenarios"))
    print(f"{len(feats)} features, {covered} mapped to scenarios, {len(feats) - covered} explicit gaps, {len(missing)} unassigned")
    for f in missing:
        print("UNASSIGNED:", f["group"], "-", f["readme"])
    return 1 if missing else 0


sys.exit(main())
