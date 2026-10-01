"""HD pieces of FurnitureLibrary.Homestead.cs / .Kitchen.cs / .Farm.cs: patchwork awning, porch lanterns, tin herb
planter, campfire, kitchen range, cannery, irrigation timer, grain silo.

blender -b -P homestead.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fcommon import K, Asset, Vector, Matrix, lo, hi, c, W, knob, screws, leg_square, drawer_front, bulb, run, VS  # noqa: E402


def porch_awning():
    a = Asset("porch_awning", "Furniture")
    w, s, cl = a.p("Wood"), a.p("Scrap"), a.p("Cloth")
    for x in (-24, 24):
        for z in (-17, 17):
            w.box2(W("", "y"), (lo(x), lo(0), lo(z)), (hi(x + 1), hi(33), hi(z + 1)), bevel=0.012)
            s.box2("steel_dark", (lo(x - 1), lo(2), lo(z - 1)), (hi(x + 2), hi(3), hi(z + 2)), bevel=0.006)
            for k in range(4):
                s.cyl("chrome", 0.008, 0.01, (c(x + 0.5) + (0.09 if k % 2 else -0.09) if k < 2 else c(x + 0.5), c(2.5), c(z + 0.5) + (0.0 if k < 2 else (0.09 if k == 2 else -0.09))), "y", 6, 0)
    for z in (-17, 17):
        w.box2(W("light", "x"), (lo(-24), lo(32), lo(z)), (hi(25), hi(33), hi(z + 1)), bevel=0.01)
    for x in (-24, 25):
        w.box2(W("light", "z"), (lo(x), hi(33) - 0.06, lo(-17)), (hi(x), hi(33), hi(18)), bevel=0.008)
    # cloth: peaked in z, stripes along x (cream / rig green, 12-voxel period, 5 wide), sewn on patches
    n = 9
    for k in range(18):
        x0 = lo(-26) + k * (hi(27) - lo(-26)) / 18
        x1 = x0 + (hi(27) - lo(-26)) / 18
        m = "cloth_cream" if (k % 2 == 0) else "cloth_green"
        prof = [(z, hi(34) + (1 - abs(z) / (20 * VS)) * 4 * VS) for z in (lo(-19), 0.0, hi(20))]
        cl.panel(m, (x0, prof[0][1], prof[0][0]), (x0, prof[1][1], 0.0), (x1, prof[1][1], 0.0), 0.012)
        cl.panel(m, (x0, prof[1][1], 0.0), (x0, prof[2][1], prof[2][0]), (x1, prof[2][1], prof[2][0]), 0.012)
        for j in range(2):                                 # scalloped front valance
            xa = x0 + j * (x1 - x0) / 2
            depth = 0.2 if (k * 2 + j) % 2 == 0 else 0.13
            cl.box2(m, (xa, hi(34) - depth, hi(20) - 0.012), (xa + (x1 - x0) / 2, hi(34), hi(20)), bevel=0.003)
    cl.panel("cloth_ochre", (c(7), hi(36) + 0.02, c(8)), (c(7), hi(36) - 0.01, c(13)), (c(14), hi(36) - 0.01, c(13)), 0.014)
    for k in range(6):                                     # stitches round the patch
        cl.box2("black", (c(7) + k * 0.1, hi(36) + 0.006, c(8) - 0.01), (c(7) + k * 0.1 + 0.04, hi(36) + 0.012, c(8) + 0.01), bevel=0)
    return a


def porch_lights():
    a = Asset("porch_lights", "Decor")
    w, s, g = a.p("Wood"), a.p("Scrap"), a.p("Glass")
    for x in (-23, 23):
        w.box2(W("", "y"), (lo(x), lo(0), lo(0)), (hi(x), hi(34), hi(0)), bevel=0.012)
    pts = [(c(x), c(30 + round(x * x / 132.0)) - 0.0, 0.0) for x in range(-23, 24, 2)]
    s.tube("black", pts, 0.006, 4, caps=False)
    for x in range(-23, 24):
        if (x + 18) % 9:
            continue
        y = c(30 + round(x * x / 132.0))
        s.rod("black", (c(x), y, 0), (c(x), y - 0.06, 0), 0.004, 4)
        s.lathe("brass", [(0.0, 0.0), (0.12, 0.0), (0.12, 0.012), (0.03, 0.04), (0.0, 0.05)], (c(x), y - 0.12, 0), "y", 12)   # cap
        s.lathe("brass", [(0.0, 0.0), (0.1, 0.0), (0.1, 0.02), (0.0, 0.02)], (c(x), y - 0.44, 0), "y", 12)                    # base
        for k in range(4):
            an = k * math.pi / 2 + math.pi / 4
            s.rod("brass", (c(x) + math.cos(an) * 0.08, y - 0.43, math.sin(an) * 0.08), (c(x) + math.cos(an) * 0.09, y - 0.12, math.sin(an) * 0.09), 0.005, 4)
        g.lathe("lamp", [(0.0, 0.0), (0.05, 0.02), (0.07, 0.12), (0.05, 0.22), (0.0, 0.24)], (c(x), y - 0.42, 0), "y", 12)
    return a


def herb_planter():
    a = Asset("herb_planter", "Decor")
    s, w = a.p("Scrap"), a.p("Wood")
    s.box2("blue", (lo(-6), lo(0), lo(-3)), (hi(6), hi(4), hi(3)), bevel=0.02)
    s.box2("blue", (lo(-6) - 0.012, hi(4) - 0.03, lo(-3) - 0.012), (hi(6) + 0.012, hi(4), hi(3) + 0.012), bevel=0.006)
    for x in (-5, 5):
        s.box2("steel_dark", (c(x) - 0.006, lo(1), lo(-3) - 0.004), (c(x) + 0.006, hi(3), hi(3) + 0.004), bevel=0)
    s.box2("white", (lo(-3), c(1.5), hi(3) - 0.002), (hi(3), c(2.6), hi(3) + 0.004), bevel=0)               # faded label
    w.box2("soil", (lo(-5), hi(4) - 0.06, lo(-2)), (hi(5), hi(4) - 0.01, hi(2)), bevel=0)
    r = K.rng(2822)
    for x in (-4, 0, 4):                                   # three herb tufts with flower buds
        for k in range(9):
            an = k / 9 * math.tau + r.uniform(0, 0.4)
            h = r.uniform(0.2, 0.5)
            top = (c(x) + math.cos(an) * 0.05, hi(4) + h, math.sin(an) * 0.05)
            w.tube("fern", [(c(x), hi(4) - 0.02, 0), (c(x) + math.cos(an) * 0.03, hi(4) + h * 0.6, math.sin(an) * 0.03), top], 0.006, 4)
            w.add(K.kit.bm_sphere(0.035, 6, 4), "fern", Matrix.Translation((top[0], top[1] - 0.12, top[2])) @ Matrix.Rotation(an, 4, "Y") @ Matrix.Diagonal((1.4, 0.3, 0.7, 1)))
        w.sphere("cloth_cream", 0.022, (c(x), c(11), 0), (1, 1, 1), 8, 5)
    return a


def campfire():
    a = Asset("campfire", "Furniture")
    st, w, s, gl = a.p("Stone"), a.p("Wood"), a.p("Scrap"), a.p("Glow", "Wood")
    for k in range(14):
        t = k / 14 * math.tau
        st.rock("stone" if k % 3 else "rock_grey", 0.085 + (k % 2) * 0.02, (math.cos(t) * 5.5 * VS, lo(0) + 0.05, math.sin(t) * 5.5 * VS), seed=k, squash=0.75, detail=1)
    st.cyl("ash", 0.28, 0.02, (0, lo(0) + 0.01, 0), "y", 18, 0.004)
    gl.add(K.kit.bm_rock(0.12, 3, 0.5, 1), "ember", Matrix.Translation((0, lo(1), 0)) @ K._axis_mtx("y"))
    for (p0, p1, m) in (((-4, 1, 0), (4, 1, 0), W("", "x")), ((0, 2, -4), (0, 2, 4), W("dark", "z")), ((-3, 1.5, -3), (2.5, 2.4, 2.5), W("", "x"))):
        w.rod(m, tuple(c(v) for v in p0), tuple(c(v) for v in p1), 0.04, 8)
    for (p0, p1) in (((-4, 1, 0), (4, 1, 0)), ((0, 2, -4), (0, 2, 4))):
        for q in (p0, p1):
            w.cyl(W("light", "x"), 0.041, 0.004, tuple(c(v) for v in q), "x" if p0[0] != p1[0] else "z", 8, 0)
    gl.lathe("flame", [(0.0, 0.0), (0.09, 0.03), (0.06, 0.12), (0.02, 0.25), (0.0, 0.32)], (0, c(2.2), 0), "y", 10)
    gl.lathe("lamp_amber", [(0.0, 0.0), (0.05, 0.02), (0.03, 0.1), (0.0, 0.2)], (0.05, c(2.4), 0.03), "y", 8)
    for x in (-7, 7):                                      # forked posts + spit
        w.rod(W("", "y"), (c(x), lo(0), 0), (c(x), c(8.6), 0), 0.022, 6)
        w.rod(W("", "y"), (c(x), c(8.4), 0), (c(x), c(9.4), -0.07), 0.014, 5)
        w.rod(W("", "y"), (c(x), c(8.4), 0), (c(x), c(9.4), 0.07), 0.014, 5)
    w.rod(W("light", "x"), (lo(-8), c(9), 0), (hi(8), c(9), 0), 0.014, 6)
    s.rod("chrome", (c(2), c(9), 0), (c(2), c(5.6), 0), 0.004, 4)
    s.tube("chrome", [(c(2) - 0.1, c(5.4), 0), (c(2), c(5.9), 0), (c(2) + 0.1, c(5.4), 0)], 0.003, 4, caps=False)
    s.lathe("steel_dark", [(0.0, 0.0), (0.11, 0.0), (0.12, 0.02), (0.12, 0.2), (0.0, 0.2)], (c(2), lo(3), 0), "y", 16)
    w.sphere("cloth_red", 0.1, (c(-2), c(9), 0), (1.4, 0.8, 0.8), 10, 6)
    w.sphere("leather", 0.09, (c(-2), c(9) - 0.02, 0), (1.5, 0.75, 0.85), 10, 6)
    a.parts["Glow"].byte = "Wood"
    return a


def kitchen_range():
    a = Asset("kitchen_range", "Furniture")
    s, g = a.p("Scrap"), a.p("Glass")
    s.box2("chrome", (lo(-7), lo(0) + 0.05, lo(-4)), (hi(7), hi(10), hi(4) - 0.02), bevel=0.02)
    s.box2("black", (lo(-7) + 0.01, lo(0), hi(4) - 0.06), (hi(7) - 0.01, lo(0) + 0.06, hi(4)), bevel=0.004)
    s.box2("chrome", (lo(-5), lo(2), hi(4) - 0.02), (hi(5), hi(6), hi(4)), bevel=0.012)
    g.box2("glass", (lo(-4), lo(3), hi(4) - 0.002), (hi(4), hi(5), hi(4) + 0.006), bevel=0.004)
    g.cyl("lamp_amber", 0.02, 0.006, (0, c(3), hi(4) + 0.004), "z", 8, 0)
    s.rod("chrome", (lo(-5), c(8), hi(4) + 0.03), (hi(5), c(8), hi(4) + 0.03), 0.012, 8)
    for x in (-5, 5):
        s.rod("chrome", (c(x), c(8), hi(4) - 0.01), (c(x), c(8), hi(4) + 0.03), 0.008, 6)
    for i in range(4):
        knob(s, "black", (c(-5 + i * 3), c(9.3), hi(4) - 0.01), 0.022, "z", 0.03)
    s.box2("black", (lo(-7) + 0.02, hi(10) - 0.01, lo(-4) + 0.02), (hi(7) - 0.02, hi(10) + 0.006, hi(4) - 0.04), bevel=0.004)
    for x in (-4, 4):
        for z in (-2, 2):
            s.cyl("black", 0.11, 0.02, (c(x), hi(10) + 0.012, c(z)), "y", 18, 0.004)
            s.torus("steel_dark", 0.08, 0.008, (c(x), hi(10) + 0.025, c(z)), "y", 16, 4)
    s.lathe("steel", [(0.0, 0.0), (0.16, 0.0), (0.19, 0.08), (0.18, 0.09), (0.15, 0.015), (0.0, 0.015)], (c(4), hi(10) + 0.03, c(2)), "y", 20)
    s.rod("black", (c(4) + 0.18, hi(10) + 0.09, c(2)), (hi(9), hi(10) + 0.11, c(2)), 0.014, 6)
    s.box2("white", (lo(-7), hi(10), lo(-4)), (hi(7), hi(20), hi(-4)), bevel=0.01)                         # splashback
    for y in range(12, 21, 2):
        s.box2("cream", (lo(-7) + 0.01, c(y) - 0.003, hi(-4)), (hi(7) - 0.01, c(y) + 0.003, hi(-4) + 0.002), bevel=0)
    s.add(K.kit.bm_loft([[(lo(-7), lo(21), lo(-4)), (hi(7), lo(21), lo(-4)), (hi(7), lo(21), hi(-1)), (lo(-7), lo(21), hi(-1))],
                         [(lo(-6), hi(23), lo(-4)), (hi(6), hi(23), lo(-4)), (hi(6), hi(23), hi(-3)), (lo(-6), hi(23), hi(-3))]]), "chrome")
    s.box2("black", (lo(-6), lo(21) - 0.004, hi(-3) - 0.1), (hi(6), lo(21) + 0.004, hi(-1) - 0.02), bevel=0)
    return a


def cannery():
    a = Asset("cannery", "Industry")
    s = a.p("Scrap")
    s.box2("steel", (lo(-14), lo(8), lo(-5)), (hi(8), hi(9), hi(5)), bevel=0.012)
    for x in (-13, 7):
        for z in (-4, 4):
            s.box2("steel_dark", (lo(x) + 0.01, lo(0), lo(z) + 0.01), (hi(x) - 0.01, hi(7), hi(z) - 0.01), bevel=0.006)
    s.box2("steel_dark", (lo(-13), lo(2), lo(-4)), (hi(7), lo(2) + 0.03, hi(4)), bevel=0.004)
    s.box2("black", (lo(-13), lo(10), lo(-2)), (hi(7), hi(10), hi(2)), bevel=0.012)                    # belt
    for x in (-13, 7):
        s.cyl("steel", 0.08, hi(2) - lo(-2) + 0.02, (c(x), c(10), 0), "z", 14, 0.004)
    for x in range(-12, 6, 3):                             # cans
        s.cyl("chrome", 1.2 * VS + 0.01, hi(13) - lo(11), (c(x), (lo(11) + hi(13)) / 2, 0), "y", 14, 0.006)
        s.cyl("steel_dark", 1.2 * VS + 0.012, 0.012, (c(x), lo(11) + 0.06, 0), "y", 14, 0.002)
    s.box2("steel", (lo(-3), lo(11), lo(-4)), (hi(-1), hi(20), hi(-4)), bevel=0.01)                    # press
    s.box2("steel", (lo(-3), lo(20), lo(-4)), (hi(-1), hi(20), hi(2)), bevel=0.01)
    s.box2("ochre", (lo(-3), lo(16), lo(-1)), (hi(-1), hi(18), hi(1)), bevel=0.012)
    s.rod("chrome", (c(-2), lo(18), 0), (c(-2), lo(20), 0), 0.02, 8)
    R = 4.5 * VS + 0.02                                    # retort
    s.lathe("chrome", [(0.0, lo(0)), (R, lo(0)), (R, hi(16) - 0.1), (R * 0.7, hi(16)), (0.0, hi(16) + 0.02)], (c(13), 0, 0), "y", 24)
    for y in (3, 8, 13):
        s.torus("steel_dark", R + 0.004, 0.012, (c(13), c(y), 0), "y", 24, 4)
    s.cyl("steel_dark", 2 * VS + 0.02, hi(18) - lo(17), (c(13), (lo(17) + hi(18)) / 2, 0), "y", 14, 0.006)
    s.torus("crimson", 0.07, 0.012, (c(13), hi(19) - 0.02, 0), "y", 12, 4)
    s.tube("rust", [(c(9), c(12), 0), (c(12) - R + 0.06, c(12), 0)], 0.03, 8)
    s.cyl("black", 0.08, 0.03, (c(11), c(9), hi(4)), "z", 14, 0.004)
    s.cyl("white", 0.06, 0.006, (c(11), c(9), hi(4) + 0.016), "z", 14, 0)
    return a


def irrigation_timer():
    a = Asset("irrigation_timer", "Utility")
    w, s = a.p("Wood"), a.p("Scrap")
    w.add(K.kit.bm_cyl(0.04, hi(7) - lo(0), 4, 0.004, 0.02), W("", "y"), Matrix.Translation((0, (lo(0) + hi(7)) / 2, 0)) @ Matrix.Rotation(math.radians(45), 4, "Y") @ K._axis_mtx("y"))
    s.box2("rig_green", (lo(-2), lo(7), lo(-1)), (hi(2), hi(11), hi(1)), bevel=0.02)
    s.cyl("brass", 1.4 * VS + 0.01, 0.03, (0, c(9), hi(1) + 0.012), "z", 18, 0.006)
    s.cyl("cream", 1.4 * VS - 0.01, 0.006, (0, c(9), hi(1) + 0.03), "z", 18, 0)
    s.box2("black", (-0.005, c(9), hi(1) + 0.03), (0.005, c(9) + 0.08, hi(1) + 0.036), bevel=0)
    for x in (-2, 2):
        s.rod("steel", (c(x), c(5.5), 0), (c(x), c(5.5), hi(1)), 0.016, 8)
        s.box2("crimson", (c(x) - 0.03, c(6.2), -0.01), (c(x) + 0.03, c(6.6), 0.01), bevel=0.004)
    s.rod("rust", (lo(-1), c(3), 0), (hi(1), c(3), 0), 0.02, 8)
    return a


def grain_silo():
    a = Asset("grain_silo", "Garden", kind="building")
    fe = a.p("Iron")
    for x in (-9, 9):
        for z in (-9, 9):
            fe.box2("steel_dark", (lo(x), lo(0), lo(z)), (hi(x), hi(8), hi(z)), bevel=0.008)
    for (p0, p1) in (((-9, -9), (9, 9)), ((9, -9), (-9, 9))):
        fe.rod("steel_dark", (c(p0[0]), c(1), c(p0[1])), (c(p1[0]), c(7), c(p1[1])), 0.012, 5)
    R = 13.5 * VS
    fe.cyl("steel_dark", R, VS, (0, c(8), 0), "y", 36, 0.006)
    # corrugated wall: horizontal corrugation rings
    for k in range(32):
        y0 = lo(9) + k * (hi(40) - lo(9)) / 32
        fe.lathe("tin" if k % 2 else "tin_v", [(R - 0.03, y0), (R + 0.005, y0), (R + 0.02, y0 + 0.04), (R + 0.005, y0 + (hi(40) - lo(9)) / 32), (R - 0.03, y0 + (hi(40) - lo(9)) / 32)], (0, 0, 0), "y", 36, close=False)
    fe.lathe("tin", [(0.0, hi(50)), (0.2, hi(50) - 0.02), (R + 0.06, lo(41)), (R + 0.06, lo(41) - 0.03), (0.0, lo(41))], (0, 0, 0), "y", 36)
    fe.lathe("steel_dark", [(0.0, hi(50) - 0.04), (0.22, hi(50) - 0.04), (0.22, hi(50) + 0.04), (0.0, hi(50) + 0.04)], (0, 0, 0), "y", 12)
    for z in (-2, 2):                                      # side ladder (+x)
        fe.box2("steel", (hi(13) - 0.02, lo(10), c(z) - 0.02), (hi(14), hi(40), c(z) + 0.02), bevel=0.004)
    for y in range(10, 41, 2):
        fe.rod("steel", (hi(13) + 0.03, c(y), c(-2)), (hi(13) + 0.03, c(y), c(2)), 0.01, 5)
    fe.prism("rust", [(lo(-15), lo(4)), (hi(-12), lo(4) + 0.1), (hi(-12), hi(8)), (lo(-15), lo(8))], lo(-2), hi(2), plane="zy")   # chute
    return a


TILES = [("furn_homestead", "Patchwork awning, porch lanterns, tin herb planter", [lambda: [porch_awning(), porch_lights(), herb_planter()]]),
         ("furn_cooking", "Campfire with spit, kitchen range, cannery", [lambda: [campfire(), kitchen_range(), cannery()]]),
         ("furn_farm", "Irrigation timer, grain silo", [lambda: [irrigation_timer(), grain_silo()]])]

if __name__ == "__main__":
    run(TILES, "homestead_")
