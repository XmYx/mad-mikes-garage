"""HD pieces of FurnitureLibrary.Workshops.cs / .Refining.cs / .Power.cs: chemistry lab, tanning rack, smokehouse, loom,
gunsmith bench, hangar (0.16 m voxels), pumpjack (+ Beam), refinery, oil press, steam generator, solar panel, large
wind turbine (0.16 m voxels, + Rotor), water wheel (+ Wheel). Movers pivot where the game places them.

blender -b -P workshops.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fcommon import K, Asset, Vector, Matrix, lo, hi, c, W, knob, screws, leg_square, drawer_front, bulb, run, VS  # noqa: E402


def chemlab():
    a = Asset("chemlab", "Industry")
    fe, g, w = a.p("Iron"), a.p("Glass"), a.p("Wood")
    for x in (-10, 10):
        for z in (-4, 4):
            fe.box2("steel_dark", (lo(x) + 0.01, lo(0), lo(z) + 0.01), (hi(x) - 0.01, hi(8), hi(z) - 0.01), bevel=0.006)
    fe.box2("steel_dark", (lo(-10), lo(2), lo(-4)), (hi(10), lo(2) + 0.03, hi(4)), bevel=0.004)
    fe.box2("steel", (lo(-11), lo(9), lo(-5)), (hi(11), hi(9), hi(5)), bevel=0.01)
    fe.box2("black", (lo(-6), lo(10), lo(-1)), (hi(-4), hi(10) - 0.02, hi(1)), bevel=0.01)                 # burner
    fe.cyl("lamp_amber", 0.03, 0.02, (c(-5), hi(10) - 0.01, 0), "y", 10, 0.002)
    for k in range(3):                                     # tripod
        an = k * math.tau / 3
        fe.rod("steel_dark", (c(-5) + math.cos(an) * 0.11, hi(10) - 0.02, math.sin(an) * 0.11), (c(-5) + math.cos(an) * 0.08, lo(11) + 0.01, math.sin(an) * 0.08), 0.005, 4)
    g.lathe("glass_clear", [(0.0, 0.0), (0.12, 0.02), (0.15, 0.1), (0.12, 0.2), (0.03, 0.25), (0.025, 0.36), (0.0, 0.36)], (c(-5), lo(11), 0), "y", 18)
    g.lathe("glow_green", [(0.0, 0.0), (0.11, 0.02), (0.135, 0.09), (0.0, 0.09)], (c(-5), lo(11) + 0.005, 0), "y", 18)
    g.tube("glass_clear", [(c(-5), lo(11) + 0.36, 0), (c(-3), c(14.8), c(-0.3)), (c(3), c(12), c(-1))], 0.018, 8)
    g.lathe("glass_clear", [(0.0, 0.0), (0.09, 0.0), (0.09, 0.22), (0.07, 0.24), (0.0, 0.24)], (c(4), hi(9), c(-2)), "y", 14)
    g.lathe("ochre", [(0.0, 0.0), (0.085, 0.0), (0.085, 0.14), (0.0, 0.14)], (c(4), hi(9) + 0.005, c(-2)), "y", 14)
    g.lathe("glass_clear", [(0.0, 0.0), (0.09, 0.0), (0.09, 0.16), (0.07, 0.18), (0.0, 0.18)], (c(7), hi(9), c(2)), "y", 14)
    g.lathe("crimson", [(0.0, 0.0), (0.085, 0.0), (0.085, 0.08), (0.0, 0.08)], (c(7), hi(9) + 0.005, c(2)), "y", 14)
    w.box2(K.wood("", "x"), (lo(-1), lo(10), lo(2)), (hi(2), hi(10), hi(3)), bevel=0.006)                    # test tube rack
    w.box2(K.wood("", "x"), (lo(-1), c(11.5), lo(2)), (hi(2), c(11.5) + 0.02, hi(3)), bevel=0.004)
    for x in range(-1, 3):
        g.cyl("glass_clear", 0.016, 0.16, (c(x), lo(11) + 0.08, c(2.6)), "y", 8, 0.004)
        g.cyl("glow_green" if x % 2 == 0 else "cloth_blue", 0.014, 0.08, (c(x), lo(11) + 0.05, c(2.6)), "y", 8, 0.0)
    return a


def tanning_rack():
    a = Asset("tanning_rack", "Industry")
    w, cl = a.p("Wood"), a.p("Cloth")
    for x in (-8, 8):
        w.box2(K.wood("dark", "y"), (lo(x), lo(0), lo(0)), (hi(x), hi(18), hi(0)), bevel=0.01)
        w.box2(K.wood("dark", "z"), (lo(x) - 0.01, lo(0), lo(-3)), (hi(x) + 0.01, hi(0), hi(3)), bevel=0.008)
        w.beam(K.wood("", "y"), (c(x), c(3), c(-2.5)), (c(x), c(1), c(-0.5)), 0.04, 0.035)
    for y in (2, 17):
        w.box2(K.wood("", "x"), (lo(-8), lo(y), lo(0) + 0.01), (hi(8), hi(y), hi(0) - 0.01), bevel=0.008)
    # stretched hide: ragged outline, darker spine, slight belly
    outline = []
    for k in range(24):
        an = k / 24 * math.tau
        rx, ry = 0.5, 0.48
        wob = 1 + 0.08 * math.sin(an * 5) + 0.05 * math.sin(an * 9 + 1)
        outline.append((math.cos(an) * rx * wob, c(9.5) + math.sin(an) * ry * wob))
    secs = [[(x, y, -0.01) for x, y in outline], [(x * 0.97, y, 0.015) for x, y in outline]]
    cl.loft("leather", secs)
    cl.box2("rust", (-0.06, c(5), 0.012), (0.06, c(14), 0.02), bevel=0.01)
    for y in range(5, 15, 3):                                # lacing to the posts
        for sx in (-1, 1):
            cl.rod("rope", (sx * 0.44, c(y), 0.0), (sx * c(8), c(y), 0.0), 0.006, 4)
    return a


def smokehouse():
    a = Asset("smokehouse", "Industry")
    w, s, st, fe = a.p("Wood"), a.p("Scrap"), a.p("Stone"), a.p("Iron")
    st.box2("gravel", (lo(-8), lo(0), lo(-7)), (hi(8), hi(0), hi(7)), bevel=0.01)
    door = [(lo(-3), hi(3), lo(1), hi(11))]
    K.boards_xy(w, K.WOODS["y"], lo(-7), hi(7), hi(0), hi(16), hi(6) - 0.06, hi(6), 0.24, door, seed=1621)
    K.boards_xy(w, K.WOODS["y"], lo(-7), hi(7), hi(0), hi(16), lo(-6), lo(-6) + 0.06, 0.24, (), seed=1622)
    for sx in (-1, 1):
        x0 = lo(-7) if sx < 0 else hi(7) - 0.06
        for k in range(5):
            z0 = lo(-6) + 0.06 + k * (hi(6) - lo(-6) - 0.12) / 5
            w.box2(K.WOODS["y"][k % 3], (x0, hi(0), z0 + 0.003), (x0 + 0.06, hi(16), z0 + (hi(6) - lo(-6) - 0.12) / 5 - 0.003), bevel=0.005)
    for y0 in (lo(12),):                                    # soot band on the upper boards
        for (x0, x1, z0, z1) in ((lo(-7) - 0.002, hi(7) + 0.002, hi(6) - 0.002, hi(6) + 0.002), (lo(-7) - 0.002, hi(7) + 0.002, lo(-6) - 0.002, lo(-6) + 0.002)):
            w.box2("ash", (x0, y0, z0), (x1, hi(16), z1), bevel=0)
    K.boards_xy(w, K.WOODS["y"], lo(-3), hi(3), lo(1), hi(11), hi(6), hi(6) + 0.04, 0.12, (), seed=1624)
    fe.sphere("chrome", 0.015, (c(2), c(6), hi(6) + 0.05), (1, 1, 1), 8, 6)
    s.panel("tin_rust", (lo(-8), hi(17) + 0.24, lo(-7)), (lo(-8), hi(17) - 0.02, hi(7)), (hi(8), hi(17) - 0.02, hi(7)), 0.02)
    s.box2("steel_dark", (lo(4), hi(17) - 0.02, lo(-4)), (hi(5), hi(24), hi(-3)), bevel=0.008)
    s.box2("steel_dark", (lo(4) - 0.03, hi(24) - 0.02, lo(-4) - 0.03), (hi(5) + 0.03, hi(24) + 0.0, hi(-3) + 0.03), bevel=0.004)
    return a


def loom():
    a = Asset("loom", "Industry")
    w, cl = a.p("Wood"), a.p("Cloth")
    for x in (-9, 9):
        w.box2(K.wood("dark", "y"), (lo(x), lo(0), lo(0)), (hi(x), hi(16), hi(0)), bevel=0.01)
        w.box2(K.wood("dark", "z"), (lo(x) - 0.01, lo(0), lo(-3)), (hi(x) + 0.01, hi(0), hi(3)), bevel=0.008)
    for y in (2, 15):
        w.cyl(K.wood("", "x"), 1.5 * VS, hi(9) - lo(-9) - 0.16, (0, c(y), 0), "x", 14, 0.006)
    for x in range(-7, 8, 1):                               # warp threads
        cl.rod("cloth_cream", (c(x) * 0.97, c(8.5), 0.0), (c(x) * 0.97, c(14.2), 0.0), 0.004, 4)
    for k in range(12):                                     # woven cloth bands
        y = lo(3) + k * (hi(8) - lo(3)) / 12
        cl.box2("cloth_red" if k % 2 else "cloth_olive", (lo(-7), y, -0.012), (hi(7), y + (hi(8) - lo(3)) / 12, 0.012), bevel=0.003)
    w.box2(K.wood("dark", "x"), (lo(-8), lo(9), lo(1)), (hi(8), hi(9), hi(1)), bevel=0.008)
    w.box2(K.wood("light", "x"), (c(-3), c(8.4), -0.03), (c(2), c(8.4) + 0.03, 0.03), bevel=0.01)          # shuttle
    return a


def gunsmith():
    a = Asset("gunsmith", "Industry")
    fe, w, s = a.p("Iron"), a.p("Wood"), a.p("Scrap")
    for x in (-11, 11):
        for z in (-4, 4):
            fe.box2("steel_dark", (lo(x) + 0.01, lo(0), lo(z) + 0.01), (hi(x) - 0.01, hi(9), hi(z) - 0.01), bevel=0.006)
    fe.box2("steel_dark", (lo(-11), lo(3), lo(-4)), (hi(11), lo(3) + 0.03, hi(4)), bevel=0.004)
    fe.box2("steel", (lo(-12), lo(10), lo(-5)), (hi(12), hi(10), hi(5)), bevel=0.01)
    fe.box2("steel_dark", (lo(-12), lo(11), lo(-6)), (hi(12), hi(20), hi(-6)), bevel=0.006)                 # pegboard
    for x in range(-11, 12):
        for y in range(12, 20, 2):
            fe.cyl("black", 0.006, 0.004, (c(x) + 0.04, c(y), hi(-6)), "z", 6, 0)
    for x in (-9, -5, -1, 3):
        fe.box2("chrome", (c(x) - 0.01, c(13), lo(-5)), (c(x) + 0.01, c(18), lo(-5) + 0.02), bevel=0.003)
        fe.rod("steel_dark", (c(x), c(18) + 0.03, hi(-6)), (c(x), c(18) + 0.03, lo(-5) + 0.01), 0.004, 4)
    fe.box2("black", (lo(8), lo(11), lo(-1)), (hi(10), hi(13) - 0.03, hi(1)), bevel=0.01)                  # vise
    fe.box2("black", (lo(8) - 0.02, hi(13) - 0.05, lo(-1)), (hi(10) + 0.02, hi(13), lo(-1) + 0.04), bevel=0.006)
    fe.box2("black", (lo(8) - 0.02, hi(13) - 0.05, hi(1) - 0.04), (hi(10) + 0.02, hi(13), hi(1)), bevel=0.006)
    fe.rod("chrome", (c(9), c(14), lo(-1)), (c(9), c(14), hi(1) + 0.04), 0.008, 6)
    # rifle on the bench: barrel, receiver, stock
    fe.rod("black", (c(-8), c(11) + 0.0, c(1)), (c(1), c(11), c(1)), 0.012, 8)
    fe.box2("black", (c(1), lo(11) + 0.01, c(1) - 0.02), (c(3), lo(11) + 0.08, c(1) + 0.02), bevel=0.006)
    w.add(K.kit.bm_box(0.32, 0.08, 0.05, bevel=0.012), K.wood("", "x"), Matrix.Translation((c(5), lo(11) + 0.05, c(1))) @ K.rot(0, 0, -6))
    s.box2("rig_green", (lo(-11), lo(11), lo(2)), (hi(-9), hi(12), hi(4)), bevel=0.012)                   # ammo can
    s.box2("steel_dark", (lo(-11) + 0.02, hi(12) - 0.005, c(3) - 0.012), (hi(-9) - 0.02, hi(12) + 0.012, c(3) + 0.012), bevel=0.004)
    return a


def hangar():
    a = Asset("hangar", "Industry", kind="building", voxel=0.16)
    v = 0.16
    fe, w = a.p("Iron"), a.p("Wood")
    R, L = 27 * v + v / 2, 30
    z0, z1 = lo(-L, v), hi(L, v)
    # ribs every 10 voxels, purlins, corrugated skin
    for zi in range(-L, L + 1, 10):                         # half-ring ribs
        pts = [(math.cos(k / 24 * math.pi) * (R - 0.03), math.sin(k / 24 * math.pi) * (R - 0.03)) for k in range(25)]
        for (p0, p1) in zip(pts, pts[1:]):
            fe.beam("black", (p0[0], p0[1], c(zi, v)), (p1[0], p1[1], c(zi, v)), 0.14, 0.1)
    n = 30
    for k in range(n):
        a0 = k / n * math.pi
        a1 = (k + 1) / n * math.pi
        m = "tin" if (k // 2) % 2 == 0 else "tin_rust"
        p0 = Vector((math.cos(a0) * R, math.sin(a0) * R, 0))
        p1 = Vector((math.cos(a1) * R, math.sin(a1) * R, 0))
        mid = (p0 + p1) / 2
        nrm = mid.normalized()
        width = (p1 - p0).length
        ang = math.degrees(math.atan2(p1.y - p0.y, p1.x - p0.x))
        mt = Matrix.Translation(mid) @ K.rot(0, 0, ang) @ Matrix(((1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 1, 0), (0, 0, 0, 1)))
        K.corrugated(fe, m, -width / 2 - 0.01, width / 2 + 0.01, z0, z1, 0.022, 0.16, 0.006, mt, samples_per_period=4)
    # back wall (half disc) of vertical sheets
    for k in range(18):
        x0 = -R + k * 2 * R / 18
        x1 = x0 + 2 * R / 18
        h = math.sqrt(max(0.0, R * R - max(abs(x0), abs(x1)) ** 2)) + (0.15 if abs(x0) < R * 0.9 else 0)
        h = min(h, math.sqrt(max(0.0, R * R - min(abs(x0), abs(x1)) ** 2)))
        fe.box2("tin_v" if k % 2 else "tin_rust_v", (x0, 0.0, z0), (x1, h, z0 + 0.03), bevel=0)
    for zi in (-L, L):
        fe.box2("conc", (-R - 0.08, lo(0, v), c(zi, v) - 0.2), (R + 0.08, lo(0, v) + 0.1, c(zi, v) + 0.2), bevel=0.01)
    w.box2(K.wood("", "x"), (lo(-10, v), lo(0, v), lo(-L + 1, v)), (hi(10, v), hi(5, v), hi(-L + 3, v)), bevel=0.02)   # workbench
    for x in (-9, 9):
        w.box2(K.wood("dark", "y"), (c(x, v) - 0.06, lo(0, v), lo(-L + 1, v)), (c(x, v) + 0.06, lo(5, v), lo(-L + 1, v) + 0.12), bevel=0.008)
    fe.box2("ochre", (lo(-1, v), lo(22, v), lo(-L + 1, v)), (hi(1, v), hi(23, v), hi(L - 1, v)), bevel=0.02)   # hoist beam
    fe.box2("steel_dark", (-0.12, lo(21, v), 1.0), (0.12, lo(22, v), 1.3), bevel=0.01)
    fe.tube("steel_dark", [(0, lo(21, v), 1.15), (0, 1.6, 1.15)], 0.008, 4)
    fe.torus("steel", 0.05, 0.01, (0, 1.56, 1.15), "z", 10, 4)
    return a


def pumpjack():
    a = Asset("pumpjack", "Industry")
    fe = a.p("Iron")
    fe.box2("steel_dark", (lo(-6), lo(0), lo(-15)), (hi(6), hi(1), hi(15)), bevel=0.012)
    for z in range(-14, 15, 4):
        fe.box2("black", (lo(-6) + 0.02, hi(1) - 0.004, c(z) - 0.02), (hi(6) - 0.02, hi(1) + 0.002, c(z) + 0.02), bevel=0)
    for x in (-4, 4):                                      # samson post A-frame
        for z in (-3, 3):
            fe.beam("crimson", (c(x), hi(1), c(z)), (c(x) * 0.2, c(21), 0.0), 0.08, 0.07)
    fe.cyl("steel_dark", 0.06, 0.4, (0, c(21.5), 0), "x", 12, 0.004)
    fe.box2("ochre", (lo(-3), lo(2), lo(-13)), (hi(3), hi(8), hi(-7)), bevel=0.03)                          # gearbox
    fe.box2("steel", (lo(-2), lo(2), lo(-6)), (hi(2), hi(5), hi(-4)), bevel=0.02)                           # motor
    fe.tube("black", [(0, c(5), c(-5)), (0, c(6), c(-7))], 0.04, 8)
    for x in (-4, 4):                                      # crank arms (static)
        fe.beam("steel_dark", (c(x), c(6), c(-10)), (c(x), c(12), c(-12)), 0.05, 0.04)
        fe.cyl("steel_dark", 0.1, 0.06, (c(x), c(6), c(-10)), "x", 14, 0.006)
    fe.cyl("steel", 1.5 * VS + 0.02, hi(6) - lo(2), (0, (lo(2) + hi(6)) / 2, c(13)), "y", 14, 0.006)          # wellhead
    fe.box2("chrome", (lo(-2), lo(6), lo(12)), (hi(2), hi(6), hi(14)), bevel=0.01)
    fe.cyl("chrome", 0.016, hi(14) - lo(7), (0, (lo(7) + hi(14)) / 2, c(13)), "y", 8, 0.0)
    # walking beam + horse head + counterweight (pivot at the A-frame top)
    piv = (0.0, 22 * VS, 0.0)
    b = a.p("Beam", "Iron", pivot=piv, mover=True)
    y0 = piv[1]
    b.box2("crimson", (lo(-1), y0 + lo(-1), lo(-13)), (hi(1), y0 + hi(1), hi(12)), bevel=0.02)
    head = [(hi(12) - 0.02, y0 + hi(1)), (hi(14), y0 + hi(1) - 0.04), (hi(14) + 0.04, y0 + c(-2)), (hi(14), y0 + lo(-7)), (hi(12) + 0.04, y0 + lo(-7) + 0.04), (hi(12), y0 + lo(-1))]
    b.prism("steel", [(z, y) for z, y in head], lo(-1) - 0.01, hi(1) + 0.01, plane="zy")
    b.box2("steel_dark", (lo(-2), y0 + lo(-5), lo(-15)), (hi(2), y0 + hi(0), hi(-12)), bevel=0.02)
    b.tube("black", [(0, y0 + lo(-7), hi(14) - 0.03), (0, y0 + lo(-8) - 0.2, hi(13))], 0.008, 4)
    return a


def refinery():
    a = Asset("refinery", "Industry", kind="building")
    fe, s, g = a.p("Iron"), a.p("Scrap"), a.p("Glass")
    fe.box2("conc_dark", (lo(-17), lo(0), lo(-17)), (hi(17), hi(1), hi(17)), bevel=0.02)
    # distillation column with trays (bands), ladder, platforms
    Rc = 4.5 * VS
    fe.lathe("chrome", [(0.0, hi(1)), (Rc, hi(1)), (Rc, hi(44) - 0.1), (Rc * 0.6, hi(44)), (0.0, hi(44))], (c(-6), 0, c(-4)), "y", 28)
    for y in range(8, 44, 8):
        fe.torus("steel_dark", Rc + 0.004, 0.016, (c(-6), c(y), c(-4)), "y", 28, 4)
    for y in (16, 32):
        fe.lathe("steel_dark", [(Rc, c(y)), (Rc + 0.28, c(y)), (Rc + 0.28, c(y) + 0.03), (Rc, c(y) + 0.03)], (c(-6), 0, c(-4)), "y", 28)
        for k in range(14):
            an = k / 14 * math.tau
            fe.rod("steel_dark", (c(-6) + math.cos(an) * (Rc + 0.27), c(y), c(-4) + math.sin(an) * (Rc + 0.27)),
                   (c(-6) + math.cos(an) * (Rc + 0.27), c(y) + 0.5, c(-4) + math.sin(an) * (Rc + 0.27)), 0.008, 4)
    for x in (-0.12, 0.12):
        fe.rod("steel_dark", (c(-6) + x, hi(1), c(-4) + Rc + 0.08), (c(-6) + x, c(40), c(-4) + Rc + 0.08), 0.01, 4)
    for y in range(4, 40, 2):
        fe.rod("steel_dark", (c(-6) - 0.12, c(y), c(-4) + Rc + 0.08), (c(-6) + 0.12, c(y), c(-4) + Rc + 0.08), 0.007, 4)
    fe.lathe("steel", [(0.0, hi(1)), (2.6 * VS + 0.04, hi(1)), (2.6 * VS + 0.04, hi(30) - 0.06), (0.1, hi(30)), (0.0, hi(30))], (c(6), 0, c(-6)), "y", 22)  # stripper
    for y in (12, 22, 32):
        fe.tube("steel_dark", [(c(-6) + Rc - 0.02, c(y), c(-4)), (c(0), c(y) + 0.1, c(-5)), (c(6) - 0.24, c(y - 2), c(-6))], 0.05, 10)
    s.box2("steel", (lo(-4), lo(2), lo(6)), (hi(8), hi(9), hi(14)), bevel=0.03)                             # fired heater
    s.box2("rust", (lo(-4) - 0.002, lo(2), lo(6) + 0.1), (hi(8) + 0.002, lo(4), hi(14) - 0.1), bevel=0.004)
    g.box2("lamp_amber", (lo(-2), lo(3), lo(15)), (hi(6), hi(6), hi(15)), bevel=0.01)
    for k in range(5):
        s.box2("steel_dark", (lo(-2) + k * 0.16, lo(3), hi(15) - 0.0), (lo(-2) + k * 0.16 + 0.03, hi(6), hi(15) + 0.012), bevel=0.003)
    s.cyl("steel_dark", 0.12, 0.8, (c(2), hi(9) + 0.4, c(10)), "y", 14, 0.006)
    fe.cyl("steel_dark", 0.9 * VS + 0.03, hi(50) - lo(2), (c(14), (lo(2) + hi(50)) / 2, c(-10)), "y", 12, 0.004)   # flare stack
    for k in range(3):                                     # guy wires
        an = k * math.tau / 3
        fe.rod("steel_dark", (c(14), c(30), c(-10)), (c(14) + math.cos(an) * 0.25, lo(1) + 0.1, c(-10) + math.sin(an) * 0.25), 0.006, 4)
    fe.lathe("flame", [(0.0, 0.0), (0.08, 0.0), (0.0, 0.12)], (c(14), hi(50) - 0.02, c(-10)), "y", 10)
    for x in (-14, -10):                                   # product tanks
        s.lathe("white", [(0.0, hi(1)), (2.5 * VS + 0.05, hi(1)), (2.5 * VS + 0.05, hi(8) - 0.04), (0.1, hi(8) + 0.02), (0.0, hi(8) + 0.02)], (c(x), 0, c(10)), "y", 20)
        s.torus("rust", 2.5 * VS + 0.055, 0.01, (c(x), c(5), c(10)), "y", 20, 4)
    fe.tube("steel_dark", [(c(-14), c(3), c(10) - 0.25), (c(-14), c(3), c(4)), (c(-6), c(3), c(4)), (c(-6), c(3), c(-4) + Rc)], 0.04, 8)
    return a


def oil_press():
    a = Asset("oil_press", "Industry")
    w, fe = a.p("Wood"), a.p("Iron")
    for x in (-6, 6):
        w.box2(K.wood("dark", "y"), (lo(x), lo(0), lo(0) - 0.02), (hi(x), hi(18), hi(0) + 0.02), bevel=0.012)
        w.box2(K.wood("dark", "z"), (lo(x) - 0.02, lo(0), lo(-4)), (hi(x) + 0.02, lo(0) + 0.08, hi(4)), bevel=0.008)
    w.box2(K.wood("", "x"), (lo(-7), lo(18), lo(-1)), (hi(7), hi(19), hi(1)), bevel=0.012)
    R = 3.8 * VS
    n = 16
    for k in range(n):                                     # barrel staves
        an = k / n * math.tau
        w.add(K.kit.bm_box(R * math.tau / n, 0.04, hi(8) - lo(1), bevel=0.004, segs=1), K.WOODS["y"][k % 3],
              Matrix.Translation((math.cos(an) * (R - 0.02), (lo(1) + hi(8)) / 2, math.sin(an) * (R - 0.02))) @ Matrix.Rotation(-an + math.pi / 2, 4, "Y") @ K._axis_mtx("y"))
    for y in (3, 6, 8):
        fe.torus("steel_dark", R + 0.005, 0.01, (0, c(y), 0), "y", 20, 4)
    fe.cyl("steel_dark", R + 0.02, 0.03, (0, lo(1) + 0.015, 0), "y", 20, 0.004)
    fe.cyl("steel", 0.035, hi(17) - lo(9), (0, (lo(9) + hi(17)) / 2, 0), "y", 10, 0.0)                   # screw
    for k in range(10):
        fe.torus("steel_dark", 0.035, 0.008, (0, lo(10) + k * 0.06, 0), "y", 10, 4)
    fe.rod("steel", (lo(-5), c(16), 0), (hi(5), c(16), 0), 0.018, 8)                                    # bar
    for sx in (-1, 1):
        fe.sphere("steel_dark", 0.03, (sx * hi(5), c(16), 0), (1, 1, 1), 8, 6)
    fe.cyl("steel", 0.25, 0.04, (0, c(9), 0), "y", 20, 0.006)
    fe.tube("steel_dark", [(0, c(2), R - 0.02), (0, c(2), hi(6) - 0.02), (0, c(1.4), hi(6))], 0.016, 8)
    fe.sphere("ochre", 0.012, (0, c(1), hi(6)), (1, 1.4, 1), 6, 4)
    return a


def coal_generator():
    a = Asset("coal_generator", "Utility")
    fe, st = a.p("Iron"), a.p("Stone")
    R = 5.4 * VS
    fe.lathe("steel_dark", [(0.0, 0.0), (R - 0.03, 0.0), (R, 0.04), (R, hi(6) - lo(-9) - 0.04), (R - 0.03, hi(6) - lo(-9)), (0.0, hi(6) - lo(-9))], (0, c(7), lo(-9)), "z", 28)
    for z in range(-8, 7, 4):
        fe.torus("black", R + 0.004, 0.014, (0, c(7), c(z)), "z", 28, 4)
    for k in range(12):
        an = k / 12 * math.tau
        fe.sphere("steel", 0.012, (math.cos(an) * (R + 0.01), c(7) + math.sin(an) * (R + 0.01), lo(-9) + 0.04), (1, 1, 1), 6, 4)
    for z in (-7, 3):                                      # saddles
        fe.box2("black", (lo(-4), lo(0), c(z) - 0.08), (hi(4), c(4), c(z) + 0.08), bevel=0.01)
    st.box2("brick_dark", (lo(-5), lo(0), lo(6)), (hi(5), hi(7), hi(9)), bevel=0.02)                      # firebox
    st.box2("black", (lo(-2), lo(2), hi(9) - 0.01), (hi(2), hi(4), hi(10)), bevel=0.01)
    a.p("Glow", "Stone").box2("ember", (lo(-2) + 0.03, lo(2) + 0.03, hi(10) - 0.006), (hi(2) - 0.03, hi(4) - 0.03, hi(10)), bevel=0)
    fe.cyl("steel_dark", 0.11, hi(20) - lo(11), (0, (lo(11) + hi(20)) / 2, c(-5)), "y", 14, 0.006)        # stack
    fe.cyl("steel_dark", 0.14, 0.03, (0, hi(20), c(-5)), "y", 14, 0.004)
    fe.cyl("crimson", 4.4 * VS, 0.16, (c(6.5), c(8), c(-10)), "x", 28, 0.01)                              # flywheel
    fe.cyl("steel", 0.06, 0.2, (c(6.5), c(8), c(-10)), "x", 14, 0.006)
    for k in range(6):
        an = k / 6 * math.tau
        fe.box("steel_dark", (0.06, 0.6, 0.04), (c(6.5), c(8), c(-10)), r=(math.degrees(an), 0, 0), bevel=0.004)
    fe.box2("brass", (lo(6), lo(3), lo(-12)), (hi(9), hi(6), hi(-8)), bevel=0.03)                         # dynamo
    fe.cyl("steel", 0.06, 0.14, (c(4), c(8), c(-1)), "y", 12, 0.006)                                      # safety valve
    fe.cyl("white", 0.05, 0.02, (c(3), c(10.5), c(5)), "z", 14, 0.003)                                    # gauge
    return a


def solar_panel():
    a = Asset("solar_panel", "Utility")
    al, g, cu = a.p("Aluminium"), a.p("Glass"), a.p("Copper")
    for x in (-9, 9):
        al.box2("steel_dark", (lo(x) + 0.01, lo(0), lo(-5) + 0.01), (hi(x) - 0.01, hi(6), hi(-5) - 0.01), bevel=0.006)
        al.box2("steel_dark", (lo(x) + 0.01, lo(0), lo(6) + 0.01), (hi(x) - 0.01, hi(12), hi(6) - 0.01), bevel=0.006)
        al.box2("conc", (lo(x) - 0.04, lo(0), lo(-6)), (hi(x) + 0.04, lo(0) + 0.05, hi(7)), bevel=0.01)
    y0, y1 = c(7), c(7) + round(15 * 0.55) * VS
    ang = math.degrees(math.atan2(y1 - y0, c(8) - c(-7)))
    L = math.hypot(y1 - y0, c(8) - c(-7)) + VS
    ctr = (0, (y0 + y1) / 2 + 0.02, (c(-7) + c(8)) / 2)
    al.box("alu", (hi(11) - lo(-11), 0.05, L), ctr, r=(-ang, 0, 0), bevel=0.008)
    g.box("solar", (hi(11) - lo(-11) - 0.06, 0.01, L - 0.06), (ctr[0], ctr[1] + 0.026, ctr[2]), r=(-ang, 0, 0), bevel=0)
    for k in range(-2, 3):
        g.box("chrome", (0.008, 0.012, L - 0.06), (k * 0.4, ctr[1] + 0.028, ctr[2]), r=(-ang, 0, 0), bevel=0)
    for k in range(-2, 3):
        rot = K.rot(-ang, 0, 0)
        p = Vector(ctr) + rot.to_3x3() @ Vector((0, 0.028, k * L / 5.2))
        g.box("chrome", (hi(11) - lo(-11) - 0.06, 0.012, 0.008), tuple(p), r=(-ang, 0, 0), bevel=0)
    cu.box2("black", (lo(-1), lo(1), lo(0)), (hi(1), hi(3), hi(1)), bevel=0.01)
    cu.cyl("lamp_amber", 0.014, 0.01, (0, c(3), hi(1) + 0.003), "z", 8, 0)
    cu.tube("black", [(0, hi(3), c(0.5)), (0, c(6), c(1)), (0, c(9), c(1.3))], 0.008, 5)
    return a


def wind_turbine_large():
    a = Asset("wind_turbine_large", "Utility", kind="building", voxel=0.16)
    v = 0.16
    cc, fe = a.p("Concrete"), a.p("Iron")
    cc.box2("conc_light", (lo(-5, v), lo(0, v), lo(-5, v)), (hi(5, v), hi(1, v), hi(5, v)), bevel=0.03)
    prof = [(0.0, hi(1, v))]
    for y in (2, 30, 60, 90):
        prof.append(((2.6 + (1.3 - 2.6) * y / 90 + 0.5) * v, c(y, v) if y > 2 else hi(1, v)))
    prof.append((0.0, hi(90, v)))
    fe.lathe("white", prof, (0, 0, 0), "y", 28)
    for y in (20, 50, 80):
        fe.torus("rust", (2.7 + (1.4 - 2.7) * y / 90 + 0.45) * v, 0.014, (0, c(y, v), 0), "y", 28, 4)
    fe.superloft("white", [(lo(-4, v), 0, c(92.5, v), 0.3, 0.38), (lo(-4, v) + 0.15, 0, c(92.5, v), 0.4, 0.48),
                           (hi(4, v) - 0.1, 0, c(92.5, v), 0.4, 0.48), (hi(4, v), 0, c(92.5, v), 0.3, 0.38)], 20, 2.6)
    fe.cyl("lamp_red", 0.05, 0.08, (0, hi(95, v) + 0.04, c(-2.5, v)), "y", 10, 0.006)
    fe.box2("steel_dark", (lo(-2, v), lo(1, v), lo(2, v) + 0.33), (hi(2, v), hi(5, v), hi(2, v) + 0.32), bevel=0.01)
    fe.box2("hazard", (lo(-2, v) - 0.02, hi(5, v), lo(2, v) + 0.33), (hi(2, v) + 0.02, hi(5, v) + 0.06, hi(2, v) + 0.35), bevel=0.006)
    # rotor (spins about +z at (0, 93 * 0.16, 0.9)): spinner + three blades with red tips
    piv = (0.0, 93 * v, 0.9)
    r = a.p("Rotor", "Aluminium", pivot=piv, mover=True)
    r.lathe("white", [(0.0, -0.2), (0.3, -0.2), (0.32, 0.05), (0.18, 0.3), (0.0, 0.42)], piv, "z", 20)
    for b in range(3):
        an = b * math.tau / 3 + 0.3
        d = Vector((math.cos(an), math.sin(an), 0))
        side = Vector((-d.y, d.x, 0))
        secs = []
        for (t, ch, tw) in ((0.25, 0.14, 30), (0.8, 0.36, 20), (2.5, 0.28, 10), (4.2, 0.18, 5), (5.0, 0.09, 2)):
            cpt = Vector(piv) + d * t + Vector((0, 0, 0.1))
            twr = math.radians(tw)
            a1 = cpt + side * (ch * math.cos(twr)) + Vector((0, 0, ch * math.sin(twr) * 0.3))
            a2 = cpt - side * (ch * math.cos(twr)) - Vector((0, 0, ch * math.sin(twr) * 0.3))
            nn = Vector((0, 0, 0.03))
            secs.append([a1 - nn, a2 - nn, a2 + nn, a1 + nn])
        r.loft("white", secs[:4])
        r.loft("crimson", secs[3:])
    return a


def water_turbine():
    a = Asset("water_turbine", "Utility")
    w, fe = a.p("Wood"), a.p("Iron")
    for x in (-9, 9):
        for z in (-8, 8):
            w.beam(K.wood("dark", "y"), (c(x), lo(0), c(z)), (c(x), c(13), c(0) + (0.06 if z > 0 else -0.06)), 0.1, 0.08)
        w.box2(K.wood("dark", "z"), (c(x) - 0.05, lo(0), c(-8) - 0.1), (c(x) + 0.05, lo(0) + 0.08, c(8) + 0.1), bevel=0.008)
    fe.box2("steel_dark", (lo(-10), lo(12), lo(-1)), (hi(10), hi(13), hi(1)), bevel=0.012)
    for x in (-9, 9):
        fe.cyl("steel", 0.08, 0.14, (c(x), c(12.5), 0), "x", 14, 0.008)
    fe.box2("gen", (lo(10), lo(10), lo(-3)), (hi(14), hi(15), hi(3)), bevel=0.03)
    fe.cyl("lamp_amber", 0.02, 0.02, (c(14), hi(15), 0), "y", 8, 0.002)
    # wheel (turns about local x at (0, 1.0, 0)): hub, 12 spokes, paddles, rims
    piv = (0.0, 1.0, 0.0)
    wh = a.p("Wheel", "Wood", pivot=piv, mover=True)
    wh.cyl("steel_dark", 1.6 * VS + 0.02, hi(8) - lo(-8), piv, "x", 16, 0.008)
    for sx in (-6, 6):
        wh.torus(K.wood("dark", "x"), 13.5 * VS, 0.035, (c(sx), piv[1], 0), "x", 36, 6)
    for i in range(12):
        an = i * math.tau / 12
        d = Vector((0, math.cos(an), math.sin(an)))
        for sx in (-6, 6):
            wh.rod(K.wood("", "y"), (c(sx), piv[1] + d.y * 0.14, d.z * 0.14), (c(sx), piv[1] + d.y * 1.06, d.z * 1.06), 0.025, 6)
        cpt = Vector((0, piv[1], 0)) + d * (14 * VS)
        wh.add(K.kit.bm_box(hi(7) - lo(-7), 0.22, 0.04, bevel=0.006), K.wood("light", "x"),
               Matrix.Translation(cpt) @ Matrix.Rotation(an, 4, "X"))
    return a


TILES = [("furn_workshops", "Chemistry lab, tanning rack, smokehouse, loom, gunsmith bench", [lambda: [chemlab(), tanning_rack(), smokehouse(), loom(), gunsmith()]]),
         ("furn_hangar", "Hangar (Quonset, 0.16 m template)", [lambda: [hangar()]]),
         ("furn_refining", "Pumpjack (+Beam), refinery, oil press, steam generator", [lambda: [pumpjack(), refinery(), oil_press(), coal_generator()]]),
         ("furn_power2", "Solar panel, water wheel (+Wheel)", [lambda: [solar_panel(), water_turbine()]]),
         ("furn_turbine_large", "Large wind turbine (+Rotor), 15 m", [lambda: [wind_turbine_large()]])]

if __name__ == "__main__":
    run(TILES, "workshops_")
