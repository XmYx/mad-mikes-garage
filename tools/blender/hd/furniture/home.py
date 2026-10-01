"""HD furniture, decor and garden pieces of FurnitureLibrary.cs / BuildPieces.cs (0.08 m voxels).
Origin = mounting point, +Y away from the surface, +Z front; each fills the voxel bounds of the piece it replaces.

blender -b -P home.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fcommon import K, Asset, Vector, Matrix, lo, hi, c, W, knob, screws, leg_square, drawer_front, bulb, run, VS


# ------------------------------------------------------------------------------------------ furniture
def bed():
    a = Asset("bed", "Furniture")
    w, cl = a.p("Wood"), a.p("Cloth")
    for x in (-5, 5):                                    # side rails + legs
        w.box2(W("", "z"), (lo(x), lo(1), lo(-12)), (hi(x), hi(2), hi(10)), bevel=0.01)
        for z in (-12, 10):
            w.box2(W("dark", "y"), (lo(x), lo(0), lo(z)), (hi(x), hi(2), hi(z)), bevel=0.01)
    w.box2(W("dark", "x"), (lo(-4), lo(1), lo(-12)), (hi(4), hi(1), hi(-12)), bevel=0.008)
    for z in range(-9, 10, 3):                           # slats
        w.box2(W("light", "x"), (lo(-4), lo(2) + 0.02, z * VS - 0.03), (hi(4), hi(2), z * VS + 0.03), bevel=0.004)
    for x in (-5, 5):                                    # headboard posts + panel (+z)
        w.box2(W("dark", "y"), (lo(x), lo(0), lo(11)), (hi(x), hi(8), hi(11)), bevel=0.012)
        w.sphere(W("dark", "y"), 0.035, (x * VS, hi(8) - 0.01, 11 * VS), (1, 0.8, 1), 10, 6)
    w.box2(W("light", "x"), (lo(-4), lo(3), lo(11) + 0.02), (hi(4), hi(7), hi(11) - 0.02), bevel=0.01)
    w.box2(W("", "x"), (lo(-4), hi(7) - 0.04, lo(11)), (hi(4), hi(7) + 0.02, hi(11)), bevel=0.008)
    # mattress, blanket, pillow
    cl.box2("sheet", (lo(-4) + 0.01, lo(3), lo(-11)), (hi(4) - 0.01, hi(4), hi(10)), bevel=0.05, segs=3)
    cl.superloft("blanket", [(lo(-11) - 0.012, 0, hi(4) - 0.03, 0.385, 0.045), (lo(-11), 0, hi(4) - 0.02, 0.4, 0.06),
                             (0.0, 0, hi(4) - 0.005, 0.395, 0.055), (hi(3) - 0.02, 0, hi(4) - 0.01, 0.39, 0.05), (hi(3) + 0.03, 0, hi(4) - 0.03, 0.37, 0.035)], 20, 3.2)
    cl.superloft("blanket", [(hi(3) - 0.06, 0, hi(4) + 0.03, 0.39, 0.035), (hi(3) + 0.06, 0, hi(4) + 0.03, 0.39, 0.03)], 16, 3.0)   # turned-down edge
    cl.superloft("cloth_cream", [(lo(7) + 0.01, 0, hi(5) + 0.0, 0.2, 0.04), (lo(7) + 0.06, 0, hi(5) + 0.02, 0.26, 0.07),
                                 (hi(10) - 0.06, 0, hi(5) + 0.02, 0.26, 0.07), (hi(10) - 0.01, 0, hi(5), 0.2, 0.04)], 18, 2.4)
    return a


def fridge():
    a = Asset("fridge", "Furniture")
    s = a.p("Scrap")
    s.box2("white", (lo(-4), lo(0) + 0.06, lo(-4)), (hi(4), hi(22), hi(3) - 0.04), bevel=0.04, segs=3)
    s.box2("black", (lo(-4) + 0.03, lo(0), lo(-4) + 0.03), (hi(4) - 0.03, lo(0) + 0.07, hi(3) - 0.06), bevel=0.01)
    s.box2("white", (lo(-4) + 0.006, lo(0) + 0.17, hi(3) - 0.05), (hi(4) - 0.006, lo(14) - 0.01, hi(3)), bevel=0.025)   # fridge door
    s.box2("white", (lo(-4) + 0.006, hi(14) + 0.004, hi(3) - 0.05), (hi(4) - 0.006, hi(22) - 0.01, hi(3)), bevel=0.025)  # freezer door
    s.box2("black", (lo(-4) + 0.01, lo(14), hi(3) - 0.05), (hi(4) - 0.01, hi(14), hi(3) - 0.02), bevel=0.003)
    for (y0, y1) in ((8, 12), (16, 20)):
        s.tube("chrome", [(3 * VS, c(y0), hi(3)), (3 * VS, c(y0), hi(4) - 0.01), (3 * VS, c(y1), hi(4) - 0.01), (3 * VS, c(y1), hi(3))], 0.014, 8)
    for k in range(5):                                   # kick grille
        s.box2("steel_dark", (lo(-3), lo(1) + k * 0.026, hi(3) - 0.05), (hi(3), lo(1) + k * 0.026 + 0.012, hi(3) - 0.01), bevel=0.002)
    s.box2("rust", (lo(-4) - 0.002, lo(0) + 0.07, lo(-4) + 0.1), (lo(-4) + 0.004, lo(2), hi(2)), bevel=0.002)
    s.box2("chrome", (-0.1, hi(22) - 0.002, -0.05), (0.1, hi(22) + 0.004, 0.05), bevel=0.002)
    return a


def workbench():
    a = Asset("workbench", "Furniture")
    s, w = a.p("Scrap"), a.p("Wood")
    for x in (-8, 8):
        for z in (-4, 3):
            s.box2("steel_dark", (lo(x) + 0.01, lo(0), lo(z) + 0.01), (hi(x) - 0.01, hi(8), hi(z) - 0.01), bevel=0.006)
            s.box2("steel_dark", (lo(x) - 0.01, lo(0), lo(z) - 0.01), (hi(x) + 0.01, lo(0) + 0.012, hi(z) + 0.01), bevel=0.003)
    s.box2("steel_dark", (lo(-8), lo(3) + 0.03, lo(-4)), (hi(8), hi(3), hi(3)), bevel=0.004)
    for z in (-4, 3):
        s.box2("steel_dark", (lo(-8), hi(8) - 0.05, lo(z) + 0.015), (hi(8), hi(8), hi(z) - 0.015), bevel=0.004)
    for k in range(5):                                   # butcher-block top
        z0 = lo(-4) + k * 0.128
        w.box2(K.WOODS["x"][k % 3], (lo(-8), lo(9), z0 + 0.002), (hi(8), hi(10), z0 + 0.126), bevel=0.006)
    w.box2(W("", "y"), (lo(-8), lo(11), lo(-4)), (hi(8), hi(21), hi(-4) - 0.04), bevel=0.006)       # pegboard
    for x in range(-7, 8):
        for y in range(12, 21, 2):
            w.cyl("black", 0.006, 0.004, (x * VS + 0.04, y * VS + 0.04, hi(-4) - 0.04), "z", 6, 0)
    for k, x in enumerate(range(-6, 7, 3)):              # hanging tools
        s.rod("steel_dark", (x * VS, c(18) + 0.02, hi(-4) - 0.04), (x * VS, c(18) + 0.02, lo(-3) + 0.02), 0.005, 5)
        if k % 2:
            s.box2("chrome", (x * VS - 0.012, c(15), lo(-3)), (x * VS + 0.012, c(18), lo(-3) + 0.02), bevel=0.004)
            s.box2("crimson", (x * VS - 0.016, c(15) - 0.08, lo(-3) - 0.005), (x * VS + 0.016, c(15), lo(-3) + 0.025), bevel=0.006)
        else:
            s.box2("chrome", (x * VS - 0.006, c(15), lo(-3)), (x * VS + 0.006, c(18), lo(-3) + 0.012), bevel=0.002)
            s.torus("chrome", 0.025, 0.007, (x * VS, c(15) - 0.01, lo(-3) + 0.006), "z", 12, 5)
    dr = a.p("Drawers", "Scrap")                         # green drawer unit under the top
    dr.box2("rig_green", (lo(-7), lo(4), lo(-3)), (hi(-1), hi(8), hi(2)), bevel=0.01)
    for k in range(3):
        drawer_front(dr, "rig_green", "chrome", lo(-7) + 0.01, hi(-1) - 0.01, lo(4) + k * 0.133 + 0.005, lo(4) + (k + 1) * 0.133, hi(2), 0.018)
    s.box2("steel", (lo(6), lo(11), lo(1)), (hi(7), hi(12) - 0.03, hi(3)), bevel=0.01)            # vise
    s.box2("steel", (lo(6) - 0.01, hi(12) - 0.05, lo(1)), (hi(7) + 0.01, hi(12), lo(1) + 0.04), bevel=0.006)
    s.box2("steel", (lo(6) - 0.01, hi(12) - 0.05, hi(3) - 0.04), (hi(7) + 0.01, hi(12), hi(3)), bevel=0.006)
    s.rod("chrome", (c(6.5), c(11.5), hi(3)), (c(6.5), c(11.5), hi(3) + 0.002), 0.012, 8)
    s.rod("chrome", (c(5.6), c(11.6), hi(3) - 0.01), (c(7.4), c(11.6), hi(3) - 0.01), 0.006, 6)
    s.box2("chrome", (lo(-5), lo(11), lo(-2) + 0.02), (hi(-1), lo(11) + 0.015, hi(-2) - 0.02), bevel=0.003, r=(0, 8, 0))  # wrench
    s.cyl("blue", 0.05, 0.08, (c(3), lo(11) + 0.04, c(0.5)), "y", 16, 0.005)                    # paint tin
    s.cyl("cream", 0.035, 0.24, (c(-6.5), lo(11) + 0.12, c(1.5)), "y", 14, 0.004)               # oil can
    s.rod("cream", (c(-6.5), lo(11) + 0.24, c(1.5)), (c(-6.5), lo(11) + 0.3, c(2.6)), 0.006, 5)
    s.rod("steel", (c(7), lo(11), c(-3)), (c(7), c(22), c(-3)), 0.012, 8)                        # task lamp
    s.rod("rig_green", (c(7), c(22), c(-3)), (c(3.5), c(22), c(-2.5)), 0.012, 8)
    s.lathe("rig_green", [(0.0, 0.0), (0.03, 0.0), (0.08, -0.07), (0.075, -0.075), (0.0, -0.01)], (c(3), c(22) + 0.01, c(-2)), "y", 16)
    s.add(K.kit.bm_sphere(0.03, 10, 6), "lamp", Matrix.Translation((c(3), c(21) - 0.01, c(-2))))
    return a


def locker():
    a = Asset("locker", "Furniture")
    s = a.p("Scrap")
    s.box2("rig_green", (lo(-3), lo(0) + 0.04, lo(-3)), (hi(3), hi(20), hi(2) - 0.02), bevel=0.012)
    s.box2("black", (lo(-3) + 0.02, lo(0), lo(-3) + 0.02), (hi(3) - 0.02, lo(0) + 0.05, hi(2) - 0.04), bevel=0.004)
    for sx in (-1, 1):                                   # two doors with louvers, handles
        x0, x1 = (lo(-3) + 0.01, -0.006) if sx < 0 else (0.006, hi(3) - 0.01)
        s.box2("rig_green", (x0, lo(1), hi(2) - 0.03), (x1, hi(19), hi(2)), bevel=0.006)
        for k in range(4):
            y = c(15) + k * 0.06
            s.box2("black", (x0 + 0.04, y, hi(2) - 0.003), (x1 - 0.04, y + 0.016, hi(2) + 0.004), bevel=0.002, r=(-25, 0, 0))
        s.box2("chrome", (-sx * 0.0 + (x1 - 0.04 if sx < 0 else x0 + 0.02), c(9), hi(2)), ((x1 - 0.02) if sx < 0 else x0 + 0.04, c(11), hi(2) + 0.014), bevel=0.003)
        s.box2("white", (x0 + 0.05, c(4), hi(2)), (x1 - 0.05, c(5.4), hi(2) + 0.002), bevel=0.001)       # name card
    s.box2("black", (-0.006, lo(1), hi(2) - 0.03), (0.006, hi(19), hi(2) - 0.005), bevel=0)
    s.torus("chrome", 0.018, 0.004, (-0.03, c(10.5), hi(2) + 0.02), "x", 10, 4)
    s.box2("brass", (-0.05, c(10.1) - 0.04, hi(2) + 0.01), (-0.01, c(10.1), hi(2) + 0.03), bevel=0.004)
    return a


def shelf():
    a = Asset("shelf", "Furniture")
    w, g, s = a.p("Wood"), a.p("Glass"), a.p("Scrap")
    w.box2(W("light", "x"), (lo(-7), lo(1), lo(-3)), (hi(7), hi(1), hi(3)), bevel=0.008)
    for x in (-6, 6):
        w.box2(W("dark", "z"), (lo(x), lo(0), lo(-3)), (hi(x), lo(1), hi(3)), bevel=0.006)
    g.lathe("glass", [(0.0, 0), (0.03, 0), (0.032, 0.012), (0.03, 0.15), (0.012, 0.19), (0.011, 0.23), (0.0, 0.23)], (c(-5), hi(1), c(-1)), "y", 14)
    g.lathe("glass", [(0.0, 0), (0.04, 0), (0.04, 0.1), (0.02, 0.13), (0.018, 0.16), (0.0, 0.16)], (c(-3), hi(1), c(0)), "y", 14)
    s.cyl("rust", 0.012, 0.012, (c(-5), hi(1) + 0.235, c(-1)), "y", 8, 0)
    s.box2("rust", (lo(2), hi(1), lo(-2)), (hi(4), hi(3), hi(0)), bevel=0.012)
    s.box2("steel", (lo(5), hi(1), lo(1)), (hi(6), hi(4) - 0.02, hi(2)), bevel=0.01)
    s.cyl("steel_dark", 0.02, 0.01, (c(5.5), hi(4) - 0.015, c(1.5)), "y", 10, 0.002)
    return a


def crate():
    a = Asset("crate", "Furniture")
    w, s = a.p("Wood"), a.p("Scrap")
    w.box2(W("dark", "x"), (lo(-3) + 0.03, lo(0) + 0.03, lo(-3) + 0.03), (hi(3) - 0.03, hi(6) - 0.03, hi(3) - 0.03), bevel=0)
    for k in range(4):                                   # slats on four sides (horizontal), lid boards
        y0 = lo(0) + k * 0.14 + 0.006
        for side in range(4):
            m = K.WOODS["x" if side < 2 else "z"][(k + side) % 3]
            if side == 0:
                w.box2(m, (lo(-3) + 0.04, y0, hi(3) - 0.03), (hi(3) - 0.04, y0 + 0.128, hi(3)), bevel=0.005)
            elif side == 1:
                w.box2(m, (lo(-3) + 0.04, y0, lo(-3)), (hi(3) - 0.04, y0 + 0.128, lo(-3) + 0.03), bevel=0.005)
            elif side == 2:
                w.box2(m, (lo(-3), y0, lo(-3) + 0.04), (lo(-3) + 0.03, y0 + 0.128, hi(3) - 0.04), bevel=0.005)
            else:
                w.box2(m, (hi(3) - 0.03, y0, lo(-3) + 0.04), (hi(3), y0 + 0.128, hi(3) - 0.04), bevel=0.005)
    for k in range(4):
        x0 = lo(-3) + 0.04 + k * 0.12
        w.box2(K.WOODS["z"][k % 3], (x0 + 0.003, hi(6) - 0.03, lo(-3) + 0.04), (x0 + 0.117, hi(6), hi(3) - 0.04), bevel=0.005)
    for x in (-3, 3):                                    # steel corner angles
        for z in (-3, 3):
            s.box2("steel", (lo(x) - 0.002, lo(0), lo(z) - 0.002), (hi(x) + 0.002, hi(6), hi(z) + 0.002), bevel=0.006)
            for y in (lo(0) + 0.08, hi(6) - 0.08):
                screws(s, "chrome", [(x * VS + (0.042 if x > 0 else -0.042), y, z * VS)], 0.007, "x")
    return a


def chest():
    a = Asset("chest", "Furniture")
    w, s = a.p("Wood"), a.p("Scrap")
    for k in range(3):                                   # body boards (horizontal), lid boards
        y0 = lo(0) + k * 0.16
        w.box2(K.WOODS["x"][k % 3], (lo(-6), y0 + 0.003, lo(-4)), (hi(6), y0 + 0.158, hi(4)), bevel=0.008)
    yl = lo(0) + 0.48
    w.box2(W("", "x"), (lo(-6), yl, lo(-4)), (hi(6), yl + 0.06, hi(4)), bevel=0.008)
    w.superloft(W("", "x"), [(lo(-6), 0, yl + 0.06, 0.32, 0.14), (hi(6), 0, yl + 0.06, 0.32, 0.14)], 24, 2.2, axis="x")
    w.box2(W("dark", "x"), (lo(-6), yl - 0.01, lo(-4)), (hi(6), yl + 0.004, hi(4)), bevel=0.003)
    for x in (-4, 4):                                    # iron bands over the lid
        s.box2("steel_dark", (lo(x), lo(0), lo(-4) - 0.006), (hi(x), yl + 0.06, hi(4) + 0.006), bevel=0.004)
        s.superloft("steel_dark", [(lo(x), 0, yl + 0.06, 0.326, 0.146), (hi(x), 0, yl + 0.06, 0.326, 0.146)], 24, 2.2, axis="x")
        for y in (0.1, 0.3):
            screws(s, "chrome", [(x * VS, y, hi(4) + 0.008)], 0.008)
    s.box2("brass", (lo(-1), c(5), hi(4)), (hi(1), yl + 0.04, hi(4) + 0.02), bevel=0.008)
    s.torus("brass", 0.025, 0.006, (0, c(5) - 0.01, hi(4) + 0.025), "z", 12, 4)
    for sx in (-1, 1):
        s.torus("steel", 0.04, 0.008, (sx * (hi(6) - 0.002), c(4), 0), "x", 14, 5)
    return a


def stove():
    a = Asset("stove", "Furniture")
    s, st = a.p("Scrap"), a.p("Stone")
    st.box2("stone", (lo(-4), lo(0), lo(-3)), (hi(4), hi(0), hi(3)), bevel=0.012)
    s.box2("iron", (lo(-4) + 0.02, hi(0), lo(-3) + 0.02), (hi(4) - 0.02, hi(8) - 0.03, hi(3) - 0.02), bevel=0.02)
    for k in range(3):
        s.box2("iron", (lo(-4), hi(0) + 0.1 + k * 0.2, lo(-3)), (hi(4), hi(0) + 0.12 + k * 0.2, hi(3)), bevel=0.004)
    s.box2("iron", (lo(-4) - 0.01, hi(8) - 0.04, lo(-3) - 0.01), (hi(4) + 0.01, hi(8), hi(3) + 0.01), bevel=0.008)
    s.box2("black", (lo(-2), lo(3), hi(3) - 0.02), (hi(2), hi(5), hi(3)), bevel=0.01)          # fire door
    for k in range(5):
        s.box2("iron", (lo(-2) + 0.02 + k * 0.07, lo(3) + 0.02, hi(3)), (lo(-2) + 0.045 + k * 0.07, hi(5) - 0.02, hi(3) + 0.012), bevel=0.002)
    a.p("Glow", "Scrap").box2("ember", (lo(-2) + 0.02, lo(3) + 0.02, hi(3) - 0.022), (hi(2) - 0.02, hi(5) - 0.02, hi(3) - 0.018), bevel=0)
    s.rod("chrome", (c(2.4), c(4), hi(3) + 0.006), (c(2.4), c(4), hi(3) + 0.02), 0.01, 8)
    s.lathe("steel", [(0.0, 0), (0.09, 0), (0.1, 0.02), (0.1, 0.19), (0.105, 0.2), (0.0, 0.2)], (c(-2), hi(8), 0), "y", 18)   # pot
    s.tube("steel", [(c(-2) - 0.1, hi(8) + 0.17, 0), (c(-2), hi(8) + 0.27, 0), (c(-2) + 0.1, hi(8) + 0.17, 0)], 0.005, 5, caps=False)
    s.cyl("rust", 0.06, hi(22) - hi(8) - 0.04, (c(2), (hi(8) + hi(22)) / 2 - 0.02, c(-2)), "y", 14, 0.003)     # flue
    s.cyl("rust", 0.075, 0.02, (c(2), hi(14), c(-2)), "y", 14, 0.003)
    s.lathe("rust", [(0.0, 0.0), (0.12, 0.0), (0.0, 0.05)], (c(2), hi(22) - 0.05, c(-2)), "y", 14)
    return a


def oven():
    a = Asset("oven", "Furniture")
    s, g = a.p("Scrap"), a.p("Glass")
    s.box2("white", (lo(-5), lo(0) + 0.05, lo(-4)), (hi(5), hi(11), hi(3)), bevel=0.025)
    s.box2("black", (lo(-5) + 0.03, lo(0), lo(-4) + 0.03), (hi(5) - 0.03, lo(0) + 0.06, hi(3) - 0.03), bevel=0.004)
    s.box2("white", (lo(-5) + 0.02, lo(1), hi(3) - 0.01), (hi(5) - 0.02, hi(7), hi(3) + 0.03), bevel=0.012)   # door
    g.box2("glass", (lo(-3), lo(2) + 0.02, hi(3) + 0.03), (hi(3), hi(6) - 0.02, hi(3) + 0.036), bevel=0)
    s.rod("chrome", (lo(-4), hi(7) - 0.04, hi(3) + 0.06), (hi(4), hi(7) - 0.04, hi(3) + 0.06), 0.012, 8)
    for x in (-4, 4):
        s.rod("chrome", (c(x), hi(7) - 0.04, hi(3) + 0.03), (c(x), hi(7) - 0.04, hi(3) + 0.06), 0.008, 6)
    s.box2("black", (lo(-5) + 0.01, lo(8), hi(3) - 0.01), (hi(5) - 0.01, hi(10), hi(3) + 0.005), bevel=0.006)
    for x in range(-3, 4, 2):
        knob(s, "chrome", (c(x), c(9), hi(3) + 0.012), 0.022, "z", 0.026)
    s.box2("black", (lo(-4), lo(12) - 0.02, lo(-3)), (hi(4), lo(12) + 0.02, hi(2)), bevel=0.006)          # hob
    for x in (-2, 2):
        for z in (-1.5, 1):
            s.torus("steel_dark", 0.07, 0.008, (c(x), lo(12) + 0.022, c(z)), "y", 16, 5)
    s.box2("white", (lo(-5), hi(11) - 0.02, lo(-4)), (hi(5), hi(12) + 0.03, lo(-4) + 0.06), bevel=0.01)   # splash back
    return a


def chair():
    a = Asset("chair", "Furniture")
    w = a.p("Wood")
    for x in (-3, 3):
        for z in (-3, 3):
            leg_square(w, W("dark", "y"), c(x), c(z), lo(0), hi(5) + (hi(14) - hi(5) if z == -3 else 0), 0.06)
    w.box2(W("", "z"), (lo(-3), lo(6) + 0.02, lo(-3)), (hi(3), hi(6), hi(3)), bevel=0.012)
    for x in (-3, 3):                                    # stretchers
        w.box2(W("dark", "z"), (c(x) - 0.018, lo(2), lo(-3) + 0.03), (c(x) + 0.018, hi(2) - 0.03, hi(3) - 0.03), bevel=0.004)
    w.box2(W("dark", "x"), (lo(-3) + 0.03, lo(4), c(3) - 0.02), (hi(3) - 0.03, hi(4) - 0.04, c(3) + 0.02), bevel=0.004)
    for y in (9, 13):                                    # back rails + slats
        w.box2(W("light", "x"), (lo(-3) + 0.02, lo(y), lo(-3) + 0.01), (hi(3) - 0.02, hi(y), hi(-3) - 0.01), bevel=0.008)
    for x in (-1.2, 0, 1.2):
        w.box2(W("", "y"), (c(x) - 0.02, hi(9), lo(-3) + 0.02), (c(x) + 0.02, lo(13), hi(-3) - 0.02), bevel=0.004)
    return a


def table():
    a = Asset("table", "Furniture")
    w = a.p("Wood")
    for x in (-8, 8):
        for z in (-5, 5):
            leg_square(w, W("dark", "y"), c(x), c(z), lo(0), hi(9), 0.07, taper=0.25)
    for z in (-5, 5):
        w.box2(W("dark", "x"), (lo(-8), lo(9) - 0.04, c(z) - 0.02), (hi(8), hi(9), c(z) + 0.02), bevel=0.004)
    for x in (-8, 8):
        w.box2(W("dark", "z"), (c(x) - 0.02, lo(9) - 0.04, lo(-5)), (c(x) + 0.02, hi(9), hi(5)), bevel=0.004)
    for k in range(5):
        z0 = lo(-6) + k * 0.208
        w.box2(K.WOODS["x"][k % 3], (lo(-9), lo(10), z0 + 0.002), (hi(9), hi(10), z0 + 0.206), bevel=0.008)
    return a


def sofa():
    a = Asset("sofa", "Furniture")
    cl, w = a.p("Cloth"), a.p("Wood")
    for x in (-11, 11):
        for z in (-3, 3):
            w.cyl(W("dark", "y"), 0.025, 0.06, (c(x), lo(0) + 0.03, c(z)), "y", 8, 0.004)
    cl.box2("cloth_olive", (lo(-12), lo(0) + 0.06, lo(-4)), (hi(12), hi(5), hi(4)), bevel=0.04, segs=3)
    cl.box2("cloth_olive", (lo(-12) + 0.02, hi(5) - 0.02, lo(-4)), (hi(12) - 0.02, hi(11), hi(-2)), bevel=0.06, segs=3)    # back
    for x0, x1 in ((-12, -10), (10, 12)):                                                          # arms
        cl.box2("cloth_olive", (lo(x0), hi(5) - 0.04, lo(-4)), (hi(x1), hi(8), hi(4)), bevel=0.06, segs=3)
    for x0, x1 in ((-11 + 0.6, -1), (1, 11 - 0.6)):                                                # seat cushions
        cl.box2("cloth_olive", (lo(x0), lo(6) - 0.02, lo(-1)), (hi(x1), hi(6) + 0.01, hi(4) - 0.02), bevel=0.04, segs=3)
        cl.box2("cloth_olive", (lo(x0) + 0.02, hi(6) - 0.03, lo(-2) + 0.0), (hi(x1) - 0.02, hi(10), lo(-1) + 0.04), bevel=0.05, segs=3, r=(-8, 0, 0))
    cl.box2("cloth_red", (c(4), hi(6) - 0.02, c(0)), (c(7), hi(6) + 0.08, c(2.5)), bevel=0.035, segs=2, r=(0, 20, 12))         # throw pillow
    return a


def tv():
    a = Asset("tv", "Furniture")
    s, g = a.p("Scrap"), a.p("Glass")
    s.box2("black", (lo(-5), lo(0), lo(-3)), (hi(5), hi(1), hi(3)), bevel=0.01)                  # VCR
    s.box2("black", (lo(-4), c(1) - 0.012, hi(3)), (hi(-1), c(1) + 0.012, hi(3) + 0.004), bevel=0.002)
    s.cyl("lamp_red", 0.008, 0.006, (c(3), c(1), hi(3) + 0.002), "z", 8, 0)
    s.box2("steel_dark", (lo(-5), lo(2), lo(-3)), (hi(5), hi(10), hi(3)), bevel=0.04, segs=3)     # cabinet
    s.box2("wood", (lo(-5) - 0.004, lo(2) + 0.04, lo(-3) + 0.02), (hi(5) + 0.004, hi(10) - 0.04, hi(2)), bevel=0.02)
    s.box2("black", (lo(-4) - 0.02, lo(3) - 0.02, hi(3) - 0.01), (hi(3) + 0.02, hi(9) + 0.02, hi(3) + 0.005), bevel=0.01)
    g.superloft("screen", [(hi(3) - 0.01, c(-0.5), c(6), 0.29, 0.26), (hi(3) + 0.012, c(-0.5), c(6), 0.27, 0.24)], 24, 3.4)
    for y in (6, 8):
        knob(s, "chrome", (c(4), c(y), hi(3) + 0.01), 0.02, "z", 0.02)
    s.rod("chrome", (c(-2), hi(10), 0), (c(-3), c(14.5), c(-0.5)), 0.006, 5)
    s.rod("chrome", (c(2), hi(10), 0), (c(3.6), c(13.5), c(0.5)), 0.006, 5)
    s.sphere("black", 0.04, (0, hi(10) + 0.01, 0), (1.4, 0.6, 1), 12, 6)
    return a


def radio():
    a = Asset("radio", "Furniture")
    s, g = a.p("Scrap"), a.p("Glass")
    s.box2("steel", (lo(-5), lo(0) + 0.02, lo(-2)), (hi(5), hi(5), hi(1)), bevel=0.03, segs=3)
    s.box2("black", (lo(-5) + 0.02, lo(0), lo(-2) + 0.02), (hi(5) - 0.02, lo(0) + 0.03, hi(1) - 0.02), bevel=0.004)
    F = hi(2) - 0.03
    s.box2("steel_dark", (lo(-5) + 0.02, lo(1) - 0.02, hi(1) - 0.01), (hi(5) - 0.02, hi(4) + 0.02, F), bevel=0.01)
    for sx in (-3, 3):                                   # speaker grilles
        s.cyl("black", 0.1, 0.012, (c(sx), c(2), F), "z", 20, 0.003)
        for k in range(-3, 4):
            s.box2("chrome", (c(sx) - 0.08, c(2) + k * 0.022 - 0.004, F + 0.004), (c(sx) + 0.08, c(2) + k * 0.022 + 0.004, F + 0.008), bevel=0)
    g.box2("lamp_amber", (lo(-1), lo(3), F - 0.004), (hi(1), hi(4) - 0.02, F + 0.006), bevel=0.003)
    s.box2("lamp_red", (c(0.2) - 0.003, lo(3) + 0.01, F + 0.006), (c(0.2) + 0.003, hi(4) - 0.03, F + 0.009), bevel=0)
    for x, y in ((0, 2), (-1, 1), (1, 1)):
        knob(s, "chrome", (c(x), c(y), F + 0.015), 0.022, "z", 0.03)
    s.tube("black", [(c(-3), hi(5) - 0.01, 0), (c(-3), c(6.8), 0), (c(-2.4), c(7.2), 0), (c(2.4), c(7.2), 0), (c(3), c(6.8), 0), (c(3), hi(5) - 0.01, 0)], 0.016, 8)
    s.rod("chrome", (c(4), hi(5), c(-1)), (c(4.6), hi(11), c(-1.5)), 0.006, 5)
    s.sphere("chrome", 0.01, (c(4.6), hi(11), c(-1.5)), (1, 1, 1), 8, 4)
    return a


def lamp():
    """Pendant oil lamp: hangs from its mount (+Y away from the ceiling)."""
    a = Asset("lamp", "Furniture")
    s, g = a.p("Scrap"), a.p("Glass")
    s.cyl("steel_dark", 0.04, 0.02, (0, lo(0) + 0.01, 0), "y", 12, 0.004)
    s.tube("black", [(0, lo(0) + 0.02, 0), (0, lo(2) + 0.02, 0)], 0.006, 6)
    s.torus("steel", 0.012, 0.004, (0, lo(2) + 0.025, 0), "z", 10, 4)
    s.lathe("steel", [(0.0, 0.0), (0.04, 0.0), (0.06, 0.03), (0.2, 0.09), (0.2, 0.1), (0.05, 0.05), (0.0, 0.05)], (0, lo(2) + 0.04, 0), "y", 24)
    g.lathe("lamp_amber", [(0.0, 0.0), (0.06, 0.0), (0.09, 0.05), (0.08, 0.09), (0.06, 0.12), (0.0, 0.12)], (0, lo(3) + 0.03, 0), "y", 16)
    g.lathe("lamp", [(0.0, 0.0), (0.02, 0.0), (0.0, 0.05)], (0, lo(3) + 0.07, 0), "y", 8)
    return a


def light_ceiling():
    a = Asset("light_ceiling", "Furniture")
    s, g = a.p("Scrap"), a.p("Glass")
    s.lathe("steel", [(0.0, 0.0), (0.27, 0.0), (0.28, 0.02), (0.26, 0.04), (0.0, 0.04)], (0, lo(0), 0), "y", 28)
    g.lathe("lamp", [(0.0, 0.0), (0.2, 0.0), (0.2, 0.03), (0.17, 0.08), (0.1, 0.11), (0.0, 0.12)], (0, lo(0) + 0.035, 0), "y", 28)
    for k in range(4):
        a_ = k * math.pi / 2
        s.cyl("chrome", 0.01, 0.012, (math.cos(a_) * 0.235, lo(0) + 0.045, math.sin(a_) * 0.235), "y", 6, 0)
    return a


def floodlight():
    a = Asset("floodlight", "Furniture")
    s, g = a.p("Scrap"), a.p("Glass")
    s.box2("steel_dark", (lo(-2), lo(0), lo(-2)), (hi(2), hi(0), hi(2)), bevel=0.01)
    s.cyl("steel", 0.028, hi(19) - hi(0), (0, (hi(0) + hi(19)) / 2, 0), "y", 12, 0.003)
    s.box2("steel", (-0.05, lo(19), -0.02), (0.05, lo(20), 0.02), bevel=0.004)
    s.tube("steel_dark", [(-0.17, lo(20) + 0.01, 0), (-0.17, lo(20) - 0.02, 0), (0.17, lo(20) - 0.02, 0), (0.17, lo(20) + 0.01, 0)], 0.01, 6, caps=True)
    s.box2("steel", (lo(-2) + 0.01, lo(20), lo(-1)), (hi(2) - 0.01, hi(23), hi(1)), bevel=0.02, r=(-8, 0, 0))
    for k in range(5):
        s.box2("steel_dark", (lo(-2) + 0.03 + k * 0.07, hi(23) - 0.004, lo(-1) + 0.02), (lo(-2) + 0.045 + k * 0.07, hi(23) + 0.02, hi(1) - 0.04), bevel=0.002, r=(-8, 0, 0))
    g.box2("lamp", (lo(-2) + 0.03, lo(20) + 0.03, hi(1)), (hi(2) - 0.03, hi(23) - 0.03, hi(2)), bevel=0.01, r=(-8, 0, 0))
    s.tube("black", [(0.0, lo(20), -0.05), (0.0, 0.6, -0.06), (0.03, 0.05, -0.06)], 0.006, 5, caps=False)
    return a


def lamppost():
    a = Asset("lamppost", "Furniture")
    fe, g = a.p("Iron"), a.p("Glass")
    fe.lathe("steel_dark", [(0.0, 0.0), (0.12, 0.0), (0.12, 0.05), (0.08, 0.1), (0.05, 0.16), (0.0, 0.16)], (0, lo(0), 0), "y", 16)
    for k in range(4):
        fe.cyl("chrome", 0.01, 0.02, (math.cos(k * 1.5708 + 0.78) * 0.09, lo(0) + 0.055, math.sin(k * 1.5708 + 0.78) * 0.09), "y", 6, 0)
    fe.cyl("steel", 0.035, hi(47) - lo(2), (0, (lo(2) + hi(47)) / 2, 0), "y", 12, 0.003, r2=0.028)
    fe.tube("steel", [(0, hi(46), 0), (0, c(48), 0.08), (0, c(48), c(7))], 0.025, 10)
    fe.lathe("steel_dark", [(0.0, 0.0), (0.03, 0.0), (0.13, -0.06), (0.13, -0.08), (0.0, -0.05)], (0, hi(48) - 0.02, c(8)), "y", 18)
    g.lathe("lamp_amber", [(0.0, 0.0), (0.09, 0.0), (0.07, -0.04), (0.0, -0.06)], (0, hi(48) - 0.08, c(8)), "y", 16)
    fe.box2("hazard", (-0.04, 1.0, -0.04), (0.04, 1.2, 0.04), bevel=0.01)
    return a


def sink():
    a = Asset("sink", "Furniture")
    s, fe = a.p("Scrap"), a.p("Iron")
    s.box2("white", (lo(-6), lo(0) + 0.06, lo(-4)), (hi(6), hi(9) + 0.02, hi(4) - 0.02), bevel=0.012)
    s.box2("black", (lo(-6) + 0.03, lo(0), lo(-4) + 0.03), (hi(6) - 0.03, lo(0) + 0.07, hi(4) - 0.05), bevel=0.004)
    for sx in (-1, 1):                                   # cupboard doors
        x0, x1 = (lo(-6) + 0.02, -0.005) if sx < 0 else (0.005, hi(6) - 0.02)
        s.box2("white", (x0, lo(1), hi(4) - 0.03), (x1, hi(8), hi(4)), bevel=0.008)
        knob(s, "chrome", ((x1 - 0.05) if sx < 0 else (x0 + 0.05), c(6.5), hi(4) + 0.01), 0.014, "z", 0.02)
    # chrome basin with water
    s.box2("chrome", (lo(-6) - 0.004, hi(9), lo(-4)), (hi(6) + 0.004, hi(11) - 0.03, hi(4) + 0.01), bevel=0.012)
    s.box2("chrome", (lo(-5), hi(10) - 0.04, lo(-3)), (hi(5), hi(11), lo(-3) + 0.02), bevel=0.005)
    s.box2("chrome", (lo(-5), hi(10) - 0.04, hi(3) - 0.02), (hi(5), hi(11), hi(3)), bevel=0.005)
    s.box2("chrome", (lo(-5), hi(10) - 0.04, lo(-3)), (lo(-5) + 0.02, hi(11), hi(3)), bevel=0.005)
    s.box2("chrome", (hi(5) - 0.02, hi(10) - 0.04, lo(-3)), (hi(5), hi(11), hi(3)), bevel=0.005)
    s.box2("water", (lo(-4), c(11) - 0.01, lo(-2)), (hi(4), c(11), hi(2)), bevel=0)
    fe.cyl("chrome", 0.018, c(15) - hi(11), (0, (hi(11) + c(15)) / 2, c(-3)), "y", 10, 0.003)
    fe.tube("chrome", [(0, c(15), c(-3)), (0, c(15) + 0.02, c(-2.5)), (0, c(15), c(-1.2)), (0, c(14.4), c(-1))], 0.013, 10)
    for sx in (-1, 1):
        fe.cyl("chrome", 0.016, 0.03, (sx * 0.1, hi(11) + 0.015, c(-3)), "y", 8, 0.003)
        fe.box2("crimson" if sx < 0 else "navy", (sx * 0.1 - 0.03, hi(11) + 0.03, c(-3) - 0.008), (sx * 0.1 + 0.03, hi(11) + 0.045, c(-3) + 0.008), bevel=0.003)
    return a


def shower():
    a = Asset("shower", "Furniture")
    s, fe, cl = a.p("Scrap"), a.p("Iron"), a.p("Cloth")
    s.box2("chrome", (lo(-6), lo(0), lo(-6)), (hi(6), hi(0), hi(6)), bevel=0.012)
    s.box2("steel_dark", (-0.05, hi(0) - 0.004, -0.05), (0.05, hi(0) + 0.002, 0.05), bevel=0.002)
    s.box2("white", (lo(-6), hi(0), lo(-6)), (hi(6), hi(28), hi(-6)), bevel=0.01)
    s.box2("white", (lo(-6), hi(0), hi(-6)), (hi(-6), hi(28), hi(6)), bevel=0.01)
    for y in range(4, 28, 4):                             # tile grout
        s.box2("cream", (lo(-5), c(y) - 0.004, hi(-6)), (hi(6) - 0.01, c(y) + 0.004, hi(-6) + 0.003), bevel=0)
        s.box2("cream", (hi(-6), c(y) - 0.004, lo(-5)), (hi(-6) + 0.003, c(y) + 0.004, hi(6) - 0.01), bevel=0)
    fe.tube("chrome", [(c(0), lo(1), hi(-6) + 0.02), (c(0), c(29), hi(-6) + 0.02), (c(0), c(29), c(-1))], 0.014, 8)
    fe.lathe("chrome", [(0.0, 0.0), (0.02, 0.0), (0.09, -0.04), (0.09, -0.05), (0.0, -0.05)], (0, c(29) - 0.01, 0), "y", 16)
    fe.rod("chrome", (hi(6), hi(27) - 0.01, lo(-6)), (hi(6), hi(27) - 0.01, hi(6)), 0.01, 8)
    for k in range(9):                                    # curtain folds
        z0, z1 = lo(-6) + k * (hi(6) - lo(-6)) / 9, lo(-6) + (k + 1) * (hi(6) - lo(-6)) / 9
        cl.box2("cloth_pale" if k % 2 else "sheet", (c(6) - 0.012 - 0.012 * (k % 2), lo(6), z0), (c(6) + 0.012 - 0.012 * (k % 2), hi(27) - 0.03, z1), bevel=0.006)
    for k in range(9):
        fe.torus("chrome", 0.012, 0.003, (hi(6), hi(27) - 0.01, lo(-6) + (k + 0.5) * 0.115), "z", 8, 4)
    return a


def fireplace():
    a = Asset("fireplace", "Furniture")
    st, w = a.p("Stone"), a.p("Wood")
    open_ = (lo(-5), hi(5), lo(1), hi(8))
    K.slab_xy(st, "brick", lo(-9), hi(9), lo(0), hi(14), lo(-4), lo(0), [], bevel=0.01)                  # back
    for x0, x1 in ((-9, -6), (6, 9)):
        st.box2("brick", (lo(x0), lo(0), lo(0)), (hi(x1), hi(14), hi(3)), bevel=0.012)
    st.box2("brick", (lo(-5), lo(9), lo(0)), (hi(5), hi(14), hi(3)), bevel=0.012)
    st.box2("conc", (lo(-9), hi(14) - 0.04, lo(-4)), (hi(9), hi(14), hi(3)), bevel=0.01)   # mantel
    st.box2("stone", (lo(-9), lo(0), lo(0)), (hi(9), hi(0) + 0.02, hi(3)), bevel=0.01)                     # hearth
    st.box2("black", (lo(-5), hi(0), lo(-3)), (hi(5), hi(1), hi(0)), bevel=0.006)
    st.box2("brick", (lo(-5), lo(15), lo(-3)), (hi(5), hi(30), hi(1)), bevel=0.012)                       # chimney
    st.box2("conc", (lo(-5) - 0.0, hi(30) - 0.04, lo(-3)), (hi(5), hi(30), hi(1)), bevel=0.008)
    st.box2("black", (lo(-5) + 0.02, lo(1) + 0.02, lo(-3) + 0.02), (hi(5) - 0.02, hi(8) - 0.02, lo(0) + 0.002), bevel=0)   # soot
    for k, (x0, x1, y) in enumerate(((-3, 3, 2.3), (-2.6, 2.4, 3.1))):
        w.cyl(W("dark", "x"), 0.045, (x1 - x0) * VS, ((x0 + x1) / 2 * VS, c(y), c(-1) + k * 0.04), "x", 10, 0.006, r_extra=K.rot(0, 15 * (1 - 2 * k), 0))
    a.p("Glow", "Wood").add(K.kit.bm_rock(0.12, 7, 0.7, 1), "flame", Matrix.Translation((0, c(4) - 0.02, c(-2) + 0.06)) @ K._axis_mtx("y") @ Matrix.Diagonal((1.4, 0.6, 1.3, 1)))
    return a


def player_stall():
    a = Asset("player_stall", "Furniture")
    w, cl, s = a.p("Wood"), a.p("Cloth"), a.p("Scrap")
    for x in (-12, 12):
        for z in (-6, 6):
            leg_square(w, W("dark", "y"), c(x), c(z), lo(0), hi(26) - 0.02 - abs(z) * 0.0, 0.07)
    w.box2(W("light", "x"), (lo(-12), lo(10), lo(4)), (hi(12), hi(11), hi(7)), bevel=0.01)              # counter
    for k in range(7):                                    # front boards
        x0 = lo(-12) + k * (hi(12) - lo(-12)) / 7
        w.box2(K.WOODS["y"][k % 3], (x0 + 0.003, lo(0), lo(5)), (x0 + (hi(12) - lo(-12)) / 7 - 0.003, hi(9), hi(7) - 0.04), bevel=0.006)
    # peaked striped awning
    for k in range(9):
        x0 = lo(-13) + k * (hi(13) - lo(-13)) / 9
        x1 = x0 + (hi(13) - lo(-13)) / 9
        m = "cloth_red" if k % 2 == 0 else "cloth_cream"
        cl.panel(m, (x0, hi(25), lo(-8)), (x0, hi(27), 0.0), (x1, hi(27), 0.0), 0.012)
        cl.panel(m, (x0, hi(27), 0.0), (x0, hi(25), hi(8)), (x1, hi(25), hi(8)), 0.012)
        for zz in (lo(-8), hi(8)):                     # scalloped valance
            cl.box2(m, (x0, hi(25) - 0.1, zz - 0.008), (x1, hi(25), zz + 0.008), bevel=0.003)
    w.box2(W("dark", "x"), (lo(-12), hi(27) - 0.06, -0.03), (hi(12), hi(27) - 0.02, 0.03), bevel=0.006)   # ridge pole
    for (x0, x1, y1, m) in ((-9, -4, 5, "wood"), (3, 9, 6, "wood_dark")):                                  # crates below
        w.box2(K.wood("" if m == "wood" else "dark", "x"), (lo(x0), lo(1), lo(-4)), (hi(x1), hi(y1), hi(1)), bevel=0.012)
        for y in range(1, y1, 2):
            w.box2(K.wood("dark", "x"), (lo(x0) - 0.004, c(y) + 0.04, hi(1) - 0.005), (hi(x1) + 0.004, c(y) + 0.06, hi(1) + 0.004), bevel=0.002)
    s.cyl("ochre", 0.07, 0.16, (c(-2), hi(11) + 0.08, c(5.5)), "y", 14, 0.008)                           # wares
    s.box2("crimson", (lo(2), hi(11), lo(5)), (hi(4), hi(12), hi(6)), bevel=0.01)
    s.cyl("tread", 0.11, 0.07, (c(7), hi(11) + 0.035, c(5.5)), "y", 18, 0.01)
    return a


# ------------------------------------------------------------------------------------------ decor
def rug():
    a = Asset("rug", "Decor")
    cl = a.p("Cloth")
    cl.box2("cloth_olive", (lo(-12) + 0.02, lo(0), lo(-8) + 0.02), (hi(12) - 0.02, lo(0) + 0.03, hi(8) - 0.02), bevel=0.006)
    for (x0, x1, z0, z1) in ((lo(-12), hi(12), lo(-8), lo(-6)), (lo(-12), hi(12), hi(6), hi(8)), (lo(-12), lo(-10), lo(-6), hi(6)), (hi(10), hi(12), lo(-6), hi(6))):
        cl.box2("brick", (x0, lo(0), z0), (x1, lo(0) + 0.036, z1), bevel=0.006)
    for cx in (-6, 0, 6):                                 # diamonds
        for r_ in (0.32, 0.2):
            pts = [(c(cx) + r_, 0), (c(cx), r_ * 0.75), (c(cx) - r_, 0), (c(cx), -r_ * 0.75)]
            cl.prism("cloth_cream" if r_ > 0.25 else "cloth_red", pts, lo(0) + 0.03, lo(0) + 0.036 + (0.002 if r_ < 0.25 else 0))
    for z in range(-7, 8):                                 # fringe
        for sx in (-1, 1):
            cl.rod("cloth_cream", (sx * hi(12), lo(0) + 0.01, c(z)), (sx * (hi(12) + 0.0), lo(0) + 0.01, c(z) + 0.0), 0.006, 4)
    return a


def painting():
    a = Asset("painting", "Decor")
    w, cl = a.p("Wood"), a.p("Cloth")
    for (x0, x1, z0, z1, ax) in ((lo(-8), hi(8), lo(-6), lo(-5), "x"), (lo(-8), hi(8), hi(5), hi(6), "x"), (lo(-8), lo(-7), lo(-5), hi(5), "z"), (hi(7), hi(8), lo(-5), hi(5), "z")):
        w.box2(W("dark", ax), (x0, lo(0), z0), (x1, hi(1), z1), bevel=0.012)
    w.box2(W("dark", "x"), (lo(-7), lo(0), lo(-5)), (hi(7), lo(0) + 0.02, hi(5)), bevel=0)
    # canvas: sky, dunes, rusty ruins, a low sun (painted wasteland)
    y = lo(1) + 0.004
    cl.box2("cloth_pale", (lo(-7), lo(0) + 0.02, c(1.5)), (hi(7), y, hi(5)), bevel=0)
    cl.box2("sand", (lo(-7), lo(0) + 0.02, c(-0.5)), (hi(7), y + 0.002, c(1.5)), bevel=0)
    cl.box2("rock", (lo(-7), lo(0) + 0.02, lo(-5)), (hi(7), y + 0.001, c(-0.5)), bevel=0)
    cl.cyl("lamp_amber", 0.07, 0.004, (c(3.5), y + 0.004, c(3)), "y", 16, 0)
    for (x, h) in ((-4, 0.18), (-3.2, 0.26), (-2.5, 0.12)):
        cl.box2("rust", (c(x) - 0.03, y, c(1.5)), (c(x) + 0.03, y + 0.003, c(1.5) + h), bevel=0)
    return a


def flag():
    a = Asset("flag", "Decor")
    s, cl = a.p("Scrap"), a.p("Cloth")
    s.cyl("steel", 0.022, hi(40) - lo(0), (0, (hi(40) + lo(0)) / 2, 0), "y", 10, 0.003)
    s.sphere("brass", 0.03, (0, hi(40), 0), (1, 1, 1), 10, 6)
    s.cyl("steel_dark", 0.06, 0.04, (0, lo(0) + 0.02, 0), "y", 12, 0.005)
    n, m = 14, 6
    for half, mm in ((0, "black"), (1, "cloth_red")):
        y0 = lo(30) + half * (hi(39) - lo(30)) / 2
        y1 = y0 + (hi(39) - lo(30)) / 2
        secs = []
        for i in range(n + 1):
            x = 0.02 + (hi(14) - 0.02) * i / n
            zw = math.sin(i * 0.9) * 0.03 * (i / n)
            secs.append([(x, y0, zw - 0.004), (x, y1, zw - 0.004), (x, y1, zw + 0.004), (x, y0, zw + 0.004)])
        cl.loft(mm, secs)
    return a


def tyres():
    a = Asset("tyres", "Decor")
    r = a.p("Rubber")
    prof = [(0.2, -0.11), (0.24, -0.12), (0.37, -0.115), (0.415, -0.08), (0.425, 0.0), (0.415, 0.08), (0.37, 0.115), (0.24, 0.12), (0.2, 0.11)]
    for k in range(3):
        y = (k * 3 + 1) * VS
        off = ((-0.012, 0.006), (0.01, -0.006), (-0.004, 0.012))[k]
        r.lathe("rubber", prof, (off[0], y, off[1]), "y", 32, close=False, r=(0, k * 23, 0))
        for j in range(18):                               # tread blocks
            an = j / 18 * math.tau + k
            r.box("tread", (0.05, 0.08, 0.016), (off[0] + math.cos(an) * 0.42, y + (0.03 if j % 2 else -0.03), off[1] + math.sin(an) * 0.42),
                  r=(0, -math.degrees(an), 0), bevel=0.0)
    return a


def barrel_drum(gid, cat, rusty=True):
    a = Asset(gid, cat)
    s = a.p("Scrap")
    R = 3.45 * VS
    s.lathe("rust" if rusty else "pump_red", [(0.0, lo(0)), (R - 0.01, lo(0)), (R, lo(0) + 0.01), (R, lo(3) - 0.01), (R + 0.008, lo(3) + 0.01), (R, lo(3) + 0.03),
                                              (R, lo(7) - 0.01), (R + 0.008, lo(7) + 0.01), (R, lo(7) + 0.03), (R, hi(10) - 0.01), (R - 0.01, hi(10)), (0.0, hi(10) - 0.012)], (0, 0, 0), "y", 32)
    s.torus("rust", R - 0.012, 0.01, (0, hi(10) - 0.006, 0), "y", 32, 6)
    s.cyl("steel_dark", 0.025, 0.012, (0.15, hi(10) - 0.004, 0.05), "y", 10, 0.002)
    s.cyl("steel_dark", 0.015, 0.012, (-0.16, hi(10) - 0.004, -0.04), "y", 8, 0.002)
    return a


def sign():
    a = Asset("sign", "Decor")
    w, s = a.p("Wood"), a.p("Scrap")
    w.box2(W("dark", "y"), (lo(0) + 0.005, lo(0), lo(0) + 0.005), (hi(0) - 0.005, hi(14), hi(0) - 0.005), bevel=0.008)
    s.box2("hazard", (lo(-7), lo(10), lo(1)), (hi(7), hi(18), hi(1) - 0.012), bevel=0.012)
    # diagonal black stripes, clipped to the plate
    for k in range(-2, 5):
        x0 = lo(-7) + k * 0.28
        pts = []
        for (px, py) in ((x0, lo(10)), (x0 + 0.12, lo(10)), (x0 + 0.12 + 0.72, hi(18)), (x0 + 0.72, hi(18))):
            pts.append((max(lo(-7) + 0.01, min(hi(7) - 0.01, px)), py))
        if pts[0][0] < pts[1][0] or pts[3][0] < pts[2][0]:
            s.prism("black", [(p[0], p[1]) for p in pts], hi(1) - 0.012, hi(1) - 0.008, plane="xy")
    for x in (-6, 6):
        for y in (11, 17):
            screws(s, "chrome", [(c(x), c(y), hi(1) - 0.006)], 0.012)
    s.box2("rust", (lo(-7), lo(10), hi(1) - 0.013), (lo(-4), lo(12), hi(1) - 0.007), bevel=0.002)
    s.box2("steel_dark", (-0.06, c(11), lo(1) - 0.004), (0.06, c(17), lo(1) + 0.002), bevel=0.002)
    return a


def skull_pole():
    a = Asset("skull_pole", "Decor")
    w, st, cl = a.p("Wood"), a.p("Stone"), a.p("Cloth")
    w.add(K.kit.bm_cyl(0.04, hi(23) - lo(0), 7, 0.0, 0.03), W("dark", "y"), Matrix.Translation((0, (hi(23) + lo(0)) / 2, 0)) @ K._axis_mtx("y"))
    # skull: cranium, cheekbones, jaw, sockets, nose hole, teeth
    st.sphere("bone", 0.17, (0, c(26), -0.02), (1.0, 0.95, 1.1), 18, 12)
    st.sphere("bone", 0.12, (0, c(24.2), 0.06), (1.1, 0.8, 1.0), 14, 8)
    st.box2("bone", (-0.1, lo(23), 0.0), (0.1, c(23.6), 0.17), bevel=0.04)
    for sx in (-1, 1):
        st.sphere("black", 0.045, (sx * 0.06, c(25.2), 0.15), (1, 0.9, 0.6), 10, 6)
    st.sphere("black", 0.02, (0, c(24.4), 0.17), (1, 1.4, 0.6), 8, 5)
    for k in range(-3, 4):
        st.box2("cream", (k * 0.024 - 0.009, c(23.5) - 0.01, 0.16), (k * 0.024 + 0.009, c(23.5) + 0.025, 0.178), bevel=0.003)
    cl.tube("cloth_red", [(0.03, c(20), 0.03), (0.08, c(18), 0.05), (0.1, c(15.5), 0.02)], [0.03, 0.025, 0.012], 6)
    cl.torus("rope", 0.045, 0.012, (0, c(20), 0), "y", 12, 5)
    return a


def flower_pot():
    a = Asset("flower_pot", "Decor")
    st, w = a.p("Stone"), a.p("Wood")
    st.lathe("terracotta", [(0.0, lo(0)), (0.13, lo(0)), (0.16, lo(0) + 0.24), (0.19, lo(0) + 0.25), (0.19, hi(3)), (0.17, hi(3)), (0.15, lo(0) + 0.26), (0.0, lo(0) + 0.26)], (0, 0, 0), "y", 24)
    st.cyl("soil", 0.16, 0.02, (0, hi(3) - 0.03, 0), "y", 20, 0)
    for i in range(5):
        x, z = c(i - 2) * 0.8, c((i * 7) % 3 - 1) * 0.8
        h = hi(8 + i % 3) - hi(3) - 0.02
        top = (x * 1.2, hi(3) + h, z * 1.2)
        w.tube("fern", [(x, hi(3) - 0.02, z), (x * 1.1, hi(3) + h * 0.5, z * 1.1), top], 0.008, 5)
        for k in range(2):
            w.add(K.kit.bm_sphere(0.03, 6, 4), "fern", Matrix.Translation((x * 1.15 + (0.02 if k else -0.02), hi(3) + h * (0.35 + 0.25 * k), z * 1.15)) @ Matrix.Diagonal((1.6, 0.4, 0.8, 1)))
        for j in range(5):
            an = j / 5 * math.tau
            w.add(K.kit.bm_sphere(0.025, 6, 4), "flower_red", Matrix.Translation((top[0] + math.cos(an) * 0.025, top[1], top[2] + math.sin(an) * 0.025)) @ Matrix.Diagonal((1.0, 0.4, 1.0, 1)))
        w.sphere("flower_yellow", 0.014, (top[0], top[1] + 0.008, top[2]), (1, 0.6, 1), 6, 4)
    return a


# ------------------------------------------------------------------------------------------ garden
def field_bed():
    a = Asset("field_bed", "Hidden")
    cl = a.p("Clay")
    cl.box2("soil", (lo(-12), lo(0), lo(-12)), (hi(12), hi(0), hi(12)), bevel=0.004)
    r = K.rng(984)
    for k in range(6):                                   # ridges along z with lumpy crests
        x0 = lo(-12) + (k * 4 + 1) * VS
        secs = []
        for j in range(13):
            z = lo(-12) + (hi(12) - lo(-12)) * j / 12
            h = 0.075 + r.uniform(-0.012, 0.012)
            w = 0.135 + r.uniform(-0.01, 0.01)
            cx = x0 + 1.5 * VS + r.uniform(-0.01, 0.01)
            secs.append([(cx - w, hi(0) - 0.01, z), (cx - w * 0.5, hi(0) + h * 0.8, z), (cx, hi(0) + h, z), (cx + w * 0.5, hi(0) + h * 0.8, z), (cx + w, hi(0) - 0.01, z)])
        cl.loft("soil_dry" if k % 2 else "soil", [s_ + [(s_[-1][0], hi(0) - 0.012, s_[0][2]), (s_[0][0], hi(0) - 0.012, s_[0][2])] for s_ in secs])
    return a


def garden_bed(gid, hx, hz, cat="Garden"):
    a = Asset(gid, cat)
    w, cl = a.p("Wood"), a.p("Clay")
    t = VS
    for (x0, x1, z0, z1, ax) in ((lo(-hx), hi(hx), lo(-hz), lo(-hz) + t, "x"), (lo(-hx), hi(hx), hi(hz) - t, hi(hz), "x"),
                                  (lo(-hx), lo(-hx) + t, lo(-hz) + t, hi(hz) - t, "z"), (hi(hx) - t, hi(hx), lo(-hz) + t, hi(hz) - t, "z")):
        for k in range(2):
            w.box2(K.WOODS[ax][k], (x0, lo(0) + k * 0.12 + 0.002, z0), (x1, lo(0) + (k + 1) * 0.12, z1), bevel=0.006)
    for x in (-hx, hx):
        for z in (-hz, hz):
            w.box2(W("dark", "y"), (c(x) - 0.035, lo(0), c(z) - 0.035), (c(x) + 0.035, hi(2) + 0.02, c(z) + 0.035), bevel=0.006)
    cl.box2("soil", (lo(-hx) + t, lo(0), lo(-hz) + t), (hi(hx) - t, hi(1) - 0.03, hi(hz) - t), bevel=0.0)
    r = K.rng(hx * 31 + hz)
    for k in range(int(hx * hz * 1.2)):                  # clods
        x = r.uniform(lo(-hx) + t + 0.04, hi(hx) - t - 0.04)
        z = r.uniform(lo(-hz) + t + 0.04, hi(hz) - t - 0.04)
        cl.rock("soil", r.uniform(0.025, 0.05), (x, hi(1) - 0.035, z), seed=k, squash=0.5, detail=1)
    return a


def composter():
    a = Asset("composter", "Garden")
    w, cl = a.p("Wood"), a.p("Clay")
    for y in range(0, 11, 2):
        for (x0, x1, z0, z1, ax) in ((lo(-6), hi(6), lo(-6), hi(-6), "x"), (lo(-6), hi(6), lo(6), hi(6), "x"), (lo(-6), hi(-6), lo(-6), hi(6), "z"), (lo(6), hi(6), lo(-6), hi(6), "z")):
            w.box2(K.WOODS[ax][(y // 2) % 3], (x0, lo(y) + 0.004, z0), (x1, hi(y) - 0.004, z1), bevel=0.006)
    for x in (-6, 6):
        for z in (-6, 6):
            w.box2(W("dark", "y"), (c(x) - 0.05, lo(0), c(z) - 0.05), (c(x) + 0.05, hi(10) + 0.03, c(z) + 0.05), bevel=0.008)
    cl.box2("soil", (lo(-5), lo(0), lo(-5)), (hi(5), hi(5), hi(5)), bevel=0)
    cl.rock("soil", 0.42, (0, hi(5) - 0.02, 0), seed=3, squash=0.42, detail=2, scale=(1.05, 1, 1.05))
    r = K.rng(9)
    for k in range(12):
        cl.rock(["leaf", "hay", "soil_dry"][k % 3], r.uniform(0.04, 0.07), (r.uniform(-0.3, 0.3), hi(6) + r.uniform(0, 0.08), r.uniform(-0.3, 0.3)), seed=k + 20, squash=0.4, detail=1)
    return a


def greenhouse():
    a = Asset("greenhouse", "Garden", kind="building")
    w, g = a.p("Wood"), a.p("Glass")
    hx, hz, wall, ridge = 20, 25, 27, 40
    fw = 0.06
    X0, X1, Z0, Z1 = lo(-hx), hi(hx), lo(-hz), hi(hz)
    Ytop = hi(wall)
    # sill plates and wall plates
    for y0, y1 in ((lo(0), hi(0)), (lo(wall), hi(wall))):
        for (a0, a1, b0, b1) in ((X0, X1, Z0, Z0 + fw), (X0, X1, Z1 - fw, Z1), (X0, X0 + fw, Z0, Z1), (X1 - fw, X1, Z0, Z1)):
            w.box2(W("", "x" if b1 - b0 < 0.1 else "z"), (a0, y0, b0), (a1, y1, b1), bevel=0.006)
    # studs
    for z in [z for z in range(-hz, hz + 1) if z % 8 == 0] + [-hz, hz]:
        for x in (-hx, hx):
            w.box2(W("", "y"), (c(x) - fw / 2 if abs(x) < hx else (X0 if x < 0 else X1 - fw), lo(0), c(z) - fw / 2), (c(x) + fw / 2 if abs(x) < hx else (X0 + fw if x < 0 else X1), Ytop, c(z) + fw / 2), bevel=0.005)
    for x in [x for x in range(-hx, hx + 1) if x % 8 == 0] + [-7, 7]:
        for z in (-hz, hz):
            if z == hz and abs(x) < 7:
                continue
            zz0 = Z0 if z < 0 else Z1 - fw
            w.box2(W("", "y"), (c(x) - fw / 2, lo(0), zz0), (c(x) + fw / 2, Ytop, zz0 + fw), bevel=0.005)
    w.box2(W("", "x"), (lo(-7), lo(26), Z1 - fw), (hi(7), hi(26), Z1), bevel=0.005)                      # door head
    # roof: rafters every 8 in z, ridge beam, eaves
    rise = (ridge - wall) * VS
    for z in [z for z in range(-hz, hz + 1) if z % 8 == 0] + [-hz, hz]:
        zz = max(Z0 + fw / 2, min(Z1 - fw / 2, c(z)))
        for sx in (-1, 1):
            w.beam(W("", "x"), (sx * (X1 - 0.02), Ytop, zz), (0.0, Ytop + rise, zz), fw, fw)
    w.box2(W("dark", "z"), (-fw, Ytop + rise - 0.02, Z0), (fw, Ytop + rise + fw / 2, Z1), bevel=0.006)
    # gable studs
    for x in range(-16, 17, 8):
        if x == 0:
            continue
        top = Ytop + (hx - abs(x)) / hx * rise
        for zz in (Z0, Z1 - fw):
            w.box2(W("", "y"), (c(x) - fw / 2, Ytop, zz), (c(x) + fw / 2, top - 0.03, zz + fw), bevel=0.004)
    # glass: walls, gables, roof (panes inset in the frame plane)
    t = 0.006
    for (x0, x1) in ((X0 + 0.02, X0 + 0.026), (X1 - 0.026, X1 - 0.02)):
        g.box2("glass_clear", (x0, hi(0), Z0 + fw), (x1, lo(wall), Z1 - fw), bevel=0)
    g.box2("glass_clear", (X0 + fw, hi(0), Z0 + 0.02), (X1 - fw, lo(wall), Z0 + 0.026), bevel=0)
    for (x0, x1) in ((X0 + fw, lo(-7)), (hi(7), X1 - fw)):
        g.box2("glass_clear", (x0, hi(0), Z1 - 0.026), (x1, lo(wall), Z1 - 0.02), bevel=0)
    for sx in (-1, 1):
        g.panel("glass_clear", (sx * (X1 - 0.01), Ytop + 0.03, Z0), (0.0, Ytop + rise + 0.03, Z0), (0.0, Ytop + rise + 0.03, Z1), t)
    for zz in (Z0 + 0.025, Z1 - 0.025):
        g.tri_plate("glass_clear", (X0 + 0.04, Ytop, zz), (X1 - 0.04, Ytop, zz), (0.0, Ytop + rise - 0.02, zz), t)
    # a potting bench and pots inside
    w.box2(W("light", "z"), (X0 + 0.1, 0.7, Z0 + 0.3), (X0 + 0.6, 0.76, Z1 - 0.5), bevel=0.006)
    for z in (-1.5, 0.0, 1.4):
        w.box2(W("dark", "y"), (X0 + 0.12, lo(0), z - 0.03), (X0 + 0.18, 0.7, z + 0.03), bevel=0.004)
    for k in range(5):
        zz = -1.4 + k * 0.62
        w.lathe("terracotta", [(0.0, 0.0), (0.07, 0.0), (0.09, 0.12), (0.0, 0.12)], (X0 + 0.35, 0.76, zz), "y", 12)
        w.sphere("leaf", 0.08, (X0 + 0.35, 0.92, zz), (1, 0.8, 1), 8, 6)
    return a


def scarecrow():
    a = Asset("scarecrow", "Garden")
    w, cl = a.p("Wood"), a.p("Cloth")
    w.box2(W("dark", "y"), (lo(0), lo(0), lo(0)), (hi(0), hi(20), hi(0)), bevel=0.012)
    w.box2(W("dark", "x"), (lo(-9), lo(16) + 0.01, lo(0) + 0.01), (hi(9), hi(16) - 0.01, hi(0) - 0.01), bevel=0.01)
    cl.superloft("cloth_blue", [(lo(9), 0, 0, 0.3, 0.11), (c(12), 0, 0, 0.32, 0.12), (hi(17) - 0.03, 0, 0, 0.36, 0.11), (hi(17), 0, 0, 0.12, 0.06)], 18, 2.6, axis="y")
    cl.box2("cloth_red", (c(-3), c(11), hi(1) - 0.02), (c(-1), c(13), hi(1) + 0.004), bevel=0.004)        # patch
    for sx in (-1, 1):
        cl.superloft("cloth_blue", [(sx * c(4), 0, c(16), 0.1, 0.09), (sx * hi(8), 0, c(16), 0.09, 0.08)], 14, 2.4, axis="x")
        for k in range(5):
            cl.rod("hay", (sx * hi(8), c(15.5) + k * 0.025, 0), (sx * (hi(10)), c(14) + k * 0.05, (k - 2) * 0.02), 0.008, 4)
    for k in range(8):                                    # straw poking out at the hem
        x = -0.26 + k * 0.075
        cl.rod("hay", (x, lo(9) + 0.03, 0.05 * ((k % 3) - 1)), (x * 1.1, lo(9) - 0.09, 0.07 * ((k % 3) - 1)), 0.007, 4)
    cl.superloft("burlap", [(lo(18), 0, 0, 0.18, 0.15), (c(19.5), 0, 0, 0.26, 0.2), (c(22), 0, 0, 0.25, 0.2), (hi(23), 0, 0, 0.12, 0.1)], 16, 2.2, axis="y")
    cl.torus("rope", 0.17, 0.012, (0, lo(18) + 0.04, 0), "y", 16, 5)
    for x in (-1, 1):                                      # stitched eyes, mouth
        cl.box2("black", (c(x) - 0.03, c(21) - 0.005, 0.19), (c(x) + 0.03, c(21) + 0.005, 0.205), bevel=0, r=(0, 0, 45))
        cl.box2("black", (c(x) - 0.03, c(21) - 0.005, 0.19), (c(x) + 0.03, c(21) + 0.005, 0.205), bevel=0, r=(0, 0, -45))
    cl.tube("black", [(-0.1, c(19), 0.2), (0.0, c(18.7), 0.205), (0.1, c(19), 0.2)], 0.005, 4)
    cl.lathe("hay", [(0.0, 0.0), (0.36, 0.0), (0.36, 0.02), (0.22, 0.03), (0.2, 0.2), (0.0, 0.21)], (0, lo(24), 0), "y", 24)
    cl.torus("cloth_red", 0.205, 0.016, (0, lo(24) + 0.05, 0), "y", 20, 5)
    return a


def trough():
    a = Asset("trough", "Garden")
    w, s, g = a.p("Wood"), a.p("Scrap"), a.p("Glass")
    for x in (-9, 9):
        for z in (-3, 3):
            w.box2(W("dark", "y"), (lo(x), lo(0), lo(z)), (hi(x), hi(2), hi(z)), bevel=0.008)
    w.box2(W("", "x"), (lo(-10), lo(3), lo(-4)), (hi(10), hi(3), hi(4)), bevel=0.006)
    for z in (-4, 4):
        for k in range(2):
            w.box2(K.WOODS["x"][k], (lo(-10), lo(4) + k * 0.16 + 0.003, lo(z)), (hi(10), lo(4) + (k + 1) * 0.16, hi(z)), bevel=0.006)
    for x in (-10, 0, 10):
        w.box2(W("dark", "z"), (lo(x), lo(4), lo(-3)), (hi(x), hi(7), hi(3)), bevel=0.006)
    w.box2("hay", (lo(-9), lo(4), lo(-3)), (hi(-1), hi(5), hi(3)), bevel=0.02)
    r = K.rng(5)
    for k in range(10):
        w.rod("hay", (r.uniform(-0.7, -0.15), hi(5), r.uniform(-0.2, 0.2)), (r.uniform(-0.7, -0.15), hi(5) + 0.03, r.uniform(-0.25, 0.25)), 0.006, 4)
    g.box2("water", (lo(1), lo(4), lo(-3)), (hi(9), hi(5) - 0.02, hi(3)), bevel=0)
    for x in range(-10, 11, 5):
        s.box2("steel_dark", (lo(x) + 0.015, hi(7) - 0.02, lo(-4) - 0.004), (hi(x) - 0.015, hi(7), hi(4) + 0.004), bevel=0.003)
        for z in (-4, 4):
            s.box2("steel_dark", (lo(x) + 0.015, lo(4), c(z) + (0.042 if z > 0 else -0.046)), (hi(x) - 0.015, hi(7), c(z) + (0.046 if z > 0 else -0.042)), bevel=0.002)
    return a


def nest_box():
    a = Asset("nest_box", "Garden")
    w, s, cl = a.p("Wood"), a.p("Scrap"), a.p("Cloth")
    w.box2(W("", "x"), (lo(-8), lo(0), lo(-3)), (hi(8), hi(0), hi(3)), bevel=0.006)
    for k in range(4):
        w.box2(K.WOODS["x"][k % 3], (lo(-8), lo(1) + k * 0.16 + 0.003, lo(-3)), (hi(8), lo(1) + (k + 1) * 0.16, hi(-3)), bevel=0.006)
    for x in (-8, -3, 2, 8):
        w.box2(W("", "z"), (lo(x), lo(1), lo(-2)), (hi(x), hi(8), hi(3)), bevel=0.006)
    w.box2(W("dark", "x"), (lo(-8), lo(1), lo(3)), (hi(8), hi(2), hi(3)), bevel=0.006)
    w.box2("hay", (lo(-7), lo(1), lo(-2)), (hi(7), hi(1) + 0.02, hi(2)), bevel=0.01)
    r = K.rng(23)
    for k in range(24):
        x = r.uniform(-0.55, 0.55)
        w.rod("hay", (x, hi(1) + 0.01, r.uniform(-0.15, 0.15)), (x + r.uniform(-0.08, 0.08), hi(1) + 0.03, r.uniform(-0.18, 0.2)), 0.005, 4)
    for x in (-5, 0, 5):
        cl.sphere("cloth_cream", 0.03, (c(x), hi(1) + 0.045, c(0)), (1, 1.3, 1), 10, 8)
    a.parts["Cloth"].byte = "Wood"
    s.panel("tin_rust", (lo(-9), hi(9), hi(4)), (lo(-9), hi(9) - 0.03, lo(-4)), (hi(9), hi(9) - 0.03, lo(-4)), 0.012)
    return a


def fish_trap():
    a = Asset("fish_trap", "Garden")
    fe, cl, rb = a.p("Iron"), a.p("Cloth"), a.p("Rubber")
    cy, R = c(4), hi(4) - 0.02
    for z in (lo(-6), -0.2, 0.15, hi(6) - 0.02):          # hoops
        fe.torus("rust", R, 0.008, (0, cy, z), "z", 24, 5)
    for k in range(16):                                  # longitudinal wires
        an = k / 16 * math.tau
        fe.rod("steel", (math.cos(an) * R, cy + math.sin(an) * R, lo(-6)), (math.cos(an) * R, cy + math.sin(an) * R, hi(6) - 0.02), 0.004, 4)
    for k in range(8):                                   # mesh rings
        z = lo(-6) + k * (hi(6) - lo(-6)) / 8
        fe.torus("steel", R, 0.003, (0, cy, z), "z", 24, 4)
    fe.cyl("steel_dark", R, 0.01, (0, cy, lo(-6) + 0.005), "z", 24, 0.002)
    fe.lathe("steel", [(R, 0.0), (R - 0.01, 0.0), (0.13, -0.24), (0.14, -0.24)], (0, cy, hi(6) - 0.02), "z", 20, close=False)
    for x in (-4, 4):
        fe.box2("steel_dark", (lo(x) + 0.02, lo(0), lo(-5)), (hi(x) - 0.02, hi(0), hi(5)), bevel=0.006)
    cl.tube("rope", [(0, cy + R, 0), (0.02, c(11), 0.02), (0, c(14.5), 0)], 0.008, 5)
    rb.sphere("pump_red", 0.1, (0, c(15.5), 0), (1, 0.8, 1), 14, 8)
    rb.cyl("white", 0.104, 0.03, (0, c(15.5), 0), "y", 14, 0.004)
    return a


def T_beds():
    return [bed(), sofa(), chair(), table(), rug(), painting()]


def T_storage():
    return [fridge(), locker(), shelf(), crate(), chest(), workbench()]


def T_kitchen():
    return [stove(), oven(), sink(), shower(), fireplace()]


def T_lights():
    return [lamp(), light_ceiling(), floodlight(), lamppost(), tv(), radio()]


def T_decor():
    return [flag(), tyres(), barrel_drum("barrel", "Decor"), sign(), skull_pole(), flower_pot(), player_stall()]


def T_garden():
    return [field_bed(), garden_bed("garden_plot", 11, 6), garden_bed("planter", 4, 4), composter(), trough(), nest_box(), fish_trap(), scarecrow()]


def T_greenhouse():
    return [greenhouse()]


TILES = [("furn_beds", "Bed, sofa, chair, table, rug, painting", [T_beds]),
         ("furn_storage", "Fridge, locker, shelf, crate, chest, workbench", [T_storage]),
         ("furn_kitchen", "Wood stove, electric oven, sink, shower, fireplace", [T_kitchen]),
         ("furn_lights", "Oil lamp, ceiling light, floodlight, lamp post, TV, radio", [T_lights]),
         ("furn_decor", "Flag, tyre stack, barrel, keep-out sign, skull pole, flower pot, market stall", [T_decor]),
         ("furn_garden", "Field bed, garden plot, planter, composter, trough, nest box, fish trap, scarecrow", [T_garden]),
         ("furn_greenhouse", "Greenhouse (timber frame, glass)", [T_greenhouse])]

if __name__ == "__main__":
    run(TILES, "home_")
