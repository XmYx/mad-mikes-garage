"""Export clips without Blender: the game JSON straight from `clips.CLIPS` (numpy only).

Run:  python3 tools/blender/hd/character/export_clips.py <out_dir> [--clips sledge,flinch] [--compare <dir>] [--strip <png_dir>]
          [--sheet <png>] [--check]

`anims.py` keys the clips on the HD armature and samples Blender's curves; the round trip there is exact
(game Euler -> basis quaternion -> game quaternion), so the curves are what matter. This reproduces them: quaternion
components keyed per bone (shortest path between keys), Bezier with AUTO_CLAMPED handles (Catmull-Rom slopes, flat at
extremes and at the ends of one-shots, cyclic for loops), sampled at 30 fps; pelvis offsets the same way.
`--compare` reports the largest angle against clips exported by anims.py (the same names, nothing written), `--strip`
draws side/back(/top) stick-figure filmstrips (matplotlib; root motion, props, nose tick = head facing), `--sheet` one
contact sheet of the selected clips, `--check` prints foot slide, floor, arm clearance and leg overlap per clip.
Clips may carry extras (clipkit.Dense): rootX/rootZ/rootYaw per frame (root motion in the start frame, metres for height
1, degrees), door (0..1 open, per frame), eventNames/eventTimes (0..1); HumanClip ignores what it doesn't read.
"""
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import clipkit as K  # noqa: E402
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
    data = {"name": name, "fps": FPS, "frames": frames, "length": c["length"], "loop": cyc, "mask": c["mask"],
            "hit": float(c.get("hit", -1.0)), "speed": float(c.get("speed", 0.0)), "gait": bool(c.get("gait", False)),
            "bones": C.PARTS, "rot": rot, "pelvis": pel}
    data.update(C.extras(c, frames))
    return data


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


# ---------------------------------------------------------------- filmstrips
_HEAD_R = 0.1


def _world_points(data, f):
    """Stick-figure points in the clip's start frame (root motion applied; gait loops walk forward at `speed`)."""
    P = frame_points(data, f)
    root = K.root_at(data, f, data.get("speed", 0.0), data["length"])
    W = K.fk(*K.pose_of(data, f), root)
    out = {k: K.to_world(root, v) for k, v in P.items()}
    Rc, pc = W["Chest"]
    out["Neck"] = pc + Rc @ np.array([0, 0.4, 0.0])
    Rh, ph = W["Head"]
    out["HeadC"] = ph + Rh @ np.array([0, 0.2, 0.0])
    out["Nose"] = ph + Rh @ np.array([0, 0.2, 0.16])
    for s in "LR":
        out["Palm" + s] = K.point(W, "Hand" + s, K.PALM)
        out["Heel" + s] = K.point(W, "Foot" + s, K.HEEL)
        out["Toe" + s] = K.point(W, "Foot" + s, K.TOE)
    return out


_SEGS = [("Pelvis", "Chest", 7), ("Chest", "Neck", 7), ("Neck", "Head", 4), ("Neck", "UpperArmL", 4), ("Neck", "UpperArmR", 4), ("UpperArmL", "ForearmL", 3.4),
         ("UpperArmR", "ForearmR", 3.4), ("ForearmL", "HandL", 2.8), ("ForearmR", "HandR", 2.8), ("HandL", "PalmL", 2.2), ("HandR", "PalmR", 2.2),
         ("Pelvis", "ThighL", 5), ("Pelvis", "ThighR", 5), ("ThighL", "ShinL", 4.4), ("ThighR", "ShinR", 4.4), ("ShinL", "FootL", 3.4),
         ("ShinR", "FootR", 3.4), ("HeelL", "ToeL", 2.6), ("HeelR", "ToeR", 2.6)]
_VIEW = {"side": (2, 1, "side (forward = right)"), "front": (0, 1, "back view (right hand on the right)"), "top": (0, 2, "top (forward = up)")}


def strip(data, out_dir, count=10, tool=True, views=None, props=None, scale=1.0):
    """Filmstrip: a row per view (side, back, top), `count` frames evenly over the clip; root motion applied, props
    (polylines, start frame) and the floor drawn, nose tick = where the head faces."""
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    os.makedirs(out_dir, exist_ok=True)
    views = views or ["side", "front"] + (["top"] if data.get("rootYaw") and max(map(abs, data["rootYaw"])) > 1 else [])
    frames = data["frames"]
    idx = [round(k * (frames - (0 if data["loop"] else 1)) / (count - (0 if data["loop"] else 1))) % frames for k in range(count)]
    pts = {f: _world_points(data, f) for f in idx}
    scroll = {f: 0.0 for f in idx}
    if data.get("speed", 0.0) > 0 and not data.get("rootX"):
        for f in idx:                                      # gait loops: drawn around the root, the floor ticks scroll by
            scroll[f] = K.root_at(data, f, data["speed"], data["length"])[1]
            pts[f] = {k: v - np.array([0, 0, scroll[f]]) for k, v in pts[f].items()}
    allp = np.array([v for f in idx for v in pts[f].values()] + [p for line in (props or []) for p in line])
    lim = {}
    for v in views:
        a, b, _ = _VIEW[v]
        lo = allp.min(0) - 0.15; hi = allp.max(0) + 0.15
        if v != "top":
            lo[1] = min(lo[1], -0.05)
        lim[v] = ((lo[a], hi[a]), (lo[b], hi[b]))
    wspan = max(lim[v][0][1] - lim[v][0][0] for v in views)
    hspan = [lim[v][1][1] - lim[v][1][0] for v in views]
    pw = 1.7 * scale * max(1.0, wspan / 1.4)
    fig, axes = plt.subplots(len(views), count, figsize=(count * pw, sum(pw * h / wspan + 0.15 for h in hspan) + 0.4),
                             gridspec_kw={"height_ratios": hspan}, squeeze=False)
    for k, f in enumerate(idx):
        P = pts[f]
        for row, v in enumerate(views):
            ax = axes[row][k]
            a, b, _ = _VIEW[v]
            for line in props or []:
                L = np.array(line)
                ax.plot(L[:, a], L[:, b], color="0.6", lw=1.0)
            if v != "top" and not props:
                ax.axhline(0, color="0.55", lw=0.8)
            if v == "side" and not props:
                for tick in np.arange(math.floor((lim[v][0][0] + scroll[f]) / 0.25) * 0.25, lim[v][0][1] + scroll[f], 0.25):
                    ax.plot([tick - scroll[f]] * 2, [0, -0.04], color="0.55", lw=0.8)
            order = sorted(_SEGS, key=lambda s: -(P[s[0]][0] + P[s[1]][0]) if v == "side" else (P[s[0]][2] + P[s[1]][2]) if v == "front" else P[s[0]][1])
            for p0, p1, lw in order:
                col = "tab:blue" if p0.endswith("L") or p1.endswith("L") else "tab:red" if p0.endswith("R") or p1.endswith("R") else "0.15"
                ax.plot([P[p0][a], P[p1][a]], [P[p0][b], P[p1][b]], color=col, lw=lw * scale * 0.8, solid_capstyle="round")
            ax.add_patch(plt.Circle((P["HeadC"][a], P["HeadC"][b]), _HEAD_R, fill=False, color="0.15", lw=1.2))
            ax.plot([P["HeadC"][a], P["Nose"][a]], [P["HeadC"][b], P["Nose"][b]], color="darkorange", lw=1.6)
            if tool:
                ax.plot([P["Grip"][a], P["ToolHead"][a]], [P["Grip"][b], P["ToolHead"][b]], color="saddlebrown", lw=2)
            ax.set_xlim(*lim[v][0]); ax.set_ylim(*lim[v][1]); ax.set_aspect("equal"); ax.set_xticks([]); ax.set_yticks([])
            if row == 0:
                ax.set_title(f"{f / max(1, frames - (0 if data['loop'] else 1)):.2f}", fontsize=8)
            if k == 0:
                ax.set_ylabel(v, fontsize=7)
    fig.suptitle(data["name"] + f"  {data['length']:.2f}s {'loop' if data['loop'] else 'once'} {data['mask']}  (side: forward = right; front: seen from behind; top: forward = up)", fontsize=9)
    fig.tight_layout()
    path = os.path.join(out_dir, data["name"] + ".png")
    fig.savefig(path, dpi=80)
    plt.close(fig)
    return path


def sheet(datas, path, count=8, view="side"):
    """Contact sheet: one row per clip (one view), `count` frames each."""
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    fig, axes = plt.subplots(len(datas), count, figsize=(count * 1.3, len(datas) * 1.45), squeeze=False)
    a, b, _ = _VIEW[view]
    for r, data in enumerate(datas):
        frames = data["frames"]
        idx = [round(k * (frames - (0 if data["loop"] else 1)) / (count - (0 if data["loop"] else 1))) % frames for k in range(count)]
        pts = {f: _world_points(data, f) for f in idx}
        if data.get("speed", 0.0) > 0 and not data.get("rootX"):
            for f in idx:
                rz = K.root_at(data, f, data["speed"], data["length"])[1]
                pts[f] = {k: v - np.array([0, 0, rz]) for k, v in pts[f].items()}
        allp = np.array([v for f in idx for v in pts[f].values()])
        lo = allp.min(0) - 0.12; hi = allp.max(0) + 0.12
        span = max(hi[a] - lo[a], 1.6)
        ylo, yhi = min(-0.05, lo[1]), max(hi[1], 1.0)
        yc, ys = (ylo + yhi) / 2, max(yhi - ylo, span * 1.0)
        for k, f in enumerate(idx):
            ax = axes[r][k]
            P = pts[f]
            ax.axhline(data.get("floorY", 0.0), color="0.6", lw=0.6)
            for p0, p1, lw in _SEGS:
                col = "tab:blue" if p0.endswith("L") or p1.endswith("L") else "tab:red" if p0.endswith("R") or p1.endswith("R") else "0.15"
                ax.plot([P[p0][a], P[p1][a]], [P[p0][b], P[p1][b]], color=col, lw=lw * 0.6, solid_capstyle="round")
            ax.add_patch(plt.Circle((P["HeadC"][a], P["HeadC"][b]), _HEAD_R, fill=False, color="0.15", lw=1.0))
            ax.plot([P["HeadC"][a], P["Nose"][a]], [P["HeadC"][b], P["Nose"][b]], color="darkorange", lw=1.3)
            c0 = (lo[a] + hi[a]) / 2
            ax.set_xlim(c0 - ys / 2, c0 + ys / 2); ax.set_ylim(yc - ys / 2, yc + ys / 2); ax.set_aspect("equal"); ax.set_xticks([]); ax.set_yticks([])
            if k == 0:
                ax.set_ylabel(data["name"], fontsize=7)
    fig.tight_layout()
    fig.savefig(path, dpi=72)
    plt.close(fig)
    return path


def checks(data, c):
    """Foot slide (gait / root-motion / planted clips), floor, arm clearance (2.5 cm clothes margin, w 1.2) and leg overlap."""
    out = []
    if c.get("gait") or "root" in c or c.get("planted"):
        w, m, f = K.slide(data)
        out.append(f"slide max {w:.2f} m/s mean {m:.3f} @{f}")
    low, f, what = K.floor_check(data)
    fl = c.get("floor", 0.0)
    out.append(f"floor {(low - fl) * 100:+.1f} cm @{f} {what}")
    d, f, what = clearance(data, arms=True) if c["mask"] != "lower" else (0.0, -1, "")
    out.append(f"clear {d * 100:.1f} cm @{f} {what}")
    d, f, what = K.legs_check(data) if c["mask"] in ("full", "lower") else (0.0, -1, "")
    out.append(f"legs {d * 100:.1f} cm @{f} {what}")
    return "  ".join(out)


if __name__ == "__main__":
    argv = sys.argv[1:]
    out = argv[0] if argv and not argv[0].startswith("--") else os.path.join(HERE, "anims_out")

    def opt(name):
        return argv[argv.index(name) + 1] if name in argv and argv.index(name) + 1 < len(argv) else None

    only = set(opt("--clips").split(",")) if opt("--clips") else None
    cmp_dir, strip_dir, sheet_path = opt("--compare"), opt("--strip"), opt("--sheet")
    do_check = "--check" in argv
    if not cmp_dir:
        os.makedirs(out, exist_ok=True)
    idx_path = os.path.join(out, "clips_index.json")
    index = {e["name"]: e for e in json.load(open(idx_path))} if not cmp_dir and os.path.exists(idx_path) else {}
    sheet_data = []
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
            if data.get("eventNames"):
                index[name]["events"] = [f"{n}@{t:.3f}" for n, t in zip(data["eventNames"], data["eventTimes"])]
            if data.get("rootX"):
                index[name]["root"] = True
        if do_check:
            msg += "  " + checks(data, c)
        if strip_dir:
            msg += " strip " + strip(data, strip_dir, count=c.get("strip", 10), tool=c.get("tool", False), views=c.get("views"), props=c.get("props"))
        if sheet_path:
            sheet_data.append(data)
        print(f"[export] {name}: {data['frames']} frames{msg}")
    if sheet_path and sheet_data:
        print("[export] sheet", sheet(sheet_data, sheet_path))
    if not cmp_dir:
        order = list(C.CLIPS)
        with open(idx_path, "w") as fh:
            json.dump(sorted(index.values(), key=lambda e: order.index(e["name"]) if e["name"] in order else 999), fh, indent=1)
