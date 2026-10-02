"""HD garments as layered SDF shells over the body (ClothingLibrary ids, slots and colours), plus trims.

Each garment returns pieces: (piece name, material key, field(ctx) -> SDF, bound parts, extra bounds, options).
Layer order = offset over the skin, like ClothingDef.inflate: underwear 4 mm < trousers 7 < shirts 10 < overalls 14
< jackets 16 < dusters 18 < vests 24-30 mm."""
import math

import numpy as np

from hdlib import PI, Prim, rot_x, rot_y, rot_z
from build_lib import FINE_H

# material key -> (hex, kind, options) ; colours from ClothingLibrary / Pal ramps
MATS = {
    "under": ("3a3634", "cloth", {}),
    "tshirt": ("dcd8c8", "cloth", {}),
    "tshirt_worn": ("c4c0b0", "cloth", {"grime": 0.6}),
    "tank": ("8c5634", "cloth", {}),
    "hoodie": ("4f4842", "knit", {}),
    "sweater": ("2e5024", "knit", {}),
    "sweater_band": ("eeeadc", "knit", {}),
    "jacket": ("302d33", "leather", {}),
    "duster": ("74492a", "leather", {}),
    "labcoat": ("eeeadc", "cloth", {"grime": 0.5}),
    "vest": ("a8967a", "canvas", {}),
    "vest_pocket": ("8a7a64", "canvas", {}),
    "bomber": ("2a3832", "cloth", {}),
    "fleece": ("dcd8c8", "knit", {"scale": 2.0}),
    "pants": ("8c5634", "cloth", {}),
    "jeans": ("2c3c52", "denim", {}),
    "overalls": ("243866", "denim", {}),
    "belt": ("3a2414", "leather", {}),
    "boots": ("56361e", "leather", {}),
    "sole": ("18171c", "rubber", {}),
    "cboots": ("232127", "leather", {}),
    "gloves": ("232127", "leather", {}),
    "fingerless": ("302d33", "knit", {}),
    "cowboy": ("74492a", "cloth", {"rough": 0.7}),
    "hatband": ("18171c", "leather", {}),
    "sunhat": ("a8967a", "canvas", {}),
    "beanie": ("7e261c", "knit", {}),
    "helmet": ("4f4842", "metal", {"hex2": "80401d", "metal": 0.7}),
    "chrome": ("aeb1b8", "metal", {"hex2": "5e2c15"}),
    "brass": ("a0582c", "metal", {"hex2": "3f1e10"}),
    "strap": ("18171c", "leather", {}),
    "amber": ("d08a30", "lens", {"rough": 0.05}),
    "glass": ("2c3c52", "lens", {"rough": 0.05}),
    "bandana": ("7e261c", "cloth", {}),
    "shemagh": ("dcd8c8", "check", {"hex2": "232127"}),
    "scarf": ("5a1a14", "stripe", {"hex2": "dcd8c8"}),
    "rubber": ("232127", "rubber", {}),
    "filter": ("6b3e24", "plastic", {}),
    "bone": ("eeeadc", "plastic", {"grime": 0.5}),
    "plate": ("4f4842", "metal", {"hex2": "80401d", "metal": 0.6}),
    "plate_rust": ("80401d", "metal", {"hex2": "3f1e10", "metal": 0.3}),
    "kevlar": ("243866", "cloth", {}),
    "pouch": ("8c5634", "canvas", {}),
    "leather_brown": ("74492a", "leather", {}),
    "backpack": ("a06e1e", "canvas", {}),
    "pack_dark": ("7c5214", "canvas", {}),
    "milpack": ("6b3e24", "canvas", {}),
    "bedroll": ("2e5024", "knit", {}),
    "wood": ("93603a", "plastic", {"rough": 0.7}),
    "red_handle": ("7c1c22", "plastic", {}),
    "poncho": ("6b3e24", "stripe", {"hex2": "a65c2e"}),
    "hazmat": ("c48c2a", "rubber", {"rough": 0.45}),
    "hazmat_trim": ("18171c", "rubber", {}),
    "dive_canvas": ("a8967a", "canvas", {"grime": 0.5}),
    "dive_brass": ("b07a34", "metal", {"hex2": "5e3a14", "metal": 0.9}),
    "tank_yellow": ("c4a030", "metal", {"hex2": "4f4842", "metal": 0.5}),
    "weld": ("302d33", "plastic", {"rough": 0.6}),
    "weld_lens": ("1a2a18", "lens", {"rough": 0.05}),
    "chitin": ("4a5220", "plastic", {"rough": 0.25, "hex2": "2a3014"}),
    "tyre": ("1c1918", "rubber", {}),
    "moto": ("7e261c", "plastic", {"rough": 0.2}),
    "veil": ("dcd8c8", "cloth", {"grime": 0.2}),
    "navy_pack": ("2c3c52", "canvas", {}),
    "black_pack": ("232127", "canvas", {}),
    "olive_pack": ("4a5a26", "canvas", {}),
    "sand_pack": ("bb8a54", "canvas", {}),
    "gunmetal": ("38332f", "metal", {"hex2": "18171c", "metal": 0.8}),
    "suitcase": ("6b3e24", "leather", {}),
}


# ---------------------------------------------------------------- field helpers
def P(part, kind, **kw):
    k = kw.pop("k", 0.01); op = kw.pop("op", "add"); rot = kw.pop("rot", None)
    return Prim(part, kind, k, op, rot, **kw)


def wy(c):
    return np.broadcast_to(c.grid.ax[1][None, :, None], c.grid.shape)


def shell(c, off, cov, k=0.004):
    """Skin offset by `off` inside the coverage, with rounded hems."""
    a, b = c.F - off, c.cover(cov)
    hh = np.clip(0.5 - 0.5 * (b - a) / k, 0, 1)
    return b + (a - b) * hh + k * hh * (1 - hh)


def band(c, o_in, o_out):
    """Hollow layer between two offsets over the skin."""
    return np.maximum(c.F - o_out, o_in - c.F)


def only(c, parts, G):
    m = np.isin(c.label, [PI[p] for p in parts])
    return np.where(m, G, 1.0)


def neck_hole(c, G, r=0.068, y0=0.02, front=0.6):
    x, y, z = c.loc("Head")
    dist = np.sqrt(x * x + (z + 0.004) ** 2)
    hole = np.minimum(r - dist, y - (y0 - front * np.maximum(z, 0)))
    return np.maximum(G, hole)


def arm_holes(c, G, r=0.07, top=0.03):
    for s in "LR":
        x, y, z = c.loc("UpperArm" + s)
        hole = np.minimum(r - np.sqrt(x * x + z * z), top - y)
        G = np.maximum(G, hole)
    return G


def hem(c, part, t, off, w=0.009, side=-1):
    """A thicker band at a garment's edge: t along the part, the band lies on the covered side (side -1 = below t)."""
    u, L = c.along(part)
    G = np.maximum(c.F - off, np.abs(u - (t * L + side * w)) - w)
    return only(c, [part], G)


def hems(c, parts, t, off, w=0.009, side=-1):
    G = np.full(c.grid.shape, 1.0, np.float32)
    for p in parts:
        G = np.minimum(G, hem(c, p, t, off, w, side))
    return G


def U(*fields):
    out = fields[0]
    for f in fields[1:]:
        out = np.minimum(out, f)
    return out


def smooth_union(a, b, k):
    hh = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b + (a - b) * hh - k * hh * (1 - hh)


ARMS = ["UpperArmL", "UpperArmR", "ForearmL", "ForearmR"]
LEGS = ["ThighL", "ThighR", "ShinL", "ShinR"]
TORSO = ["Pelvis", "Chest"]


def piece(name, mat, field, parts, extra=None, **kw):
    d = dict(name=name, mat=mat, field=field, parts=parts, extra=extra)
    d.update(kw)
    return d


# ---------------------------------------------------------------- garments (by ClothingLibrary id)
def underwear(h):
    def briefs(c):
        G = shell(c, 0.0045, {"Pelvis": (0, 1), "ThighL": (0, 0.12), "ThighR": (0, 0.12), "Chest": (0, 0.2)})
        x, y, z = c.loc("Pelvis")
        return np.maximum(G, y - 0.075)
    out = [piece("under", "under", briefs, TORSO + ["ThighL", "ThighR"], tris=1400)]
    if h.sex == "f":
        def top(c):
            x, y, z = c.loc("Chest")
            G = only(c, ["Chest"], np.maximum(c.F - 0.0045, np.abs(y - 0.262 * c.hum.h) - 0.055))
            strap = only(c, ["Chest"], np.maximum(np.maximum(c.F - 0.0045, np.abs(np.abs(x) - 0.068) - 0.01), 0.2 - y))
            return U(G, strap)
        out.append(piece("top", "under", top, ["Chest"], tris=1200))
    return out


def tshirt(h, mat="tshirt"):
    def f(c):
        off = 0.01 + 0.012 * np.clip((1.02 * c.hum.h - wy(c)) / 0.07, 0, 1)
        G = shell(c, off, {"Chest": (0, 1), "Head": (0, 0.2), "UpperArmL": (0, 0.45), "UpperArmR": (0, 0.45), "Pelvis": (0, 0.42)})
        G = neck_hole(c, G, 0.066, 0.012, 0.5)
        G = U(G, hems(c, ["UpperArmL", "UpperArmR"], 0.45, 0.0125, 0.008))
        return G
    return [piece("tshirt", mat, f, TORSO + ["Head", "UpperArmL", "UpperArmR"], tris=2600)]


def tank(h):
    def f(c):
        off = 0.01 + 0.012 * np.clip((1.02 * c.hum.h - wy(c)) / 0.07, 0, 1)
        G = shell(c, off, {"Chest": (0, 1), "Pelvis": (0, 0.42), "Head": (0, 0.2)})
        G = neck_hole(c, G, 0.075, 0.0, 1.5)
        return arm_holes(c, G, 0.075, 0.04)
    return [piece("tank", "tank", f, TORSO + ["Head"], tris=1800)]


def hoodie(h):
    def f(c):
        off = 0.012 + 0.01 * np.clip((1.0 * c.hum.h - wy(c)) / 0.07, 0, 1)
        G = shell(c, off, {"Chest": (0, 1), "Head": (0, 0.2), "Pelvis": (0, 0.5), "UpperArmL": (0, 1), "UpperArmR": (0, 1),
                           "ForearmL": (0, 0.9), "ForearmR": (0, 0.9)})
        G = neck_hole(c, G, 0.07, 0.02, 0.4)
        hood = c.prims([P("Chest", "ell", c=(0, 0.45, -0.075), rad=(0.105, 0.075, 0.07), k=0.03),
                        P("Chest", "ell", c=(0, 0.47, -0.035), rad=(0.085, 0.05, 0.06), op="sub", k=0.02)])
        pocket = c.prims([P("Chest", "box", c=(0, 0.075, 0.105), half=(0.08, 0.05, 0.012), round=0.009, k=0.012)])
        cuffs = hems(c, ["ForearmL", "ForearmR"], 0.9, 0.0145, 0.012)
        waist = hem(c, "Pelvis", 0.5, 0.025, 0.014)
        return U(smooth_union(G, hood, 0.02), pocket, cuffs, waist)
    def strings(c):
        x, y, z = c.loc("Chest")
        return c.prims([P("Chest", "cap", a=(sx * 0.03, 0.43, 0.075), b=(sx * 0.034, 0.33, 0.112), ra=0.0035, rb=0.0035) for sx in (-1, 1)]
                       + [P("Chest", "sph", c=(sx * 0.034, 0.33, 0.112), r=0.006) for sx in (-1, 1)])
    return [piece("hoodie", "hoodie", f, TORSO + ["Head"] + ARMS, tris=3600),
            piece("hoodie_strings", "fleece", strings, ["Chest"], res=FINE_H, tris=300, smooth=1, allowed=["Chest"])]


def sweater(h):
    def f(c):
        G = shell(c, 0.012, {"Chest": (0, 1), "Head": (0, 0.2), "Pelvis": (0, 0.45), "UpperArmL": (0, 1), "UpperArmR": (0, 1),
                             "ForearmL": (0, 0.95), "ForearmR": (0, 0.95)})
        G = neck_hole(c, G, 0.064, 0.03, 0.3)
        return U(G, hems(c, ["ForearmL", "ForearmR"], 0.95, 0.015, 0.014), hem(c, "Pelvis", 0.45, 0.016, 0.016),
                 only(c, ["Head", "Chest"], np.maximum(band(c, 0.0, 0.019), np.maximum(c.loc("Head")[1] - 0.045, -0.03 - c.loc("Head")[1]))))
    def bandf(c):
        x, y, z = c.loc("Chest")
        G = only(c, ["Chest", "UpperArmL", "UpperArmR"], np.maximum(c.F - 0.0135, np.abs(y - 0.275 * c.hum.h) - 0.028))
        return G
    return [piece("sweater", "sweater", f, TORSO + ["Head"] + ARMS, tris=3200),
            piece("sweater_band", "sweater_band", bandf, ["Chest", "UpperArmL", "UpperArmR"], tris=1200)]


def _coat_collar(c, o_in, o_out, top=0.07, open_z=0.03):
    x, y, z = c.loc("Head")
    G = np.maximum(band(c, o_in, o_out), np.maximum(y - top, -0.035 - y))
    return only(c, ["Head", "Chest"], np.maximum(G, z - open_z))


def jacket(h):
    def f(c):
        off = 0.016 + 0.006 * np.clip((1.04 * c.hum.h - wy(c)) / 0.08, 0, 1)
        G = shell(c, off, {"Chest": (0, 1), "Head": (0, 0.2), "Pelvis": (0, 0.36), "UpperArmL": (0, 1), "UpperArmR": (0, 1),
                           "ForearmL": (0, 0.92), "ForearmR": (0, 0.92)})
        G = neck_hole(c, G, 0.072, 0.02, 0.9)
        x, y, z = c.loc("Chest")
        G = np.maximum(G, np.minimum(0.006 - np.abs(x), z - 0.02))         # open front gap
        lapel = only(c, ["Chest"], np.maximum(np.maximum(band(c, 0.016, 0.026), 0.3 - y), np.maximum(z - 0.2, np.abs(x) - 0.08)))
        lapel = np.maximum(lapel, -(np.abs(x) - (0.012 + (y - 0.3) * 0.35)))
        return U(G, _coat_collar(c, 0.016, 0.03, 0.06, 0.035), hems(c, ["ForearmL", "ForearmR"], 0.92, 0.02, 0.011),
                 hem(c, "Pelvis", 0.36, 0.026, 0.013), lapel)
    def zip_(c):
        x, y, z = c.loc("Chest")
        G = np.maximum(np.maximum(np.abs(x - 0.011) - 0.0035, band(c, 0.014, 0.022)), np.maximum(y - 0.3, -0.045 - y))
        return only(c, ["Chest", "Pelvis"], np.maximum(G, -z))
    return [piece("jacket", "jacket", f, TORSO + ["Head"] + ARMS, tris=4200),
            piece("jacket_zip", "chrome", zip_, ["Chest", "Pelvis"], res=FINE_H, tris=500, smooth=1, allowed=["Chest", "Pelvis"], sym=False)]


def bomber(h):
    def f(c):
        G = shell(c, 0.017, {"Chest": (0, 1), "Head": (0, 0.2), "Pelvis": (0, 0.3), "UpperArmL": (0, 1), "UpperArmR": (0, 1),
                             "ForearmL": (0, 0.95), "ForearmR": (0, 0.95)})
        G = neck_hole(c, G, 0.074, 0.02, 0.5)
        x, y, z = c.loc("Chest")
        G = U(G, c.prims([P("Chest", "box", c=(sx * 0.07, 0.2, 0.105), half=(0.03, 0.035, 0.01), round=0.007) for sx in (-1, 1)]))
        return U(G, hems(c, ["ForearmL", "ForearmR"], 0.95, 0.022, 0.013), hem(c, "Pelvis", 0.3, 0.026, 0.016))
    def collar(c):
        return _coat_collar(c, 0.014, 0.034, 0.065, 0.06)
    def zip_(c):
        x, y, z = c.loc("Chest")
        G = np.maximum(np.maximum(np.abs(x) - 0.0035, band(c, 0.015, 0.023)), np.maximum(y - 0.42, -0.05 - y))
        return only(c, ["Chest", "Pelvis"], np.maximum(G, -z))
    return [piece("bomber", "bomber", f, TORSO + ["Head"] + ARMS, tris=3800),
            piece("bomber_collar", "fleece", collar, ["Chest", "Head"], tris=900, allowed=["Chest", "Head"]),
            piece("bomber_zip", "chrome", zip_, ["Chest", "Pelvis"], res=FINE_H, tris=400, smooth=1, allowed=["Chest", "Pelvis"])]


def duster(h, mat="duster"):
    def f(c):
        off = 0.018 + 0.01 * np.clip((1.0 * c.hum.h - wy(c)) / 0.3, 0, 1)
        G = shell(c, off, {"Chest": (0, 1), "Head": (0, 0.2), "Pelvis": (0, 1), "UpperArmL": (0, 1), "UpperArmR": (0, 1),
                           "ForearmL": (0, 0.9), "ForearmR": (0, 0.9), "ThighL": (0, 1), "ThighR": (0, 1)})
        G = neck_hole(c, G, 0.075, 0.025, 0.9)
        x, y, z = c.loc("Pelvis")
        skirt = c.prims([P("Pelvis", "cap", a=(0, 0.0, -0.012), b=(0, -0.64 * c.hum.h, -0.04), ra=0.17 * c.hum.w, rb=0.255, k=0.04)])
        skirt = np.maximum(skirt, -0.64 * c.hum.h - y)
        G = smooth_union(G, skirt, 0.03)
        gap = 0.004 + 0.07 * np.clip((-0.02 - y) / 0.6, 0, 1)
        G = np.maximum(G, np.minimum(gap - np.abs(x), np.minimum(z, 0.0 - y)))         # front opening below the waist
        xc, yc, zc = c.loc("Chest")
        G = np.maximum(G, np.minimum(0.005 - np.abs(xc), np.minimum(zc - 0.02, yc + 0.2)))
        cape = c.prims([P("Chest", "ell", c=(0, 0.37, -0.01), rad=(0.235 * c.hum.w, 0.07, 0.135), k=0.02)])
        cape = np.maximum(cape, 0.3 - yc)
        cape = np.maximum(cape, -(c.F - 0.03))
        cape = neck_hole(c, cape, 0.08, 0.0, 0.9)
        return U(G, _coat_collar(c, 0.02, 0.036, 0.085, 0.03), hems(c, ["ForearmL", "ForearmR"], 0.9, 0.024, 0.014), cape)
    return [piece(mat, mat, f, TORSO + ["Head"] + ARMS + LEGS, tris=5200)]


def vest(h):
    def f(c):
        G = shell(c, 0.024, {"Chest": (0.05, 0.98), "Pelvis": (0, 0.3), "Head": (0, 0.2)})
        G = neck_hole(c, G, 0.085, 0.0, 1.6)
        G = arm_holes(c, G, 0.085, 0.05)
        x, y, z = c.loc("Chest")
        G = np.maximum(G, np.minimum(0.008 - np.abs(x), z))
        return G
    def pockets(c):
        return c.prims([P("Chest", "box", c=(sx * 0.065, yy, 0.12 if yy < 0.2 else 0.118), half=(0.032, 0.03, 0.014), round=0.006, rot=rot_x(-6))
                        for sx in (-1, 1) for yy in (0.1, 0.24)])
    def stripes(c):
        x, y, z = c.loc("Chest")
        rows = np.abs(((y + 0.02) % 0.1) - 0.05) - 0.007
        G = np.maximum(band(c, 0.022, 0.0275), rows)
        G = neck_hole(c, np.maximum(G, c.cover({"Chest": (0.05, 0.98), "Pelvis": (0, 0.3)})), 0.085, 0.0, 1.6)
        G = arm_holes(c, G, 0.085, 0.05)
        return np.maximum(G, np.minimum(0.008 - np.abs(x), z))
    return [piece("vest", "vest", f, TORSO + ["Head"], tris=2400, allowed=TORSO + ["Head", "UpperArmL", "UpperArmR"]),
            piece("vest_pockets", "vest_pocket", pockets, ["Chest"], tris=600, allowed=["Chest"], smooth=1, sharp=50),
            piece("vest_stripes", "plate_rust", stripes, TORSO, tris=1600, allowed=TORSO, smooth=1)]


def _legs_shell(c, off, shin_t, waist_y=0.045, thigh_t=1.0):
    cov = {"Pelvis": (0, 1), "ThighL": (0, thigh_t), "ThighR": (0, thigh_t), "Chest": (0, 0.25)}
    if shin_t > 0:
        cov.update({"ShinL": (0, shin_t), "ShinR": (0, shin_t)})
    G = shell(c, off, cov)
    x, y, z = c.loc("Chest")
    return np.maximum(G, y - waist_y * c.hum.h)


def _belt(c, off=0.011, y0=0.025):
    x, y, z = c.loc("Chest")
    return only(c, ["Chest", "Pelvis"], np.maximum(c.F - off, np.abs(y - y0 * c.hum.h) - 0.017))


def pants(h, shin_t=0.85, name="pants", mat="pants"):
    short = shin_t < 0.6
    def f(c):
        if short:
            G = _legs_shell(c, 0.0075, 0, thigh_t=shin_t)
            return U(G, hems(c, ["ThighL", "ThighR"], shin_t, 0.011, 0.01))
        G = _legs_shell(c, 0.0075, shin_t)
        pk = [P("Thigh" + s, "box", c=(sx * 0.074 * c.hum.w, -0.22, 0.004), half=(0.014, 0.055, 0.05), round=0.009, rot=rot_z(sx * 4))
              for s, sx in (("L", -1), ("R", 1))]
        return U(G, c.prims(pk), hems(c, ["ShinL", "ShinR"], shin_t, 0.011, 0.01))
    return [piece(name, mat, f, TORSO + LEGS, tris=3200),
            piece(name + "_belt", "belt", lambda c: _belt(c), TORSO, tris=600, allowed=TORSO)]


def jeans(h):
    def f(c):
        G = _legs_shell(c, 0.0075, 0.88)
        x, y, z = c.loc("Pelvis")
        back = c.prims([P("Pelvis", "box", c=(sx * 0.065, -0.02, -0.112), half=(0.04, 0.045, 0.01), round=0.006, rot=rot_x(12)) for sx in (-1, 1)])
        return U(G, back, hems(c, ["ShinL", "ShinR"], 0.88, 0.011, 0.01))
    return [piece("jeans", "jeans", f, TORSO + LEGS, tris=3200),
            piece("jeans_belt", "belt", lambda c: _belt(c), TORSO, tris=600, allowed=TORSO)]


def shorts(h):
    return pants(h, 0.55, "shorts", "pants")


def overalls(h):
    def f(c):
        G = shell(c, 0.014, {"Pelvis": (0, 1), "ThighL": (0, 1), "ThighR": (0, 1), "ShinL": (0, 0.9), "ShinR": (0, 0.9), "Chest": (0, 1)})
        x, y, z = c.loc("Chest")
        hh = c.hum.h
        strap = np.abs(np.abs(x) - 0.068) - 0.016
        bib = np.maximum(np.maximum(np.abs(x) - 0.105, -z), y - 0.31 * hh)
        backp = y - 0.12 * hh
        keep = np.minimum(np.minimum(bib, backp), strap)
        G = np.where(c.label == PI["Chest"], np.maximum(G, keep), G)
        pocket = c.prims([P("Chest", "box", c=(0, 0.23 * hh, 0.112), half=(0.06, 0.045, 0.01), round=0.006)])
        return U(G, pocket, hems(c, ["ShinL", "ShinR"], 0.9, 0.017, 0.011))
    def buttons(c):
        return c.prims([P("Chest", "cyl", c=(sx * 0.068, 0.3 * c.hum.h, 0.112), r=0.012, halfh=0.004, round=0.002, rot=rot_x(90)) for sx in (-1, 1)])
    return [piece("overalls", "overalls", f, TORSO + LEGS, tris=4000),
            piece("overalls_buttons", "chrome", buttons, ["Chest"], res=FINE_H, tris=300, allowed=["Chest"], smooth=0)]


def boots(h, name="boots", mat="boots", top=0.72):
    def f(c):
        G = shell(c, 0.013, {"FootL": (0, 1), "FootR": (0, 1), "ShinL": (top, 1), "ShinR": (top, 1)})
        toes = []
        for s in "LR":
            toes.append(P("Foot" + s, "ell", c=(0, -0.036, 0.15), rad=(0.05, 0.034, 0.06), k=0.03))
            toes.append(P("Foot" + s, "box", c=(0, -0.036, 0.06), half=(0.048, 0.03, 0.09), round=0.025, k=0.03))
        G = smooth_union(G, c.prims(toes), 0.02)
        for s in "LR":
            x, y, z = c.loc("Foot" + s)
            G = np.maximum(G, np.where(c.label == PI["Foot" + s], -0.048 - y, G))
        cuff = hems(c, ["ShinL", "ShinR"], top, 0.017, 0.012, side=1)
        return U(G, cuff)
    def sole(c):
        out = []
        for s in "LR":
            out.append(P("Foot" + s, "box", c=(0, -0.053, 0.072), half=(0.053, 0.009, 0.128), round=0.008, k=0.004))
            out.append(P("Foot" + s, "box", c=(0, -0.045, -0.035), half=(0.042, 0.017, 0.03), round=0.008, k=0.004))
        return c.prims(out)
    pcs = [piece(name, mat, f, ["FootL", "FootR", "ShinL", "ShinR"], tris=2200, allowed=["FootL", "FootR", "ShinL", "ShinR"]),
           piece(name + "_sole", "sole", sole, ["FootL", "FootR"], tris=600, allowed=["FootL", "FootR"], sharp=45, smooth=1)]
    if name == "combat_boots":
        def laces(c):
            out = []
            for s in "LR":
                u, L = c.along("Shin" + s)
                x, y, z = c.loc("Shin" + s)
                rows = np.abs(((u - 0.26) % 0.028) - 0.014) - 0.0028
                G = np.maximum(np.maximum(band(c, 0.012, 0.0185), rows), np.maximum(np.abs(x) - 0.022, -z))
                out.append(only(c, ["Shin" + s], np.maximum(G, np.maximum(0.25 - u, u - 0.42))))
            return U(*out)
        pcs.append(piece("combat_laces", "strap", laces, ["ShinL", "ShinR"], res=FINE_H, tris=900, allowed=["ShinL", "ShinR"], smooth=1))
    return pcs


def combat_boots(h):
    return boots(h, "combat_boots", "cboots", 0.6)


def gloves(h, name="gloves", mat="gloves", t=1.0):
    def f(c):
        cov = {"HandL": (0, t), "HandR": (0, t), "ForearmL": (0.85, 1), "ForearmR": (0.85, 1)}
        G = shell(c, 0.0042, cov)
        if t < 1:
            G = np.maximum(G, -(c.F + 0.0))
        return U(G, hems(c, ["ForearmL", "ForearmR"], 0.85, 0.008, 0.008, side=1))
    return [piece(name, mat, f, ["HandL", "HandR", "ForearmL", "ForearmR"], res=FINE_H, tris=2400,
                  allowed=["HandL", "HandR", "ForearmL", "ForearmR"])]


def fingerless(h):
    return gloves(h, "fingerless", "fingerless", 0.95)


# ---------------------------------------------------------------- head wear (head-local, y up from the neck base)
def cowboy(h):
    def crown(c):
        x, y, z = c.loc("Head")
        s = c.hum.h
        cr = c.prims([P("Head", "ell", c=(0, 0.275 * s, -0.01), rad=(0.094, 0.075, 0.108), k=0.01),
                      P("Head", "ell", c=(0, 0.36 * s, -0.01), rad=(0.022, 0.03, 0.075), op="sub", k=0.02),
                      P("Head", "sph", c=(0.05, 0.335 * s, 0.07), r=0.026, op="sub", k=0.02),
                      P("Head", "sph", c=(-0.05, 0.335 * s, 0.07), r=0.026, op="sub", k=0.02)])
        cr = np.maximum(cr, 0.236 * s - y)
        r = np.sqrt((x / 0.18) ** 2 + ((z + 0.005) / 0.205) ** 2)
        yb = 0.238 * s + 0.045 * np.clip(np.abs(x) / 0.18, 0, 1) ** 2 - 0.012 * np.clip(z / 0.2, 0, 1)
        brim = np.maximum(np.abs(y - yb) - 0.0045, (r - 1) * 0.18)
        return smooth_union(cr, brim, 0.012)
    def bandf(c):
        x, y, z = c.loc("Head")
        cr = c.prims([P("Head", "ell", c=(0, 0.275 * c.hum.h, -0.01), rad=(0.098, 0.079, 0.112))])
        return np.maximum(cr, np.abs(y - 0.255 * c.hum.h) - 0.011)
    return [piece("cowboy", "cowboy", crown, ["Head"], extra=_hat_box(h, 0.24), tris=1800, allowed=["Head"], hides=False),
            piece("cowboy_band", "hatband", bandf, ["Head"], tris=500, allowed=["Head"], hides=False)]


def _hat_box(h, r):
    y = h.Wb["Head"][1, 3]
    return (np.array([-r, y, -r]), np.array([r, y + 0.42, r]))


def sunhat(h):
    def f(c):
        x, y, z = c.loc("Head")
        s = c.hum.h
        cr = c.prims([P("Head", "ell", c=(0, 0.272 * s, -0.008), rad=(0.098, 0.07, 0.106), k=0.01)])
        cr = np.maximum(cr, 0.226 * s - y)
        r = np.sqrt(x * x + (z + 0.005) ** 2)
        yb = 0.235 * s - 0.05 * np.clip((r - 0.09) / 0.15, 0, 1) ** 1.6
        brim = np.maximum(np.abs(y - yb) - 0.004, r - 0.235)
        return smooth_union(cr, brim, 0.015)
    def bandf(c):
        x, y, z = c.loc("Head")
        cr = c.prims([P("Head", "ell", c=(0, 0.272 * c.hum.h, -0.008), rad=(0.102, 0.074, 0.11))])
        return np.maximum(cr, np.abs(y - 0.248 * c.hum.h) - 0.008)
    return [piece("sunhat", "sunhat", f, ["Head"], extra=_hat_box(h, 0.27), tris=1800, allowed=["Head"], hides=False),
            piece("sunhat_band", "leather_brown", bandf, ["Head"], tris=400, allowed=["Head"], hides=False)]


def beanie(h):
    def f(c):
        x, y, z = c.loc("Head")
        s = c.hum.h
        G = np.maximum(c.F - 0.016, 0.225 * s - y)
        top = c.prims([P("Head", "ell", c=(0, 0.3 * s, -0.025), rad=(0.075, 0.055, 0.085), k=0.01)])
        G = smooth_union(G, np.maximum(top, 0.24 * s - y), 0.02)
        fold = np.maximum(c.F - 0.022, np.abs(y - 0.243 * s) - 0.019)
        return U(G, np.maximum(fold, 0.225 * s - y))
    return [piece("beanie", "beanie", f, ["Head"], extra=_hat_box(h, 0.14), tris=1500, allowed=["Head"], hides=False)]


def helmet(h):
    def f(c):
        x, y, z = c.loc("Head")
        s = c.hum.h
        rim = 0.218 * s - 0.022 * np.clip(-z / 0.08, 0, 1)
        G = np.maximum(c.F - 0.024, rim - y)
        visor = c.prims([P("Head", "box", c=(0, 0.232 * s, 0.11), half=(0.07, 0.006, 0.03), round=0.004, rot=rot_x(-10))])
        crest = np.maximum(np.maximum(c.F - 0.036, np.abs(x) - 0.007), 0.27 * s - y)
        return U(G, visor, crest)
    def rivets(c):
        x, y, z = c.loc("Head")
        s = c.hum.h
        a = np.arctan2(x, z)
        step = 2 * math.pi / 16
        da = (np.abs(((a + step / 2) % step) - step / 2)) * 0.1
        d = np.sqrt((y - 0.228 * s) ** 2 + da ** 2) - 0.0055
        return np.maximum(d, c.F - 0.03)
    return [piece("helmet", "helmet", f, ["Head"], extra=_hat_box(h, 0.16), tris=1800, allowed=["Head"], sharp=50, hides=False),
            piece("helmet_rivets", "chrome", rivets, ["Head"], res=FINE_H, extra=_hat_box(h, 0.16), tris=900, allowed=["Head"], hides=False, smooth=0)]


def goggles(h, up=False, name="goggles"):
    dy = 0.075 if up else 0.0
    dz = -0.012 if up else 0.0
    tilt = rot_x(-30) if up else np.eye(3)
    def strap(c):
        x, y, z = c.loc("Head")
        s = c.hum.h
        yc = (0.2 + dy) * s
        G = np.maximum(c.F - 0.007, np.abs(y - yc + 0.02 * np.clip(-z / 0.1, 0, 1)) - 0.011)
        return np.maximum(G, z - 0.06)
    def cups(c):
        s = c.hum.h
        return c.prims([P("Head", "cyl", c=(sx * 0.034, (0.2 + dy) * s, 0.098 + dz), r=0.023, halfh=0.013, round=0.006, rot=tilt @ rot_x(90), k=0.004)
                        for sx in (-1, 1)] + [P("Head", "cap", a=(-0.012, (0.204 + dy) * s, 0.108 + dz), b=(0.012, (0.204 + dy) * s, 0.108 + dz), ra=0.005, rb=0.005)])
    def lens(c):
        s = c.hum.h
        return c.prims([P("Head", "cyl", c=(sx * 0.034, (0.2 + dy) * s, 0.111 + dz + (0.004 if up else 0)), r=0.0175, halfh=0.0035, round=0.002, rot=tilt @ rot_x(90))
                        for sx in (-1, 1)])
    ex = _hat_box(h, 0.13)
    return [piece(name + "_strap", "strap", strap, ["Head"], extra=ex, res=FINE_H, tris=900, allowed=["Head"], hides=False),
            piece(name + "_cups", "rubber", cups, ["Head"], extra=ex, res=FINE_H, tris=900, allowed=["Head"], hides=False, smooth=1),
            piece(name + "_lens", "amber", lens, ["Head"], extra=ex, res=FINE_H, tris=300, allowed=["Head"], hides=False, smooth=0)]


def bandana(h):
    def f(c):
        x, y, z = c.loc("Head")
        s = c.hum.h
        off = 0.006 + 0.022 * np.clip((0.12 * s - y) / 0.07, 0, 1) * np.clip(z / 0.06, 0, 1)
        G = np.maximum(c.F - off, np.maximum(y - 0.168 * s + 0.03 * np.clip(-z / 0.08, 0, 1), 0.05 * s - y + 0.08 * np.clip(z / 0.1, 0, 1)))
        knot = c.prims([P("Head", "ell", c=(0, 0.15 * s, -0.095), rad=(0.025, 0.018, 0.016))])
        return U(G, knot)
    return [piece("bandana", "bandana", f, ["Head"], extra=_hat_box(h, 0.14), tris=1000, allowed=["Head", "Chest"], hides=False)]


def shemagh(h):
    def f(c):
        x, y, z = c.loc("Head")
        s = c.hum.h
        G = np.maximum(c.F - 0.012, -0.03 - y)
        slit = np.maximum(np.maximum(np.abs(y - 0.205 * s) - 0.026, 0.02 - z), np.abs(x) - 0.072)
        G = np.maximum(G, -slit)
        drape = only(c, ["Chest"], np.maximum(c.F - 0.02, c.loc("Chest")[1] * -1 + 0.37 * s))
        tail = c.prims([P("Chest", "cap", a=(0.05, 0.42, -0.06), b=(0.09, 0.2, -0.11), ra=0.035, rb=0.05, k=0.03)])
        tail = np.maximum(tail, -(c.F - 0.015))
        return U(smooth_union(G, drape, 0.02), tail)
    return [piece("shemagh", "shemagh", f, ["Head", "Chest"], extra=_hat_box(h, 0.14), tris=2400, allowed=["Head", "Chest"])]


def scarf(h):
    def f(c):
        x, y, z = c.loc("Head")
        s = c.hum.h
        G = np.maximum(c.F - 0.024, np.maximum(y - 0.075 * s, -0.05 - y))
        G = only(c, ["Head", "Chest"], G)
        tails = c.prims([P("Chest", "box", c=(0.045, 0.33, 0.115), half=(0.028, 0.08, 0.006), round=0.005, rot=rot_x(-12) @ rot_z(-6), k=0.01),
                         P("Chest", "box", c=(0.012, 0.36, 0.122), half=(0.026, 0.06, 0.006), round=0.005, rot=rot_x(-10) @ rot_z(8), k=0.01)])
        return smooth_union(G, tails, 0.015)
    return [piece("scarf", "scarf", f, ["Head", "Chest"], tris=1400, allowed=["Head", "Chest"])]


def gasmask(h):
    def f(c):
        x, y, z = c.loc("Head")
        s = c.hum.h
        G = np.maximum(c.F - 0.01, np.maximum(np.maximum(y - 0.228 * s, 0.085 * s - y), 0.01 - z))
        straps = np.maximum(np.maximum(c.F - 0.006, np.minimum(np.abs(y - 0.215 * s) - 0.007, np.abs(y - 0.13 * s) - 0.007)), z - 0.03)
        return U(G, straps)
    def lens(c):
        s = c.hum.h
        return c.prims([P("Head", "cyl", c=(sx * 0.034, 0.198 * s, 0.105), r=0.02, halfh=0.006, round=0.002, rot=rot_y(sx * 14) @ rot_x(90))
                        for sx in (-1, 1)])
    def canister(c):
        s = c.hum.h
        return c.prims([P("Head", "cyl", c=(0, 0.125 * s, 0.128), r=0.03, halfh=0.024, round=0.006, rot=rot_x(70)),
                        P("Head", "cyl", c=(0, 0.13 * s, 0.108), r=0.022, halfh=0.012, round=0.004, rot=rot_x(70))])
    return [piece("gasmask", "rubber", f, ["Head"], extra=_hat_box(h, 0.15), tris=1600, allowed=["Head"], hides=False),
            piece("gasmask_lens", "glass", lens, ["Head"], extra=_hat_box(h, 0.15), res=FINE_H, tris=400, allowed=["Head"], hides=False, smooth=0),
            piece("gasmask_filter", "filter", canister, ["Head"], extra=_hat_box(h, 0.18), res=FINE_H, tris=600, allowed=["Head"], hides=False, sharp=50, smooth=0)]


def skull_mask(h):
    def f(c):
        x, y, z = c.loc("Head")
        s = c.hum.h
        G = np.maximum(c.F - 0.011, np.maximum(np.maximum(y - 0.262 * s, 0.075 * s - y), 0.015 - z))
        holes = c.prims([P("Head", "ell", c=(sx * 0.034, 0.196 * s, 0.11), rad=(0.02, 0.016, 0.03)) for sx in (-1, 1)]
                        + [P("Head", "ell", c=(0, 0.163 * s, 0.125), rad=(0.009, 0.013, 0.03))])
        G = np.maximum(G, -holes)
        teeth = np.maximum(np.abs(y - 0.128 * s) - 0.011, np.abs(np.abs(((x + 0.006) % 0.012) - 0.006)) - 0.0012)
        G = np.maximum(G, -np.maximum(np.maximum(teeth, 0.06 - z), np.abs(x) - 0.04))
        strap = np.maximum(np.maximum(c.F - 0.006, np.abs(y - 0.2 * s) - 0.008), z - 0.03)
        return U(G, strap)
    def back(c):
        s = c.hum.h
        x, y, z = c.loc("Head")
        return np.maximum(np.maximum(c.F - 0.004, np.maximum(np.maximum(y - 0.25 * s, 0.08 * s - y), 0.03 - z)), np.abs(x) - 0.07)
    return [piece("skull_mask", "bone", f, ["Head"], extra=_hat_box(h, 0.15), res=FINE_H, tris=2000, allowed=["Head"], hides=False),
            piece("skull_mask_back", "rubber", back, ["Head"], extra=_hat_box(h, 0.15), tris=600, allowed=["Head"], hides=False)]


# ---------------------------------------------------------------- armour (roadmap 12) and bags
def _vest_base(c, off):
    G = shell(c, off, {"Chest": (0, 1), "Pelvis": (0, 0.3), "Head": (0, 0.2)})
    G = neck_hole(c, G, 0.085, 0.0, 1.3)
    return arm_holes(c, G, 0.08, 0.04)


def vest_scrap(h):
    def rows(c, parity):
        x, y, z = c.loc("Chest")
        G = _vest_base(c, 0.03)
        row = np.floor((y + 0.06) / 0.085)
        sel = ((row + (x > 0)) % 2) == parity
        gaps = np.minimum(np.abs(((y + 0.06) % 0.085)) - 0.004, 0.081 - ((y + 0.06) % 0.085))
        G = np.maximum(G, -gaps)
        G = np.maximum(G, np.minimum(0.003 - np.abs(x), 0.0))
        G = np.maximum(G, y - 0.36 * c.hum.h)
        return np.where(sel, G, 1.0)
    def straps(c):
        x, y, z = c.loc("Chest")
        G = np.maximum(c.F - 0.028, np.abs(np.abs(x) - 0.075) - 0.017)
        return only(c, ["Chest"], np.maximum(G, 0.33 - y))
    def rivets(c):
        x, y, z = c.loc("Chest")
        yy = ((y + 0.06) % 0.085) - 0.012
        xx = np.abs(((x + 0.0175) % 0.035) - 0.0175)
        d = np.sqrt(yy * yy + xx * xx) - 0.0045
        G = np.maximum(d, np.abs(c.F - 0.032) - 0.004)
        G = np.maximum(G, _vest_base(c, 0.036))
        return np.maximum(G, y - 0.36 * c.hum.h)
    return [piece("vest_scrap_a", "plate", lambda c: rows(c, 0), TORSO, tris=2200, allowed=TORSO + ["UpperArmL", "UpperArmR"], sharp=40, smooth=1, sym=False),
            piece("vest_scrap_b", "plate_rust", lambda c: rows(c, 1), TORSO, tris=2200, allowed=TORSO + ["UpperArmL", "UpperArmR"], sharp=40, smooth=1, sym=False),
            piece("vest_scrap_straps", "strap", straps, ["Chest"], tris=600, allowed=["Chest"]),
            piece("vest_scrap_rivets", "chrome", rivets, TORSO, res=FINE_H, tris=1800, allowed=TORSO, smooth=0)]


def vest_kevlar(h):
    def f(c):
        G = _vest_base(c, 0.024)
        x, y, z = c.loc("Chest")
        G = np.maximum(G, y - 0.36 * c.hum.h)
        G = U(G, hem(c, "Pelvis", 0.25, 0.03, 0.012))
        return G
    def straps(c):
        x, y, z = c.loc("Chest")
        G = np.maximum(c.F - 0.024, np.abs(np.abs(x) - 0.075) - 0.02)
        return only(c, ["Chest"], np.maximum(G, 0.34 - y))
    def pouches(c):
        return c.prims([P("Chest", "box", c=(xx, 0.115, 0.13), half=(0.024, 0.036, 0.018), round=0.007) for xx in (-0.08, -0.028, 0.028, 0.08)]
                       + [P("Chest", "box", c=(xx, 0.155, 0.142), half=(0.025, 0.006, 0.02), round=0.003) for xx in (-0.08, -0.028, 0.028, 0.08)])
    return [piece("vest_kevlar", "kevlar", f, TORSO + ["Head"], tris=2400, allowed=TORSO + ["UpperArmL", "UpperArmR"]),
            piece("vest_kevlar_straps", "kevlar", straps, ["Chest"], tris=600, allowed=["Chest"]),
            piece("vest_kevlar_pouches", "pouch", pouches, ["Chest"], tris=1200, allowed=["Chest"], sharp=50, smooth=1)]


def shoulder(h):
    def plates(c):
        out = []
        for i in range(3):
            x, y, z = c.loc("UpperArmL")
            G = np.maximum(c.F - (0.03 + 0.008 * i), np.maximum(y - (0.05 - 0.035 * i), -0.055 - 0.04 * i - y))
            G = np.maximum(G, -(c.F - (0.022 + 0.008 * i)))
            out.append(only(c, ["UpperArmL", "Chest"], G))
        G = U(*out)
        xc, yc, zc = c.loc("Chest")
        return np.maximum(G, xc + 0.115 * c.hum.w)
    def strap(c):
        xc, yc, zc = c.loc("Chest")
        G = np.maximum(c.F - 0.014, np.abs(0.55 * (xc + 0.15) + 0.83 * (yc - 0.4)) - 0.016)
        return only(c, ["Chest"], np.maximum(G, xc - 0.13))
    return [piece("shoulder", "chrome", plates, ["UpperArmL", "Chest"], tris=1800, allowed=["UpperArmL", "Chest"], sym=False, sharp=40, smooth=1),
            piece("shoulder_strap", "strap", strap, ["Chest"], tris=700, allowed=["Chest"], sym=False)]


def arm_guards(h):
    def f(c):
        G = shell(c, 0.016, {"UpperArmL": (0.38, 0.92), "UpperArmR": (0.38, 0.92), "ForearmL": (0.06, 0.85), "ForearmR": (0.06, 0.85)})
        return G
    def studs(c):
        out = []
        for s in "LR":
            u, L = c.along("Forearm" + s)
            x, y, z = c.loc("Forearm" + s)
            a = np.arctan2(x, z)
            step = 2 * math.pi / 8
            da = np.abs(((a + step / 2) % step) - step / 2) * 0.04
            for uu in (0.06, 0.12, 0.18):
                d = np.sqrt((u - uu) ** 2 + da ** 2) - 0.005
                out.append(only(c, ["Forearm" + s], np.maximum(d, np.abs(c.F - 0.019) - 0.004)))
        return U(*out)
    return [piece("arm_guards", "leather_brown", f, ARMS, tris=1600, allowed=ARMS),
            piece("arm_guards_studs", "chrome", studs, ["ForearmL", "ForearmR"], res=FINE_H, tris=1200, allowed=["ForearmL", "ForearmR"], smooth=0)]


def shin_guards(h):
    def f(c):
        out = []
        for s in "LR":
            x, y, z = c.loc("Shin" + s)
            G = shell(c, 0.02, {"Shin" + s: (0.06, 0.78)})
            G = np.maximum(G, -0.012 - z)
            rows = np.minimum(np.abs((-y) % 0.075) - 0.003, 0.072 - ((-y) % 0.075))
            out.append(np.maximum(G, -rows))
        return U(*out)
    def knee(c):
        return c.prims([P("Shin" + s, "ell", c=(0, -0.005, 0.045), rad=(0.05, 0.05, 0.03), k=0.005) for s in "LR"])
    def straps(c):
        out = []
        for s in "LR":
            u, L = c.along("Shin" + s)
            G = np.maximum(c.F - 0.012, np.minimum(np.abs(u - 0.1) - 0.01, np.abs(u - 0.28) - 0.01))
            out.append(only(c, ["Shin" + s], G))
        return U(*out)
    return [piece("shin_guards", "plate", f, ["ShinL", "ShinR"], tris=1600, allowed=["ShinL", "ShinR", "ThighL", "ThighR"], sharp=40, smooth=1),
            piece("shin_guards_knee", "chrome", knee, ["ShinL", "ShinR"], tris=600, allowed=["ShinL", "ShinR"]),
            piece("shin_guards_straps", "strap", straps, ["ShinL", "ShinR"], tris=600, allowed=["ShinL", "ShinR"])]


def _pack_straps(c, off=0.016):
    x, y, z = c.loc("Chest")
    G = np.maximum(c.F - off, np.abs(np.abs(x) - 0.072) - 0.02)
    G = np.maximum(G, 0.14 - y + np.where(z < 0, 0.2, 0.0))
    return only(c, ["Chest"], G)


def backpack(h):
    def bag(c):
        return c.prims([P("Chest", "box", c=(0, 0.25, -0.175), half=(0.13, 0.165, 0.07), round=0.035, k=0.02),
                        P("Chest", "box", c=(0, 0.18, -0.25), half=(0.1, 0.08, 0.02), round=0.016, k=0.012),
                        P("Chest", "ell", c=(0, 0.41, -0.17), rad=(0.13, 0.04, 0.075), k=0.02)]
                       + [P("Chest", "box", c=(sx * 0.14, 0.17, -0.175), half=(0.02, 0.06, 0.04), round=0.012, k=0.01) for sx in (-1, 1)])
    def straps(c):
        return _pack_straps(c)
    def roll(c):
        return c.prims([P("Chest", "cyl", c=(0, 0.47, -0.17), r=0.048, halfh=0.15, round=0.02, rot=rot_z(90))])
    def buckles(c):
        return c.prims([P("Chest", "box", c=(sx * 0.072, 0.27, 0.12), half=(0.018, 0.012, 0.004), round=0.002, rot=rot_x(-18)) for sx in (-1, 1)]
                       + [P("Chest", "box", c=(0, 0.33, -0.25), half=(0.012, 0.016, 0.004), round=0.002)])
    return [piece("backpack", "backpack", bag, ["Chest"], extra=(np.array([-0.2, 1.0, -0.36]), np.array([0.2, 1.6, 0.0])), tris=1800, allowed=["Chest"], hides=False),
            piece("backpack_straps", "pack_dark", straps, ["Chest"], tris=800, allowed=["Chest"]),
            piece("backpack_roll", "bedroll", roll, ["Chest"], extra=(np.array([-0.24, 1.38, -0.3]), np.array([0.24, 1.6, -0.05])), tris=700, allowed=["Chest"], hides=False),
            piece("backpack_buckles", "chrome", buckles, ["Chest"], extra=(np.array([-0.1, 1.2, -0.3]), np.array([0.1, 1.4, 0.2])), res=FINE_H, tris=300, allowed=["Chest"], hides=False, smooth=0)]


def milpack(h):
    def bag(c):
        x, y, z = c.loc("Chest")
        B = c.prims([P("Chest", "box", c=(0, 0.24, -0.19), half=(0.14, 0.2, 0.085), round=0.03, k=0.02)]
                    + [P("Chest", "box", c=(sx * 0.16, 0.16, -0.19), half=(0.025, 0.08, 0.05), round=0.012, k=0.01) for sx in (-1, 1)])
        molle = np.abs(((y + 0.01) % 0.05) - 0.025) - 0.006
        webbing = np.maximum(np.maximum(B - 0.004, -B), np.maximum(molle, z + 0.27))
        return U(B, webbing)
    def pouch(c):
        return c.prims([P("Chest", "box", c=(0, 0.16, -0.285), half=(0.08, 0.06, 0.02), round=0.012)])
    return [piece("milpack", "milpack", bag, ["Chest"], extra=(np.array([-0.22, 1.0, -0.36]), np.array([0.22, 1.6, 0.0])), tris=2200, allowed=["Chest"], hides=False),
            piece("milpack_pouch", "bedroll", pouch, ["Chest"], extra=(np.array([-0.12, 1.1, -0.34]), np.array([0.12, 1.3, -0.2])), tris=400, allowed=["Chest"], hides=False),
            piece("milpack_straps", "milpack", lambda c: _pack_straps(c, 0.017), ["Chest"], tris=800, allowed=["Chest"])]


def toolbelt(h):
    def belt(c):
        x, y, z = c.loc("Pelvis")
        return only(c, ["Pelvis", "Chest", "ThighL", "ThighR"], np.maximum(c.F - 0.016, np.abs(y - 0.045 * c.hum.h) - 0.024))
    def pouches(c):
        w = c.hum.w
        return c.prims([P("Pelvis", "box", c=(0.155 * w, -0.02, 0.03), half=(0.03, 0.055, 0.05), round=0.012, rot=rot_y(-15), k=0.01),
                        P("Pelvis", "box", c=(-0.15 * w, -0.0, 0.05), half=(0.028, 0.04, 0.035), round=0.01, rot=rot_y(15), k=0.01),
                        P("Pelvis", "box", c=(0.12 * w, 0.0, -0.09), half=(0.03, 0.045, 0.03), round=0.01, rot=rot_y(-40), k=0.01)])
    def buckle(c):
        return c.prims([P("Pelvis", "box", c=(0, 0.045 * c.hum.h, 0.112), half=(0.026, 0.02, 0.005), round=0.003)])
    def tools(c):
        w = c.hum.w
        return c.prims([P("Pelvis", "cap", a=(-0.178 * w, 0.06, 0.0), b=(-0.185 * w, -0.17, 0.01), ra=0.009, rb=0.009),
                        P("Pelvis", "cap", a=(0.17 * w, 0.06, 0.005), b=(0.172 * w, 0.0, 0.0), ra=0.006, rb=0.006),
                        P("Pelvis", "cap", a=(0.165 * w, 0.06, 0.05), b=(0.168 * w, 0.0, 0.05), ra=0.006, rb=0.006)])
    def metal(c):
        w = c.hum.w
        return c.prims([P("Pelvis", "box", c=(-0.183 * w, -0.17, 0.01), half=(0.012, 0.012, 0.045), round=0.004),
                        P("Pelvis", "cap", a=(0.125 * w, 0.03, -0.11), b=(0.13 * w, 0.12, -0.12), ra=0.006, rb=0.006),
                        P("Pelvis", "tor", c=(0.13 * w, 0.13, -0.12), R=0.012, r=0.005, rot=rot_x(90) @ rot_z(0))])
    return [piece("toolbelt", "leather_brown", belt, ["Pelvis"], tris=900, allowed=["Pelvis", "Chest"]),
            piece("toolbelt_pouches", "belt", pouches, ["Pelvis"], extra=(np.array([-0.25, 0.8, -0.2]), np.array([0.25, 1.1, 0.15])), tris=900, allowed=["Pelvis"], hides=False, sym=False),
            piece("toolbelt_buckle", "chrome", buckle, ["Pelvis"], res=FINE_H, tris=200, allowed=["Pelvis"], hides=False, smooth=0),
            piece("toolbelt_handles", "wood", tools, ["Pelvis"], extra=(np.array([-0.25, 0.7, -0.15]), np.array([0.25, 1.1, 0.15])), res=FINE_H, tris=600, allowed=["Pelvis"], hides=False, sym=False, smooth=0),
            piece("toolbelt_metal", "chrome", metal, ["Pelvis"], extra=(np.array([-0.25, 0.7, -0.2]), np.array([0.25, 1.15, 0.15])), res=FINE_H, tris=600, allowed=["Pelvis"], hides=False, sym=False, smooth=0)]


GARMENTS = {
    "underwear": (underwear, "Base"), "tshirt": (tshirt, "Torso"), "tank": (tank, "Torso"), "hoodie": (hoodie, "Torso"),
    "sweater": (sweater, "Torso"), "jacket": (jacket, "Outer"), "bomber": (bomber, "Outer"), "duster": (duster, "Outer"),
    "vest": (vest, "Outer"), "pants": (pants, "Legs"), "jeans": (jeans, "Legs"), "shorts": (shorts, "Legs"),
    "overalls": (overalls, "Legs"), "boots": (boots, "Feet"), "combat_boots": (combat_boots, "Feet"),
    "gloves": (gloves, "Hands"), "fingerless": (fingerless, "Hands"), "cowboy": (cowboy, "Head"), "sunhat": (sunhat, "Head"),
    "beanie": (beanie, "Head"), "helmet": (helmet, "Head"), "goggles": (goggles, "Face"), "bandana": (bandana, "Face"),
    "shemagh": (shemagh, "Face"), "scarf": (scarf, "Face"), "gasmask": (gasmask, "Face"), "skull_mask": (skull_mask, "Face"),
    "vest_scrap": (vest_scrap, "Vest"), "vest_kevlar": (vest_kevlar, "Vest"), "shoulder": (shoulder, "Back"),
    "arm_guards": (arm_guards, "Arms"), "shin_guards": (shin_guards, "Shins"), "backpack": (backpack, "Pack"),
    "milpack": (milpack, "Pack"), "toolbelt": (toolbelt, "Belt"),
    # variants (same game ids, re-coloured / re-posed for an outfit)
    "goggles_up": (lambda h: goggles(h, True, "goggles_up"), "Face"),
    "labcoat": (lambda h: duster(h, "labcoat"), "Outer"),
    "tshirt_worn": (lambda h: tshirt(h, "tshirt_worn"), "Torso"),
}

HEADWEAR = {"cowboy": 0.236, "sunhat": 0.232, "beanie": 0.226, "helmet": 0.216, "shemagh": 0.0}

# ---------------------------------------------------------------- wave 2 (2026-10-02): every ClothingLibrary id
FULL_BODY = TORSO + ARMS + LEGS


def poncho(h):
    def f(c):
        xc, yc, zc = c.loc("Chest")
        # a bell from the shoulders: the offset grows with the drop below the collar, open below mid-thigh
        drop = np.clip((0.38 * c.hum.h - wy(c) + 1.2 * c.hum.h) * 0 + (0.4 - yc) / 0.55, 0, 1.6)
        off = 0.02 + 0.09 * drop
        G = shell(c, off, {"Chest": (0, 1), "Head": (0, 0.18), "Pelvis": (0, 1), "UpperArmL": (0, 1), "UpperArmR": (0, 1),
                           "ForearmL": (0, 0.55), "ForearmR": (0, 0.55), "ThighL": (0, 0.45), "ThighR": (0, 0.45)})
        G = neck_hole(c, G, 0.08, 0.02, 0.8)
        G = np.maximum(G, -(c.F - off + 0.006))                                           # a sheet, not a solid
        return G
    def hood(c):
        x, y, z = c.loc("Head")
        s = c.hum.h
        H_ = c.prims([P("Head", "ell", c=(0, 0.13 * s, -0.07), rad=(0.11, 0.1, 0.08), k=0.02)])
        return np.maximum(np.maximum(H_, -(H_ + 0.008)), y - 0.2 * s)
    return [piece("poncho", "poncho", f, TORSO + ["Head"] + ARMS + ["ThighL", "ThighR"], tris=4200, allowed=FULL_BODY + ["Head"]),
            piece("poncho_hood", "poncho", hood, ["Head"], extra=_hat_box(h, 0.16), tris=900, allowed=["Head"], hides=False)]


def _suit(c, off):
    return shell(c, off, {"Chest": (0, 1), "Pelvis": (0, 1), "UpperArmL": (0, 1), "UpperArmR": (0, 1), "ForearmL": (0, 1),
                          "ForearmR": (0, 1), "ThighL": (0, 1), "ThighR": (0, 1), "ShinL": (0, 1), "ShinR": (0, 1), "Head": (0, 0.2)})


def hazmat(h):
    def f(c):
        G = neck_hole(c, _suit(c, 0.016), 0.075, 0.02, 0.9)
        x, y, z = c.loc("Head")
        s = c.hum.h
        hood = np.maximum(c.F - 0.02, -0.02 - y)
        face = np.maximum(np.maximum(np.abs(y - 0.18 * s) - 0.07, 0.03 - z), np.abs(x) - 0.075)
        hood = np.maximum(only(c, ["Head"], hood), -face)
        return smooth_union(G, hood, 0.02)
    def trim(c):
        return U(hems(c, ["ForearmL", "ForearmR"], 0.95, 0.02, 0.016), hems(c, ["ShinL", "ShinR"], 0.95, 0.02, 0.018),
                 only(c, ["Pelvis"], np.maximum(c.F - 0.022, np.abs(c.loc("Pelvis")[1] - 0.045 * c.hum.h) - 0.016)))
    return [piece("hazmat", "hazmat", f, FULL_BODY + ["Head"], tris=5200, allowed=FULL_BODY + ["Head"]),
            piece("hazmat_trim", "hazmat_trim", trim, ["ForearmL", "ForearmR", "ShinL", "ShinR", "Pelvis"], tris=1200,
                  allowed=["ForearmL", "ForearmR", "ShinL", "ShinR", "Pelvis"])]


def dive_suit(h):
    def f(c):
        return neck_hole(c, _suit(c, 0.02), 0.09, 0.0, 0.6)
    def collar(c):
        return c.prims([P("Chest", "cyl", c=(0, 0.4 * c.hum.h, 0), r=0.15 * c.hum.w, halfh=0.02, round=0.008),
                        P("Chest", "cyl", c=(0, 0.4 * c.hum.h, 0), r=0.105, halfh=0.04, op="sub", k=0.004)])
    def cuffs(c):
        return U(hems(c, ["ForearmL", "ForearmR"], 0.96, 0.024, 0.016), hems(c, ["ShinL", "ShinR"], 0.95, 0.026, 0.02))
    return [piece("dive_suit", "dive_canvas", f, FULL_BODY + ["Head"], tris=5200, allowed=FULL_BODY + ["Head"]),
            piece("dive_suit_collar", "dive_brass", collar, ["Chest"], tris=1200, allowed=["Chest", "Head"], hides=False, sharp=50),
            piece("dive_suit_cuffs", "tyre", cuffs, ["ForearmL", "ForearmR", "ShinL", "ShinR"], tris=1000, allowed=["ForearmL", "ForearmR", "ShinL", "ShinR"])]


def dive_helmet(h):
    def dome(c):
        s = c.hum.h
        D = c.prims([P("Head", "sph", c=(0, 0.17 * s, 0.01), r=0.165)])
        D = np.maximum(D, -(D + 0.01))
        win = c.prims([P("Head", "cyl", c=(0, 0.18 * s, 0.16), r=0.065, halfh=0.05, rot=rot_x(90))]
                      + [P("Head", "cyl", c=(sx * 0.15, 0.18 * s, 0.03), r=0.045, halfh=0.05, rot=rot_z(90)) for sx in (-1, 1)])
        return np.maximum(D, -win)
    def rims(c):
        s = c.hum.h
        return c.prims([P("Head", "tor", c=(0, 0.18 * s, 0.168), R=0.068, r=0.011, rot=rot_x(90))]
                       + [P("Head", "tor", c=(sx * 0.158, 0.18 * s, 0.03), R=0.048, r=0.009, rot=rot_z(90)) for sx in (-1, 1)]
                       + [P("Head", "cyl", c=(0, 0.02 * s, 0.0), r=0.17, halfh=0.022, round=0.008)])
    def glass(c):
        s = c.hum.h
        return c.prims([P("Head", "cyl", c=(0, 0.18 * s, 0.155), r=0.064, halfh=0.003, rot=rot_x(90))]
                       + [P("Head", "cyl", c=(sx * 0.145, 0.18 * s, 0.03), r=0.044, halfh=0.003, rot=rot_z(90)) for sx in (-1, 1)])
    ex = _hat_box(h, 0.22)
    ex = (ex[0] - np.array([0, 0.08, 0]), ex[1])
    return [piece("dive_helmet", "dive_brass", dome, ["Head"], extra=ex, tris=2600, allowed=["Head", "Chest"], hides=False, sharp=60),
            piece("dive_helmet_rims", "dive_brass", rims, ["Head"], extra=ex, res=FINE_H, tris=1600, allowed=["Head", "Chest"], hides=False),
            piece("dive_helmet_glass", "glass", glass, ["Head"], extra=ex, res=FINE_H, tris=400, allowed=["Head"], hides=False, smooth=0)]


def air_tank(h):
    def tanks(c):
        return c.prims([P("Chest", "cyl", c=(sx * 0.062, 0.22, -0.2), r=0.058, halfh=0.2, round=0.05) for sx in (-1, 1)])
    def valves(c):
        return c.prims([P("Chest", "cyl", c=(sx * 0.062, 0.45, -0.2), r=0.018, halfh=0.03, round=0.006) for sx in (-1, 1)]
                       + [P("Chest", "cap", a=(-0.062, 0.48, -0.2), b=(0.062, 0.48, -0.2), ra=0.01, rb=0.01)])
    ex = (np.array([-0.2, 0.95, -0.34]), np.array([0.2, 1.62, 0.0]))
    return [piece("air_tank", "tank_yellow", tanks, ["Chest"], extra=ex, tris=1600, allowed=["Chest"], hides=False),
            piece("air_tank_valves", "chrome", valves, ["Chest"], extra=ex, res=FINE_H, tris=600, allowed=["Chest"], hides=False, smooth=0),
            piece("air_tank_straps", "strap", lambda c: _pack_straps(c, 0.016), ["Chest"], tris=800, allowed=["Chest"])]


def welding_mask(h):
    def f(c):
        x, y, z = c.loc("Head")
        s = c.hum.h
        G = np.maximum(c.F - 0.045, np.maximum(np.maximum(y - 0.3 * s, 0.05 * s - y), 0.02 - z))
        G = np.maximum(G, -(c.F - 0.035))
        strap = np.maximum(np.maximum(c.F - 0.008, np.abs(y - 0.27 * s) - 0.012), z - 0.03)
        return U(G, strap)
    def lens(c):
        s = c.hum.h
        return c.prims([P("Head", "box", c=(0, 0.2 * s, 0.148), half=(0.05, 0.018, 0.006), round=0.003)])
    ex = _hat_box(h, 0.16)
    return [piece("welding_mask", "weld", f, ["Head"], extra=ex, tris=1600, allowed=["Head"], hides=False, sharp=40),
            piece("welding_mask_lens", "weld_lens", lens, ["Head"], extra=ex, res=FINE_H, tris=200, allowed=["Head"], hides=False, smooth=0)]


def vest_chitin(h):
    def scales(c):
        x, y, z = c.loc("Chest")
        G = _vest_base(c, 0.026)
        row = (y + 0.06) % 0.07
        bulge = np.sqrt(np.maximum(0, 1 - ((row - 0.04) / 0.04) ** 2)) * 0.008
        G = np.maximum(G - bulge, -(c.F - 0.018))
        return np.maximum(G, y - 0.36 * c.hum.h)
    def ridge(c):
        x, y, z = c.loc("Chest")
        return only(c, ["Chest"], np.maximum(np.maximum(c.F - 0.04, np.abs(x) - 0.012), np.maximum(-z, y - 0.34 * c.hum.h)))
    return [piece("vest_chitin", "chitin", scales, TORSO, tris=2600, allowed=TORSO + ["UpperArmL", "UpperArmR"], smooth=1),
            piece("vest_chitin_ridge", "chitin", ridge, ["Chest"], tris=500, allowed=["Chest"])]


def vest_tyre(h):
    def f(c):
        x, y, z = c.loc("Chest")
        G = _vest_base(c, 0.03)
        groove = np.abs(((y + 0.01) % 0.045) - 0.0225) - 0.004
        tread = np.abs(((np.arctan2(x, z) * 0.12) % 0.02) - 0.01) - 0.002
        G = np.maximum(G, -np.maximum(np.maximum(groove, tread), -(c.F - 0.034)))
        return np.maximum(G, y - 0.36 * c.hum.h)
    return [piece("vest_tyre", "tyre", f, TORSO, tris=2800, allowed=TORSO + ["UpperArmL", "UpperArmR"], sharp=40, smooth=1),
            piece("vest_tyre_straps", "strap", lambda c: only(c, ["Chest"], np.maximum(np.maximum(c.F - 0.036, np.abs(np.abs(c.loc("Chest")[0]) - 0.075) - 0.017), 0.33 - c.loc("Chest")[1])), ["Chest"], tris=500, allowed=["Chest"])]


def gauntlets(h):
    def cuff(c):
        out = []
        for s_ in "LR":
            G = shell(c, 0.017, {"Forearm" + s_: (0.45, 1.0)})
            u, L = c.along("Forearm" + s_)
            ridges = np.abs(((u + 0.005) % 0.045) - 0.0225) - 0.003
            out.append(np.maximum(G, -np.maximum(ridges, -(c.F - 0.02))))
        return U(*out)
    return gloves(h, "gauntlets", "gloves") + [piece("gauntlets_cuffs", "plate", cuff, ["ForearmL", "ForearmR"], tris=1600, allowed=["ForearmL", "ForearmR", "HandL", "HandR"], sharp=40, smooth=1)]


def moto_helmet(h):
    def shell_(c):
        x, y, z = c.loc("Head")
        s = c.hum.h
        G = np.maximum(c.F - 0.03, 0.04 * s - y)
        visor = np.maximum(np.maximum(np.abs(y - 0.19 * s) - 0.04, 0.02 - z), np.abs(x) - 0.08)
        return np.maximum(np.maximum(G, -visor), -(c.F - 0.012))
    def visor(c):
        x, y, z = c.loc("Head")
        s = c.hum.h
        V = np.maximum(c.F - 0.034, np.maximum(np.maximum(np.abs(y - 0.19 * s) - 0.046, 0.0 - z), np.abs(x) - 0.088))
        return np.maximum(V, -(c.F - 0.03))
    ex = _hat_box(h, 0.17)
    ex = (ex[0] - np.array([0, 0.06, 0]), ex[1])
    return [piece("moto_helmet", "moto", shell_, ["Head"], extra=ex, tris=2200, allowed=["Head"], hides=False),
            piece("moto_helmet_visor", "glass", visor, ["Head"], extra=ex, tris=600, allowed=["Head"], hides=False, smooth=0)]


def bee_veil(h):
    def veil(c):
        x, y, z = c.loc("Head")
        s = c.hum.h
        r = np.sqrt(x * x + (z + 0.005) ** 2)
        V = np.maximum(np.abs(r - 0.2) - 0.0025, np.maximum(y - 0.235 * s, -0.06 - y))
        return V
    hat = sunhat(h)
    hat = [dict(pc, name=pc["name"].replace("sunhat", "bee_veil")) for pc in hat]
    return hat + [piece("bee_veil_mesh", "veil", veil, ["Head"], extra=_hat_box(h, 0.24), tris=1600, allowed=["Head", "Chest"], hides=False, smooth=0)]


def leather_boots(h):
    return boots(h, "leather_boots", "leather_brown", 0.78)


def leather_cuirass(h):
    def f(c):
        G = _vest_base(c, 0.022)
        x, y, z = c.loc("Chest")
        G = np.maximum(G, y - 0.37 * c.hum.h)
        return U(G, hem(c, "Pelvis", 0.3, 0.026, 0.01))
    def laces(c):
        x, y, z = c.loc("Chest")
        cross_ = np.abs(np.abs(x) - 0.012 * (1 + np.cos((y % 0.04) / 0.04 * 6.283))) - 0.002
        return only(c, ["Chest"], np.maximum(np.maximum(c.F - 0.026, cross_), np.maximum(-z, np.abs(y - 0.18) - 0.15)))
    return [piece("leather_cuirass", "leather_brown", f, TORSO + ["Head"], tris=2400, allowed=TORSO + ["UpperArmL", "UpperArmR"], sharp=40),
            piece("leather_cuirass_laces", "strap", laces, ["Chest"], res=FINE_H, tris=800, allowed=["Chest"], smooth=0)]


def leather_chaps(h):
    def f(c):
        out = []
        for s_ in "LR":
            G = shell(c, 0.014, {"Thigh" + s_: (0.05, 1), "Shin" + s_: (0, 0.9)})
            x, y, z = c.loc("Thigh" + s_)
            out.append(G)
        return U(*out)
    def fringe(c):
        out = []
        for s_ in "LR":
            x, y, z = c.loc("Shin" + s_)
            side = np.abs(x) - 0.05
            out.append(only(c, ["Shin" + s_, "Thigh" + s_], np.maximum(np.maximum(c.F - 0.03, np.abs(side) - 0.01), -(c.F - 0.012))))
        return U(*out)
    return [piece("leather_chaps", "leather_brown", f, LEGS, tris=2400, allowed=LEGS + ["Pelvis"]),
            piece("leather_chaps_fringe", "leather_brown", fringe, LEGS, tris=1200, allowed=LEGS)]


def _belt_band(c, off=0.016, w=0.024):
    x, y, z = c.loc("Pelvis")
    return only(c, ["Pelvis", "Chest", "ThighL", "ThighR"], np.maximum(c.F - off, np.abs(y - 0.045 * c.hum.h) - w))


def work_belt(h):
    pcs = toolbelt(h)
    return [dict(pc, name=pc["name"].replace("toolbelt", "work_belt")) for pc in pcs if "handles" not in pc["name"]]


def gun_belt(h):
    def holster(c):
        w = c.hum.w
        return c.prims([P("ThighR", "box", c=(0.075, -0.07, 0.0), half=(0.022, 0.085, 0.045), round=0.012, rot=rot_x(-8))])
    def gun(c):
        return c.prims([P("ThighR", "box", c=(0.077, 0.02, -0.005), half=(0.014, 0.04, 0.016), round=0.005, rot=rot_x(-25)),
                        P("ThighR", "cyl", c=(0.077, -0.015, 0.02), r=0.012, halfh=0.012, round=0.003, rot=rot_z(90))])
    ex = (np.array([0.02, 0.55, -0.15]), np.array([0.32, 1.1, 0.18]))
    return [piece("gun_belt", "leather_brown", _belt_band, ["Pelvis"], tris=900, allowed=["Pelvis", "Chest"]),
            piece("gun_belt_holster", "belt", holster, ["ThighR"], extra=ex, tris=600, allowed=["ThighR", "Pelvis"], hides=False, sym=False),
            piece("gun_belt_pistol", "gunmetal", gun, ["ThighR"], extra=ex, res=FINE_H, tris=500, allowed=["ThighR", "Pelvis"], hides=False, sym=False, smooth=0)]


def fanny_bag(h):
    def bag(c):
        return c.prims([P("Pelvis", "box", c=(0, 0.035 * c.hum.h, 0.13), half=(0.085, 0.045, 0.03), round=0.022)])
    return [piece("fanny_bag", "black_pack", lambda c: _belt_band(c, 0.012, 0.012), ["Pelvis"], tris=700, allowed=["Pelvis", "Chest"]),
            piece("fanny_bag_pouch", "black_pack", bag, ["Pelvis"], extra=(np.array([-0.15, 0.85, 0.0]), np.array([0.15, 1.15, 0.22])), tris=700, allowed=["Pelvis"], hides=False)]


def back_brace(h):
    def f(c):
        return U(_belt_band(c, 0.012, 0.05), only(c, ["Pelvis", "Chest"], np.maximum(np.maximum(c.F - 0.016, np.abs(c.loc("Pelvis")[1] - 0.06 * c.hum.h) - 0.02), c.loc("Pelvis")[2] + 0.02)))
    return [piece("back_brace", "black_pack", f, ["Pelvis"], tris=900, allowed=["Pelvis", "Chest"])]


def _pack(h, name, mat, w, hgt, d, y0=0.24, frame=False, roll=None):
    def bag(c):
        return c.prims([P("Chest", "box", c=(0, y0, -0.12 - d), half=(w, hgt, d), round=min(0.035, d * 0.7), k=0.02),
                        P("Chest", "box", c=(0, y0 - hgt * 0.45, -0.12 - 2 * d), half=(w * 0.75, hgt * 0.35, 0.02), round=0.015, k=0.012),
                        P("Chest", "ell", c=(0, y0 + hgt, -0.12 - d), rad=(w, 0.035, d * 1.05), k=0.02)])
    ex = (np.array([-w - 0.08, 0.9, -0.2 - 2.6 * d]), np.array([w + 0.08, 1.75, 0.0]))
    out = [piece(name, mat, bag, ["Chest"], extra=ex, tris=1800, allowed=["Chest"], hides=False),
           piece(name + "_straps", "strap" if mat != "black_pack" else "pack_dark", lambda c: _pack_straps(c, 0.016), ["Chest"], tris=800, allowed=["Chest"])]
    if frame:
        def tubes(c):
            return c.prims([P("Chest", "cap", a=(sx * (w + 0.015), y0 - hgt - 0.05, -0.1), b=(sx * (w + 0.015), y0 + hgt + 0.12, -0.12), ra=0.011, rb=0.011) for sx in (-1, 1)]
                           + [P("Chest", "cap", a=(-(w + 0.015), y0 + hgt + 0.12, -0.12), b=(w + 0.015, y0 + hgt + 0.12, -0.12), ra=0.011, rb=0.011),
                              P("Chest", "cap", a=(-(w + 0.015), y0 - hgt - 0.05, -0.1), b=(w + 0.015, y0 - hgt - 0.05, -0.1), ra=0.011, rb=0.011)])
        out.append(piece(name + "_frame", "chrome", tubes, ["Chest"], extra=(ex[0] - 0.05, ex[1] + 0.1), res=FINE_H, tris=900, allowed=["Chest"], hides=False, smooth=0))
    if roll:
        out.append(piece(name + "_roll", roll, lambda c: c.prims([P("Chest", "cyl", c=(0, y0 + hgt + 0.06, -0.12 - d), r=0.045, halfh=w + 0.02, round=0.02, rot=rot_z(90))]),
                         ["Chest"], extra=(ex[0], ex[1] + 0.12), tris=700, allowed=["Chest"], hides=False))
    return out


def schoolbag(h):
    return _pack(h, "schoolbag", "navy_pack", 0.12, 0.14, 0.055)


def hikingpack(h):
    return _pack(h, "hikingpack", "black_pack", 0.14, 0.22, 0.08, 0.22, roll="red_handle")


def framepack(h):
    return _pack(h, "framepack", "olive_pack", 0.15, 0.24, 0.09, 0.2, frame=True, roll="bedroll")


def craftpack(h):
    return _pack(h, "craftpack", "sand_pack", 0.13, 0.17, 0.07)


def leather_satchel(h):
    return _pack(h, "leather_satchel", "leather_brown", 0.12, 0.11, 0.045, 0.27)


def _shoulder_bag(h, name, mat, back=False, w=0.12, hh=0.09, d=0.04):
    def strap(c):
        xc, yc, zc = c.loc("Chest")
        G = np.maximum(c.F - 0.014, np.abs(0.55 * (xc + 0.15) + 0.83 * (yc - 0.4)) - 0.016)
        return only(c, ["Chest", "Pelvis"], G)
    def bag(c):
        if back:
            return c.prims([P("Chest", "box", c=(0.04, 0.16, -0.14), half=(w, hh, d), round=0.03, rot=rot_z(-35), k=0.02)])
        return c.prims([P("Pelvis", "box", c=(0.17 * c.hum.w, 0.0, 0.02), half=(d, hh, w), round=0.025, rot=rot_y(-10), k=0.02)])
    ex = (np.array([-0.3, 0.75, -0.3]), np.array([0.32, 1.35, 0.25]))
    return [piece(name + "_strap", mat, strap, ["Chest"], tris=800, allowed=["Chest", "Pelvis"], sym=False),
            piece(name, mat, bag, ["Chest"] if back else ["Pelvis"], extra=ex, tris=1200, allowed=["Chest", "Pelvis", "ThighR"], hides=False, sym=False)]


def shoulder_bag(h):
    return _shoulder_bag(h, "shoulder_bag", "navy_pack")


def sling_bag(h):
    return _shoulder_bag(h, "sling_bag", "black_pack", back=True, w=0.1, hh=0.13, d=0.045)


def canvas_satchel(h):
    return _shoulder_bag(h, "canvas_satchel", "sand_pack", w=0.13, hh=0.1, d=0.04)


def _hand_bag(h, name, mat, prims_fn):
    ex = (np.array([0.0, 0.25, -0.4]), np.array([0.45, 1.0, 0.4]))
    return [piece(name, mat, lambda c: c.prims(prims_fn(c)), ["HandR"], extra=ex, tris=1600, allowed=["HandR", "ForearmR"], hides=False, sym=False)]


def duffel(h):
    return _hand_bag(h, "duffel", "olive_pack", lambda c: [P("HandR", "cyl", c=(0, -0.21, 0), r=0.12, halfh=0.26, round=0.08, rot=rot_x(90), k=0.02),
                                                          P("HandR", "tor", c=(0, -0.06, 0), R=0.045, r=0.01, rot=rot_x(90))])


def suitcase(h):
    return _hand_bag(h, "suitcase", "suitcase", lambda c: [P("HandR", "box", c=(0, -0.26, 0), half=(0.055, 0.18, 0.25), round=0.015, k=0.01),
                                                           P("HandR", "tor", c=(0, -0.07, 0), R=0.04, r=0.01, rot=rot_x(90))])


GARMENTS.update({
    "poncho": (poncho, "Outer"), "hazmat": (hazmat, "Outer"), "dive_suit": (dive_suit, "Outer"), "dive_helmet": (dive_helmet, "Head"),
    "air_tank": (air_tank, "Pack"), "welding_mask": (welding_mask, "Face"), "vest_chitin": (vest_chitin, "Vest"), "vest_tyre": (vest_tyre, "Vest"),
    "gauntlets": (gauntlets, "Hands"), "moto_helmet": (moto_helmet, "Head"), "bee_veil": (bee_veil, "Head"), "leather_boots": (leather_boots, "Feet"),
    "leather_cuirass": (leather_cuirass, "Vest"), "leather_chaps": (leather_chaps, "Shins"), "work_belt": (work_belt, "Belt"), "gun_belt": (gun_belt, "Belt"),
    "fanny_bag": (fanny_bag, "Belt"), "back_brace": (back_brace, "Belt"), "schoolbag": (schoolbag, "Pack"), "hikingpack": (hikingpack, "Pack"),
    "framepack": (framepack, "Pack"), "craftpack": (craftpack, "Pack"), "leather_satchel": (leather_satchel, "Pack"),
    "shoulder_bag": (shoulder_bag, "Shoulder"), "sling_bag": (sling_bag, "Shoulder"), "canvas_satchel": (canvas_satchel, "Shoulder"),
    "duffel": (duffel, "Hand"), "suitcase": (suitcase, "Hand"),
})
HEADWEAR.update({"dive_helmet": 0.0, "moto_helmet": 0.0, "bee_veil": 0.232})

# HD_GARMENTS=id,id: build only these (previews of new garments)
import os as _os
if _os.environ.get("HD_GARMENTS"):
    _keep = set(_os.environ["HD_GARMENTS"].split(","))
    GARMENTS = {k: v for k, v in GARMENTS.items() if k in _keep}

