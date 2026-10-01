"""HD furniture / build pieces (FurnitureLibrary*.cs, BuildPieces.cs; 0.08 m voxels). Origin = mounting point
(bottom centre on the surface), +Z (game +Y) away from the surface, front = Blender -Y (game +Z).

blender -b -P furniture.py -- <out_dir>"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit  # noqa: E402
import comps  # noqa: E402
from kit import V, Part, Vector, Matrix  # noqa: E402

M = {}


def extra(M):
    M["planks"] = kit.textured("WallPlanks", "planks", kit.P("wood", 2), kit.P("wood", 3), scale=1.0, dust=0.35, rough=0.85, bump=0.6, rot="X")
    M["planks_v"] = kit.textured("FloorBoards", "planks", kit.P("wood", 1), kit.P("wood", 2), scale=1.0, dust=0.3, rough=0.85, bump=0.5, rot="X")
    M["brick"] = kit.textured("Brick", "brick", kit._hex("823e28"), kit._hex("6e3422"), kit._hex("9a9080"), dust=0.35, rough=0.9, bump=0.6)
    M["blanket"] = kit.textured("ArmyBlanket", "canvas", kit.P("riggreen", 3), kit.P("riggreen", 4), dust=0.1, rough=0.95, bump=0.4)
    M["sheet"] = kit.textured("Sheet", "canvas", kit.P("cream", 1), kit.P("cream", 2), dust=0.1, rough=0.95, bump=0.2)
    M["stone"] = kit.textured("FireStone", "stone", kit.P("sand", 1), kit.P("sand", 3), kit._hex("5c5a56"), dust=0.2, rough=0.9, bump=0.8)
    M["ash"] = kit.surface("Ash", kit.P("black", 2), dust=0.0, rough=0.95, var=0.4, bump=0.8)
    M["ember"] = kit.emissive("Ember", kit._hex("ff7a20"), 6.0)
    M["flame"] = kit.emissive("Flame", kit._hex("ffc040"), 8.0)
    M["solar"] = kit.textured("SolarCells", "corrugated", kit.P("navy", 0), kit.P("navy", 2), kit.P("chrome", 1), scale=3.0, dust=0.2, rough=0.15, bump=0.2, rot="Y")
    M["plaster"] = kit.surface("Plaster", kit.P("cream", 1), dust=0.2, rough=0.9, var=0.15, bump=0.4)
    M["pegboard"] = kit.surface("Pegboard", kit.P("wood", 3), dust=0.2, rough=0.8, var=0.15, bump=0.3)
    M["gen"] = kit.surface("GenOlive", kit.P("olive", 2), chips=0.45, rust=0.3, dust=0.4, rough=0.55, streaks=0.4)


def workbench():
    p = Part("workbench")
    for x in (-0.64, 0.64):
        for y in (-0.26, 0.26):
            p.box(M["steel_dark"], (0.06, 0.06, 0.82), (x, y, 0.41), bevel=0.006)
    p.box(M["steel_dark"], (1.32, 0.56, 0.03), (0, 0, 0.26), bevel=0.004)
    p.box(M["wood_light"], (1.36, 0.64, 0.08), (0, 0, 0.84), bevel=0.01)
    p.box(M["steel"], (0.4, 0.5, 0.26), (0.38, -0.02, 0.62), bevel=0.01)  # drawer unit
    for k in range(2):
        p.box(M["chrome"], (0.12, 0.02, 0.02), (0.38, -0.28, 0.56 + k * 0.12), bevel=0.004)
    p.box(M["pegboard"], (1.36, 0.03, 0.88), (0, 0.3, 1.32), bevel=0.006)
    for x in range(-6, 7, 3):  # hanging tools
        p.tube(M["chrome"], [(x * 0.08, 0.27, 1.6), (x * 0.08, 0.27, 1.3)], 0.012, 6)
        p.box(M["crimson"] if x % 2 else M["black"], (0.04, 0.03, 0.1), (x * 0.08, 0.27, 1.27), bevel=0.006)
    p.box(M["steel"], (0.16, 0.2, 0.12), (0.56, -0.18, 0.94), bevel=0.012)  # vise
    p.cyl(M["chrome"], 0.012, 0.22, (0.56, -0.34, 0.94), "X", 8)
    p.box(M["chrome"], (0.3, 0.04, 0.012), (-0.25, -0.12, 0.885), (0, 0, 0.3), bevel=0.003)  # wrench
    p.cyl(M["navy"], 0.06, 0.1, (-0.5, 0.05, 0.93), "Z", 16, bevel=0.006)  # paint tin
    p.box(M["crimson"], (0.24, 0.2, 0.02), (-0.2, 0.1, 0.885), (0, 0, 0.2), bevel=0.006)  # shop cloth
    p.tube(M["black"], [(-0.6, 0.2, 0.88), (-0.6, 0.15, 1.35), (-0.45, -0.05, 1.45)], 0.01, 6)  # task lamp
    p.add(kit.bm_lathe([(0, 0), (0.07, 0.0), (0.04, 0.08), (0, 0.08)], 16), M["black"], (-0.42, -0.08, 1.38), (2.4, 0, 0))
    return p.build()


def bed():
    p = Part("bed")
    p.box(M["wood"], (0.88, 1.92, 0.16), (0, 0, 0.12), bevel=0.012)
    for x in (-0.4, 0.4):
        for y in (-0.92, 0.92):
            p.box(M["wood_dark"], (0.08, 0.08, 0.2), (x, y, 0.1), bevel=0.008)
    p.box(M["wood_light"], (0.88, 0.06, 0.72), (0, -0.92, 0.36), bevel=0.015)
    p.box(M["wood_light"], (0.88, 0.05, 0.42), (0, 0.92, 0.21), bevel=0.012)
    p.box(M["sheet"], (0.78, 1.8, 0.16), (0, 0, 0.28), bevel=0.05, segs=3)
    blanket = kit.superloft([(y, 0, 0.34, 0.42, 0.06 + (0.01 if abs(y) < 0.3 else 0)) for y in (1.0, 0.6, 0.0, -0.3, -0.38)], 20, 3.0)
    p.add(blanket, M["blanket"])
    p.add(kit.superloft([(y, 0, 0.42, 0.28, 0.07) for y in (-0.82, -0.75, -0.55, -0.48)], 20, 2.2), M["sheet"])
    return p.build()


def chest():
    p = Part("chest")
    p.box(M["wood"], (1.04, 0.72, 0.5), (0, 0, 0.25), bevel=0.015)
    p.add(kit.superloft([(y, 0, 0.5, 0.52, 0.12) for y in (-0.36, 0.36)], 24, 2.2, close=True), M["wood"])
    for x in (-0.32, 0.32):
        p.box(M["steel_dark"], (0.06, 0.74, 0.52), (x, 0, 0.26), bevel=0.006)
        p.add(kit.superloft([(y, x, 0.5, 0.03, 0.125) for y in (-0.37, 0.37)], 16, 2.2), M["steel_dark"])
    p.box(M["brass"], (0.12, 0.03, 0.14), (0, -0.37, 0.48), bevel=0.01)
    p.add(kit.bm_torus(0.03, 0.008, 12, 6), M["brass"], (0, -0.39, 0.4), (math.pi / 2, 0, 0))
    for s in (-1, 1):
        p.add(kit.bm_torus(0.05, 0.01, 14, 6), M["steel"], (s * 0.53, 0, 0.42), (0, math.pi / 2, 0))
    return p.build()


def stove():
    p = Part("stove")
    p.box(M["stone"], (0.72, 0.56, 0.08), (0, 0, 0.04), bevel=0.01)
    for x in (-0.3, 0.3):
        for y in (-0.2, 0.2):
            p.cyl(M["iron"], 0.025, 0.1, (x, y, 0.13), "Z", 8)
    p.box(M["iron"], (0.66, 0.5, 0.52), (0, 0, 0.44), bevel=0.02)
    for k in range(3):
        p.box(M["iron"], (0.7, 0.54, 0.02), (0, 0, 0.26 + k * 0.18), bevel=0.004)
    p.box(M["black"], (0.32, 0.03, 0.22), (0, -0.26, 0.42), bevel=0.01)
    for k in range(5):
        p.box(M["iron"], (0.02, 0.035, 0.18), (-0.12 + k * 0.06, -0.27, 0.42), bevel=0.003)
    p.box(M["ember"], (0.28, 0.01, 0.16), (0, -0.245, 0.42), bevel=0)
    p.cyl(M["chrome"], 0.012, 0.08, (0.17, -0.3, 0.48), "Y", 6)
    p.cyl(M["steel"], 0.11, 0.18, (-0.16, 0, 0.8), "Z", 20, bevel=0.01)  # pot
    p.tube(M["steel"], [(-0.27, 0, 0.86), (-0.16, 0, 0.98), (-0.05, 0, 0.86)], 0.006, 5)
    p.tube(M["rust"], [(0.16, 0.12, 0.7), (0.16, 0.12, 1.84)], 0.08, 14)
    p.cyl(M["rust"], 0.12, 0.05, (0.16, 0.12, 1.86), "Z", 14, bevel=0.01, r2=0.03)
    return p.build()


def generator():
    p = Part("generator")
    for y in (-0.3, 0.3):
        p.box(M["steel_dark"], (1.2, 0.08, 0.08), (0, y, 0.04), bevel=0.008)
    for x in (-0.5, 0.5):
        p.box(M["steel_dark"], (0.08, 0.68, 0.06), (x, 0, 0.1), bevel=0.006)
    p.box(M["gen"], (0.72, 0.5, 0.5), (-0.18, 0, 0.42), bevel=0.04)
    comps.finned_cylinder(p, M, (-0.18, 0, 0.66), (-0.12, 0, 0.82), 0.09, 6, 0.15, M["iron"])
    p.cyl(M["steel"], 0.2, 0.3, (0.36, 0, 0.36), "X", 24, bevel=0.02)  # alternator
    for k in range(10):
        a = k / 10 * math.tau
        p.box(M["steel"], (0.28, 0.012, 0.02), (0.36, math.cos(a) * 0.2, 0.36 + math.sin(a) * 0.2), (a, 0, 0), bevel=0)
    p.box(M["black"], (0.18, 0.04, 0.22), (0.36, -0.24, 0.38), bevel=0.01)
    for k in range(3):
        p.cyl(M["hazard"] if k == 0 else M["chrome"], 0.015, 0.02, (0.31 + k * 0.05, -0.27, 0.42), "Y", 8)
    p.box(M["crimson"], (0.4, 0.3, 0.2), (-0.3, 0, 0.82), bevel=0.04)  # fuel tank
    p.cyl(M["black"], 0.035, 0.03, (-0.3, 0.05, 0.94), "Z", 12)
    p.tube(M["rust"], [(-0.48, 0.18, 0.5), (-0.62, 0.22, 0.6), (-0.62, 0.22, 0.86)], 0.025, 8)
    p.add(kit.bm_torus(0.05, 0.01, 14, 6), M["copper"], (-0.58, -0.02, 0.36), (0, math.pi / 2, 0))
    p.tube(M["black"], [(0.5, 0.1, 0.36), (0.6, 0.2, 0.15), (0.6, 0.4, 0.05)], 0.012, 6)
    return p.build()


def solar_panel():
    p = Part("solar_panel")
    for x in (-0.72, 0.72):
        p.box(M["steel_dark"], (0.04, 0.04, 0.5), (x, 0.4, 0.25), bevel=0.004)
        p.box(M["steel_dark"], (0.04, 0.04, 1.0), (x, -0.48, 0.5), bevel=0.004)
        p.box(M["steel_dark"], (0.04, 1.0, 0.04), (x, -0.04, 0.03), bevel=0.004)
    ang = math.atan(0.55)
    p.add(kit.bm_box(1.84, 1.3, 0.04, bevel=0.006), M["alu"], (0, 0.02, 0.86), (-ang, 0, 0))
    p.add(kit.bm_box(1.76, 1.22, 0.012, bevel=0.0), M["solar"], (0, 0.012, 0.886), (-ang, 0, 0))
    for k in range(1, 4):
        p.add(kit.bm_box(0.012, 1.22, 0.016, bevel=0.0), M["chrome"], (-0.88 + k * 0.44, 0.012, 0.888), (-ang, 0, 0))
    p.box(M["black"], (0.16, 0.08, 0.16), (0, 0.4, 0.2), bevel=0.01)
    p.cyl(M["lamp_amber"], 0.015, 0.02, (0, 0.35, 0.24), "Y", 8)
    p.tube(M["black"], [(0, 0.4, 0.28), (0, 0.3, 0.6), (0.1, 0.2, 0.7)], 0.008, 6)
    return p.build()


def light_switch():
    """light_switch: wall piece (+Z out of the wall). Plate 0.24 x 0.4, toggle lever (LightSwitch.FitLever)."""
    p = Part("light_switch")
    p.box(M["cream"], (0.24, 0.4, 0.03), (0, 0, 0.015), bevel=0.008)
    p.box(M["black"], (0.06, 0.12, 0.02), (0, 0, 0.035), bevel=0.006)
    p.box(M["cream"], (0.03, 0.08, 0.03), (0, -0.03, 0.055), (0.5, 0, 0), bevel=0.006)
    for y in (-0.16, 0.16):
        p.cyl(M["chrome"], 0.012, 0.006, (0, y, 0.032), "Z", 10)
    p.tube(M["black"], [(0, 0.2, 0.01), (0.0, 0.6, 0.01)], 0.008, 6)
    ob = p.build()
    ob.rotation_euler = (math.radians(90), 0, 0)  # shown on an upright wall facing the camera
    ob.location = (0, 0, 1.2)
    w = Part("PreviewWall")
    w.box(M["plaster"], (0.8, 0.06, 1.6), (0, 0.03, 0.8), bevel=0.01)
    wo = w.build()
    return [ob, wo]


def campfire():
    p = Part("campfire")
    rnd = random.Random(2)
    for a in range(14):
        t = a / 14 * math.tau
        p.add(kit.bm_rock(0.09 + 0.03 * (a % 3), a, 0.75, 1), M["stone"], (math.cos(t) * 0.44, math.sin(t) * 0.44, 0.04))
    p.cyl(M["ash"], 0.34, 0.03, (0, 0, 0.015), "Z", 24, bevel=0.01)
    for k in range(10):
        p.add(kit.bm_rock(0.05, 30 + k, 0.6, 1), M["ember"], (rnd.uniform(-0.15, 0.15), rnd.uniform(-0.15, 0.15), 0.04))
    for a in (0.3, 1.9, 3.5, 5.0):
        d = Vector((math.cos(a), math.sin(a), 0))
        p.tube(M["wood_dark"], [d * 0.36 + Vector((0, 0, 0.05)), Vector((0, 0, 0.22))], 0.04, 8)
    for k, (h, r) in enumerate(((0.36, 0.1), (0.26, 0.07), (0.22, 0.06))):
        a = k * 2.1
        p.add(kit.bm_cyl(r, h, 8, 0.0, 0.004), M["flame"], (math.cos(a) * 0.05, math.sin(a) * 0.05, 0.18 + h / 2))
    for x in (-0.56, 0.56):
        p.tube(M["wood"], [(x, 0, 0.0), (x, 0, 0.62), (x, -0.06, 0.74)], 0.025, 8)
        p.tube(M["wood"], [(x, 0, 0.6), (x, 0.06, 0.74)], 0.02, 6)
    p.tube(M["wood_light"], [(-0.68, 0, 0.72), (0.68, 0, 0.72)], 0.018, 8)
    p.tube(M["steel"], [(0.1, 0, 0.72), (0.1, 0, 0.6)], 0.004, 4)
    p.cyl(M["iron"], 0.11, 0.14, (0.1, 0, 0.52), "Z", 18, bevel=0.01)
    return p.build()


def streetlamp():
    """streetlamp_pole: 5.1 m timber pole, galvanised arm, enamel shade (head at 4.08 m, 1.04 m forward)."""
    p = Part("streetlamp_pole")
    p.cyl(M["wood_dark"], 0.12, 5.1, (0, 0, 2.55), "Z", 14, bevel=0.0, r2=0.09)
    p.tube(M["steel"], [(0, -0.1, 4.6), (0, -0.6, 4.55), (0, -1.04, 4.3), (0, -1.04, 4.2)], 0.025, 10)
    p.tube(M["steel"], [(0, -0.1, 4.2), (0, -0.6, 4.5)], 0.015, 6)
    p.add(kit.bm_lathe([(0.0, 0.18), (0.06, 0.18), (0.24, 0.0), (0.25, -0.02), (0.22, -0.01), (0.05, 0.15)], 24), M["olive"], (0, -1.04, 4.0))
    p.sphere(M["lamp"], 0.06, (0, -1.04, 4.05))
    for z in (4.75, 4.95):
        p.box(M["wood"], (0.9, 0.08, 0.08), (0, 0, z), bevel=0.01)
        for x in (-0.38, 0.38):
            p.cyl(M["glass"], 0.03, 0.06, (x, 0, z + 0.07), "Z", 10, bevel=0.005)
    p.tube(M["black"], [(-0.38, 0, 5.06), (-0.12, -0.05, 4.6), (0.0, -0.13, 4.5)], 0.006, 4, caps=False)
    p.box(M["steel_dark"], (0.2, 0.12, 0.3), (0, -0.14, 2.0), bevel=0.02)
    return p.build()


def home():
    objs = [workbench(), bed(), chest(), stove()]
    kit.grid(objs, 2, 0.4)
    return objs


def utility():
    ls = light_switch()
    objs = [generator(), solar_panel(), campfire(), streetlamp(), ls[0]]
    kit.grid(objs, 3, 0.5)
    ls[1].location = (ls[0].location.x, ls[0].location.y, 0)
    return objs + [ls[1]]


def wall_piece(name, mat, hole, door_mat=None):
    """BuildPieces.Wall: 2.0 x 2.4 m, 0.16 m thick (front face toward -Y); hole 1 = window, 2 = doorway."""
    p = Part(name)
    holes = []
    if hole == 1:
        holes = [(0.56, 1.44, 0.96, 1.84)]
    elif hole == 2:
        holes = [(0.48, 1.52, 0.0, 2.24)]
    cuts = sorted({0.0, 2.0} | {h[i] for h in holes for i in (0, 1)})
    for u0, u1 in zip(cuts, cuts[1:]):
        hs = [h for h in holes if h[0] <= u0 + 1e-6 and h[1] >= u1 - 1e-6]
        spans = [(0.0, 2.4)] if not hs else [(0.0, hs[0][2]), (hs[0][3], 2.4)]
        for a, b in spans:
            if b - a > 1e-3:
                p.box(mat, (u1 - u0, 0.16, b - a), (u0 + (u1 - u0) / 2 - 1.0, 0, (a + b) / 2), bevel=0.004)
    if hole == 1:
        for x in (-0.44, 0.0, 0.44):
            p.box(M["wood"], (0.05, 0.08, 0.88), (x, 0, 1.4), bevel=0.004)
        for z in (0.98, 1.4, 1.82):
            p.box(M["wood"], (0.88, 0.08, 0.05), (0, 0, z), bevel=0.004)
        p.box(M["glass"], (0.86, 0.01, 0.86), (0, 0, 1.4), bevel=0)
        p.box(M["stone"], (1.0, 0.24, 0.05), (0, -0.04, 0.94), bevel=0.006)
    if hole == 2:
        for x in (-0.56, 0.56):
            p.box(M["wood_dark"], (0.08, 0.2, 2.3), (x, 0, 1.15), bevel=0.006)
        p.box(M["wood_dark"], (1.2, 0.2, 0.08), (0, 0, 2.28), bevel=0.006)
    return p.build()


def door():
    """BuildPieces.Door: 1.04 x 2.24 x 0.16 plank door, ledges, handle at 1 m (hinge side x = -0.52)."""
    p = Part("door")
    for k in range(6):
        p.box(M["wood"] if k % 2 else M["wood_light"], (0.17, 0.1, 2.22), (-0.43 + k * 0.173, 0, 1.12), bevel=0.006)
    for z in (0.36, 1.86):
        p.box(M["wood_dark"], (0.98, 0.04, 0.12), (0, -0.07, z), bevel=0.006)
    p.add(kit.bm_box(0.1, 0.04, 1.6, bevel=0.006), M["wood_dark"], (0, -0.07, 1.1), (0, 0.62, 0))
    for z in (0.36, 1.86):
        p.box(M["steel_dark"], (0.3, 0.012, 0.04), (-0.38, -0.095, z), bevel=0.003)
    p.cyl(M["chrome"], 0.02, 0.06, (0.4, -0.1, 1.0), "Y", 10)
    p.tube(M["chrome"], [(0.4, -0.13, 1.0), (0.3, -0.13, 1.0)], 0.01, 6)
    return p.build()


def foundation():
    """foundation_wood: 2 x 2 m timber deck (0.16 m) on 9 posts reaching 1.92 m down; origin = deck top."""
    p = Part("foundation_wood")
    for k in range(12):
        p.box(M["wood"] if k % 2 else M["wood_light"], (2.0, 0.16, 0.06), (0, -0.92 + k * 0.167, -0.03), bevel=0.005)
    for y in (-0.92, 0.0, 0.92):
        p.box(M["wood_dark"], (2.0, 0.12, 0.12), (0, y, -0.12), bevel=0.008)
    for x in (-0.88, 0.0, 0.88):
        for y in (-0.88, 0.0, 0.88):
            p.box(M["wood_dark"], (0.14, 0.14, 1.92), (x, y, -0.96), bevel=0.01)
    for y in (-0.88, 0.88):
        p.add(kit.bm_box(1.9, 0.06, 0.1, bevel=0.005), M["wood"], (0, y, -1.0), (0, 0.7, 0))
    ob = p.build()
    ob.location.z = 1.92
    return ob


def floor_piece():
    p = Part("floor_plank")
    for k in range(12):
        p.box(M["planks_v"], (0.16, 2.0, 0.08), (-0.92 + k * 0.167, 0, 0.04), bevel=0.006)
    for x in (-0.6, 0.6):
        for y in (-0.9, 0.9):
            p.cyl(M["steel"], 0.01, 0.01, (x, y, 0.085), "Z", 6)
    return p.build()


def structure():
    ww = wall_piece("wall_wood", M["planks"], 0)
    wb = wall_piece("wall_brick_window", M["brick"], 1)
    dw = wall_piece("doorway_wood", M["planks"], 2)
    d = door()
    d.parent = dw
    d.location = (-0.52, -0.02, 0)
    d.data.transform(Matrix.Translation((0.52, 0, 0)))
    d.rotation_euler = (0, 0, math.radians(-35))
    fd = foundation()
    fl = floor_piece()
    objs = [ww, wb, dw, fd, fl]
    kit.grid(objs, 3, 0.5)
    return objs


if __name__ == "__main__":
    kit.run_group([(home, "furn_home", "Workbench, bed, chest, wood stove", "workbench, bed, chest, stove"),
                   (utility, "furn_utility", "Generator, solar panel, campfire, pole lamp, light switch", "generator, solar_panel, campfire, streetlamp_pole, light_switch"),
                   (structure, "furn_structure", "Walls (plank, brick window), doorway + door, timber foundation, floor", "wall_*, doorway_*, door, foundation_wood, floor_*")], M, extra)
