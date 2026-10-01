"""HD world / held versions of every garment and bag (ClothingLibrary: item id cloth_<def id>). The worn meshes live in
tools/blender/hd/character/garments.py (same colours); these are the models for a garment lying on the ground, in a
container or on a trade table: folded clothes, pairs of boots / gloves / guards, hats, masks, armour vests, and bags
standing with their straps. Bags (BagLibrary) carry a "grip" marker (where a hand holds them: the top handle or the
carry strap) so the same model works as the held version for hand luggage (duffel, suitcase). Real size; origin =
resting point (bottom centre).

blender -b -P tools/blender/hd/items/clothing.py [-- ids]"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "parts_all"))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pa  # noqa: E402
from pa import M, Part, Vector, Matrix, kit  # noqa: E402
import ishapes as I  # noqa: E402
from ishapes import col  # noqa: E402

F = "clothing"
CL = {}   # cloth id -> material, from character/garments.py MATS


def cloth(hexc, kind="cloth"):
    key = (hexc, kind)
    if key not in CL:
        if kind == "denim":
            CL[key] = kit.textured("Denim_" + hexc, "canvas", kit._hex(I.shade(hexc, 0.85)), kit._hex(hexc), dust=0.2, rough=0.95, bump=0.3)
        elif kind == "knit":
            CL[key] = kit.textured("Knit_" + hexc, "canvas", kit._hex(I.shade(hexc, 0.8)), kit._hex(hexc), dust=0.15, rough=0.98, bump=0.6, scale=0.5)
        elif kind == "leather":
            CL[key] = kit.surface("Leather_" + hexc, kit._hex(hexc), dust=0.2, rough=0.5, var=0.25, bump=0.3)
        elif kind == "metal":
            CL[key] = kit.surface("Metal_" + hexc, kit._hex(hexc), rust=0.3, dust=0.3, metal=0.7, rough=0.5, var=0.2)
        elif kind == "rubber":
            CL[key] = kit.surface("RubberC_" + hexc, kit._hex(hexc), dust=0.3, rough=0.85, var=0.1, bump=0.4)
        else:
            CL[key] = kit.textured("Cloth_" + hexc, "canvas", kit._hex(I.shade(hexc, 0.88)), kit._hex(hexc), dust=0.25, rough=0.95, bump=0.25)
    return CL[key]


# ------------------------------------------------------------------------------------------------ folded clothes
def folded_top(n, m, w=0.32, d=0.26, h=0.07, collar=True, hood=False, zip_=False, buttons=False, trim=None):
    p = Part(n)
    for k in range(3):                                           # folded layers
        p.box(m, (w - k * 0.004, d - k * 0.006, h / 3), (0, k * 0.002, h / 6 + k * h / 3), bevel=h / 7)
    if collar:
        p.add(kit.bm_torus(0.06, 0.01, 18, 6), m, (0, -d / 2 + 0.07, h + 0.004), (0, 0, 0), scale=None)
    if hood:
        p.add(kit.bm_rock(0.08, 2, 0.4, 2), m, (0, -d / 2 + 0.08, h + 0.01))
    if zip_:
        p.box(M["chrome"], (0.008, d * 0.9, 0.003), (0, 0, h + 0.002), bevel=0)
    if buttons:
        for k in range(4):
            p.cyl(M["black"], 0.007, 0.004, (0, -d * 0.3 + k * d * 0.2, h + 0.003), "Z", 8)
    if trim:
        p.box(trim, (w + 0.002, 0.03, h + 0.002), (0, d / 2 - 0.015, h / 2), bevel=0.01)
    for s in (-1, 1):                                            # sleeves folded over
        p.box(m, (0.07, d * 0.8, 0.02), (s * (w / 2 - 0.05), 0.0, h + 0.006), bevel=0.008)
    return p.build()


def folded_trousers(n, m, w=0.24, d=0.34, h=0.06, shorts=False, bib=False, pockets=True):
    p = Part(n)
    d = d * (0.6 if shorts else 1.0)
    for k in range(2):
        p.box(m, (w, d, h / 2), (0, 0, h / 4 + k * h / 2), bevel=h / 6)
    p.box(cloth("3a2414", "leather"), (w + 0.004, 0.035, 0.012), (0, -d / 2 + 0.02, h + 0.002), bevel=0.003)   # belt loop band
    p.box(M["brass"], (0.03, 0.02, 0.006), (0, -d / 2 + 0.02, h + 0.008), bevel=0.002)
    if pockets:
        for s in (-1, 1):
            p.box(m, (0.08, 0.09, 0.006), (s * 0.06, 0.05, h + 0.003), bevel=0.002)
    if bib:
        p.box(m, (0.2, 0.14, 0.012), (0, -d / 2 - 0.04, h * 0.5), (0.4, 0, 0), bevel=0.004)
        for s in (-1, 1):
            p.box(M["brass"], (0.02, 0.02, 0.006), (s * 0.07, -d / 2 - 0.08, h * 0.7), bevel=0.002)
    return p.build()


def folded_cloth(n, m, w=0.2, d=0.16, h=0.03, fringe=False, pattern=None):
    p = Part(n)
    p.box(m, (w, d, h), (0, 0, h / 2), bevel=h / 4)
    p.add(kit.bm_box(w * 0.5, d * 0.5, h * 0.6, bevel=h / 5), m, (w * 0.2, d * 0.15, h * 1.2), (0, 0, 0.6))
    if pattern:
        for k in range(3):
            p.box(pattern, (w + 0.002, 0.012, h + 0.002), (0, -d / 2 + 0.03 + k * 0.05, h / 2), bevel=0)
    if fringe:
        for k in range(10):
            pa.rod(p, m, (-w / 2 + 0.01 + k * w / 10, d / 2, 0.006), (-w / 2 + 0.01 + k * w / 10, d / 2 + 0.03, 0.004), 0.002, 4)
    return p.build()


# ------------------------------------------------------------------------------------------------ pairs
def boots(n, upper, sole, h=0.24, laces=True, toe_cap=None):
    p = Part(n)
    for s in (-1, 1):
        x = s * 0.07
        p.add(kit.superloft([(y, x, 0.04, 0.045, 0.04) for y in (-0.14, -0.06, 0.05, 0.12)], 16, 2.4), upper)
        p.add(kit.superloft([(y, x, 0.04 + h / 2, 0.045, h / 2 - 0.02) for y in (0.04, 0.12)], 16, 2.4), upper)
        p.box(sole, (0.095, 0.29, 0.025), (x, -0.01, 0.0125), bevel=0.01)
        if laces:
            for k in range(5):
                p.box(M["black"], (0.06, 0.006, 0.004), (x, -0.04 + k * 0.03, 0.08 + k * 0.025), (0.5, 0, 0), bevel=0)
        if toe_cap is not None:
            p.add(kit.bm_sphere(0.047, 14, 8), toe_cap, (x, -0.13, 0.04), scale=(1, 0.9, 0.7))
    return p.build()


def gloves(n, m, fingerless=False, gauntlet=False):
    p = Part(n)
    for s in (-1, 1):
        x = s * 0.07
        p.box(m, (0.09, 0.12, 0.025), (x, 0.0, 0.0125), bevel=0.01)
        for k in range(4):
            L = 0.05 if fingerless else 0.08
            pa.rod(p, m, (x - 0.03 + k * 0.02, -0.06, 0.012), (x - 0.03 + k * 0.02 - 0.004, -0.06 - L, 0.01), 0.009, 6)
        pa.rod(p, m, (x + s * 0.045, -0.02, 0.012), (x + s * 0.075, -0.06, 0.012), 0.01, 6)
        cuff = 0.12 if gauntlet else 0.04
        p.box(cloth("4f4842", "metal") if gauntlet else m, (0.1, cuff, 0.03), (x, 0.06 + cuff / 2, 0.015), bevel=0.008)
    return p.build()


def guards(n, m, strap, L=0.36, arm=False):
    p = Part(n)
    for s in (-1, 1):
        x = s * 0.09
        bm = kit.bm_cyl(0.06 if not arm else 0.05, L, 16, 0.006)
        for v in bm.verts:
            v.co.y = max(v.co.y, -0.01)
        p.add(bm, m, (x, 0, 0.0), (math.pi / 2, 0, 0))
        for y in (-L * 0.3, L * 0.3):
            p.box(strap, (0.13, 0.025, 0.012), (x, y, 0.005), bevel=0.003)
    return p.build()


# ------------------------------------------------------------------------------------------------ hats, masks
def hat(n, kind, m, band=None):
    p = Part(n)
    if kind == "beanie":
        p.lathe(m, [(0.0, 0.16), (0.05, 0.155), (0.09, 0.12), (0.1, 0.06), (0.1, 0.0), (0.0, 0.0)], (0, 0, 0), "Z", 20)
        p.cyl(m, 0.103, 0.04, (0, 0, 0.02), "Z", 20, bevel=0.01)
        p.sphere(m, 0.03, (0, 0, 0.17), 10, 6)
    elif kind == "sunhat":
        p.lathe(m, [(0.0, 0.11), (0.08, 0.105), (0.09, 0.05), (0.18, 0.012), (0.19, 0.0), (0.0, 0.0)], (0, 0, 0), "Z", 24)
        if band:
            p.cyl(band, 0.091, 0.02, (0, 0, 0.06), "Z", 24, bevel=0, caps=False)
    elif kind == "cowboy":
        p.lathe(m, [(0.0, 0.14), (0.05, 0.12), (0.08, 0.14), (0.085, 0.05), (0.16, 0.03), (0.2, 0.06), (0.2, 0.05), (0.15, 0.015), (0.0, 0.0)], (0, 0, 0), "Z", 24)
        p.cyl(band or M["black"], 0.087, 0.02, (0, 0, 0.065), "Z", 24, bevel=0, caps=False)
    elif kind == "helmet":
        bm = kit.bm_sphere(0.13, 20, 12)
        for v in bm.verts:
            v.co.z = max(v.co.z, 0.0)
        p.add(bm, m, (0, 0, 0.0))
        for k in range(8):
            a = k / 8 * math.tau
            p.cyl(M["chrome"], 0.008, 0.008, (math.cos(a) * 0.125, math.sin(a) * 0.125, 0.03), "Z", 6)
        p.box(M["rust"], (0.02, 0.26, 0.11), (0, 0, 0.07), bevel=0.005)
    elif kind == "moto":
        bm = kit.bm_sphere(0.14, 20, 14)
        for v in bm.verts:
            v.co.z = max(v.co.z, -0.04) + 0.04
        p.add(bm, m, (0, 0, 0.0))
        p.box(M["glass"], (0.16, 0.02, 0.07), (0, -0.13, 0.1), (0.2, 0, 0), bevel=0.01)
    elif kind == "dive":
        p.lathe(M["brass"], [(0.0, 0.0), (0.18, 0.0), (0.18, 0.04), (0.14, 0.06), (0.16, 0.2), (0.14, 0.32), (0.0, 0.36)], (0, 0, 0), "Z", 24)
        for a, r in ((-math.pi / 2, 0.06), (0.0, 0.04), (math.pi, 0.04)):
            d = Vector((math.cos(a), math.sin(a), 0))
            pa.ring(p, M["brass"], Vector((0, 0, 0.2)) + d * 0.155, r, 0.012, "Y" if abs(d.y) > 0.5 else "X", 16, 5)
            pa.lens(p, M["glass"], Vector((0, 0, 0.2)) + d * 0.16, r * 0.9, "-Y" if d.y < -0.5 else ("X" if d.x > 0.5 else "-X"), 0.006)
        for k in range(8):
            a = k / 8 * math.tau
            p.cyl(M["chrome"], 0.01, 0.02, (math.cos(a) * 0.17, math.sin(a) * 0.17, 0.05), "Z", 6)
    elif kind == "veil":
        p.lathe(m, [(0.0, 0.1), (0.08, 0.1), (0.09, 0.06), (0.17, 0.04), (0.17, 0.03), (0.0, 0.03)], (0, 0, 0), "Z", 24)
        p.lathe(cloth("232127"), [(0.17, 0.03), (0.17, 0.0), (0.16, 0.0)], (0, 0, 0), "Z", 24)
    return p.build()


def goggles(n):
    p = Part(n)
    for s in (-1, 1):
        p.cyl(M["black"], 0.03, 0.03, (s * 0.04, 0, 0.03), "Y", 16, bevel=0.006)
        pa.lens(p, kit.glass("GogglesAmber", tint=kit._hex("d08a30")), (s * 0.04, -0.016, 0.03), 0.026, "-Y", 0.004)
    p.add(kit.bm_tube([(-0.07, 0, 0.03), (-0.1, 0.06, 0.02), (0, 0.1, 0.015), (0.1, 0.06, 0.02), (0.07, 0, 0.03)], 0.008, 6), M["webbing"])
    return p.build()


def gasmask(n):
    p = Part(n)
    bm = kit.bm_sphere(0.09, 18, 12)
    for v in bm.verts:
        v.co.y = min(v.co.y, 0.02)
    p.add(bm, cloth("232127", "rubber"), (0, 0, 0.09))
    for s in (-1, 1):
        pa.lens(p, M["glass"], (s * 0.035, -0.08, 0.11), 0.025, "-Y", 0.004)
    p.cyl(cloth("6b3e24"), 0.04, 0.05, (0, -0.1, 0.05), "Y", 16, bevel=0.006)
    return p.build()


def mask_plate(n, m, eyes=True, welding=False):
    p = Part(n)
    bm = kit.bm_sphere(0.1, 16, 10)
    for v in bm.verts:
        v.co.y = min(v.co.y, 0.0) * 0.6
    p.add(bm, m, (0, 0, 0.1))
    if welding:
        p.box(M["glass"], (0.1, 0.01, 0.035), (0, -0.065, 0.12), bevel=0.003)
    elif eyes:
        for s in (-1, 1):
            p.sphere(M["black"], 0.018, (s * 0.035, -0.055, 0.12), 10, 6)
        for k in range(5):
            p.box(M["black"], (0.006, 0.004, 0.02), (-0.024 + k * 0.012, -0.058, 0.06), bevel=0)
    p.add(kit.bm_tube([(-0.09, 0, 0.1), (0, 0.06, 0.1), (0.09, 0, 0.1)], 0.006, 6), M["webbing"])
    return p.build()


def armour_vest(n, m, kind):
    p = Part(n)
    p.box(m, (0.4, 0.5, 0.05), (0, 0, 0.025), bevel=0.02)
    if kind == "scrap":
        for k in range(6):
            p.box(cloth("80401d", "metal") if k % 2 else cloth("4f4842", "metal"), (0.16, 0.12, 0.012), (-0.09 + (k % 2) * 0.18, -0.16 + (k // 2) * 0.15, 0.058), (0, 0, 0.1 * (k - 3)), bevel=0.004)
    elif kind == "chitin":
        for k in range(5):
            bm = kit.bm_sphere(0.14, 14, 8)
            for v in bm.verts:
                v.co.z = max(v.co.z, 0.0) * 0.25
            p.add(bm, col("2a3832", 0.25, 0.5), (0, -0.18 + k * 0.09, 0.05), scale=(1.3, 0.5, 1))
    elif kind == "tyre":
        for k in range(4):
            p.box(M["tread"], (0.38, 0.11, 0.02), (0, -0.18 + k * 0.12, 0.06), bevel=0.006)
    elif kind == "kevlar":
        p.box(m, (0.36, 0.4, 0.03), (0, 0.0, 0.065), bevel=0.012)
        for k in range(3):
            p.box(cloth("18244a"), (0.08, 0.06, 0.03), (-0.1 + k * 0.1, -0.12, 0.09), bevel=0.008)
    elif kind == "leather":
        for k in range(4):
            pa.rod(p, M["brass"], (-0.16, -0.18 + k * 0.12, 0.052), (0.16, -0.18 + k * 0.12, 0.052), 0.004, 4)
    for s in (-1, 1):
        p.box(M["webbing"], (0.05, 0.2, 0.012), (s * 0.12, 0.3, 0.02), bevel=0.003)
    return p.build()


def pauldron(n, m):
    p = Part(n)
    for k in range(3):
        bm = kit.bm_sphere(0.14 - k * 0.015, 16, 8)
        for v in bm.verts:
            v.co.z = max(v.co.z, 0.0) * 0.5
        p.add(bm, m, (0, -0.06 + k * 0.06, 0.0 + k * 0.012))
    p.box(M["webbing"], (0.04, 0.3, 0.01), (0, 0.0, 0.08), bevel=0.003)
    return p.build()


# ------------------------------------------------------------------------------------------------ bags
def backpack(n, m, w=0.3, d=0.18, h=0.46, roll=None, frame=False, molle=False, pockets=True, craft=False):
    p = Part(n)
    p.add(kit.superloft([(y, 0, h / 2, w / 2, h / 2) for y in (-d / 2, -d * 0.3, d * 0.3, d / 2)], 20, 3.2), m)
    p.add(kit.superloft([(y, 0, h - 0.02, w / 2 + 0.005, 0.05) for y in (-d / 2 - 0.02, d / 2 * 0.6)], 18, 2.8), m)   # flap
    if pockets:
        p.add(kit.superloft([(y, 0, h * 0.32, w * 0.32, h * 0.18) for y in (-d / 2 - 0.05, -d / 2)], 16, 3.0), m)
        for s in (-1, 1):
            p.add(kit.superloft([(y, s * (w / 2 + 0.02), h * 0.35, 0.03, h * 0.16) for y in (-d * 0.3, d * 0.3)], 12, 3.0), m)
    for s in (-1, 1):                                             # shoulder straps on the back
        p.add(kit.bm_tube([(s * 0.07, d / 2, h * 0.92), (s * 0.09, d / 2 + 0.05, h * 0.6), (s * 0.1, d / 2 + 0.03, h * 0.12)], 0.016, 6), M["webbing"])
        p.box(M["chrome"], (0.03, 0.006, 0.02), (s * 0.07, -d / 2 - 0.022, h * 0.82), bevel=0.002)
    if roll:
        p.cyl(roll, 0.06, w * 1.05, (0, 0, h + 0.05), "X", 16, bevel=0.02)
        for s in (-1, 1):
            pa.ring(p, M["webbing"], (s * w * 0.3, 0, h + 0.05), 0.062, 0.006, "X", 14, 4)
    if frame:
        for s in (-1, 1):
            pa.rod(p, M["alu"], (s * w * 0.45, d / 2 + 0.03, 0.0), (s * w * 0.45, d / 2 + 0.03, h * 1.3), 0.012, 8)
        pa.rod(p, M["alu"], (-w * 0.45, d / 2 + 0.03, h * 1.3), (w * 0.45, d / 2 + 0.03, h * 1.3), 0.012, 8)
    if molle:
        for k in range(5):
            p.box(M["webbing"], (w * 0.8, 0.004, 0.016), (0, -d / 2 - 0.002, h * 0.2 + k * h * 0.12), bevel=0)
    if craft:
        for s in (-1, 1):
            pa.rod(p, M["wood_dark"], (s * w * 0.4, -d / 2 - 0.01, 0.0), (s * w * 0.4, -d / 2 - 0.01, h * 1.1), 0.012, 6)
        for k in range(3):
            pa.rod(p, M["rope"], (-w * 0.45, -d / 2 - 0.02, h * (0.25 + k * 0.3)), (w * 0.45, -d / 2 - 0.02, h * (0.25 + k * 0.3)), 0.005, 4)
    ob = p.build()
    pa.marker("grip", (0, d / 2, h + 0.04), ob)
    return ob


def satchel(n, m, w=0.3, d=0.1, h=0.24, strap=None, flap_buckles=2):
    p = Part(n)
    p.box(m, (w, d, h), (0, 0, h / 2), bevel=0.02)
    p.box(m, (w + 0.006, 0.02, h * 0.6), (0, -d / 2 - 0.006, h * 0.66), bevel=0.01)       # flap
    for k in range(flap_buckles):
        x = -w * 0.25 + k * w * 0.5 if flap_buckles > 1 else 0
        p.box(M["webbing"] if m is not M["leather"] else M["leather"], (0.025, 0.006, 0.08), (x, -d / 2 - 0.018, h * 0.45), bevel=0.002)
        p.box(M["brass"], (0.03, 0.006, 0.02), (x, -d / 2 - 0.02, h * 0.42), bevel=0.002)
    st = strap or M["webbing"]
    pts = [Vector((-w / 2 - 0.01, 0, h * 0.85)), Vector((-w * 0.45, 0, h + 0.25)), Vector((0, 0, h + 0.36)), Vector((w * 0.45, 0, h + 0.25)), Vector((w / 2 + 0.01, 0, h * 0.85))]
    p.add(kit.bm_tube(pts, 0.012, 6), st)
    ob = p.build()
    pa.marker("grip", (0, 0, h + 0.36), ob)
    return ob


def belt(n, m, pouches=(), holster=False, buckle="brass", tools=False):
    p = Part(n)
    r = 0.16
    p.add(kit.bm_torus(r, 0.012, 32, 4), m, (0, 0, 0.022), scale=(1, 0.75, 1.8))
    p.box(M[buckle], (0.06, 0.012, 0.05), (0, -r * 0.75 - 0.006, 0.022), bevel=0.004)
    for (a, w, h) in pouches:
        d = Vector((math.cos(a) * r, math.sin(a) * r * 0.75, 0))
        p.box(m, (w, 0.05, h), d * 1.12 + Vector((0, 0, h / 2 - 0.01)), (0, 0, a + math.pi / 2), bevel=0.012)
    if holster:
        d = Vector((math.cos(-0.4) * r, math.sin(-0.4) * r * 0.75, 0))
        p.box(M["leather"], (0.05, 0.08, 0.2), d * 1.15 + Vector((0, 0, 0.0)), (0, 0, -0.4), bevel=0.02)
        for k in range(8):
            a = 1.6 + k * 0.18
            p.cyl(M["brass"], 0.006, 0.03, Vector((math.cos(a) * r * 1.07, math.sin(a) * r * 0.8, 0.022)), "Z", 8)
    if tools:
        d = Vector((math.cos(0.3) * r, math.sin(0.3) * r * 0.75, 0))
        pa.rod(p, M["wood"], d * 1.18 + Vector((0, 0, 0.06)), d * 1.18 + Vector((0, 0, -0.14)), 0.012, 8)
        p.box(M["steel"], (0.03, 0.1, 0.03), d * 1.18 + Vector((0, 0, -0.16)), bevel=0.006)
    ob = p.build()
    pa.marker("grip", (0, -r * 0.75, 0.04), ob)
    return ob


def duffel(n, m):
    p = Part(n)
    p.add(kit.superloft([(y, 0, 0.16, 0.16 * f, 0.16 * f) for y, f in ((-0.32, 0.6), (-0.3, 0.92), (0.0, 1.0), (0.3, 0.92), (0.32, 0.6))], 22, 2.2), m)
    p.box(M["chrome"], (0.006, 0.5, 0.006), (0, 0, 0.318), bevel=0)
    for s in (-1, 1):
        p.add(kit.bm_tube([(s * 0.04, -0.12, 0.3), (s * 0.04, -0.06, 0.4), (s * 0.04, 0.06, 0.4), (s * 0.04, 0.12, 0.3)], 0.012, 6), M["webbing"])
    ob = p.build()
    pa.marker("grip", (0, 0, 0.4), ob)
    return ob


def suitcase(n, m):
    p = Part(n)
    p.box(m, (0.5, 0.18, 0.36), (0, 0, 0.18), bevel=0.025)
    p.box(M["leather"], (0.5, 0.185, 0.03), (0, 0, 0.27), bevel=0.01)
    for x in (-0.18, 0.18):
        p.box(M["brass"], (0.04, 0.19, 0.03), (x, 0, 0.27), bevel=0.004)
    for x in (-0.22, 0.22):
        for z in (0.03, 0.33):
            p.box(M["brass"], (0.04, 0.19, 0.04), (x, 0, z), bevel=0.006)
    p.add(kit.bm_tube([(-0.07, 0, 0.36), (-0.06, 0, 0.42), (0.06, 0, 0.42), (0.07, 0, 0.36)], 0.012, 8), M["leather"])
    ob = p.build()
    pa.marker("grip", (0, 0, 0.42), ob)
    return ob


def air_tank(n):
    p = Part(n)
    p.lathe(M["steel"], [(0.0, 0.0), (0.08, 0.0), (0.09, 0.02), (0.09, 0.5), (0.06, 0.58), (0.0, 0.6)], (0, 0, 0), "Z", 20)
    p.cyl(M["brass"], 0.02, 0.06, (0, 0, 0.62), "Z", 10, bevel=0.003)
    p.tube(M["rubber"], [(0, 0, 0.64), (0.06, 0, 0.7), (0.12, 0.04, 0.6)], 0.01, 6)
    for z in (0.15, 0.4):
        p.box(M["webbing"], (0.2, 0.2, 0.03), (0, 0, z), bevel=0.01)
    ob = p.build()
    pa.marker("grip", (0, 0, 0.62), ob)
    return ob


def brace(n):
    p = Part(n)
    p.add(kit.bm_torus(0.16, 0.05, 28, 6), cloth("dcd8c8"), (0, 0, 0.05), scale=(1, 0.75, 1.1))
    for k in range(4):
        a = math.pi + (k - 1.5) * 0.25
        p.box(M["steel"], (0.012, 0.012, 0.1), (math.cos(a) * 0.17, math.sin(a) * 0.13, 0.05), bevel=0.003)
    p.box(M["webbing"], (0.08, 0.02, 0.06), (0, -0.13, 0.05), bevel=0.006)
    ob = p.build()
    pa.marker("grip", (0, -0.13, 0.08), ob)
    return ob


# ------------------------------------------------------------------------------------------------ catalogue
def C(hexc, kind="cloth"):
    return cloth(hexc, kind)


DEFS = {
    # torso / outer
    "tshirt": lambda n: folded_top(n, C("dcd8c8")),
    "tank": lambda n: folded_top(n, C("8c5634"), collar=False),
    "hoodie": lambda n: folded_top(n, C("4f4842", "knit"), collar=False, hood=True),
    "sweater": lambda n: folded_top(n, C("2e5024", "knit"), trim=C("eeeadc", "knit")),
    "jacket": lambda n: folded_top(n, C("302d33", "leather"), zip_=True, w=0.36, d=0.3, h=0.09),
    "bomber": lambda n: folded_top(n, C("2a3832"), zip_=True, w=0.36, d=0.3, h=0.09, trim=C("6b3e24", "knit")),
    "coat": lambda n: folded_top(n, C("6b3e24"), zip_=True, hood=True, w=0.4, d=0.34, h=0.12),
    "duster": lambda n: folded_top(n, C("74492a", "leather"), buttons=True, w=0.4, d=0.4, h=0.1),
    "vest": lambda n: folded_top(n, C("a8967a"), collar=False, buttons=True, trim=C("8a7a64")),
    "poncho": lambda n: folded_cloth(n, C("4e6458", "rubber"), 0.34, 0.28, 0.05),
    "hazmat": lambda n: folded_top(n, C("d4b020", "rubber"), hood=True, zip_=True, w=0.38, d=0.34, h=0.12),
    "dive_suit": lambda n: folded_top(n, C("a8967a"), collar=True, w=0.4, d=0.36, h=0.12, trim=C("232127", "rubber")),
    # legs
    "jeans": lambda n: folded_trousers(n, C("2c3c52", "denim")),
    "pants": lambda n: folded_trousers(n, C("8c5634")),
    "shorts": lambda n: folded_trousers(n, C("a8967a"), shorts=True),
    "overalls": lambda n: folded_trousers(n, C("243866", "denim"), bib=True),
    "leather_chaps": lambda n: guards(n, C("74492a", "leather"), M["brass"], 0.6),
    # feet / hands / limbs
    "boots": lambda n: boots(n, C("56361e", "leather"), C("18171c", "rubber")),
    "combat_boots": lambda n: boots(n, C("232127", "leather"), C("18171c", "rubber"), 0.28),
    "leather_boots": lambda n: boots(n, C("74492a", "leather"), C("3a2414", "leather"), 0.3, laces=False),
    "gloves": lambda n: gloves(n, C("232127", "leather")),
    "fingerless": lambda n: gloves(n, C("302d33", "knit"), fingerless=True),
    "gauntlets": lambda n: gloves(n, C("4f4842", "leather"), gauntlet=True),
    "arm_guards": lambda n: guards(n, C("4f4842", "metal"), M["webbing"], 0.26, arm=True),
    "shin_guards": lambda n: guards(n, C("80401d", "metal"), M["webbing"], 0.34),
    # head
    "beanie": lambda n: hat(n, "beanie", C("7e261c", "knit")),
    "sunhat": lambda n: hat(n, "sunhat", C("a8967a"), C("6b3e24")),
    "cowboy": lambda n: hat(n, "cowboy", C("74492a"), C("18171c", "leather")),
    "helmet": lambda n: hat(n, "helmet", C("4f4842", "metal")),
    "moto_helmet": lambda n: hat(n, "moto", C("a02a2c", "rubber")),
    "dive_helmet": lambda n: hat(n, "dive", M["brass"]),
    "bee_veil": lambda n: hat(n, "veil", C("dcd8c8")),
    # face
    "bandana": lambda n: folded_cloth(n, C("7e261c"), 0.14, 0.14, 0.02, pattern=C("eeeadc")),
    "scarf": lambda n: folded_cloth(n, C("5a1a14", "knit"), 0.3, 0.12, 0.03, fringe=True, pattern=C("dcd8c8", "knit")),
    "shemagh": lambda n: folded_cloth(n, C("dcd8c8"), 0.22, 0.18, 0.03, fringe=True, pattern=C("232127")),
    "goggles": goggles,
    "gasmask": gasmask,
    "skull_mask": lambda n: mask_plate(n, M["bone"]),
    "welding_mask": lambda n: mask_plate(n, C("302d33", "metal"), welding=True),
    # armour
    "leather_cuirass": lambda n: armour_vest(n, C("74492a", "leather"), "leather"),
    "vest_scrap": lambda n: armour_vest(n, C("6b3e24"), "scrap"),
    "vest_chitin": lambda n: armour_vest(n, C("3a2414", "leather"), "chitin"),
    "vest_tyre": lambda n: armour_vest(n, C("232127", "rubber"), "tyre"),
    "vest_kevlar": lambda n: armour_vest(n, C("243866"), "kevlar"),
    "shoulder": lambda n: pauldron(n, C("4f4842", "metal")),
    # bags (BagLibrary)
    "schoolbag": lambda n: backpack(n, C("a02a2c"), 0.28, 0.14, 0.36),
    "backpack": lambda n: backpack(n, C("a06e1e"), roll=C("2e5024", "knit")),
    "hikingpack": lambda n: backpack(n, C("34508a"), 0.32, 0.22, 0.6, roll=C("a02a2c")),
    "framepack": lambda n: backpack(n, C("c47a40"), 0.34, 0.22, 0.62, frame=True, roll=C("2e5024", "knit")),
    "milpack": lambda n: backpack(n, C("6b3e24"), 0.36, 0.24, 0.56, molle=True),
    "craftpack": lambda n: backpack(n, M["fur"], 0.3, 0.18, 0.44, pockets=False, craft=True),
    "leather_satchel": lambda n: backpack(n, C("74492a", "leather"), 0.3, 0.14, 0.36, pockets=False),
    "air_tank": air_tank,
    "work_belt": lambda n: belt(n, C("3a2414", "leather"), [(0.6, 0.08, 0.1), (2.5, 0.08, 0.1)]),
    "gun_belt": lambda n: belt(n, C("56361e", "leather"), [], holster=True),
    "toolbelt": lambda n: belt(n, C("74492a", "leather"), [(0.9, 0.1, 0.12), (2.2, 0.08, 0.1), (-0.9, 0.07, 0.08)], tools=True),
    "fanny_bag": lambda n: belt(n, M["webbing"], [(-math.pi / 2, 0.18, 0.1)], buckle="plastic"),
    "shoulder_bag": lambda n: satchel(n, C("6b3e24"), 0.34, 0.12, 0.28),
    "sling_bag": lambda n: satchel(n, C("2a3832"), 0.22, 0.1, 0.3, flap_buckles=1),
    "canvas_satchel": lambda n: satchel(n, C("a8967a"), 0.3, 0.1, 0.24),
    "duffel": lambda n: duffel(n, C("4e6458")),
    "suitcase": lambda n: suitcase(n, C("8c5634", "leather")),
    "back_brace": brace,
}
BAGS = {"schoolbag", "backpack", "hikingpack", "framepack", "milpack", "craftpack", "leather_satchel", "air_tank", "work_belt", "gun_belt",
        "toolbelt", "fanny_bag", "shoulder_bag", "sling_bag", "canvas_satchel", "duffel", "suitcase", "back_brace"}

for i, (k, fn) in enumerate(sorted(DEFS.items(), key=lambda kv: (kv[0] in BAGS, kv[0]))):
    iid = "cloth_" + k
    bag = k in BAGS
    pa.add(iid, (lambda n=iid, f=fn: f(n)), F, kind="item", category="Clothing", label=k.replace("_", " "), fit=False, origin="rest",
           garment=k, bag=bag, held="grip marker" if bag else "", worn_mesh="tools/blender/hd/character/garments.py",
           tile=("clothing_bags_%d" if bag else "clothing_%d") % ((i - (len(DEFS) - len(BAGS)) if bag else i) // 9 + 1))

if __name__ == "__main__":
    pa.run(F, out_sub="items", cols=3, gap=0.08)
