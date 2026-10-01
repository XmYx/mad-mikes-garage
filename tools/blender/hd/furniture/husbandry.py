"""HD pieces of FurnitureLibrary.Husbandry.cs: feed mill, spinning wheel, leather bench, butchering table, beehive,
stable, snare, cage trap, beeswax candles.

blender -b -P husbandry.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fcommon import K, Asset, Vector, Matrix, lo, hi, c, W, knob, screws, leg_square, drawer_front, bulb, run, VS  # noqa: E402


def feed_mill():
    a = Asset("feed_mill", "Industry")
    w, hay, fe, cl = a.p("Wood"), a.p("Hay"), a.p("Iron"), a.p("Cloth")
    for x in (-5, 5):
        for z in (-4, 4):
            w.box2(W("dark", "y"), (lo(x), lo(0), lo(z)), (hi(x), hi(12), hi(z)), bevel=0.01)
    for (p0, p1) in (((-5, -4), (5, -4)), ((-5, 4), (5, 4)), ((-5, -4), (-5, 4)), ((5, -4), (5, 4))):
        w.box2(W("", "x" if p0[1] == p1[1] else "z"), (lo(min(p0[0], p1[0])), lo(4), lo(min(p0[1], p1[1]))), (hi(max(p0[0], p1[0])), hi(4), hi(max(p0[1], p1[1]))), bevel=0.006)
    secs = []                                              # plank hopper, wider at the top
    for y, wv in ((lo(13), 2), (hi(20), 5.5)):
        r_ = (wv + 0.5) * VS
        secs.append([(-r_, y, -r_), (r_, y, -r_), (r_, y, r_), (-r_, y, r_)])
    for side in range(4):
        w.quad(K.WOODS["y"][side % 3], secs[0][side], secs[0][(side + 1) % 4], secs[1][(side + 1) % 4], secs[1][side], 0.04)
    w.box2(W("dark", "x"), (lo(-2), lo(13), lo(-2)), (hi(2), hi(13), hi(2)), bevel=0.006)
    hay.box2("hay", (lo(-4) + 0.02, hi(19) - 0.06, lo(-4) + 0.02), (hi(4) - 0.02, hi(20), hi(4) - 0.02), bevel=0.03)
    r = K.rng(3805)
    for k in range(30):
        x, z = r.uniform(-0.3, 0.3), r.uniform(-0.3, 0.3)
        hay.rod("hay", (x, hi(20) - 0.01, z), (x + r.uniform(-0.1, 0.1), hi(20) + r.uniform(0.02, 0.08), z + r.uniform(-0.1, 0.1)), 0.005, 4)
    fe.cyl("steel_dark", 3.6 * VS, hi(3) - lo(-3), (0, c(9), 0), "z", 20, 0.012)                       # grinder drum
    fe.torus("steel", 5.1 * VS, 0.035, (c(7), c(9), 0), "x", 28, 6)                                   # flywheel
    for i in range(6):
        an = i / 6 * math.tau
        fe.rod("steel", (c(7), c(9), 0), (c(7), c(9) + math.sin(an) * 0.4, math.cos(an) * 0.4), 0.012, 5)
    fe.rod("chrome", (c(5.5), c(9), 0), (c(8), c(9), 0), 0.02, 8)
    fe.rod("rust", (c(8), c(9), 0), (c(8), c(13), 0), 0.014, 6)
    w.rod(W("light", "x"), (c(8), c(13), 0), (hi(10), c(13), 0), 0.016, 8)
    fe.quad("rust", (lo(-1), lo(6), hi(3)), (hi(1), lo(6), hi(3)), (hi(1), lo(3), hi(8)), (lo(-1), lo(3), hi(8)), 0.02)
    for x in (-9, -6):                                     # tied feed sacks
        cl.superloft("burlap", [(lo(0), c(x + 1), c(6.5), 0.08, 0.1), (c(1), c(x + 1), c(6.5), 0.12, 0.15), (c(4), c(x + 1), c(6.5), 0.11, 0.14), (hi(5), c(x + 1), c(6.5), 0.04, 0.05)], 14, 2.2, axis="y")
        cl.torus("rope", 0.035, 0.008, (c(x + 1), hi(5) - 0.02, c(6.5)), "y", 10, 4)
    return a


def spinning_wheel():
    a = Asset("spinning_wheel", "Industry")
    w, wo, fe = a.p("Wood"), a.p("Wool"), a.p("Iron")
    w.box(W("light", "z"), (0.34, 0.06, hi(6) - lo(-7)), (0, c(5), c(-0.5)), r=(4, 0, 0), bevel=0.012)   # slanted bench
    for (p0, p1) in (((-2, 0, -7), (-1, 5, -6)), ((2, 0, -7), (1, 5, -6)), ((0, 0, 7), (0, 5, 5))):
        w.rod(W("dark", "y"), tuple(c(v) for v in p0), tuple(c(v) for v in p1), 0.022, 8)
    for x in (-2, 2):
        w.lathe(W("", "y"), [(0.0, lo(6)), (0.025, lo(6)), (0.03, c(9)), (0.02, c(11)), (0.028, c(13)), (0.022, hi(15)), (0.0, hi(15))], (c(x), 0, c(2)), "y", 10)
    w.torus(W("light", "x"), 5.8 * VS, 0.022, (0, c(14), c(2)), "x", 40, 6)                        # drive wheel rim
    w.torus(W("", "x"), 5.8 * VS - 0.03, 0.01, (0, c(14), c(2)), "x", 40, 4)
    for i in range(8):
        an = i / 8 * math.tau
        w.rod(W("dark", "x"), (0, c(14), c(2)), (0, c(14) + math.sin(an) * 0.42, c(2) + math.cos(an) * 0.42), 0.008, 5)
    fe.rod("steel", (c(-2), c(14), c(2)), (c(2), c(14), c(2)), 0.01, 6)
    w.cyl(W("dark", "x"), 0.045, 0.06, (0, c(14), c(2)), "x", 12, 0.006)
    w.box2(W("", "x"), (lo(-2), lo(1), lo(-1)), (hi(2), hi(1) - 0.04, hi(3)), bevel=0.008)              # treadle
    w.rod(W("dark", "y"), (c(1), c(1), c(1)), (c(1), c(11), c(2)), 0.008, 5)
    w.box2(W("dark", "x"), (lo(-1), lo(7), lo(-7)), (hi(1), hi(9), hi(-4)), bevel=0.012)                 # mother-of-all
    wo.cyl("cloth_cream", 1.3 * VS, hi(-5) - lo(-7), (0, c(10), c(-6)), "z", 14, 0.01)                   # bobbin of yarn
    for k in range(6):
        wo.torus("cloth_cream", 1.3 * VS + 0.004, 0.006, (0, c(10), c(-6.8) + k * 0.03), "z", 14, 4)
    w.rod(W("", "y"), (c(2), lo(6), c(-2)), (c(2), hi(18), c(-2)), 0.012, 6)                               # distaff
    wo.rock("cloth_cream", 0.13, (c(2), c(18), c(-2)), seed=7, squash=1.6, detail=2, scale=(1, 1, 1))
    return a


def leather_bench():
    a = Asset("leather_bench", "Industry")
    w, le, fe, cl = a.p("Wood"), a.p("Leather"), a.p("Iron"), a.p("Cloth")
    for x in (-9, 9):
        for z in (-4, 4):
            w.box2(W("dark", "y"), (lo(x), lo(0), lo(z)), (hi(x), hi(8), hi(z)), bevel=0.01)
    w.box2(W("dark", "x"), (lo(-9), lo(3), lo(-4)), (hi(9), hi(3), hi(4)), bevel=0.006)
    for k in range(4):
        z0 = lo(-4) + k * (hi(4) - lo(-4)) / 4
        w.box2(K.WOODS["x"][k % 3], (lo(-9), lo(9), z0 + 0.003), (hi(9), hi(10), z0 + (hi(4) - lo(-4)) / 4 - 0.003), bevel=0.01)
    w.box2(W("", "x"), (lo(-9), lo(11), lo(-5)), (hi(9), hi(21), hi(-5)), bevel=0.008)                    # backboard
    outline = []
    for k in range(20):                                    # hide cut to a pattern
        an = k / 20 * math.tau
        wob = 1 + 0.1 * math.sin(an * 4) + 0.06 * math.sin(an * 7)
        outline.append((c(-3) + math.cos(an) * 0.44 * wob, math.sin(an) * 0.26 * wob))
    le.prism("leather", outline, hi(10), hi(10) + 0.012)
    for i in range(4):                                     # strips on the rack
        le.box2("leather" if i % 2 == 0 else "black", (c(-7 + i * 3) - 0.025, lo(12), lo(-4) - 0.01), (c(-7 + i * 3) + 0.025, hi(20), lo(-4) + 0.004), bevel=0.003)
    fe.rod("steel_dark", (lo(-8), hi(20) - 0.02, lo(-4)), (hi(2), hi(20) - 0.02, lo(-4)), 0.008, 5)
    for x in (6, 8):                                       # stitching clamp
        w.box2(W("light", "y"), (lo(x) + 0.01, lo(11), lo(-1)), (hi(x) - 0.01, hi(16), hi(0)), bevel=0.01)
    w.cyl(W("light", "x"), 0.04, 0.12, (c(4.5), c(12.2), c(2)), "x", 12, 0.006)                         # mallet
    w.rod(W("", "z"), (c(4.5), c(12), c(1.5)), (c(4.5), c(11.4), c(3.5)), 0.012, 6)
    fe.lathe("chrome", [(0.0, 0.0), (0.06, 0.0), (0.0, 0.004)], (c(0), hi(10) + 0.004, c(2)), "y", 14)    # round knife
    w.rod(W("dark", "x"), (c(-3), hi(10) + 0.012, c(2)), (c(-0.8), hi(10) + 0.012, c(2)), 0.012, 6)
    fe.rod("chrome", (c(2), hi(10), c(-2)), (c(2), hi(10) + 0.05, c(-2)), 0.004, 4)
    w.sphere(W("", "y"), 0.02, (c(2), hi(10) + 0.065, c(-2)), (1, 1.4, 1), 8, 5)
    cl.cyl("cloth_cream", 0.045, 0.1, (c(-6), hi(10) + 0.05, c(3)), "y", 12, 0.006)
    return a


def butcher_table():
    a = Asset("butcher_table", "Industry")
    w, fe, hide = a.p("Wood"), a.p("Iron"), a.p("Hide")
    for x in (-8, 7):
        for z in (-4, 3):
            w.box2(W("dark", "y"), (lo(x), lo(0), lo(z)), (hi(x + 1), hi(7), hi(z + 1)), bevel=0.014)
    for k in range(6):                                     # end-grain block (blocks of 3 voxels)
        x0 = lo(-9) + k * (hi(9) - lo(-9)) / 6
        w.box2(K.WOODS["y"][k % 3], (x0 + 0.003, lo(8), lo(-5)), (x0 + (hi(9) - lo(-9)) / 6 - 0.003, hi(10), hi(5)), bevel=0.012)
    r = K.rng(3864)
    for k in range(8):
        hide.cyl("crimson", r.uniform(0.02, 0.05), 0.002, (r.uniform(-0.6, 0.6), hi(10) + 0.001, r.uniform(-0.3, 0.3)), "y", 10, 0)
    for x in (-9, 9):
        fe.box2("steel_dark", (lo(x), lo(11), lo(-6)), (hi(x), hi(26), hi(-6)), bevel=0.008)
    fe.rod("steel", (lo(-9), c(26), c(-6)), (hi(9), c(26), c(-6)), 0.022, 8)
    for x in (-5, 0, 5):
        fe.tube("chrome", [(c(x), c(26), c(-6)), (c(x), c(24), c(-6)), (c(x) + 0.03, c(23.2), c(-6)), (c(x) + 0.05, c(23.6), c(-6))], 0.006, 4)
    hide.superloft("cloth_red", [(c(16), 0, c(-6), 0.07, 0.06), (c(18), 0, c(-6), 0.12, 0.08), (c(21), 0, c(-6), 0.1, 0.07), (c(22.6), 0, c(-6), 0.05, 0.04)], 14, 2.2, axis="y")
    hide.superloft("bone", [(c(21.5), 0, c(-6), 0.06, 0.05), (c(22.8), 0, c(-6), 0.03, 0.03)], 10, 2.0, axis="y")
    fe.box2("chrome", (lo(1), hi(10), lo(1)), (hi(5), hi(10) + 0.008, hi(2)), bevel=0.003)               # cleaver
    w.rod(W("dark", "x"), (lo(-2), hi(10) + 0.012, c(1.2)), (lo(1), hi(10) + 0.012, c(1.2)), 0.014, 6)
    fe.lathe("steel", [(0.0, lo(0)), (0.13, lo(0)), (0.16, hi(4)), (0.15, hi(4)), (0.12, lo(0) + 0.02), (0.0, lo(0) + 0.02)], (c(-6), 0, c(8)), "y", 16, close=False)
    fe.tube("steel_dark", [(c(-6) - 0.15, hi(4), c(8)), (c(-6), hi(4) + 0.12, c(8)), (c(-6) + 0.15, hi(4), c(8))], 0.005, 4, caps=False)
    return a


def beehive():
    a = Asset("beehive", "Garden")
    w, s, st, wax = a.p("Wood"), a.p("Scrap"), a.p("Stone"), a.p("Beeswax")
    for x in (-5, 5):
        for z in (-4, 4):
            w.box2(W("dark", "y"), (lo(x), lo(0), lo(z)), (hi(x), hi(4), hi(z)), bevel=0.01)
    w.box2(W("", "z"), (lo(-6), lo(5), lo(-5)), (hi(6), hi(5), hi(7)), bevel=0.01)
    for b in range(3):                                     # supers
        y0 = 6 + b * 5
        m = "white" if b % 2 == 0 else "ochre"
        w.box2(m, (lo(-5), lo(y0), lo(-4)), (hi(5), hi(y0 + 4) - 0.012, hi(4)), bevel=0.012)
        w.box2(W("dark", "x"), (lo(-5) + 0.004, hi(y0 + 4) - 0.014, lo(-4) + 0.004), (hi(5) - 0.004, hi(y0 + 4), hi(4) - 0.004), bevel=0.003)
        for x in (-6, 6):
            w.box2(W("", "z"), (lo(x), lo(y0 + 2), lo(-1)), (hi(x), hi(y0 + 2), hi(1)), bevel=0.008)
    w.box2("black", (lo(-3), lo(6), hi(4) - 0.01), (hi(3), hi(6) - 0.03, hi(4) + 0.002), bevel=0)
    s.box2("tin", (lo(-6), lo(21), lo(-5)), (hi(6), hi(22), hi(5)), bevel=0.01)
    st.rock("rock_grey", 0.13, (c(-0.5), hi(22) + 0.06, 0), seed=3888, squash=0.6, detail=1)
    wax.sphere("lamp_amber", 0.018, (c(4), c(6.5), hi(5) - 0.01), (1, 1.6, 1), 8, 5)
    for (x, z) in ((-2, 5), (0, 6), (2, 5), (-1, 7)):     # bees
        wax.sphere("ochre", 0.012, (c(x), hi(5) + 0.03, c(z)), (1, 0.8, 1.5), 6, 4)
        wax.sphere("black", 0.008, (c(x), hi(5) + 0.031, c(z) + 0.01), (1, 0.8, 1), 6, 4)
    return a


def stable():
    a = Asset("stable", "Garden", kind="building")
    w, s, hay, le = a.p("Wood"), a.p("Scrap"), a.p("Hay"), a.p("Leather")
    roof = lambda z: 31 + (z + 20) * 4 // 41
    for x in (-26, 26):
        for z in (-19, 19):
            w.box2(W("dark", "y"), (lo(x), lo(0), lo(z)), (hi(x), hi(roof(z) - 1), hi(z)), bevel=0.012)
    K.boards_xy(w, K.WOODS["y"], lo(-26) + 0.08, hi(26) - 0.08, lo(0), hi(30), lo(-19), hi(-19), 0.32, (), seed=3902)   # back wall
    for x in (-26, 26):                                    # side walls (boards along z, up to 20)
        for k in range(8):
            y0 = lo(0) + k * (hi(20) - lo(0)) / 8
            w.box2(K.WOODS["z"][k % 3], (lo(x), y0 + 0.004, lo(-18)), (hi(x), y0 + (hi(20) - lo(0)) / 8, hi(18)), bevel=0.008)
    w.box2(W("dark", "x"), (lo(-26), lo(roof(19) - 2), lo(19)), (hi(26), hi(roof(19) - 1), hi(19)), bevel=0.012)   # header
    for k in range(5):                                     # stall partition
        y0 = lo(0) + k * (hi(14) - lo(0)) / 5
        w.box2(K.WOODS["z"][k % 3], (lo(0), y0 + 0.004, lo(-18)), (hi(0), y0 + (hi(14) - lo(0)) / 5, hi(2)), bevel=0.008)
    w.box2(W("dark", "y"), (lo(0) - 0.01, lo(0), lo(2)), (hi(0) + 0.01, hi(16), hi(2)), bevel=0.01)
    w.box2(W("", "x"), (lo(-25), lo(7), lo(-18)), (hi(25), hi(8), hi(-15)), bevel=0.01)              # manger
    w.box2(W("light", "x"), (lo(-25), lo(9), lo(-15)), (hi(25), hi(11), hi(-15)), bevel=0.008)
    hay.box2("hay", (lo(-24), lo(9), lo(-18)), (hi(24), hi(9) + 0.06, hi(-16)), bevel=0.03)
    r = K.rng(3906)
    for k in range(60):
        x = r.uniform(-1.9, 1.9)
        hay.rod("hay", (x, hi(9) + 0.03, r.uniform(-1.48, -1.32)), (x + r.uniform(-0.1, 0.1), hi(9) + r.uniform(0.06, 0.14), r.uniform(-1.5, -1.25)), 0.005, 4)
    hay.rock("hay", 0.3, (c(21), lo(0) + 0.08, c(13)), seed=3908, squash=0.55, detail=2, scale=(1, 1, 1))
    for i in range(14):                                    # corrugated tin roof sloping up to the front
        x0 = lo(-28) + i * (hi(28) - lo(-28)) / 14
        x1 = x0 + (hi(28) - lo(-28)) / 14 + 0.02
        za, zb = lo(-21), hi(22)
        ya, yb = c(roof(-21)), c(roof(22))
        ang = math.atan2(yb - ya, zb - za)
        mt = Matrix.Translation((0, (ya + yb) / 2, (za + zb) / 2)) @ Matrix.Rotation(-ang, 4, "X")
        Lr = math.hypot(zb - za, yb - ya)
        K.corrugated(s, "tin_rust" if i % 3 == 0 else "tin", x0, x1, -Lr / 2, Lr / 2, 0.02, 0.1, 0.004, mt, samples_per_period=4)
    s.box2("chrome", (lo(-25), lo(22), lo(-18) + 0.06), (hi(-23), hi(22), hi(-18) + 0.02), bevel=0.004)
    le.box2("leather", (c(-24) - 0.02, lo(17), hi(-18)), (c(-24) + 0.02, hi(21), hi(-18) + 0.02), bevel=0.004)
    le.torus("leather", 0.06, 0.01, (c(-24), lo(17), hi(-18) + 0.02), "z", 12, 4)
    return a


def snare():
    a = Asset("snare", "Garden")
    w, s = a.p("Wood"), a.p("Scrap")
    w.add(K.kit.bm_cyl(0.035, hi(4) - lo(0), 6, 0.0, 0.006), W("dark", "y"), Matrix.Translation((0, (hi(4) + lo(0)) / 2, c(-3))) @ K._axis_mtx("y"))
    w.tube("green", [(c(-2), lo(0), c(-5)), (c(-1.2), c(4), c(-4)), (c(-0.2), c(7.5), c(-3.2)), (0, c(8), c(-3))], [0.028, 0.022, 0.014, 0.01], 6)
    for k in range(4):
        w.add(K.kit.bm_sphere(0.03, 6, 4), "leaf", Matrix.Translation((c(-1) + k * 0.03, c(5) + k * 0.06, c(-4))) @ Matrix.Diagonal((1.4, 0.4, 0.7, 1)))
    s.torus("chrome", 2 * VS, 0.004, (0, c(3), c(1)), "z", 18, 4)
    s.tube("chrome", [(0, c(5), c(1)), (0, c(5), c(-3)), (0, c(8), c(-3))], 0.003, 4, caps=False)
    return a


def cage_trap():
    a = Asset("cage_trap", "Garden")
    s = a.p("Scrap")
    X0, X1, Y0, Y1, Z0, Z1 = lo(-3), hi(3), lo(0), hi(4), lo(-5), hi(5)
    edges = [((X0, Y0, Z0), (X1, Y0, Z0)), ((X0, Y1, Z0), (X1, Y1, Z0)), ((X0, Y0, Z1), (X1, Y0, Z1)), ((X0, Y1, Z1), (X1, Y1, Z1)),
             ((X0, Y0, Z0), (X0, Y1, Z0)), ((X1, Y0, Z0), (X1, Y1, Z0)), ((X0, Y0, Z1), (X0, Y1, Z1)), ((X1, Y0, Z1), (X1, Y1, Z1)),
             ((X0, Y0, Z0), (X0, Y0, Z1)), ((X1, Y0, Z0), (X1, Y0, Z1)), ((X0, Y1, Z0), (X0, Y1, Z1)), ((X1, Y1, Z0), (X1, Y1, Z1))]
    for p0, p1 in edges:
        s.rod("steel_dark", p0, p1, 0.008, 5)
    s.box2("steel_dark", (X0, Y0, Z0), (X1, Y0 + 0.01, Z1), bevel=0.002)
    for k in range(1, 12):                                 # mesh: rods along x on the sides / top, along y on the ends
        z = Z0 + k * (Z1 - Z0) / 12
        s.rod("chrome", (X0, Y1, z), (X1, Y1, z), 0.003, 4)
        for x in (X0, X1):
            s.rod("chrome", (x, Y0, z), (x, Y1, z), 0.003, 4)
    for k in range(1, 6):
        y = Y0 + k * (Y1 - Y0) / 6
        for x in (X0, X1):
            s.rod("chrome", (x, y, Z0), (x, y, Z1), 0.003, 4)
        s.rod("chrome", (X0, y, Z0), (X1, y, Z0), 0.003, 4)
    s.tube("steel_dark", [(c(-1), Y1, 0), (c(-1), c(5), 0), (c(1), c(5), 0), (c(1), Y1, 0)], 0.007, 5)
    s.box2("rust", (lo(-2), lo(5) - 0.02, lo(4)), (hi(2), lo(5) + 0.01, hi(5)), bevel=0.003)
    return a


def candles():
    a = Asset("candles", "Decor")
    s, wax, g = a.p("Scrap"), a.p("Beeswax"), a.p("Glass")
    s.lathe("steel", [(0.0, lo(0)), (0.19, lo(0)), (0.21, hi(0)), (0.2, hi(0)), (0.17, lo(0) + 0.012), (0.0, lo(0) + 0.012)], (0, 0, 0), "y", 24)
    for (x, z, h) in ((-1, -1, 5), (1, 0, 3), (0, 1, 4)):
        top = hi(h)
        wax.lathe("cloth_cream", [(0.0, lo(1)), (0.035, lo(1)), (0.035, top - 0.01), (0.03, top), (0.0, top - 0.006)], (c(x), 0, c(z)), "y", 12)
        wax.sphere("cloth_cream", 0.012, (c(x) + 0.03, top - 0.05, c(z)), (1, 2.2, 1), 6, 4)
        s.rod("black", (c(x), top - 0.006, c(z)), (c(x), top + 0.03, c(z)), 0.003, 4)
        g.lathe("flame", [(0.0, 0.0), (0.014, 0.012), (0.0, 0.05)], (c(x), top + 0.02, c(z)), "y", 8)
    a.parts["Glass"].byte = "Beeswax"
    return a


TILES = [("furn_husbandry_crafts", "Feed mill, spinning wheel, leather bench, butchering table", [lambda: [feed_mill(), spinning_wheel(), leather_bench(), butcher_table()]]),
         ("furn_husbandry_small", "Beehive, snare, cage trap, beeswax candles", [lambda: [beehive(), snare(), cage_trap(), candles()]]),
         ("furn_stable", "Stable (open shed, stalls, manger, tin roof)", [lambda: [stable()]])]

if __name__ == "__main__":
    run(TILES, "husbandry_")
