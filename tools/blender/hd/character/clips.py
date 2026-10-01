"""Keyframed animation clips for the HD human (and the voxel HumanRig: same bones, same identity rest pose).

Pure python + numpy (no bpy): `anims.py` keys these on the HD armature in Blender, lets Blender's Bezier curves
interpolate, samples the result and writes the game clips. Run `python3 clips.py` for a grounding / sanity report.

Conventions (HumanAnimator): every bone's local rotation is Unity `Quaternion.Euler(x, y, z)` in degrees on top of an
identity rest (arms hanging, legs straight). Thigh / upper arm x < 0 swings the limb forward, shin x > 0 bends the knee,
forearm x < 0 bends the elbow, foot x > 0 points the toes down, chest / head x > 0 lean forward, pelvis y > 0 brings
the left hip forward, left limbs z < 0 / right limbs z > 0 spread them outward. Pelvis offsets are metres (game
space, height 1) added to the rest pelvis position.

A clip: dict(length seconds, loop, mask full|upper|lower, keys=[(t 0..1, pose dict, opts)]); opts:
  pelvis=(x, y, z)  explicit pelvis offset; ground=True computes y so the lowest heel/toe touches the floor (+ lift);
  lift=dy           added after grounding (flight phase of a run, jump).
Loops repeat their first key at t = 1. Clip metadata: speed (m/s the cycle was authored for, gaits), hit (0..1
impact moment for actions; the game maps it onto the tool's strikeAt), gait (cycle starts at left heel strike).
"""
import math

import numpy as np

PARTS = ["Pelvis", "Chest", "Head", "UpperArmL", "UpperArmR", "ForearmL", "ForearmR", "HandL", "HandR",
         "ThighL", "ThighR", "ShinL", "ShinR", "FootL", "FootR"]
UPPER = {"Chest", "Head", "UpperArmL", "UpperArmR", "ForearmL", "ForearmR", "HandL", "HandR"}
LOWER = {"Pelvis", "ThighL", "ThighR", "ShinL", "ShinR", "FootL", "FootR"}
REST_FLOOR = 0.01     # heel / toe height above the floor at rest (ankle 0.06)


def skeleton(h=1.0, w=1.0):
    return {
        "Pelvis": (None, (0, 0.94 * h, 0)), "Chest": ("Pelvis", (0, 0.1 * h, 0)), "Head": ("Chest", (0, 0.44 * h, 0)),
        "UpperArmL": ("Chest", (-0.2 * w, 0.39 * h, 0)), "UpperArmR": ("Chest", (0.2 * w, 0.39 * h, 0)),
        "ForearmL": ("UpperArmL", (0, -0.29 * h, 0)), "ForearmR": ("UpperArmR", (0, -0.29 * h, 0)),
        "HandL": ("ForearmL", (0, -0.25 * h, 0)), "HandR": ("ForearmR", (0, -0.25 * h, 0)),
        "ThighL": ("Pelvis", (-0.09 * w, -0.02 * h, 0)), "ThighR": ("Pelvis", (0.09 * w, -0.02 * h, 0)),
        "ShinL": ("ThighL", (0, -0.43 * h, 0)), "ShinR": ("ThighR", (0, -0.43 * h, 0)),
        "FootL": ("ShinL", (0, -0.43 * h, 0)), "FootR": ("ShinR", (0, -0.43 * h, 0)),
    }


def unity_euler(x, y, z):
    def rx(a):
        c, s = math.cos(a), math.sin(a); return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])
    def ry(a):
        c, s = math.cos(a), math.sin(a); return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
    def rz(a):
        c, s = math.cos(a), math.sin(a); return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])
    r = math.radians
    return ry(r(y)) @ rx(r(x)) @ rz(r(z))


def fk(pose, pelvis=(0, 0, 0), skel=None):
    skel = skel or skeleton()
    W = {}
    for p in PARTS:
        par, off = skel[p]
        M = np.eye(4)
        M[:3, 3] = np.add(off, pelvis) if p == "Pelvis" else off
        M[:3, :3] = unity_euler(*pose.get(p, (0, 0, 0)))
        W[p] = (W[par] @ M) if par else M
    return W


def foot_low(pose, pelvis=(0, 0, 0)):
    """Lowest heel / toe point of both feet (game y)."""
    W = fk(pose, pelvis)
    ys = []
    for f in ("FootL", "FootR"):
        for pt in ((0, -0.05, -0.03), (0, -0.05, 0.13)):
            ys.append((W[f] @ np.array([*pt, 1.0]))[1])
    return min(ys)


def mirror(pose):
    """Left <-> right (sagittal mirror): swap sides, negate y and z."""
    out = {}
    for k, (x, y, z) in pose.items():
        k2 = k[:-1] + ("R" if k.endswith("L") else "L") if k[-1] in "LR" and k not in ("Pelvis",) else k
        out[k2] = (x, -y, -z)
    return out


def blend(a, b, t=0.5):
    keys = set(a) | set(b)
    return {k: tuple((1 - t) * np.array(a.get(k, (0, 0, 0))) + t * np.array(b.get(k, (0, 0, 0)))) for k in keys}


def add(a, b):
    keys = set(a) | set(b)
    return {k: tuple(np.array(a.get(k, (0, 0, 0))) + np.array(b.get(k, (0, 0, 0)))) for k in keys}


def flat_feet(pose):
    """Feet parallel to the floor (sole flat): foot x = -(thigh + shin)."""
    p = dict(pose)
    for s in "LR":
        th = p.get("Thigh" + s, (0, 0, 0)); sh = p.get("Shin" + s, (0, 0, 0)); ft = p.get("Foot" + s, (0, 0, 0))
        p["Foot" + s] = (-(th[0] + sh[0]), ft[1], -th[2])
    return p


# ---------------------------------------------------------------- gaits (one cycle, left heel strike at t = 0)
def gait(keys4, lean=0.0, arms=None, extra=None):
    """keys4 = first half (left leads): [(t, pose, opts)] for contact/down/passing/up in 0..0.5; mirrored for 0.5..1."""
    out = []
    for t, pose, opts in keys4:
        p = add(pose, {"Chest": (lean, 0, 0)})
        if extra:
            p = add(p, extra)
        out.append((t, p, opts))
    for t, pose, opts in keys4:
        p = add(mirror(pose), {"Chest": (lean, 0, 0)})
        if extra:
            p = add(p, extra)
        o = dict(opts)
        if "pelvis" in o:
            x, y, z = o["pelvis"]; o["pelvis"] = (-x, y, z)
        out.append((t + 0.5, p, o))
    return out


WALK = gait([
    (0.0, {"Pelvis": (0, 5, 1), "Chest": (2, -7, -1), "Head": (-1, 3, 0),
           "ThighL": (-27, 0, -2), "ShinL": (4, 0, 0), "FootL": (-12, 0, 2),
           "ThighR": (16, 0, 2), "ShinR": (16, 0, 0), "FootR": (16, 0, -2),
           "UpperArmL": (17, 0, -6), "ForearmL": (-10, 0, 0), "HandL": (0, 0, 4),
           "UpperArmR": (-19, 0, 6), "ForearmR": (-26, 0, 0), "HandR": (0, 0, -4)}, {"ground": True}),
    (0.1, {"Pelvis": (0, 4, -3), "Chest": (3, -5, 2), "Head": (0, 2, -1),
           "ThighL": (-20, 0, -2), "ShinL": (17, 0, 0), "FootL": (3, 0, 2),
           "ThighR": (9, 0, 2), "ShinR": (38, 0, 0), "FootR": (14, 0, -2),
           "UpperArmL": (12, 0, -6), "ForearmL": (-12, 0, 0), "HandL": (0, 0, 4),
           "UpperArmR": (-13, 0, 6), "ForearmR": (-28, 0, 0), "HandR": (0, 0, -4)}, {"ground": True}),
    (0.25, {"Pelvis": (0, 0, -2), "Chest": (2, 0, 1.5), "Head": (-1, 0, -0.5),
            "ThighL": (-1, 0, -2), "ShinL": (5, 0, 0), "FootL": (-3, 0, 2),
            "ThighR": (-14, 0, 2), "ShinR": (58, 0, 0), "FootR": (6, 0, -2),
            "UpperArmL": (1, 0, -6), "ForearmL": (-15, 0, 0), "HandL": (0, 0, 4),
            "UpperArmR": (-1, 0, 6), "ForearmR": (-20, 0, 0), "HandR": (0, 0, -4)}, {"ground": True}),
    (0.375, {"Pelvis": (0, -3, 0), "Chest": (2, 4, 0), "Head": (-1, -2, 0),
             "ThighL": (9, 0, -2), "ShinL": (8, 0, 0), "FootL": (7, 0, 2),
             "ThighR": (-25, 0, 2), "ShinR": (24, 0, 0), "FootR": (-8, 0, -2),
             "UpperArmL": (-10, 0, -6), "ForearmL": (-20, 0, 0), "HandL": (0, 0, 4),
             "UpperArmR": (10, 0, 6), "ForearmR": (-12, 0, 0), "HandR": (0, 0, -4)}, {"ground": True}),
])

RUN = gait([
    (0.0, {"Pelvis": (0, 9, 1), "Chest": (0, -12, -1), "Head": (-6, 6, 0),
           "ThighL": (-30, 0, -2), "ShinL": (14, 0, 0), "FootL": (-6, 0, 2),
           "ThighR": (18, 0, 2), "ShinR": (60, 0, 0), "FootR": (24, 0, -2),
           "UpperArmL": (34, 0, -10), "ForearmL": (-62, 0, 0), "HandL": (0, 0, 6),
           "UpperArmR": (-40, 0, 10), "ForearmR": (-88, 0, 0), "HandR": (0, 0, -6)}, {"ground": True}),
    (0.1, {"Pelvis": (0, 6, -4), "Chest": (2, -8, 2), "Head": (-7, 4, 0),
           "ThighL": (-24, 0, -2), "ShinL": (38, 0, 0), "FootL": (8, 0, 2),
           "ThighR": (-2, 0, 2), "ShinR": (92, 0, 0), "FootR": (20, 0, -2),
           "UpperArmL": (22, 0, -10), "ForearmL": (-70, 0, 0), "HandL": (0, 0, 6),
           "UpperArmR": (-28, 0, 10), "ForearmR": (-90, 0, 0), "HandR": (0, 0, -6)}, {"ground": True}),
    (0.25, {"Pelvis": (0, 0, -2), "Chest": (0, 0, 1), "Head": (-6, 0, 0),
            "ThighL": (12, 0, -2), "ShinL": (16, 0, 0), "FootL": (22, 0, 2),
            "ThighR": (-42, 0, 2), "ShinR": (104, 0, 0), "FootR": (12, 0, -2),
            "UpperArmL": (-4, 0, -10), "ForearmL": (-82, 0, 0), "HandL": (0, 0, 6),
            "UpperArmR": (4, 0, 10), "ForearmR": (-76, 0, 0), "HandR": (0, 0, -6)}, {"ground": True}),
    (0.375, {"Pelvis": (0, -6, 0), "Chest": (-1, 8, 0), "Head": (-5, -4, 0),
             "ThighL": (26, 0, -2), "ShinL": (44, 0, 0), "FootL": (26, 0, 2),
             "ThighR": (-50, 0, 2), "ShinR": (64, 0, 0), "FootR": (-4, 0, -2),
             "UpperArmL": (-30, 0, -10), "ForearmL": (-90, 0, 0), "HandL": (0, 0, 6),
             "UpperArmR": (30, 0, 10), "ForearmR": (-62, 0, 0), "HandR": (0, 0, -6)}, {"pelvis": (0, 0.03, 0)}),
], lean=10)

SPRINT = gait([
    (0.0, {"Pelvis": (0, 11, 1), "Chest": (0, -14, -1), "Head": (-12, 7, 0),
           "ThighL": (-36, 0, -2), "ShinL": (18, 0, 0), "FootL": (2, 0, 2),
           "ThighR": (26, 0, 2), "ShinR": (74, 0, 0), "FootR": (30, 0, -2),
           "UpperArmL": (48, 0, -8), "ForearmL": (-60, 0, 0), "HandL": (0, 0, 6),
           "UpperArmR": (-62, 0, 8), "ForearmR": (-96, 0, 0), "HandR": (0, 0, -6)}, {"ground": True}),
    (0.1, {"Pelvis": (0, 7, -4), "Chest": (2, -9, 2), "Head": (-13, 4, 0),
           "ThighL": (-34, 0, -2), "ShinL": (44, 0, 0), "FootL": (10, 0, 2),
           "ThighR": (-8, 0, 2), "ShinR": (118, 0, 0), "FootR": (26, 0, -2),
           "UpperArmL": (30, 0, -8), "ForearmL": (-70, 0, 0), "HandL": (0, 0, 6),
           "UpperArmR": (-40, 0, 8), "ForearmR": (-96, 0, 0), "HandR": (0, 0, -6)}, {"ground": True}),
    (0.25, {"Pelvis": (0, 0, -2), "Chest": (0, 0, 1), "Head": (-12, 0, 0),
            "ThighL": (18, 0, -2), "ShinL": (22, 0, 0), "FootL": (30, 0, 2),
            "ThighR": (-62, 0, 2), "ShinR": (124, 0, 0), "FootR": (16, 0, -2),
            "UpperArmL": (-6, 0, -8), "ForearmL": (-90, 0, 0), "HandL": (0, 0, 6),
            "UpperArmR": (6, 0, 8), "ForearmR": (-80, 0, 0), "HandR": (0, 0, -6)}, {"ground": True}),
    (0.375, {"Pelvis": (0, -8, 0), "Chest": (-1, 10, 0), "Head": (-11, -5, 0),
             "ThighL": (34, 0, -2), "ShinL": (60, 0, 0), "FootL": (30, 0, 2),
             "ThighR": (-68, 0, 2), "ShinR": (78, 0, 0), "FootR": (0, 0, -2),
             "UpperArmL": (-46, 0, -8), "ForearmL": (-96, 0, 0), "HandL": (0, 0, 6),
             "UpperArmR": (44, 0, 8), "ForearmR": (-60, 0, 0), "HandR": (0, 0, -6)}, {"pelvis": (0, 0.03, 0)}),
], lean=20)

CROUCH_BASE = {"Pelvis": (0, 0, 0), "Chest": (26, 0, 0), "Head": (-18, 0, 0),
               "UpperArmL": (-22, 0, -10), "UpperArmR": (-22, 0, 10), "ForearmL": (-48, 0, 0), "ForearmR": (-48, 0, 0)}
CROUCH_WALK = gait([
    (0.0, add(CROUCH_BASE, {"Pelvis": (0, 4, 0), "Chest": (0, -5, 0), "ThighL": (-80, 0, -6), "ShinL": (86, 0, 0), "FootL": (-14, 0, 4),
                            "ThighR": (-28, 0, 6), "ShinR": (104, 0, 0), "FootR": (-20, 0, -4),
                            "UpperArmL": (8, 0, 0), "UpperArmR": (-10, 0, 0)}), {"ground": True}),
    (0.25, add(CROUCH_BASE, {"Pelvis": (0, 0, -2), "ThighL": (-56, 0, -6), "ShinL": (104, 0, 0), "FootL": (-46, 0, 4),
                             "ThighR": (-74, 0, 6), "ShinR": (130, 0, 0), "FootR": (-30, 0, -4)}), {"ground": True, "lift": 0.02}),
])

# ---------------------------------------------------------------- standing / idle
STAND = {"Pelvis": (0, 0, 0), "Chest": (1, 0, 0), "UpperArmL": (-2, 0, -5), "UpperArmR": (-2, 0, 5),
         "ForearmL": (-12, 0, 0), "ForearmR": (-14, 0, 0), "HandL": (0, 0, 4), "HandR": (0, 0, -4),
         "ThighL": (0, 0, -3), "ThighR": (0, 0, 3), "FootL": (0, -6, 3), "FootR": (0, 6, -3)}


def breath(pose, chest, arms=0.0):
    return add(pose, {"Chest": (chest, 0, 0), "Head": (-chest * 0.6, 0, 0), "UpperArmL": (0, 0, -arms), "UpperArmR": (0, 0, arms)})


IDLE = [(0.0, breath(STAND, 0.0), {"ground": True}), (0.45, breath(STAND, 1.6, 1.2), {"ground": True})]

SHIFT_L = {"Pelvis": (0, 3, -4), "Chest": (1, -2, 4), "Head": (-1, 4, -2), "ThighL": (-1, 0, 1), "ThighR": (3, 0, 7), "ShinR": (12, 0, 0),
           "FootR": (-6, 10, -4), "FootL": (0, -6, 0), "UpperArmL": (-2, 0, -4), "UpperArmR": (2, 0, 8),
           "ForearmL": (-14, 0, 0), "ForearmR": (-18, 0, 0), "HandL": (0, 0, 4), "HandR": (0, 0, -6)}
IDLE_SHIFT = [(0.0, STAND, {"ground": True}), (0.18, SHIFT_L, {"ground": True, "pelvis_x": -0.035}),
              (0.42, breath(SHIFT_L, 1.4), {"ground": True, "pelvis_x": -0.035}), (0.6, STAND, {"ground": True}),
              (0.75, mirror(SHIFT_L), {"ground": True, "pelvis_x": 0.035}), (0.9, breath(mirror(SHIFT_L), 1.2), {"ground": True, "pelvis_x": 0.035})]

IDLE_LOOK = [(0.0, STAND, {"ground": True}), (0.2, add(STAND, {"Head": (-3, 38, 2), "Chest": (0, 10, 0), "Pelvis": (0, 3, 0)}), {"ground": True}),
             (0.4, add(STAND, {"Head": (-4, 42, 0), "Chest": (0, 12, 0), "Pelvis": (0, 3, 0)}), {"ground": True}),
             (0.55, add(STAND, {"Head": (4, -6, 0)}), {"ground": True}),
             (0.72, add(STAND, {"Head": (-2, -40, -2), "Chest": (0, -11, 0), "Pelvis": (0, -3, 0)}), {"ground": True}),
             (0.88, add(STAND, {"Head": (-1, -36, 0), "Chest": (0, -10, 0), "Pelvis": (0, -3, 0)}), {"ground": True})]

CROUCH_IDLE_P = add(CROUCH_BASE, {"ThighL": (-74, 0, -10), "ShinL": (124, 0, 0), "ThighR": (-60, 0, 10), "ShinR": (118, 0, 0)})
CROUCH_IDLE = [(0.0, flat_feet(CROUCH_IDLE_P), {"ground": True}), (0.5, flat_feet(add(CROUCH_IDLE_P, {"Chest": (2, 0, 0)})), {"ground": True})]

# ---------------------------------------------------------------- air
JUMP = [
        (0.0, add(STAND, {"ThighL": (-6, 0, -3), "ShinL": (8, 0, 0), "ThighR": (4, 0, 3), "ShinR": (14, 0, 0), "FootL": (34, 0, 0), "FootR": (38, 0, 0),
                          "Chest": (2, 0, 0), "UpperArmL": (-120, 0, -16), "UpperArmR": (-110, 0, 16), "ForearmL": (-20, 0, 0), "ForearmR": (-20, 0, 0)}),
         {"pelvis": (0, 0.04, 0)}),
        (1.0, add(STAND, {"ThighL": (-48, 0, -4), "ShinL": (80, 0, 0), "ThighR": (-26, 0, 4), "ShinR": (60, 0, 0), "FootL": (8, 0, 0), "FootR": (14, 0, 0),
                          "Chest": (8, 0, 0), "UpperArmL": (-70, 0, -30), "UpperArmR": (-60, 0, 30), "ForearmL": (-30, 0, 0), "ForearmR": (-30, 0, 0)}),
         {"pelvis": (0, 0.02, 0)})]
FALL = [(0.0, add(STAND, {"ThighL": (-40, 0, -6), "ShinL": (60, 0, 0), "ThighR": (-14, 0, 6), "ShinR": (42, 0, 0), "FootL": (10, 0, 0), "FootR": (16, 0, 0),
                          "Chest": (4, 0, 0), "Head": (6, 0, 0), "UpperArmL": (-60, 0, -48), "UpperArmR": (-52, 0, 44), "ForearmL": (-26, 0, 0), "ForearmR": (-30, 0, 0)}),
         {"pelvis": (0, 0, 0)}),
        (0.5, add(STAND, {"ThighL": (-34, 0, -6), "ShinL": (54, 0, 0), "ThighR": (-20, 0, 6), "ShinR": (50, 0, 0), "FootL": (12, 0, 0), "FootR": (14, 0, 0),
                          "Chest": (6, 0, 0), "Head": (4, 0, 0), "UpperArmL": (-72, 0, -40), "UpperArmR": (-64, 0, 50), "ForearmL": (-34, 0, 0), "ForearmR": (-22, 0, 0)}),
         {"pelvis": (0, 0, 0)})]
LAND_P = flat_feet(add(STAND, {"ThighL": (-62, 0, -6), "ShinL": (104, 0, 0), "ThighR": (-58, 0, 6), "ShinR": (100, 0, 0), "Chest": (30, 0, 0), "Head": (-20, 0, 0),
                               "UpperArmL": (-34, 0, -22), "UpperArmR": (-30, 0, 22), "ForearmL": (-30, 0, 0), "ForearmR": (-30, 0, 0)}))
LAND = [(0.0, LAND_P, {"ground": True}), (0.3, add(LAND_P, {"Chest": (6, 0, 0)}), {"ground": True}), (1.0, breath(STAND, 0), {"ground": True})]

# ---------------------------------------------------------------- parkour (progress driven)
VAULT = [(0.0, flat_feet(add(STAND, {"ThighL": (-30, 0, -3), "ShinL": (50, 0, 0), "ThighR": (-20, 0, 3), "ShinR": (40, 0, 0), "Chest": (30, 0, 0),
                                     "UpperArmL": (-70, 0, -6), "UpperArmR": (-70, 0, 6), "ForearmL": (-10, 0, 0), "ForearmR": (-10, 0, 0)})), {"pelvis": (0, -0.12, 0)}),
         (0.35, add(STAND, {"Pelvis": (0, -30, 20), "ThighL": (-90, 0, -24), "ShinL": (100, 0, 0), "ThighR": (-70, 0, -6), "ShinR": (80, 0, 0),
                            "Chest": (24, 20, -14), "Head": (-14, 10, 6), "UpperArmL": (-40, 0, -4), "UpperArmR": (-34, 0, 4), "ForearmL": (-4, 0, 0), "ForearmR": (-6, 0, 0)}),
          {"pelvis": (0, 0.0, 0)}),
         (0.7, add(STAND, {"Pelvis": (0, -12, 6), "ThighL": (-50, 0, -10), "ShinL": (40, 0, 0), "ThighR": (-30, 0, 6), "ShinR": (30, 0, 0),
                           "Chest": (10, 6, -4), "UpperArmL": (-30, 0, -40), "UpperArmR": (-60, 0, 20), "ForearmL": (-20, 0, 0), "ForearmR": (-20, 0, 0)}),
          {"pelvis": (0, -0.05, 0)}),
         (1.0, flat_feet(add(STAND, {"ThighL": (-40, 0, -4), "ShinL": (70, 0, 0), "ThighR": (-34, 0, 4), "ShinR": (66, 0, 0), "Chest": (22, 0, 0),
                                     "UpperArmL": (-30, 0, -20), "UpperArmR": (-30, 0, 20), "ForearmL": (-30, 0, 0), "ForearmR": (-30, 0, 0)})), {"ground": True})]
MANTLE = [(0.0, add(STAND, {"UpperArmL": (-165, 0, -12), "UpperArmR": (-165, 0, 12), "ForearmL": (-20, 0, 0), "ForearmR": (-20, 0, 0),
                            "Chest": (-4, 0, 0), "Head": (-20, 0, 0), "ThighL": (-10, 0, -3), "ShinL": (20, 0, 0), "FootL": (30, 0, 0), "FootR": (30, 0, 0)}),
           {"pelvis": (0, 0.02, 0)}),
          (0.35, add(STAND, {"UpperArmL": (-100, 0, -30), "UpperArmR": (-100, 0, 30), "ForearmL": (-120, 0, 0), "ForearmR": (-120, 0, 0),
                             "Chest": (20, 0, 0), "Head": (-14, 0, 0), "ThighL": (-50, 0, -3), "ShinL": (90, 0, 0), "ThighR": (-10, 0, 3), "ShinR": (40, 0, 0),
                             "FootL": (20, 0, 0), "FootR": (30, 0, 0)}), {"pelvis": (0, 0, 0)}),
          (0.65, add(STAND, {"UpperArmL": (-20, 0, -30), "UpperArmR": (-20, 0, 30), "ForearmL": (-20, 0, 0), "ForearmR": (-20, 0, 0),
                             "Chest": (48, 0, 0), "Head": (-30, 0, 0), "ThighL": (-110, 0, -8), "ShinL": (130, 0, 0), "ThighR": (-30, 0, 3), "ShinR": (70, 0, 0)}),
           {"pelvis": (0, -0.1, 0)}),
          (1.0, flat_feet(add(STAND, {"ThighL": (-30, 0, -4), "ShinL": (50, 0, 0), "ThighR": (-10, 0, 4), "ShinR": (24, 0, 0), "Chest": (16, 0, 0),
                                      "UpperArmL": (-10, 0, -10), "UpperArmR": (-10, 0, 10)})), {"ground": True})]

# ---------------------------------------------------------------- seated
DRIVE = {"ThighL": (-84, 0, -4), "ThighR": (-84, 0, 4), "ShinL": (78, 0, 0), "ShinR": (72, 0, 0), "FootL": (4, 0, 0), "FootR": (10, 0, 0),
         "Chest": (-6, 0, 0), "Head": (6, 0, 0), "UpperArmL": (-72, 0, -6), "UpperArmR": (-72, 0, 6), "ForearmL": (-24, 0, 0),
         "ForearmR": (-24, 0, 0), "HandL": (0, 0, 10), "HandR": (0, 0, -10)}
SIT_DRIVE = [(0.0, DRIVE, {"pelvis": (0, 0, 0)}), (0.3, add(DRIVE, {"Head": (0, 6, 0), "Chest": (0.8, 0, 0)}), {"pelvis": (0, 0.003, 0)}),
             (0.55, add(DRIVE, {"Head": (2, -3, 0)}), {"pelvis": (0, 0, 0)}),
             (0.8, add(DRIVE, {"Head": (-1, -8, 0), "Chest": (0.8, 0, 0), "ShinL": (-4, 0, 0)}), {"pelvis": (0, 0.003, 0)})]
LOUNGE = {"ThighL": (-84, 0, -6), "ThighR": (-82, 0, 8), "ShinL": (80, 0, 0), "ShinR": (70, 0, 0), "FootL": (4, -6, 0), "FootR": (12, 8, 0),
          "Chest": (8, 0, 0), "Head": (-4, 0, 0), "UpperArmL": (-28, 0, -10), "UpperArmR": (-30, 0, 10), "ForearmL": (-56, 0, 0),
          "ForearmR": (-60, 0, 0), "HandL": (0, 0, 12), "HandR": (0, 0, -12)}
SIT = [(0.0, LOUNGE, {"pelvis": (0, 0, 0)}), (0.4, add(LOUNGE, {"Chest": (1.5, 0, 0), "Head": (-1, 8, 0)}), {"pelvis": (0, 0.003, 0)}),
       (0.7, add(LOUNGE, {"Chest": (0.5, 0, 0), "Head": (2, -6, 0), "ForearmR": (-6, 0, 0)}), {"pelvis": (0, 0, 0)})]
RIDE_P = {"ThighL": (-38, 0, -24), "ThighR": (-38, 0, 24), "ShinL": (52, 0, 0), "ShinR": (52, 0, 0), "FootL": (-10, 0, 0), "FootR": (-10, 0, 0),
          "Chest": (4, 0, 0), "UpperArmL": (-40, 0, -6), "UpperArmR": (-40, 0, 6), "ForearmL": (-50, 0, 0), "ForearmR": (-50, 0, 0)}
RIDE = [(0.0, RIDE_P, {"pelvis": (0, 0, 0)}), (0.25, add(RIDE_P, {"Chest": (3, 0, 0), "Head": (-2, 0, 0), "ForearmL": (-6, 0, 0), "ForearmR": (-6, 0, 0)}), {"pelvis": (0, 0.02, 0)}),
        (0.5, add(RIDE_P, {"Chest": (-1, 0, 0), "Head": (1, 0, 0)}), {"pelvis": (0, 0, 0)}),
        (0.75, add(RIDE_P, {"Chest": (3, 0, 0), "Head": (-2, 0, 0), "ForearmL": (-6, 0, 0), "ForearmR": (-6, 0, 0)}), {"pelvis": (0, 0.02, 0)})]
PEDAL_B = add(RIDE_P, {"ThighL": (-20, 0, 16), "ThighR": (-20, 0, -16), "Chest": (12, 0, 0), "ShinL": (20, 0, 0), "ShinR": (20, 0, 0)})
PEDAL = [(0.0, add(PEDAL_B, {"ThighL": (22, 0, 0), "ShinL": (-20, 0, 0), "ThighR": (-22, 0, 0), "ShinR": (20, 0, 0), "FootL": (10, 0, 0), "FootR": (-6, 0, 0)}), {"pelvis": (0, 0, 0)}),
         (0.25, add(PEDAL_B, {"ShinL": (-6, 0, 0), "ShinR": (6, 0, 0)}), {"pelvis": (0, 0, 0)}),
         (0.5, add(PEDAL_B, {"ThighL": (-22, 0, 0), "ShinL": (20, 0, 0), "ThighR": (22, 0, 0), "ShinR": (-20, 0, 0), "FootL": (-6, 0, 0), "FootR": (10, 0, 0)}), {"pelvis": (0, 0, 0)}),
         (0.75, add(PEDAL_B, {"ShinL": (6, 0, 0), "ShinR": (-6, 0, 0)}), {"pelvis": (0, 0, 0)})]
# lying on the back (sleep): the pelvis turns up, the body lies along -z from the root
LIE_P = {"Pelvis": (-88, 0, 0), "Chest": (-4, 0, 0), "Head": (-12, 0, 6), "ThighL": (-4, 0, -6), "ThighR": (-10, 0, 4), "ShinL": (8, 0, 0), "ShinR": (18, 0, 0),
         "FootL": (30, -10, 0), "FootR": (34, 12, 0), "UpperArmL": (4, 0, -14), "UpperArmR": (-10, 0, 14), "ForearmL": (-20, 0, 0), "ForearmR": (-34, 0, 0),
         "HandL": (0, 0, 10), "HandR": (0, 0, -10)}
LIE = [(0.0, LIE_P, {"pelvis": (0, -0.8, 0)}), (0.45, add(LIE_P, {"Chest": (-2, 0, 0), "Head": (2, 0, 0)}), {"pelvis": (0, -0.795, 0)})]

# ---------------------------------------------------------------- work (upper body unless noted)
WRENCH_P = {"Chest": (34, 0, 0), "Head": (-10, 0, 0), "UpperArmR": (-34, 0, 6), "ForearmR": (-40, 0, 0), "UpperArmL": (-30, 0, 10), "ForearmL": (-44, 0, 0),
            "HandL": (0, 0, 10), "HandR": (0, 0, -10)}
WRENCH = [(0.0, add(WRENCH_P, {"HandR": (0, 0, 0)}), {}), (0.3, add(WRENCH_P, {"UpperArmR": (6, 8, 0), "ForearmR": (-6, 0, 0), "HandR": (0, 0, 40), "Chest": (2, 3, 0)}), {}),
          (0.45, add(WRENCH_P, {"UpperArmR": (6, 8, 0), "ForearmR": (-6, 0, 0), "HandR": (0, 0, 44), "Chest": (2, 3, 0)}), {}),
          (0.7, add(WRENCH_P, {"HandR": (0, 0, -6)}), {})]
POUR_P = {"Chest": (14, -6, 0), "Head": (6, 0, 0), "UpperArmR": (-46, 0, 8), "ForearmR": (-36, 0, 0), "HandR": (0, 0, 64),
          "UpperArmL": (-50, 0, 24), "ForearmL": (-60, 0, 0), "HandL": (0, 0, 20)}
POUR = [(0.0, POUR_P, {}), (0.5, add(POUR_P, {"HandR": (0, 0, 10), "UpperArmR": (-4, 0, 0), "Chest": (2, 0, 0)}), {})]
WELD_P = flat_feet({"ThighL": (-90, 0, -10), "ShinL": (100, 0, 0), "ThighR": (-10, 0, 8), "ShinR": (118, 0, 0),
                    "Chest": (30, 0, 0), "Head": (12, 0, 0), "UpperArmR": (-40, 0, 6), "ForearmR": (-50, 0, 0), "HandR": (0, 0, -20),
                    "UpperArmL": (-46, 0, 18), "ForearmL": (-48, 0, 0)})
WELD_P["FootR"] = (60, 0, -8)   # kneeling on the right knee: toes tucked
WELD = [(0.0, WELD_P, {"ground": True}), (0.25, add(WELD_P, {"UpperArmR": (1, 4, 0), "HandR": (0, 0, 3)}), {"ground": True}),
        (0.5, add(WELD_P, {"UpperArmR": (-1, 8, 0), "Chest": (1, 2, 0)}), {"ground": True}),
        (0.75, add(WELD_P, {"UpperArmR": (1, 4, 0), "HandR": (0, 0, -3)}), {"ground": True})]
DIG = [(0.0, flat_feet(add(STAND, {"Chest": (20, -20, 0), "Head": (14, 10, 0), "UpperArmR": (-60, 0, 10), "ForearmR": (-20, 0, 0), "UpperArmL": (-40, 0, 30),
                                   "ForearmL": (-60, 0, 0), "ThighL": (-30, 0, -4), "ShinL": (36, 0, 0), "ThighR": (6, 0, 6), "ShinR": (10, 0, 0)})), {"ground": True}),
       (0.35, flat_feet(add(STAND, {"Chest": (42, -14, 0), "Head": (10, 6, 0), "UpperArmR": (-20, 0, 10), "ForearmR": (-10, 0, 0), "UpperArmL": (-24, 0, 26),
                                    "ForearmL": (-40, 0, 0), "ThighL": (-50, 0, -4), "ShinL": (70, 0, 0), "ThighR": (-14, 0, 6), "ShinR": (40, 0, 0)})), {"ground": True}),
       (0.6, flat_feet(add(STAND, {"Chest": (28, -10, 0), "Head": (6, 0, 0), "UpperArmR": (-40, 0, 10), "ForearmR": (-40, 0, 0), "UpperArmL": (-50, 0, 26),
                                   "ForearmL": (-70, 0, 0), "ThighL": (-30, 0, -4), "ShinL": (44, 0, 0), "ThighR": (-4, 0, 6), "ShinR": (24, 0, 0)})), {"ground": True}),
       (0.85, flat_feet(add(STAND, {"Chest": (8, 30, 0), "Head": (0, -10, 0), "UpperArmR": (-80, 30, 10), "ForearmR": (-10, 0, 0), "UpperArmL": (-70, 20, 10),
                                    "ForearmL": (-30, 0, 0), "ThighL": (-10, 0, -4), "ShinL": (14, 0, 0)})), {"ground": True}),
       (1.0, flat_feet(add(STAND, {"Chest": (14, 0, 0), "UpperArmR": (-50, 0, 10), "ForearmR": (-30, 0, 0), "UpperArmL": (-40, 0, 24), "ForearmL": (-50, 0, 0)})), {"ground": True})]
HAMMER = [(0.0, {"Chest": (8, 0, 0), "UpperArmR": (-60, 0, 8), "ForearmR": (-40, 0, 0), "UpperArmL": (-30, 0, 16), "ForearmL": (-50, 0, 0)}, {}),
          (0.4, {"Chest": (-8, -8, 0), "Head": (6, 0, 0), "UpperArmR": (-170, -10, 10), "ForearmR": (-70, 0, 0), "HandR": (0, 0, -20),
                 "UpperArmL": (-40, 0, 20), "ForearmL": (-50, 0, 0)}, {}),
          (0.6, {"Chest": (22, 6, 0), "Head": (10, 0, 0), "UpperArmR": (-58, 0, 8), "ForearmR": (-10, 0, 0), "HandR": (0, 0, 10),
                 "UpperArmL": (-46, 0, 22), "ForearmL": (-56, 0, 0)}, {}),
          (1.0, {"Chest": (10, 0, 0), "UpperArmR": (-60, 0, 8), "ForearmR": (-40, 0, 0), "UpperArmL": (-30, 0, 16), "ForearmL": (-50, 0, 0)}, {})]
CARRY_P = {"Chest": (-4, 0, 0), "UpperArmL": (-58, 0, -10), "UpperArmR": (-58, 0, 10), "ForearmL": (-34, 0, 0), "ForearmR": (-34, 0, 0),
           "HandL": (0, 0, -30), "HandR": (0, 0, 30)}
CARRY = [(0.0, CARRY_P, {}), (0.5, add(CARRY_P, {"UpperArmL": (2, 0, 0), "UpperArmR": (2, 0, 0), "Chest": (-1, 0, 0)}), {})]
AIM_P = {"Chest": (4, -10, 0), "Head": (6, 8, 0), "UpperArmR": (-80, -6, -4), "ForearmR": (-10, 0, 0), "UpperArmL": (-78, 0, 22),
         "ForearmL": (-36, 0, 0), "HandL": (0, 0, 10), "HandR": (0, 0, -6)}
AIM = [(0.0, AIM_P, {}), (0.5, add(AIM_P, {"Chest": (0.6, 0, 0), "UpperArmL": (0.6, 0, 0)}), {})]
FIRE = [(0.0, AIM_P, {}), (0.15, add(AIM_P, {"UpperArmR": (-14, 0, 0), "UpperArmL": (-10, 0, 0), "ForearmR": (-8, 0, 0), "Chest": (-5, 0, 0), "Head": (-3, 0, 0)}), {}),
        (1.0, AIM_P, {})]
SWING_OH = [(0.0, {"Chest": (6, 0, 0), "UpperArmR": (-40, 0, 8), "ForearmR": (-50, 0, 0), "UpperArmL": (-20, 0, -8), "ForearmL": (-30, 0, 0)}, {}),
            (0.45, {"Chest": (-12, -14, 0), "Head": (8, 6, 0), "UpperArmR": (-175, -20, 14), "ForearmR": (-80, 0, 0), "HandR": (0, 0, -20),
                    "UpperArmL": (-40, 0, -30), "ForearmL": (-30, 0, 0)}, {}),
            (0.64, {"Chest": (30, 10, 0), "Head": (12, 0, 0), "UpperArmR": (-50, 10, 6), "ForearmR": (-6, 0, 0), "HandR": (0, 0, 14),
                    "UpperArmL": (-10, 0, -24), "ForearmL": (-20, 0, 0)}, {}),
            (1.0, {"Chest": (6, 0, 0), "UpperArmR": (-40, 0, 8), "ForearmR": (-50, 0, 0), "UpperArmL": (-20, 0, -8), "ForearmL": (-30, 0, 0)}, {})]
SWING_SL = [(0.0, {"Chest": (4, 0, 0), "UpperArmR": (-40, 0, 8), "ForearmR": (-50, 0, 0), "UpperArmL": (-20, 0, -8), "ForearmL": (-30, 0, 0)}, {}),
            (0.4, {"Chest": (2, 34, 0), "Head": (0, -20, 0), "UpperArmR": (-90, 60, 50), "ForearmR": (-60, 0, 0), "HandR": (0, 0, -30),
                   "UpperArmL": (-30, 0, -20), "ForearmL": (-40, 0, 0)}, {}),
            (0.55, {"Chest": (8, -30, 0), "Head": (0, 18, 0), "UpperArmR": (-86, -40, 10), "ForearmR": (-10, 0, 0), "HandR": (0, 0, 20),
                    "UpperArmL": (-20, 0, -30), "ForearmL": (-30, 0, 0)}, {}),
            (1.0, {"Chest": (4, 0, 0), "UpperArmR": (-40, 0, 8), "ForearmR": (-50, 0, 0), "UpperArmL": (-20, 0, -8), "ForearmL": (-30, 0, 0)}, {})]
THRUST = [(0.0, {"Chest": (4, 0, 0), "UpperArmR": (-30, 0, 6), "ForearmR": (-90, 0, 0), "UpperArmL": (-24, 0, -8), "ForearmL": (-30, 0, 0)}, {}),
          (0.3, {"Chest": (-4, 16, 0), "Head": (0, -10, 0), "UpperArmR": (-20, 20, 10), "ForearmR": (-120, 0, 0), "UpperArmL": (-40, 0, -14), "ForearmL": (-40, 0, 0)}, {}),
          (0.5, {"Chest": (16, -14, 0), "Head": (4, 10, 0), "UpperArmR": (-88, 0, 4), "ForearmR": (-4, 0, 0), "UpperArmL": (-10, 0, -30), "ForearmL": (-20, 0, 0)}, {}),
          (1.0, {"Chest": (4, 0, 0), "UpperArmR": (-30, 0, 6), "ForearmR": (-90, 0, 0), "UpperArmL": (-24, 0, -8), "ForearmL": (-30, 0, 0)}, {})]
TALK_A = [(0.0, {"Chest": (1, 0, 0), "UpperArmR": (-20, 0, 10), "ForearmR": (-70, 0, 0), "HandR": (0, 0, -30), "UpperArmL": (-2, 0, -6), "ForearmL": (-14, 0, 0)}, {}),
          (0.2, {"Chest": (2, 4, 0), "Head": (-3, -4, 2), "UpperArmR": (-28, 10, 20), "ForearmR": (-80, 0, 0), "HandR": (0, 0, -50),
                 "UpperArmL": (-2, 0, -6), "ForearmL": (-14, 0, 0)}, {}),
          (0.45, {"Chest": (0, -3, 0), "Head": (2, 4, 0), "UpperArmR": (-16, 0, 8), "ForearmR": (-60, 0, 0), "HandR": (0, 0, -20),
                  "UpperArmL": (-24, 0, -16), "ForearmL": (-66, 0, 0), "HandL": (0, 0, 40)}, {}),
          (0.7, {"Chest": (1, 2, 0), "Head": (-2, 0, -2), "UpperArmR": (-6, 0, 6), "ForearmR": (-20, 0, 0),
                 "UpperArmL": (-20, 0, -14), "ForearmL": (-60, 0, 0), "HandL": (0, 0, 30)}, {})]
TALK_B = [(0.0, {"Chest": (0, 0, 0), "UpperArmR": (-4, 0, 6), "ForearmR": (-16, 0, 0), "UpperArmL": (-4, 0, -6), "ForearmL": (-16, 0, 0)}, {}),
          (0.3, {"Chest": (3, 0, 0), "Head": (4, 0, 0), "UpperArmR": (-30, 0, 24), "ForearmR": (-60, 0, 0), "HandR": (0, 0, -60),
                 "UpperArmL": (-30, 0, -24), "ForearmL": (-60, 0, 0), "HandL": (0, 0, 60)}, {}),
          (0.55, {"Chest": (-2, 0, 0), "Head": (-6, 0, 0), "UpperArmR": (-34, 0, 30), "ForearmR": (-50, 0, 0), "HandR": (0, 0, -70),
                  "UpperArmL": (-34, 0, -30), "ForearmL": (-50, 0, 0), "HandL": (0, 0, 70)}, {}),
          (0.8, {"Chest": (1, 0, 0), "UpperArmR": (-6, 0, 8), "ForearmR": (-20, 0, 0), "UpperArmL": (-6, 0, -8), "ForearmL": (-20, 0, 0)}, {})]
WAVE = [(0.0, {"UpperArmR": (-10, 0, 10), "ForearmR": (-20, 0, 0)}, {}),
        (0.25, {"Chest": (-2, 0, 0), "UpperArmR": (-150, 0, 30), "ForearmR": (-30, 0, 0), "HandR": (0, 0, -20)}, {}),
        (0.45, {"Chest": (-2, 0, 0), "UpperArmR": (-150, 0, 40), "ForearmR": (-20, 0, 30), "HandR": (0, 0, 20)}, {}),
        (0.65, {"Chest": (-2, 0, 0), "UpperArmR": (-150, 0, 30), "ForearmR": (-30, 0, -20), "HandR": (0, 0, -20)}, {}),
        (1.0, {"UpperArmR": (-10, 0, 10), "ForearmR": (-20, 0, 0)}, {})]
# ---------------------------------------------------------------- death: knees buckle, fall forward onto the front
DEATH = [(0.0, STAND, {"ground": True}),
         (0.2, flat_feet(add(STAND, {"ThighL": (-30, 0, -4), "ShinL": (60, 0, 0), "ThighR": (-24, 0, 4), "ShinR": (54, 0, 0), "Chest": (16, 0, 6), "Head": (20, 0, 0),
                                     "UpperArmL": (-10, 0, -20), "UpperArmR": (-14, 0, 24)})), {"ground": True}),
         (0.45, add(STAND, {"Pelvis": (24, 0, 4), "ThighL": (-70, 0, -6), "ShinL": (110, 0, 0), "ThighR": (-60, 0, 8), "ShinR": (100, 0, 0), "FootL": (40, 0, 0),
                            "FootR": (40, 0, 0), "Chest": (30, 0, 8), "Head": (20, 10, 0), "UpperArmL": (-50, 0, -30), "UpperArmR": (-60, 0, 30),
                            "ForearmL": (-30, 0, 0), "ForearmR": (-30, 0, 0)}), {"pelvis": (0, -0.3, 0.06)}),
         (0.75, add(STAND, {"Pelvis": (82, 0, 4), "ThighL": (-12, 0, -8), "ShinL": (30, 0, 0), "ThighR": (-4, 0, 6), "ShinR": (20, 0, 0), "FootL": (60, 0, 0),
                            "FootR": (60, 0, 0), "Chest": (4, 0, 6), "Head": (-30, 40, 0), "UpperArmL": (-140, 0, -30), "UpperArmR": (-120, 0, 40),
                            "ForearmL": (-20, 0, 0), "ForearmR": (-30, 0, 0)}), {"pelvis": (0, -0.78, 0.6)}),
         (1.0, add(STAND, {"Pelvis": (90, 0, 4), "ThighL": (-8, 0, -10), "ShinL": (24, 0, 0), "ThighR": (0, 0, 6), "ShinR": (16, 0, 0), "FootL": (12, 0, 0),
                           "FootR": (8, 0, 0), "Chest": (2, 0, 6), "Head": (-34, 60, 0), "UpperArmL": (-150, 0, -40), "UpperArmR": (-110, 0, 46),
                           "ForearmL": (-20, 0, 0), "ForearmR": (-40, 0, 0)}), {"pelvis": (0, -0.82, 0.78)})]

CLIPS = {
    # name: length (s), loop, mask, keys, meta
    "idle": dict(length=4.0, loop=True, mask="full", keys=IDLE),
    "idle_shift": dict(length=7.0, loop=True, mask="full", keys=IDLE_SHIFT),
    "idle_look": dict(length=6.0, loop=True, mask="full", keys=IDLE_LOOK),
    "walk": dict(length=1.0, loop=True, mask="full", keys=WALK, speed=1.4, gait=True),
    "run": dict(length=0.72, loop=True, mask="full", keys=RUN, speed=3.9, gait=True),
    "sprint": dict(length=0.6, loop=True, mask="full", keys=SPRINT, speed=5.8, gait=True),
    "crouch_idle": dict(length=3.0, loop=True, mask="full", keys=CROUCH_IDLE),
    "crouch_walk": dict(length=1.2, loop=True, mask="full", keys=CROUCH_WALK, speed=1.0, gait=True),
    "jump": dict(length=0.6, loop=False, mask="full", keys=JUMP),
    "fall": dict(length=1.2, loop=True, mask="full", keys=FALL),
    "land": dict(length=0.45, loop=False, mask="full", keys=LAND),
    "vault": dict(length=0.7, loop=False, mask="full", keys=VAULT),
    "mantle": dict(length=1.0, loop=False, mask="full", keys=MANTLE),
    "sit_drive": dict(length=6.0, loop=True, mask="full", keys=SIT_DRIVE),
    "sit": dict(length=6.0, loop=True, mask="full", keys=SIT),
    "ride": dict(length=0.8, loop=True, mask="full", keys=RIDE),
    "pedal": dict(length=1.0, loop=True, mask="lower", keys=PEDAL, gait=True),
    "lie": dict(length=5.0, loop=True, mask="full", keys=LIE),
    "wrench": dict(length=1.1, loop=True, mask="upper", keys=WRENCH),
    "pour": dict(length=2.5, loop=True, mask="upper", keys=POUR),
    "weld": dict(length=1.6, loop=True, mask="full", keys=WELD),
    "dig": dict(length=1.1, loop=False, mask="full", keys=DIG, hit=0.35),
    "hammer": dict(length=0.7, loop=False, mask="upper", keys=HAMMER, hit=0.6),
    "carry": dict(length=1.0, loop=True, mask="upper", keys=CARRY),
    "aim": dict(length=3.0, loop=True, mask="upper", keys=AIM),
    "fire": dict(length=0.3, loop=False, mask="upper", keys=FIRE, hit=0.0),
    "swing_overhead": dict(length=0.55, loop=False, mask="upper", keys=SWING_OH, hit=0.64),
    "swing_slash": dict(length=0.5, loop=False, mask="upper", keys=SWING_SL, hit=0.55),
    "thrust": dict(length=0.45, loop=False, mask="upper", keys=THRUST, hit=0.5),
    "talk_a": dict(length=3.2, loop=True, mask="upper", keys=TALK_A),
    "talk_b": dict(length=3.6, loop=True, mask="upper", keys=TALK_B),
    "wave": dict(length=1.4, loop=False, mask="upper", keys=WAVE),
    "death_fall": dict(length=1.3, loop=False, mask="full", keys=DEATH),
}


def resolve_key(pose, opts):
    """Full pose dict + pelvis offset for a key (grounding applied)."""
    full = {p: tuple(pose.get(p, (0, 0, 0))) for p in PARTS}
    if "pelvis" in opts:
        pel = np.array(opts["pelvis"], float)
    else:
        pel = np.array([opts.get("pelvis_x", 0.0), 0.0, 0.0])
    if opts.get("ground"):
        pel[1] = REST_FLOOR - foot_low(full, (pel[0], 0, pel[2])) + opts.get("lift", 0.0)
    return full, pel


if __name__ == "__main__":
    for name, c in CLIPS.items():
        ys = []
        for t, pose, opts in c["keys"]:
            full, pel = resolve_key(pose, opts)
            low = foot_low(full, pel)
            ys.append(f"{t:.2f}:{pel[1]:+.3f}/{low:+.3f}")
        print(f"{name:15s} {c['length']:.2f}s {c['mask']:5s} " + " ".join(ys))
