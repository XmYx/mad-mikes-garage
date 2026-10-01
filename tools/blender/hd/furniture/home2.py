"""HD pieces of FurnitureLibrary.Home.cs (armchair, bench, dining table, wardrobe, bookshelf, weapon rack, mirror,
bathtub, kitchen counter, latrine, sewing table, wall clock, trophy mount). Wall pieces are authored in the XZ plane
(+Y out of the wall), like the voxel grids.

blender -b -P home2.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fcommon import K, Asset, Vector, Matrix, lo, hi, c, W, knob, screws, leg_square, drawer_front, bulb, run, VS  # noqa: E402


def armchair():
    a = Asset("armchair", "Furniture")
    cl, w = a.p("Cloth"), a.p("Wood")
    M = "leather"
    for x in (-6, 6):
        for z in (-5, 5):
            w.lathe(K.wood("dark", "y"), [(0.0, lo(0)), (0.03, lo(0)), (0.04, hi(0)), (0.0, hi(0))], (c(x) * 0.92, 0, c(z) * 0.9), "y", 10)
    cl.box2(M, (lo(-6), lo(1), lo(-5)), (hi(6), hi(5) - 0.02, hi(5)), bevel=0.05, segs=3)
    cl.box2(M, (lo(-6) + 0.02, hi(5) - 0.06, lo(-5)), (hi(6) - 0.02, hi(15), hi(-3)), bevel=0.07, segs=3)
    for x0, x1 in ((-6, -5), (5, 6)):
        cl.superloft(M, [(lo(-5), (c(x0) + c(x1)) / 2, c(7.5), 0.08, 0.15), (hi(5) - 0.04, (c(x0) + c(x1)) / 2, c(7.5), 0.08, 0.15),
                         (hi(5), (c(x0) + c(x1)) / 2, c(7.5), 0.06, 0.12)], 16, 2.2)
    cl.box2(M, (lo(-4) - 0.02, hi(5) - 0.04, lo(-2)), (hi(4) + 0.02, hi(6) + 0.03, hi(4) + 0.04), bevel=0.05, segs=3)   # seat cushion
    for x in (-4, 0, 4):                                  # tufting buttons on the back
        for y in (9, 13):
            cl.sphere("brass", 0.015, (c(x), c(y), hi(-3) + 0.005), (1, 1, 0.6), 8, 5)
    return a


def bench():
    a = Asset("bench", "Furniture")
    w = a.p("Wood")
    for x in (-10, 10):
        for z in (-2, 2):
            w.box2(W("dark", "y"), (lo(x) + 0.005, lo(0), lo(z) + 0.005), (hi(x) - 0.005, lo(5), hi(z) - 0.005), bevel=0.008)
        w.box2(W("dark", "z"), (lo(x) + 0.01, lo(1), lo(-1)), (hi(x) - 0.01, hi(1), hi(1)), bevel=0.006)
    for k in range(5):
        z0 = lo(-2) + k * 0.08
        w.box2(K.WOODS["x"][k % 3], (lo(-12), lo(5), z0 + 0.003), (hi(12), hi(5), z0 + 0.077), bevel=0.006)
    w.box2(W("dark", "x"), (lo(-10), lo(5) - 0.04, -0.03), (hi(10), lo(5), 0.03), bevel=0.004)
    return a


def dining_table():
    a = Asset("dining_table", "Furniture")
    w, cl, s, g = a.p("Wood"), a.p("Cloth"), a.p("Scrap"), a.p("Glass")
    for x in (-13, 13):
        for z in (-6, 6):
            leg_square(w, W("dark", "y"), c(x), c(z), lo(0), hi(9), 0.08, taper=0.2)
    for z in (-6, 6):
        w.box2(W("dark", "x"), (lo(-13), lo(8), c(z) - 0.025), (hi(13), hi(8) + 0.02, c(z) + 0.025), bevel=0.004)
    for x in (-13, 13):
        w.box2(W("dark", "z"), (c(x) - 0.025, lo(8), lo(-6)), (c(x) + 0.025, hi(8) + 0.02, hi(6)), bevel=0.004)
    for k in range(5):
        z0 = lo(-7) + k * (hi(7) - lo(-7)) / 5
        w.box2(K.WOODS["x"][k % 3], (lo(-15), lo(10), z0 + 0.002), (hi(15), hi(10), z0 + (hi(7) - lo(-7)) / 5 - 0.002), bevel=0.01)
    # table runner (checked), brass candle holder, candle, flame
    for k in range(25):
        x0 = lo(-12) + k * (hi(12) - lo(-12)) / 25
        cl.box2("cloth_cream" if k % 4 == 0 else "cloth_red", (x0, hi(10), lo(-2)), (x0 + (hi(12) - lo(-12)) / 25, hi(10) + 0.008, hi(2)), bevel=0)
    s.lathe("brass", [(0.0, 0.0), (0.06, 0.0), (0.06, 0.012), (0.02, 0.03), (0.018, 0.07), (0.035, 0.08), (0.035, 0.09), (0.0, 0.09)], (0, hi(10) + 0.008, 0), "y", 16)
    w.cyl("cloth_cream", 0.018, 0.14, (0, hi(10) + 0.17, 0), "y", 10, 0.002)
    g.lathe("flame", [(0.0, 0.0), (0.012, 0.012), (0.0, 0.05)], (0, hi(10) + 0.24, 0), "y", 8)
    a.parts["Glass"].byte = "Wood"
    for x in (-8, 8):                                     # plates and mugs
        s.cyl("enamel", 0.09, 0.012, (c(x), hi(10) + 0.006, c(4)), "y", 18, 0.004)
        s.cyl("enamel", 0.035, 0.08, (c(x) + 0.14, hi(10) + 0.04, c(4.5)), "y", 12, 0.004)
    return a


def wardrobe():
    a = Asset("wardrobe", "Furniture")
    w, fe = a.p("Wood"), a.p("Iron")
    w.box2(W("dark", "y"), (lo(-7), lo(1), lo(-4)), (hi(7), hi(25), hi(4)), bevel=0.012)
    w.box2(W("", "x"), (lo(-8), lo(26), lo(-5)), (hi(8), hi(26), hi(5)), bevel=0.012)
    w.box2(W("dark", "x"), (lo(-7), lo(0), lo(-4)), (hi(7), hi(0), hi(4)), bevel=0.01)
    for sx in (-1, 1):                                    # two doors with raised panels
        x0, x1 = (lo(-7) + 0.01, -0.004) if sx < 0 else (0.004, hi(7) - 0.01)
        w.box2(W("", "y"), (x0, lo(1) + 0.01, hi(4) - 0.01), (x1, hi(25) - 0.01, hi(4) + 0.02), bevel=0.006)
        for (y0, y1) in ((3, 11), (14, 23)):
            px0 = c(-6) if sx < 0 else c(2)
            w.box2(W("light", "y"), (px0 - 0.03, lo(y0), hi(4) + 0.02), (px0 + c(3) + 0.03, hi(y1), hi(5) - 0.01), bevel=0.014)
    fe.sphere("chrome", 0.016, (c(-1) + 0.03, c(13), hi(5) - 0.01), (1, 1, 1), 8, 6)
    fe.sphere("chrome", 0.016, (c(1) - 0.03, c(13), hi(5) - 0.01), (1, 1, 1), 8, 6)
    for x in (-7, 7):
        for y in (4, 22):
            fe.box2("brass", (c(x) - 0.012 * x / 7 * 0, c(y), hi(4) + 0.0), (c(x) + 0.0, c(y) + 0.08, hi(4) + 0.024), bevel=0.003)
    return a


def bookshelf():
    a = Asset("bookshelf", "Furniture")
    w, cl = a.p("Wood"), a.p("Cloth")
    for x in (-9, 9):
        w.box2(W("dark", "y"), (lo(x), lo(0), lo(-3)), (hi(x), hi(24), hi(3)), bevel=0.01)
    w.box2(W("dark", "y"), (lo(-8), lo(0), lo(-3)), (hi(8), hi(24), hi(-3)), bevel=0.004)
    for y in (0, 8, 16, 24):
        w.box2(W("", "x"), (lo(-8), lo(y), lo(-2)), (hi(8), hi(y), hi(3)), bevel=0.006)
    spines = ["rust", "cloth_olive", "cloth_pale", "cloth_cream", "rig_green", "leather", "cloth_red", "cloth_blue"]
    for shelf in (1, 9, 17):
        r = K.rng(1123 + shelf)
        x = lo(-8) + 0.005
        while x < hi(8) - 0.04:
            wdt = r.uniform(0.035, 0.07)
            h = r.uniform(0.3, 0.52)
            if r.random() < 0.1:
                x += 0.1
                continue
            lean = r.uniform(-4, 4) if r.random() < 0.15 else 0
            if x + wdt > hi(8) - 0.005:
                break
            m = spines[r.randrange(len(spines))]
            cl.box(m, (wdt - 0.004, h, r.uniform(0.3, 0.38)), (x + wdt / 2, lo(shelf) + h / 2, 0.02), r=(0, 0, lean), bevel=0.006)
            if r.random() < 0.5:
                cl.box2("brass", (x + 0.004, lo(shelf) + h * 0.7, 0.21), (x + wdt - 0.008, lo(shelf) + h * 0.72, 0.215), bevel=0)
            x += wdt
    return a


def weapon_rack():
    a = Asset("weapon_rack", "Furniture")
    w, fe = a.p("Wood"), a.p("Iron")
    for k in range(5):                                    # backboard (wall piece, +Y out)
        z0 = lo(-6) + k * (hi(6) - lo(-6)) / 5
        w.box2(K.WOODS["x"][k % 3], (lo(-10), lo(0), z0 + 0.003), (hi(10), hi(0), z0 + (hi(6) - lo(-6)) / 5 - 0.003), bevel=0.006)
    for x in (-9, 9):
        w.box2(W("dark", "z"), (lo(x), hi(0), lo(-6)), (hi(x), hi(0) + 0.02, hi(6)), bevel=0.004)
    for z in (4, 0, -4):                                  # J hooks
        for x in (-7, 7):
            fe.tube("chrome", [(c(x), hi(0), c(z)), (c(x), c(2.5), c(z)), (c(x), c(3), c(z) + 0.04), (c(x), c(2.6), c(z) + 0.09)], 0.01, 6)
            fe.cyl("steel_dark", 0.02, 0.01, (c(x), hi(0) + 0.004, c(z)), "y", 8, 0)
    return a


def mirror():
    a = Asset("mirror", "Furniture")
    w, g = a.p("Wood"), a.p("Glass")
    w.box2(W("dark", "z"), (lo(-5), lo(0), lo(-8)), (hi(5), hi(0), hi(8)), bevel=0.01)
    for (x0, x1, z0, z1, ax) in ((lo(-5), hi(5), lo(-8), lo(-7), "x"), (lo(-5), hi(5), hi(7), hi(8), "x"), (lo(-5), lo(-4), lo(-7), hi(7), "z"), (hi(4), hi(5), lo(-7), hi(7), "z")):
        w.box2("brass", (x0, hi(0), z0), (x1, hi(1), z1), bevel=0.014)
    g.box2("chrome", (lo(-4), hi(0), lo(-7)), (hi(4), hi(0) + 0.02, hi(7)), bevel=0.002)
    for k in range(3):                                    # streaks
        g.box2("glass_clear", (lo(-3) + k * 0.2, hi(0) + 0.02, lo(-5) + k * 0.15), (lo(-3) + k * 0.2 + 0.03, hi(0) + 0.022, lo(-5) + k * 0.15 + 0.5), bevel=0, r=(0, 30, 0))
    return a


def bathtub():
    a = Asset("bathtub", "Furniture")
    s, fe, g = a.p("Scrap"), a.p("Iron"), a.p("Glass")
    # roll-top tub: outer shell superloft, inner well, rolled rim
    s.superloft("enamel", [(lo(-10) + 0.03, 0, c(5.5), 0.3, 0.16), (lo(-10) + 0.12, 0, c(5.3), 0.4, 0.26), (hi(10) - 0.12, 0, c(5.3), 0.4, 0.26),
                           (hi(10) - 0.03, 0, c(5.5), 0.3, 0.16)], 24, 3.0, axis="x")
    s.box2("enamel", (lo(-10) + 0.02, c(5), lo(-5) + 0.02), (hi(10) - 0.02, hi(8), hi(5) - 0.02), bevel=0.05, segs=3)
    s.box2("white", (lo(-9), lo(4), lo(-4)), (hi(9), hi(8) + 0.002, hi(4)), bevel=0.04, segs=2)
    g.box2("water", (lo(-9) + 0.02, hi(5) - 0.01, lo(-4) + 0.02), (hi(9) - 0.02, hi(5) + 0.0, hi(4) - 0.02), bevel=0)
    for x in (-9, 9):                                     # claw feet
        for z in (-4, 4):
            fe.lathe("brass", [(0.0, lo(0)), (0.045, lo(0)), (0.03, lo(0) + 0.05), (0.04, hi(1) + 0.04), (0.0, hi(1) + 0.04)], (c(x), 0, c(z)), "y", 10)
    fe.tube("chrome", [(lo(-10) + 0.02, c(9), 0), (lo(-10) + 0.02, c(11), 0), (c(-8), c(11), 0), (c(-8), c(10.4), 0)], 0.013, 8)
    for z in (-0.08, 0.08):
        fe.cyl("chrome", 0.012, 0.05, (lo(-10) + 0.03, c(9.5), z), "x", 8, 0.003)
    return a


def kitchen_counter():
    a = Asset("kitchen_counter", "Furniture")
    w, s, st, fe, g = a.p("Wood"), a.p("Scrap"), a.p("Stone"), a.p("Iron"), a.p("Glass")
    w.box2(W("dark", "y"), (lo(-12), lo(1), lo(-4)), (hi(12), hi(10), hi(4)), bevel=0.01)
    s.box2("black", (lo(-12) + 0.02, lo(0), lo(-4)), (hi(12) - 0.02, hi(0) + 0.02, hi(3)), bevel=0.004)
    for x0 in (-11, -5, 1, 7):                            # cupboard doors with knobs
        w.box2(W("", "y"), (lo(x0), lo(2), hi(4) - 0.005), (hi(x0 + 4), hi(9), hi(5) - 0.01), bevel=0.01)
        w.box2(W("light", "y"), (lo(x0) + 0.05, lo(2) + 0.06, hi(5) - 0.012), (hi(x0 + 4) - 0.05, hi(9) - 0.06, hi(5) - 0.004), bevel=0.008)
        knob(fe, "chrome", (c(x0 + 2), c(8), hi(5)), 0.014, "z", 0.02)
    # stone top with a sunk chrome basin
    holes = [(lo(3), hi(9), lo(-3), hi(2))]
    for (x0, x1, z0, z1) in K.rect_cells(lo(-13), hi(13), lo(-5), hi(5), holes):
        st.box2("conc_light", (x0, lo(11), z0), (x1, hi(11), z1), bevel=0.006)
    s.box2("chrome", (lo(3), lo(10) - 0.06, lo(-3)), (hi(9), lo(11), hi(2)), bevel=0.01)
    g.box2("water", (lo(3) + 0.02, lo(10) - 0.02, lo(-3) + 0.02), (hi(9) - 0.02, lo(10) - 0.015, hi(2) - 0.02), bevel=0)
    fe.cyl("chrome", 0.016, hi(14) - hi(11), (c(6), (hi(11) + hi(14)) / 2, c(-4)), "y", 10, 0.003)
    fe.tube("chrome", [(c(6), hi(14) - 0.02, c(-4)), (c(6), hi(14) + 0.01, c(-3)), (c(6), c(13.5), c(-2))], 0.012, 8)
    s.cyl("enamel", 0.08, 0.16, (c(-8), hi(11) + 0.08, c(-1)), "y", 14, 0.006)              # a pot and a board
    w.box2(W("light", "x"), (c(-4), hi(11), c(-2)), (c(0), hi(11) + 0.02, c(1)), bevel=0.004)
    return a


def latrine():
    a = Asset("latrine", "Utility")
    w, s, fe = a.p("Wood"), a.p("Scrap"), a.p("Iron")
    hole = [(lo(-4), hi(4), lo(1), hi(22))]
    for side in range(4):                                 # vertical board walls
        if side == 0:      # front (+z) with the door opening
            K.boards_xy(w, K.WOODS["y"], lo(-7), hi(7), lo(0), hi(26), hi(7) - 0.06, hi(7), 0.24, hole, seed=1181)
        elif side == 1:
            K.boards_xy(w, K.WOODS["y"], lo(-7), hi(7), lo(0), hi(26), lo(-7), lo(-7) + 0.06, 0.24, (), seed=1182)
        else:
            sx = -1 if side == 2 else 1
            for k in range(7):
                z0 = lo(-7) + 0.06 + k * (hi(7) - lo(-7) - 0.12) / 7
                x0 = lo(-7) if sx < 0 else hi(7) - 0.06
                w.box2(K.WOODS["y"][k % 3], (x0, lo(0), z0 + 0.003), (x0 + 0.06, hi(26), z0 + (hi(7) - lo(-7) - 0.12) / 7 - 0.003), bevel=0.005)
    for x in (-7, 7):
        for z in (-7, 7):
            w.box2(W("dark", "y"), (c(x) - 0.04, lo(0), c(z) - 0.04), (c(x) + 0.04, hi(26), c(z) + 0.04), bevel=0.008)
    # door with the crescent moon cut-out
    d = a.p("Door", "Wood")
    K.boards_xy(d, K.WOODS["y"], lo(-4), hi(4), lo(1), hi(22), hi(7) - 0.04, hi(7) + 0.0, 0.18, [(c(-1.6), c(1.6), c(17.4), c(21))], seed=1184)
    for y in (4, 19):
        d.box2(W("dark", "x"), (lo(-4), c(y) - 0.04, hi(7)), (hi(4), c(y) + 0.04, hi(7) + 0.012), bevel=0.004)
    d.add(K.kit.bm_torus(0.12, 0.022, 20, 6), "black", Matrix.Translation((c(0), c(19), hi(7) - 0.01)) @ K._axis_mtx("z") @ Matrix.Diagonal((1, 1, 0.3, 1)))
    fe.sphere("chrome", 0.014, (c(3), c(12), hi(7) + 0.02), (1, 1, 1), 8, 6)
    # lean-to tin roof falling to the back, vent pipe
    s.panel("tin_rust", (lo(-8), hi(27) + 0.27, hi(8)), (lo(-8), hi(27) - 0.02, lo(-8)), (hi(8), hi(27) - 0.02, lo(-8)), 0.02)
    s.cyl("steel_dark", 0.04, hi(33) - lo(28), (c(5), (lo(28) + hi(33)) / 2, c(-5)), "y", 10, 0.003)
    s.lathe("steel_dark", [(0.0, 0.0), (0.07, 0.0), (0.0, 0.04)], (c(5), hi(33), c(-5)), "y", 10)
    return a


def sewing_table():
    a = Asset("sewing_table", "Furniture")
    w, fe, cl = a.p("Wood"), a.p("Iron"), a.p("Cloth")
    for x in (-8, 8):                                     # cast-iron treadle frame ends
        fe.box2("black", (lo(x) + 0.01, lo(0), lo(-4)), (hi(x) - 0.01, hi(8), lo(-3)), bevel=0.006)
        fe.box2("black", (lo(x) + 0.01, lo(0), hi(3)), (hi(x) - 0.01, hi(8), hi(4)), bevel=0.006)
        fe.beam("black", (c(x), c(1), c(-3.5)), (c(x), c(7), c(3.5)), 0.03, 0.025)
        fe.beam("black", (c(x), c(1), c(3.5)), (c(x), c(7), c(-3.5)), 0.03, 0.025)
    for k in range(6):                                    # treadle grille
        fe.box2("steel_dark" if k % 2 else "black", (lo(-6) + k * 0.17, lo(1), lo(-3)), (lo(-6) + k * 0.17 + 0.12, hi(1), hi(3)), bevel=0.004)
    fe.cyl("black", 0.2, 0.03, (c(7), c(4), 0), "x", 24, 0.003)                               # flywheel
    for k in range(4):
        an = k * math.pi / 4
        fe.box("black", (0.016, 0.38, 0.02), (c(7), c(4), 0), r=(math.degrees(an), 0, 0), bevel=0)
    for k in range(5):
        x0 = lo(-9) + k * (hi(9) - lo(-9)) / 5
        w.box2(K.WOODS["z"][k % 3], (x0 + 0.003, lo(9), lo(-5)), (x0 + (hi(9) - lo(-9)) / 5 - 0.003, hi(9), hi(5)), bevel=0.006)
    # the machine: bed, pillar, arm, head, needle, handwheel, gold decal
    fe.box2("black", (lo(-5), lo(10), lo(-2)), (hi(3), hi(11) - 0.02, hi(2)), bevel=0.02)
    fe.box2("black", (lo(2), lo(12) - 0.02, lo(-1)), (hi(3), hi(15), hi(1)), bevel=0.025)
    fe.superloft("black", [(lo(-5), 0, c(15.5), 0.08, 0.08), (hi(3), 0, c(15.5), 0.08, 0.08)], 16, 2.6, axis="x")
    fe.box2("ochre", (c(-1.5), hi(16) - 0.004, c(0.5)), (c(0.5), hi(16) + 0.002, c(1.2)), bevel=0)
    fe.cyl("chrome", 0.008, 0.2, (c(-4), c(13), 0), "y", 6, 0)
    fe.cyl("chrome", 1.6 * VS, 0.04, (c(4) - 0.02, c(14), 0), "x", 20, 0.006)
    cl.box2("cloth_red", (lo(-8), hi(9), lo(3)), (hi(-6), hi(11), hi(4)), bevel=0.02)
    cl.cyl("cloth_red", 0.06, 0.24, (c(-7), hi(9) + 0.07, c(3.5) - 0.0), "x", 14, 0.006)
    return a


def wall_clock():
    a = Asset("wall_clock", "Decor")
    s = a.p("Scrap")
    s.lathe("brass", [(0.0, lo(0)), (0.5, lo(0)), (0.52, hi(0)), (0.5, hi(1)), (0.44, hi(1)), (0.43, hi(0)), (0.0, hi(0))], (0, 0, 0), "y", 40)
    s.cyl("enamel", 0.43, 0.01, (0, hi(0) + 0.005, 0), "y", 40, 0)
    for i in range(12):
        an = math.radians(i * 30)
        L = 0.06 if i % 3 == 0 else 0.03
        s.box("black", (0.012 if i % 3 else 0.02, 0.006, L), (math.sin(an) * 0.35, hi(0) + 0.012, math.cos(an) * 0.35), r=(0, math.degrees(an), 0), bevel=0)
    s.box("black", (0.016, 0.006, 0.2), (0.05, hi(0) + 0.018, 0.08), r=(0, 30, 0), bevel=0.002)
    s.box("black", (0.01, 0.006, 0.3), (-0.12, hi(0) + 0.024, 0.07), r=(0, -60, 0), bevel=0.002)
    s.cyl("brass", 0.02, 0.02, (0, hi(0) + 0.02, 0), "y", 10, 0.003)
    s.add(K.kit.bm_box(0.08, 0.01, 0.006), "pump_red", Matrix.Translation((0.0, hi(0) + 0.03, 0.0)) @ K.rot(0, 200, 0))
    a.parts["Scrap"].byte = "Scrap"
    return a


def trophy_mount():
    a = Asset("trophy_mount", "Decor")
    w = a.p("Wood")
    outline = [(hi(5), hi(6))]                            # shield: straight top, rounded bottom
    for k in range(17):
        an = k / 16 * math.pi
        outline.append((math.cos(an) * hi(5), -math.sin(an) * hi(7) * 0.95 + 0.0))
    outline.append((lo(-5), hi(6)))
    w.prism(W("dark", "x"), outline, lo(0), hi(0), plane="xz")
    inner = [(x * 0.8, z * 0.8 + 0.02) for x, z in outline]
    w.prism(W("light", "x"), inner, hi(0), hi(1), plane="xz")
    w.cyl("brass", 0.025, 0.012, (0, hi(1) + 0.004, c(3)), "y", 10, 0.002)
    for x in (-3, 3):
        w.cyl("brass", 0.012, 0.01, (c(x), hi(1), c(-4)), "y", 8, 0.0)
    return a


TILES = [("furn_home2_seating", "Armchair, bench, dining table, sewing table", [lambda: [armchair(), bench(), dining_table(), sewing_table()]]),
         ("furn_home2_storage", "Wardrobe, bookshelf, weapon rack, mirror, wall clock, trophy mount", [lambda: [wardrobe(), bookshelf(), weapon_rack(), mirror(), wall_clock(), trophy_mount()]]),
         ("furn_home2_bath", "Bathtub, kitchen counter, latrine", [lambda: [bathtub(), kitchen_counter(), latrine()]])]

if __name__ == "__main__":
    run(TILES, "home2_")
