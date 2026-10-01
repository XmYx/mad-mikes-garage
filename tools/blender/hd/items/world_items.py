"""HD world models that stand in for whole item groups (WorldItemModels), live-animal items and quest (story_*) items.

  world_res_fluid / world_res_sack / world_res_crate  resources lying in the world ("res:N"): a jerry can, a sack, a
        slatted crate; the material slot "ResourceContent" is tinted by ResourceInfo.Color at runtime (as the game does)
  world_kit                                           every build kit (kit_*): the flat-pack crate with its sheet
  animal_*                                            young animals as items: a carrying crate with the animal inside
  story_*                                             quest items (StoryLibrary / Story/Quests), one model per id
Real size; origin = resting point (bottom centre).

blender -b -P tools/blender/hd/items/world_items.py [-- ids]"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "parts_all"))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pa  # noqa: E402
from pa import M, Part, Vector, Matrix, kit  # noqa: E402
import ishapes as I  # noqa: E402
from ishapes import col  # noqa: E402

F = "world_items"
ITEMS = []


def reg(iid, cat, fn, label=None, **props):
    ITEMS.append((iid, cat, label or iid, fn, props))


def content():
    m = M.get("content")
    if m is None:
        m = M["content"] = kit.surface("ResourceContent", kit._hex("bbbbbb"), dust=0.05, rough=0.8, var=0.2, bump=0.6)
    return m


# ------------------------------------------------------------------------------------------------ resources and kits
def res_fluid(n):
    p = Part(n)
    W, H, D = 0.35, 0.47, 0.165
    c = content()
    p.box(c, (D, W, H), (0, 0, H / 2), bevel=0.02)
    for s in (-1, 1):
        pa.stencil_x(p, c, (s * (D / 2 + 0.003), 0, H * 0.47), H * 0.6, "X", w=0.022)
    for y in (-0.1, 0.0, 0.1):
        p.box(M["black"], (0.03, 0.022, 0.07), (0, y, H + 0.03), bevel=0.006)
    p.box(M["black"], (0.03, 0.26, 0.02), (0, 0, H + 0.07), bevel=0.006)
    p.cyl(M["steel"], 0.026, 0.04, (0, -0.15, H + 0.02), "Z", 14, bevel=0.004)
    return p.build()


def res_sack(n):
    p = Part(n)
    prof = [(0.0, 0.0), (0.15, 0.0), (0.2, 0.06), (0.2, 0.26), (0.15, 0.34), (0.09, 0.36)]
    p.add(kit.bm_lathe(prof, 18), M["burlap"])
    p.add(kit.bm_rock(0.11, 3, 0.45, 2), content(), (0, 0, 0.36))
    pa.ring(p, M["rope"], (0, 0, 0.33), 0.1, 0.008, "Z", 16, 4)
    p.box(content(), (0.12, 0.002, 0.08), (0, -0.2, 0.16), bevel=0)
    return p.build()


def res_crate(n):
    p = Part(n)
    S, H = 0.4, 0.3
    for k in range(4):
        z = 0.035 + k * 0.075
        for ax in (0, 1):
            for s in (-1, 1):
                size = (S, 0.02, 0.06) if ax == 0 else (0.02, S, 0.06)
                loc = (0, s * (S / 2 - 0.01), z) if ax == 0 else (s * (S / 2 - 0.01), 0, z)
                p.box(M["wood"] if k % 2 else M["wood_dark"], size, loc, bevel=0.004)
    p.box(M["wood_dark"], (S, S, 0.02), (0, 0, 0.01), bevel=0.003)
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.box(M["wood_dark"], (0.03, 0.03, H), (sx * (S / 2 - 0.015), sy * (S / 2 - 0.015), H / 2), bevel=0.004)
    p.add(kit.bm_rock(0.17, 5, 0.4, 2), content(), (0, 0, H - 0.02))
    p.box(content(), (0.2, 0.004, 0.05), (0, -S / 2 - 0.002, 0.11), bevel=0)
    return p.build()


def world_kit(n):
    p = Part(n)
    W, D, H = 0.56, 0.4, 0.24
    p.box(M["wood_light"], (W, D, H), (0, 0, H / 2), bevel=0.006)
    for k in range(3):
        p.box(M["wood"], (W + 0.004, 0.004, 0.06), (0, -D / 2 - 0.002, 0.04 + k * 0.08), bevel=0)
    p.box(M["label_blue"], (W * 0.7, 0.003, 0.05), (0, -D / 2 - 0.004, 0.12), bevel=0)
    p.box(M["paper"], (0.2, 0.28, 0.002), (0, 0, H + 0.001), (0, 0, 0.1), bevel=0)
    for k in range(5):
        p.box(M["black"], (0.12, 0.004, 0.0004), (0, -0.1 + k * 0.04, H + 0.0025), (0, 0, 0.1), bevel=0)
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.box(M["steel"], (0.05, 0.05, 0.05), (sx * (W / 2 - 0.02), sy * (D / 2 - 0.02), H - 0.02), bevel=0.004)
    pa.rod(p, M["webbing"], (-W / 2, 0.08, H + 0.003), (W / 2, 0.08, H + 0.003), 0.004, 4)
    return p.build()


reg("world_res_fluid", "Resource", lambda: res_fluid("world_res_fluid"), "Resource: fluid (jerry can)", tint_slot="ResourceContent", used_for="res:N fluids")
reg("world_res_sack", "Resource", lambda: res_sack("world_res_sack"), "Resource: sacked (soil, ore, powder, wool, hay)", tint_slot="ResourceContent", used_for="res:N sacked")
reg("world_res_crate", "Resource", lambda: res_crate("world_res_crate"), "Resource: crate (scrap, metals, wood ...)", tint_slot="ResourceContent", used_for="res:N other")
reg("world_kit", "Kit", lambda: world_kit("world_kit"), "Build kit (all kit_*)", used_for="kit_*")


# ------------------------------------------------------------------------------------------------ live animals (young)
ANIMALS = {"animal_chick": ("e0ac40", 0.06, False), "animal_kid": ("dcd8c8", 0.16, True), "animal_calf": ("74492a", 0.24, True),
           "animal_piglet": ("e0a0a0", 0.13, True), "animal_puppy": ("a06e1e", 0.12, True), "animal_lamb": ("eeeadc", 0.15, True),
           "animal_rabbit": ("8a7a64", 0.08, True)}


def animal_crate(n, hexc, size, legs):
    p = Part(n)
    L, W, H = size * 3.2 + 0.12, size * 2.2 + 0.1, size * 2.4 + 0.1
    for z in (0.01, H - 0.01):
        for s in (-1, 1):
            p.box(M["wood_dark"], (L, 0.03, 0.03), (0, s * (W / 2 - 0.015), z), bevel=0.004)
            p.box(M["wood_dark"], (0.03, W, 0.03), (s * (L / 2 - 0.015), 0, z), bevel=0.004)
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.box(M["wood_dark"], (0.03, 0.03, H), (sx * (L / 2 - 0.015), sy * (W / 2 - 0.015), H / 2), bevel=0.004)
    for k in range(1, 6):
        x = -L / 2 + k * L / 6
        for s in (-1, 1):
            p.box(M["wood"], (0.03, 0.015, H - 0.02), (x, s * (W / 2 - 0.01), H / 2), bevel=0.003)
    p.box(M["wood"], (L, W, 0.02), (0, 0, 0.02), bevel=0.003)
    p.add(kit.bm_rock(W * 0.3, 4, 0.25, 2), M["hay"], (0, 0, 0.04))
    body = Vector((0, 0, 0.05 + size * 0.7))
    p.add(kit.bm_sphere(size, 16, 10), col(hexc, 0.9, 0.8), body, scale=(1.3, 0.8, 0.75))
    p.add(kit.bm_sphere(size * 0.55, 14, 8), col(hexc, 0.9, 0.8), body + Vector((size * 1.2, 0, size * 0.5)))
    p.sphere(M["black"], size * 0.08, body + Vector((size * 1.55, size * 0.25, size * 0.6)), 8, 6)
    p.sphere(M["black"], size * 0.08, body + Vector((size * 1.55, -size * 0.25, size * 0.6)), 8, 6)
    p.add(kit.bm_tube([(-L / 2, 0, H + 0.04), (-L / 2 + 0.05, 0, H + 0.1), (L / 2 - 0.05, 0, H + 0.1), (L / 2, 0, H + 0.04)], 0.012, 6), M["rope"])
    ob = p.build()
    pa.marker("grip", (0, 0, H + 0.1), ob)
    return ob


for aid, (hx, sz, legs) in ANIMALS.items():
    reg(aid, "Other", (lambda n=aid, h=hx, s=sz, l=legs: animal_crate(n, h, s, l)), aid.replace("animal_", "Live ") + " (crate)",
        note="item form only; the released animal uses the AnimalLibrary model")


# ------------------------------------------------------------------------------------------------ story items
def horn(n, button=False):
    p = Part(n)
    if button:
        p.cyl(M["chrome"], 0.05, 0.02, (0, 0, 0.01), "Z", 24, bevel=0.005)
        p.cyl(M["black"], 0.035, 0.012, (0, 0, 0.025), "Z", 20, bevel=0.003)
        p.box(M["brass"], (0.03, 0.01, 0.002), (0, 0, 0.032), bevel=0)
        return p.build()
    p.lathe(M["chrome"], [(0.012, 0.0), (0.014, 0.12), (0.03, 0.2), (0.07, 0.25), (0.072, 0.26), (0.06, 0.26)], (0, 0, 0.04), "Y", 20)
    p.box(M["black"], (0.06, 0.06, 0.06), (0, -0.02, 0.04), bevel=0.008)
    return p.build()


def tin_letters(n, label="a02a2c", papers=True):
    p = Part(n)
    p.box(col(label, 0.5), (0.22, 0.14, 0.07), (0, 0, 0.035), bevel=0.006)
    p.box(M["tin"], (0.225, 0.145, 0.015), (0, 0, 0.072), bevel=0.004)
    if papers:
        p.box(M["paper_old"], (0.16, 0.1, 0.004), (0.03, 0.08, 0.002), (0, 0, 0.3), bevel=0)
    return p.build()


def envelope(n, seal="a02a2c"):
    p = Part(n)
    p.box(M["paper_old"], (0.22, 0.11, 0.004), (0, 0, 0.002), bevel=0.001)
    for s in (-1, 1):
        p.box(M["paper_old"], (0.155, 0.003, 0.002), (s * 0.055, 0.02, 0.0045), (0, 0, s * 0.45), bevel=0)
    p.cyl(col(seal, 0.4), 0.012, 0.003, (0, -0.005, 0.006), "Z", 12, bevel=0.001)
    return p.build()


def ticket(n, hexc="e0ac40"):
    p = Part(n)
    p.box(col(hexc, 0.8), (0.12, 0.05, 0.001), (0, 0, 0.0005), bevel=0)
    for k in range(3):
        p.box(M["black"], (0.06, 0.004, 0.0003), (-0.01, -0.012 + k * 0.012, 0.0012), bevel=0)
    return p.build()


def folder(n, hexc="8c5634"):
    p = Part(n)
    p.box(col(hexc, 0.8), (0.24, 0.32, 0.01), (0, 0, 0.005), bevel=0.002)
    p.box(M["paper"], (0.22, 0.3, 0.008), (0.006, 0, 0.006), bevel=0)
    p.box(col(hexc, 0.8), (0.24, 0.32, 0.002), (0, 0, 0.011), bevel=0)
    p.box(M["label_red"], (0.08, 0.03, 0.001), (0, 0.1, 0.012), bevel=0)
    return p.build()


def logger(n):
    p = Part(n)
    p.box(M["plastic_white"], (0.16, 0.12, 0.2), (0, 0, 0.1), bevel=0.01)
    for k in range(5):
        p.box(M["plastic_white"], (0.18, 0.14, 0.012), (0, 0, 0.04 + k * 0.035), bevel=0.003)
    p.box(M["glass"], (0.08, 0.002, 0.04), (0, -0.071, 0.14), bevel=0)
    pa.rod(p, M["chrome"], (0.04, 0.0, 0.2), (0.04, 0.0, 0.42), 0.004, 6)
    for k in range(3):
        a = k / 3 * math.tau
        p.sphere(M["plastic"], 0.018, (0.04 + math.cos(a) * 0.06, math.sin(a) * 0.06, 0.42), 8, 6, scale=(1, 1, 0.6))
    return p.build()


def organ_pipe(n):
    p = Part(n)
    p.lathe(M["tin"], [(0.0, 0.0), (0.012, 0.0), (0.04, 0.1), (0.04, 0.9), (0.0, 0.9)], (0, 0, 0.04), "Y", 20)
    p.box(M["black"], (0.06, 0.03, 0.012), (0, -0.25, 0.08), bevel=0.002)
    return p.build()


def instruments(n):
    p = Part(n)
    p.box(M["wood_dark"], (0.3, 0.22, 0.12), (0, 0, 0.06), bevel=0.01)
    p.box(M["felt_green"], (0.28, 0.2, 0.004), (0, 0, 0.121), bevel=0)
    p.add(kit.bm_torus(0.07, 0.006, 24, 5), M["brass"], (-0.06, 0, 0.13))
    pa.rod(p, M["brass"], (-0.06, 0, 0.13), (-0.06, 0.07, 0.13), 0.004, 6)
    p.cyl(M["brass"], 0.04, 0.02, (0.08, 0.04, 0.135), "Z", 20, bevel=0.003)
    pa.lens(p, M["glass"], (0.08, 0.04, 0.146), 0.034, "Z", 0.003)
    pa.rod(p, M["brass"], (0.04, -0.07, 0.13), (0.13, -0.07, 0.13), 0.01, 10)
    return p.build()


def photo(n):
    p = Part(n)
    p.box(M["wood_dark"], (0.18, 0.015, 0.24), (0, 0.04, 0.12), (0.2, 0, 0), bevel=0.004)
    p.box(col("8a7a64", 0.4), (0.14, 0.002, 0.2), (0, 0.03, 0.124), (0.2, 0, 0), bevel=0)
    pa.rod(p, M["steel_dark"], (0, 0.05, 0.12), (0, 0.12, 0.0), 0.004, 4)
    return p.build()


def leash(n):
    p = Part(n)
    for k in range(4):
        pa.ring(p, M["leather"], (0, 0, 0.008 + k * 0.006), 0.07 - k * 0.006, 0.006, "Z", 20, 4)
    p.add(kit.bm_torus(0.02, 0.004, 12, 4), M["steel"], (0.08, 0, 0.01))
    p.add(kit.bm_torus(0.045, 0.008, 16, 4), M["leather"], (-0.09, 0, 0.01))
    return p.build()


def pressed_flower(n):
    p = Part(n)
    p.box(M["paper_old"], (0.14, 0.2, 0.004), (0, 0, 0.002), bevel=0.001)
    pa.rod(p, col("5a6a30", 0.8), (0, -0.06, 0.005), (0.01, 0.04, 0.005), 0.002, 4)
    for k in range(5):
        a = k / 5 * math.tau
        p.box(col("b04a6a", 0.8), (0.018, 0.01, 0.001), (0.01 + math.cos(a) * 0.012, 0.05 + math.sin(a) * 0.012, 0.0045), (0, 0, a), bevel=0)
    p.box(M["glass_clear"], (0.15, 0.21, 0.003), (0, 0, 0.0055), bevel=0.001)
    return p.build()


def cloth_patch(n):
    p = Part(n)
    p.cyl(col("232127", 0.9), 0.05, 0.004, (0, 0, 0.002), "Z", 24, bevel=0.001)
    pa.ring(p, col("e0ac40", 0.8), (0, 0, 0.004), 0.048, 0.003, "Z", 24, 4)
    p.box(col("e0ac40", 0.8), (0.07, 0.008, 0.001), (0, 0, 0.0045), bevel=0)
    for k in range(3):
        p.box(col("fbf8ee", 0.8), (0.012, 0.004, 0.001), (-0.02 + k * 0.02, 0, 0.005), bevel=0)
    return p.build()


def rail(n):
    p = Part(n)
    p.box(M["wood"], (0.9, 0.08, 0.06), (0, 0, 0.03), bevel=0.006)
    p.add(kit.bm_rock(0.06, 2, 0.6, 2), col("1a1410", 0.95, 1.0), (0.4, 0, 0.04))
    p.box(col("1a1410", 0.95, 1.0), (0.3, 0.082, 0.062), (0.32, 0, 0.031), bevel=0.006)
    for x in (-0.3, 0.1):
        p.cyl(M["rust"], 0.006, 0.02, (x, 0, 0.065), "Z", 6)
    return p.build()


def carved_fish(n):
    o = I.fish(n, "93603a", l=0.2, h=0.06)
    return o


def whistle(n):
    p = Part(n)
    pa.rod(p, M["tin"], (-0.13, 0, 0.01), (0.13, 0, 0.01), 0.009, 14)
    p.box(M["plastic_green"], (0.04, 0.02, 0.014), (-0.13, 0, 0.01), bevel=0.003)
    for k in range(6):
        p.cyl(M["black"], 0.003, 0.002, (-0.06 + k * 0.026, 0, 0.019), "Z", 6)
    return p.build()


def marker_stake(n):
    p = Part(n)
    p.box(M["wood_light"], (0.04, 0.04, 0.5), (0, 0, 0.02), (0, math.pi / 2, 0), bevel=0.004)
    p.box(M["plastic_red"], (0.12, 0.002, 0.04), (0.2, -0.021, 0.02), bevel=0)
    p.add(kit.bm_cyl(0.02, 0.06, 4, 0.0, 0.0), M["wood_light"], (-0.28, 0, 0.02), (0, -math.pi / 2, 0))
    return p.build()


def village_mark(n):
    p = Part(n)
    p.cyl(M["wood"], 0.05, 0.016, (0, 0, 0.008), "Z", 6, bevel=0.003)
    p.box(M["black"], (0.05, 0.006, 0.001), (0, 0, 0.0165), (0, 0, 0.8), bevel=0)
    p.box(M["black"], (0.05, 0.006, 0.001), (0, 0, 0.0165), (0, 0, -0.8), bevel=0)
    p.add(kit.bm_torus(0.008, 0.002, 10, 4), M["rope"], (0, 0.05, 0.008))
    return p.build()


def lift_bags(n):
    p = Part(n)
    for x in (-0.12, 0.12):
        p.add(kit.bm_rock(0.12, int(x * 100) % 7, 0.35, 2), M["plastic_yellow"], (x, 0, 0.04))
    p.add(kit.bm_tube([(-0.25, 0.1, 0.02), (0.0, 0.18, 0.03), (0.25, 0.1, 0.02)], 0.012, 6), M["webbing"])
    p.add(kit.bm_torus(0.03, 0.006, 12, 4), M["steel"], (0.27, 0.08, 0.01))
    return p.build()


def haybox(n):
    p = Part(n)
    p.box(M["wood"], (0.4, 0.3, 0.3), (0, 0, 0.15), bevel=0.006)
    p.box(M["wood_dark"], (0.42, 0.32, 0.03), (0, 0, 0.31), bevel=0.004)
    p.add(kit.bm_rock(0.06, 3, 0.4, 1), M["hay"], (0.14, -0.1, 0.33))
    p.add(kit.bm_tube([(-0.06, 0, 0.325), (-0.05, 0, 0.36), (0.05, 0, 0.36), (0.06, 0, 0.325)], 0.008, 6), M["rope"])
    return p.build()


def small_can(n, hexc):
    p = Part(n)
    p.box(col(hexc, 0.5), (0.1, 0.18, 0.26), (0, 0, 0.13), bevel=0.012)
    pa.stencil_x(p, col(I.shade(hexc, 0.8), 0.5), (0.052, 0, 0.13), 0.16, "X", w=0.014)
    p.cyl(M["tin"], 0.02, 0.03, (0, -0.06, 0.27), "Z", 12, bevel=0.003)
    p.add(kit.bm_tube([(0, 0.04, 0.26), (0, 0.04, 0.3), (0, -0.02, 0.3), (0, -0.02, 0.26)], 0.008, 6), M["black"])
    return p.build()


STORY = {
    "story_a5_plan_all": (lambda n: I.document(n, M["paper"], 0.297, 0.42, 2), "Plan (all)"),
    "story_a5_plan_group": (lambda n: I.document(n, M["paper"], 0.297, 0.42, 2, seed=11), "Plan (group)"),
    "story_a5_worksheet": (lambda n: I.document(n, M["paper_old"], 0.21, 0.297, 1, seed=12), "Worksheet"),
    "story_schedule": (lambda n: I.document(n, M["paper"], 0.21, 0.297, 1, seed=13), "Schedule"),
    "story_convoy_horn": (lambda n: horn(n), "Convoy horn"),
    "story_a6_script": (lambda n: I.document(n, M["paper"], 0.21, 0.297, 8, seed=14), "Broadcast script"),
    "story_a6_dossier": (lambda n: folder(n, "8c5634"), "Dossier"),
    "story_b5_ledger": (lambda n: I.ledger(n, "3a4c44"), "Ledger"),
    "story_bill_of_sale": (lambda n: I.document(n, M["paper_old"], 0.15, 0.21, 1, True, seed=15), "Bill of sale"),
    "story_varga_note": (lambda n: I.document(n, M["paper_old"], 0.1, 0.14, 1, True, seed=16), "Varga's note"),
    "story_c1_slip": (lambda n: ticket(n, "dcd8c8"), "Slip"),
    "story_c3_can": (lambda n: small_can(n, "3a6aa0"), "Steriliser can"),
    "story_c3_posted": (lambda n: I.document(n, M["paper"], 0.21, 0.297, 1, seed=17), "Posted shares (copy)"),
    "story_c3_mark": (village_mark, "The village's mark"),
    "story_c3_agreement": (lambda n: I.document(n, M["paper_old"], 0.21, 0.297, 3, True, seed=18), "Agreement"),
    "story_c5_crate": (lambda n: I.crate_small(n, "c48c2a"), "Crate"),
    "story_f1_can": (lambda n: small_can(n, "4e6458"), "Water can"),
    "story_f1_posted": (lambda n: I.document(n, M["paper"], 0.21, 0.297, 1, seed=19), "Posted fuel week (copy)"),
    "story_f1_nowater": (lambda n: I.document(n, M["paper_old"], 0.3, 0.3, 1, True, seed=20), "No-water route (map)"),
    "story_build_sheet": (lambda n: I.document(n, M["label_blue"], 0.3, 0.42, 1, seed=21), "Harlan's build sheet"),
    "story_flight_booklet": (lambda n: I.book(n, "34508a", w=0.12, h=0.18, t=0.012, title="e0ac40"), "Flight maintenance booklet"),
    "story_grist_marker": (marker_stake, "Grist's marker"),
    "story_harlan_horn": (lambda n: horn(n, button=True), "Harlan's horn button"),
    "story_haybox": (haybox, "Haybox"),
    "story_recipe_card": (lambda n: I.document(n, M["label_cream"], 0.13, 0.08, 1, seed=22), "Recipe card"),
    "story_letter_hal": (lambda n: envelope(n, "a02a2c"), "Letter (Hal)"),
    "story_letter_ida": (lambda n: envelope(n, "34508a"), "Letter (Ida)"),
    "story_letter_pell": (lambda n: envelope(n, "58843c"), "Letter (Pell)"),
    "story_lift_bags": (lift_bags, "Lift bags and sling"),
    "story_logger": (logger, "Weather logger"),
    "story_order_book": (lambda n: I.ledger(n, "7c1c22"), "Ruth's old order book"),
    "story_organ_pipe": (organ_pipe, "Organ pipe"),
    "story_p1_list": (lambda n: I.document(n, M["paper_old"], 0.15, 0.21, 1, seed=23), "Nell's parts list"),
    "story_p2_tally": (lambda n: I.document(n, M["paper"], 0.21, 0.297, 1, seed=24), "Gate tally sheet"),
    "story_p3_cargo": (lambda n: tin_letters(n, "4e6458", False), "Alba's cargo manifest tin"),
    "story_p3_instruments": (instruments, "Meridian's brass instruments"),
    "story_p3_letters": (lambda n: tin_letters(n, "a02a2c"), "A tin of letters"),
    "story_p3_photo": (photo, "A framed photograph"),
    "story_penny_leash": (leash, "Penny's old leash"),
    "story_pressed_flower": (pressed_flower, "Sam's pressed flower"),
    "story_race_ticket": (lambda n: ticket(n, "e0ac40"), "Race ticket"),
    "story_courier_medal": (lambda n: I.medal(n, "silver", "34508a"), "Courier medal"),
    "story_recital_tape": (lambda n: I.cassette(n, "c47a40"), "Recital tape"),
    "story_road_patch": (cloth_patch, "Dax's road patch"),
    "story_s02_rail": (rail, "Half-burnt fence rail"),
    "story_s05_medal": (lambda n: I.medal(n, "gold", "a02a2c"), "Range precision medal"),
    "story_s07_ornament": (carved_fish, "Carved fish ornament"),
    "story_s10_medal": (lambda n: I.medal(n, "brass", "58843c"), "Hill trial medal"),
    "story_s23_ledger": (lambda n: I.ledger(n, "302d33"), "Brick's debt book"),
    "story_tin_whistle": (whistle, "Tin whistle"),
}
for k, (fn, lab) in STORY.items():
    reg(k, "Story", (lambda n=k, f=fn: f(n)), lab)

for i, (iid, cat, lab, fn, props) in enumerate(ITEMS):
    pa.add(iid, fn, F, kind="item", category=cat, label=lab, fit=False, origin="rest", tile="%s_%02d" % (F, i // 13 + 1), **props)

if __name__ == "__main__":
    pa.run(F, out_sub="items", cols=4, gap=0.08)
