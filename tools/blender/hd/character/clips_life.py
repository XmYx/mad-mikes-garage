"""The 2026-10 clip revamp: locomotion, prone, ground sit / lie, chair, car, idles, reactions.

Built with clipkit (dense 30 fps keys): gaits from a footstep synthesiser (feet planted in the world, legs by IK, pelvis
height from what the standing leg reaches), transitions from sparse keys with the feet / hands pinned where they rest.
`build(C)` returns {name: clip} (C = the clips module, for its poses and resolve_key); clips.py merges it into CLIPS.
Placement contracts (root motion, seat frame, door curve, events) are documented per clip below and in CLIPS.md.
"""
import math

import numpy as np

import clipkit as K
from clipkit import Dense, Plant, interp, smooth

TAU = 2 * math.pi


def _c(phi, lag=0.0):
    return math.cos(TAU * (phi - lag))


def _s(phi, lag=0.0):
    return math.sin(TAU * (phi - lag))


# ---------------------------------------------------------------- gait loops
WALK = dict(length=1.0, speed=1.4, beta=0.62, strike_pitch=-17.0, toe_pitch=38.0, roll_in=0.1, roll_out=0.24, lift=0.075, step_w=0.085,
            toe_out=5.0, bias=0.0, knee=[(0.0, 3.0), (0.14, 15.0), (0.4, 6.0), (0.6, 2.0), (0.75, 0.0)],
            p_yaw=6.0, p_roll=4.5, p_sway=0.022, p_pitch=1.5, lean=3.0, c_yaw=4.0, c_lag=0.03, head_pitch=4.0,
            arm=17.0, arm_base=-1.0, arm_z=7.0, arm_lag=0.07, fore=16.0, fore_amp=16.0, fore_lag=0.05, hand_flop=6.0, y_cap=0.3,
            swing_pitch=-8.0)
RUN = dict(length=0.72, speed=3.9, beta=0.38, strike_pitch=-8.0, toe_pitch=40.0, roll_in=0.05, roll_out=0.12, lift=0.16, step_w=0.06,
           toe_out=3.0, bias=0.03, knee=[(0.0, 18.0), (0.4, 40.0), (0.8, 22.0), (1.0, 14.0)],
           p_yaw=9.0, p_roll=5.0, p_sway=0.012, p_pitch=3.0, lean=9.0, c_yaw=8.0, c_lag=0.03, head_pitch=6.0,
           arm=30.0, arm_base=-6.0, arm_z=9.0, arm_lag=0.04, fore=84.0, fore_amp=14.0, fore_lag=0.04, hand_flop=4.0, y_cap=0.3,
           swing_pitch=-10.0, fist=True)
SPRINT = dict(length=0.6, speed=5.8, beta=0.31, strike_pitch=6.0, toe_pitch=46.0, roll_in=0.04, roll_out=0.1, lift=0.24, step_w=0.05,
              toe_out=2.0, bias=0.05, knee=[(0.0, 20.0), (0.4, 42.0), (0.85, 18.0), (1.0, 10.0)],
              p_yaw=11.0, p_roll=5.0, p_sway=0.008, p_pitch=4.0, lean=17.0, c_yaw=11.0, c_lag=0.02, head_pitch=10.0,
              arm=48.0, arm_base=-10.0, arm_z=8.0, arm_lag=0.03, fore=88.0, fore_amp=22.0, fore_lag=0.03, hand_flop=3.0, y_cap=0.3,
              swing_pitch=-14.0, fist=True)
TIRED_WALK = dict(length=1.05, speed=1.1, beta=0.66, strike_pitch=-10.0, toe_pitch=28.0, roll_in=0.12, roll_out=0.22, lift=0.05, step_w=0.095,
                  toe_out=7.0, bias=0.0, knee=[(0.0, 6.0), (0.14, 16.0), (0.45, 9.0), (0.65, 4.0), (0.78, 0.0)],
                  p_yaw=5.0, p_roll=6.5, p_sway=0.03, p_pitch=1.0, lean=13.0, c_yaw=3.0, c_lag=0.06, head_pitch=14.0,
                  arm=8.0, arm_base=1.0, arm_z=6.0, arm_lag=0.12, fore=10.0, fore_amp=6.0, fore_lag=0.08, hand_flop=8.0, y_cap=0.3,
                  swing_pitch=-3.0, chest_roll=2.0, head_bob=3.0)
CROUCH_WALK = dict(length=0.95, speed=1.0, beta=0.64, strike_pitch=-8.0, toe_pitch=30.0, roll_in=0.08, roll_out=0.2, lift=0.07, step_w=0.12,
                   toe_out=9.0, bias=0.03, knee=[(0.0, 60.0), (1.0, 60.0)],
                   p_yaw=7.0, p_roll=3.0, p_sway=0.03, p_pitch=14.0, lean=20.0, c_yaw=4.0, c_lag=0.04, head_pitch=6.0,
                   arm=10.0, arm_base=-22.0, arm_z=12.0, arm_lag=0.08, fore=50.0, fore_amp=10.0, fore_lag=0.05, hand_flop=4.0, y_cap=-0.27,
                   swing_pitch=-6.0)


def gait_upper(g, phi):
    """Everything but the legs of a gait at cycle phase phi: pose dict (thighs / shins neutral) and pelvis x, z."""
    py = g["p_yaw"] * _c(phi, 0.02)
    pr = -g["p_roll"] * _s(phi, 0.03)
    ppitch = g["p_pitch"] + 1.0 * _c(2 * phi, 0.1)
    cx = g["lean"] - ppitch + 1.2 * _c(2 * phi, 0.12)
    cy = -py - g["c_yaw"] * _c(phi, g["c_lag"])
    cz = -0.7 * pr + g.get("chest_roll", 0.0) * _s(phi, 0.05)
    hy = -(py + cy) * 0.9
    hx = g["head_pitch"] - (ppitch + cx) - 0.6 * _c(2 * phi, 0.15) + g.get("head_bob", 0.0) * _c(2 * phi, 0.2)
    hz = -(pr + cz) * 0.8
    sw = _c(phi, g["arm_lag"])
    foreL = -(g["fore"] + g["fore_amp"] * 0.5 * (1 - _c(phi, 0.5 + g["arm_lag"] + g["fore_lag"])))
    foreR = -(g["fore"] + g["fore_amp"] * 0.5 * (1 - _c(phi, g["arm_lag"] + g["fore_lag"])))
    twist = 6.0 if g.get("fist") else 2.0
    pose = {
        "Pelvis": (ppitch, py, pr), "Chest": (cx, cy, cz), "Head": (hx, hy, hz),
        "UpperArmL": (g["arm_base"] + g["arm"] * sw, twist * sw, -g["arm_z"] - 2.0 * max(0.0, sw)),
        "UpperArmR": (g["arm_base"] - g["arm"] * sw, twist * sw, g["arm_z"] + 2.0 * max(0.0, -sw)),
        "ForearmL": (foreL, 0, 0), "ForearmR": (foreR, 0, 0),
        "HandL": (g["hand_flop"] * _s(phi, g["arm_lag"] + 0.12), 0, 6 if g.get("fist") else 4),
        "HandR": (-g["hand_flop"] * _s(phi, g["arm_lag"] + 0.12), 0, -6 if g.get("fist") else -4),
        "ThighL": (-10, 0, -2), "ThighR": (-10, 0, 2), "ShinL": (20, 0, 0), "ShinR": (20, 0, 0),
    }
    return pose, np.array([-g["p_sway"] * _s(phi, 0.06), g["y_cap"], 0.008 * _c(2 * phi, 0.05)])


def gait_strike_z(g):
    """Where a heel lands ahead of the root (local z) in this gait."""
    return g["speed"] * g["length"] * g["beta"] / 2 - 0.03 + g["bias"]


def gait_plants(g, side, t0, t1, z_of):
    """Steady plants of one foot struck between t0 and t1 (s); z_of(t_on) = world heel z."""
    T = g["length"]
    off = 0.0 if side == "L" else 0.5
    sx = -1 if side == "L" else 1
    ps = []
    for k in range(-3, 6):
        ton = (k + off) * T
        if t0 - 1e-6 <= ton <= t1 + 1e-6:
            ps.append(Plant(ton, ton + g["beta"] * T, sx * g["step_w"], z_of(ton), sx * g["toe_out"], strike_pitch=g["strike_pitch"],
                            toe_pitch=g["toe_pitch"], roll_in=g["roll_in"], roll_out=g["roll_out"]))
    return ps


def gait_knee(g):
    kn = g["knee"]
    return lambda side, u, pl: interp(kn, u) if pl.strike else 1.5


def gait_loop(C, g):
    """One gait cycle (left heel strike at t = 0), feet planted in the world while the root moves at `speed`."""
    T, v = g["length"], g["speed"]
    d = Dense(T, loop=True, mask="full")
    d.set_root(lambda s: (0.0, v * s, 0.0))
    for i, t in enumerate(d.t):
        d.poses[i], d.pels[i] = gait_upper(g, t)
    a = gait_strike_z(g)
    pl = {s: gait_plants(g, s, -2 * T, 3 * T, lambda ton: v * ton + a) for s in "LR"}
    K.plant_feet(d, pl["L"], pl["R"], knee=gait_knee(g), lift=g["lift"], swing_pitch=g["swing_pitch"])
    d.root[:] = 0.0                                                     # loops: the runtime moves the root at `speed`
    d.events = [e for e in d.events if e[1] < 1.0]
    return d


# ---------------------------------------------------------------- standing
def stand_heels(C, pose=None):
    """World heel points (x, z) and toe-out yaw of both feet of a standing pose (root at the origin)."""
    full, pel = C.resolve_key(pose or C.STAND, {"ground": True})
    W = K.fk(full, pel)
    out = {}
    for s in "LR":
        h = K.point(W, "Foot" + s, K.HEEL)
        fwd = W["Foot" + s][0] @ np.array([0, 0, 1.0])
        out[s] = (h[0], h[2], math.degrees(math.atan2(fwd[0], fwd[2])))
    return out


def standing(t_on, t_off, heel, toe_off=False, strike=False, **kw):
    x, z, yaw = heel
    return Plant(t_on, t_off, x, z, yaw, strike=strike, toe_off=toe_off, **kw)


def keys_dense(C, keys, length, loop=False, mask="full"):
    return Dense(length, loop, mask).from_keys(keys, C.resolve_key)


def root_path(vfun, length, steps=600):
    """Root travel z(t) from a speed profile v(t) (m/s): returns (zfun, total)."""
    ts = np.linspace(0, length, steps + 1)
    vs = np.array([vfun(t) for t in ts])
    zs = np.concatenate([[0.0], np.cumsum((vs[1:] + vs[:-1]) * 0.5 * (ts[1] - ts[0]))])
    return (lambda t: float(np.interp(t, ts, zs))), float(zs[-1])


def walk_start(C):
    """Standing (idle frame) -> walking: right foot steps first, ends at walk's left heel strike (continue walk at u = 0).
    Root motion: rootZ accelerates from 0 to 1.4 m/s (the runtime moves the root along it)."""
    g = WALK
    T = 1.0
    zf, dist = root_path(lambda t: g["speed"] * smooth((t - 0.04) / 0.9) * (0.75 + 0.25 * min(1.0, t)), T)
    d = Dense(T, mask="full")
    d.set_root(lambda s: (0.0, zf(s), 0.0))
    a = gait_strike_z(g)
    st = stand_heels(C)
    zend = zf(T)
    L = [standing(-1.0, 0.62, st["L"], toe_off=True, roll_out=0.22, toe_pitch=30.0),
         Plant(1.0, 1.0 + g["beta"], -g["step_w"], zend + a, -g["toe_out"], strike_pitch=g["strike_pitch"], toe_pitch=g["toe_pitch"])]
    R = [standing(-1.0, 0.13, st["R"], toe_off=True, roll_out=0.1, toe_pitch=14.0),
         Plant(0.5, 0.5 + g["beta"], g["step_w"], zend + a - g["speed"] * 0.5 * T, g["toe_out"], strike_pitch=g["strike_pitch"],
               toe_pitch=g["toe_pitch"], roll_out=g["roll_out"])]
    p0, _ = C.resolve_key(C.STAND, {"ground": True})
    for i, t in enumerate(d.t):
        up, pel = gait_upper(g, t)
        w = smooth(t / 0.75)
        pose = K.blend_pose(p0, up, w)
        # a first lean into the step and the weight shifting over the left foot before the right lifts
        pose["Chest"] = (pose["Chest"][0] + 3.0 * math.sin(math.pi * min(1.0, t / 0.6)), pose["Chest"][1], pose["Chest"][2])
        shift = -0.03 * math.sin(math.pi * min(1.0, t / 0.45))
        d.poses[i] = pose
        d.pels[i] = np.array([pel[0] * w + shift, 0.3, pel[2] * w])
    K.plant_feet(d, L, R, knee=gait_knee(g), lift=g["lift"], swing_pitch=g["swing_pitch"])
    return d


def walk_stop(C):
    """Walking (walk at u = 0, left heel strike) -> standing still: the right foot closes beside the left, the weight
    settles back. Root motion: rootZ slows from 1.4 m/s to 0 (0.45 m)."""
    g = WALK
    T = 0.75
    zf, dist = root_path(lambda t: g["speed"] * (1 - smooth(t / 0.62)), T)
    a = gait_strike_z(g)
    d = Dense(T, mask="full")
    d.set_root(lambda s: (0.0, zf(s), 0.0))
    st = stand_heels(C)
    zL = a
    end = zL + 0.03 - st["L"][1] - 0.03                      # root ends with the left heel where standing puts it
    L = [Plant(0.0, 9.0, -g["step_w"], zL, -g["toe_out"], strike_pitch=g["strike_pitch"], toe_off=False, roll_in=g["roll_in"])]
    R = [Plant(-0.5, -0.5 + g["beta"], g["step_w"], a - g["speed"] * 0.5, g["toe_out"], strike_pitch=g["strike_pitch"], toe_pitch=g["toe_pitch"],
               roll_out=g["roll_out"]),
         Plant(0.44, 9.0, st["R"][0], zL + (st["R"][1] - st["L"][1]), st["R"][2], strike_pitch=-6.0, toe_off=False, roll_in=0.08)]
    p1, _ = C.resolve_key(C.STAND, {"ground": True})
    for i, t in enumerate(d.t):
        up, pel = gait_upper(g, min(0.45, t * T))
        w = smooth((t * T - 0.1) / 0.55)
        pose = K.blend_pose(up, p1, w)
        settle = -2.5 * math.sin(math.pi * min(1.0, max(0.0, (t * T - 0.35) / 0.4)))
        pose["Chest"] = (pose["Chest"][0] + settle, pose["Chest"][1], pose["Chest"][2])
        d.poses[i] = pose
        d.pels[i] = np.array([pel[0] * (1 - w), 0.3, pel[2] * (1 - w)])
    K.plant_feet(d, L, R, knee=gait_knee(g), lift=g["lift"], swing_pitch=g["swing_pitch"])
    return d, end, dist


def run_stop(C):
    """Running (run at u = 0, left foot strike) -> standing: a braking step with the right, the left closes, the body
    leans back and settles. Root motion: rootZ slows from 3.9 m/s to 0."""
    g = RUN
    T = 0.75
    zf, dist = root_path(lambda t: g["speed"] * (1 - smooth(t / 0.52)) ** 1.2, T)
    a = gait_strike_z(g)
    d = Dense(T, mask="full")
    d.set_root(lambda s: (0.0, zf(s), 0.0))
    st = stand_heels(C)
    zR = zf(T) + st["R"][1]
    L = [Plant(0.0, 0.3, -g["step_w"], a, -g["toe_out"], strike_pitch=g["strike_pitch"], toe_pitch=24.0, roll_in=g["roll_in"], roll_out=0.12),
         Plant(0.52, 9.0, st["L"][0], zf(T) + st["L"][1], st["L"][2], strike_pitch=-4.0, toe_off=False, roll_in=0.06)]
    R = [Plant(-0.36, -0.36 + g["beta"] * g["length"], g["step_w"], a - g["speed"] * 0.36, g["toe_out"], strike_pitch=g["strike_pitch"],
               toe_pitch=g["toe_pitch"], roll_out=g["roll_out"]),
         Plant(0.2, 9.0, st["R"][0] + 0.02, zR, st["R"][2], strike_pitch=-14.0, toe_off=False, roll_in=0.07)]
    p1, _ = C.resolve_key(C.STAND, {"ground": True})
    brake = C.add(p1, {"Chest": (-6, 0, 0), "Head": (6, 0, 0), "UpperArmL": (-34, 0, -14), "UpperArmR": (-30, 0, 16),
                       "ForearmL": (-50, 0, 0), "ForearmR": (-46, 0, 0)})
    for i, t in enumerate(d.t):
        s = t * T
        up, pel = gait_upper(g, min(0.25, s / g["length"]))
        if s < 0.3:
            pose = K.blend_pose(up, brake, smooth(s / 0.3))
        else:
            pose = K.blend_pose(brake, p1, smooth((s - 0.3) / 0.42))
        d.poses[i] = pose
        w = smooth(s / 0.3)
        d.pels[i] = np.array([pel[0] * (1 - w), 0.3, pel[2] * (1 - w) - 0.04 * math.sin(math.pi * min(1.0, s / 0.5))])
    def knee(side, u, pl):
        if pl.t_on < 0.1:
            return interp(g["knee"], u)
        return 1.5
    K.plant_feet(d, L, R, knee=knee, lift=0.12, swing_pitch=-8.0)
    # absorb the stop in the knees
    for i, t in enumerate(d.t):
        s = t * T
        dip = 0.07 * math.sin(math.pi * min(1.0, max(0.0, (s - 0.12) / 0.5)))
        d.pels[i][1] -= dip
    for i, t in enumerate(d.t):
        for side, plants in (("L", L), ("R", R)):
            a_, R_, stance, u, pl = K.feet_track(plants, t * T, 0.12, -8.0)
            K.leg_ik(d.poses[i], d.pels[i], side, K.to_local(d.root[i], a_), K.ry(-K.D(d.root[i][2])) @ R_)
    return d


def turn(C, deg=-90.0, length=0.75):
    """Turn on the spot by `deg` (negative = left). The clip keeps the root yaw out: the runtime turns the root along
    rootYaw (smoothstep over the clip, exported per frame); feet step and stay planted in the world meanwhile."""
    d = Dense(length, mask="full")
    d.set_root(lambda s: (0.0, 0.0, deg * smooth(s / length)))
    st = stand_heels(C)
    sgn = 1 if deg > 0 else -1
    lead, trail = ("R", "L") if deg > 0 else ("L", "R")

    def heel_at(side, yaw):
        x, z, toe = st[side]
        p = K.ry(K.D(yaw)) @ np.array([x, 0, z])
        return (p[0], p[2], toe + yaw)
    mid = deg * 0.55
    Lp = {lead: [standing(-1.0, 0.06 * length / 0.75, st[lead], toe_off=True, roll_out=0.06, toe_pitch=10.0),
                 standing(0.34 * length / 0.75, 9.0, heel_at(lead, mid), strike=True, strike_pitch=-6.0, roll_in=0.06)],
          trail: [standing(-1.0, 0.36 * length / 0.75, st[trail], toe_off=True, roll_out=0.08, toe_pitch=12.0),
                  standing(0.62 * length / 0.75, 9.0, heel_at(trail, deg), strike=True, strike_pitch=-4.0, roll_in=0.05)]}
    # the leading foot's last pivot to the final heading (a short shuffle at the end)
    Lp[lead][-1].t_off = 0.66 * length / 0.75
    Lp[lead].append(standing(0.74 * length / 0.75, 9.0, heel_at(lead, deg), strike=False))
    p0, _ = C.resolve_key(C.STAND, {"ground": True})
    for i, t in enumerate(d.t):
        s = t
        yaw_root = deg * smooth(s)
        # the head leads the turn, the chest follows, the pelvis lags behind the root
        head_w = deg * smooth(min(1.0, s / 0.55)) - yaw_root
        chest_w = deg * smooth(min(1.0, s / 0.75)) - yaw_root
        pel_w = deg * smooth(max(0.0, (s - 0.05) / 0.95)) - yaw_root
        pose = dict(p0)
        pose["Pelvis"] = (0, pel_w, 0)
        pose["Chest"] = (p0["Chest"][0] + 2 * math.sin(math.pi * s), chest_w - pel_w, -sgn * 3 * math.sin(math.pi * s))
        pose["Head"] = (-1.0, head_w - chest_w, 0)
        swing = math.sin(math.pi * min(1.0, s / 0.8))
        pose["UpperArmL"] = (-2 - 8 * sgn * swing, 0, -9 - 6 * swing)
        pose["UpperArmR"] = (-2 + 8 * sgn * swing, 0, 9 + 6 * swing)
        pose["ForearmL"] = (-12 - 8 * swing, 0, 0); pose["ForearmR"] = (-14 - 8 * swing, 0, 0)
        d.poses[i] = pose
        d.pels[i] = np.array([0.025 * sgn * math.sin(math.pi * min(1.0, s / 0.5)) * -1, 0.3, 0.0])
    K.plant_feet(d, Lp["L"], Lp["R"], knee=lambda side, u, pl: 4.0 if pl.strike else 2.0, lift=0.06, swing_pitch=-4.0)
    return d


# ---------------------------------------------------------------- reaching helpers (sparse keys)
HEAD_PTS = {"mouth": (0, 0.11, 0.1), "brow": (0, 0.25, 0.1), "back": (0, 0.22, -0.11), "templeR": (0.08, 0.22, 0.03),
            "templeL": (-0.08, 0.22, 0.03), "browL": (-0.05, 0.25, 0.09), "browR": (0.05, 0.25, 0.09), "crown": (0, 0.31, -0.02)}


def body_point(C, pose, opts, part, local):
    full, pel = C.resolve_key(pose, opts)
    return K.point(K.fk(full, pel), part, local)


def reach(C, pose, opts, side, target, palm=0.06, pole=None):
    """The pose with the `side` arm reaching so the palm is at `target` (root-local; the wrist sits `palm` back
    toward the shoulder); `target` may be a function of the posed body (W -> point)."""
    full, pel = C.resolve_key(pose, opts)
    W = K.fk(full, pel)
    T = np.asarray(target(W) if callable(target) else target, float)
    sh = K.point(W, "Chest", K.OFF["UpperArm" + side])
    wrist = T + (sh - T) / max(1e-9, np.linalg.norm(sh - T)) * palm
    p = dict(full)
    K.arm_ik(p, pel, side, wrist, pole=None if pole is None else (pole(W) if callable(pole) else pole))
    return {k: p[k] for k in p if k in pose or k.startswith(("UpperArm", "Forearm", "Hand"))}


def head_pt(name, dx=0.0, dy=0.0, dz=0.0):
    x, y, z = HEAD_PTS[name]
    return lambda W: K.point(W, "Head", (x + dx, y + dy, z + dz))


def pin_feet(d, t0=0.0, t1=1.0, fade=0.0):
    d.pin("FootL", t0, t1, fade=fade); d.pin("FootR", t0, t1, fade=fade)
    return d


def tremor(d, amp=1.2, hz=9.0, parts=("Chest", "Head", "UpperArmL", "UpperArmR")):
    """Shivering: a small fast shake added frame by frame (loops stay seamless: whole cycles)."""
    cyc = max(1, round(hz * d.length))
    for i, t in enumerate(d.t):
        for k, p in enumerate(parts):
            x, y, z = d.poses[i][p]
            ph = TAU * (cyc * t + 0.31 * k)
            d.poses[i][p] = (x + amp * math.sin(ph), y + 0.6 * amp * math.sin(ph * 1.0 + 1.3), z + 0.8 * amp * math.cos(ph + 0.7))
    return d


# ---------------------------------------------------------------- idles
def idles(C):
    out = {}
    G = {"ground": True}
    S = C.STAND
    # idle: one breath every 4 s, the weight drifting a few millimetres, the head settling
    ins = C.add(C.breath(S, 1.8, 1.0), {"Head": (0, 1.5, 0.5), "UpperArmL": (0.6, 0, 0), "UpperArmR": (0.6, 0, 0)})
    out_ = C.add(C.breath(S, -0.4, -0.3), {"Head": (0.6, -1.0, -0.3), "Pelvis": (0, 0.8, 0.4), "Chest": (0, -0.6, -0.4)})
    d = keys_dense(C, [(0.0, S, G), (0.38, ins, dict(G, pelvis_x=0.006)), (0.72, out_, dict(G, pelvis_x=-0.004))], 4.0, loop=True)
    out["idle"] = pin_feet(d).clip()

    # idle_look: the head leads, the chest and the weight follow; a glance back over the other shoulder
    def look(yh, yc, yp, px, extra=None):
        p = C.add(S, {"Head": (-2, yh, yh * 0.05), "Chest": (0, yc, -yc * 0.05), "Pelvis": (0, yp, yp * 0.3)})
        if extra:
            p = C.add(p, extra)
        return p, dict(G, pelvis_x=px)
    keys = [(0.0, S, G)]
    for t, args in ((0.1, (30, 4, 1, 0.004)), (0.2, (38, 12, 4, 0.018)), (0.4, (42, 13, 4, 0.02)), (0.47, (40, 12, 4, 0.02)),
                    (0.56, (2, 2, 0, 0.0)), (0.64, (-44, -6, -2, -0.008)), (0.72, (-52, -16, -5, -0.02)), (0.88, (-50, -15, -5, -0.02))):
        p, o = look(*args)
        keys.append((t, p, o))
    d = keys_dense(C, keys, 6.0, loop=True)
    out["idle_look"] = pin_feet(d).clip()

    # idle_scratch (upper): right hand to the back of the head, a few scratches, the head bowing into it
    R = C.REST_ARMS
    hb = C.add(R, {"Head": (10, -6, 6), "Chest": (2, 0, 2)})
    up = C.add(R, {"UpperArmR": (-120, 0, 40)})
    keys = [(0.0, R, {})]
    keys.append((0.2, reach(C, C.add(hb, {"UpperArmR": (-130, 10, 40)}), {}, "R", head_pt("back", 0.02, 0.0, -0.01), pole=lambda W: K.point(W, "Chest", (0.6, 0.5, 0.0))), {}))
    for k, t in enumerate((0.3, 0.38, 0.46, 0.54, 0.62)):
        dx = 0.035 if k % 2 == 0 else -0.01
        keys.append((t, reach(C, C.add(hb, {"Head": (2 * (k % 2), 0, 0), "UpperArmR": (-130, 10, 40)}), {}, "R", head_pt("back", dx, 0.02 * (k % 2), -0.01),
                                pole=lambda W: K.point(W, "Chest", (0.6, 0.5, 0.0))), {}))
    keys += [(0.82, C.add(R, {"UpperArmR": (-20, 0, 14), "ForearmR": (-30, 0, 0), "Head": (2, 0, 0)}), {}), (1.0, R, {})]
    out["idle_scratch"] = keys_dense(C, keys, 2.6, mask="upper").clip()

    # idle_wipe_brow (upper, heat): the back of the right wrist across the forehead, a flick of the hand, a sigh
    hd = C.add(R, {"Head": (8, 6, 0), "Chest": (3, 0, 0)})
    keys = [(0.0, R, {}),
            (0.22, reach(C, C.add(hd, {"UpperArmR": (-100, -20, 20)}), {}, "R", head_pt("browL", -0.02, 0.0, 0.05), palm=0.0,
                         pole=lambda W: K.point(W, "Chest", (0.7, 0.2, 0.2))), {}),
            (0.45, reach(C, C.add(hd, {"Head": (6, -4, 0), "UpperArmR": (-100, -20, 20)}), {}, "R", head_pt("templeR", 0.05, 0.0, 0.04), palm=0.0,
                         pole=lambda W: K.point(W, "Chest", (0.8, 0.1, 0.0))), {}),
            (0.6, C.add(R, {"UpperArmR": (-46, 0, 34), "ForearmR": (-70, 0, 0), "HandR": (30, 0, -20), "Chest": (-2, 0, 0), "Head": (-6, 0, 0)}), {}),
            (0.68, C.add(R, {"UpperArmR": (-34, 0, 30), "ForearmR": (-40, 0, 0), "HandR": (-40, 0, 10), "Chest": (-3, 0, 0), "Head": (-8, 0, 0)}), {}),
            (0.82, C.add(R, {"Chest": (-1, 0, 0), "Head": (-2, 0, 0)}), {}), (1.0, R, {})]
    out["idle_wipe_brow"] = keys_dense(C, keys, 2.4, mask="upper").clip(events=[("wipe", 0.3)])

    # idle_check_pack (upper): thumbs under the pack straps, a shrug to settle the load, a tug down
    def straps(p, dy=0.0, dz=0.0):
        for s, sx in (("L", -1), ("R", 1)):
            p = reach(C, p, {}, s, (lambda sx_: lambda W: K.point(W, "Chest", (sx_ * 0.12, 0.33 + dy, 0.175 + dz)))(sx), palm=0.03,
                      pole=(lambda sx_: lambda W: K.point(W, "Chest", (sx_ * 0.8, -0.1, -0.2)))(sx))
        return p
    keys = [(0.0, R, {}), (0.22, straps(C.add(R, {"Head": (6, 0, 0)})), {}),
            (0.4, straps(C.add(R, {"Chest": (-5, 0, 0), "Head": (-2, 0, 0)}), 0.05), {}),
            (0.52, straps(C.add(R, {"Chest": (2, 0, 0), "Head": (3, 0, 0)}), -0.03), {}),
            (0.64, straps(C.add(R, {"Chest": (1, 6, 0), "Head": (2, -4, 0)}), -0.04), {}),
            (0.78, straps(C.add(R, {"Chest": (1, -3, 0)}), 0.0), {}), (1.0, R, {})]
    out["idle_check_pack"] = keys_dense(C, keys, 2.6, mask="upper").clip()

    # idle_shiver (cold, loop): arms wrapped round the chest, shoulders up, head down, shaking, stepping on the spot
    hug = C.add(S, {"Chest": (8, 0, 0), "Head": (14, 0, 0)})
    hug = reach(C, hug, G, "L", lambda W: K.point(W, "Chest", (0.2, 0.22, 0.1)), palm=0.04, pole=lambda W: K.point(W, "Chest", (-0.5, 0.0, 0.6)))
    hug = reach(C, hug, G, "R", lambda W: K.point(W, "Chest", (-0.2, 0.26, 0.13)), palm=0.04, pole=lambda W: K.point(W, "Chest", (0.5, 0.0, 0.6)))
    hug2 = C.add(hug, {"Chest": (1.5, 0, 0), "Head": (2, 4, 0)})
    d = keys_dense(C, [(0.0, hug, G), (0.5, hug2, dict(G, pelvis_x=0.0))], 3.0, loop=True)
    st = stand_heels(C, S)
    L = [standing(-3.0, 0.45, st["L"], toe_off=True, roll_out=0.12, toe_pitch=18.0), standing(0.7, 1.95, st["L"], strike=True, strike_pitch=6.0, toe_off=True, roll_out=0.12, toe_pitch=18.0),
         standing(2.2, 3.45, st["L"], strike=True, strike_pitch=6.0, toe_off=True, roll_out=0.12, toe_pitch=18.0), standing(3.7, 9.0, st["L"], strike=True, strike_pitch=6.0)]
    Rr = [standing(-3.0, 1.2, st["R"], toe_off=True, roll_out=0.12, toe_pitch=18.0), standing(1.45, 2.7, st["R"], strike=True, strike_pitch=6.0, toe_off=True, roll_out=0.12, toe_pitch=18.0),
          standing(2.95, 9.0, st["R"], strike=True, strike_pitch=6.0)]
    for i, t in enumerate(d.t):
        d.pels[i][0] = 0.012 * math.sin(TAU * 2 * t)               # the weight goes over the standing foot
    K.plant_feet(d, L, Rr, knee=lambda side, u, pl: 3.0, lift=0.025, swing_pitch=0.0)
    tremor(d, amp=1.0, hz=9.0)
    out["idle_shiver"] = d.clip()

    # tired (loop): bent over, hands on the knees, heaving breaths
    bent = C.add(S, {"Pelvis": (22, 0, 0), "Chest": (26, 0, 0), "Head": (-34, 0, 0)})
    bent_g = dict(G, pelvis=(0, -0.08, -0.06))
    st2 = {"L": (-0.17, 0.0, -10.0), "R": (0.17, 0.0, 10.0)}

    def hands_on_knees(p, o, dy=0.0):
        full, pel = C.resolve_key(p, o)
        q = dict(full)
        for s in "LR":
            K.leg_ik(q, pel, s, np.array([st2[s][0], 0.06, st2[s][1] + 0.03]))
        for s, sx in (("L", -1), ("R", 1)):
            q = reach(C, q, {"pelvis": tuple(pel)}, s, (lambda s_, sx_: lambda W: K.point(W, "Shin" + s_, (-sx_ * 0.02, 0.06 + dy, 0.12)))(s, sx), palm=0.05,
                      pole=(lambda sx_: lambda W: K.point(W, "Chest", (sx_ * 0.9, 0.3, -0.3)))(sx))
        return q
    k0 = hands_on_knees(bent, bent_g)
    k1 = hands_on_knees(C.add(bent, {"Chest": (-4, 0, 0), "Head": (3, 0, 0)}), dict(bent_g, pelvis=(0, -0.075, -0.06)), 0.0)
    keys = []
    for b in range(3):                                                # three heaving breaths per loop
        keys += [(b / 3, k0, {"pelvis": (0, -0.08, -0.06)}), (b / 3 + 0.13, k1, {"pelvis": (0, -0.075, -0.06)})]
    d = keys_dense(C, keys, 2.4, loop=True)
    Lp = [standing(-9, 9, st2["L"])]; Rp = [standing(-9, 9, st2["R"])]
    for i in range(d.frames):
        for s, pl in (("L", Lp[0]), ("R", Rp[0])):
            a, Rm = pl.foot(0.0)
            K.leg_ik(d.poses[i], d.pels[i], s, a, Rm)
    out["tired"] = d.clip()

    # crouch_idle (loop): low on the heels, forearms forward, breathing, a look each way
    cr = C.add(S, {"Pelvis": (14, 0, 0), "Chest": (8, 0, 0), "Head": (-14, 0, 0), "UpperArmL": (-24, 0, -12), "UpperArmR": (-24, 0, 12),
                   "ForearmL": (-50, 0, 0), "ForearmR": (-50, 0, 0)})
    keys = [(0.0, cr, {"pelvis": (0, -0.27, -0.02)}),
            (0.25, C.add(cr, {"Chest": (1.5, 3, 0), "Head": (-2, 26, 0)}), {"pelvis": (0.01, -0.265, -0.02)}),
            (0.5, C.add(cr, {"Chest": (0.5, 0, 0), "Head": (1, 0, 0)}), {"pelvis": (0, -0.27, -0.02)}),
            (0.72, C.add(cr, {"Chest": (1.5, -4, 0), "Head": (-3, -30, 0)}), {"pelvis": (-0.012, -0.265, -0.02)})]
    d = keys_dense(C, keys, 4.0, loop=True)
    fl = {"L": (-0.15, -0.02, -12.0), "R": (0.15, -0.02, 12.0)}
    for i in range(d.frames):
        for s in "LR":
            a, Rm = standing(0, 1, fl[s]).foot(0.5)
            K.leg_ik(d.poses[i], d.pels[i], s, a, Rm)
    out["crouch_idle"] = d.clip()
    return out


# ---------------------------------------------------------------- reactions, eating (upper body)
def reactions(C):
    out = {}
    R = C.REST_ARMS
    # startle: a jolt (shoulders up, arms in, head ducks), then the chest and head turn toward the noise (right)
    jolt = C.add(R, {"Chest": (-6, 0, 0), "Head": (6, 0, 0), "UpperArmL": (-30, 0, 6), "UpperArmR": (-30, 0, -6), "ForearmL": (-70, 0, 0),
                     "ForearmR": (-70, 0, 0), "HandL": (0, 0, 10), "HandR": (0, 0, -10)})
    turnp = C.add(R, {"Chest": (-2, 22, 0), "Head": (-4, 44, 0), "UpperArmL": (-16, 0, -4), "UpperArmR": (-18, 0, 12), "ForearmL": (-40, 0, 0),
                      "ForearmR": (-36, 0, 0)})
    keys = [(0.0, R, {}), (0.1, jolt, {}), (0.2, C.add(jolt, {"Chest": (1, 6, 0), "Head": (0, 14, 0)}), {}), (0.45, turnp, {}),
            (0.7, C.add(turnp, {"Head": (-2, -2, 0), "Chest": (1, -1, 0)}), {}), (1.0, R, {})]
    st = keys_dense(C, keys, 1.2, mask="upper")
    out["startle"] = st.clip()
    out["startle_l"] = st.mirror(C.mirror).clip()

    # shield_eyes: the right hand flat over the brow, the head forward and squinting into the distance; holds 0.3..0.75
    sh = C.add(R, {"Head": (-4, 4, 0), "Chest": (-2, 0, 0)})
    sh = reach(C, C.add(sh, {"UpperArmR": (-110, -10, 30), "HandR": (-20, 0, -70)}), {}, "R", head_pt("brow", 0.02, 0.03, 0.08), palm=0.03,
               pole=lambda W: K.point(W, "Chest", (0.8, 0.3, 0.0)))
    keys = [(0.0, R, {}), (0.25, sh, {}), (0.5, C.add(sh, {"Head": (0, 10, 0), "Chest": (0, 3, 0)}), {}),
            (0.72, C.add(sh, {"Head": (0, -6, 0), "Chest": (0, -2, 0)}), {}), (1.0, R, {})]
    out["shield_eyes"] = keys_dense(C, keys, 2.2, mask="upper").clip(events=[("hold", 0.3), ("release", 0.75)])

    # point: the right arm swings up to point ahead, the head and chest line up behind it, a little emphasis bob
    pt = C.add(R, {"Chest": (-1, -8, 0), "Head": (-2, 6, 0), "UpperArmR": (-84, -6, 4), "ForearmR": (-6, 0, 0), "HandR": (6, 0, 0),
                   "UpperArmL": (-6, 0, -4)})
    keys = [(0.0, R, {}), (0.12, C.add(R, {"UpperArmR": (-50, 0, 10), "ForearmR": (-60, 0, 0), "Chest": (1, -3, 0)}), {}),
            (0.28, pt, {}), (0.36, C.add(pt, {"UpperArmR": (4, 0, 0), "Head": (2, 0, 0)}), {}), (0.62, pt, {}), (1.0, R, {})]
    out["point"] = keys_dense(C, keys, 1.6, mask="upper").clip(events=[("point", 0.28)])

    # cover_head: hunch, both forearms over the head, hold, peek and recover
    cv = C.add(R, {"Chest": (24, 0, 0), "Head": (18, 0, 0)})
    for s, sx in (("L", -1), ("R", 1)):
        cv = reach(C, cv, {}, s, (lambda sx_: head_pt("crown", sx_ * 0.04, 0.06, 0.02))(sx), palm=0.05,
                   pole=(lambda sx_: lambda W: K.point(W, "Head", (sx_ * 0.6, 0.3, 0.5)))(sx))
    keys = [(0.0, R, {}), (0.14, cv, {}), (0.55, C.add(cv, {"Chest": (2, 0, 0), "Head": (2, 0, 0)}), {}),
            (0.72, C.add(cv, {"Chest": (-8, 0, 0), "Head": (-12, 8, 0)}), {}), (1.0, R, {})]
    out["cover_head"] = keys_dense(C, keys, 1.6, mask="upper").clip(events=[("covered", 0.14)])

    # eat: two bites, the right hand up to the mouth, chewing nods between them
    bite = reach(C, C.add(R, {"Head": (6, 0, 0), "Chest": (4, 0, 0), "UpperArmR": (-40, 0, -10), "HandR": (0, 0, -20)}), {}, "R",
                 head_pt("mouth", 0.0, -0.02, 0.06), palm=0.04, pole=lambda W: K.point(W, "Chest", (0.6, -0.3, 0.0)))
    hold = C.add(R, {"UpperArmR": (-24, 0, 4), "ForearmR": (-82, 0, 0), "HandR": (0, 0, -16), "Head": (2, 0, 0)})
    chew = lambda a: C.add(hold, {"Head": (a, 0, 0)})
    keys = [(0.0, R, {}), (0.16, bite, {}), (0.24, C.add(bite, {"Head": (-4, 0, 0)}), {}), (0.32, chew(4), {}), (0.38, chew(-1), {}), (0.44, chew(4), {}),
            (0.52, bite, {}), (0.6, C.add(bite, {"Head": (-4, 0, 0)}), {}), (0.68, chew(4), {}), (0.74, chew(-1), {}), (0.8, chew(4), {}), (1.0, R, {})]
    out["eat"] = keys_dense(C, keys, 2.4, mask="upper").clip(events=[("bite", 0.17), ("bite", 0.53)])

    # drink: bottle up, the head tips back as it tilts, two gulps, down with a breath out
    sip = reach(C, C.add(R, {"Head": (-10, 0, 0), "Chest": (-3, 0, 0), "UpperArmR": (-60, 0, -8), "HandR": (-30, 0, -40)}), {}, "R",
                head_pt("mouth", 0.0, -0.03, 0.07), palm=0.03, pole=lambda W: K.point(W, "Chest", (0.6, -0.2, 0.0)))
    tilt = reach(C, C.add(R, {"Head": (-28, 0, 0), "Chest": (-7, 0, 0), "UpperArmR": (-90, 0, -8), "HandR": (-60, 0, -40)}), {}, "R",
                 head_pt("mouth", 0.0, -0.02, 0.07), palm=0.03, pole=lambda W: K.point(W, "Chest", (0.6, 0.0, 0.0)))
    keys = [(0.0, R, {}), (0.2, sip, {}), (0.34, tilt, {}), (0.44, C.add(tilt, {"Head": (4, 0, 0)}), {}), (0.52, tilt, {}),
            (0.6, C.add(tilt, {"Head": (4, 0, 0)}), {}), (0.7, sip, {}), (0.8, C.add(R, {"UpperArmR": (-20, 0, 6), "ForearmR": (-60, 0, 0), "Chest": (2, 0, 0), "Head": (4, 0, 0)}), {}),
            (1.0, R, {})]
    out["drink"] = keys_dense(C, keys, 2.2, mask="upper").clip(events=[("sip", 0.34), ("swallow", 0.44), ("swallow", 0.6)])
    return out


# ---------------------------------------------------------------- seats: chair, driver's seat
WHEEL = np.array([0.0, 1.34, 0.47])          # steering wheel centre in the seat frame (root = seat hip - 0.94, pelvis offset 0)
WHEEL_R, WHEEL_TILT = 0.19, 25.0


def wheel_grip(side, clock=None):
    """A point on the wheel rim: 10 o'clock (left hand) / 2 o'clock (right), seat frame."""
    ang = math.radians(clock if clock is not None else (-60.0 if side == "L" else 60.0))
    up = np.array([0, math.cos(math.radians(WHEEL_TILT)), -math.sin(math.radians(WHEEL_TILT))])
    return WHEEL + np.array([WHEEL_R * math.sin(ang), 0, 0]) + up * WHEEL_R * math.cos(ang)


def seat_poses(C):
    """(DRIVE2: hands at 10 and 2, elbows low and out; LOUNGE2: settled back, hands resting on the thighs)."""
    P0 = {"pelvis": (0, 0, 0)}
    dr = C.add(C.DRIVE, {"Chest": (2, 0, 0), "Head": (-2, 0, 0), "UpperArmL": (24, 0, -10), "UpperArmR": (24, 0, 10)})
    for s, sx in (("L", -1), ("R", 1)):
        dr = reach(C, dr, P0, s, wheel_grip(s), palm=0.045, pole=(lambda sx_: lambda W: K.point(W, "Chest", (sx_ * 0.7, -0.2, -0.3)))(sx))
    dr["HandL"] = (dr["HandL"][0] - 10, dr["HandL"][1], dr["HandL"][2] + 8)
    dr["HandR"] = (dr["HandR"][0] - 10, dr["HandR"][1], dr["HandR"][2] - 8)
    lo = C.add(C.LOUNGE, {"Pelvis": (-4, 0, 0), "Chest": (-6, 0, 0), "Head": (6, 0, 0)})
    for s, sx in (("L", -1), ("R", 1)):
        lo = reach(C, lo, P0, s, (lambda s_, sx_: lambda W: K.point(W, "Thigh" + s_, (sx_ * 0.01, -0.3, 0.1)))(s, sx), palm=0.05,
                   pole=(lambda sx_: lambda W: K.point(W, "Chest", (sx_ * 0.8, 0.0, -0.4)))(sx))
    return dr, lo


def seats(C):
    out = {}
    P0 = {"pelvis": (0, 0, 0)}
    DR, LO = seat_poses(C)
    # sit_drive (loop): hands stay on the wheel, breathing, glances at the mirrors and the gauges
    keys = [(0.0, DR, P0), (0.14, C.add(DR, {"Chest": (1, 0, 0), "Head": (-1, 0, 0)}), {"pelvis": (0, 0.003, 0)}),
            (0.26, C.add(DR, {"Head": (2, -34, -3), "Chest": (0, -4, 0)}), P0), (0.32, C.add(DR, {"Head": (1, -30, -3), "Chest": (0, -4, 0)}), P0),
            (0.4, DR, P0), (0.55, C.add(DR, {"Chest": (1, 0, 0), "Head": (-1, 4, 0)}), {"pelvis": (0, 0.003, 0)}),
            (0.64, C.add(DR, {"Head": (0, 26, 2), "Chest": (0, 3, 0)}), P0), (0.7, C.add(DR, {"Head": (0, 22, 2), "Chest": (0, 3, 0)}), P0),
            (0.8, C.add(DR, {"Head": (14, 0, 0)}), P0), (0.88, DR, P0)]
    d = keys_dense(C, keys, 7.0, loop=True)
    for i in range(d.frames):
        for s in "LR":
            K.arm_ik(d.poses[i], d.pels[i], s, _wrist_of(C, DR, s))
    out["sit_drive"] = d.clip()

    # sit (loop): settled back, hands on the thighs, breathing, a look round, the weight shifted, a foot moved
    keys = [(0.0, LO, P0), (0.18, C.add(LO, {"Chest": (1.5, 0, 0), "Head": (-2, 14, 0)}), {"pelvis": (0, 0.003, 0)}),
            (0.34, C.add(LO, {"Head": (1, 4, 0)}), P0),
            (0.48, C.add(LO, {"Pelvis": (0, 4, 2), "Chest": (1, -3, -2), "Head": (2, -6, 2), "ThighR": (-2, 4, 0), "ShinR": (-6, 0, 0)}), P0),
            (0.66, C.add(LO, {"Pelvis": (0, 4, 2), "Chest": (2, -3, -2), "Head": (-2, -20, 2), "ThighR": (-2, 4, 0), "ShinR": (-6, 0, 0)}), {"pelvis": (0, 0.003, 0)}),
            (0.84, C.add(LO, {"Chest": (0.5, 0, 0), "Head": (2, 0, 0)}), P0)]
    d = keys_dense(C, keys, 9.0, loop=True)
    for i in range(d.frames):
        for s in "LR":
            K.arm_ik(d.poses[i], d.pels[i], s, _hand_on_thigh(d.poses[i], d.pels[i], s))
    out["sit"] = d.clip()

    # sit_down / stand_up: the pelvis offset stays 0 (PlayerCharacter.Transit moves the root from the standing spot to
    # the seat and back); hands go out for balance on the way down, push on the thighs on the way up
    S = C.STAND
    mid = C.add(C.SIT_MID, {"UpperArmL": (-20, 0, -6), "UpperArmR": (-20, 0, 6), "ForearmL": (-30, 0, 0), "ForearmR": (-30, 0, 0)})
    low = C.add(C.SIT_LOW, {"Chest": (-4, 0, 0)})
    keys = [(0.0, S, P0), (0.12, C.add(S, {"Head": (8, 0, 0), "Chest": (4, 0, 0)}), P0), (0.42, mid, P0), (0.72, low, P0), (0.88, C.add(LO, {"Chest": (3, 0, 0)}), P0), (1.0, LO, P0)]
    out["sit_down"] = keys_dense(C, keys, 0.8).clip()
    push = C.add(C.UP_PUSH, {})
    push = {k: v for k, v in push.items()}
    pd = keys_dense(C, [(0.0, LO, P0), (0.25, push, P0), (0.6, C.UP_MID, P0), (0.82, C.add(S, {"Chest": (3, 0, 0)}), P0), (1.0, S, P0)], 0.85)
    for i, t in enumerate(pd.t):
        w = smooth(t / 0.2) * (1 - smooth((t - 0.5) / 0.18))
        if w > 0:
            for s in "LR":
                q = dict(pd.poses[i])
                K.arm_ik(q, pd.pels[i], s, _hand_on_thigh(q, pd.pels[i], s, 0.38))
                for p in ("UpperArm" + s, "Forearm" + s, "Hand" + s):
                    pd.poses[i][p] = K.slerp_e(pd.poses[i][p], q[p], w)
    out["stand_up"] = pd.clip()
    return out, DR, LO


def _wrist_of(C, pose, side):
    full, pel = C.resolve_key(pose, {"pelvis": (0, 0, 0)})
    return K.fk(full, pel)["Hand" + side][1]


def _hand_on_thigh(pose, pel, side, along=0.3):
    W = K.fk(pose, pel)
    sx = -1 if side == "L" else 1
    return K.point(W, "Thigh" + side, (sx * 0.01, -along, 0.1)) + (K.point(W, "Chest", K.OFF["UpperArm" + side]) - K.point(W, "Thigh" + side, (0, -along, 0.1))) * 0.0 + np.array([0, 0.05, 0])


# ---------------------------------------------------------------- cars
# Seat frame: the root sits where sit_drive puts it (seat hip - 0.94 up, facing the car's forward), pelvis offset 0 =
# seated. The driver's door is on the left (-x). Car: ground y = 0.42 (seat hip 0.52 above the ground), sill top 0.74 at
# x -0.36, door frame top 1.80, door hinge at (x -0.36, z 0.55), 0.95 m long, open = 68 deg. Truck cab ("_high"): ground
# y -0.51 (seat hip 1.45 above the ground), step top -0.01 at x -0.62, cab floor 0.40, grab bars at x -0.45, z +0.36 / -0.38.
CAR = dict(ground=0.42, hinge=np.array([-0.36, 0.0, 0.55]), door_len=0.95, open=68.0, sill=0.74, frame=1.80, start=(-0.6, -0.45))
TRUCK = dict(ground=-0.51, step=-0.01, step_x=-0.66, floor=0.40, bars=(np.array([-0.45, 0.0, 0.36]), np.array([-0.45, 0.0, -0.38])), start=(-0.9, 0.0))


def door_point(theta, along, y, inner=0.0):
    """A point on the driver's door `along` m back from the hinge at height y, door open theta degrees (seat frame)."""
    h = CAR["hinge"]
    s, c = math.sin(math.radians(theta)), math.cos(math.radians(theta))
    return np.array([h[0] - along * s + inner * c, y, h[2] - along * c - inner * s])


def car_props(truck=False):
    if truck:
        g, f = TRUCK["ground"], TRUCK["floor"]
        sx, sy = TRUCK["step_x"], TRUCK["step"]
        A, B = TRUCK["bars"]
        return [[(-1.4, g, -0.8), (0.6, g, -0.8), (0.6, g, 0.8), (-1.4, g, 0.8), (-1.4, g, -0.8)],
                [(sx - 0.12, sy, 0.0), (-0.42, sy, 0.0), (-0.42, f, 0.0), (0.6, f, 0.0)],
                [(-0.42, f, -0.6), (-0.42, f + 1.35, -0.6), (0.6, f + 1.35, -0.6)],
                [(A[0], 0.6, A[2]), (A[0], 1.6, A[2])], [(B[0], 0.6, B[2]), (B[0], 1.6, B[2])],
                [(-0.25, 0.84, -0.25), (0.25, 0.84, -0.25), (0.25, 0.84, 0.25), (-0.25, 0.84, 0.25), (-0.25, 0.84, -0.25)],
                _wheel_line()]
    g = CAR["ground"]
    d0, d1 = door_point(CAR["open"], 0.0, g + 0.25), door_point(CAR["open"], CAR["door_len"], g + 0.25)
    return [[(-1.4, g, -0.8), (0.6, g, -0.8), (0.6, g, 0.8), (-1.4, g, 0.8), (-1.4, g, -0.8)],
            [(-0.36, g + 0.1, 0.0), (-0.36, CAR["sill"], 0.0), (0.6, CAR["sill"], 0.0)],
            [(-0.38, CAR["frame"], -0.6), (-0.38, CAR["frame"], 0.6)], [(-0.38, CAR["frame"] + 0.04, 0.0), (0.6, CAR["frame"] + 0.04, 0.0)],
            [tuple(d0), tuple(d1), (d1[0], 1.72, d1[2]), (d0[0], 1.72, d0[2]), tuple(d0)],
            [(-0.25, 0.84, -0.25), (0.25, 0.84, -0.25), (0.25, 0.84, 0.25), (-0.25, 0.84, 0.25), (-0.25, 0.84, -0.25)],
            [(-0.25, 0.84, -0.27), (-0.25, 1.55, -0.33)], _wheel_line()]


def _wheel_line():
    return [tuple(wheel_grip("L", a)) for a in range(0, 361, 30)]


def secs(d, s):
    return s / d.length


def pin_s(d, limb, s0, s1, at=None, fade=0.12, rot=None, keep_rot=True, pole=None):
    """Dense.pin with seconds."""
    return d.pin(limb, secs(d, s0), secs(d, s1), at=at, fade=fade / d.length, rot=rot, keep_rot=keep_rot, pole=pole)


def curve(d, pts):
    """Per-frame values through [(seconds, value)] (smoothstep segments)."""
    return [interp(pts, d.sec(i)) for i in range(d.frames)]


def P(C, base, x, y, z, yaw, extra=None):
    p = dict(base)
    p["Pelvis"] = (p.get("Pelvis", (0, 0, 0))[0], yaw, p.get("Pelvis", (0, 0, 0))[2])
    if extra:
        p = C.add(p, extra)
    return p, {"pelvis": (x, y, z)}


def car_enter(C, DR, LO, seated):
    """Driver side: from standing at the open door (seat frame (-0.6, -0.45), facing forward) to seated (sit_drive's
    first frame, or sit's with seated = LO for passengers): turn the back to the seat, hand on the door frame and the
    roof edge, sit down onto the seat edge, swing the legs in under a ducked head, hands to the wheel."""
    S = C.STAND
    g = CAR["ground"]
    T = 2.4
    sx, sz = CAR["start"]
    stand_y = g + 0.02
    knees = {"ThighL": (-122, 0, -4), "ThighR": (-118, 0, 4), "ShinL": (70, 0, 0), "ShinR": (70, 0, 0), "FootL": (10, 0, 0), "FootR": (10, 0, 0)}
    side_sit = {k: v for k, v in DR.items() if k.startswith(("Thigh", "Shin", "Foot"))}
    keys = [(0.0, *P(C, S, sx, stand_y, sz, 0.0)),
            (0.25, *P(C, S, sx + 0.02, stand_y, sz + 0.08, -15.0, {"Chest": (2, -6, 0), "Head": (0, -20, 0)})),
            (0.5, *P(C, S, -0.58, stand_y, -0.18, -55.0, {"Chest": (8, -10, 0), "Head": (6, -10, 0)})),
            (0.9, *P(C, S, -0.6, stand_y, -0.02, -88.0, {"Chest": (14, 0, 0), "Head": (6, 0, 0)})),
            (1.1, *P(C, C.add(S, side_sit), -0.42, 0.18, 0.0, -90.0, {"Chest": (34, 0, 0), "Head": (10, 0, 0)})),
            (1.32, *P(C, C.add(S, side_sit), -0.2, 0.01, 0.0, -90.0, {"Chest": (22, 0, -4), "Head": (14, 0, 0)})),
            (1.52, *P(C, C.add(S, side_sit), -0.14, 0.0, 0.0, -84.0, {"Chest": (6, 0, -10), "Head": (16, 0, -6)})),
            (1.72, *P(C, C.add(S, knees), -0.08, 0.0, 0.0, -56.0, {"Chest": (-2, 6, -14), "Head": (18, 8, -12)})),
            (1.95, *P(C, dict(C.add(S, knees), **{"UpperArmL": (-36, 0, -30), "ForearmL": (-50, 0, 0), "UpperArmR": (-36, 0, 30), "ForearmR": (-50, 0, 0)}),
                      -0.02, 0.0, 0.0, -10.0, {"Chest": (0, 4, -8), "Head": (10, 4, -6)})),
            (2.2, seated, {"pelvis": (0, 0.004, 0)}),
            (2.4, seated, {"pelvis": (0, 0, 0)})]
    keys = [(t / T, p, o) for t, p, o in keys]
    d = keys_dense(C, keys, T)
    st = stand_heels(C)
    Lp = [standing(-1.0, 0.5, (sx + st["L"][0], sz + st["L"][1], st["L"][2]), toe_off=True, roll_out=0.12, toe_pitch=16.0, y=g + K.FLOOR),
          standing(0.85, 9.0, (-0.6, -0.12, -90.0), strike=True, strike_pitch=-8.0, roll_in=0.08, y=g + K.FLOOR)]
    Rp = [standing(-1.0, 0.12, (sx + st["R"][0], sz + st["R"][1], st["R"][2]), toe_off=True, roll_out=0.1, toe_pitch=14.0, y=g + K.FLOOR),
          standing(0.46, 9.0, (-0.6, 0.12, -80.0), strike=True, strike_pitch=-8.0, roll_in=0.08, y=g + K.FLOOR)]
    K.plant_feet(d, Lp, Rp, knee=lambda side, u, pl: 3.0, lift=0.07, swing_pitch=-4.0, until=1.42, fade=0.18)
    # hands: right on the door's window frame, left on the B-pillar while lowering
    pin_s(d, "HandR", 0.75, 1.45, at=door_point(CAR["open"], 0.42, 1.48, -0.04), fade=0.25, keep_rot=False)
    pin_s(d, "HandL", 0.85, 1.4, at=np.array([-0.36, 1.58, -0.3]), fade=0.25, keep_rot=False)
    d.props = car_props()
    d.door = [1.0] * d.frames
    d.ground = curve(d, [(0.0, 1.0), (1.05, 1.0), (1.32, 0.0), (T, 0.0)])
    d.events = [e for e in d.events] + [("seated", round(1.32 / T, 4)), ("legs_in", round(2.0 / T, 4))]
    d.views = ["side", "front", "top"]
    return d


def car_exit(C, DR, LO, seated):
    """Driver side, seated (sit_drive's first frame) -> standing outside at (-0.6, -0.45) facing forward: open the door
    from inside (door 0 -> 1, event door_open), swing the legs out, feet down, hand on the door frame, stand up,
    step round to face forward."""
    S = C.STAND
    g = CAR["ground"]
    T = 2.5
    sx, sz = CAR["start"]
    stand_y = g + 0.02
    knees = {"ThighL": (-118, 0, -4), "ThighR": (-112, 0, 4), "ShinL": (70, 0, 0), "ShinR": (74, 0, 0), "FootL": (10, 0, 0), "FootR": (10, 0, 0)}
    side_sit = {k: v for k, v in DR.items() if k.startswith(("Thigh", "Shin", "Foot"))}
    keys = [(0.0, seated, {"pelvis": (0, 0, 0)}),
            (0.3, C.add(seated, {"Chest": (2, -6, 8), "Head": (0, -20, 0)}), {"pelvis": (0, 0, 0)}),
            (0.55, *P(C, dict(C.add(C.add(seated, {k: v for k, v in knees.items()}), {"Chest": (-4, -4, 6)}), **{"UpperArmL": (-30, 0, -24), "ForearmL": (-40, 0, 0), "UpperArmR": (-20, 0, 40), "ForearmR": (-30, 0, 0)}),
                      -0.01, 0.0, 0.0, -12.0, {"Head": (8, -12, 0)})),
            (0.8, *P(C, C.add(S, knees), -0.06, 0.0, 0.0, -48.0, {"Chest": (-2, 6, -12), "Head": (16, 6, -10), "UpperArmR": (-70, 0, 34),
                                                                  "ForearmR": (-50, 0, 0), "UpperArmL": (-40, 0, -20), "ForearmL": (-40, 0, 0)})),
            (1.02, *P(C, C.add(S, side_sit), -0.14, 0.0, 0.0, -86.0, {"Chest": (8, 0, -8), "Head": (16, 0, -4)})),
            (1.25, *P(C, C.add(S, side_sit), -0.24, 0.02, 0.0, -90.0, {"Chest": (30, 0, -2), "Head": (12, 0, 0)})),
            (1.5, *P(C, S, -0.48, 0.2, 0.0, -90.0, {"Chest": (30, 0, 0), "Head": (6, 0, 0)})),
            (1.72, *P(C, S, -0.6, stand_y, -0.04, -84.0, {"Chest": (8, 0, 0), "Head": (0, 6, 0)})),
            (2.05, *P(C, S, -0.6, stand_y, -0.3, -40.0, {"Chest": (3, 8, 0), "Head": (0, 14, 0)})),
            (2.5, *P(C, S, sx, stand_y, sz, 0.0))]
    keys = [(t / T, p, o) for t, p, o in keys]
    d = keys_dense(C, keys, T)
    st = stand_heels(C)
    Lp = [standing(0.98, 1.62, (-0.6, -0.12, -90.0), strike=True, strike_pitch=-6.0, roll_in=0.06, toe_off=True, roll_out=0.12, toe_pitch=14.0, y=g + K.FLOOR),
          standing(1.95, 9.0, (sx + st["L"][0], sz + st["L"][1], st["L"][2]), strike=True, strike_pitch=-8.0, roll_in=0.07, y=g + K.FLOOR)]
    Rp = [standing(1.06, 2.0, (-0.6, 0.12, -86.0), strike=True, strike_pitch=-6.0, roll_in=0.06, toe_off=True, roll_out=0.12, toe_pitch=14.0, y=g + K.FLOOR),
          standing(2.3, 9.0, (sx + st["R"][0], sz + st["R"][1], st["R"][2]), strike=True, strike_pitch=-6.0, roll_in=0.07, y=g + K.FLOOR)]
    K.plant_feet(d, Lp, Rp, knee=lambda side, u, pl: 3.0, lift=0.07, swing_pitch=-4.0, since=1.05, fade=0.16)
    # the left hand works the inner handle and shoves the door open; then hands on the door frame and the B-pillar
    th = lambda s: interp([(0.0, 0.0), (0.3, 0.0), (0.55, 40.0), (0.75, CAR["open"])], s)
    for i in range(d.frames):
        s = d.sec(i)
        if s > 0.62:
            continue
        w = smooth(s / 0.28) * (1 - smooth((s - 0.52) / 0.1))
        q = dict(d.poses[i])
        K.arm_ik(q, d.pels[i], "L", door_point(min(th(s), 45.0), 0.45, 1.2, 0.05))
        for p in ("UpperArmL", "ForearmL", "HandL"):
            d.poses[i][p] = K.slerp_e(d.poses[i][p], q[p], w)
    pin_s(d, "HandR", 1.0, 1.7, at=door_point(CAR["open"], 0.42, 1.48, -0.04), fade=0.25, keep_rot=False)
    pin_s(d, "HandL", 1.05, 1.5, at=np.array([-0.36, 1.58, -0.3]), fade=0.25, keep_rot=False)
    d.props = car_props()
    d.door = [min(1.0, th(d.sec(i)) / CAR["open"]) for i in range(d.frames)]
    d.ground = curve(d, [(0.0, 0.0), (1.0, 0.0), (1.3, 1.0), (T, 1.0)])
    d.events = d.events + [("door_open", round(0.32 / T, 4)), ("standing", round(1.72 / T, 4))]
    d.views = ["side", "front", "top"]
    return d


def car_open_door(C):
    """Standing frame: root at the feet at the seat frame's (-0.6, -0.45), facing forward; the right hand pulls the
    handle (closed door at standing (0.22, 0.95, 0.17)), the door swings out, the left hand pushes it wide."""
    R = C.REST_ARMS
    T = 1.4
    off = np.array([CAR["start"][0], CAR["ground"], CAR["start"][1]])
    hinge = CAR["hinge"] - np.array([off[0], 0, off[2]])
    th = lambda s: interp([(0.0, 0.0), (0.3, 0.0), (0.68, 34.0), (0.95, 52.0), (1.2, CAR["open"])], s)

    def handle(theta, along=0.83, y=0.95):
        sn, cs = math.sin(math.radians(theta)), math.cos(math.radians(theta))
        return np.array([hinge[0] - along * sn, y, hinge[2] - along * cs])
    keys = [(0.0, R, {}), (0.21, C.add(R, {"Chest": (4, 16, 0), "Head": (8, 14, 0), "UpperArmR": (-30, 0, 20)}), {}),
            (0.5, C.add(R, {"Chest": (2, 4, 0), "Head": (4, 0, 0), "UpperArmL": (-30, 0, -10)}), {}),
            (0.68, C.add(R, {"Chest": (0, -6, 0), "Head": (2, -10, 0), "UpperArmL": (-50, 0, -10)}), {}),
            (1.0, C.add(R, {"Chest": (0, -4, 0), "Head": (0, -6, 0), "UpperArmL": (-40, 0, -14)}), {}), (1.4, R, {})]
    d = keys_dense(C, [(t / T, p, o) for t, p, o in keys], T, mask="upper")
    for i in range(d.frames):
        s = d.sec(i)
        if 0.05 < s < 0.85:
            w = smooth((s - 0.05) / 0.25) * (1 - smooth((s - 0.68) / 0.17))
            q = dict(d.poses[i]); K.arm_ik(q, d.pels[i], "R", handle(th(s)) + np.array([0.0, 0.0, 0.0]))
            for p in ("UpperArmR", "ForearmR", "HandR"):
                d.poses[i][p] = K.slerp_e(d.poses[i][p], q[p], w)
        if 0.5 < s < 1.2:
            w = smooth((s - 0.5) / 0.2) * (1 - smooth((s - 0.98) / 0.2))
            q = dict(d.poses[i]); K.arm_ik(q, d.pels[i], "L", handle(th(s), 0.9, 1.25))
            for p in ("UpperArmL", "ForearmL", "HandL"):
                d.poses[i][p] = K.slerp_e(d.poses[i][p], q[p], w)
    d.door = [th(d.sec(i)) / CAR["open"] for i in range(d.frames)]
    d.events = [("door_open", round(0.3 / T, 4))]
    return d


def car_close_door(C, seated, side_pose_hand="L"):
    """Seated (upper body over sit_drive / sit): lean out, the left hand takes the door's inner pull and swings it
    shut (door 1 -> 0, event door_shut), back to the wheel."""
    T = 1.4
    th = lambda s: interp([(0.0, CAR["open"]), (0.45, CAR["open"]), (0.85, 0.0)], s)
    keys = [(0.0, seated, {"pelvis": (0, 0, 0)}), (0.35, C.add(seated, {"Chest": (4, -14, 16), "Head": (2, -26, -6)}), {"pelvis": (0, 0, 0)}),
            (0.55, C.add(seated, {"Chest": (3, -12, 14), "Head": (2, -20, -4)}), {"pelvis": (0, 0, 0)}),
            (0.85, C.add(seated, {"Chest": (1, -4, 4), "Head": (0, -10, 0)}), {"pelvis": (0, 0, 0)}), (1.4, seated, {"pelvis": (0, 0, 0)})]
    d = keys_dense(C, [(t / T, p, o) for t, p, o in keys], T, mask="upper")
    for i in range(d.frames):
        s = d.sec(i)
        if 0.05 < s < 1.2:
            w = smooth((s - 0.05) / 0.3) * (1 - smooth((s - 0.88) / 0.3))
            q = dict(d.poses[i]); K.arm_ik(q, d.pels[i], "L", door_point(th(s), 0.45, 1.2, 0.05))
            for p in ("UpperArmL", "ForearmL", "HandL"):
                d.poses[i][p] = K.slerp_e(d.poses[i][p], q[p], w)
    d.door = [th(d.sec(i)) / CAR["open"] for i in range(d.frames)]
    d.events = [("door_shut", round(0.85 / T, 4))]
    return d


def car_enter_high(C, DR, LO, seated):
    """Truck / bus cab (seat hip 1.45 m over the ground): from standing at seat frame (-0.9, 0) facing forward, turn to
    the cab, both hands on the grab bars, left foot onto the step, push up, right foot onto the cab floor, duck in,
    turn and sit (sit_drive's first frame)."""
    S = C.STAND
    g, step, fl = TRUCK["ground"], TRUCK["step"], TRUCK["floor"]
    A, B = TRUCK["bars"]
    T = 2.8
    sx, sz = TRUCK["start"]
    sy = g + 0.02
    keys = [(0.0, *P(C, S, sx, sy, sz, 0.0)),
            (0.35, *P(C, S, sx - 0.04, sy, sz, 50.0, {"Head": (0, 20, 0)})),
            (0.65, *P(C, S, sx - 0.06, sy, sz, 88.0, {"Chest": (4, 0, 0), "Head": (-14, 0, 0)})),
            (1.0, *P(C, S, sx + 0.02, sy - 0.04, sz, 90.0, {"Chest": (14, 0, 0), "Head": (-24, 0, 0)})),
            (1.35, *P(C, S, -0.68, step + 0.06, sz + 0.03, 90.0, {"Chest": (20, 0, 0), "Head": (-20, 0, 0)})),
            (1.6, *P(C, S, -0.5, step + 0.18, sz, 84.0, {"Chest": (26, 0, 0), "Head": (-6, 0, 0)})),
            (1.95, *P(C, S, -0.26, fl - 0.94 + 0.86, -0.02, 50.0, {"Chest": (30, -10, 6), "Head": (10, -10, 0)})),
            (2.3, *P(C, C.add(S, {k: v for k, v in DR.items() if k.startswith(("Thigh", "Shin", "Foot"))}), -0.08, 0.04, 0.0, 14.0,
                     {"Chest": (8, 0, 0), "Head": (4, 0, 0)})),
            (2.55, seated, {"pelvis": (0, 0.004, 0)}), (2.8, seated, {"pelvis": (0, 0, 0)})]
    d = keys_dense(C, [(t / T, p, o) for t, p, o in keys], T)
    st = stand_heels(C)
    y0 = g + K.FLOOR
    Lp = [standing(-1, 0.3, (sx + st["L"][0], sz + st["L"][1], st["L"][2]), toe_off=True, roll_out=0.1, toe_pitch=12.0, y=y0),
          standing(0.55, 1.0, (-0.98, 0.13, 86.0), strike=True, strike_pitch=-6.0, roll_in=0.06, toe_off=True, roll_out=0.1, toe_pitch=10.0, y=y0),
          standing(1.2, 1.95, (-0.72, 0.08, 90.0), strike=True, strike_pitch=-4.0, roll_in=0.06, toe_off=True, roll_out=0.15, toe_pitch=20.0, y=step + K.FLOOR),
          standing(2.25, 9.0, (-0.18, 0.2, 40.0), strike=True, strike_pitch=-4.0, roll_in=0.06, y=fl + K.FLOOR)]
    Rp = [standing(-1, 0.15, (sx + st["R"][0], sz + st["R"][1], st["R"][2]), toe_off=True, roll_out=0.1, toe_pitch=12.0, y=y0),
          standing(0.42, 1.4, (-0.98, -0.13, 92.0), strike=True, strike_pitch=-6.0, roll_in=0.06, toe_off=True, roll_out=0.15, toe_pitch=24.0, y=y0),
          standing(1.72, 9.0, (-0.3, -0.16, 70.0), strike=True, strike_pitch=-6.0, roll_in=0.06, y=fl + K.FLOOR)]
    K.plant_feet(d, Lp, Rp, knee=lambda side, u, pl: 4.0, lift=0.12, swing_pitch=-8.0, until=2.05, fade=0.3)
    pin_s(d, "HandL", 0.75, 1.95, at=A + np.array([0, 0.95, 0]), fade=0.3, keep_rot=False)
    pin_s(d, "HandR", 0.8, 1.3, at=B + np.array([0, 0.95, 0]), fade=0.25, keep_rot=False)
    pin_s(d, "HandR", 1.5, 2.0, at=B + np.array([0, 1.3, 0]), fade=0.2, keep_rot=False)
    d.props = car_props(True)
    d.ground = curve(d, [(0.0, 1.0), (1.2, 1.0), (1.9, 0.0), (T, 0.0)])
    d.events = d.events + [("seated", round(2.4 / T, 4))]
    d.views = ["side", "front", "top"]
    return d


def car_exit_high(C, DR, LO, seated):
    """Seated in a cab -> standing on the ground at seat frame (-0.9, 0) facing forward: turn to the door with the back
    to it... (facing the cab), hands on the grab bars, climb down backwards onto the step and the ground, turn forward.
    The door is assumed open (door 0 -> 1 over the first 0.5 s, event door_open)."""
    S = C.STAND
    g, step, fl = TRUCK["ground"], TRUCK["step"], TRUCK["floor"]
    A, B = TRUCK["bars"]
    T = 2.8
    sx, sz = TRUCK["start"]
    sy = g + 0.02
    legs_dr = {k: v for k, v in DR.items() if k.startswith(("Thigh", "Shin", "Foot"))}
    keys = [(0.0, seated, {"pelvis": (0, 0, 0)}),
            (0.35, C.add(seated, {"Chest": (2, -10, 6), "Head": (0, -24, 0)}), {"pelvis": (0, 0, 0)}),
            (0.7, *P(C, C.add(S, legs_dr), -0.06, 0.03, 0.0, 30.0, {"Chest": (10, -6, 0), "Head": (4, -10, 0)})),
            (1.0, *P(C, S, -0.22, fl - 0.94 + 0.84, 0.0, 80.0, {"Chest": (28, 0, 0), "Head": (-10, 0, 0)})),
            (1.35, *P(C, S, -0.42, step + 0.12, sz, 90.0, {"Chest": (24, 0, 0), "Head": (-14, 0, 0)})),
            (1.7, *P(C, S, -0.7, step + 0.02, sz, 90.0, {"Chest": (18, 0, 0), "Head": (-20, 0, 0)})),
            (2.05, *P(C, S, sx + 0.03, sy - 0.02, sz, 90.0, {"Chest": (8, 0, 0), "Head": (-10, 0, 0)})),
            (2.45, *P(C, S, sx, sy, sz, 30.0, {"Chest": (2, 0, 0), "Head": (0, -14, 0)})),
            (2.8, *P(C, S, sx, sy, sz, 0.0))]
    d = keys_dense(C, [(t / T, p, o) for t, p, o in keys], T)
    st = stand_heels(C)
    y0 = g + K.FLOOR
    Lp = [standing(0.75, 1.15, (-0.18, 0.18, 70.0), strike=True, strike_pitch=-4.0, roll_in=0.06, toe_off=True, roll_out=0.1, toe_pitch=12.0, y=fl + K.FLOOR),
          standing(1.35, 2.05, (-0.72, 0.08, 90.0), strike=True, strike_pitch=8.0, roll_in=0.08, toe_off=True, roll_out=0.12, toe_pitch=14.0, y=step + K.FLOOR),
          standing(2.25, 9.0, (sx + st["L"][0], sz + st["L"][1], st["L"][2]), strike=True, strike_pitch=-4.0, roll_in=0.06, y=y0)]
    Rp = [standing(1.0, 1.55, (-0.26, -0.16, 86.0), strike=True, strike_pitch=-4.0, roll_in=0.06, toe_off=True, roll_out=0.1, toe_pitch=10.0, y=fl + K.FLOOR),
          standing(1.8, 2.4, (-0.98, -0.13, 92.0), strike=True, strike_pitch=10.0, roll_in=0.08, toe_off=True, roll_out=0.1, toe_pitch=10.0, y=y0),
          standing(2.6, 9.0, (sx + st["R"][0], sz + st["R"][1], st["R"][2]), strike=True, strike_pitch=-4.0, roll_in=0.06, y=y0)]
    K.plant_feet(d, Lp, Rp, knee=lambda side, u, pl: 4.0, lift=0.1, swing_pitch=-6.0, since=0.95, fade=0.3)
    pin_s(d, "HandL", 0.9, 2.0, at=A + np.array([0, 1.0, 0]), fade=0.3, keep_rot=False)
    pin_s(d, "HandR", 0.95, 1.5, at=B + np.array([0, 1.25, 0]), fade=0.25, keep_rot=False)
    pin_s(d, "HandR", 1.65, 2.1, at=B + np.array([0, 0.9, 0]), fade=0.2, keep_rot=False)
    d.props = car_props(True)
    d.door = curve(d, [(0.0, 0.0), (0.15, 0.0), (0.5, 1.0), (T, 1.0)])
    d.ground = curve(d, [(0.0, 0.0), (0.9, 0.0), (1.8, 1.0), (T, 1.0)])
    d.events = d.events + [("door_open", round(0.16 / T, 4))]
    d.views = ["side", "front", "top"]
    return d


# ---------------------------------------------------------------- on the ground: sit, lie
GROUND_HIP_Z = -0.45          # sitting on the ground: the hips land this far behind where the feet stood


def sit_ground_pose(C):
    """Knees up, forearms across the knees, rounded back (feet where standing put them, hips 0.45 m behind)."""
    base = {"Pelvis": (-20, 0, 0), "Chest": (30, 0, 0), "Head": (-12, 0, 0), "ThighL": (-70, 0, -6), "ThighR": (-70, 0, 6),
            "ShinL": (110, 0, 0), "ShinR": (110, 0, 0), "UpperArmL": (-40, 0, -10), "UpperArmR": (-40, 0, 10), "ForearmL": (-60, 0, 0), "ForearmR": (-60, 0, 0),
            "HandL": (20, 0, 10), "HandR": (20, 0, -10)}
    pel = (0.0, -0.8, GROUND_HIP_Z)
    st = stand_heels(C)
    full = {p: base.get(p, (0, 0, 0)) for p in K.PARTS}
    for s in "LR":
        a, R = standing(0, 1, st[s]).foot(0.5)
        K.leg_ik(full, np.array(pel), s, a, R)
    for s, sx in (("L", -1), ("R", 1)):
        W = K.fk(full, pel)
        knee = W["Shin" + s][1]
        K.arm_ik(full, np.array(pel), s, knee + np.array([sx * 0.03, 0.13, 0.2]), pole=K.point(W, "Chest", (sx * 1.2, -0.1, 0.3)))
    return full, pel


def ground(C):
    out = {}
    S = C.STAND
    G = {"ground": True}
    SG, sgp = sit_ground_pose(C)
    st = stand_heels(C)
    feet = lambda: ([standing(-9, 99, st["L"])], [standing(-9, 99, st["R"])])
    z = GROUND_HIP_Z

    def plant(d):
        L, R = feet()
        for i in range(d.frames):
            for s, pl in (("L", L[0]), ("R", R[0])):
                a, Rm = pl.foot(0.5)
                K.leg_ik(d.poses[i], d.pels[i], s, K.to_local(d.root[i], a), K.ry(-K.D(d.root[i][2])) @ Rm)
        return d

    # sit_ground (loop): breathing, a look round, rubbing the hands
    keys = [(0.0, SG, {"pelvis": sgp}), (0.2, C.add(SG, {"Chest": (2, 0, 0), "Head": (-2, 0, 0)}), {"pelvis": sgp}),
            (0.42, C.add(SG, {"Chest": (0, 8, 0), "Head": (-4, 38, 2)}), {"pelvis": sgp}), (0.56, C.add(SG, {"Chest": (1, 6, 0), "Head": (-2, 30, 0)}), {"pelvis": sgp}),
            (0.7, C.add(SG, {"Chest": (3, 0, 0), "Head": (12, 0, 0), "ForearmL": (-8, 0, 0), "ForearmR": (-8, 0, 0)}), {"pelvis": sgp}),
            (0.76, C.add(SG, {"Chest": (3, 0, 0), "Head": (12, 0, 0), "ForearmL": (-2, 0, 0), "ForearmR": (-12, 0, 0)}), {"pelvis": sgp}),
            (0.82, C.add(SG, {"Chest": (3, 0, 0), "Head": (12, 0, 0), "ForearmL": (-10, 0, 0), "ForearmR": (-4, 0, 0)}), {"pelvis": sgp}),
            (0.9, SG, {"pelvis": sgp})]
    out["sit_ground"] = plant(keys_dense(C, keys, 9.0, loop=True)).clip()

    # sit_ground_down: squat, the right hand back to the ground, the seat lands behind the feet, settle
    squat = C.add(S, {"Pelvis": (18, 0, 0), "Chest": (22, 0, 0), "Head": (-24, 0, 0), "UpperArmL": (-50, 0, -10), "UpperArmR": (-30, 0, 20), "ForearmL": (-30, 0, 0), "ForearmR": (-20, 0, 0)})
    reachb = C.add(S, {"Pelvis": (6, 18, 6), "Chest": (16, 14, 0), "Head": (-14, -8, 0), "UpperArmL": (-56, 0, -10), "ForearmL": (-40, 0, 0)})
    hand = np.array([0.3, 0.04, z - 0.12])
    keys = [(0.0, S, G), (0.12, C.add(S, {"Head": (10, 0, 0)}), G), (0.35, squat, {"pelvis": (0, -0.34, -0.12)}),
            (0.6, reachb, {"pelvis": (0.03, -0.56, -0.28)}), (0.85, C.add(SG, {"Pelvis": (6, 10, 4), "Chest": (-4, 6, 0)}), {"pelvis": (0.02, -0.8, z)}),
            (1.1, C.add(SG, {"Chest": (-3, 0, 0)}), {"pelvis": sgp}), (1.4, SG, {"pelvis": sgp})]
    d = plant(keys_dense(C, [(t / 1.4, p, o) for t, p, o in keys], 1.4))
    pin_s(d, "HandR", 0.55, 1.0, at=hand, fade=0.2, keep_rot=False)
    out["sit_ground_down"] = d.clip(events=[("seated", 0.6)])

    # sit_ground_up: lean forward, the right hand pushes off the ground, rock over the feet, squat, stand
    keys = [(0.0, SG, {"pelvis": sgp}), (0.3, C.add(SG, {"Chest": (14, 12, 0), "Head": (-10, -6, 0)}), {"pelvis": sgp}),
            (0.6, C.add(S, {"Pelvis": (30, 10, 0), "Chest": (30, 6, 0), "Head": (-30, 0, 0), "UpperArmL": (-60, 0, -10), "ForearmL": (-30, 0, 0)}), {"pelvis": (0, -0.58, -0.24)}),
            (0.9, C.add(S, {"Pelvis": (24, 0, 0), "Chest": (22, 0, 0), "Head": (-26, 0, 0), "UpperArmL": (-40, 0, -10), "UpperArmR": (-40, 0, 10)}), {"pelvis": (0, -0.3, -0.1)}),
            (1.2, C.add(S, {"Chest": (6, 0, 0), "Head": (-4, 0, 0)}), G), (1.5, S, G)]
    d = plant(keys_dense(C, [(t / 1.5, p, o) for t, p, o in keys], 1.5))
    pin_s(d, "HandR", 0.25, 0.7, at=np.array([0.32, 0.04, z + 0.02]), fade=0.2, keep_rot=False)
    out["sit_ground_up"] = d.clip(events=[("standing", 0.8)])

    # lie_down / lie_up: through sitting on the ground; root motion (rootZ) moves the root under the lying hips, so the
    # lie loop (pelvis offset (0, -0.8, 0)) follows seamlessly; lie_up moves it back over the feet
    LP = lie_pose(C)
    T = 2.4
    rz = lambda s: -0.45 * smooth((s - 1.1) / 1.3)
    keys = [(0.0, S, G), (0.2, C.add(S, {"Head": (10, 0, 0)}), G), (0.45, squat, {"pelvis": (0, -0.34, -0.12)}),
            (0.7, reachb, {"pelvis": (0.03, -0.56, -0.28)}), (0.95, C.add(SG, {"Pelvis": (6, 10, 4), "Chest": (-4, 6, 0)}), {"pelvis": (0.02, -0.8, z)}),
            (1.35, C.LIE_ELBOWS, {"pelvis": (0, -0.79, z)}), (1.8, C.LIE_SETTLE, {"pelvis": (0, -0.795, z)}), (2.4, LP, {"pelvis": (0, -0.8, z)})]
    d = keys_dense(C, [(t / T, p, o) for t, p, o in keys], T)
    L, R = feet()
    K.plant_feet(d, L, R, knee=lambda s, u, pl: 2.0, until=1.3, fade=0.3)
    pin_s(d, "HandR", 0.65, 1.05, at=hand, fade=0.2, keep_rot=False)
    d.set_root(lambda s: (0.0, rz(s), 0.0))
    for i in range(d.frames):
        d.pels[i] = d.pels[i] - np.array([0, 0, d.root[i][1]])
    K.floor_fix(d)
    out["lie_down"] = d.clip(events=[("seated", round(0.95 / T, 3)), ("lying", round(2.0 / T, 3))])

    T = 2.4
    zf = 0.45
    rz = lambda s: zf * smooth((s - 1.0) / 1.3)
    keys = [(0.0, LP, {"pelvis": (0, -0.8, 0)}), (0.5, C.LIE_ELBOWS, {"pelvis": (0, -0.79, 0)}),
            (0.95, SG, {"pelvis": (0, -0.8, 0)}), (1.2, C.add(SG, {"Chest": (14, 12, 0), "Head": (-10, -6, 0)}), {"pelvis": (0, -0.8, 0)}),
            (1.5, C.add(S, {"Pelvis": (30, 10, 0), "Chest": (30, 6, 0), "Head": (-30, 0, 0), "UpperArmL": (-60, 0, -10), "ForearmL": (-30, 0, 0)}), {"pelvis": (0, -0.58, zf - 0.24)}),
            (1.8, C.add(S, {"Pelvis": (24, 0, 0), "Chest": (22, 0, 0), "Head": (-26, 0, 0), "UpperArmL": (-40, 0, -10), "UpperArmR": (-40, 0, 10)}), {"pelvis": (0, -0.3, zf - 0.1)}),
            (2.1, C.add(S, {"Chest": (6, 0, 0), "Head": (-4, 0, 0)}), dict(G, pelvis=(0, 0, zf))), (2.4, S, dict(G, pelvis=(0, 0, zf)))]
    keys = [(t, p, (dict(o, pelvis=(o["pelvis"][0], C.resolve_key(p, o)[1][1], o["pelvis"][2])) if o.get("ground") else o)) for t, p, o in keys]
    keys = [(t, p, {"pelvis": o["pelvis"]}) for t, p, o in keys]
    d = keys_dense(C, [(t / T, p, o) for t, p, o in keys], T)
    Ls = [standing(0.8, 99, (st["L"][0], st["L"][1] + zf, st["L"][2]), strike=True, strike_pitch=-6.0, roll_in=0.1)]
    Rs = [standing(0.85, 99, (st["R"][0], st["R"][1] + zf, st["R"][2]), strike=True, strike_pitch=-6.0, roll_in=0.1)]
    K.plant_feet(d, [standing(-9, -8, st["L"])] + Ls, [standing(-9, -8, st["R"])] + Rs, knee=lambda s, u, pl: 2.0, since=0.95, fade=0.3)
    pin_s(d, "HandR", 1.15, 1.6, at=np.array([0.32, 0.04, 0.02]), fade=0.2, keep_rot=False)
    d.set_root(lambda s: (0.0, rz(s), 0.0))
    for i in range(d.frames):
        d.pels[i] = d.pels[i] - np.array([0, 0, d.root[i][1]])
    K.floor_fix(d)
    out["lie_up"] = d.clip(events=[("seated", round(0.95 / T, 3)), ("standing", round(2.1 / T, 3))])

    # lie, lie_side (loops) and lie_back (bed edge -> lying, rebuilt to start from the new sit)
    keys = [(0.0, LP, {"pelvis": (0, -0.8, 0)}), (0.35, C.add(LP, {"Chest": (2, 0, 0), "Head": (-1.5, 0, 0)}), {"pelvis": (0, -0.798, 0)}),
            (0.55, LP, {"pelvis": (0, -0.8, 0)}),
            (0.68, C.add(LP, {"Chest": (1.5, 0, 0), "ThighR": (-14, 0, 2), "ShinR": (22, 0, 0), "FootR": (-8, 0, 0), "Head": (0, 6, 0)}), {"pelvis": (0, -0.799, 0)}),
            (0.86, C.add(LP, {"ThighR": (-12, 0, 2), "ShinR": (18, 0, 0), "FootR": (-6, 0, 0), "Head": (0, 5, 0)}), {"pelvis": (0, -0.8, 0)})]
    out["lie"] = keys_dense(C, keys, 6.0, loop=True).clip()
    side = side_pose(C)
    keys = [(0.0, side, {"pelvis": (0.0, -0.78, 0)}), (0.42, C.add(side, {"Chest": (2, 0, 0), "Head": (-1, 0, 0)}), {"pelvis": (0.0, -0.778, 0)})]
    out["lie_side"] = keys_dense(C, keys, 5.0, loop=True).clip()
    return out


def side_pose(C):
    """Asleep on the right side (same layout as lie: head toward -z, legs toward +z, face toward +x): knees drawn up,
    the top leg resting on the bottom one, the right arm under the head, the left hand on the ground before the chest."""
    Rp = K.rz(K.D(-90)) @ K.E(-88, 0, 0)
    p = {q: (0, 0, 0) for q in K.PARTS}
    p.update({"Pelvis": K.euler(Rp), "Chest": (14, -50, 20), "Head": (8, 50, -48), "UpperArmR": (-90, 0, 0), "ForearmR": (-90, 0, 0),
              "UpperArmL": (-40, 0, 0), "ForearmL": (-40, 0, 0), "ThighL": (-60, 0, 0), "ThighR": (-50, 0, 0), "ShinL": (70, 0, 0), "ShinR": (70, 0, 0)})
    pel = np.array([0.0, -0.78, 0.0])
    K.leg_ik(p, pel, "R", np.array([0.14, 0.06, 0.62]), K.ry(K.D(10)) @ K.rz(K.D(-80)), pole=np.array([0.6, 0.06, 0.3]))
    K.leg_ik(p, pel, "L", np.array([0.2, 0.17, 0.58]), K.ry(K.D(10)) @ K.rz(K.D(-80)), pole=np.array([0.7, 0.2, 0.25]))
    K.arm_ik(p, pel, "R", np.array([0.16, 0.05, -0.72]), pole=np.array([0.5, 0.04, -0.45]))
    K.arm_ik(p, pel, "L", np.array([0.46, 0.04, -0.42]), pole=np.array([0.3, 0.7, -0.2]))
    return p


def lie_pose(C):
    """On the back (clips.LIE_P layout) with the arms resting on the ground beside the body."""
    p = dict(C.LIE_P)
    p.update({"UpperArmL": (2, 0, -16), "UpperArmR": (2, 0, 14), "ForearmL": (-10, 0, 0), "ForearmR": (-14, 0, 0), "HandL": (0, 0, 10),
              "HandR": (0, 0, -10), "Head": (-8, 0, 6)})
    return p


# ---------------------------------------------------------------- prone: down, idle, crawl, up
PRONE_PEL = np.array([0.0, -0.82, 0.25])      # face down, propped on the forearms, hips 0.25 m ahead of the root


def prone_pose(C, breath=0.0, look=0.0):
    p = {q: (0, 0, 0) for q in K.PARTS}
    p.update({"Pelvis": (90, 0, 0), "Chest": (-25 - breath, 0, 0), "Head": (-42 + breath * 0.5, look, 0)})
    pel = PRONE_PEL.copy()
    for s, sx in (("L", -1), ("R", 1)):
        K.leg_ik(p, pel, s, np.array([sx * 0.15, 0.085, pel[2] - 0.865]), K.rx(K.D(112)) @ K.rz(K.D(-sx * 8)), pole=np.array([sx * 0.2, -0.5, pel[2] - 0.43]))
        K.arm_ik(p, pel, s, np.array([sx * 0.1, 0.035, pel[2] + 0.78]), pole=np.array([sx * 0.25, 0.04, pel[2] + 0.56]))
    return p


def prone(C):
    out = {}
    S = C.STAND
    G = {"ground": True}
    PR = prone_pose(C)
    pp = tuple(PRONE_PEL)
    keys = [(0.0, PR, {"pelvis": pp}), (0.4, prone_pose(C, 2.5, 0), {"pelvis": (pp[0], pp[1] + 0.004, pp[2])}),
            (0.55, prone_pose(C, 0.5, 14), {"pelvis": pp}), (0.75, prone_pose(C, 1.5, -10), {"pelvis": pp})]
    d = keys_dense(C, keys, 5.0, loop=True)
    pin_s(d, "HandL", 0, 5.0, fade=0.0, keep_rot=True); pin_s(d, "HandR", 0, 5.0, fade=0.0, keep_rot=True)
    pin_s(d, "FootL", 0, 5.0, fade=0.0); pin_s(d, "FootR", 0, 5.0, fade=0.0)
    out["prone_idle"] = d.clip()

    kneel = C.add(S, {"Pelvis": (40, 0, 0), "Chest": (10, 0, 0), "Head": (-34, 0, 0), "ThighL": (-40, 0, -3), "ThighR": (-40, 0, 3), "ShinL": (90, 0, 0),
                      "ShinR": (90, 0, 0), "FootL": (50, 0, 0), "FootR": (50, 0, 0), "UpperArmL": (-60, 0, -6), "UpperArmR": (-60, 0, 6)})
    plank = C.add(S, {"Pelvis": (72, 0, 0), "Chest": (-6, 0, 0), "Head": (-40, 0, 0), "ThighL": (-24, 0, -4), "ThighR": (-24, 0, 4), "ShinL": (40, 0, 0),
                      "ShinR": (40, 0, 0), "FootL": (60, 0, 0), "FootR": (60, 0, 0), "UpperArmL": (-80, 0, -6), "UpperArmR": (-80, 0, 6)})
    crouch = C.flat_feet(C.add(S, {"Pelvis": (30, 0, 0), "Chest": (26, 0, 0), "Head": (-36, 0, 0), "UpperArmL": (-50, 0, -8), "UpperArmR": (-50, 0, 8),
                                   "ForearmL": (-20, 0, 0), "ForearmR": (-20, 0, 0), "ThighL": (-70, 0, -4), "ThighR": (-70, 0, 4), "ShinL": (80, 0, 0), "ShinR": (80, 0, 0)}))
    hands = {s: np.array([sx * 0.2, 0.035, 0.62]) for s, sx in (("L", -1), ("R", 1))}
    # go_prone: crouch, hands down, onto the knees, lower the chest as the legs stretch back
    T = 1.1
    keys = [(0.0, S, G), (0.25, crouch, {"pelvis": (0, -0.34, -0.04)}), (0.48, kneel, {"pelvis": (0, -0.44, -0.04)}),
            (0.75, plank, {"pelvis": (0, -0.68, 0.14)}), (1.1, PR, {"pelvis": pp})]
    d = keys_dense(C, [(t / T, p, o) for t, p, o in keys], T)
    st = stand_heels(C)
    L = [standing(-9, 0.28, st["L"], toe_off=True, roll_out=0.12, toe_pitch=30.0)]; R = [standing(-9, 0.3, st["R"], toe_off=True, roll_out=0.12, toe_pitch=30.0)]
    K.plant_feet(d, L + [standing(9, 10, st["L"])], R + [standing(9, 10, st["R"])], knee=lambda s, u, pl: 2.0, until=0.3, fade=0.12)
    for s in "LR":
        pin_s(d, "Hand" + s, 0.42, 0.85, at=hands[s], fade=0.15, keep_rot=False)
    K.floor_fix(d)
    out["go_prone"] = d.clip(events=[("knees", round(0.45 / T, 3)), ("down", round(0.95 / T, 3))])

    # prone_up: push up on the hands, knees under, the left foot forward, stand
    T = 1.2
    keys = [(0.0, PR, {"pelvis": pp}), (0.3, plank, {"pelvis": (0, -0.64, 0.14)}), (0.55, kneel, {"pelvis": (0, -0.44, -0.02)}),
            (0.82, C.add(S, {"Pelvis": (20, 0, 0), "Chest": (12, 0, 0), "Head": (-14, 0, 0), "ThighL": (-60, 0, -4), "ShinL": (70, 0, 0), "ThighR": (-10, 0, 3), "ShinR": (95, 0, 0),
                              "FootR": (40, 0, 0), "UpperArmL": (-30, 0, -10), "UpperArmR": (-20, 0, 12)}), {"pelvis": (0, -0.4, -0.08)}),
            (1.02, C.add(S, {"Chest": (6, 0, 0), "Head": (-4, 0, 0)}), G), (1.2, S, G)]
    d = keys_dense(C, [(t / T, p, o) for t, p, o in keys], T)
    Lp = [standing(0.72, 9.0, st["L"], strike=True, strike_pitch=-6.0, roll_in=0.06)]
    Rp = [standing(0.98, 9.0, st["R"], strike=True, strike_pitch=-6.0, roll_in=0.06)]
    K.plant_feet(d, [standing(-9, -8, st["L"])] + Lp, [standing(-9, -8, st["R"])] + Rp, knee=lambda s, u, pl: 2.0, since=0.85, fade=0.2)
    for s in "LR":
        pin_s(d, "Hand" + s, 0.08, 0.6, at=hands[s], fade=0.12, keep_rot=False)
    K.floor_fix(d)
    out["prone_up"] = d.clip(events=[("standing", round(1.0 / T, 3))])
    out["crawl"] = crawl(C).clip(speed=0.55, gait=True)
    return out


def crawl(C):
    """Leopard crawl (loop, root at 0.55 m/s): forearms and inner knees on the ground, the left elbow reaching with the
    right knee drawn up, hips wriggling. Cycle starts as the left elbow lifts."""
    T, v = 1.2, 0.55
    Sd = v * T
    d = Dense(T, loop=True, mask="full")
    d.set_root(lambda s: (0.0, v * s, 0.0))
    pel0 = PRONE_PEL + np.array([0, -0.01, 0])
    base = prone_pose(C)
    # shoulder / hip world-ish anchors (root-local) for target placement
    W = K.fk(base, pel0)
    sh = {s: W["UpperArm" + s][1] for s in "LR"}
    hp = {s: W["Thigh" + s][1] for s in "LR"}

    def limb(phase0, swing, stance_mid_local, travel, t):
        """World contact z of a limb whose swing starts at phase0 and lasts `swing` (fractions of the cycle)."""
        u = (t / T - phase0) % 1.0
        k = math.floor(t / T - phase0)
        land = lambda kk: v * ((phase0 + kk + swing) * T) + stance_mid_local + travel / 2
        if u < swing:
            a = land(k - 1) ; b = land(k)
            s = u / swing
            return a + (b - a) * K.mj(s), math.sin(math.pi * s)
        return land(k), 0.0
    eb = {"L": 0.0, "R": 0.5}
    kn = {"L": 0.55, "R": 0.05}
    for i, t in enumerate(d.t):
        s = t * T
        phi = t
        pose = dict(base)
        yaw = 7.0 * math.sin(TAU * (phi - 0.2))
        pose["Pelvis"] = (90, 0, yaw)                                    # pelvis roll in its own frame = a yaw on the ground
        pose["Chest"] = (-24 + 2 * math.sin(TAU * 2 * phi), 0, -yaw * 1.4)
        pose["Head"] = (-46, 0, yaw * 0.6)
        pel = pel0 + np.array([0.0, 0.0, 0.03 * math.sin(TAU * 2 * (phi - 0.1))])
        d.pels[i] = pel
        root = d.root[i]
        Wp = K.fk(pose, pel)
        for side, sx in (("L", -1), ("R", 1)):
            # forearm: elbow and wrist on the ground
            z, lift = limb(eb[side], 0.32, sh[side][2] - 0.02, 0.4, s)
            ez = K.to_local(root, np.array([0, 0, z]))[2]
            S_ = Wp["Chest"][1] + Wp["Chest"][0] @ K.OFF["UpperArm" + side]
            Ed = np.array([sx * 0.25, 0.045 + 0.06 * lift, ez])
            E = S_ + (Ed - S_) / np.linalg.norm(Ed - S_) * 0.29
            Wr = E + np.array([-sx * 0.1, -E[1] + 0.035 + 0.04 * lift, 0.22])
            Wr = E + (Wr - E) / np.linalg.norm(Wr - E) * 0.25
            K.arm_ik(pose, pel, side, Wr, pole=E + (E - (S_ + Wr) / 2) * 2)
            # knee drawn up beside the body, shin on the ground
            z, lift = limb(kn[side], 0.32, hp[side][2] - 0.24, 0.4, s)
            kz = K.to_local(root, np.array([0, 0, z]))[2]
            H = Wp["Pelvis"][1] + Wp["Pelvis"][0] @ K.OFF["Thigh" + side]
            Kd = np.array([sx * 0.38, 0.06 + 0.03 * lift, kz])
            Kn = H + (Kd - H) / np.linalg.norm(Kd - H) * 0.43
            A = Kn + np.array([-sx * 0.18, 0.0, -0.4])
            A[1] = 0.07
            A = Kn + (A - Kn) / np.linalg.norm(A - Kn) * 0.43
            K.leg_ik(pose, pel, side, A, K.rx(K.D(115)) @ K.rz(K.D(-sx * 20)), pole=Kn + (Kn - (H + A) / 2) * 2)
        d.poses[i] = pose
    d.root[:] = 0.0
    K.floor_fix(d)
    d.events = [("elbowL", 0.32), ("kneeR", 0.37), ("elbowR", 0.82), ("kneeL", 0.87)]
    return d


def build(C):
    out = {}

    def add(name, dense, **meta):
        out[name] = dense.clip(**meta)

    for name, g in (("walk", WALK), ("run", RUN), ("sprint", SPRINT), ("tired_walk", TIRED_WALK), ("crouch_walk", CROUCH_WALK)):
        add(name, gait_loop(C, g), speed=g["speed"], gait=True)
    add("walk_start", walk_start(C))
    ws, _, _ = walk_stop(C)
    add("walk_stop", ws)
    add("run_stop", run_stop(C))
    tl = turn(C, -90.0)
    add("turn_l90", tl)
    add("turn_r90", tl.mirror(C.mirror))
    out.update(idles(C))
    out.update(reactions(C))
    so, DR, LO = seats(C)
    out.update(so)
    out.update(cars(C, DR, LO))
    out.update(ground(C))
    out.update(prone(C))
    lb = list(C.CLIPS["lie_back"]["keys"]) if "lie_back" in C.CLIPS else []
    if lb:
        out["lie_back"] = dict(C.CLIPS["lie_back"], keys=[(lb[0][0], LO, lb[0][2])] + lb[1:-1] + [(lb[-1][0], lie_pose(C), lb[-1][2])])
    return out


def cars(C, DR, LO):
    out = {}
    for suffix, seated in (("", DR), ("_r", LO)):
        for name, fn, fl in (("car_enter", car_enter, CAR["ground"]), ("car_exit", car_exit, CAR["ground"]),
                             ("car_enter_high", car_enter_high, TRUCK["ground"]), ("car_exit_high", car_exit_high, TRUCK["ground"])):
            d = fn(C, DR, LO, seated if suffix == "" else C.mirror(LO))
            if suffix:
                d = d.mirror(C.mirror)
            out[name + suffix] = d.clip(ground=d.ground, floor=fl)
        d = car_close_door(C, seated if suffix == "" else C.mirror(LO))
        out["car_close_door" + suffix] = (d.mirror(C.mirror) if suffix else d).clip()
        d = car_open_door(C)
        out["car_open_door" + suffix] = (d.mirror(C.mirror) if suffix else d).clip()
    return out
