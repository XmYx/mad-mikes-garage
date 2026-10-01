"""HD hand tools, hand weapons and hand fluid containers (ToolLibrary.Order + block tools RoadsTools / HusbandryTools /
MedMineTools / FluidTools). One root per tool id.

Tool convention (ToolLibrary): origin = the grip (where the hand closes), the tool runs along the arm away from the
hand = game -Y (Blender -Z), its working face / edge points game +Z (Blender -Y). Hand scale (the game lays tools at
0.7 x on the ground). Each tool is fitted to the extent of its voxel model (the boxes listed in VOX, voxel units).

blender -b -P tools/blender/hd/items/tools.py [-- ids]"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "parts_all"))
import pa  # noqa: E402
from pa import M, Part, Vector, Matrix, kit  # noqa: E402

F = "tools"
S = 0.08

# voxel extents of the game's tool models (min x, y, z, max x, y, z), read off ToolLibrary.MeshFor and the block tools
VOX = {
    "tool_sledgehammer": (-1, -15, -3, 1, 0, 2), "tool_wrench": (-1, -12, 0, 1, 0, 0), "tool_cutter": (-1, -14.6, -1.6, 2, 0, 3.6),
    "tool_pipe_club": (-1, -12, -1, 1, 0, 1), "tool_machete": (-1, -12, 0, 1, 0, 2), "tool_pipe_shotgun": (-1, -14, -2, 1, 3, 1),
    "tool_claw_hammer": (0, -10, -2, 0, 0, 2), "tool_shovel": (-2, -17, 0, 2, 1, 0), "tool_axe": (0, -14, -1, 0, 0, 3),
    "tool_pickaxe": (0, -14, -6, 0, 0, 6), "tool_torch": (-1, -14, -1, 1, 0, 1), "tool_gas_torch": (-1.4, -10, -1.4, 1.4, 0, 1.4),
    "tool_lantern": (-1, -6, -1, 1, 0, 1), "tool_crowbar": (0, -14, 0, 0, 1, 2), "tool_welder": (-1, -10, -1, 2, 1, 1),
    "tool_jack": (-1, -9, -1, 5, 0, 1), "tool_binoculars": (-2, -4, -1, 2, 0, 1), "tool_geiger": (-1, -8, -1, 1, 0, 2),
    "tool_flashlight": (-1.7, -9, -1.7, 1.7, 0, 1.7), "tool_detector": (-2.4, -15.4, -1, 2.4, 0, 3), "tool_hoe": (-2, -17, 0, 2, 0, 1),
    "tool_watering_can": (-2.2, -7, -2.2, 2.2, 0, 7), "tool_fishing_rod": (-2, -24, -0.2, 3, 1, 2.2), "tool_spear": (0, -30, -1, 0, 2, 1),
    "tool_nail_bat": (-2, -13, -2, 1, 0, 1), "tool_knife": (0, -9, -1, 0, 0, 1), "tool_leaf_blade": (0, -16, -1, 0, 0, 2),
    "tool_slingshot": (-2, -8, 0, 2, 0, 1), "tool_bow": (0, -3, -9, 0, 1, 9), "tool_crossbow": (-6, -14, -1, 6, 1, 1),
    "tool_pipe_pistol": (0, -8, -1, 0, 1, 1), "tool_revolver": (-1.2, -9, -1.2, 1.2, 2, 1.2), "tool_bolt_rifle": (-1, -22, -1, 1, 4, 2),
    "tool_flare_gun": (-1.5, -6, -1.5, 1.5, 1, 1.5), "tool_grapple": (-3, -7, -2.2, 3, 3, 3),
    "tool_rake": (-5, -18, 0, 5, 1, 1), "tool_tamper": (-3, -17, -3, 3, 1, 3), "tool_line_painter": (-2.2, -17, -2.2, 2.2, 1, 2.2),
    "tool_scythe": (-1, -26, -1, 1, 4, 11), "tool_shears": (-1, -10, 0, 1, 0, 0), "tool_gold_pan": (-6.2, -12.2, 0, 6.2, 0, 2),
    "tool_jerrycan": (-3, -9, -1, 4, 1, 1), "tool_fuel_can": (-1, -6, -1, 3, 0, 1), "tool_bottle": (-1.1, -6, -1.1, 1.1, 0, 1.1),
    "tool_bucket": (-2.4, -8, -2.4, 2.4, 0, 2.4), "tool_oil_jug": (-1, -6, -1, 1, 1, 1),
}
for k, (x0, y0, z0, x1, y1, z1) in VOX.items():
    pa.GAME[k] = {"bounds": [[(x0 - 0.5) * S, (y0 - 0.5) * S, (z0 - 0.5) * S], [(x1 + 0.5) * S, (y1 + 0.5) * S, (z1 + 0.5) * S]], "segments": {}}


def T(y):
    """Point on the tool axis `y` metres from the grip (game -Y = down the arm = kit -Z)."""
    return Vector((0, 0, -y))


def handle(p, mat, a, b, r, grip=None, grip_len=0.14, grip_mat=None):
    pa.rod(p, mat, T(a), T(b), r, 12)
    if grip:
        pa.rod(p, grip_mat or M["black"], T(a - 0.02), T(a + grip_len), r * 1.18, 12)


def edge_blade(p, mat, edge_mat, y0, y1, w0, w1, t, fwd=1.0, x=0.0):
    """Flat blade in the YZ plane (thin in x) from y0 to y1 down the arm, width toward game +Z (kit -Y)."""
    secs = []
    for y, w in ((y0, w0), (y1, w1)):
        secs.append([(x - t / 2, 0.0, -y), (x + t / 2, 0.0, -y), (x + t / 2, -w * fwd, -y), (x - t / 2, -w * fwd, -y)])
    p.add(kit.bm_loft(secs), mat)
    pa.rod(p, edge_mat, (x, -w0 * fwd, -y0), (x, -w1 * fwd, -y1), t * 0.55, 4)


# ---------------------------------------------------------------- hand tools
def sledgehammer():
    p = Part("tool_sledgehammer")
    handle(p, M["wood"], 0.0, 1.06, 0.026, grip=True, grip_len=0.2)
    h = T(1.12)
    p.box(M["steel_dark"], (0.16, 0.36, 0.17), h + Vector((0, 0.02, 0)), bevel=0.02)
    for s in (-1, 1):
        p.box(M["chrome"], (0.17, 0.02, 0.18), h + Vector((0, s * 0.19 + 0.02, 0)), bevel=0.01)
    p.cyl(M["steel"], 0.034, 0.03, h + Vector((0, 0.0, 0.09)), "Z", 12, bevel=0.004)          # wedge
    return p.build()


def wrench():
    p = Part("tool_wrench")
    p.box(M["chrome"], (0.07, 0.02, 0.84), T(0.42), bevel=0.008)
    jaw = T(0.92)
    p.add(kit.bm_box(0.2, 0.024, 0.14, bevel=0.01), M["chrome"], jaw)
    p.box(M["chrome"], (0.07, 0.024, 0.08), jaw + Vector((-0.07, 0, -0.08)), bevel=0.01)
    p.box(M["chrome"], (0.07, 0.024, 0.08), jaw + Vector((0.07, 0, -0.08)), bevel=0.01)
    p.cyl(M["steel_dark"], 0.04, 0.03, jaw + Vector((0, 0, 0.04)), "Y", 16, bevel=0.004)   # adjuster
    p.box(M["black"], (0.08, 0.03, 0.22), T(0.1), bevel=0.012)
    return p.build()


def cutter():
    """tool_cutter: salvage cutter, shaft with a grip, amber motor housing, chrome cut-off disc with a guard."""
    p = Part("tool_cutter")
    handle(p, M["steel_dark"], 0.0, 0.7, 0.025)
    p.box(M["black"], (0.18, 0.12, 0.2), T(0.08) + Vector((0, -0.04, 0)), bevel=0.02)
    p.box(M["plastic_yellow"], (0.22, 0.22, 0.26), T(0.84), bevel=0.03)
    for k in range(5):
        p.box(M["black"], (0.225, 0.01, 0.02), T(0.76 + k * 0.035), bevel=0)
    d = T(0.96) + Vector((0.16, -0.08, 0))
    p.cyl(M["chrome"], 0.2, 0.012, d, "X", 40, bevel=0.002)
    p.cyl(M["steel_dark"], 0.04, 0.03, d, "X", 16, bevel=0.004)
    p.add(kit.bm_lathe([(0.21, -0.02), (0.22, 0.0), (0.22, 0.03), (0.0, 0.03)], 24, "X"), M["plastic_yellow"], d + Vector((-0.04, 0, 0)), (0, 0, 0))
    return p.build()


def pipe_club():
    p = Part("tool_pipe_club")
    pa.rod(p, M["steel"], T(0.0), T(0.92), 0.028, 14)
    p.cyl(M["label_cream"], 0.032, 0.26, T(0.12), "Z", 14, bevel=0.004)                       # tape grip
    for k in range(6):
        pa.ring(p, M["paper_old"], T(0.02 + k * 0.045), 0.033, 0.004, "Z", 14, 4)
    p.cyl(M["rust"], 0.06, 0.14, T(0.96), "Z", 16, bevel=0.01)                                  # pipe fitting
    p.cyl(M["rust"], 0.05, 0.05, T(1.04), "Z", 6, bevel=0.006)
    return p.build()


def claw_hammer():
    p = Part("tool_claw_hammer")
    handle(p, M["wood_light"], 0.0, 0.76, 0.022)
    h = T(0.8)
    p.box(M["steel"], (0.05, 0.14, 0.07), h + Vector((0, -0.04, 0)), bevel=0.01)
    p.cyl(M["chrome"], 0.035, 0.05, h + Vector((0, -0.12, 0)), "Y", 16, bevel=0.006)           # face
    for s in (-1, 1):
        p.add(kit.bm_tube([h + Vector((s * 0.012, 0.03, 0)), h + Vector((s * 0.014, 0.1, 0.03)), h + Vector((s * 0.016, 0.16, 0.08))], [0.016, 0.012, 0.004], 8), M["steel"])
    return p.build()


def shovel():
    p = Part("tool_shovel")
    handle(p, M["wood"], 0.0, 1.0, 0.022)
    p.add(kit.bm_tube([T(-0.06) + Vector((-0.12, 0, 0)), T(-0.1) + Vector((-0.1, 0, 0)), T(-0.1) + Vector((0.1, 0, 0)), T(-0.06) + Vector((0.12, 0, 0))], 0.014, 8), M["black"])
    pa.rod(p, M["black"], T(-0.06) + Vector((-0.12, 0, 0)), T(0.04), 0.012, 6)
    pa.rod(p, M["black"], T(-0.06) + Vector((0.12, 0, 0)), T(0.04), 0.012, 6)
    blade = []
    for y, w, dish in ((1.02, 0.12, 0.0), (1.1, 0.19, 0.02), (1.3, 0.2, 0.03), (1.4, 0.12, 0.02)):
        blade.append([(-w, dish, -y), (0, -0.0, -y), (w, dish, -y), (w, dish + 0.008, -y), (0, 0.008, -y), (-w, dish + 0.008, -y)])
    p.add(kit.bm_loft(blade), M["steel"])
    p.box(M["chrome"], (0.2, 0.012, 0.02), T(1.4) + Vector((0, 0.02, 0)), bevel=0.003)
    p.cyl(M["steel"], 0.03, 0.12, T(1.0), "Z", 12, bevel=0.004)
    return p.build()


def axe():
    p = Part("tool_axe")
    handle(p, M["wood"], 0.0, 1.12, 0.022)
    h = T(1.08)
    p.box(M["steel_dark"], (0.05, 0.08, 0.1), h, bevel=0.01)
    blade = [[(-0.022, -0.02, -0.98), (0.022, -0.02, -0.98), (0.004, -0.3, -0.92), (-0.004, -0.3, -0.92)],
             [(-0.022, -0.02, -1.18), (0.022, -0.02, -1.18), (0.004, -0.3, -1.22), (-0.004, -0.3, -1.22)]]
    p.add(kit.bm_loft(blade), M["steel"])
    pa.rod(p, M["chrome"], (0, -0.3, -0.92), (0, -0.3, -1.22), 0.004, 4)
    return p.build()


def pickaxe():
    p = Part("tool_pickaxe")
    handle(p, M["wood"], 0.0, 1.1, 0.024)
    h = T(1.14)
    p.box(M["steel_dark"], (0.06, 0.1, 0.1), h, bevel=0.012)
    for s in (-1, 1):
        p.add(kit.bm_tube([h, h + Vector((0, s * 0.25, 0.04)), h + Vector((0, s * 0.48, 0.12))], [0.03, 0.022, 0.004], 8), M["steel"])
    return p.build()


def torch():
    p = Part("tool_torch")
    handle(p, M["wood_dark"], 0.0, 0.8, 0.024)
    p.add(kit.bm_rock(0.07, 3, 1.6, 2), M["canvas"], T(0.9))
    for k in range(3):
        pa.ring(p, M["rope"], T(0.84 + k * 0.05), 0.065, 0.008, "Z", 14, 4)
    p.add(kit.bm_cyl(0.06, 0.2, 10, 0.0, 0.0), M["flame"], T(1.02) + Vector((0, 0, 0)), (math.pi, 0, 0))
    return p.build()


def gas_torch():
    p = Part("tool_gas_torch")
    p.lathe(M["rust"], [(0.0, 0.0), (0.09, 0.0), (0.11, -0.04), (0.11, -0.42), (0.08, -0.5), (0.0, -0.5)], (0, 0, 0), "Z", 24)
    p.box(M["label_yellow"], (0.01, 0.12, 0.18), (0.11, 0, -0.26), bevel=0.002)
    p.cyl(M["brass"], 0.03, 0.08, T(0.54), "Z", 12, bevel=0.004)
    p.cyl(M["black"], 0.025, 0.04, T(0.56) + Vector((0.04, 0, 0)), "X", 12, bevel=0.004)         # valve knob
    pa.rod(p, M["chrome"], T(0.58), T(0.76), 0.012, 10)
    p.add(kit.bm_cyl(0.025, 0.08, 10, 0.0, 0.0), M["arc"], T(0.82), (math.pi, 0, 0))
    return p.build()


def lantern():
    p = Part("tool_lantern")
    p.add(kit.bm_tube([(-0.05, 0, -0.16), (-0.06, 0, -0.04), (0, 0, 0.0), (0.06, 0, -0.04), (0.05, 0, -0.16)], 0.006, 6), M["steel_dark"])
    p.lathe(M["steel_dark"], [(0.0, -0.14), (0.05, -0.14), (0.085, -0.18), (0.08, -0.2), (0.0, -0.2)], (0, 0, 0), "Z", 20)
    p.lathe(M["glass_clear"], [(0.06, -0.2), (0.075, -0.28), (0.075, -0.38), (0.06, -0.44)], (0, 0, 0), "Z", 20)
    p.sphere(M["flame"], 0.03, (0, 0, -0.33), scale=(1, 1, 1.6))
    for a in range(4):
        aa = a / 4 * math.tau + 0.4
        pa.rod(p, M["steel_dark"], (math.cos(aa) * 0.08, math.sin(aa) * 0.08, -0.2), (math.cos(aa) * 0.08, math.sin(aa) * 0.08, -0.44), 0.005, 4)
    p.lathe(M["steel_dark"], [(0.0, -0.44), (0.1, -0.44), (0.1, -0.5), (0.0, -0.5)], (0, 0, 0), "Z", 20)
    return p.build()


def crowbar():
    p = Part("tool_crowbar")
    pa.rod(p, M["rust"], T(-0.02), T(1.1), 0.016, 6)
    p.add(kit.bm_tube([T(0.0), T(-0.06) + Vector((0, -0.02, 0)), T(-0.08) + Vector((0, -0.1, 0)), T(-0.04) + Vector((0, -0.16, 0))], 0.016, 6), M["rust"])
    p.add(kit.bm_box(0.04, 0.012, 0.06, bevel=0.003), M["chrome"], T(1.14) + Vector((0, -0.03, 0)), (0.5, 0, 0))
    return p.build()


def welder():
    p = Part("tool_welder")
    p.cyl(M["black"], 0.03, 0.32, T(0.12), "Z", 14, bevel=0.006)
    p.box(M["steel_dark"], (0.14, 0.14, 0.1), T(0.24), bevel=0.012)
    pa.rod(p, M["chrome"], T(0.3), T(0.74), 0.01, 8)
    p.cyl(M["copper"], 0.012, 0.04, T(0.76), "Z", 8)
    p.sphere(M["arc"], 0.022, T(0.8))
    p.add(kit.bm_tube([T(-0.02), T(-0.06) + Vector((0.06, 0, 0)), T(-0.02) + Vector((0.14, 0, 0))], 0.014, 8), M["plastic_red"])
    return p.build()


def jack():
    p = Part("tool_jack")
    p.lathe(M["plastic_red"], [(0.0, 0.0), (0.1, 0.0), (0.1, -0.06), (0.08, -0.08), (0.08, -0.36), (0.1, -0.38), (0.1, -0.4), (0.0, -0.4)], (0, 0, 0), "Z", 24)
    p.box(M["steel_dark"], (0.22, 0.22, 0.03), T(0.0), bevel=0.006)
    pa.rod(p, M["chrome"], T(0.4), T(0.66), 0.035, 16)
    p.cyl(M["steel_dark"], 0.06, 0.03, T(0.7), "Z", 16, bevel=0.004)
    pa.rod(p, M["steel"], (0.1, 0, -0.16), (0.44, 0, -0.16), 0.012, 8)
    p.box(M["steel_dark"], (0.04, 0.06, 0.06), (0.1, 0, -0.16), bevel=0.006)
    return p.build()


def binoculars():
    p = Part("tool_binoculars")
    for s in (-1, 1):
        p.lathe(M["plastic"], [(0.0, 0.0), (0.04, 0.0), (0.05, -0.04), (0.05, -0.22), (0.06, -0.26), (0.06, -0.32), (0.0, -0.32)], (s * 0.1, 0, 0), "Z", 20)
        pa.lens(p, M["glass"], (s * 0.1, 0, -0.322), 0.05, "-Z", 0.006)
        p.lathe(M["rubber"], [(0.03, 0.0), (0.042, 0.0), (0.042, 0.04), (0.03, 0.04)], (s * 0.1, 0, 0), "Z", 16)
    p.box(M["steel_dark"], (0.12, 0.04, 0.12), T(0.14), bevel=0.01)
    p.cyl(M["steel"], 0.02, 0.04, T(0.04), "Y", 12, bevel=0.003)
    return p.build()


def geiger():
    p = Part("tool_geiger")
    p.box(M["hazard"], (0.2, 0.22, 0.38), T(0.2), bevel=0.03)
    p.box(M["glass"], (0.12, 0.012, 0.1), T(0.16) + Vector((0, -0.112, 0)), bevel=0.004)
    p.box(M["black"], (0.004, 0.002, 0.07), T(0.16) + Vector((0.02, -0.12, 0)), (0, 0.6, 0), bevel=0)
    p.cyl(M["black"], 0.02, 0.02, T(0.32) + Vector((-0.05, -0.115, 0)), "Y", 12, bevel=0.003)
    p.add(kit.bm_tube([T(0.02) + Vector((0, 0, 0.0)), T(0.0) + Vector((0, 0.02, 0.0))], 0.012, 6), M["black"])
    pa.rod(p, M["steel"], T(0.4), T(0.68), 0.02, 12)
    p.cyl(M["black"], 0.024, 0.06, T(0.42), "Z", 12, bevel=0.004)
    p.tube(M["black"], [T(0.42) + Vector((0.02, 0, 0)), T(0.34) + Vector((0.1, 0.02, 0))], 0.006, 6)
    return p.build()


def flashlight():
    p = Part("tool_flashlight")
    p.lathe(M["steel_dark"], [(0.0, 0.0), (0.08, 0.0), (0.09, -0.02), (0.09, -0.5), (0.13, -0.58), (0.13, -0.68), (0.0, -0.68)], (0, 0, 0), "Z", 24)
    for k in range(6):
        pa.ring(p, M["black"], T(0.08 + k * 0.06), 0.092, 0.006, "Z", 20, 4)
    pa.ring(p, M["chrome"], T(0.68), 0.13, 0.01, "Z", 24, 5)
    pa.lens(p, M["lamp"], T(0.685), 0.12, "-Z", 0.01)
    p.box(M["black"], (0.03, 0.04, 0.05), T(0.2) + Vector((0, -0.09, 0)), bevel=0.006)
    return p.build()


def detector():
    p = Part("tool_detector")
    pa.rod(p, M["steel"], T(0.0), T(1.0), 0.016, 10)
    p.box(M["plastic"], (0.12, 0.12, 0.14), T(0.12) + Vector((0, -0.1, 0)), bevel=0.02)
    p.cyl(M["lamp_amber"], 0.015, 0.01, T(0.1) + Vector((0, -0.165, 0)), "Y", 10)
    p.cyl(M["black"], 0.03, 0.06, T(0.02) + Vector((0, 0.04, 0)), "Y", 10, bevel=0.004)          # arm cuff
    c = T(1.06) + Vector((0, -0.12, 0))
    pa.ring(p, M["ochre"], c, 0.17, 0.02, "Y", 28, 6)
    p.cyl(M["ochre"], 0.15, 0.01, c, "Y", 28, bevel=0)
    pa.rod(p, M["steel"], T(1.0), c + Vector((0, 0, 0.06)), 0.012, 8)
    p.tube(M["black"], [T(0.18) + Vector((0, -0.04, 0)), T(0.6) + Vector((0.02, -0.02, 0)), c + Vector((0, 0, 0.12))], 0.005, 4)
    return p.build()


def hoe():
    p = Part("tool_hoe")
    handle(p, M["wood"], 0.0, 1.28, 0.02)
    h = T(1.3)
    p.cyl(M["steel"], 0.026, 0.06, h, "Z", 10, bevel=0.004)
    p.box(M["steel"], (0.36, 0.06, 0.12), h + Vector((0, -0.04, -0.04)), (0.4, 0, 0), bevel=0.006)
    p.box(M["chrome"], (0.36, 0.012, 0.02), h + Vector((0, -0.08, -0.1)), (0.4, 0, 0), bevel=0.003)
    return p.build()


def watering_can():
    p = Part("tool_watering_can")
    p.add(kit.bm_tube([(0, 0.08, -0.25), (0, 0.06, 0.0), (0, -0.06, 0.0), (0, -0.08, -0.25)], 0.014, 8), M["steel"])
    c = Vector((0, 0, -0.4))
    p.lathe(M["plastic_green"], [(0.0, 0.18), (0.16, 0.18), (0.17, 0.16), (0.17, -0.16), (0.16, -0.18), (0.0, -0.18)], c, "Z", 28)
    pa.ring(p, M["moss"] if "moss" in M else M["plastic_green"], c, 0.172, 0.008, "Z", 28, 4)
    p.tube(M["plastic_green"], [c + Vector((0, -0.14, -0.1)), c + Vector((0, -0.3, 0.05)), c + Vector((0, -0.52, 0.26))], [0.03, 0.022, 0.018], 10)
    p.lathe(M["chrome"], [(0.0, 0.0), (0.02, 0.0), (0.05, 0.05), (0.05, 0.06), (0.0, 0.06)], c + Vector((0, -0.52, 0.26)), "Y", 16, rot=(0, 0, 0))
    return p.build()


def fishing_rod():
    p = Part("tool_fishing_rod")
    p.cyl(M["canvas"], 0.022, 0.32, T(0.06), "Z", 12, bevel=0.006)                              # cork grip
    pa.rod(p, M["crimson"], T(0.2), T(1.94), 0.012, 8)
    for k in range(5):
        y = 0.5 + k * 0.32
        pa.ring(p, M["chrome"], T(y) + Vector((0, 0.025, 0)), 0.012, 0.002, "X", 10, 4)
    c = T(0.16) + Vector((0.08, -0.08, 0))
    p.cyl(M["chrome"], 0.06, 0.05, c, "X", 20, bevel=0.006)
    p.cyl(M["black"], 0.04, 0.052, c, "X", 16, bevel=0.0)
    pa.rod(p, M["black"], c + Vector((0.03, 0, 0)), c + Vector((0.09, 0.03, 0.03)), 0.006, 6)
    p.tube(M["paper"], [c + Vector((0, 0.04, 0.04)), T(0.5) + Vector((0, 0.03, 0)), T(1.94) + Vector((0, 0.015, 0))], 0.0015, 3, caps=False)
    return p.build()


def grapple():
    p = Part("tool_grapple")
    pa.rod(p, M["steel"], T(0.0), T(0.5), 0.02, 10)
    for k in range(3):
        a = k / 3 * math.tau
        d = Vector((math.cos(a), math.sin(a), 0))
        p.add(kit.bm_tube([T(0.5), T(0.56) + d * 0.1, T(0.48) + d * 0.22, T(0.36) + d * 0.24], [0.018, 0.016, 0.012, 0.004], 8), M["steel_dark"])
    for k in range(5):                                                                         # coiled rope at the grip
        pa.ring(p, M["rope"], T(-0.04 - k * 0.035), 0.15, 0.018, "Z", 24, 6)
    return p.build()


# ---------------------------------------------------------------- weapons
def machete():
    p = Part("tool_machete")
    p.cyl(M["wood_dark"], 0.024, 0.26, T(0.12), "Z", 12, bevel=0.008)
    p.box(M["steel_dark"], (0.14, 0.05, 0.02), T(0.27), bevel=0.004)
    edge_blade(p, M["steel"], M["chrome"], 0.28, 0.98, 0.15, 0.12, 0.008)
    p.add(kit.bm_cyl(0.07, 0.008, 3, 0.0), M["steel"], T(1.0) + Vector((0, -0.07, 0)), (0, math.pi / 2, 0))
    return p.build()


def shotgun():
    p = Part("tool_pipe_shotgun")
    for x in (-0.04, 0.04):
        pa.rod(p, M["steel"], (x, 0, -0.22), (x, 0, -1.16), 0.028, 14)
    p.box(M["wood"], (0.12, 0.1, 0.5), (0, 0.0, 0.0), bevel=0.02)                                 # stock
    p.box(M["wood"], (0.1, 0.16, 0.14), (0, 0.02, 0.2), (0.3, 0, 0), bevel=0.02)
    p.add(kit.bm_tube([(0, 0.06, -0.02), (0, 0.12, -0.06), (0, 0.12, -0.12), (0, 0.06, -0.16)], 0.008, 6), M["steel_dark"])
    p.box(M["rust"], (0.16, 0.08, 0.04), (0, 0, -0.66), bevel=0.008)
    p.box(M["steel_dark"], (0.14, 0.08, 0.1), (0, 0.0, -0.26), bevel=0.012)
    return p.build()


def spear():
    p = Part("tool_spear")
    handle(p, M["wood"], -0.16, 2.1, 0.022)
    for k in range(4):
        pa.ring(p, M["rope"], T(2.0 + k * 0.025), 0.026, 0.006, "Z", 12, 4)
    tip = [(0.0, 2.12), (0.05, 2.18), (0.035, 2.34), (0.0, 2.48)]
    p.add(kit.bm_lathe([(r, -y) for r, y in tip], 4, "Z"), M["steel"], (0, 0, 0), (0, 0, math.pi / 4))
    return p.build()


def nail_bat():
    p = Part("tool_nail_bat")
    p.cyl(M["black"], 0.026, 0.26, T(0.12), "Z", 12, bevel=0.006)
    p.lathe(M["wood_light"], [(0.024, -0.24), (0.04, -0.4), (0.06, -0.8), (0.06, -1.02), (0.0, -1.04)], (0, 0, 0), "Z", 18)
    import random
    rnd = random.Random(7)
    for k in range(16):
        y = 0.5 + rnd.random() * 0.48
        a = rnd.random() * math.tau
        d = Vector((math.cos(a), math.sin(a), 0))
        base = T(y) + d * 0.05
        pa.rod(p, M["chrome"], base, base + d * 0.08, 0.004, 4)
        p.cyl(M["chrome"], 0.008, 0.004, base + d * 0.08, "Z", 6)
    return p.build()


def knife():
    p = Part("tool_knife")
    p.cyl(M["black"], 0.02, 0.26, T(0.12), "Z", 12, bevel=0.006)
    p.box(M["steel"], (0.03, 0.1, 0.02), T(0.27), bevel=0.003)
    edge_blade(p, M["steel"], M["chrome"], 0.28, 0.68, 0.08, 0.02, 0.006)
    return p.build()


def leaf_blade():
    p = Part("tool_leaf_blade")
    p.cyl(M["leather"], 0.022, 0.26, T(0.12), "Z", 12, bevel=0.006)
    for k in range(5):
        pa.ring(p, M["leather"], T(0.02 + k * 0.05), 0.024, 0.004, "Z", 12, 4)
    p.box(M["steel_dark"], (0.04, 0.14, 0.02), T(0.27) + Vector((0, -0.03, 0)), bevel=0.004)
    edge_blade(p, M["rust"], M["chrome"], 0.28, 1.28, 0.14, 0.06, 0.014)
    return p.build()


def slingshot():
    p = Part("tool_slingshot")
    handle(p, M["wood"], 0.0, 0.36, 0.022)
    for s in (-1, 1):
        p.add(kit.bm_tube([T(0.36), T(0.44) + Vector((s * 0.08, 0, 0)), T(0.62) + Vector((s * 0.15, 0, 0))], [0.022, 0.018, 0.016], 10), M["wood"])
    p.tube(M["plastic_red"], [T(0.6) + Vector((-0.15, 0, 0)), T(0.6) + Vector((0, -0.05, 0)), T(0.6) + Vector((0.15, 0, 0))], 0.006, 6)
    p.box(M["leather"], (0.05, 0.012, 0.03), T(0.6) + Vector((0, -0.055, 0)), bevel=0.004)
    return p.build()


def bow():
    p = Part("tool_bow")
    arc = [Vector((0, -u * 0.74, -(1 - u * u) * 0.24)) for u in [(-1 + k / 8) for k in range(17)]]
    p.add(kit.bm_tube(arc, [0.01 + 0.012 * (1 - abs(-1 + k / 8)) for k in range(17)], 8), M["wood"])
    pa.rod(p, M["cream"], arc[0], arc[-1], 0.003, 4)
    p.cyl(M["black"], 0.026, 0.12, (0, 0, -0.24), "Y", 12, bevel=0.004)
    return p.build()


def crossbow():
    p = Part("tool_crossbow")
    p.box(M["wood"], (0.06, 0.08, 1.16), T(0.5), bevel=0.012)
    p.box(M["wood"], (0.08, 0.12, 0.2), T(-0.02), bevel=0.02)
    arc = [Vector((u * 0.48, -(1 - u * u) * 0.08, -1.04)) for u in [(-1 + k / 6) for k in range(13)]]
    p.add(kit.bm_tube(arc, 0.014, 8), M["steel"])
    p.tube(M["cream"], [arc[0], T(0.66) + Vector((0, 0.0, 0)), arc[-1]], 0.003, 4)
    p.box(M["steel_dark"], (0.02, 0.06, 0.08), T(0.12) + Vector((0, 0.06, 0)), bevel=0.004)
    p.box(M["steel_dark"], (0.02, 0.012, 0.6), T(0.7) + Vector((0, -0.045, 0)), bevel=0.002)      # bolt rail
    return p.build()


def pipe_pistol():
    p = Part("tool_pipe_pistol")
    pa.rod(p, M["steel"], T(0.1), T(0.68), 0.026, 14)
    p.box(M["wood_dark"], (0.06, 0.1, 0.24), T(0.0) + Vector((0, 0.03, 0.04)), (0.25, 0, 0), bevel=0.014)
    p.box(M["rust"], (0.05, 0.06, 0.06), T(0.24) + Vector((0, -0.04, 0)), bevel=0.008)
    p.cyl(M["steel_dark"], 0.03, 0.06, T(0.12), "Z", 12, bevel=0.004)
    return p.build()


def revolver():
    p = Part("tool_revolver")
    pa.rod(p, M["chrome"], T(0.3), T(0.74), 0.022, 14)
    p.box(M["steel"], (0.05, 0.08, 0.16), T(0.28), bevel=0.01)
    p.cyl(M["steel"], 0.07, 0.13, T(0.24), "Z", 18, bevel=0.012)
    for k in range(6):
        a = k / 6 * math.tau
        p.cyl(M["steel_dark"], 0.014, 0.135, T(0.24) + Vector((math.cos(a) * 0.04, math.sin(a) * 0.04, 0)), "Z", 8)
    p.box(M["wood"], (0.05, 0.1, 0.24), T(0.0) + Vector((0, 0.04, 0.04)), (0.3, 0, 0), bevel=0.016)
    return p.build()


def bolt_rifle():
    p = Part("tool_bolt_rifle")
    pa.rod(p, M["steel_dark"], T(0.6), T(1.78), 0.018, 12)
    p.box(M["wood"], (0.1, 0.08, 1.0), T(0.16), bevel=0.02)
    p.box(M["wood"], (0.12, 0.14, 0.3), T(-0.24), bevel=0.025)
    c = T(0.8) + Vector((0, 0, 0)) + Vector((0, 0.0, 0))
    p.cyl(M["black"], 0.03, 0.56, T(0.8) + Vector((0, -0.16, 0)), "Z", 14, bevel=0.004)          # scope
    p.cyl(M["black"], 0.04, 0.06, T(1.06) + Vector((0, -0.16, 0)), "Z", 14, bevel=0.004)
    pa.lens(p, M["glass"], T(1.09) + Vector((0, -0.16, 0)), 0.035, "-Z", 0.005)
    p.add(kit.bm_tube([T(0.42) + Vector((0.04, 0, 0)), T(0.42) + Vector((0.1, 0, 0)), T(0.4) + Vector((0.12, 0.02, 0))], 0.008, 6), M["chrome"])
    p.sphere(M["chrome"], 0.016, T(0.4) + Vector((0.12, 0.02, 0)))
    return p.build()


def flare_gun():
    p = Part("tool_flare_gun")
    p.lathe(M["plastic_yellow"], [(0.0, -0.12), (0.06, -0.12), (0.07, -0.2), (0.07, -0.48), (0.05, -0.5), (0.0, -0.5)], (0, 0, 0), "Z", 20)
    p.cyl(M["black"], 0.05, 0.012, T(0.5), "Z", 18)
    p.box(M["plastic_yellow"], (0.06, 0.1, 0.24), T(0.02) + Vector((0, 0.04, 0.02)), (0.3, 0, 0), bevel=0.014)
    return p.build()


# ---------------------------------------------------------------- block tools
def rake():
    p = Part("tool_rake")
    handle(p, M["wood"], -0.08, 1.24, 0.02)
    h = T(1.28)
    p.box(M["steel"], (0.88, 0.04, 0.04), h, bevel=0.006)
    p.cyl(M["rust"], 0.026, 0.06, T(1.22), "Z", 10, bevel=0.004)
    for k in range(6):
        x = -0.4 + k * 0.16
        p.add(kit.bm_tube([h + Vector((x, 0, 0)), h + Vector((x, -0.04, -0.06)), h + Vector((x, -0.06, -0.14))], [0.01, 0.009, 0.004], 6), M["steel"])
    return p.build()


def tamper():
    p = Part("tool_tamper")
    p.cyl(M["black"], 0.022, 0.4, T(-0.08), "X", 12, bevel=0.006)
    pa.rod(p, M["steel"], T(-0.08), T(1.12), 0.022, 12)
    p.cyl(M["rust"], 0.05, 0.04, T(1.12), "Z", 12, bevel=0.006)
    p.box(M["steel_dark"], (0.5, 0.5, 0.12), T(1.24), bevel=0.012)
    p.box(M["chrome"], (0.5, 0.5, 0.02), T(1.31), bevel=0.004)
    return p.build()


def line_painter():
    p = Part("tool_line_painter")
    p.cyl(M["black"], 0.024, 0.24, T(-0.02), "Z", 12, bevel=0.006)
    p.box(M["steel"], (0.02, 0.06, 0.08), T(0.08) + Vector((0, -0.04, 0)), bevel=0.004)
    c = T(0.48)
    p.lathe(M["label_cream"], [(0.0, 0.24), (0.17, 0.24), (0.175, 0.22), (0.175, -0.22), (0.17, -0.24), (0.0, -0.24)], c, "Z", 28)
    pa.ring(p, M["steel"], c + Vector((0, 0, 0.24)), 0.172, 0.01, "Z", 28, 4)
    pa.ring(p, M["steel"], c + Vector((0, 0, -0.24)), 0.172, 0.01, "Z", 28, 4)
    p.cyl(M["plastic_yellow"], 0.177, 0.08, c, "Z", 28, bevel=0.0)
    pa.rod(p, M["chrome"], T(0.72), T(1.28), 0.012, 10)
    p.box(M["steel_dark"], (0.2, 0.2, 0.04), T(1.32), bevel=0.008)
    p.cyl(M["plastic_white"], 0.02, 0.02, T(1.35), "Z", 10)
    return p.build()


def scythe():
    p = Part("tool_scythe")
    pa.rod(p, M["wood"], T(-0.32), T(2.02), 0.022, 12)
    for y in (0.08, 0.96):
        pa.rod(p, M["wood_light"], T(y), T(y) + Vector((0, -0.18, 0)), 0.018, 8)
    p.box(M["steel_dark"], (0.07, 0.07, 0.1), T(2.06), bevel=0.01)
    pts = []
    for k in range(12):
        u = k / 11
        pts.append(Vector((0, -u * 0.9, -2.08 + (u * u) * 0.32)))
    secs = []
    for k, q in enumerate(pts):
        w = 0.1 * (1 - (k / 11) ** 1.5) + 0.01
        secs.append([q + Vector((-0.004, 0, 0)), q + Vector((0.004, 0, 0)), q + Vector((0.004, 0, w)), q + Vector((-0.004, 0, w))])
    p.add(kit.bm_loft(secs), M["steel"])
    p.add(kit.bm_tube(pts, 0.004, 4), M["chrome"])
    return p.build()


def shears():
    p = Part("tool_shears")
    p.add(kit.bm_tube([(-0.08, 0, -0.24), (-0.07, 0, -0.04), (0, 0, 0.02), (0.07, 0, -0.04), (0.08, 0, -0.24)], 0.014, 8), M["steel"])
    p.box(M["rust"], (0.2, 0.03, 0.05), T(0.3), bevel=0.006)
    for s in (-1, 1):
        secs = [[(s * 0.06 + d, 0.0, -0.32) for d in (-0.012, 0.012)] + [(s * 0.06 + d, -0.004, -0.32) for d in (0.012, -0.012)],
                [(s * 0.008 + d, 0.0, -0.8) for d in (-0.004, 0.004)] + [(s * 0.008 + d, -0.004, -0.8) for d in (0.004, -0.004)]]
        p.add(kit.bm_loft(secs), M["chrome"])
    return p.build()


def gold_pan():
    p = Part("tool_gold_pan")
    c = Vector((0, -0.08, -0.48))
    q = Matrix.Rotation(math.pi / 2, 4, "X")
    p.add(kit.bm_lathe([(0.0, 0.0), (0.26, 0.0), (0.5, 0.12), (0.52, 0.13), (0.5, 0.135), (0.25, 0.012), (0.0, 0.012)], 40), M["black"], matrix=Matrix.Translation(c) @ q)
    for k in range(3):
        p.add(kit.bm_torus(0.34 + k * 0.05, 0.006, 40, 4), M["black"], matrix=Matrix.Translation(c + Vector((0, -0.05 - k * 0.012, 0))) @ q)
    p.add(kit.bm_lathe([(0.0, 0.012), (0.2, 0.012), (0.18, 0.04), (0.0, 0.05)], 24), M["burlap"], matrix=Matrix.Translation(c) @ q)
    for k, (dx, dz) in enumerate(((0.05, 0.04), (-0.06, -0.08), (0.1, -0.02))):
        p.sphere(M["gold"], 0.012, c + Vector((dx, -0.05, dz)))
    p.box(M["steel"], (0.08, 0.02, 0.1), T(0.02) + Vector((0, -0.06, 0)), bevel=0.006)
    return p.build()


# ---------------------------------------------------------------- fluid containers (hand)
def jerrycan():
    p = Part("tool_jerrycan")
    W, H, D = 0.5, 0.66, 0.2          # game: 7 x 9 x 3 voxels, carried by the handle (origin)
    c = Vector((0.0, 0.0, -0.08 - H / 2))
    p.box(M["jerry"], (W, D, H), c, bevel=0.025)
    for s in (-1, 1):
        pa.stencil_x(p, M["jerry"], c + Vector((0, s * (D / 2 + 0.004), 0)), H * 0.62, "Y", w=0.03)
    for x in (-0.12, 0.0, 0.12):
        p.box(M["jerry"], (0.03, 0.05, 0.1), (x, 0, -0.04), bevel=0.008)
    p.box(M["jerry"], (0.28, 0.05, 0.03), (0, 0, 0.0), bevel=0.008)
    p.cyl(M["steel"], 0.04, 0.08, (0.24, 0, -0.06), "Z", 14, bevel=0.006)
    p.box(M["steel"], (0.1, 0.05, 0.03), (0.28, 0, -0.01), bevel=0.006)
    return p.build()


def fuel_can():
    p = Part("tool_fuel_can")
    p.box(M["plastic_red"], (0.22, 0.2, 0.38), (0, 0, -0.28), bevel=0.03)
    p.add(kit.bm_tube([(-0.04, 0, -0.06), (-0.04, 0, 0.0), (0.04, 0, 0.0), (0.04, 0, -0.06)], 0.014, 8), M["black"])
    p.tube(M["ochre"], [(0.08, 0, -0.1), (0.14, 0, -0.04), (0.2, 0, 0.0), (0.26, 0, 0.0)], 0.02, 10)
    p.cyl(M["black"], 0.03, 0.03, (0.08, 0, -0.09), "Z", 12, bevel=0.004)
    p.box(M["label_yellow"], (0.12, 0.01, 0.1), (0, -0.102, -0.28), bevel=0.002)
    return p.build()


def bottle():
    p = Part("tool_bottle")
    p.lathe(M["glass_clear"], [(0.0, -0.5), (0.08, -0.5), (0.085, -0.46), (0.085, -0.22), (0.06, -0.14), (0.03, -0.1), (0.028, -0.04), (0.0, -0.04)], (0, 0, 0), "Z", 20)
    p.lathe(M["water"], [(0.0, -0.49), (0.075, -0.49), (0.075, -0.3), (0.0, -0.3)], (0, 0, 0), "Z", 20)
    p.cyl(M["plastic_blue"], 0.032, 0.06, (0, 0, -0.04), "Z", 16, bevel=0.006)
    p.cyl(M["label_cream"], 0.087, 0.12, (0, 0, -0.34), "Z", 20, bevel=0.0)
    return p.build()


def bucket():
    p = Part("tool_bucket")
    p.lathe(M["tin"], [(0.0, -0.66), (0.16, -0.66), (0.17, -0.64), (0.2, -0.16), (0.205, -0.14), (0.19, -0.15), (0.155, -0.64), (0.0, -0.64)], (0, 0, 0), "Z", 28)
    p.lathe(M["water"], [(0.0, -0.3), (0.18, -0.3)], (0, 0, 0), "Z", 28)
    for z in (-0.3, -0.5):
        pa.ring(p, M["tin"], (0, 0, z), 0.19 - (z + 0.16) * 0.06, 0.006, "Z", 28, 4)
    p.add(kit.bm_tube([(-0.2, 0, -0.16), (-0.16, 0, -0.02), (0, 0, 0.02), (0.16, 0, -0.02), (0.2, 0, -0.16)], 0.006, 6), M["steel"])
    p.cyl(M["wood"], 0.018, 0.12, (0, 0, 0.02), "X", 10, bevel=0.004)
    return p.build()


def oil_jug():
    p = Part("tool_oil_jug")
    p.box(M["plastic_yellow"], (0.22, 0.2, 0.42), (0, 0, -0.27), bevel=0.035)
    p.add(kit.bm_tube([(-0.08, 0, -0.08), (-0.06, 0, 0.0), (0.04, 0, 0.0), (0.06, 0, -0.08)], 0.016, 8), M["plastic_yellow"])
    p.cyl(M["black"], 0.03, 0.05, (0.08, 0, -0.02), "Z", 14, bevel=0.004)
    p.box(M["label_cream"], (0.16, 0.01, 0.14), (0, -0.102, -0.3), bevel=0.002)
    p.box(M["label_red"], (0.16, 0.011, 0.03), (0, -0.103, -0.26), bevel=0.0)
    return p.build()


HAND = [("tool_sledgehammer", sledgehammer), ("tool_wrench", wrench), ("tool_cutter", cutter), ("tool_claw_hammer", claw_hammer),
        ("tool_shovel", shovel), ("tool_axe", axe), ("tool_pickaxe", pickaxe), ("tool_crowbar", crowbar), ("tool_welder", welder),
        ("tool_jack", jack), ("tool_hoe", hoe), ("tool_watering_can", watering_can), ("tool_rake", rake), ("tool_tamper", tamper),
        ("tool_line_painter", line_painter), ("tool_scythe", scythe), ("tool_shears", shears), ("tool_gold_pan", gold_pan),
        ("tool_fishing_rod", fishing_rod), ("tool_grapple", grapple), ("tool_torch", torch), ("tool_gas_torch", gas_torch),
        ("tool_lantern", lantern), ("tool_flashlight", flashlight), ("tool_binoculars", binoculars), ("tool_geiger", geiger),
        ("tool_detector", detector)]
WEAPONS = [("tool_pipe_club", pipe_club), ("tool_machete", machete), ("tool_pipe_shotgun", shotgun), ("tool_spear", spear),
           ("tool_nail_bat", nail_bat), ("tool_knife", knife), ("tool_leaf_blade", leaf_blade), ("tool_slingshot", slingshot),
           ("tool_bow", bow), ("tool_crossbow", crossbow), ("tool_pipe_pistol", pipe_pistol), ("tool_revolver", revolver),
           ("tool_bolt_rifle", bolt_rifle), ("tool_flare_gun", flare_gun)]
FLUIDS = [("tool_jerrycan", jerrycan), ("tool_fuel_can", fuel_can), ("tool_bottle", bottle), ("tool_bucket", bucket), ("tool_oil_jug", oil_jug)]

for i, (tid, fn) in enumerate(HAND):
    pa.add(tid, fn, F, kind="tool", category="Tool", origin="grip", held_along="game -Y", world_scale=0.7, tile="tools_hand_%d" % (i // 7 + 1), azim=-45, display=(0, math.pi / 2, 0.5))
for i, (tid, fn) in enumerate(WEAPONS):
    pa.add(tid, fn, F, kind="tool", category="Weapon", origin="grip", held_along="game -Y", world_scale=0.7, tile="tools_weapons_%d" % (i // 7 + 1), azim=-45, display=(0, math.pi / 2, 0.5))
for tid, fn in FLUIDS:
    pa.add(tid, fn, F, kind="tool", category="FluidContainer", origin="handle", held_along="game -Y", world_scale=0.7, tile="tools_fluids", azim=-45)

if __name__ == "__main__":
    pa.run(F, out_sub="items", cols=2, gap=0.12)
