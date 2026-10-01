"""HD pieces of FurnitureLibrary.MedMine.cs and .Seasons.cs: clinic bed, surgery table, medicine cabinet, sluice box,
stamp mill (+ Stamp0..2 movers at x -0.4 / 0 / 0.4, y 0.36), mine prop, tunnel lamp, mine rail, ore cart, drying rack,
pickling crock.

blender -b -P medmine.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fcommon import K, Asset, Vector, Matrix, lo, hi, c, W, knob, screws, leg_square, drawer_front, bulb, run, VS  # noqa: E402


def clinic_bed():
    a = Asset("clinic_bed", "Furniture")
    s, cl, g = a.p("Scrap"), a.p("Cloth"), a.p("Glass")
    for x in (-11, 11):
        for z in (-4, 4):
            s.cyl("chrome", 0.02, hi(5) - lo(1), (c(x), (lo(1) + hi(5)) / 2, c(z)), "y", 10, 0.003)
            s.cyl("black", 0.035, 0.03, (c(x), lo(0) + 0.035, c(z)), "x", 12, 0.004)
            s.box2("chrome", (c(x) - 0.012, lo(0) + 0.06, c(z) - 0.012), (c(x) + 0.012, lo(1), c(z) + 0.012), bevel=0.002)
    s.box2("chrome", (lo(-12), lo(5), lo(-5)), (hi(12), hi(5), hi(5)), bevel=0.01)
    for z in (-4, 4):
        s.rod("chrome", (c(-11), c(2), c(z)), (c(11), c(2), c(z)), 0.012, 6)
    # head board with red cross, foot rail with spindles
    s.box2("enamel", (lo(-12), lo(6), lo(-5)), (hi(-12), hi(13), hi(5)), bevel=0.012)
    s.box2("cloth_red", (lo(-12) - 0.004, c(8), -0.03), (lo(-12) + 0.006, c(12), 0.03), bevel=0)
    s.box2("cloth_red", (lo(-12) - 0.004, c(10) - 0.03, -0.16), (lo(-12) + 0.006, c(10) + 0.03, 0.16), bevel=0)
    s.tube("chrome", [(c(12), lo(6), lo(-5) + 0.01), (c(12), hi(10), lo(-5) + 0.01), (c(12), hi(10), hi(5) - 0.01), (c(12), lo(6), hi(5) - 0.01)], 0.014, 8)
    for z in range(-5, 6, 2):
        s.rod("chrome", (c(12), lo(6), c(z)), (c(12), hi(10), c(z)), 0.008, 6)
    # ticking mattress, blanket, sheet, pillow
    cl.box2("cloth_pale", (lo(-11), lo(6), lo(-4)), (hi(11), hi(6) + 0.02, hi(4)), bevel=0.04, segs=3)
    cl.box2("cloth_blue", (lo(3), hi(6), lo(-4) - 0.01), (hi(11), hi(7), hi(4) + 0.01), bevel=0.03, segs=2)
    cl.box2("cloth_cream", (lo(3) - 0.06, hi(6) + 0.01, lo(-4) - 0.012), (lo(3) + 0.04, hi(7) + 0.01, hi(4) + 0.012), bevel=0.02)
    cl.box2("cloth_cream", (lo(-11), hi(6), lo(-3)), (hi(-8), hi(8), hi(3)), bevel=0.05, segs=3)
    # drip stand with saline bag + line
    s.cyl("chrome", 0.012, hi(21) - lo(0), (c(-14), (lo(0) + hi(21)) / 2, c(-4)), "y", 8, 0.0)
    for k in range(4):
        an = k * math.pi / 2
        s.rod("black", (c(-14), lo(0) + 0.02, c(-4)), (c(-14) + math.cos(an) * 0.11, lo(0) + 0.02, c(-4) + math.sin(an) * 0.11), 0.008, 5)
    s.rod("chrome", (c(-15), hi(21), c(-4)), (c(-13), hi(21), c(-4)), 0.008, 5)
    g.superloft("glass_clear", [(c(17), c(-15), c(-4), 0.05, 0.015), (c(19.5), c(-15), c(-4), 0.06, 0.02), (c(20.5), c(-15), c(-4), 0.03, 0.01)], 10, 2.4, axis="y")
    cl.tube("cloth_cream", [(c(-15), c(17), c(-4)), (c(-15), c(12), c(-3.5)), (c(-14), c(8), c(-3))], 0.004, 4)
    return a


def surgery_table():
    a = Asset("surgery_table", "Furniture")
    s, cl, g = a.p("Scrap"), a.p("Cloth"), a.p("Glass")
    s.box2("steel_dark", (lo(-4), lo(0), lo(-3)), (hi(4), hi(0), hi(3)), bevel=0.012)
    s.cyl("chrome", 1.5 * VS, hi(6) - lo(1), (0, (lo(1) + hi(6)) / 2, 0), "y", 16, 0.006)
    s.box2("steel", (lo(-11), lo(7), lo(-4)), (hi(11), hi(7), hi(4)), bevel=0.01)
    s.box2("chrome", (lo(-11), lo(8), lo(-4)), (hi(11), hi(8), hi(4)), bevel=0.012)
    cl.box2("rig_green", (lo(-10), lo(9), lo(-3)), (hi(-6), hi(9) + 0.02, hi(3)), bevel=0.03, segs=2)
    cl.box2("rig_green", (lo(-5), lo(9), lo(-3) - 0.03), (hi(10), hi(9), hi(3) + 0.03), bevel=0.02)
    for k in range(4):
        cl.box2("blanket", (lo(-5) + k * 0.32, hi(9) - 0.004, lo(-3) - 0.03), (lo(-5) + k * 0.32 + 0.02, hi(9) + 0.002, hi(3) + 0.03), bevel=0)
    s.cyl("chrome", 0.016, hi(30) - lo(0), (c(-13), (lo(0) + hi(30)) / 2, c(-6)), "y", 8, 0.0)       # lamp post + arm + dish
    s.box2("black", (lo(-15), lo(0), lo(-8)), (hi(-11), hi(0), hi(-4)), bevel=0.01)
    s.tube("chrome", [(c(-13), c(30), c(-6)), (c(0), c(30), c(-6)), (c(0), c(28), c(-6)), (c(0), c(27), c(-3))], 0.014, 8)
    s.lathe("enamel", [(0.0, 0.0), (0.06, 0.0), (0.26, -0.1), (0.26, -0.12), (0.0, -0.08)], (0, hi(26) + 0.02, c(-3)), "y", 24)
    g.cyl("lamp", 0.18, 0.012, (0, lo(25) + 0.04, c(-3)), "y", 20, 0.002)
    for x in (9, 13):                                       # trolley
        for z in (-10, -7):
            s.cyl("chrome", 0.012, hi(9) - lo(0), (c(x), (lo(0) + hi(9)) / 2, c(z)), "y", 8, 0.0)
    s.box2("chrome", (lo(9), lo(10), lo(-10)), (hi(13), hi(10), hi(-7)), bevel=0.008)
    s.box2("steel", (lo(9), lo(5), lo(-10)), (hi(13), lo(5) + 0.02, hi(-7)), bevel=0.004)
    for k in range(4):
        s.box2("chrome", (c(10) + k * 0.06, hi(10), c(-9.4)), (c(10) + k * 0.06 + 0.012, hi(10) + 0.008, c(-7.8)), bevel=0.002)
    s.cyl("cloth_red", 0.03, 0.04, (c(12), hi(10) + 0.02, c(-9)), "y", 10, 0.004)
    return a


def medicine_cabinet():
    a = Asset("medicine_cabinet", "Furniture")
    s, g = a.p("Scrap"), a.p("Glass")
    s.box2("white", (lo(-5), lo(0), lo(0)), (hi(5), hi(3), hi(13)), bevel=0.012)
    s.box2("enamel", (lo(-4), hi(3), lo(1)), (hi(4), hi(4) - 0.03, hi(12)), bevel=0.01)
    g.box2("glass_clear", (lo(-3), hi(4) - 0.03, lo(2)), (hi(2), hi(4) - 0.02, hi(5)), bevel=0)
    for k, m in enumerate(("ochre", "cloth_blue", "crimson", "ochre")):
        x = c(-2.5) + k * 0.1
        s.lathe(m, [(0.0, 0.0), (0.03, 0.0), (0.03, 0.12), (0.012, 0.15), (0.0, 0.15)], (x, hi(3) - 0.06, c(2.6)), "z", 10)
    s.box2("cloth_red", (lo(0) + 0.01, hi(4) - 0.032, lo(8)), (hi(0) - 0.01, hi(4) - 0.026, hi(11)), bevel=0)
    s.box2("cloth_red", (lo(-1), hi(4) - 0.032, lo(9) + 0.03), (hi(1), hi(4) - 0.026, hi(10) - 0.03), bevel=0)
    s.box2("chrome", (lo(3) + 0.02, hi(4) - 0.03, lo(5)), (hi(3) - 0.02, hi(5), hi(6)), bevel=0.006)
    return a


def sluice_box():
    a = Asset("sluice_box", "Industry")
    w, cl, fe, au = a.p("Wood"), a.p("Cloth"), a.p("Iron"), a.p("Gold")
    f = lambda z: (z + 12) * 4 / 21                         # the floor falls towards -z
    zA, zB = lo(-12), hi(9)
    for x in (-4, 4):                                       # trough sides (sloped boards)
        w.quad(K.wood("", "z"), (c(x), lo(round(f(-12))), zA), (c(x), lo(round(f(9))), zB), (c(x), hi(round(f(9)) + 4), zB), (c(x), hi(round(f(-12)) + 4), zA), VS)
    w.quad(K.wood("dark", "z"), (lo(-3), lo(round(f(-12))), zA), (hi(3), lo(round(f(-12))), zA), (hi(3), lo(round(f(9))), zB), (lo(-3), lo(round(f(9))), zB), 0.06)
    cl.quad("green", (lo(-3), lo(round(f(-12))) + 0.07, zA + 0.08), (hi(3), lo(round(f(-12))) + 0.07, zA + 0.08), (hi(3), lo(round(f(9))) + 0.07, zB - 0.08), (lo(-3), lo(round(f(9))) + 0.07, zB - 0.08), 0.02)
    for z in range(-8, 9, 4):                              # iron riffles
        y = lo(f(z)) + 0.1
        fe.box2("steel", (lo(-3), y, c(z) - 0.012), (hi(3), y + 0.05, c(z) + 0.012), bevel=0.004, r=(-12, 0, 0))
    for (x, z) in ((-2, -8), (1, -4), (2, -8), (-1, 0)):
        au.sphere("brass", 0.012, (c(x), lo(f(z)) + 0.1, c(z) + 0.03), (1, 0.6, 1), 6, 4)
    w.box2(K.wood("", "x"), (lo(-5), lo(4), lo(9)), (hi(5), hi(4), hi(14)), bevel=0.008)                # hopper
    for x in (-5, 5):
        w.box2(K.wood("", "z"), (lo(x), lo(5), lo(9)), (hi(x), hi(9), hi(14)), bevel=0.008)
    w.box2(K.wood("", "x"), (lo(-5), lo(5), lo(14)), (hi(5), hi(9), hi(14)), bevel=0.008)
    for k in range(-4, 5, 2):
        fe.rod("steel_dark", (c(k), c(9), lo(10)), (c(k), c(9), hi(13)), 0.008, 5)
    for z in range(10, 14, 2):
        fe.rod("steel_dark", (lo(-4), c(9), c(z)), (hi(4), c(9), c(z)), 0.008, 5)
    for x in (-5, 5):
        for z in (10, 13):
            w.box2(K.wood("dark", "y"), (lo(x), lo(0), lo(z)), (hi(x), hi(3), hi(z)), bevel=0.008)
    w.box2(K.wood("dark", "x"), (lo(-5), lo(1), lo(10)), (hi(5), hi(1), hi(10)), bevel=0.006)
    return a


def stamp_mill():
    a = Asset("stamp_mill", "Industry")
    w, fe, cu, st = a.p("Wood"), a.p("Iron"), a.p("Copper"), a.p("Stone")
    for z in (-5, 5):
        w.box2(K.wood("dark", "x"), (lo(-12), lo(0), lo(z)), (hi(12), hi(1), hi(z)), bevel=0.012)
        w.box2(K.wood("", "x"), (lo(-12), lo(27), lo(z)), (hi(12), hi(27), hi(z)), bevel=0.012)
    for x in (-11, 11):
        for z in (-5, 5):
            w.box2(K.wood("", "y"), (lo(x), lo(2), lo(z)), (hi(x), hi(26), hi(z)), bevel=0.012)
        w.box2(K.wood("", "z"), (lo(x), lo(27), lo(-5)), (hi(x), hi(27), hi(5)), bevel=0.012)
        w.beam(K.wood("dark", "y"), (c(x), c(3), c(-5)), (c(x), c(14), c(0)), 0.06, 0.05)
    for z in (-2, 2):
        w.box2(K.wood("light", "x"), (lo(-10), lo(22), lo(z)), (hi(10), hi(22), hi(z)), bevel=0.01)
    holes = [(lo(-7), hi(7), lo(4), hi(10))]
    fe.box2("steel_dark", (lo(-8), lo(0), lo(-3)), (hi(8), lo(4), hi(3)), bevel=0.02)                    # mortar
    for (x0, x1, z0, z1) in ((lo(-8), hi(8), lo(-3), hi(-3)), (lo(-8), hi(8), lo(3), hi(3)), (lo(-8), hi(-8), lo(-3), hi(3)), (lo(8), hi(8), lo(-3), hi(3))):
        fe.box2("steel_dark", (x0, lo(4), z0), (x1, hi(9), z1), bevel=0.01)
    for z in (-3, 3):
        fe.box2("rust", (lo(-8), lo(9), lo(z)), (hi(8), hi(9), hi(z)), bevel=0.006)
    fe.cyl("steel", 1.2 * VS, hi(13) - lo(-10), ((lo(-10) + hi(13)) / 2, c(19), c(-3)), "x", 12, 0.004)   # cam shaft
    for x in (-5, 0, 5):
        fe.box2("chrome", (lo(x - 1), lo(18), lo(-2)), (hi(x + 1), hi(20), hi(-1)), bevel=0.01)
    fe.cyl("rust", 4.2 * VS, 2 * VS, (c(14.5), c(19), c(-3)), "x", 24, 0.01)                             # pulley
    fe.cyl("steel_dark", 1.2 * VS, 2 * VS + 0.02, (c(14.5), c(19), c(-3)), "x", 12, 0.004)
    fe.quad("black", (c(14.5), c(19) - 0.3, c(-3)), (c(14.5), c(5), c(-7) + 0.06), (c(14.5), c(5), c(-7) - 0.02), (c(14.5), c(19) - 0.3, c(-3) - 0.1), 0.06)
    fe.box2("gen", (lo(12), lo(0), lo(-10)), (hi(17), hi(5), hi(-5)), bevel=0.03)                        # motor
    cu.box2("copper", (lo(12), lo(2), lo(-11)), (hi(17), hi(3), hi(-11)), bevel=0.006)
    for k in range(6):                                      # amalgam apron (copper plate in ribs)
        cu.box2("copper" if k % 2 else "brass", (lo(-7), lo(1), lo(4) + k * 0.08), (hi(7), hi(1), lo(4) + (k + 1) * 0.08), bevel=0.002)
    w.box2(K.wood("", "x"), (lo(-7), lo(0), lo(10)), (hi(7), hi(3), hi(13)), bevel=0.012)                 # concentrate box
    st.box2("sand", (lo(-6), hi(2) - 0.02, lo(11)), (hi(6), hi(2), hi(12)), bevel=0)
    secs = [[(lo(-7), lo(10), lo(-11)), (hi(7), lo(10), lo(-11)), (hi(7), lo(10), hi(-6)), (lo(-7), lo(10), hi(-6))],
            [(lo(-7) - 0.06, hi(16), lo(-11) - 0.04), (hi(7) + 0.06, hi(16), lo(-11) - 0.04), (hi(7) + 0.06, hi(16), hi(-6)), (lo(-7) - 0.06, hi(16), hi(-6))]]
    for side in range(4):
        w.quad(K.WOODS["x" if side % 2 == 0 else "z"][side % 3], secs[0][side], secs[0][(side + 1) % 4], secs[1][(side + 1) % 4], secs[1][side], 0.05)
    r = K.rng(4408)
    for k in range(14):
        st.rock("rock_grey", r.uniform(0.05, 0.09), (r.uniform(-0.4, 0.4), hi(13) + r.uniform(0, 0.06), r.uniform(c(-10), c(-7))), seed=k, squash=0.8, detail=1)
    w.quad(K.wood("dark", "z"), (lo(-3), lo(10), hi(-6)), (hi(3), lo(10), hi(-6)), (hi(3), lo(9), lo(-4)), (lo(-3), lo(9), lo(-4)), 0.03)
    for i, x in enumerate((-0.4, 0.0, 0.4)):                # the stamps (movers): shoe, stem, tappet
        piv = (x, 0.36, 0.0)
        m = a.p("Stamp%d" % i, "Iron", pivot=piv, mover=True)
        m.cyl("steel_dark", 1.3 * VS, 3 * VS, (x, 0.36 + c(1), 0), "y", 14, 0.008)
        m.cyl("chrome", 0.03, c(17) - c(3), (x, 0.36 + (c(3) + c(17)) / 2, 0), "y", 10, 0.002)
        m.cyl("rust", 1.3 * VS, 2 * VS, (x, 0.36 + c(13.5), 0), "y", 12, 0.006)
    return a


def mine_prop():
    a = Asset("mine_prop", "Structure")
    w, fe = a.p("Wood"), a.p("Iron")
    for x in (-10, 10):
        w.add(K.kit.bm_cyl(1.5 * VS, hi(28) - lo(0), 10, 0.01, 1.4 * VS), K.wood("dark", "y"), Matrix.Translation((c(x), (hi(28) + lo(0)) / 2, 0)) @ K._axis_mtx("y"))
    w.box2(K.wood("", "x"), (lo(-13), lo(29), lo(-1)), (hi(13), hi(31), hi(1)), bevel=0.03)
    for x in (-8, 8):
        w.prism(K.wood("light", "x"), [(lo(x) - 0.04, hi(28)), (hi(x) + 0.02, hi(28)), (hi(x) + 0.02, lo(29))], lo(-1), hi(1), plane="xy")
    for x in (-12, 12):
        w.box2(K.wood("light", "z"), (lo(x), lo(32), lo(-1)), (hi(x), hi(32), hi(1)), bevel=0.01)
    for x in (-10, 10):
        fe.box2("steel_dark", (lo(x - 1) - 0.01, lo(27), lo(-2)), (hi(x + 1) + 0.01, hi(27), hi(2)), bevel=0.004)
    fe.box2("ochre", (c(-10) - 0.04, c(20), hi(1) + 0.02), (c(-10) + 0.04, c(22) + 0.04, hi(2)), bevel=0.003)
    return a


def mine_lamp():
    a = Asset("mine_lamp", "Furniture")
    w, fe, g = a.p("Wood"), a.p("Iron"), a.p("Glass")
    w.box2(K.wood("dark", "x"), (lo(-1), lo(0), lo(-1)), (hi(1), hi(0), hi(1)), bevel=0.01)
    w.box2(K.wood("", "y"), (lo(0) + 0.01, hi(0), lo(0) + 0.01), (hi(0) - 0.01, hi(22), hi(0) - 0.01), bevel=0.008)
    fe.tube("steel_dark", [(c(0), c(22), 0), (c(4), c(22), 0), (c(4), c(21), 0)], 0.012, 6)
    fe.lathe("steel_dark", [(0.0, 0.0), (0.1, 0.0), (0.1, 0.015), (0.0, 0.015)], (c(4), lo(16), 0), "y", 12)
    fe.lathe("steel_dark", [(0.0, 0.0), (0.09, 0.0), (0.05, 0.05), (0.0, 0.055)], (c(4), hi(20) - 0.06, 0), "y", 12)
    for k in range(4):
        an = k * math.pi / 2 + math.pi / 4
        fe.rod("steel_dark", (c(4) + math.cos(an) * 0.075, lo(16) + 0.01, math.sin(an) * 0.075), (c(4) + math.cos(an) * 0.075, hi(20) - 0.05, math.sin(an) * 0.075), 0.006, 4)
    g.lathe("glass_clear", [(0.0, 0.0), (0.05, 0.0), (0.065, 0.1), (0.05, 0.25), (0.0, 0.26)], (c(4), lo(16) + 0.015, 0), "y", 12)
    g.lathe("flame", [(0.0, 0.0), (0.02, 0.02), (0.0, 0.07)], (c(4), c(17.5), 0), "y", 8)
    return a


def mine_rail():
    a = Asset("mine_rail", "Industry")
    w, fe = a.p("Wood"), a.p("Iron")
    for z in range(-12, 13, 4):
        w.box2(K.WOODS["x"][(z // 4) % 3], (lo(-6), lo(0), lo(z)), (hi(6), hi(1), hi(z + (1 if z < 12 else 0))), bevel=0.012)
    for x in (-4, 4):                                       # rails: foot, web, head
        fe.box2("rust", (lo(x) - 0.01, lo(2), lo(-12)), (hi(x) + 0.01, lo(2) + 0.012, hi(12)), bevel=0.002)
        fe.box2("steel_dark", (c(x) - 0.008, lo(2), lo(-12)), (c(x) + 0.008, hi(2) - 0.015, hi(12)), bevel=0.0)
        fe.box2("chrome", (c(x) - 0.02, hi(2) - 0.018, lo(-12)), (c(x) + 0.02, hi(2), hi(12)), bevel=0.004)
        for z in range(-12, 13, 4):
            fe.cyl("steel_dark", 0.012, 0.02, (c(x) + 0.03, hi(1) + 0.01, c(z)), "y", 6, 0.002)
            fe.cyl("steel_dark", 0.012, 0.02, (c(x) - 0.03, hi(1) + 0.01, c(z)), "y", 6, 0.002)
    for z in (-12, 12):
        for x in (-5, 5):
            fe.box2("steel_dark", (lo(x) + 0.02, lo(2), lo(z)), (hi(x) - 0.02, hi(2) - 0.02, hi(z)), bevel=0.004)
    return a


def ore_cart():
    a = Asset("ore_cart", "Industry")
    fe, st = a.p("Iron"), a.p("Stone")
    secs = [[(lo(-4), lo(5), lo(-6)), (hi(4), lo(5), lo(-6)), (hi(4), lo(5), hi(6)), (lo(-4), lo(5), hi(6))],
            [(lo(-6), hi(13), lo(-8)), (hi(6), hi(13), lo(-8)), (hi(6), hi(13), hi(8)), (lo(-6), hi(13), hi(8))]]
    for side in range(4):
        fe.quad("rust", secs[0][side], secs[0][(side + 1) % 4], secs[1][(side + 1) % 4], secs[1][side], 0.025)
    fe.box2("rust", (lo(-4), lo(5), lo(-6)), (hi(4), lo(5) + 0.025, hi(6)), bevel=0.004)
    for z in (-8, 8):
        fe.box2("steel", (lo(-6), lo(13), lo(z)), (hi(6), hi(13), hi(z)), bevel=0.008)
    for x in (-6, 6):
        fe.box2("steel", (lo(x), lo(13), lo(-8)), (hi(x), hi(13), hi(8)), bevel=0.008)
    for x in (-4, 4):
        for z in (-4, 4):
            fe.cyl("steel_dark", 2.3 * VS, VS * 0.8, (c(x), c(2), c(z)), "x", 20, 0.008)
            fe.cyl("chrome", 1.0 * VS, VS * 0.82, (c(x), c(2), c(z)), "x", 12, 0.004)
            xi = x - 1 if x > 0 else x + 1
            fe.cyl("black", 2.9 * VS, VS * 0.4, (c(xi), c(2), c(z)), "x", 20, 0.004)
    for z in (-4, 4):
        fe.rod("steel", (c(-4), c(2), c(z)), (c(4), c(2), c(z)), 0.025, 8)
    fe.box2("steel_dark", (lo(-2), lo(3), lo(-5)), (hi(2), hi(4), hi(5)), bevel=0.01)
    for x in (-3, 3):
        fe.rod("steel", (c(x), c(8), c(-7.6)), (c(x), c(12), c(-9.5)), 0.014, 6)
    fe.rod("chrome", (c(-3), c(12), c(-9.5)), (c(3), c(12), c(-9.5)), 0.018, 8)
    r = K.rng(4802)
    for k in range(18):
        st.rock(["rock_grey", "rock", "ore_rust"][k % 3], r.uniform(0.05, 0.09), (r.uniform(-0.3, 0.3), c(7) + r.uniform(0, 0.12), r.uniform(-0.45, 0.45)), seed=k, squash=0.8, detail=1)
    return a


def drying_rack():
    a = Asset("drying_rack", "Garden")
    w, cl, f = a.p("Wood"), a.p("Cloth"), a.p("Food", "Wood")
    for x in (-9, 9):
        for sz in (-1, 1):
            w.beam(K.wood("dark", "y"), (c(x), lo(0), sz * c(4)), (c(x), hi(16), sz * c(0.8)), 0.06, 0.05)
        w.box2(K.wood("dark", "z"), (lo(x), lo(16), lo(-1)), (hi(x), hi(17), hi(1)), bevel=0.008)
    w.cyl(K.wood("", "x"), 0.035, hi(10) - lo(-10), (0, c(17), 0), "x", 10, 0.004)
    for t in range(4):                                     # slatted trays
        y = 3 + t * 4
        half = 4 - (y // 5)
        for k in range(-8, 9, 2):
            w.box2(K.wood("light", "z"), (c(k) - 0.025, lo(y), lo(-half)), (c(k) + 0.025, hi(y) - 0.04, hi(half)), bevel=0.004)
        for z in (-half, half):
            w.box2(K.wood("", "x"), (lo(-8), lo(y) - 0.02, c(z) - 0.02), (hi(8), hi(y) - 0.04, c(z) + 0.02), bevel=0.004)
    for x in range(-7, 8, 2):                              # apple rings, tomatoes, corn, fish
        f.torus("cloth_cream", 0.03, 0.012, (c(x), hi(3) - 0.02, c((x // 2) % 2)), "y", 10, 4)
    for x in range(-6, 7, 3):
        f.sphere("crimson", 0.04, (c(x), hi(7) + 0.0, 0), (1, 0.6, 1), 10, 6)
    for x in range(-7, 6, 4):
        f.superloft("ochre", [(c(x) - 0.02, 0, hi(11) + 0.0, 0.025, 0.025), (c(x) + 0.08, 0, hi(11) + 0.01, 0.035, 0.035), (c(x + 1) + 0.06, 0, hi(11), 0.02, 0.02)], 10, 2.2, axis="x")
    f.prism("chrome", [(0.0, c(16)), (0.25, c(15)), (0.18, c(14)), (0.0, c(14.3)), (-0.18, c(14)), (-0.25, c(15))], -0.012, 0.012, plane="xy")
    f.prism("cloth_cream", [(0.0, c(15.8)), (0.18, c(15)), (0.12, c(14.3)), (0.0, c(14.5)), (-0.12, c(14.3)), (-0.18, c(15))], 0.012, 0.016, plane="xy")
    for k in range(10):                                    # cloth fly
        x0 = lo(-10) + k * (hi(10) - lo(-10)) / 10
        m = "cloth_cream" if (k // 2) % 2 == 0 else "sheet"
        cl.panel(m, (x0, hi(18) - 0.2, lo(-5)), (x0, hi(18) + 0.03, 0.0), (x0 + (hi(10) - lo(-10)) / 10, hi(18) + 0.03, 0.0), 0.01)
        cl.panel(m, (x0, hi(18) + 0.03, 0.0), (x0, hi(18) - 0.2, hi(5)), (x0 + (hi(10) - lo(-10)) / 10, hi(18) - 0.2, hi(5)), 0.01)
    a.parts["Food"].byte = "Wood"
    return a


def pickling_crock():
    a = Asset("pickling_crock", "Furniture")
    cl, w, st, g = a.p("Clay"), a.p("Wood"), a.p("Stone"), a.p("Glass")
    R = 3.8 * VS
    cl.lathe("enamel", [(0.0, lo(0)), (R * 0.85, lo(0)), (R, c(2)), (R, c(6)), (R * 0.8, hi(7)), (R * 0.72, hi(8)), (R * 0.65, hi(8)), (R * 0.6, hi(7)), (0.0, hi(7))], (0, 0, 0), "y", 24)
    cl.torus("cloth_blue", R + 0.002, 0.02, (0, c(5), 0), "y", 24, 5)
    cl.lathe("ochre", [(0.0, lo(0)), (R * 0.86, lo(0)), (R * 0.99, c(1.6)), (0.0, c(1.6))], (0, 0.002, 0), "y", 24)
    w.cyl(K.wood("", "x"), 2.4 * VS, VS * 0.6, (0, c(9) - 0.02, 0), "y", 18, 0.008)
    st.rock("rock_grey", 0.09, (0, c(10), 0), seed=4114, squash=0.7, detail=2)
    for (x, m) in ((5, "crimson"), (7, "green")):
        g.lathe("glass_clear", [(0.0, lo(0)), (0.09, lo(0)), (0.09, hi(3) - 0.02), (0.07, hi(3)), (0.0, hi(3))], (c(x), 0, c(2)), "y", 12)
        g.lathe(m, [(0.0, lo(0) + 0.005), (0.085, lo(0) + 0.005), (0.085, hi(2)), (0.0, hi(2))], (c(x), 0, c(2)), "y", 12)
        g.cyl("chrome", 0.075, 0.03, (c(x), hi(3) + 0.012, c(2)), "y", 12, 0.004)
    a.parts["Glass"].byte = "Glass"
    return a


TILES = [("furn_clinic", "Clinic bed with drip stand, surgery table with lamp + trolley, medicine cabinet", [lambda: [clinic_bed(), surgery_table(), medicine_cabinet()]]),
         ("furn_mining", "Sluice box, stamp mill (+3 stamps), mine prop, tunnel lamp, mine rail, ore cart", [lambda: [sluice_box(), stamp_mill(), mine_prop(), mine_lamp(), mine_rail(), ore_cart()]]),
         ("furn_seasons", "Drying rack with produce, pickling crock + jars", [lambda: [drying_rack(), pickling_crock()]])]

if __name__ == "__main__":
    run(TILES, "medmine_")
