"""HD inventory items other than food, tools and clothing: ammo, throwables, bait, medicine, supplies (use_*), farm,
dyes, blueprints, media (books, VHS), trophies, relics, misc trade goods and parts, seeds, saplings and crops.
Real size; origin = resting point (bottom centre).

blender -b -P tools/blender/hd/items/consumables.py [-- ids]"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "parts_all"))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pa  # noqa: E402
from pa import M, Part, Vector, Matrix, kit  # noqa: E402
import ishapes as I  # noqa: E402
from ishapes import col  # noqa: E402

F = "consumables"
ITEMS = []


def item(iid, cat, label=None):
    def deco(fn):
        ITEMS.append((iid, cat, label or iid, fn))
        return fn
    return deco


def reg(iid, cat, fn, label=None):
    ITEMS.append((iid, cat, label or iid, fn))


# ------------------------------------------------------------------------------------------------ ammo
def shells_box(n, shell_hex, box_hex, rows=2, cols=5, r=0.0105, h=0.07, brass=0.018):
    p = Part(n)
    w, d = cols * r * 2.3 + 0.01, rows * r * 2.3 + 0.01
    p.box(col(box_hex, 0.8), (w, d, h * 0.55), (0, 0, h * 0.275), bevel=0.003)
    p.box(M["label_cream"], (w * 0.6, 0.002, h * 0.25), (0, -d / 2 - 0.001, h * 0.28), bevel=0)
    for i in range(cols):
        for j in range(rows):
            x, y = -w / 2 + 0.005 + r * 1.15 + i * r * 2.3, -d / 2 + 0.005 + r * 1.15 + j * r * 2.3
            p.cyl(col(shell_hex, 0.5), r, h - brass, (x, y, (h - brass) / 2 + 0.004), "Z", 8, bevel=0.0)
            p.cyl(M["brass"], r * 1.05, brass, (x, y, h - brass / 2 + 0.002), "Z", 8, bevel=0.0)
    return p.build()


def arrows(n, bolt=False):
    p = Part(n)
    L = 0.42 if bolt else 0.76
    for k in range(3):
        y = (k - 1) * 0.022
        pa.rod(p, M["wood_light"], (-L / 2, y, 0.008), (L / 2, y, 0.008), 0.004, 6)
        p.add(kit.bm_cyl(0.008, 0.035, 4, 0.0, 0.0), M["steel"], (L / 2 + 0.017, y, 0.008), (0, math.pi / 2, 0))
        for f in range(3):
            a = f / 3 * math.tau
            p.box(col("c44038" if f == 0 else "dcd8c8", 0.8), (0.07 if not bolt else 0.04, 0.001, 0.012), (-L / 2 + 0.05, y + math.cos(a) * 0.006, 0.008 + math.sin(a) * 0.006), (a, 0, 0), bevel=0)
    pa.ring(p, M["rope"], (0, 0, 0.008), 0.035, 0.003, "X", 12, 4)
    return p.build()


def mg_belt(n):
    p = Part(n)
    p.box(M["olive"], (0.28, 0.1, 0.18), (0, 0, 0.09), bevel=0.008)
    p.box(M["olive"], (0.29, 0.11, 0.02), (0, 0, 0.185), bevel=0.004)
    p.add(kit.bm_tube([(-0.04, 0, 0.2), (0, 0, 0.23), (0.04, 0, 0.2)], 0.006, 6), M["steel_dark"])
    for k in range(8):
        x = -0.12 + k * 0.03
        p.cyl(M["brass"], 0.006, 0.06, (x, -0.06, 0.2 - abs(k - 4) * 0.004), "Y", 8)
        p.box(M["steel_dark"], (0.012, 0.012, 0.016), (x, -0.06, 0.2 - abs(k - 4) * 0.004), bevel=0)
    return p.build()


def harpoon_bolt(n):
    p = Part(n)
    pa.rod(p, M["steel"], (-0.45, 0, 0.02), (0.4, 0, 0.02), 0.012, 10)
    p.add(kit.bm_cyl(0.03, 0.1, 4, 0.0, 0.0), M["chrome"], (0.45, 0, 0.02), (0, math.pi / 2, 0))
    for s in (-1, 1):
        pa.rod(p, M["chrome"], (0.42, 0, 0.02), (0.36, s * 0.035, 0.02), 0.005, 4)
    pa.ring(p, M["steel_dark"], (-0.44, 0, 0.02), 0.016, 0.004, "X", 10, 4)
    return p.build()


def caltrops(n):
    p = Part(n)
    sk = I.pouch("bag", M["burlap"], None, 0.18, 0.2, True, 3)
    r = I.rnd(4)
    for k in range(3):
        q = Vector((0.12 + r.uniform(-0.02, 0.02), -0.04 + k * 0.05, 0.012))
        for d in ((1, 0, 0.5), (-0.5, 0.86, 0.5), (-0.5, -0.86, 0.5), (0, 0, -1)):
            pa.rod(p, M["steel"], q, q + Vector(d) * 0.025, 0.003, 4)
    ob = p.build()
    sk.parent = ob
    return ob


def canister(n, body, band, h=0.13, r=0.03, pin=True):
    p = Part(n)
    p.cyl(col(body, 0.6), r, h, (0, 0, h / 2), "Z", 16, bevel=0.004)
    p.cyl(col(band, 0.6), r + 0.001, 0.02, (0, 0, h * 0.7), "Z", 16, bevel=0)
    p.cyl(M["steel"], r * 0.6, 0.02, (0, 0, h + 0.01), "Z", 12, bevel=0.003)
    if pin:
        p.add(kit.bm_torus(0.01, 0.002, 10, 4), M["steel"], (0.015, 0, h + 0.022), (math.pi / 2, 0, 0))
        p.box(M["steel_dark"], (0.008, 0.012, 0.06), (r * 0.8, 0, h - 0.02), bevel=0.002)
    return p.build()


reg("ammo_shells", "Ammo", lambda: shells_box("ammo_shells", "b02818", "a8572a"), "Shotgun shells")
reg("ammo_cartridge", "Ammo", lambda: shells_box("ammo_cartridge", "c47a40", "56361e", 3, 6, 0.0055, 0.03, 0.02), "Pistol rounds")
reg("ammo_rifle", "Ammo", lambda: shells_box("ammo_rifle", "c47a40", "4e6458", 2, 5, 0.006, 0.07, 0.05), "Rifle rounds")
reg("ammo_arrow", "Ammo", lambda: arrows("ammo_arrow"), "Arrows")
reg("ammo_bolt", "Ammo", lambda: arrows("ammo_bolt", True), "Crossbow bolts")
reg("ammo_mg", "Ammo", lambda: mg_belt("ammo_mg"), "MG belt can")
reg("ammo_harpoon", "Ammo", lambda: harpoon_bolt("ammo_harpoon"), "Harpoon bolt")
reg("ammo_caltrops", "Ammo", lambda: caltrops("ammo_caltrops"), "Caltrop bag")
reg("ammo_smoke", "Ammo", lambda: canister("ammo_smoke", "4e6458", "dcd8c8", 0.16, 0.035), "Smoke grenade")
reg("ammo_flare", "Ammo", lambda: canister("ammo_flare", "c44038", "e0ac40", 0.1, 0.014, pin=False), "Flare cartridge")


# ------------------------------------------------------------------------------------------------ throwables, bait
def dynamite(n):
    p = Part(n)
    for k, (x, z) in enumerate(((-0.024, 0.02), (0.024, 0.02), (0.0, 0.058))):
        p.cyl(col("a02a2c", 0.6), 0.022, 0.2, (x, 0, z), "Y", 14, bevel=0.004)
    for y in (-0.06, 0.06):
        pa.ring(p, M["black"], (0, y, 0.035), 0.05, 0.004, "Y", 16, 4)
    p.box(M["label_cream"], (0.04, 0.08, 0.002), (0, 0, 0.082), bevel=0)
    p.tube(M["black"], [(0, 0.1, 0.058), (0.01, 0.14, 0.07), (0.03, 0.16, 0.06)], 0.0025, 4)
    return p.build()


def molotov(n):
    p = Part(n)
    ob = I.bottle("b", M["glass_green"], liquid="c48c2a", h=0.25, r=0.035, cap=None)
    p.add(kit.bm_rock(0.025, 2, 1.6, 2), M["canvas"], (0, 0, 0.27))
    p.tube(M["canvas"], [(0, 0, 0.28), (0.02, 0, 0.32), (0.05, 0, 0.3)], 0.008, 6)
    o = p.build()
    ob.parent = o
    return o


def pipebomb(n):
    p = Part(n)
    p.cyl(M["steel"], 0.025, 0.16, (0, 0, 0.025), "Y", 14, bevel=0.003)
    for y in (-0.085, 0.085):
        p.cyl(M["steel_dark"], 0.03, 0.025, (0, y, 0.03), "Y", 6, bevel=0.003)
    p.tube(M["black"], [(0, 0.1, 0.03), (0.0, 0.13, 0.04), (0.02, 0.15, 0.03)], 0.0025, 4)
    p.box(M["black"], (0.03, 0.06, 0.004), (0, 0, 0.052), bevel=0.0)
    return p.build()


def rock(n):
    p = Part(n)
    p.add(kit.bm_rock(0.05, 6, 0.7, 2), M["concrete"], (0, 0, 0.032))
    return p.build()


def bait(n, hexc, worms=False):
    p = Part(n)
    p.lathe(M["tin"], [(0.0, 0.0), (0.04, 0.0), (0.042, 0.004), (0.042, 0.04), (0.04, 0.04), (0.04, 0.006), (0.0, 0.006)], (0, 0, 0), "Z", 20)
    p.cyl(M["burlap"], 0.038, 0.01, (0, 0, 0.032), "Z", 16, bevel=0)
    r = I.rnd(len(n))
    for k in range(6):
        a = r.random() * math.tau
        d = r.random() * 0.025
        if worms:
            p.tube(col(hexc, 0.3), [(math.cos(a) * d, math.sin(a) * d, 0.04), (math.cos(a) * d + 0.01, math.sin(a) * d + 0.005, 0.045), (math.cos(a) * d + 0.02, math.sin(a) * d - 0.004, 0.04)], 0.003, 5)
        else:
            p.add(kit.bm_rock(0.008, k, 0.7, 1), col(hexc, 0.6), (math.cos(a) * d, math.sin(a) * d, 0.04))
    p.cyl(M["tin"], 0.041, 0.004, (0.05, 0, 0.046), "Z", 20, bevel=0.0)
    return p.build()


reg("throw_dynamite", "Throwable", lambda: dynamite("throw_dynamite"), "Dynamite")
reg("throw_molotov", "Throwable", lambda: molotov("throw_molotov"), "Molotov")
reg("throw_pipebomb", "Throwable", lambda: pipebomb("throw_pipebomb"), "Pipe bomb")
reg("throw_rock", "Throwable", lambda: rock("throw_rock"), "Rock")
reg("throw_smoke", "Throwable", lambda: canister("throw_smoke", "a8967a", "4e6458", 0.14, 0.032), "Smoke bomb")
for bid, hx in (("bait_worms", "c87a78"), ("bait_insects", "3a3020"), ("bait_meat", "a83a3a"), ("bait_corn", "e0c040"), ("bait_bread", "c89050"), ("bait_maggots", "e8dcb8")):
    reg(bid, "Ammo", (lambda b=bid, h=hx: bait(b, h, worms=b in ("bait_worms", "bait_maggots"))), bid.replace("bait_", "Bait: "))


# ------------------------------------------------------------------------------------------------ medicine, supplies
def pill_bottle(n, body, cap, label=None, h=0.07, r=0.02):
    p = Part(n)
    p.lathe(col(body, 0.3), [(0.0, 0.0), (r, 0.0), (r, h * 0.85), (r * 0.8, h * 0.9), (0.0, h * 0.9)], (0, 0, 0), "Z", 18)
    p.cyl(col(cap, 0.5), r * 1.02, h * 0.18, (0, 0, h * 0.95), "Z", 18, bevel=0.002)
    p.cyl(M["label_cream"] if label is None else col(label, 0.7), r + 0.0008, h * 0.45, (0, 0, h * 0.42), "Z", 18, bevel=0.0, caps=False)
    return p.build()


def first_aid(n):
    p = Part(n)
    p.box(M["plastic_white"], (0.24, 0.1, 0.16), (0, 0, 0.08), bevel=0.012)
    for s in ((0.08, 0.025), (0.025, 0.08)):
        p.box(M["label_red"], (s[0], 0.002, s[1]), (0, -0.051, 0.08), bevel=0)
    p.add(kit.bm_tube([(-0.05, 0, 0.16), (-0.05, 0, 0.19), (0.05, 0, 0.19), (0.05, 0, 0.16)], 0.006, 6), M["plastic"])
    return p.build()


def bandage(n):
    p = Part(n)
    p.cyl(M["paper"], 0.03, 0.06, (0, 0, 0.03), "Y", 20, bevel=0.006)
    p.box(M["paper"], (0.004, 0.06, 0.1), (0.03, 0, 0.0), bevel=0)
    return p.build()


def splint(n):
    p = Part(n)
    for x in (-0.03, 0.03):
        p.box(M["wood_light"], (0.025, 0.4, 0.01), (x, 0, 0.005), bevel=0.002)
    for y in (-0.12, 0.0, 0.12):
        pa.ring(p, M["paper"], (0, y, 0.006), 0.045, 0.006, "Y", 14, 4)
    return p.build()


def poultice(n):
    p = Part(n)
    p.add(kit.bm_rock(0.04, 3, 0.45, 2), M["canvas"], (0, 0, 0.016))
    pa.ring(p, M["rope"], (0, 0, 0.018), 0.03, 0.003, "X", 12, 4)
    p.add(kit.bm_box(0.03, 0.01, 0.002), M["leaf"], (0.02, 0.0, 0.036), (0, 0.3, 0.4))
    return p.build()


def vial_box(n, hexc):
    p = Part(n)
    p.box(M["cardboard"], (0.1, 0.05, 0.04), (0, 0, 0.02), bevel=0.003)
    for k in range(3):
        x = -0.03 + k * 0.03
        p.cyl(M["glass_clear"], 0.008, 0.05, (x, 0, 0.065), "Z", 10)
        p.cyl(col(hexc, 0.2), 0.0072, 0.03, (x, 0, 0.055), "Z", 10)
        p.cyl(col("c44038", 0.5), 0.009, 0.01, (x, 0, 0.093), "Z", 10, bevel=0.001)
    return p.build()


def battery(n):
    p = Part(n)
    p.box(M["plastic"], (0.26, 0.17, 0.2), (0, 0, 0.1), bevel=0.006)
    p.box(M["plastic"], (0.26, 0.17, 0.02), (0, 0, 0.205), bevel=0.004)
    for x, c in ((-0.09, "label_red"), (0.09, "plastic")):
        p.cyl(M["steel"], 0.012, 0.025, (x, 0.04, 0.225), "Z", 10, bevel=0.002)
        p.cyl(M[c], 0.016, 0.004, (x, 0.04, 0.215), "Z", 10)
    p.box(M["label_yellow"], (0.16, 0.002, 0.07), (0, -0.086, 0.12), bevel=0)
    p.add(kit.bm_tube([(-0.08, -0.03, 0.215), (-0.06, -0.03, 0.26), (0.06, -0.03, 0.26), (0.08, -0.03, 0.215)], 0.006, 6), M["plastic"])
    return p.build()


def canteen(n):
    p = Part(n)
    q = Matrix.Rotation(math.pi / 2, 4, "X")
    p.add(kit.bm_lathe([(0.0, -0.04), (0.08, -0.035), (0.1, -0.01), (0.1, 0.01), (0.08, 0.035), (0.0, 0.04)], 24), M["olive"], matrix=Matrix.Translation((0, 0, 0.1)) @ q)
    p.cyl(M["black"], 0.016, 0.03, (0, 0, 0.21), "Z", 12, bevel=0.003)
    p.add(kit.bm_torus(0.1, 0.006, 24, 4), M["webbing"], matrix=Matrix.Translation((0, 0, 0.1)) @ q)
    return p.build()


def filter_round(n, hexc="e0ac40", r=0.1, h=0.06):
    p = Part(n)
    p.cyl(M["steel"], r, 0.008, (0, 0, 0.004), "Z", 28)
    p.cyl(M["steel"], r, 0.008, (0, 0, h - 0.004), "Z", 28)
    p.cyl(col(hexc, 0.9, 1.0), r - 0.004, h - 0.012, (0, 0, h / 2), "Z", 36, bevel=0.0)
    p.cyl(M["black"], r * 0.45, h + 0.002, (0, 0, h / 2), "Z", 20)
    return p.build()


def oil_filter(n):
    p = Part(n)
    p.cyl(M["hazard"], 0.04, 0.1, (0, 0, 0.05), "Z", 20, bevel=0.008)
    p.cyl(M["steel"], 0.04, 0.012, (0, 0, 0.104), "Z", 20, bevel=0.002)
    p.cyl(M["black"], 0.012, 0.006, (0, 0, 0.112), "Z", 10)
    return p.build()


def gas_bottle(n, hexc, h=0.45, r=0.06, valve="chrome"):
    p = Part(n)
    p.lathe(col(hexc, 0.4), [(0.0, 0.0), (r * 0.9, 0.0), (r, 0.02), (r, h * 0.8), (r * 0.7, h * 0.92), (r * 0.3, h), (0.0, h)], (0, 0, 0), "Z", 20)
    p.cyl(M[valve], 0.014, 0.05, (0, 0, h + 0.02), "Z", 10, bevel=0.002)
    p.cyl(M["black"], 0.02, 0.01, (0.02, 0, h + 0.035), "X", 10)
    p.box(M["label_cream"], (0.002, r * 0.8, h * 0.25), (r + 0.001, 0, h * 0.5), bevel=0)
    return p.build()


def horseshoes(n):
    p = Part(n)
    for k, x in enumerate((-0.04, 0.05)):
        pts = [Vector((x + math.cos(a) * 0.055, math.sin(a) * 0.06, 0.005 + k * 0.008)) for a in [math.radians(-30 + j * 20) for j in range(13)]]
        p.add(kit.bm_tube(pts, 0.007, 6), M["iron"])
    return p.build()


def toolbox(n, hexc="a02a2c"):
    p = Part(n)
    p.box(col(hexc, 0.5), (0.36, 0.16, 0.14), (0, 0, 0.07), bevel=0.008)
    p.box(col(hexc, 0.5), (0.36, 0.16, 0.03), (0, 0, 0.15), bevel=0.006)
    p.add(kit.bm_tube([(-0.08, 0, 0.165), (-0.08, 0, 0.2), (0.08, 0, 0.2), (0.08, 0, 0.165)], 0.008, 6), M["chrome"])
    for x in (-0.15, 0.15):
        p.box(M["chrome"], (0.03, 0.01, 0.03), (x, -0.081, 0.14), bevel=0.002)
    return p.build()


def saddle(n):
    p = Part(n)
    p.add(kit.superloft([(y, 0, 0.14 + 0.06 * abs(y / 0.25) ** 2, 0.2, 0.06) for y in (-0.25, -0.12, 0.0, 0.12, 0.25)], 20, 2.2), M["leather"])
    p.add(kit.superloft([(y, 0, 0.18, 0.12, 0.03) for y in (-0.18, 0.18)], 16, 2.0), M["wood_dark"])
    p.cyl(M["leather"], 0.025, 0.08, (0, -0.24, 0.24), "Z", 12, bevel=0.01)      # horn
    for s in (-1, 1):
        p.box(M["leather"], (0.01, 0.2, 0.3), (s * 0.2, 0, 0.06), (0, s * 0.15, 0), bevel=0.004)
        p.add(kit.bm_torus(0.03, 0.006, 12, 4), M["steel"], (s * 0.23, 0.0, -0.08), (0, math.pi / 2, 0))
    p.box(M["felt_green"], (0.42, 0.56, 0.01), (0, 0, 0.1), bevel=0.003)
    return p.build()


def tin_small(n, hexc, h=0.04, r=0.05, lid_hex=None):
    p = Part(n)
    p.cyl(col(hexc, 0.4), r, h, (0, 0, h / 2), "Z", 24, bevel=0.003)
    p.cyl(col(lid_hex or hexc, 0.4), r + 0.002, 0.012, (0, 0, h), "Z", 24, bevel=0.003)
    return p.build()


def spark_plugs(n):
    p = Part(n)
    p.box(M["cardboard"], (0.12, 0.06, 0.03), (0, 0, 0.015), bevel=0.003)
    for k in range(4):
        x = -0.042 + k * 0.028
        p.cyl(M["plastic_white"], 0.007, 0.05, (x, 0, 0.036), "Y", 10)
        p.cyl(M["steel"], 0.009, 0.02, (x, -0.035, 0.036), "Y", 6)
    return p.build()


def sponge(n):
    p = Part(n)
    p.box(M["foam"], (0.12, 0.08, 0.035), (0, 0, 0.0175), bevel=0.008)
    p.box(M["plastic_green"], (0.12, 0.08, 0.008), (0, 0, 0.039), bevel=0.003)
    return p.build()


def water_test(n):
    p = Part(n)
    p.box(M["plastic_white"], (0.14, 0.08, 0.04), (0, 0, 0.02), bevel=0.006)
    for k, c in enumerate(("c44038", "e0ac40", "58843c", "3a6aa0")):
        p.cyl(M["glass_clear"], 0.008, 0.08, (-0.045 + k * 0.03, 0, 0.07), "Z", 10)
        p.cyl(col(c, 0.2), 0.007, 0.03, (-0.045 + k * 0.03, 0, 0.05), "Z", 10)
    return p.build()


reg("med_firstaid", "Consumable", lambda: first_aid("med_firstaid"), "First-aid kit")
reg("med_bandage", "Consumable", lambda: bandage("med_bandage"), "Bandage")
reg("med_splint", "Consumable", lambda: splint("med_splint"), "Splint")
reg("med_poultice", "Consumable", lambda: poultice("med_poultice"), "Poultice")
reg("med_pills", "Consumable", lambda: pill_bottle("med_pills", "eeeadc", "c44038"), "Pills")
reg("med_painkillers", "Consumable", lambda: pill_bottle("med_painkillers", "c47a40", "fbf8ee", "3a6aa0"), "Painkillers")
reg("med_antibiotics", "Consumable", lambda: pill_bottle("med_antibiotics", "dcd8c8", "3a6aa0", "58843c"), "Antibiotics")
reg("med_antivenom", "Consumable", lambda: vial_box("med_antivenom", "e0ac40"), "Antivenom")
reg("med_disinfectant", "Consumable", lambda: I.bottle("med_disinfectant", M["glass_brown"], label="dcd8c8", h=0.16, r=0.03, cap="plastic"), "Disinfectant")
reg("vet_salve", "Consumable", lambda: I.jar("vet_salve", "e0a030", h=0.06, r=0.04, lid="tin"), "Honey salve (animals)")
reg("use_battery", "Consumable", lambda: battery("use_battery"), "Car battery")
reg("use_canteen", "Consumable", lambda: canteen("use_canteen"), "Canteen")
reg("use_air_filter", "Consumable", lambda: filter_round("use_air_filter"), "Air filter")
reg("use_oil_filter", "Consumable", lambda: oil_filter("use_oil_filter"), "Oil filter")
reg("use_filter_cartridge", "Consumable", lambda: filter_round("use_filter_cartridge", "dcd8c8", 0.04, 0.14), "Water filter cartridge")
reg("use_fuel_additive", "Consumable", lambda: I.bottle("use_fuel_additive", M["plastic_red"], label="e0ac40", h=0.18, r=0.035, cap="plastic", neck=0.2), "Fuel additive")
reg("use_horseshoes", "Consumable", lambda: horseshoes("use_horseshoes"), "Horseshoes")
reg("use_nitrous", "Consumable", lambda: gas_bottle("use_nitrous", "3a6aa0", 0.5, 0.06), "Nitrous bottle")
reg("use_o2_bottle", "Consumable", lambda: gas_bottle("use_o2_bottle", "fbf8ee", 0.55, 0.07, "brass"), "O2 bottle")
reg("use_repair_kit", "Consumable", lambda: toolbox("use_repair_kit"), "Repair kit")
reg("use_saddle", "Consumable", lambda: saddle("use_saddle"), "Saddle")
reg("use_sewing_kit", "Consumable", lambda: tin_small("use_sewing_kit", "3a6aa0", 0.04, 0.06, "c44038"), "Sewing kit")
reg("use_spark_plugs", "Consumable", lambda: spark_plugs("use_spark_plugs"), "Spark plugs")
reg("use_sponge", "Consumable", lambda: sponge("use_sponge"), "Sponge")
reg("use_water_test", "Consumable", lambda: water_test("use_water_test"), "Water test kit")
reg("farm_fertilizer", "Consumable", lambda: I.sack("farm_fertilizer", M["burlap"], "5a3a20", 0.3, 0.42, 2), "Fertilizer")
reg("farm_manure", "Consumable", lambda: I.sack("farm_manure", M["burlap"], "3a2a14", 0.3, 0.4, 5), "Manure")
for i, d in enumerate(("red", "blue", "green", "yellow", "black", "white")):
    hx = {"red": "c44038", "blue": "34508a", "green": "58843c", "yellow": "e0ac40", "black": "232127", "white": "fbf8ee"}[d]
    reg("dye_" + d, "Consumable", (lambda n="dye_" + d, h=hx: I.jar(n, h, lid="wood_dark", h=0.08, r=0.03)), d.title() + " dye")


# ------------------------------------------------------------------------------------------------ blueprints, media, tokens
def blueprint(n):
    p = Part(n)
    p.box(M["label_blue"], (0.3, 0.22, 0.002), (0.02, 0.0, 0.001), (0, 0, 0.1), bevel=0)
    for k in range(5):
        p.box(M["paper"], (0.2, 0.002, 0.0005), (0.02, -0.08 + k * 0.04, 0.0025), (0, 0, 0.1), bevel=0)
    p.cyl(M["label_blue"], 0.025, 0.3, (-0.05, 0.12, 0.025), "X", 16, bevel=0.002)
    p.cyl(M["paper"], 0.026, 0.02, (-0.05, 0.12, 0.025), "X", 16)
    return p.build()


def coin(n, metal="brass", r=0.016):
    p = Part(n)
    p.cyl(M[metal], r, 0.003, (0, 0, 0.0015), "Z", 20, bevel=0.0008)
    p.cyl(M["steel_dark"], r * 0.3, 0.0034, (0, 0, 0.0017), "Z", 8)
    return p.build()


BOOKS = {"book_builder": ("8c5634", "BUILDER"), "book_charm": ("a02a2c", "CHARM"), "book_chemistry": ("34508a", "CHEMISTRY"),
         "book_gunsmith": ("302d33", "GUNSMITH"), "book_mechanics_1": ("4e6458", "MECHANICS I"), "book_mechanics_2": ("3a4c44", "MECHANICS II"),
         "book_scrapper": ("a06e1e", "SCRAPPER")}
VHS = {"vhs_demolition": "c44038", "vhs_driving": "e0ac40", "vhs_karate": "34508a", "vhs_salesman": "58843c", "vhs_survival": "8c5634"}
for i, (b, (c, t)) in enumerate(BOOKS.items()):
    reg(b, "Media", (lambda n=b, cc=c, k=i: I.book(n, cc, w=0.15, h=0.22, t=0.03 + (k % 3) * 0.008, title="e0ac40")), "Book: " + t.title())
for v, c in VHS.items():
    reg(v, "Media", (lambda n=v, cc=c: I.cassette(n, cc)), "VHS: " + v[4:].title())
for b in ("bp_aviation", "bp_cargo_generator", "bp_framepack", "bp_lights_search", "bp_submarine", "bp_weapon_flamer", "bp_weapon_harpoon", "bp_weapon_mg"):
    reg(b, "Consumable", (lambda n=b: blueprint(n)), "Blueprint: " + b[3:].replace("_", " "))
reg("coin_chit", "Other", lambda: coin("coin_chit"), "Guild chit")
reg("keepsake_badge", "Other", lambda: I.medal("keepsake_badge", "silver", "34508a"), "Convoy enamel badge")
reg("evidence_receipt", "Other", lambda: I.document("evidence_receipt", M["paper"], 0.08, 0.16, 1), "Fuel receipt (evidence)")
reg("evidence_manifest", "Other", lambda: I.document("evidence_manifest", M["paper_old"], 0.21, 0.297, 4, True), "Forged manifest (evidence)")


# ------------------------------------------------------------------------------------------------ trophies
def mounted(n, kind):
    p = Part(n)
    p.box(M["wood"], (0.5, 0.03, 0.22), (0, 0.0, 0.11), bevel=0.01)
    if kind == "fish":
        o = I.fish("f", "8a9a4a", l=0.38, h=0.1)
    else:
        o = I.fish("f", "8aff5a", l=0.38, h=0.1, glow=True)
    o.rotation_euler = (math.pi / 2, 0, 0)
    o.location = (0, -0.02, 0.11)
    ob = p.build()
    o.parent = ob
    return ob


def antlers(n, horns=False, tusks=False):
    p = Part(n)
    if tusks:
        for s in (-1, 1):
            pts = [Vector((s * 0.03 + s * math.sin(t) * 0.08, -math.cos(t) * 0.04, 0.01 + t * 0.03)) for t in [k / 8 * 2.2 for k in range(9)]]
            p.add(kit.bm_tube(pts, [0.016 - k * 0.0017 for k in range(9)], 8), M["bone"])
        return p.build()
    p.add(kit.bm_rock(0.05, 1, 0.6, 2), M["bone"], (0, 0, 0.03))
    for s in (-1, 1):
        if horns:
            pts = [Vector((s * (0.04 + t * 0.12), 0.0, 0.04 + math.sin(t * 2.4) * 0.12)) for t in [k / 10 for k in range(11)]]
            p.add(kit.bm_tube(pts, [0.02 - k * 0.0017 for k in range(11)], 8), M["wood_dark"])
        else:
            main = [Vector((s * (0.04 + t * 0.22), -t * 0.05, 0.04 + t * 0.26)) for t in [k / 8 for k in range(9)]]
            p.add(kit.bm_tube(main, [0.014 - k * 0.0012 for k in range(9)], 6), M["bone"])
            for k in (3, 5, 7):
                b = main[k]
                p.add(kit.bm_tube([b, b + Vector((s * 0.03, -0.04, 0.08))], [0.008, 0.003], 6), M["bone"])
    return p.build()


def pelt(n, hexc="74492a", bear=False):
    p = Part(n)
    pts = [(math.cos(a) * 0.5 * (1 + 0.25 * math.sin(4 * a)), math.sin(a) * 0.35 * (1 + 0.2 * math.cos(4 * a))) for a in [k / 24 * math.tau for k in range(24)]]
    if bear:
        pts = [(x * 1.4, y * 1.4) for x, y in pts]
    p.add(kit.bm_grid(pts, 0.0, 0.012), M["fur"] if not bear else col("3a2414", 0.95, 1.0))
    if bear:
        p.add(kit.bm_rock(0.12, 4, 0.6, 2), col("3a2414", 0.95, 1.0), (0.66, 0, 0.05))
    else:
        p.add(kit.bm_box(0.12, 0.05, 0.02, bevel=0.008), M["fur"], (0.55, 0, 0.01))
    return p.build()


def licence_plate(n):
    p = Part(n)
    p.box(M["label_yellow"], (0.3, 0.15, 0.004), (0, 0, 0.002), bevel=0.001)
    p.box(M["chrome"], (0.304, 0.154, 0.002), (0, 0, 0.0005), bevel=0)
    for k in range(6):
        p.box(M["black"], (0.026, 0.06, 0.002), (-0.1 + k * 0.04, 0, 0.005), bevel=0.0005)
    return p.build()


def hood_ornament(n):
    p = Part(n)
    p.box(M["chrome"], (0.04, 0.06, 0.02), (0, 0, 0.01), bevel=0.004)
    p.add(kit.bm_tube([(0, 0.02, 0.02), (0, -0.02, 0.08), (0, -0.08, 0.1)], [0.012, 0.008, 0.003], 8), M["chrome"])
    for s in (-1, 1):
        p.add(kit.bm_box(0.08, 0.04, 0.004, bevel=0.002), M["chrome"], (s * 0.04, -0.01, 0.07), (0.3, s * 0.4, 0))
    return p.build()


def hubcap(n):
    p = Part(n)
    p.add(kit.bm_lathe([(0.0, 0.05), (0.06, 0.045), (0.16, 0.02), (0.18, 0.0), (0.0, 0.0)], 32), M["chrome"])
    for k in range(8):
        a = k / 8 * math.tau
        p.box(M["steel_dark"], (0.08, 0.008, 0.004), (math.cos(a) * 0.11, math.sin(a) * 0.11, 0.032), (0, -0.2, a), bevel=0)
    return p.build()


def bull_skull(n):
    p = Part(n)
    p.add(kit.superloft([(y, 0, 0.08, w, h) for y, w, h in ((-0.2, 0.05, 0.04), (-0.08, 0.08, 0.06), (0.06, 0.11, 0.08), (0.12, 0.1, 0.07))], 18, 2.2), M["bone"])
    for s in (-1, 1):
        p.sphere(M["black"], 0.025, (s * 0.07, 0.0, 0.13), 10, 6)
        pts = [Vector((s * (0.1 + t * 0.25), 0.08, 0.12 + math.sin(t * 2.6) * 0.12)) for t in [k / 10 for k in range(11)]]
        p.add(kit.bm_tube(pts, [0.025 - k * 0.002 for k in range(11)], 8), M["bone"])
    return p.build()


reg("trophy_fish", "Other", lambda: mounted("trophy_fish", "fish"), "Mounted fish")
reg("trophy_fish_mutant", "Other", lambda: mounted("trophy_fish_mutant", "mutant"), "Mounted mutant fish")
reg("trophy_antlers", "Other", lambda: antlers("trophy_antlers"), "Deer antlers")
reg("trophy_horns", "Other", lambda: antlers("trophy_horns", horns=True), "Antelope horns")
reg("trophy_tusks", "Other", lambda: antlers("trophy_tusks", tusks=True), "Boar tusks")
reg("trophy_pelt", "Other", lambda: pelt("trophy_pelt"), "Wolf pelt")
reg("trophy_bearskin", "Other", lambda: pelt("trophy_bearskin", bear=True), "Bearskin")
reg("trophy_plate", "Other", lambda: licence_plate("trophy_plate"), "Licence plate")
reg("trophy_ornament", "Other", lambda: hood_ornament("trophy_ornament"), "Hood ornament")
reg("trophy_hubcap", "Other", lambda: hubcap("trophy_hubcap"), "Chrome hubcap")
reg("trophy_skull", "Other", lambda: bull_skull("trophy_skull"), "Bull skull")


# ------------------------------------------------------------------------------------------------ relics (the Last Engine)
def relic(n, kind):
    p = Part(n)
    red = M["crimson"]
    if kind == "block":
        p.box(M["steel"], (0.5, 0.9, 0.3), (0, 0, 0.15), bevel=0.02)
        for s in (-1, 1):
            p.box(M["steel"], (0.16, 0.86, 0.14), (s * 0.2, 0, 0.34), (0, s * -0.6, 0), bevel=0.012)
        p.box(M["label_cream"], (0.002, 0.3, 0.08), (0.251, 0, 0.15), bevel=0)
    elif kind == "heads":
        for s in (-1, 1):
            p.box(M["steel"], (0.16, 0.86, 0.1), (s * 0.14, 0, 0.05), bevel=0.01)
            p.box(red, (0.13, 0.82, 0.07), (s * 0.14, 0, 0.13), bevel=0.02)
            pa.bolts(p, M["chrome"], [(s * 0.14, -0.35 + k * 0.14, 0.17) for k in range(6)], 0.008, 0.01)
    elif kind == "crank":
        pts = []
        for k in range(13):
            y = -0.45 + k * 0.075
            pa.rod(p, M["chrome"], (0, y, 0.06), (0, y + 0.04, 0.06), 0.03, 14)
            if k % 2 == 1:
                a = k * 1.05
                p.box(M["steel"], (0.02, 0.03, 0.14), (math.cos(a) * 0.03, y + 0.05, 0.06 + math.sin(a) * 0.03), (0, a, 0), bevel=0.006)
        p.cyl(M["steel_dark"], 0.06, 0.03, (0, 0.5, 0.06), "Y", 18, bevel=0.004)
        p.box(M["steel_dark"], (0.06, 0.04, 0.03), (0, 0.0, 0.015), bevel=0.004)
    else:  # twin blowers
        for y in (-0.22, 0.22):
            p.add(kit.superloft([(yy, 0, 0.12, 0.15, 0.1) for yy in (y - 0.2, y + 0.2)], 24, 3.2), M["chrome"])
            for k in range(6):
                p.box(M["chrome"], (0.32, 0.012, 0.014), (0, y - 0.17 + k * 0.07, 0.12), bevel=0.003)
            p.add(kit.superloft([(yy, 0, 0.27, 0.12, 0.05) for yy in (y - 0.12, y + 0.12)], 16, 4.0), M["black"])
    if kind != "crank":
        p.box(M["label_red"], (0.12, 0.002, 0.04), (0, -0.46 if kind != "heads" else -0.44, 0.06), bevel=0)
    return p.build()


for k in ("block", "heads", "crank", "blower"):
    reg("relic_" + k, "Other", (lambda kk=k: relic("relic_" + kk, kk)), "Relic: V12 " + k)


# ------------------------------------------------------------------------------------------------ misc goods and parts
def coil_item(n):
    p = Part(n)
    p.cyl(M["steel_dark"], 0.03, 0.14, (0, 0, 0.07), "Z", 16)
    for k in range(10):
        pa.ring(p, M["copper"], (0, 0, 0.015 + k * 0.012), 0.05, 0.006, "Z", 20, 4)
    for z in (0.004, 0.136):
        p.cyl(M["plastic"], 0.07, 0.008, (0, 0, z), "Z", 20)
    return p.build()


def turbine_blade(n):
    p = Part(n)
    secs = []
    for k in range(8):
        t = k / 7
        y = -0.5 + t * 1.0
        ch = 0.14 * (1 - t) + 0.05
        secs.append([(-ch / 2, y, 0.01), (0, y, 0.022), (ch / 2, y, 0.01), (0, y, 0.002)])
    p.add(kit.bm_loft(secs), M["cream"])
    p.cyl(M["steel"], 0.04, 0.06, (0, -0.52, 0.02), "Y", 16, bevel=0.004)
    return p.build()


def solar_cell(n):
    p = Part(n)
    p.box(M["alu"], (0.3, 0.3, 0.015), (0, 0, 0.0075), bevel=0.002)
    p.box(M["glass"], (0.28, 0.28, 0.003), (0, 0, 0.016), bevel=0)
    for k in range(1, 4):
        p.box(M["chrome"], (0.28, 0.003, 0.0005), (0, -0.14 + k * 0.07, 0.018), bevel=0)
        p.box(M["chrome"], (0.003, 0.28, 0.0005), (-0.14 + k * 0.07, 0, 0.018), bevel=0)
    return p.build()


def nuts_box(n, kind):
    p = Part(n)
    p.box(M["cardboard"], (0.14, 0.1, 0.06), (0, 0, 0.03), bevel=0.003)
    r = I.rnd(len(n))
    for k in range(10):
        x, y = r.uniform(-0.05, 0.05), r.uniform(-0.035, 0.035)
        if kind == "nails":
            pa.rod(p, M["steel"], (x, y, 0.062), (x + r.uniform(-0.04, 0.04), y + r.uniform(-0.02, 0.02), 0.064), 0.002, 4)
        else:
            p.cyl(M["steel"], 0.007, 0.01, (x, y, 0.065), "Z", 6)
    return p.build()


def castings(n):
    p = Part(n)
    for k, (x, y) in enumerate(((-0.06, 0.0), (0.05, 0.03), (0.02, -0.06))):
        p.cyl(M["iron"], 0.04, 0.03, (x, y, 0.015 + k * 0.005), "Z", 8, bevel=0.004)
        p.cyl(M["steel_dark"], 0.012, 0.032, (x, y, 0.016 + k * 0.005), "Z", 8)
    return p.build()


def engine_block(n):
    p = Part(n)
    p.box(M["iron"], (0.3, 0.46, 0.28), (0, 0, 0.14), bevel=0.02)
    for k in range(4):
        p.cyl(M["steel"], 0.045, 0.012, (0, -0.165 + k * 0.11, 0.282), "Z", 18)
    p.box(M["label_cream"], (0.002, 0.14, 0.05), (0.151, 0, 0.12), bevel=0)
    return p.build()


def parcel(n):
    p = Part(n)
    p.box(M["cardboard"], (0.3, 0.22, 0.16), (0, 0, 0.08), bevel=0.006)
    for ax in ("X", "Y"):
        p.box(M["rope"], (0.305 if ax == "X" else 0.01, 0.01 if ax == "X" else 0.225, 0.165), (0, 0, 0.08), bevel=0)
    p.box(M["label_cream"], (0.1, 0.06, 0.001), (0.06, 0.04, 0.161), bevel=0)
    return p.build()


def receiver(n, relay=False):
    p = Part(n)
    p.box(M["olive"] if not relay else M["steel_dark"], (0.24, 0.14, 0.12), (0, 0, 0.06), bevel=0.01)
    p.box(M["black"], (0.2, 0.002, 0.08), (0, -0.071, 0.06), bevel=0)
    if relay:
        for k in range(4):
            p.cyl(M["lamp_green"] if k % 2 else M["lamp_red"], 0.006, 0.004, (-0.07 + k * 0.045, -0.073, 0.09), "Y", 8)
    else:
        p.cyl(M["chrome"], 0.022, 0.01, (-0.06, -0.074, 0.06), "Y", 16)
        p.box(M["glass"], (0.08, 0.003, 0.03), (0.04, -0.073, 0.075), bevel=0)
        pa.rod(p, M["chrome"], (0.1, 0.05, 0.12), (0.18, 0.08, 0.4), 0.004, 6)
    return p.build()


def rope_coil(n):
    p = Part(n)
    for k in range(5):
        pa.ring(p, M["rope"], (0, 0, 0.012 + k * 0.016), 0.1 - k * 0.002, 0.009, "Z", 24, 6)
    return p.build()


def spool(n):
    p = Part(n)
    p.cyl(M["wood_light"], 0.03, 0.006, (0, 0, 0.003), "Z", 16)
    p.cyl(M["wood_light"], 0.03, 0.006, (0, 0, 0.057), "Z", 16)
    p.cyl(M["paper"], 0.026, 0.048, (0, 0, 0.03), "Z", 16)
    return p.build()


def shell(n):
    p = Part(n)
    bm = kit.bm_sphere(0.14, 18, 10)
    for v in bm.verts:
        v.co.z = max(v.co.z, 0.0) * 0.7
        v.co.y *= 1.3
    p.add(bm, col("8a7a5a", 0.6, 0.8), (0, 0, 0))
    for k in range(6):
        pa.ring(p, col("6a5a3a", 0.6), (0, -0.12 + k * 0.05, 0.0), 0.14 * math.sqrt(max(0.05, 1 - ((-0.12 + k * 0.05) / 0.18) ** 2)), 0.004, "Y", 20, 4)
    return p.build()


def chitin(n):
    p = Part(n)
    bm = kit.bm_sphere(0.1, 14, 8)
    for v in bm.verts:
        v.co.z = max(v.co.z, 0.0) * 0.35
    p.add(bm, col("2a3832", 0.25, 0.5), (0, 0, 0))
    return p.build()


def venom(n):
    p = Part(n)
    p.add(kit.bm_rock(0.025, 2, 0.8, 2), col("8aa040", 0.15), (0, 0, 0.02))
    return p.build()


def feather(n):
    p = Part(n)
    pa.rod(p, M["bone"], (-0.12, 0, 0.004), (0.12, 0, 0.006), 0.0015, 4)
    secs = [[(x, -w, 0.003), (x, w, 0.003), (x, w, 0.005), (x, -w, 0.005)] for x, w in ((-0.08, 0.01), (0.0, 0.025), (0.1, 0.012))]
    p.add(kit.bm_loft(secs), col("3a3632", 0.8))
    return p.build()


def ring_item(n):
    p = Part(n)
    p.add(kit.bm_torus(0.011, 0.0025, 20, 6), M["gold"], (0, 0, 0.003))
    return p.build()


def brick(n, hexc):
    p = Part(n)
    p.box(col(hexc, 0.85, 0.8), (0.22, 0.1, 0.065), (0, 0, 0.0325), bevel=0.004)
    return p.build()


def ice(n):
    p = Part(n)
    p.box(M["glass_clear"], (0.2, 0.2, 0.16), (0, 0, 0.08), bevel=0.02)
    return p.build()


def birthday_machine(n):
    p = Part(n)
    p.box(M["plastic_red"], (0.16, 0.12, 0.1), (0, 0, 0.05), bevel=0.01)
    p.cyl(M["chrome"], 0.012, 0.08, (0, 0, 0.14), "Z", 10)
    for k in range(4):
        a = k / 4 * math.tau
        p.box(M["plastic_yellow"], (0.08, 0.02, 0.004), (math.cos(a) * 0.04, math.sin(a) * 0.04, 0.18), (0, 0, a), bevel=0.001)
    p.sphere(M["lamp_amber"], 0.012, (0.05, -0.06, 0.08))
    p.sphere(M["lamp_green"], 0.012, (-0.05, -0.06, 0.08))
    return p.build()


def saddlebags(n):
    p = Part(n)
    for s in (-1, 1):
        p.box(M["leather"], (0.1, 0.32, 0.28), (s * 0.12, 0, 0.14), bevel=0.03)
        p.box(M["leather"], (0.105, 0.3, 0.12), (s * 0.12, 0, 0.24), (s * 0.15, 0, 0), bevel=0.02)
        p.box(M["brass"], (0.004, 0.03, 0.03), (s * 0.172, 0, 0.18), bevel=0.002)
    p.box(M["leather"], (0.18, 0.3, 0.01), (0, 0, 0.29), bevel=0.003)
    return p.build()


def ore_sack(n, hexc):
    return I.sack(n, M["burlap"], hexc, 0.26, 0.34, 6)


MISC = {
    "misc_birthday_machine": (birthday_machine, "Birthday machine"), "misc_blade": (turbine_blade, "Turbine blade"),
    "misc_bolts": (lambda n: nuts_box(n, "bolts"), "Bolts"), "misc_nails": (lambda n: nuts_box(n, "nails"), "Nails"),
"misc_castings": (castings, "Castings"), "misc_chitin": (chitin, "Chitin plate"),
    "misc_coil": (coil_item, "Generator coil"), "misc_delivery_chit": (lambda n: I.document(n, M["paper_old"], 0.1, 0.07, 1), "Delivery chit"),
    "misc_engine_block": (engine_block, "Engine block"), "misc_feather": (feather, "Feather"), "misc_gold_ring": (ring_item, "Gold ring"),
    "misc_green_brick": (lambda n: brick(n, "4a6a3a"), "Green brick"), "misc_ice": (ice, "Ice block"),
    "misc_iron_concentrate": (lambda n: ore_sack(n, "4a3a34"), "Iron concentrate"), "misc_paper": (lambda n: I.document(n, M["paper"], 0.21, 0.297, 6), "Paper"),
    "misc_parcel": (parcel, "Parcel"), "misc_receiver": (receiver, "June's receiver"), "misc_relay_module": (lambda n: receiver(n, True), "Relay module"),
    "misc_rope": (rope_coil, "Rope"), "misc_saddlebags": (saddlebags, "Saddlebags"), "misc_shell": (shell, "Armadillo shell"),
    "misc_silk": (spool, "Spider silk"), "misc_solar_cell": (solar_cell, "Solar cell"), "misc_venom": (venom, "Venom sac"),
}


def _bone(n):
    p = Part(n)
    pa.rod(p, M["bone"], (-0.1, 0, 0.018), (0.1, 0, 0.018), 0.012, 10)
    for x in (-0.11, 0.11):
        for y in (-0.014, 0.014):
            p.sphere(M["bone"], 0.017, (x, y, 0.018), 10, 6)
    return p.build()


MISC["misc_bone"] = (_bone, "Bone")
for k, (fn, lab) in MISC.items():
    reg(k, "Other", (lambda n=k, f=fn: f(n)), lab)


# ------------------------------------------------------------------------------------------------ seeds, saplings, crops
SEEDS = {"seed_corn": "e0c040", "seed_potato": "a88050", "seed_tomato": "c83020", "seed_carrot": "e07020", "seed_cabbage": "9ac060",
         "seed_pumpkin": "e08020", "seed_sunflower": "f0c020", "seed_cotton": "eeeadc", "seed_hemp": "6a8a3a", "seed_herbs": "6aa040",
         "seed_flower": "d04070", "seed_berries": "6a1c50", "seed_wheat": "d8b860", "seed_beet": "7a2440", "seed_mushroom": "d8c8a8", "seed_oil": "c8a030"}


def seed_packet(n, hexc):
    p = Part(n)
    p.box(M["paper_old"], (0.09, 0.13, 0.008), (0, 0, 0.004), bevel=0.002)
    p.box(col(hexc, 0.6), (0.06, 0.05, 0.001), (0, 0.01, 0.0085), bevel=0)
    p.box(M["paper_old"], (0.09, 0.03, 0.002), (0, 0.065, 0.009), (0.2, 0, 0), bevel=0)
    return p.build()


def sapling(n, leaf_hex, palm=False):
    p = Part(n)
    p.lathe(M["rust"], [(0.0, 0.0), (0.06, 0.0), (0.08, 0.12), (0.085, 0.13), (0.0, 0.13)], (0, 0, 0), "Z", 18)
    p.cyl(col("4a3a24", 0.95, 1.0), 0.075, 0.01, (0, 0, 0.12), "Z", 18)
    pa.rod(p, M["wood_dark"], (0, 0, 0.12), (0.01, 0, 0.42), 0.008, 6)
    r = I.rnd(len(n))
    for k in range(6 if not palm else 5):
        a = k / 6 * math.tau
        if palm:
            p.add(kit.bm_box(0.18, 0.03, 0.003, bevel=0.001), col(leaf_hex, 0.7), (math.cos(a) * 0.08, math.sin(a) * 0.08, 0.4), (0, 0.4, a))
        else:
            p.add(kit.bm_rock(0.04, k, 0.8, 1), col(leaf_hex, 0.8, 0.8), (math.cos(a) * 0.04, math.sin(a) * 0.04, 0.3 + r.random() * 0.12))
    return p.build()


def sheaf(n, hexc, head=True):
    p = Part(n)
    r = I.rnd(len(n))
    for k in range(14):
        y, z = r.uniform(-0.03, 0.03), 0.02 + r.uniform(0, 0.03)
        pa.rod(p, col(hexc, 0.8), (-0.3, y, z), (0.3, y * 1.8, z + r.uniform(-0.01, 0.02)), 0.003, 4)
        if head:
            p.add(kit.bm_cyl(0.008, 0.06, 6, 0.0, 0.002), col(hexc, 0.8, 0.8), (0.33, y * 1.8, z), (0, math.pi / 2, 0))
    pa.ring(p, M["rope"], (0, 0, 0.035), 0.04, 0.005, "X", 12, 4)
    return p.build()


def cotton(n):
    p = Part(n)
    r = I.rnd(3)
    for k in range(5):
        p.add(kit.bm_rock(0.035, k, 0.8, 2), M["plastic_white"], (r.uniform(-0.06, 0.06), r.uniform(-0.06, 0.06), 0.03))
    return p.build()


def flowers(n):
    p = Part(n)
    r = I.rnd(5)
    for k, c in enumerate(("d04070", "e0ac40", "fbf8ee", "d04070", "8a6ad0")):
        y = r.uniform(-0.03, 0.03)
        pa.rod(p, col("3a7024", 0.8), (-0.18, y * 0.3, 0.01), (0.12, y, 0.02 + k * 0.006), 0.003, 4)
        p.sphere(col(c, 0.6), 0.02, (0.13, y, 0.02 + k * 0.006), 10, 6, scale=(0.5, 1, 1))
    pa.ring(p, M["rope"], (-0.08, 0, 0.012), 0.016, 0.003, "X", 10, 4)
    return p.build()


for s, c in SEEDS.items():
    reg(s, "Seed", (lambda n=s, cc=c: seed_packet(n, cc)), "Seeds: " + s[5:])
reg("sapling_apple", "Seed", lambda: sapling("sapling_apple", "46862c"), "Apple sapling")
reg("sapling_pine", "Seed", lambda: sapling("sapling_pine", "2a5024"), "Pine sapling")
reg("sapling_palm", "Seed", lambda: sapling("sapling_palm", "58843c", True), "Palm sapling")
reg("crop_wheat", "Crop", lambda: sheaf("crop_wheat", "d8b860"), "Wheat sheaf")
reg("crop_hemp", "Crop", lambda: sheaf("crop_hemp", "6a8a3a", False), "Hemp")
reg("crop_cotton", "Crop", lambda: cotton("crop_cotton"), "Cotton")
reg("crop_flower", "Crop", lambda: flowers("crop_flower"), "Flowers")
reg("crop_flour", "Crop", lambda: I.sack("crop_flour", M["burlap"], "eeeadc", 0.28, 0.38, 7), "Flour sack")

per = 12
for i, (iid, cat, lab, fn) in enumerate(ITEMS):
    pa.add(iid, fn, F, kind="item", category=cat, label=lab, fit=False, origin="rest",
           world_scale_note="WorldItemModels shows icons x0.3-0.45; this model is real size", tile="%s_%02d" % (F, i // per + 1))

if __name__ == "__main__":
    pa.run(F, out_sub="items", cols=4, gap=0.08)
