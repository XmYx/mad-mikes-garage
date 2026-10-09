"""Export clips without Blender: the game JSON straight from `clips.CLIPS` (numpy only).

Run:  python3 tools/blender/hd/character/export_clips.py <out_dir> [--clips sledge,flinch] [--compare <dir>] [--strip <png_dir>]

`anims.py` keys the clips on the HD armature and samples Blender's curves; the round trip there is exact
(game Euler -> basis quaternion -> game quaternion), so the curves are what matter. This reproduces them: quaternion
components keyed per bone (shortest path between keys), Bezier with AUTO_CLAMPED handles (Catmull-Rom slopes, flat at
extremes and at the ends of one-shots, cyclic for loops), sampled at 30 fps; pelvis offsets the same way.
`--compare` reports the largest angle against clips exported by anims.py (the same names, nothing written), `--strip`
draws side/front stick-figure filmstrips (matplotlib) with the held sledge, for checking the motion by eye.
"""
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import clips as C  # noqa: E402

FPS = 30


def quat(R):
    """Rotation matrix -> (x, y, z, w)."""
    m = R
    tr = m[0, 0] + m[1, 1] + m[2, 2]
    if tr > 0:
        s = math.sqrt(tr + 1.0) * 2
        w = 0.25 * s; x = (m[2, 1] - m[1, 2]) / s; y = (m[0, 2] - m[2, 0]) / s; z = (m[1, 0] - m[0, 1]) / s
    elif m[0, 0] > m[1, 1] and m[0, 0] > m[2, 2]:
        s = math.sqrt(1.0 + m[0, 0] - m[1, 1] - m[2, 2]) * 2
        w = (m[2, 1] - m[1, 2]) / s; x = 0.25 * s; y = (m[0, 1] + m[1, 0]) / s; z = (m[0, 2] + m[2, 0]) / s
    elif m[1, 1] > m[2, 2]:
        s = math.sqrt(1.0 + m[1, 1] - m[0, 0] - m[2, 2]) * 2
        w = (m[0, 2] - m[2, 0]) / s; x = (m[0, 1] + m[1, 0]) / s; y = 0.25 * s; z = (m[1, 2] + m[2, 1]) / s
    else:
        s = math.sqrt(1.0 + m[2, 2] - m[0, 0] - m[1, 1]) * 2
        w = (m[1, 0] - m[0, 1]) / s; x = (m[0, 2] + m[2, 0]) / s; y = (m[1, 2] + m[2, 1]) / s; z = 0.25 * s
    return np.array([x, y, z, w])


def slopes(ts, vs, cyclic):
    """AUTO_CLAMPED tangents: Catmull-Rom (non-uniform), zero at local extremes; ends flat unless cyclic."""
    n = len(ts)
    m = np.zeros_like(vs)
    for i in range(n):
        if cyclic:
            if i == 0 or i == n - 1:
                ip, inx, tp, tn = n - 2, 1, ts[n - 2] - 1.0, ts[1] + (1.0 if i == n - 1 else 0.0)
                tp = tp + (1.0 if i == n - 1 else 0.0)
            else:
                ip, inx, tp, tn = i - 1, i + 1, ts[i - 1], ts[i + 1]
        else:
            if i == 0 or i == n - 1:
                continue
            ip, inx, tp, tn = i - 1, i + 1, ts[i - 1], ts[i + 1]
        d = (vs[inx] - vs[ip]) / max(1e-6, tn - tp)
        ext = (vs[i] - vs[ip]) * (vs[inx] - vs[i]) <= 0
        m[i] = np.where(ext, 0.0, d)
    return m


def hermite(ts, vs, ms, t):
    i = int(np.searchsorted(ts, t, side="right") - 1)
    i = min(max(i, 0), len(ts) - 2)
    h = ts[i + 1] - ts[i]
    u = 0.0 if h <= 0 else (t - ts[i]) / h
    h00 = 2 * u ** 3 - 3 * u ** 2 + 1; h10 = u ** 3 - 2 * u ** 2 + u; h01 = -2 * u ** 3 + 3 * u ** 2; h11 = u ** 3 - u ** 2
    return h00 * vs[i] + h10 * h * ms[i] + h01 * vs[i + 1] + h11 * h * ms[i + 1]


def build(name, c):
    n = max(2, int(round(c["length"] * FPS)))
    keys = list(c["keys"])
    if c["loop"] and keys[0][0] == 0.0:
        keys = keys + [(1.0, keys[0][1], keys[0][2])]
    ts = np.array([k[0] for k in keys])
    Q = np.zeros((len(keys), len(C.PARTS), 4))
    P = np.zeros((len(keys), 3))
    for k, (t, pose, opts) in enumerate(keys):
        full, pel = C.resolve_key(pose, opts)
        P[k] = pel
        for b, p in enumerate(C.PARTS):
            q = quat(C.unity_euler(*full[p]))
            if k > 0 and np.dot(q, Q[k - 1, b]) < 0:      # shortest path between keys
                q = -q
            Q[k, b] = q
    cyc = bool(c["loop"])
    MQ = [slopes(ts, Q[:, b, :], cyc) for b in range(len(C.PARTS))]
    MP = slopes(ts, P, cyc)
    frames = n if c["loop"] else n + 1
    rot, pel = [], []
    prev = None
    for i in range(frames):
        t = i / n
        row = []
        for b in range(len(C.PARTS)):
            q = hermite(ts, Q[:, b, :], MQ[b], t)
            q = q / np.linalg.norm(q)
            if prev is not None and np.dot(q, prev[b]) < 0:
                q = -q
            row.append(q)
        prev = row
        for q in row:
            rot += [round(float(v), 5) for v in q]
        pel += [round(float(v), 5) for v in hermite(ts, P, MP, t)]
    return {"name": name, "fps": FPS, "frames": frames, "length": c["length"], "loop": cyc, "mask": c["mask"],
            "hit": float(c.get("hit", -1.0)), "speed": float(c.get("speed", 0.0)), "gait": bool(c.get("gait", False)),
            "bones": C.PARTS, "rot": rot, "pelvis": pel}


def angle(a, b):
    d = min(1.0, abs(float(np.dot(a, b))))
    return math.degrees(2 * math.acos(d))


def compare(data, path):
    ref = json.load(open(path))
    A = np.array(data["rot"]).reshape(-1, len(C.PARTS), 4)
    B = np.array(ref["rot"]).reshape(-1, len(C.PARTS), 4)
    if A.shape != B.shape:
        return f"frames differ {A.shape[0]} vs {B.shape[0]}"
    worst = max(angle(A[f, b], B[f, b]) for f in range(A.shape[0]) for b in range(A.shape[1]))
    pw = float(np.abs(np.array(data["pelvis"]) - np.array(ref["pelvis"])).max())
    return f"max {worst:.2f} deg, pelvis {pw * 100:.1f} cm"


# ---------------------------------------------------------------- stick figures (game axes: +x right, +y up, +z forward)
def qmat(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


TOOL_GRIP = -0.055                        # PlayerCharacter.AttachTool, in the hand
TOOL_HEAD = -0.055 - 14 * 0.08 * 0.7      # sledge head centre (ToolLibrary: y -13..-15 voxels, x0.7)


def frame_points(data, f, h=1.0):
    """Points of the bones (game height 1 rig) + the tool grip / head for frame f."""
    R = np.array(data["rot"]).reshape(-1, len(C.PARTS), 4)[f]
    pel = np.array(data["pelvis"]).reshape(-1, 3)[f]
    skel = C.skeleton(h)
    W = {}
    for b, p in enumerate(C.PARTS):
        par, off = skel[p]
        M = np.eye(4)
        M[:3, 3] = np.add(off, pel * h) if p == "Pelvis" else off
        M[:3, :3] = qmat(R[b])
        W[p] = (W[par] @ M) if par else M
    pts = {p: W[p][:3, 3] for p in C.PARTS}
    hand = W["HandR"]
    pts["Grip"] = (hand @ np.array([0, TOOL_GRIP, 0.007, 1]))[:3]
    pts["ToolHead"] = (hand @ np.array([0, TOOL_HEAD, 0.0, 1]))[:3]
    pts["HandLEnd"] = (W["HandL"] @ np.array([0, -0.05, 0, 1]))[:3]
    for s in "LR":
        pts["Toe" + s] = (W["Foot" + s] @ np.array([0, -0.05, 0.13, 1]))[:3]
    return pts


# ---------------------------------------------------------------- clearance: limbs and the held tool against the body
# HumanDesign.Sdf (bone space, metres, negative inside) for the parts an arm or tool can sink into
def _sdf_body(part, p, h=1.0, w=1.2):
    x, y, z = p
    if part == "Head":
        neck = math.dist((x, y, z), (0, min(max(y, 0.09 * h), 0.18 * h), 0)) - 0.05
        r = np.array([0.082, 0.108 * h, 0.098]); q = (np.array([x, y - 0.2 * h, z - 0.005])) / r
        return min(neck, (np.linalg.norm(q) - 1) * r.min())
    if part == "Pelvis":
        r = np.array([0.15 * w, 0.1 * h, 0.1]); q = (np.array([x, y - 0.02, z])) / r
        return (np.linalg.norm(q) - 1) * r.min()
    if part == "Chest":
        if y < -0.02 or y > 0.44 * h:
            return 1.0
        t = min(1, max(0, y / (0.44 * h))); u = min(1, t * 1.3); ss = u * u * (3 - 2 * u)
        rx = (0.13 + (0.185 - 0.13) * ss) * w * ((1 + (0.75 - 1) * (t - 0.85) / 0.15) if t > 0.85 else 1)
        rz = (0.085 + 0.02 * t) * (1.05 if z > 0 else 1)
        return (math.hypot(x / rx, z / rz) - 1) * min(rx, rz)
    # thighs: capsule down -y
    L, r0, r1 = 0.43 * h, 0.075 * w, 0.055 * w
    t = min(1, max(0, -y / L))
    return math.dist((x, y, z), (0, -t * L, 0)) - (r0 + (r1 - r0) * t)


LIMBS = [("UpperArmL", 0.29, 0.047, 0.04), ("UpperArmR", 0.29, 0.047, 0.04), ("ForearmL", 0.25, 0.039, 0.031),
         ("ForearmR", 0.25, 0.039, 0.031), ("HandL", 0.09, 0.025, 0.022), ("HandR", 0.09, 0.025, 0.022)]
TRUNK = ["Chest", "Pelvis", "ThighL", "ThighR"]


def bone_frames(data, f, w=1.2):
    R = np.array(data["rot"]).reshape(-1, len(C.PARTS), 4)[f]
    pel = np.array(data["pelvis"]).reshape(-1, 3)[f]
    skel = C.skeleton(1.0, w)
    W = {}
    for b, p in enumerate(C.PARTS):
        par, off = skel[p]
        M = np.eye(4)
        M[:3, 3] = np.add(off, pel) if p == "Pelvis" else off
        M[:3, :3] = qmat(R[b])
        W[p] = (W[par] @ M) if par else M
    return W


def clearance(data, tool=None, margin=0.025, w=1.2, arms=True):
    """Worst overlap (m, > 0 = sinks in, incl. a clothing margin) of the arms / hands and of a held tool (grip offset,
    length below the grip, extension above it, radius; right hand's space) with the torso, pelvis, thighs and head over
    all frames, at build w; returns (depth, frame, what). Upper arms are not tested against the chest (shoulder).
    With margin=None it returns the smallest gap instead (negative = clear by that much)."""
    gap = margin is None
    margin = 0.0 if gap else margin
    worst = (-9.0 if gap else 0.0, -1, "")
    for f in range(data["frames"]):
        W = bone_frames(data, f, w)
        inv = {t: np.linalg.inv(W[t]) for t in TRUNK + ["Head"]}
        segs = []
        if arms:
            for name, L, r0, r1 in LIMBS:
                for k in range(1, 6):
                    u = k / 5.0
                    segs.append((name, (W[name] @ np.array([0, -u * L, 0, 1]))[:3], (r0 + (r1 - r0) * u) * w))
        if tool:
            g0, length, up, rad = tool if len(tool) == 4 else (tool[0], tool[1], 0.0, tool[2])
            for k in range(0, 13):
                u = -up + (length + up) * k / 12.0
                segs.append(("tool", (W["HandR"] @ np.array([0, g0 - u, 0, 1]))[:3], rad))
        for name, pt, r in segs:
            for t in TRUNK + ["Head"]:
                if name.startswith("Upper") and t == "Chest" or t == "Head" and name != "tool":
                    continue
                q = (inv[t] @ np.array([*pt, 1]))[:3]
                d = r + margin - _sdf_body(t, q, 1.0, w)
                if d > worst[0]:
                    worst = (d, f, f"{name}->{t}")
    return worst


BONES = [("Pelvis", "Chest"), ("Chest", "Head"), ("Chest", "UpperArmL"), ("Chest", "UpperArmR"), ("UpperArmL", "ForearmL"),
         ("UpperArmR", "ForearmR"), ("ForearmL", "HandL"), ("ForearmR", "HandR"), ("Pelvis", "ThighL"), ("Pelvis", "ThighR"),
         ("ThighL", "ShinL"), ("ThighR", "ShinR"), ("ShinL", "FootL"), ("ShinR", "FootR"), ("FootL", "ToeL"), ("FootR", "ToeR")]


def strip(data, out_dir, count=10, tool=True):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    os.makedirs(out_dir, exist_ok=True)
    fig, axes = plt.subplots(2, count, figsize=(count * 1.5, 4.0))
    frames = data["frames"]
    for k in range(count):
        f = round(k * (frames - 1) / (count - 1))
        P = frame_points(data, f)
        for row, ia in enumerate((2, 0)):
            ax = axes[row][k]
            for a, b in BONES:
                col = "tab:blue" if a.endswith("L") or b.endswith("L") else "tab:red" if a.endswith("R") or b.endswith("R") else "k"
                ax.plot([P[a][ia], P[b][ia]], [P[a][1], P[b][1]], color=col, lw=1.4)
            if tool:
                ax.plot([P["Grip"][ia], P["ToolHead"][ia]], [P["Grip"][1], P["ToolHead"][1]], color="saddlebrown", lw=2)
                ax.plot(P["ToolHead"][ia], P["ToolHead"][1], "s", color="dimgray", ms=5)
            ax.plot([-1.0, 1.0], [0, 0], color="0.7", lw=0.8)
            ax.set_xlim(-1.0, 1.3); ax.set_ylim(-0.05, 2.1); ax.set_aspect("equal"); ax.set_xticks([]); ax.set_yticks([])
            if row == 0:
                ax.set_title(f"{f / max(1, frames - 1):.2f}", fontsize=8)
    fig.suptitle(data["name"] + "  (top: side, forward = right; bottom: front, right hand on the right)", fontsize=9)
    fig.tight_layout()
    path = os.path.join(out_dir, data["name"] + ".png")
    fig.savefig(path, dpi=80)
    plt.close(fig)
    return path


if __name__ == "__main__":
    argv = sys.argv[1:]
    out = argv[0] if argv and not argv[0].startswith("--") else os.path.join(HERE, "anims_out")

    def opt(name):
        return argv[argv.index(name) + 1] if name in argv and argv.index(name) + 1 < len(argv) else None

    only = set(opt("--clips").split(",")) if opt("--clips") else None
    cmp_dir, strip_dir = opt("--compare"), opt("--strip")
    if not cmp_dir:
        os.makedirs(out, exist_ok=True)
    idx_path = os.path.join(out, "clips_index.json")
    index = {e["name"]: e for e in json.load(open(idx_path))} if not cmp_dir and os.path.exists(idx_path) else {}
    for name, c in C.CLIPS.items():
        if only and name not in only:
            continue
        data = build(name, c)
        msg = ""
        if cmp_dir:
            ref = os.path.join(cmp_dir, name + ".json")
            msg = " vs anims.py: " + (compare(data, ref) if os.path.exists(ref) else "none")
        else:
            with open(os.path.join(out, name + ".json"), "w") as fh:
                json.dump(data, fh, separators=(",", ":"))
            index[name] = {"name": name, "frames": data["frames"], "length": c["length"], "loop": bool(c["loop"]), "mask": c["mask"],
                           "hit": float(c.get("hit", -1.0)), "speed": float(c.get("speed", 0.0)), "keyError": 0.0}
        if strip_dir:
            msg += " strip " + strip(data, strip_dir, tool=c.get("tool", False))
        print(f"[export] {name}: {data['frames']} frames{msg}")
    if not cmp_dir:
        order = list(C.CLIPS)
        with open(idx_path, "w") as fh:
            json.dump(sorted(index.values(), key=lambda e: order.index(e["name"]) if e["name"] in order else 999), fh, indent=1)
