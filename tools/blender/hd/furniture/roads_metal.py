"""HD pieces of FurnitureLibrary.Roads.cs / .Metal.cs: rock crusher, stop / speed / direction signs, guard rail, curb,
bollard, timber and steel bridges (0.16 m templates spanning along -Z from the bank), forge and anvil, machine shop.

blender -b -P roads_metal.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fcommon import K, Asset, Vector, Matrix, lo, hi, c, W, knob, screws, leg_square, drawer_front, bulb, run, VS  # noqa: E402

V2 = 0.16


def rock_crusher():
    a = Asset("rock_crusher", "Industry")
    fe, st, rb = a.p("Iron"), a.p("Stone"), a.p("Rubber")
    fe.box2("steel_dark", (lo(-12), lo(0), lo(-9)), (hi(12), hi(1), hi(9)), bevel=0.012)
    for k in range(12):                                    # hazard edge
        for (z0, z1) in ((lo(-9) - 0.004, lo(-9) + 0.006), (hi(9) - 0.006, hi(9) + 0.004)):
            x0 = lo(-12) + k * (hi(12) - lo(-12)) / 12
            fe.box2("hazard" if k % 2 == 0 else "black", (x0, lo(0) + 0.02, z0), (x0 + (hi(12) - lo(-12)) / 12, hi(1) - 0.02, z1), bevel=0)
    fe.box2("ochre", (lo(-6), lo(2), lo(-6)), (hi(6), hi(14), hi(3)), bevel=0.03)                     # crusher body
    for z in (-6, 3):
        for y in (4, 9, 13):
            fe.cyl("steel_dark", 0.022, 0.02, (lo(-6) + 0.1, c(y), (lo(z) if z < 0 else hi(z))), "z", 8, 0.003)
    fe.box2("black", (lo(-4), lo(13), lo(-4)), (hi(4), hi(14) + 0.004, hi(1)), bevel=0.006)
    secs = []                                              # flared hopper (open box)
    for y, r in ((lo(15), 5), (hi(20), 10)):
        rr = (r + 0.5) * VS
        secs.append([(-rr, y, -rr - VS), (rr, y, -rr - VS), (rr, y, rr - 2 * VS), (-rr, y, rr - 2 * VS)])
    for side in range(4):
        q0a, q0b = secs[0][side], secs[0][(side + 1) % 4]
        q1a, q1b = secs[1][side], secs[1][(side + 1) % 4]
        fe.quad("rust", q0a, q0b, q1b, q1a, 0.03)
    r = K.rng(3303)
    for k in range(18):                                    # rock in the hopper
        st.rock("rock_grey", r.uniform(0.08, 0.15), (r.uniform(-0.35, 0.35), hi(16) + r.uniform(0, 0.15), r.uniform(-0.45, 0.15)), seed=k, squash=0.8, detail=1)
    for x in (-9, 8):                                      # flywheels (spoked), shaft
        fe.cyl("crimson", 5.5 * VS, 2 * VS - 0.02, (c(x + 0.5), c(9), c(-2)), "x", 28, 0.01)
        fe.cyl("steel_dark", 1.5 * VS, 2 * VS, (c(x + 0.5), c(9), c(-2)), "x", 14, 0.006)
    fe.cyl("chrome", 0.08, hi(9) - lo(-9), (0, c(9), c(-2)), "x", 12, 0.004)
    fe.box2("rig_green", (lo(7), lo(2), lo(-9)), (hi(11), hi(6), hi(-5)), bevel=0.02)                 # motor
    rb.tube("black", [(c(9), c(5), c(-7)), (c(9), c(12), c(-4))], 0.03, 6)
    for k in range(10):                                    # chute
        z = 4 + k
        y = 4 - k / 3
        fe.box2("steel", (lo(-3), lo(y), lo(z)), (hi(3), lo(y) + 0.04, hi(z)), bevel=0.003)
    for x in (-4, 4):
        fe.beam("steel_dark", (c(x), c(5), c(4)), (c(x), c(2), c(13)), 0.04, 0.06)
    for k in range(40):                                    # gravel heap
        an = r.uniform(0, math.tau)
        d = r.uniform(0, 0.4) ** 1.0
        st.rock("gravel", r.uniform(0.03, 0.06), (math.cos(an) * d * 1.2, lo(0) + 0.03 + (0.4 - d) * 0.45, c(15) + math.sin(an) * d), seed=k + 50, squash=0.8, detail=1)
    st.lathe("gravel", [(0.0, lo(0)), (0.4, lo(0)), (0.0, hi(2))], (0, 0, c(15)), "y", 18)
    return a


def _post(a, x, y1):
    a.p("Iron").cyl("steel", 0.035, hi(y1) - lo(0), (c(x), (hi(y1) + lo(0)) / 2, c(-2)), "y", 10, 0.003)


def sign_stop():
    a = Asset("sign_stop", "Decor")
    s = a.p("Scrap")
    _post(a, 0, 25)
    pts = [(math.cos(math.radians(22.5 + k * 45)) * 0.48, c(30) + math.sin(math.radians(22.5 + k * 45)) * 0.48) for k in range(8)]
    s.prism("steel", pts, lo(-1), hi(-1) - 0.01, plane="xy")
    s.prism("white", pts, hi(-1) - 0.012, hi(-1) + 0.0, plane="xy")
    s.prism("pump_red", [(x * 0.86, c(30) + (y - c(30)) * 0.86) for x, y in pts], hi(-1), hi(-1) + 0.006, plane="xy")
    for k in range(4):                                     # STOP letters as blocks
        x = -0.24 + k * 0.16
        s.box2("white", (x - 0.05, c(30) - 0.08, hi(-1) + 0.006), (x + 0.05, c(30) + 0.08, hi(0)), bevel=0.003)
    for y in (27, 33):
        screws(s, "chrome", [(0, c(y), hi(0))], 0.012)
    return a


def sign_speed():
    a = Asset("sign_speed", "Decor")
    s = a.p("Scrap")
    _post(a, 0, 25)
    s.cyl("steel", 6.6 * VS, 0.02, (0, c(30), c(-1)), "z", 32, 0.004)
    s.cyl("pump_red", 6.5 * VS, 0.01, (0, c(30), hi(-1)), "z", 32, 0.002)
    s.cyl("white", 4.7 * VS, 0.012, (0, c(30), hi(-1) + 0.002), "z", 32, 0.002)
    digits = {4: "101101111001001", 0: "111101101101111"}
    for (d, x0) in ((4, -3), (0, 1)):
        bits = digits[d]
        for row in range(5):
            for col in range(3):
                if bits[row * 3 + col] == "1":
                    s.box2("black", (lo(x0 + col) + 0.005, lo(32 - row) + 0.005, hi(-1) + 0.004), (hi(x0 + col) - 0.005, hi(32 - row) - 0.005, hi(0)), bevel=0.004)
    return a


def sign_direction():
    a = Asset("sign_direction", "Decor")
    s = a.p("Scrap")
    for x in (-7, 7):
        _post(a, x, 31)
    s.box2("steel", (lo(-10), lo(23), lo(-1)), (hi(10), hi(32), hi(-1)), bevel=0.012)
    s.box2("white", (lo(-10) + 0.004, lo(23) + 0.004, hi(-1)), (hi(10) - 0.004, hi(32) - 0.004, hi(-1) + 0.006), bevel=0.01)
    s.box2("green", (lo(-10) + 0.05, lo(23) + 0.05, hi(-1) + 0.004), (hi(10) - 0.05, hi(32) - 0.05, hi(-1) + 0.01), bevel=0.006)
    s.box2("white", (lo(-7), c(27.5) - 0.04, hi(-1) + 0.01), (c(2), c(27.5) + 0.04, hi(0)), bevel=0.003)
    s.prism("white", [(c(2), c(27.5) - 0.32), (c(7.5), c(27.5)), (c(2), c(27.5) + 0.32)], hi(-1) + 0.01, hi(0), plane="xy")
    for x in (-7, 7):
        for y in (25, 30):
            s.box2("steel_dark", (c(x) - 0.06, c(y) - 0.012, lo(-1) - 0.03), (c(x) + 0.06, c(y) + 0.012, lo(-1)), bevel=0.003)
    return a


def guard_rail():
    a = Asset("guard_rail", "Structure")
    fe = a.p("Iron")
    for x in (-12, 0, 12):
        fe.box2("steel_dark", (lo(x) + 0.01, lo(0), lo(0)), (hi(x) - 0.01, hi(9), hi(0)), bevel=0.004)          # I-post
        fe.box2("steel_dark", (lo(x) - 0.01, lo(0), lo(0) + 0.0), (hi(x) + 0.01, hi(9), lo(0) + 0.012), bevel=0.002)
        fe.box2("steel_dark", (lo(x) - 0.01, lo(0), hi(0) - 0.012), (hi(x) + 0.01, hi(9), hi(0)), bevel=0.002)
        fe.box2(K.wood("dark", "z"), (lo(x), lo(7), lo(1)), (hi(x), hi(8), hi(1)), bevel=0.006)               # spacer block
        fe.box2("lamp_amber", (c(x) - 0.03, hi(9) - 0.02, lo(3)), (c(x) + 0.03, hi(9) + 0.03, lo(3) + 0.012), bevel=0.003)
    # W-beam: two ridges + valley along x
    prof = [(lo(2), lo(6)), (hi(2) + 0.01, lo(6) + 0.04), (hi(2) + 0.01, c(7) - 0.03), (lo(2) + 0.02, c(7.5)), (hi(2) + 0.01, c(8) + 0.03), (hi(2) + 0.01, hi(9) - 0.04), (lo(2), hi(9))]
    secs = []
    for x in (lo(-13), hi(13)):
        loop = [(x, y, z) for z, y in prof] + [(x, y, z - 0.012) for z, y in reversed(prof)]
        secs.append(loop)
    fe.loft("chrome", secs)
    for x in range(-12, 13, 6):
        fe.cyl("steel", 0.014, 0.01, (c(x), c(7.5), lo(2) + 0.025), "z", 8, 0.002)
    return a


def curb():
    a = Asset("curb", "Structure")
    cc = a.p("Concrete")
    prof = [(lo(-2), lo(0)), (hi(1), lo(0)), (hi(1), lo(2) + 0.02), (lo(1), hi(2)), (lo(-2), hi(2))]
    cc.prism("conc_light", [(z, y) for z, y in prof], lo(-6) + 0.006, hi(6) - 0.006, plane="zy")
    cc.box2("cream", (lo(-5), hi(2) - 0.004, lo(0) + 0.01), (hi(5), hi(2) + 0.002, hi(0) - 0.01), bevel=0)
    r = K.rng(3351)
    for k in range(6):                                     # chipped arris
        x = r.uniform(-0.4, 0.4)
        cc.rock("conc_dark", 0.025, (x, hi(2) - 0.01, hi(0) + 0.02), seed=k, squash=0.6, detail=1)
    return a


def bollard():
    a = Asset("bollard", "Structure")
    fe = a.p("Iron")
    fe.box2("steel_dark", (lo(-2), lo(0), lo(-2)), (hi(2), hi(0), hi(2)), bevel=0.01)
    for k in range(4):
        y0 = hi(0) + k * 0.24
        fe.cyl("black" if k % 2 == 0 else "hazard", 1.9 * VS, 0.24, (0, y0 + 0.12, 0), "y", 18, 0.0 if 0 < k < 3 else 0.004)
    fe.lathe("chrome", [(0.0, hi(11)), (1.6 * VS, hi(11)), (1.3 * VS, hi(12)), (0.0, hi(12) + 0.01)], (0, 0, 0), "y", 18)
    for k in range(4):
        an = k * math.pi / 2 + math.pi / 4
        fe.cyl("chrome", 0.012, 0.012, (math.cos(an) * 0.15, hi(0) + 0.004, math.sin(an) * 0.15), "y", 6, 0)
    return a


def bridge_timber():
    a = Asset("bridge_timber", "Structure", kind="building", voxel=V2)
    sh, de, fe = a.p("Shell", "Wood"), a.p("Detail", "Wood"), a.p("Iron")
    L = 50
    for zi in range(0, -L, -1):                           # deck planks across x
        m = K.WOODS["x"][(zi * 7) % 3] if zi % 7 else K.wood("dark", "x")
        sh.box2(m, (lo(-12, V2), lo(0, V2), lo(zi, V2) + 0.004), (hi(12, V2), hi(0, V2), hi(zi, V2) - 0.004), bevel=0.01, segs=1)
    for x in (-9, -3, 3, 9):                              # log stringers
        sh.cyl(K.wood("dark", "z"), 0.13, hi(0, V2) - lo(-L + 1, V2), (c(x, V2), c(-1.5, V2), (hi(0, V2) + lo(-L + 1, V2)) / 2), "z", 12, 0.01)
    for x in (-12, 12):
        de.cyl(K.wood("", "z"), 0.08, hi(0, V2) - lo(-L + 1, V2), (c(x, V2), c(1, V2), (hi(0, V2) + lo(-L + 1, V2)) / 2), "z", 10, 0.008)
        for zi in range(0, -L, -7):
            de.box2(K.wood("", "y"), (lo(x, V2) + 0.01, lo(2, V2), lo(zi, V2) + 0.01), (hi(x, V2) - 0.01, hi(6, V2), hi(zi, V2) - 0.01), bevel=0.012)
        for y in (4, 6):
            de.box2(K.wood("light" if y == 6 else "", "z"), (lo(x, V2) + 0.02, lo(y, V2) + 0.03, lo(-L + 1, V2)), (hi(x, V2) - 0.02, hi(y, V2) - 0.02, hi(0, V2)), bevel=0.012)
    for pz in (-2, -25, -47):                             # log piers 4 m down
        for x in (-9, 9):
            sh.cyl(K.wood("dark", "y"), 1.5 * V2 + 0.02, hi(-2, V2) - lo(-25, V2), (c(x, V2), (hi(-2, V2) + lo(-25, V2)) / 2, c(pz, V2)), "y", 12, 0.01)
        sh.box2(K.wood("dark", "x"), (lo(-11, V2), lo(-3, V2), lo(pz - 1, V2)), (hi(11, V2), hi(-3, V2), hi(pz + 1, V2)), bevel=0.02)
        for (x0, x1) in ((-9, 9), (9, -9)):
            de.beam(K.wood("", "y"), (c(x0, V2), c(-22, V2), c(pz, V2) + 0.12), (c(x1, V2), c(-5, V2), c(pz, V2) + 0.12), 0.12, 0.08)
        for x in (-10, -8, 8, 10):
            fe.cyl("steel_dark", 0.025, 0.04, (c(x, V2), c(-3, V2), hi(pz + 1, V2)), "z", 8, 0.004)
    return a


def bridge_steel():
    a = Asset("bridge_steel", "Structure", kind="building", voxel=V2)
    sh, de, cc = a.p("Shell", "Iron"), a.p("Detail", "Iron"), a.p("Concrete")
    L = 75
    Z0, Z1 = lo(-L + 1, V2), hi(0, V2)
    sh.box2("steel", (lo(-11, V2), lo(0, V2), Z0), (hi(11, V2), hi(0, V2), Z1), bevel=0.01)          # checker deck plate
    for k in range(60):
        z = Z0 + (k + 0.5) * (Z1 - Z0) / 60
        de.box2("steel_dark", (lo(-9, V2), hi(0, V2), z - 0.01), (hi(9, V2), hi(0, V2) + 0.006, z + 0.01), bevel=0)
    for zi in range(0, -L, -2):
        for x in (-10, 10):
            de.box2("hazard" if (zi // 2) % 2 == 0 else "black", (lo(x, V2), hi(0, V2), lo(zi, V2)), (hi(x, V2), hi(0, V2) + 0.008, hi(zi, V2) + V2), bevel=0)
    for zi in range(0, -L, -6):                           # cross girders
        sh.box2("ochre", (lo(-11, V2), lo(-3, V2), lo(zi, V2) + 0.02), (hi(11, V2), hi(-1, V2), hi(zi, V2) - 0.02), bevel=0.01)
    for x in (-12, 12):                                    # through girders (parapets) with flanges, rivets, stiffeners
        o = 1 if x > 0 else -1
        sh.box2("ochre", (lo(x, V2) + 0.04, lo(-4, V2), Z0), (hi(x, V2) - 0.04, hi(4, V2), Z1), bevel=0.01)
        for y in (-4, 4):
            sh.box2("ochre", (lo(x - 1, V2), lo(y, V2), Z0), (hi(x + 1, V2), hi(y, V2), Z1), bevel=0.012)
        for zi in range(0, -L, -4):
            for y in (0, 3):
                de.sphere("chrome", 0.025, (c(x, V2) + o * (V2 / 2 - 0.03), c(y, V2), c(zi, V2)), (1, 1, 1), 6, 4)
                de.sphere("chrome", 0.025, (c(x, V2) - o * (V2 / 2 - 0.03), c(y, V2), c(zi, V2)), (1, 1, 1), 6, 4)
        for zi in range(-3, -L, -9):
            de.box2("rust", (c(x + o, V2) - 0.06, lo(-3, V2), lo(zi, V2) + 0.03), (c(x + o, V2) + 0.06, hi(3, V2), hi(zi, V2) - 0.03), bevel=0.006)
    for z0 in (0, -L + 3):                                 # abutments
        cc.box2("conc", (lo(-13, V2), lo(-12, V2), lo(z0 - 3, V2)), (hi(13, V2), hi(-5, V2), hi(z0, V2)), bevel=0.03)
    for pz in (-25, -50):                                  # steel trestles
        for x in (-9, 9):
            sh.box2("steel_dark", (lo(x - 1, V2), lo(-25, V2), lo(pz - 1, V2)), (hi(x, V2), hi(-5, V2), hi(pz, V2)), bevel=0.012)
        sh.box2("ochre", (lo(-11, V2), lo(-5, V2), lo(pz - 1, V2)), (hi(11, V2), hi(-4, V2), hi(pz, V2)), bevel=0.012)
        for (x0, x1) in ((-9, 9), (9, -9)):
            de.beam("rust", (c(x0, V2), c(-24, V2), c(pz, V2)), (c(x1, V2), c(-7, V2), c(pz, V2)), 0.1, 0.06)
        cc.box2("conc", (lo(-11, V2), lo(-26, V2), lo(pz - 2, V2)), (hi(11, V2), hi(-25, V2), hi(pz + 1, V2)), bevel=0.02)
    return a


def forge():
    a = Asset("forge", "Industry")
    st, w, fe, gl = a.p("Stone"), a.p("Wood"), a.p("Iron"), a.p("Glow", "Stone")
    holes = []
    st.box2("brick_fired", (lo(-13), lo(0), lo(-5)), (hi(-1), lo(8), hi(5)), bevel=0.012)            # hearth
    for (x0, x1, z0, z1) in ((lo(-13), lo(-10), lo(-5), hi(5)), (hi(-4), hi(-1), lo(-5), hi(5)), (lo(-10), hi(-4), lo(-5), lo(-3)), (lo(-10), hi(-4), hi(3), hi(5))):
        st.box2("brick_fired", (x0, lo(8), z0), (x1, hi(9), z1), bevel=0.01)
    gl.box2("ember", (lo(-10), lo(8), lo(-3)), (hi(-4), lo(8) + 0.05, hi(3)), bevel=0)
    r = K.rng(4302)
    for k in range(16):
        st.rock("black" if r.random() < 0.6 else "ember", r.uniform(0.03, 0.05), (r.uniform(c(-10), c(-4)), lo(8) + 0.05, r.uniform(c(-3), c(3))), seed=k, squash=0.7, detail=1)
    for x in (-13, -1):                                    # hood posts + hood + chimney
        st.box2("brick_fired", (lo(x), lo(10), lo(-5)), (hi(x), hi(17), hi(-3)), bevel=0.01)
    st.add(K.kit.bm_loft([[(lo(-13), lo(18), lo(-5)), (hi(-1), lo(18), lo(-5)), (hi(-1), lo(18), hi(3)), (lo(-13), lo(18), hi(3))],
                          [(lo(-9), hi(22), lo(-5)), (hi(-5), hi(22), lo(-5)), (hi(-5), hi(22), hi(-1)), (lo(-9), hi(22), hi(-1))]]), "brick_dark")
    st.box2("brick_fired", (lo(-9), lo(23), lo(-4)), (hi(-5), hi(36), hi(-1)), bevel=0.012)
    st.box2("black", (lo(-9) - 0.01, lo(37), lo(-4) - 0.01), (hi(-5) + 0.01, hi(37), hi(-1) + 0.01), bevel=0.008)
    # bellows on a stand behind the hearth, lever
    w.box2(K.wood("", "x"), (lo(-12), lo(3), lo(-9)), (hi(-6), lo(3) + 0.04, hi(-6)), bevel=0.006)
    w.box2(K.wood("", "x"), (lo(-12), hi(7) - 0.04, lo(-9)), (hi(-6), hi(7), hi(-6)), bevel=0.006)
    w.add(K.kit.bm_loft([[(lo(-12), lo(3) + 0.04, lo(-9) + 0.02), (hi(-6), lo(3) + 0.04, lo(-9) + 0.02), (hi(-6), lo(3) + 0.04, hi(-6) - 0.02), (lo(-12), lo(3) + 0.04, hi(-6) - 0.02)],
                         [(lo(-12) + 0.06, c(5), lo(-9) - 0.01), (hi(-6) - 0.02, c(5), lo(-9) + 0.04), (hi(-6) - 0.02, c(5), hi(-6) - 0.04), (lo(-12) + 0.06, c(5), hi(-6) + 0.01)],
                         [(lo(-12), hi(7) - 0.04, lo(-9) + 0.02), (hi(-6), hi(7) - 0.04, lo(-9) + 0.02), (hi(-6), hi(7) - 0.04, hi(-6) - 0.02), (lo(-12), hi(7) - 0.04, hi(-6) - 0.02)]]), "leather")
    w.box2(K.wood("dark", "y"), (lo(-9), lo(8), lo(-9)), (hi(-9), hi(12), hi(-9)), bevel=0.008)
    w.box2(K.wood("dark", "z"), (lo(-9), lo(12), lo(-12)), (hi(-9), hi(12), hi(-9)), bevel=0.008)
    fe.rod("black", (c(-7.5), c(5), c(-6)), (c(-7.5), c(5), lo(-5)), 0.02, 8)
    # oak stump + anvil + hot billet + tongs + hammer
    w.lathe(K.wood("", "y"), [(0.0, lo(0)), (0.3, lo(0)), (0.27, 0.1), (0.25, hi(6)), (0.0, hi(6))], (c(7), 0, 0), "y", 16)
    w.cyl(K.wood("light", "y"), 0.245, 0.006, (c(7), hi(6), 0), "y", 16, 0)
    fe.box2("black", (lo(5), lo(7), lo(-2)), (hi(9), hi(7), hi(2)), bevel=0.012)
    fe.box2("black", (lo(6), lo(8), lo(-1)), (hi(8), hi(8), hi(1)), bevel=0.01)
    fe.box2("steel_dark", (lo(3), lo(9), lo(-2)), (hi(11), hi(10), hi(2)), bevel=0.014)
    fe.add(K.kit.bm_cyl(0.11, 0.24, 12, 0.0, 0.01), "steel_dark", Matrix.Translation((hi(11) + 0.12, c(9.6), 0)) @ K._axis_mtx("x"))   # horn
    fe.box2("steel_dark", (lo(2), c(9.6), lo(-1)), (lo(3), hi(10), hi(1)), bevel=0.006)
    gl.box2("lamp_amber", (lo(5), lo(11), -0.025), (hi(7), lo(11) + 0.04, 0.025), bevel=0.006)
    fe.rod("steel_dark", (c(8), lo(11) + 0.02, c(-1)), (c(12), lo(11) + 0.02, c(-1)), 0.008, 5)
    fe.rod("steel_dark", (c(8), lo(11) + 0.02, c(-0.6)), (c(12), lo(11) + 0.02, c(0)), 0.008, 5)
    fe.box2("steel", (lo(9), lo(11), lo(1)), (hi(10), hi(12), hi(1)), bevel=0.01)
    w.rod(K.wood("", "z"), (c(10), lo(11) + 0.03, c(1)), (c(10), lo(11) + 0.03, c(4)), 0.014, 6)
    w.lathe(K.wood("dark", "y"), [(0.0, lo(0)), (0.17, lo(0)), (0.2, hi(5)), (0.19, hi(5)), (0.15, lo(0) + 0.02), (0.0, lo(0) + 0.02)], (c(12), 0, c(6)), "y", 16, close=False)
    w.cyl("water", 0.17, 0.01, (c(12), hi(4) - 0.02, c(6)), "y", 16, 0)
    for y in (1, 4):
        fe.torus("steel_dark", 0.18 + y * 0.004, 0.008, (c(12), c(y), c(6)), "y", 16, 4)
    return a


def machine_shop():
    a = Asset("machine_shop", "Industry")
    fe, w = a.p("Iron"), a.p("Wood")
    P = "green"
    fe.box2("steel_dark", (lo(-16), lo(0), lo(-6)), (hi(15), hi(0), hi(6)), bevel=0.008)
    for k in range(16):
        x0 = lo(-16) + k * (hi(15) - lo(-16)) / 16
        for (z0, z1) in ((lo(-6), lo(-6) + 0.06), (hi(6) - 0.06, hi(6))):
            fe.box2("hazard" if k % 2 == 0 else "black", (x0, hi(0) - 0.002, z0), (x0 + (hi(15) - lo(-16)) / 16, hi(0) + 0.004, z1), bevel=0)
    # lathe
    for (x0, x1) in ((-15, -12), (-4, -1)):
        fe.box2(P, (lo(x0), lo(1), lo(-2)), (hi(x1), hi(7), hi(2)), bevel=0.012)
    fe.box2("steel", (lo(-15), lo(8), lo(-2)), (hi(-1), hi(9), hi(1)), bevel=0.01)
    fe.box2(P, (lo(-15), lo(10), lo(-3)), (hi(-11), hi(15), hi(2)), bevel=0.02)
    fe.cyl("chrome", 2.4 * VS, 2 * VS, (c(-9.5), c(13), 0), "x", 20, 0.01)
    fe.cyl("chrome", 0.04, 5 * VS, (c(-6), c(13), 0), "x", 12, 0.002)
    fe.box2(P, (lo(-3), lo(10), lo(-2)), (hi(-1), hi(14), hi(1)), bevel=0.015)
    fe.add(K.kit.bm_cyl(0.04, 0.08, 10, 0.0, 0.0), "chrome", Matrix.Translation((c(-3.5), c(13), 0)) @ K._axis_mtx("x"))
    fe.box2("steel_dark", (lo(-8), lo(10), lo(-1)), (hi(-6), hi(10), hi(3)), bevel=0.008)
    fe.box2("chrome", (lo(-7), lo(11), lo(1)), (hi(-6), hi(11), hi(2)), bevel=0.004)
    for (x, y) in ((-12, 12), (-7, 9)):
        fe.torus("chrome", 1.4 * VS, 0.008, (c(x), c(y), hi(3) - 0.02), "z", 16, 4)
        fe.rod("chrome", (c(x), c(y), hi(2)), (c(x), c(y), hi(3) - 0.02), 0.01, 6)
    fe.cyl("lamp_red", 0.025, 0.02, (c(-13.5), c(14), hi(2) + 0.01), "z", 10, 0.003)
    # knee mill
    fe.box2(P, (lo(6), lo(1), lo(-5)), (hi(10), hi(24), hi(-2)), bevel=0.02)
    fe.box2(P, (lo(6), lo(1), lo(-1)), (hi(10), hi(10), hi(3)), bevel=0.02)
    fe.box2("steel", (lo(3), lo(11), lo(-1)), (hi(13), hi(12), hi(4)), bevel=0.01)
    for z in range(-1, 5, 2):
        fe.box2("black", (lo(3) + 0.02, hi(12) - 0.004, c(z) - 0.012), (hi(13) - 0.02, hi(12) + 0.002, c(z) + 0.012), bevel=0)
    fe.box2(P, (lo(5), lo(18), lo(-2)), (hi(11), hi(24), hi(3)), bevel=0.025)
    fe.cyl("chrome", 1.1 * VS, hi(17) - lo(14), (c(8), (lo(14) + hi(17)) / 2, c(1.5)), "y", 12, 0.004)
    fe.cyl("chrome", 0.025, 0.08, (c(8), c(13.4), c(1)), "y", 8, 0.0)
    fe.box2("black", (lo(6), lo(12), lo(1)), (hi(7), hi(13), hi(2)), bevel=0.008)
    fe.torus("chrome", 1.4 * VS, 0.008, (hi(14) - 0.02, c(11), c(1)), "x", 16, 4)
    fe.box2("black", (lo(9), lo(25), lo(-4)), (hi(11), hi(27), hi(-2)), bevel=0.02)
    # tool board + grinder
    w.box2("pegboard", (lo(-10), lo(17), lo(-6)), (hi(3), hi(26), hi(-6)), bevel=0.006)
    for x in (-10, 3):
        w.box2(K.wood("dark", "y"), (lo(x), lo(1), lo(-6)), (hi(x), hi(16), hi(-6)), bevel=0.006)
    for x in range(-8, 2, 3):
        fe.box2("chrome", (c(x) - 0.01, c(19), hi(-6)), (c(x) + 0.01, c(23), lo(-5) + 0.01), bevel=0.003)
    fe.box2("steel_dark", (lo(1), lo(1), lo(-4)), (hi(3), hi(8), hi(-2)), bevel=0.01)
    fe.box2("green", (lo(1), lo(9), lo(-4)), (hi(3), hi(10), hi(-2)), bevel=0.01)
    for x in (0, 4):
        fe.cyl("steel", 1.6 * VS + 0.01, VS * 0.8, (c(x), c(10), c(-3)), "x", 16, 0.006)
    return a


TILES = [("furn_roads_signs", "Stop, speed limit and direction signs, guard rail, curb, bollard", [lambda: [sign_stop(), sign_speed(), sign_direction(), guard_rail(), curb(), bollard()]]),
         ("furn_rock_crusher", "Rock crusher (jaw, hopper, flywheels, chute, gravel heap)", [lambda: [rock_crusher()]]),
         ("furn_bridges", "Timber trestle bridge (8 m), steel girder bridge (12 m)", [lambda: [bridge_timber(), bridge_steel()]]),
         ("furn_metal", "Forge and anvil, machine shop (lathe, knee mill, grinder)", [lambda: [forge(), machine_shop()]])]

if __name__ == "__main__":
    run(TILES, "roads_metal_")
