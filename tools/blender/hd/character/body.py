"""Body, head, hair and beard shapes as SDF primitives per HumanRig part (game space, part-local frames)."""
import numpy as np

from hdlib import Prim, rot_x, rot_y, rot_z

SKIN_TONES = [("e9c09a", "f5d2ae", "c99a72"), ("c98e62", "dba478", "a8734c"),
              ("8e5a38", "a56c46", "6e4228"), ("5a3620", "6e442a", "422616")]       # HumanDesign.SkinTones
HAIR_COLORS = ["1c1512", "4a2c18", "8a5a2a", "c8a060", "b0b0b0", "8a2a1a"]          # HumanDesign.HairColors
HAIR_STYLES = ["Bald", "Buzz", "Short", "Mohawk", "Ponytail", "Long"]                 # HairStyle enum

FACES = {
    # name: (sex, overrides)
    "rugged": ("m", dict(jaw=(0.058, 0.032, 0.046), chin=0.025, nose=(0.010, 0.0145), nose_tip=0.013, brow=0.0135,
                         cheek=(0.022, 0.016, 0.02), lips=(0.0072, 0.0082), nose_bend=0.004)),
    "lean": ("m", dict(jaw=(0.049, 0.03, 0.046), chin=0.021, nose=(0.009, 0.0125), nose_tip=0.0115, brow=0.012,
                       cheek=(0.025, 0.017, 0.021), hollow=True, nose_len=0.006, lips=(0.0065, 0.0078))),
    "soft": ("f", dict(jaw=None, jawell=(0.054, 0.042, 0.05), chin=0.018, nose=(0.0075, 0.0105), nose_tip=0.0105,
                       brow=0.008, cheek=(0.024, 0.018, 0.022), lips=(0.0088, 0.0098), mid=(0.068, 0.056, 0.061))),
    "sharp": ("f", dict(jaw=None, jawell=(0.049, 0.038, 0.05), chin=0.0165, chin_z=0.075, nose=(0.0075, 0.011),
                        nose_tip=0.0098, brow=0.0085, cheek=(0.023, 0.016, 0.02), cheek_y=0.185, lips=(0.0078, 0.0092))),
}


def _m(p, side):
    """Mirror a right-side local point for the left limbs."""
    return (p[0] * side, p[1], p[2])


def head_prims(face="rugged", h=1.0):
    sex, o = FACES[face]
    s = h
    P = []
    def a(kind, k=0.02, op="add", rot=None, **kw):
        for key in ("c", "a", "b"):
            if key in kw:
                kw[key] = tuple(np.multiply(kw[key], s))
        for key in ("r", "ra", "rb"):
            if key in kw:
                kw[key] = kw[key] * s
        if "rad" in kw:
            kw["rad"] = tuple(np.multiply(kw["rad"], s))
        if "half" in kw:
            kw["half"] = tuple(np.multiply(kw["half"], s))
        P.append(Prim("Head", kind, k * s, op, rot, **kw))
    fem = sex == "f"
    a("cap", a=(0, -0.01, -0.006), b=(0, 0.125, 0.0), ra=0.047 if fem else 0.057, rb=0.042 if fem else 0.05, k=0.03)
    a("ell", c=(0, 0.215, -0.012), rad=(0.077 if fem else 0.079, 0.098, 0.097), k=0.02)
    a("ell", c=(0, 0.214, 0.03), rad=(0.07, 0.06, 0.064), k=0.02)
    a("ell", c=(0, 0.172, 0.036), rad=o.get("mid", (0.064, 0.055, 0.06)), k=0.02)
    if o.get("jaw"):
        a("box", c=(0, 0.137, 0.035), half=o["jaw"], round=0.026, rot=rot_x(-12), k=0.02)
    else:
        a("ell", c=(0, 0.142, 0.035), rad=o["jawell"], k=0.02)
    a("sph", c=(0, 0.113, o.get("chin_z", 0.07)), r=o["chin"], k=0.02)
    cy = o.get("cheek_y", 0.18)
    for sx in (-1, 1):
        a("ell", c=(0.046 * sx, cy, 0.058), rad=o["cheek"], k=0.018)
        a("ell", c=(0.081 * sx, 0.19, -0.004), rad=(0.011, 0.029, 0.019), rot=rot_y(-12 * sx), k=0.007)    # ear
        a("sph", c=(0.033 * sx, 0.197, 0.091), r=0.0165, op="sub", k=0.007)                                 # eye socket
        a("sph", c=(0.0115 * sx, 0.162, 0.1005), r=0.0088, k=0.006)                                        # nostril wing
        if o.get("hollow"):
            a("sph", c=(0.056 * sx, 0.145, 0.066), r=0.016, op="sub", k=0.02)
    a("cap", a=(-0.041, 0.213, 0.081), b=(0.041, 0.213, 0.081), ra=o["brow"], rb=o["brow"], k=0.012)
    nl = o.get("nose_len", 0.0)
    nb = o.get("nose_bend", 0.0)
    a("cap", a=(0, 0.207, 0.087), b=(nb, 0.163 - nl, 0.111 + nl * 0.5), ra=o["nose"][0], rb=o["nose"][1], k=0.008)
    a("sph", c=(nb * 0.7, 0.165 - nl, 0.111 + nl * 0.5), r=o["nose_tip"], k=0.006)
    lu, ll = o["lips"]
    a("cap", a=(-0.021, 0.1415, 0.0915), b=(0.021, 0.1415, 0.0915), ra=lu, rb=lu, k=0.006)
    a("cap", a=(-0.018, 0.1295, 0.0885), b=(0.018, 0.1295, 0.0885), ra=ll, rb=ll, k=0.006)
    return P


def body_prims(sex="m", face="rugged", h=1.0, w=1.0):
    fem = sex == "f"
    P = []
    def a(part, kind, k=0.025, op="add", rot=None, **kw):
        P.append(Prim(part, kind, k, op, rot, **kw))
    # pelvis (origin = hips centre, 0.94 h)
    if fem:
        a("Pelvis", "ell", c=(0, -0.01, -0.005), rad=(0.168 * w, 0.112, 0.108), k=0.03)
        for sx in (-1, 1):
            a("Pelvis", "ell", c=(0.068 * sx * w, -0.06, -0.05), rad=(0.084, 0.09, 0.078), k=0.03)
        a("Pelvis", "ell", c=(0, 0.035, 0.035), rad=(0.12, 0.075, 0.07), k=0.03)
    else:
        a("Pelvis", "ell", c=(0, -0.01, 0.0), rad=(0.152 * w, 0.105, 0.1), k=0.03)
        for sx in (-1, 1):
            a("Pelvis", "ell", c=(0.062 * sx * w, -0.055, -0.045), rad=(0.074, 0.084, 0.07), k=0.03)
        a("Pelvis", "ell", c=(0, 0.035, 0.035), rad=(0.128, 0.078, 0.075), k=0.03)
        a("Pelvis", "sph", c=(0, -0.085, 0.035), r=0.04, k=0.03)
    # chest (origin = waist, grows up 0.44 h)
    L = 0.44 * h
    if fem:
        a("Chest", "ell", c=(0, 0.07 * h, 0), rad=(0.112 * w, 0.11 * h, 0.086), k=0.04)
        a("Chest", "ell", c=(0, 0.25 * h, 0.0), rad=(0.138 * w, 0.165 * h, 0.095), k=0.04)
        a("Chest", "ell", c=(0, 0.335 * h, -0.005), rad=(0.146 * w, 0.08 * h, 0.088), k=0.03)
        for sx in (-1, 1):
            a("Chest", "ell", c=(0.064 * sx * w, 0.262 * h, 0.07), rad=(0.062, 0.06, 0.056), rot=rot_y(14 * sx), k=0.018)
            a("Chest", "ell", c=(0.075 * sx * w, 0.24 * h, -0.03), rad=(0.07, 0.13, 0.06), k=0.03)
        a("Chest", "cap", a=(-0.13 * w, 0.383 * h, -0.016), b=(0.13 * w, 0.383 * h, -0.016), ra=0.037, rb=0.037, k=0.035)
        a("Chest", "cap", a=(0, 0.35 * h, -0.025), b=(0, L, -0.01), ra=0.058, rb=0.042, k=0.03)
    else:
        a("Chest", "ell", c=(0, 0.065 * h, 0), rad=(0.13 * w, 0.105 * h, 0.094), k=0.04)
        a("Chest", "ell", c=(0, 0.125 * h, 0.035), rad=(0.11 * w, 0.1 * h, 0.07), k=0.03)
        a("Chest", "ell", c=(0, 0.25 * h, 0.0), rad=(0.152 * w, 0.17 * h, 0.104), k=0.04)
        a("Chest", "ell", c=(0, 0.335 * h, -0.005), rad=(0.162 * w, 0.085 * h, 0.098), k=0.03)
        for sx in (-1, 1):
            a("Chest", "ell", c=(0.062 * sx * w, 0.3 * h, 0.058), rad=(0.07, 0.056, 0.05), k=0.02)
            a("Chest", "ell", c=(0.085 * sx * w, 0.23 * h, -0.035), rad=(0.08, 0.14, 0.066), k=0.03)
        a("Chest", "cap", a=(-0.145 * w, 0.388 * h, -0.02), b=(0.145 * w, 0.388 * h, -0.02), ra=0.045, rb=0.045, k=0.035)
        a("Chest", "cap", a=(0, 0.35 * h, -0.03), b=(0, L, -0.012), ra=0.07, rb=0.05, k=0.03)
    # arms (right-side authoring, mirrored for L)
    f = 0.85 if fem else 1.0
    for side, sfx in ((-1, "L"), (1, "R")):
        U, Fo, Hd = "UpperArm" + sfx, "Forearm" + sfx, "Hand" + sfx
        a(U, "ell", c=(0.004 * side, -0.035 * h, 0), rad=(0.054 * f * w, 0.078 * h, 0.057 * f * w), k=0.02)
        a(U, "cap", a=(0, -0.02, 0), b=(0, -0.27 * h, 0), ra=0.045 * f * w, rb=0.035 * f * w, k=0.02)
        a(U, "ell", c=(0, -0.145 * h, 0.012), rad=(0.039 * f * w, 0.075 * h, 0.042 * f * w), k=0.02)
        a(U, "ell", c=(0, -0.13 * h, -0.012), rad=(0.039 * f * w, 0.085 * h, 0.04 * f * w), k=0.02)
        a(Fo, "sph", c=(0, 0.0, -0.006), r=0.037 * f * w, k=0.02)
        a(Fo, "cap", a=(0, 0, 0), b=(0, -0.235 * h, 0), ra=0.035 * f * w, rb=0.024 * f * w, k=0.02)
        a(Fo, "ell", c=(0, -0.075 * h, 0.0), rad=(0.041 * f * w, 0.085 * h, 0.037 * f * w), k=0.02)
        a(Fo, "ell", c=(0, -0.232 * h, 0), rad=(0.02 * f, 0.02, 0.029 * f), k=0.012)
        hs = 0.92 if fem else 1.0
        a(Hd, "box", c=_m((0, -0.05 * hs, 0.002), side), half=(0.0125 * hs, 0.045 * hs, 0.037 * hs), round=0.011 * hs, k=0.012)
        for fz, ln in ((0.0255, 0.07), (0.0085, 0.078), (-0.0085, 0.073), (-0.0245, 0.058)):
            ln *= hs
            a(Hd, "cap", a=_m((0.0, -0.088 * hs, fz * hs), side), b=_m((-0.008, -(0.088 * hs + ln * 0.62), fz * hs), side),
              ra=0.0083 * hs, rb=0.0078 * hs, k=0.004)
            a(Hd, "cap", a=_m((-0.008, -(0.088 * hs + ln * 0.62), fz * hs), side), b=_m((-0.017, -(0.088 * hs + ln), fz * 0.95 * hs), side),
              ra=0.0078 * hs, rb=0.0068 * hs, k=0.004)
        a(Hd, "cap", a=_m((-0.006, -0.012, 0.028 * hs), side), b=_m((-0.019, -0.052 * hs, 0.046 * hs), side), ra=0.0125 * hs, rb=0.0095 * hs, k=0.008)
        a(Hd, "cap", a=_m((-0.019, -0.052 * hs, 0.046 * hs), side), b=_m((-0.024, -0.079 * hs, 0.05 * hs), side), ra=0.0092 * hs, rb=0.0082 * hs, k=0.004)
    # legs
    for side, sfx in ((-1, "L"), (1, "R")):
        T, S, Ft = "Thigh" + sfx, "Shin" + sfx, "Foot" + sfx
        tw = 1.04 if fem else 1.0
        a(T, "cap", a=(0, 0.0, 0), b=(0, -0.41 * h, 0.0), ra=0.082 * tw * w, rb=0.05 * w, k=0.03)
        a(T, "ell", c=(0.006 * side, -0.16 * h, 0.022), rad=(0.07 * tw * w, 0.15 * h, 0.064 * tw), k=0.03)
        a(T, "ell", c=(0, -0.2 * h, -0.022), rad=(0.064 * tw * w, 0.15 * h, 0.06 * tw), k=0.03)
        if fem:
            a(T, "ell", c=(0.035 * side, -0.05 * h, -0.005), rad=(0.06, 0.08, 0.07), k=0.03)
        a(S, "sph", c=(0, -0.005, 0.014), r=0.047 * w, k=0.025)
        a(S, "cap", a=(0, -0.02, 0), b=(0, -0.405 * h, 0), ra=0.046 * w, rb=0.029 * w, k=0.02)
        a(S, "ell", c=(-0.004 * side, -0.135 * h, -0.024), rad=(0.047 * w, 0.11 * h, 0.05 * w), k=0.03)
        a(S, "sph", c=(0, -0.418 * h, 0.0), r=0.031, k=0.015)
        fs = 0.93 if fem else 1.0
        a(Ft, "ell", c=(0, -0.034, -0.022 * fs), rad=(0.032 * fs, 0.028, 0.04 * fs), k=0.015)
        a(Ft, "box", c=(0, -0.034, 0.062 * fs), half=(0.037 * fs, 0.026, 0.075 * fs), round=0.02, rot=rot_x(6), k=0.02)
        a(Ft, "ell", c=(0.002 * side, -0.043, 0.128 * fs), rad=(0.046 * fs, 0.019, 0.04 * fs), k=0.015)
        a(Ft, "cap", a=(-0.032 * fs, -0.048, 0.166 * fs), b=(0.03 * fs, -0.048, 0.158 * fs), ra=0.0135, rb=0.012, k=0.01)
    P += head_prims(face, h)
    return P


def eye_centres(h=1.0):
    return [((0.033 * sx) * h, 0.196 * h, 0.077 * h) for sx in (-1, 1)]


# ---------------------------------------------------------------- hair / beard / brows (head-local fields on a fine grid)
def hair_field(style, x, y, z, Fh, h=1.0):
    """x,y,z: head-local coords; Fh: head SDF on the same grid. Returns hair SDF (np array) or None."""
    if style == "Bald":
        return None
    y = y / h
    # hairline: forehead top at the front, temples, low at the nape
    zt = np.clip((z - (-0.03)) / 0.09, 0, 1)
    line = 0.13 + (0.245 - 0.13) * zt
    side = np.abs(x) > 0.064
    line = np.where(side & (z < 0.045) & (z > -0.02), np.minimum(line, 0.168), line)   # sideburns
    R = line - y
    if style == "Buzz":
        th = 0.0045 + 0.0008 * np.sin(60 * np.arctan2(x, z) + 80 * y)
        return np.maximum(Fh - th, R)
    if style == "Mohawk":
        th = 0.004 + 0.05 * np.clip((y - 0.19) / 0.1, 0, 1) * np.clip((0.07 - np.abs(z + 0.0)) / 0.05 + 0.6, 0, 1)
        return np.maximum(np.maximum(Fh - th, R), np.abs(x) - 0.016 - 0.004 * np.clip((y - 0.2) / 0.1, 0, 1))
    ang = np.arctan2(x, z + 0.02)
    clumps = 0.0028 * np.sin(22 * ang + 35 * y) + 0.0015 * np.sin(57 * ang - 20 * y)               # strand clumps
    th = 0.006 + 0.01 * np.clip((y - 0.19) / 0.11, 0, 1) + clumps
    cap = np.maximum(Fh - th, R)
    fr = Prim("Head", "ell", c=(0.015, 0.268, 0.07), rad=(0.05, 0.022, 0.03), rot=rot_z(-14)).eval(x, y, z)       # fringe swept to one side
    cap = np.minimum(cap, np.maximum(fr, 0.247 - y))
    if style == "Short":
        return cap
    if style == "Ponytail":
        tail = Prim("Head", "cap", a=(0, 0.21, -0.098), b=(0, 0.075, -0.128), ra=0.023, rb=0.011).eval(x, y, z)
        tie = Prim("Head", "tor", c=(0, 0.205, -0.098), R=0.017, r=0.007, rot=rot_x(70)).eval(x, y, z)
        knot = Prim("Head", "sph", c=(0, 0.215, -0.095), r=0.024).eval(x, y, z)
        return np.minimum(np.minimum(cap, tail), np.minimum(tie, knot))
    if style == "Long":
        th2 = 0.011 + 0.012 * np.clip((y - 0.19) / 0.11, 0, 1)
        shell = np.maximum(Fh - th2, 0.13 - y)
        curtain = Prim("Head", "ell", c=(0, 0.12, -0.045), rad=(0.098, 0.15, 0.068)).eval(x, y, z)
        hair = np.minimum(shell, np.maximum(curtain, -0.005 - y))
        face = np.maximum(np.maximum(0.02 - z, np.abs(x) - 0.066), y - 0.25)      # negative over the open face
        hair = np.maximum(hair, -face)
        return np.minimum(hair, np.maximum(fr, 0.247 - y))
    return cap


def beard_field(kind, x, y, z, Fh, h=1.0):
    """kind 2 = full beard (mesh). Stubble (1) is painted on the skin."""
    if kind < 2:
        return None
    y = y / h
    th = 0.007 + 0.012 * np.clip((0.14 - y) / 0.06, 0, 1)
    R = np.maximum(y - 0.176, 0.005 - z)                       # below the cheekbones, in front of the ears
    R = np.maximum(R, 0.07 - y - 0.03 * (z > 0.03))            # not down the neck
    beard = np.maximum(Fh - th, R)
    mouth = Prim("Head", "ell", c=(0, 0.135, 0.1), rad=(0.02, 0.0075, 0.03)).eval(x, y, z)
    return np.maximum(beard, -mouth)


def brow_prims(face, h=1.0):
    sex, o = FACES[face]
    t = 0.0058 if sex == "m" else 0.0042
    P = []
    for sx in (-1, 1):
        P.append(Prim("Head", "cap", a=(0.016 * sx * h, 0.219 * h, 0.0915 * h), b=(0.05 * sx * h, 0.216 * h, 0.0835 * h),
                      ra=t * h, rb=t * 0.7 * h, k=0.002))
    return P
