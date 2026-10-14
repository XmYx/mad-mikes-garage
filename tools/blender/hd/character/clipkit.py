"""Authoring kit for the human clips (pure numpy, no bpy): forward / inverse kinematics on the HumanRig skeleton, dense
clips built frame by frame (sparse keys interpolated like the exporter, then feet planted by IK), a footstep gait
synthesiser, mirroring, and the checks the strips are judged by (foot slide, floor, leg overlap).

Axes and conventions are clips.py's (Unity: +x right, +y up, +z forward; bone rotations `Quaternion.Euler(x, y, z)` on an
identity rest; pelvis offsets in metres for height 1, added to the rest pelvis 0.94 above the root). A clip may carry root
motion: per frame (x, z, yaw) of the root in the clip's start frame (metres, degrees, +yaw turns right); the pelvis offsets
are relative to that moving root. Gait loops have none: the runtime moves the root at `speed`.
"""
import math

import numpy as np

PARTS = ["Pelvis", "Chest", "Head", "UpperArmL", "UpperArmR", "ForearmL", "ForearmR", "HandL", "HandR",
         "ThighL", "ThighR", "ShinL", "ShinR", "FootL", "FootR"]
PARENT = {"Pelvis": None, "Chest": "Pelvis", "Head": "Chest", "UpperArmL": "Chest", "UpperArmR": "Chest", "ForearmL": "UpperArmL",
          "ForearmR": "UpperArmR", "HandL": "ForearmL", "HandR": "ForearmR", "ThighL": "Pelvis", "ThighR": "Pelvis",
          "ShinL": "ThighL", "ShinR": "ThighR", "FootL": "ShinL", "FootR": "ShinR"}
OFF = {"Pelvis": (0, 0.94, 0), "Chest": (0, 0.1, 0), "Head": (0, 0.44, 0), "UpperArmL": (-0.2, 0.39, 0), "UpperArmR": (0.2, 0.39, 0),
       "ForearmL": (0, -0.29, 0), "ForearmR": (0, -0.29, 0), "HandL": (0, -0.25, 0), "HandR": (0, -0.25, 0),
       "ThighL": (-0.09, -0.02, 0), "ThighR": (0.09, -0.02, 0), "ShinL": (0, -0.43, 0), "ShinR": (0, -0.43, 0),
       "FootL": (0, -0.43, 0), "FootR": (0, -0.43, 0)}
OFF = {k: np.array(v, float) for k, v in OFF.items()}
FLOOR = 0.01                                   # heel / toe points rest this high (clips.REST_FLOOR)
HEEL = np.array([0, -0.05, -0.03]); TOE = np.array([0, -0.05, 0.13]); BALL = np.array([0, -0.05, 0.09])
PALM = np.array([0, -0.06, 0.0])
FPS = 30
D = math.radians


# ---------------------------------------------------------------- rotations
def rx(a):
    c, s = math.cos(a), math.sin(a); return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])


def ry(a):
    c, s = math.cos(a), math.sin(a); return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])


def rz(a):
    c, s = math.cos(a), math.sin(a); return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


def E(x, y=0.0, z=0.0):
    """Unity Quaternion.Euler(x, y, z) as a matrix (degrees): Ry Rx Rz."""
    return ry(D(y)) @ rx(D(x)) @ rz(D(z))


def euler(R):
    """Matrix -> Unity Euler (x, y, z) degrees (inverse of E)."""
    sx = -R[1, 2]
    sx = max(-1.0, min(1.0, sx))
    x = math.asin(sx)
    if abs(math.cos(x)) < 1e-7:
        if sx > 0:
            y = math.atan2(R[0, 1], R[0, 0])
        else:
            y = math.atan2(-R[0, 1], R[0, 0])
        z = 0.0
    else:
        y = math.atan2(R[0, 2], R[2, 2]); z = math.atan2(R[1, 0], R[1, 1])
    return (math.degrees(x), math.degrees(y), math.degrees(z))


def quat(R):
    m = R
    tr = m[0, 0] + m[1, 1] + m[2, 2]
    if tr > 0:
        s = math.sqrt(tr + 1.0) * 2
        return np.array([(m[2, 1] - m[1, 2]) / s, (m[0, 2] - m[2, 0]) / s, (m[1, 0] - m[0, 1]) / s, 0.25 * s])
    if m[0, 0] > m[1, 1] and m[0, 0] > m[2, 2]:
        s = math.sqrt(1.0 + m[0, 0] - m[1, 1] - m[2, 2]) * 2
        return np.array([0.25 * s, (m[0, 1] + m[1, 0]) / s, (m[0, 2] + m[2, 0]) / s, (m[2, 1] - m[1, 2]) / s])
    if m[1, 1] > m[2, 2]:
        s = math.sqrt(1.0 + m[1, 1] - m[0, 0] - m[2, 2]) * 2
        return np.array([(m[0, 1] + m[1, 0]) / s, 0.25 * s, (m[1, 2] + m[2, 1]) / s, (m[0, 2] - m[2, 0]) / s])
    s = math.sqrt(1.0 + m[2, 2] - m[0, 0] - m[1, 1]) * 2
    return np.array([(m[0, 2] + m[2, 0]) / s, (m[1, 2] + m[2, 1]) / s, 0.25 * s, (m[1, 0] - m[0, 1]) / s])


def qmat(q):
    x, y, z, w = q / np.linalg.norm(q)
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def smooth(u):
    u = min(1.0, max(0.0, u)); return u * u * (3 - 2 * u)


def mj(u):
    """Minimum-jerk 0..1."""
    u = min(1.0, max(0.0, u)); return u ** 3 * (10 - 15 * u + 6 * u * u)


def interp(points, u):
    """Piecewise smoothstep through [(u, v)] (clamped)."""
    if u <= points[0][0]:
        return points[0][1]
    for (a, va), (b, vb) in zip(points, points[1:]):
        if u <= b:
            return va + (vb - va) * smooth((u - a) / max(1e-9, b - a))
    return points[-1][1]


# ---------------------------------------------------------------- forward kinematics (root-local unless root given)
def fk(pose, pel, root=None):
    """{part: (R 3x3, p 3)} for a pose dict of Euler triples and a pelvis offset; root = (x, z, yaw) places it."""
    W = {}
    for p in PARTS:
        R = E(*pose.get(p, (0, 0, 0)))
        if p == "Pelvis":
            W[p] = (R, OFF[p] + np.asarray(pel, float))
        else:
            Rp, pp = W[PARENT[p]]
            W[p] = (Rp @ R, pp + Rp @ OFF[p])
    if root is not None:
        Ry_, t = ry(D(root[2])), np.array([root[0], 0.0, root[1]])
        W = {k: (Ry_ @ R, Ry_ @ p + t) for k, (R, p) in W.items()}
    return W


def point(W, part, local):
    R, p = W[part]
    return p + R @ np.asarray(local, float)


def to_local(root, pw):
    """World (clip start frame) point -> root-local."""
    return ry(-D(root[2])) @ (np.asarray(pw, float) - np.array([root[0], 0.0, root[1]]))


def to_world(root, pl):
    return ry(D(root[2])) @ np.asarray(pl, float) + np.array([root[0], 0.0, root[1]])


# ---------------------------------------------------------------- two-bone IK
def two_bone(parent_R, A, L1, L2, T, pole, sign):
    """Upper-bone world rotation and middle-joint flex (deg) putting the chain end at T from joint A, the middle joint
    bent toward `pole`. sign +1: legs (shin +x bends the knee), -1: arms (forearm -x bends the elbow)."""
    d_vec = T - A
    d = np.linalg.norm(d_vec)
    d = min(max(d, abs(L1 - L2) + 1e-4), L1 + L2 - 1e-5)
    cos_g = (L1 * L1 + L2 * L2 - d * d) / (2 * L1 * L2)
    f = math.pi - math.acos(max(-1.0, min(1.0, cos_g)))
    v = np.array([0, -L1, 0]) + rx(sign * f) @ np.array([0, -L2, 0])
    m = np.array([0, -L1, 0.0])
    e1 = v / np.linalg.norm(v)
    e2 = m - e1 * (m @ e1)
    if np.linalg.norm(e2) < 1e-6:
        e2 = np.array([0, 0, sign * 1.0]) - e1 * (sign * e1[2])
    e2 /= np.linalg.norm(e2)
    e3 = np.cross(e1, e2)
    tdir = d_vec / max(1e-9, np.linalg.norm(d_vec))
    pv = pole - A
    p2 = pv - tdir * (pv @ tdir)
    if np.linalg.norm(p2) < 1e-6:
        p2 = np.array([0, 0, 1.0]) - tdir * tdir[2]
    p2 /= np.linalg.norm(p2)
    f3 = np.cross(tdir, p2)
    Rw = np.column_stack([tdir, p2, f3]) @ np.column_stack([e1, e2, e3]).T
    return Rw, math.degrees(sign * f)


def leg_ik(pose, pel, side, ankle, foot_rot=None, pole=None):
    """Sets Thigh/Shin (and Foot when foot_rot, a root-local rotation, is given) so the ankle reaches `ankle`
    (root-local). Returns the miss distance (m)."""
    W = fk(pose, pel)
    Rp, pp = W["Pelvis"]
    A = pp + Rp @ OFF["Thigh" + side]
    Rb = Rp @ E(*pose.get("Thigh" + side, (0, 0, 0)))
    if pole is None:                                                  # knees go the way the thigh faces, a little out
        pole = A + Rp @ np.array([0.18 if side == "R" else -0.18, 0, 0]) + Rb @ np.array([0, -0.2, 1.0])
    Rw, f = two_bone(Rp, A, 0.43, 0.43, np.asarray(ankle, float), pole, +1)
    pose["Thigh" + side] = euler(Rp.T @ Rw)
    pose["Shin" + side] = (f, 0.0, 0.0)
    shinW = Rw @ E(f, 0, 0)
    if foot_rot is not None:
        pose["Foot" + side] = euler(shinW.T @ foot_rot)
    end = A + Rw @ (np.array([0, -0.43, 0]) + E(f, 0, 0) @ np.array([0, -0.43, 0]))
    return float(np.linalg.norm(end - ankle))


def arm_ik(pose, pel, side, wrist, hand_rot=None, pole=None):
    """Sets UpperArm/Forearm (and Hand when hand_rot, root-local, is given) so the wrist reaches `wrist`; the elbow
    stays toward where the base pose had it (or `pole`, root-local). Returns the miss distance."""
    W = fk(pose, pel)
    Rc, pc = W["Chest"]
    A = pc + Rc @ OFF["UpperArm" + side]
    Rb = Rc @ E(*pose.get("UpperArm" + side, (0, 0, 0)))
    if pole is None:
        pole = A + Rb @ np.array([0, -0.29, -1.0])                    # elbows behind the upper arm's line
    Rw, f = two_bone(Rc, A, 0.29, 0.25, np.asarray(wrist, float), np.asarray(pole, float), -1)
    pose["UpperArm" + side] = euler(Rc.T @ Rw)
    pose["Forearm" + side] = (f, 0.0, 0.0)
    if hand_rot is not None:
        pose["Hand" + side] = euler((Rw @ E(f, 0, 0)).T @ hand_rot)
    end = A + Rw @ (np.array([0, -0.29, 0]) + E(f, 0, 0) @ np.array([0, -0.25, 0]))
    return float(np.linalg.norm(end - wrist))


def leg_reach_y(pose, pel, side, ankle, flex):
    """Highest pelvis offset y with which the leg reaches `ankle` with the knee bent `flex` degrees (None: never)."""
    W = fk(pose, (pel[0], 0.0, pel[2]))
    Rp, pp = W["Pelvis"]
    A = pp + Rp @ OFF["Thigh" + side]
    d = math.sqrt(0.43 ** 2 * 2 + 2 * 0.43 * 0.43 * math.cos(D(flex)))
    dxz2 = (A[0] - ankle[0]) ** 2 + (A[2] - ankle[2]) ** 2
    if dxz2 >= d * d:
        return None
    return ankle[1] + math.sqrt(d * d - dxz2) - A[1]


# ---------------------------------------------------------------- interpolation of sparse keys (like the exporter)
def _slopes(ts, vs, cyclic):
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
        dd = (vs[inx] - vs[ip]) / max(1e-6, tn - tp)
        ext = (vs[i] - vs[ip]) * (vs[inx] - vs[i]) <= 0
        m[i] = np.where(ext, 0.0, dd)
    return m


def _hermite(ts, vs, ms, t):
    i = int(np.searchsorted(ts, t, side="right") - 1)
    i = min(max(i, 0), len(ts) - 2)
    h = ts[i + 1] - ts[i]
    u = 0.0 if h <= 0 else (t - ts[i]) / h
    h00 = 2 * u ** 3 - 3 * u ** 2 + 1; h10 = u ** 3 - 2 * u ** 2 + u; h01 = -2 * u ** 3 + 3 * u ** 2; h11 = u ** 3 - u ** 2
    return h00 * vs[i] + h10 * h * ms[i] + h01 * vs[i + 1] + h11 * h * ms[i + 1]


def sampler(keys, loop, resolve):
    """f(t 0..1) -> (pose dict, pelvis offset) through the keys, the exporter's curves (quaternions, AUTO_CLAMPED)."""
    keys = sorted(keys, key=lambda k: k[0])
    if loop and keys[0][0] == 0.0:
        keys = keys + [(1.0, keys[0][1], keys[0][2])]
    ts = np.array([k[0] for k in keys])
    Q = np.zeros((len(keys), len(PARTS), 4)); P = np.zeros((len(keys), 3))
    for k, (t, pose, opts) in enumerate(keys):
        full, pel = resolve(pose, opts)
        P[k] = pel
        for b, p in enumerate(PARTS):
            q = quat(E(*full[p]))
            if k > 0 and np.dot(q, Q[k - 1, b]) < 0:
                q = -q
            Q[k, b] = q
    MQ = [_slopes(ts, Q[:, b, :], loop) for b in range(len(PARTS))]
    MP = _slopes(ts, P, loop)

    def f(t):
        t = min(max(t, ts[0]), ts[-1])
        pose = {}
        for b, p in enumerate(PARTS):
            q = _hermite(ts, Q[:, b, :], MQ[b], t)
            pose[p] = euler(qmat(q))
        return pose, _hermite(ts, P, MP, t)
    return f


# ---------------------------------------------------------------- dense clips
class Dense:
    """A clip as one pose per frame (30 fps). Build the base from sparse keys, then plant feet / pin hands, set root
    motion, door curve and events; `clip()` gives the CLIPS entry (dense keys + extras)."""

    def __init__(self, length, loop=False, mask="full"):
        self.length, self.loop, self.mask = length, loop, mask
        self.n = max(2, int(round(length * FPS)))
        self.frames = self.n if loop else self.n + 1
        self.t = [i / self.n for i in range(self.frames)]
        self.poses = [dict() for _ in range(self.frames)]
        self.pels = [np.zeros(3) for _ in range(self.frames)]
        self.root = np.zeros((self.frames, 3))
        self.door = None
        self.events = []
        self.props = []
        self.views = None
        self.ground = None

    def from_keys(self, keys, resolve):
        f = sampler(keys, self.loop, resolve)
        for i, t in enumerate(self.t):
            pose, pel = f(t)
            self.poses[i], self.pels[i] = pose, np.array(pel, float)
        return self

    def sec(self, i):
        return self.t[i] * self.length

    def world(self, i):
        return fk(self.poses[i], self.pels[i], self.root[i])

    def set_root(self, fn):
        """fn(seconds) -> (x, z, yaw)."""
        for i in range(self.frames):
            self.root[i] = fn(self.sec(i))
        return self

    def pin(self, limb, t0, t1, at=None, rot=None, fade=0.06, keep_rot=True, pole=None):
        """Holds a hand or foot where it is (world, clip start frame) at t0 (or `at`, world) between t0 and t1 (0..1),
        fading in/out over `fade`. Feet keep their world rotation too (keep_rot); `rot` overrides it (world)."""
        i0 = min(range(self.frames), key=lambda i: abs(self.t[i] - t0))
        W0 = self.world(i0)
        Rr = ry(D(self.root[i0][2]))
        tgt = np.asarray(at, float) if at is not None else W0[limb][1]
        rw = rot if rot is not None else (W0[limb][0] if keep_rot else None)
        side = limb[-1]
        for i, t in enumerate(self.t):
            if t < t0 - fade or t > t1 + fade:
                continue
            w = 1.0 if t0 <= t <= t1 else smooth(1 - (t0 - t) / fade) if t < t0 else smooth(1 - (t - t1) / fade)
            if w <= 0:
                continue
            pose = dict(self.poses[i])
            root = self.root[i]
            loc = to_local(root, tgt)
            rl = None if rw is None else ry(-D(root[2])) @ rw
            if limb.startswith("Foot"):
                leg_ik(pose, self.pels[i], side, loc, rl)
                parts = ("Thigh" + side, "Shin" + side, "Foot" + side)
            else:
                arm_ik(pose, self.pels[i], side, loc, rl, None if pole is None else to_local(root, pole))
                parts = ("UpperArm" + side, "Forearm" + side, "Hand" + side)
            for p in parts:
                self.poses[i][p] = slerp_e(self.poses[i][p], pose[p], w)
        return self

    def mirror(self, mirror_pose):
        d = Dense(self.length, self.loop, self.mask)
        d.poses = [mirror_pose(p) for p in self.poses]
        d.pels = [np.array([-p[0], p[1], p[2]]) for p in self.pels]
        d.root = self.root * np.array([-1, 1, -1])
        d.door = None if self.door is None else list(self.door)
        d.events = [(swap_lr(n), t) for n, t in self.events]
        d.props = [[(-x, y, z) for x, y, z in line] for line in self.props]
        d.views = self.views
        d.ground = self.ground
        return d

    def clip(self, **meta):
        keys = [(t, self.poses[i], {"pelvis": tuple(float(v) for v in self.pels[i])}) for i, t in enumerate(self.t)]
        c = dict(length=self.length, loop=self.loop, mask=self.mask, keys=keys, dense=True)
        if np.abs(self.root).max() > 1e-6:
            c["root"] = [tuple(float(v) for v in r) for r in self.root]
        if self.door is not None:
            c["door"] = [float(v) for v in self.door]
        if self.ground is not None:
            c["ground"] = [float(v) for v in self.ground]
        if self.events:
            c["events"] = sorted(self.events, key=lambda e: e[1])
        if self.props:
            c["props"] = self.props
        if self.views:
            c["views"] = self.views
        c.update(meta)
        return c


def slerp_e(a, b, w):
    if w >= 0.9999:
        return b
    qa, qb = quat(E(*a)), quat(E(*b))
    if np.dot(qa, qb) < 0:
        qb = -qb
    return euler(qmat(qa * (1 - w) + qb * w))


def swap_lr(name):
    return name[:-1] + {"L": "R", "R": "L"}[name[-1]] if name and name[-1] in "LR" else name


# ---------------------------------------------------------------- feet from a footstep plan
class Plant:
    """A foot on the ground from t_on to t_off (seconds): heel point at world (x, z), yaw (deg, + toes right).
    strike: rolls in from `strike_pitch` (deg; < 0 heel first, > 0 forefoot first) over `roll_in` s; toe_off: rolls up
    onto the toe to `toe_pitch` over the last `roll_out` s."""

    def __init__(self, t_on, t_off, x, z, yaw=0.0, strike=True, toe_off=True, strike_pitch=-16.0, toe_pitch=36.0,
                 roll_in=0.1, roll_out=0.2, y=FLOOR):
        self.t_on, self.t_off, self.x, self.z, self.yaw = t_on, t_off, x, z, yaw
        self.strike, self.toe_off, self.sp, self.tp, self.ri, self.ro, self.y = strike, toe_off, strike_pitch, toe_pitch, roll_in, roll_out, y

    def heel(self):
        return np.array([self.x, self.y, self.z])

    def foot(self, t):
        """(ankle world, rotation world) at time t inside the plant."""
        Ry_ = ry(D(self.yaw))
        p = 0.0
        pivot = "heel"
        if self.strike and t < self.t_on + self.ri:
            p = self.sp * (1 - smooth((t - self.t_on) / self.ri))
            pivot = "heel" if self.sp < 0 else "ball"
        if self.toe_off and t > self.t_off - self.ro:
            u = (t - (self.t_off - self.ro)) / self.ro
            p = self.tp * min(1.0, max(0.0, u)) ** 1.6
            pivot = "ball"
        R = Ry_ @ rx(D(p))
        h = self.heel()
        if pivot == "heel":
            return h - R @ HEEL, R
        toe = h + Ry_ @ (TOE - HEEL)                                       # a rigid foot rolls over the toe tip
        return toe - R @ TOE, R

    def ankle_flat(self):
        return self.heel() - ry(D(self.yaw)) @ HEEL


def feet_track(plants, t, lift=0.08, swing_pitch=-6.0):
    """(ankle, rotation, stance, stance progress) of one foot at time t from its sorted plants: inside a plant it rolls,
    between two it swings (minimum-jerk travel, lifted, toes up before the strike)."""
    for k, pl in enumerate(plants):
        if pl.t_on <= t <= pl.t_off:
            a, R = pl.foot(t)
            return a, R, True, (t - pl.t_on) / max(1e-6, pl.t_off - pl.t_on), pl
        if k + 1 < len(plants) and pl.t_off < t < plants[k + 1].t_on:
            nx = plants[k + 1]
            a0, R0 = pl.foot(pl.t_off)
            a1, R1 = nx.foot(nx.t_on)
            s = (t - pl.t_off) / (nx.t_on - pl.t_off)
            m = mj(s)
            a = a0 + (a1 - a0) * m
            a[1] += lift * math.sin(math.pi * min(1.0, s ** 0.8)) ** 1.2
            dist = abs(a1[2] - a0[2]) + abs(a1[0] - a0[0])
            p0 = pl.tp if pl.toe_off else 0.0
            p1 = nx.sp if nx.strike else 0.0
            pitch = interp([(0.0, p0), (0.45, max(-4.0, swing_pitch * 0.4)), (0.8, swing_pitch), (1.0, p1)], s) if dist > 0.08 else p0 + (p1 - p0) * smooth(s)
            yaw = pl.yaw + (nx.yaw - pl.yaw) * smooth(s)
            R = ry(D(yaw)) @ rx(D(pitch))
            return a, R, False, s, pl
    pl = plants[0] if t < plants[0].t_on else plants[-1]
    a, R = pl.foot(min(max(t, pl.t_on), pl.t_off))
    return a, R, True, 0.5, pl


def plant_feet(dense, plantsL, plantsR, knee=None, y_cap=True, smooth_y=1.0, lift=0.08, gravity=9.81, flight=True,
               swing_pitch=-6.0, since=None, until=None, fade=0.15):
    """Legs by IK from the footstep plans; the pelvis height is the lowest of the base pose's and of what each
    standing leg reaches with the knee flex `knee(side, progress)` asks for (in the air: a ballistic arc between take-off
    and touchdown). Outside [since, until] (seconds) the legs and pelvis height are the base pose's, blended over `fade`."""
    n = dense.frames
    track = []
    for i in range(n):
        tsec = dense.sec(i)
        st = {}
        for side, plants in (("L", plantsL), ("R", plantsR)):
            st[side] = feet_track(plants, tsec, lift, swing_pitch)
        track.append(st)
    ys = np.zeros(n); grounded = np.zeros(n, bool)
    for i in range(n):
        root = dense.root[i]
        pose, pel = dense.poses[i], dense.pels[i]
        y = pel[1] if y_cap else 9.0
        any_st = False
        for side in "LR":
            a, R, stance, u, pl = track[i][side]
            if not stance:
                continue
            any_st = True
            loc = to_local(root, a)
            flex = knee(side, u, pl) if knee else 2.0
            yr = leg_reach_y(pose, pel, side, loc, flex)
            if yr is None:
                yr = leg_reach_y(pose, pel, side, loc, 0.5) or y
            y = min(y, yr)
        ys[i] = y if any_st else np.nan
        grounded[i] = any_st
    # in the air: a ballistic arc between take-off and touchdown
    if (~grounded).any():
        idx = np.arange(n)
        for i in range(n):
            if grounded[i]:
                continue
            if dense.loop:
                a = next(j for j in range(1, n) if grounded[(i - j) % n]); b = next(j for j in range(1, n) if grounded[(i + j) % n])
                ya, yb = ys[(i - a) % n], ys[(i + b) % n]
                ta, tb = -a / FPS, b / FPS
            else:
                prev = [j for j in idx[:i] if grounded[j]]; nxt = [j for j in idx[i + 1:] if grounded[j]]
                if not prev or not nxt:
                    ys[i] = ys[prev[-1]] if prev else ys[nxt[0]]
                    continue
                ya, yb, ta, tb = ys[prev[-1]], ys[nxt[0]], (prev[-1] - i) / FPS, (nxt[0] - i) / FPS
            u = -ta / (tb - ta)
            ys[i] = ya + (yb - ya) * u + (0.5 * gravity * (-ta) * tb if flight else 0.0)
    if smooth_y > 0:
        k = np.exp(-0.5 * (np.arange(-4, 5) / smooth_y) ** 2); k /= k.sum()
        if dense.loop:
            ys2 = np.array([sum(k[j + 4] * ys[(i + j) % n] for j in range(-4, 5)) for i in range(n)])
        else:
            pad = np.concatenate([[ys[0]] * 4, ys, [ys[-1]] * 4])
            ys2 = np.convolve(pad, k, mode="valid")
        ys = np.minimum(ys2, ys + 0.004)
    for i in range(n):
        tsec = dense.sec(i)
        w = 1.0
        if since is not None and tsec < since:
            w = 1.0 - smooth((since - tsec) / fade)
        if until is not None and tsec > until:
            w = min(w, 1.0 - smooth((tsec - until) / fade))
        if w <= 0.0:
            continue
        base = dict(dense.poses[i])
        y = dense.pels[i][1] + (ys[i] - dense.pels[i][1]) * w
        dense.pels[i] = np.array([dense.pels[i][0], y, dense.pels[i][2]])
        root = dense.root[i]
        Rr = ry(-D(root[2]))
        for side in "LR":
            a, R, stance, u, pl = track[i][side]
            leg_ik(dense.poses[i], dense.pels[i], side, to_local(root, a), Rr @ R)
            if w < 1.0:
                for p in ("Thigh" + side, "Shin" + side, "Foot" + side):
                    dense.poses[i][p] = slerp_e(base[p], dense.poses[i][p], w)
    # footstep events
    for side, plants in (("L", plantsL), ("R", plantsR)):
        for pl in plants:
            if pl.t_on > -1e-6 and (pl.strike or pl.t_on > 1e-6) and pl.t_on / dense.length <= 1.0 + 1e-6 and (not dense.loop or pl.t_on / dense.length < 1.0 - 1e-6):
                dense.events.append(("step" + side, round(pl.t_on / dense.length, 4)))
    dense.events = sorted(set(dense.events), key=lambda e: e[1])
    return dense


# ---------------------------------------------------------------- checks
def contacts(W):
    out = {}
    for s in "LR":
        out["heel" + s] = point(W, "Foot" + s, HEEL); out["toe" + s] = point(W, "Foot" + s, TOE)
    return out


def root_at(data, f, speed=0.0, length=1.0):
    if data.get("rootX"):
        return (data["rootX"][f], data["rootZ"][f], data["rootYaw"][f])
    return (0.0, speed * length * f / max(1, data["frames"] - (0 if data["loop"] else 1)), 0.0)


def pose_of(data, f):
    R = np.array(data["rot"]).reshape(-1, len(PARTS), 4)[f]
    pel = np.array(data["pelvis"]).reshape(-1, 3)[f]
    return {p: euler(qmat(R[b])) for b, p in enumerate(PARTS)}, pel


def slide(data):
    """Foot slide (m/s) of heel / toe points in contact (≤ 6 mm over the floor) in two neighbouring frames, world space with
    the clip's root motion (gait loops: the root moves at `speed`). Returns (worst, mean, worst frame)."""
    n = data["frames"]
    pts = []
    for f in range(n):
        pose, pel = pose_of(data, f)
        pts.append(contacts(fk(pose, pel, root_at(data, f, data.get("speed", 0.0), data["length"]))))
    worst, tot, cnt, wf = 0.0, 0.0, 0, -1
    pairs = list(zip(range(n - 1), range(1, n))) + ([(n - 1, 0)] if data["loop"] else [])
    for a, b in pairs:
        for k in pts[a]:
            pa, pb = pts[a][k], pts[b][k].copy()
            if data["loop"] and b == 0:
                pb[2] += data.get("speed", 0.0) * data["length"]
            if pa[1] < FLOOR + 0.006 and pb[1] < FLOOR + 0.006:
                v = math.hypot(pb[0] - pa[0], pb[2] - pa[2]) * FPS
                tot += v; cnt += 1
                if v > worst:
                    worst, wf = v, a
    return worst, tot / max(1, cnt), wf


# body radii for the floor test (w = 1): ellipsoid sections for trunk / head, capsules for limbs
_CAPS = {"UpperArm": (0.29, 0.047, 0.04), "Forearm": (0.25, 0.039, 0.031), "Hand": (0.09, 0.025, 0.022),
         "Thigh": (0.43, 0.075, 0.055), "Shin": (0.43, 0.052, 0.04)}


def _ell_support(R, c, radii, down=np.array([0, -1.0, 0])):
    dl = R.T @ down
    return (c + R @ (radii ** 2 * dl) / max(1e-9, np.linalg.norm(radii * dl)))[1]


def body_floor(W):
    """Lowest body surface point (y) of a posed body (approximate shapes); also which part."""
    low, what = 9.0, ""
    Rp, pp = W["Pelvis"]
    y = _ell_support(Rp, pp + Rp @ np.array([0, 0.02, 0]), np.array([0.15, 0.1, 0.1]))
    if y < low:
        low, what = y, "Pelvis"
    Rc, pc = W["Chest"]
    for k in range(9):
        u = k / 8.0
        rxx = 0.13 + 0.055 * smooth(min(1.0, u * 1.3)); rzz = 0.085 + 0.02 * u
        y = _ell_support(Rc, pc + Rc @ np.array([0, 0.44 * u, 0]), np.array([rxx, 0.02, rzz]))
        if y < low:
            low, what = y, "Chest"
    Rh, ph = W["Head"]
    y = _ell_support(Rh, ph + Rh @ np.array([0, 0.2, 0.005]), np.array([0.082, 0.108, 0.098]))
    if y < low:
        low, what = y, "Head"
    for s in "LR":
        for name, (L, r0, r1) in _CAPS.items():
            R, p = W[name + s]
            for k in range(6):
                u = k / 5.0
                y = (p + R @ np.array([0, -L * u, 0]))[1] - (r0 + (r1 - r0) * u)
                if y < low:
                    low, what = y, name + s
        for pt in (HEEL, TOE):
            y = point(W, "Foot" + s, pt)[1] - 0.0
            if y < low:
                low, what = y, "Foot" + s
    return low, what


def floor_check(data):
    """(lowest surface y over the clip, frame, part) in root space (the floor is y = 0 unless the clip says)."""
    worst = (9.0, -1, "")
    for f in range(data["frames"]):
        pose, pel = pose_of(data, f)
        low, what = body_floor(fk(pose, pel))
        if low < worst[0]:
            worst = (low, f, what)
    return worst


def _seg_dist(a0, a1, b0, b1):
    best = 9.0
    for i in range(7):
        p = a0 + (a1 - a0) * i / 6
        for j in range(7):
            q = b0 + (b1 - b0) * j / 6
            best = min(best, float(np.linalg.norm(p - q)))
    return best


def legs_check(data):
    """Worst overlap (m, > 0 sinks in) of the left and right thighs / shins (capsules, w = 1.1)."""
    worst = (0.0, -1, "")
    for f in range(data["frames"]):
        pose, pel = pose_of(data, f)
        W = fk(pose, pel)
        segs = {}
        for s in "LR":
            for name in ("Thigh", "Shin"):
                R, p = W[name + s]
                segs[name + s] = (p, p + R @ np.array([0, -0.43, 0]), _CAPS[name][1] * 1.1)
        for a in ("ThighL", "ShinL"):
            for b in ("ThighR", "ShinR"):
                pa, qa, ra = segs[a]; pb, qb, rb = segs[b]
                d = ra + rb - _seg_dist(pa, qa, pb, qb)
                if d > worst[0]:
                    worst = (d, f, a + "-" + b)
    return worst


def aim(pose, pel, bone, target, up_hint=None):
    """Turns `bone` (local rotation) so its -y axis points at `target` (root-local); its z axis stays as close as it
    can to `up_hint` (root-local) or to where it was."""
    W = fk(pose, pel)
    par = PARENT[bone]
    Rp = W[par][0] if par else np.eye(3)
    Rb, pb = W[bone]
    d = np.asarray(target, float) - pb
    d /= max(1e-9, np.linalg.norm(d))
    y_new = -d
    z_old = Rb @ np.array([0, 0, 1.0]) if up_hint is None else np.asarray(up_hint, float)
    z_new = z_old - y_new * (z_old @ y_new)
    if np.linalg.norm(z_new) < 1e-6:
        z_new = Rb @ np.array([1.0, 0, 0])
        z_new = z_new - y_new * (z_new @ y_new)
    z_new /= np.linalg.norm(z_new)
    x_new = np.cross(y_new, z_new)
    pose[bone] = euler(Rp.T @ np.column_stack([x_new, y_new, z_new]))


def blend_pose(a, b, w):
    return {p: slerp_e(a.get(p, (0, 0, 0)), b.get(p, (0, 0, 0)), w) for p in PARTS}


def floor_fix(dense, floor=0.0, knee_r=0.05, elbow_r=0.045):
    """Keeps knees, feet and elbows out of the floor frame by frame (y = floor in root space): a sunk knee / elbow
    swings its upper bone up onto the floor, a sunk foot is lifted with leg IK (its world rotation kept)."""
    for i in range(dense.frames):
        pose, pel = dense.poses[i], dense.pels[i]
        for s in "LR":
            W = fk(pose, pel)
            kn = W["Shin" + s][1]
            if kn[1] < floor + knee_r:
                aim(pose, pel, "Thigh" + s, np.array([kn[0], floor + knee_r, kn[2]]))
                W = fk(pose, pel)
            Rf, af = W["Foot" + s]
            low = min(point(W, "Foot" + s, HEEL)[1], point(W, "Foot" + s, TOE)[1], af[1] - 0.04)
            if low < floor + FLOOR - 0.002:
                kn = W["Shin" + s][1]
                leg_ik(pose, pel, s, af + np.array([0, floor + FLOOR - low, 0]), Rf, pole=kn + (kn - W["Thigh" + s][1]) * 0.3)
            W = fk(pose, pel)
            el = W["Forearm" + s][1]
            if el[1] < floor + elbow_r:
                Rh = W["Hand" + s][0]; wr = W["Hand" + s][1]
                aim(pose, pel, "UpperArm" + s, np.array([el[0], floor + elbow_r, el[2]]))
                W = fk(pose, pel)
                if wr[1] < floor + 0.03:
                    wr = np.array([wr[0], floor + 0.03, wr[2]])
                aim(pose, pel, "Forearm" + s, wr)
            W = fk(pose, pel)
            wr = W["Hand" + s][1]
            if wr[1] < floor + 0.03:
                el = W["Forearm" + s][1]
                aim(pose, pel, "Forearm" + s, el + (np.array([wr[0], floor + 0.03, wr[2]]) - el))
                W = fk(pose, pel)
            tip = point(W, "Hand" + s, (0, -0.09, 0))
            if tip[1] < floor + 0.022:
                aim(pose, pel, "Hand" + s, np.array([tip[0], floor + 0.022, tip[2]]))
    return dense
