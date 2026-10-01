"""HD defence pieces (FurnitureLibrary.Defence.cs, .Base.cs): sandbag wall, watchtower, landmine, tripwire, gate frame,
motorised gate (drive + Leaf mover), MG nest (+ Head gun at (0, 0.86, 0.24)), saw bench, sawmill, brick mould, spike
wall, barbed wire, auto turret (+ Head at (0, 0.92, 0)), alarm bell, claim flag.

blender -b -P defence.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fcommon import K, Asset, Vector, Matrix, lo, hi, c, W, knob, screws, leg_square, drawer_front, bulb, run, VS  # noqa: E402


def bag(p, x0, x1, y0, z0, z1, m="sandbag", seed=0):
    """A burlap sandbag between voxel x0..x1, 2 voxels high from y0, z0..z1: rounded pillow with tied ends."""
    L, Wd, H = hi(x1) - lo(x0), hi(z1) - lo(z0), 2 * VS
    cx, cz = (hi(x1) + lo(x0)) / 2, (hi(z1) + lo(z0)) / 2
    yc = lo(y0) + H / 2
    stations = [(lo(x0) + 0.01, cz, yc, Wd * 0.3, H * 0.3), (lo(x0) + 0.06, cz, yc, Wd * 0.48, H * 0.47),
                (cx, cz, yc + 0.006, Wd * 0.5, H * 0.5), (hi(x1) - 0.06, cz, yc, Wd * 0.48, H * 0.47), (hi(x1) - 0.01, cz, yc, Wd * 0.3, H * 0.3)]
    p.superloft(m, [(t, c1, c2, h1, h2) for (t, c1, c2, h1, h2) in stations], 12, 2.6, axis="x")


def sandbag_wall():
    a = Asset("sandbag_wall", "Defence")
    s = a.p("Sand")
    for course in range(8):
        y0, off = course * 2, (course & 1) * 4
        for z0 in ((-4, 0) if course < 4 else (-2,)):
            x0 = -12 - off
            while x0 <= 12:
                bag(s, max(-12, x0), min(12, x0 + 7), y0, z0, z0 + 3, "sandbag" if (course + x0) % 3 else "burlap")
                x0 += 8
    return a


def watchtower():
    a = Asset("watchtower", "Defence", kind="building")
    w, pl, s, fe, g = a.p("Wood"), a.p("Plank"), a.p("Scrap"), a.p("Iron"), a.p("Glass")
    F, T = 55, 86
    for x in (-14, 14):
        for z in (-14, 14):
            w.box2(W("dark", "y"), (lo(x - 1), lo(0), lo(z - 1)), (hi(x + 1), hi(T - 1), hi(z + 1)), bevel=0.02)
    for bay in range(2):
        y0, y1 = 3 + bay * 26, 3 + bay * 26 + 24
        for s_ in (-14, 14):
            for (p0, p1) in (((-13, y0, s_), (13, y1, s_)), ((13, y0, s_), (-13, y1, s_)), ((s_, y0, -13), (s_, y1, 13)), ((s_, y0, 13), (s_, y1, -13))):
                w.beam(W("", "y"), tuple(c(v) for v in p0), tuple(c(v) for v in p1), 0.1, 0.06)
    for z in range(-16, 17, 4):
        w.box2(W("dark", "x"), (lo(-16), lo(F - 1), lo(z)), (hi(16), hi(F - 1), hi(z)), bevel=0.008)
    for k in range(8):                                     # deck planks along z
        x0 = lo(-16) + k * (hi(16) - lo(-16)) / 8
        pl.box2(K.WOODS["z"][k % 3], (x0 + 0.004, lo(F), lo(-16)), (x0 + (hi(16) - lo(-16)) / 8 - 0.004, hi(F), hi(16)), bevel=0.008)
    # parapet boards (horizontal), rail uprights and top rail; ladder gap at the front
    for (x0, x1, z0, z1, ax) in ((lo(-16), hi(16), lo(-16), hi(-16), "x"), (lo(-16), hi(-16), lo(-15), hi(15), "z"), (lo(16), hi(16), lo(-15), hi(15), "z"),
                                 (lo(-16), lo(-4), lo(16), hi(16), "x"), (hi(4), hi(16), lo(16), hi(16), "x")):
        for k in range(3):
            y0 = lo(F + 1) + k * (hi(F + 8) - lo(F + 1)) / 3
            pl.box2(K.WOODS[ax][k % 3], (x0, y0 + 0.004, z0), (x1, y0 + (hi(F + 8) - lo(F + 1)) / 3, z1), bevel=0.008)
        w.box2(W("", ax), (x0, lo(F + 13), z0), (x1, hi(F + 13), z1), bevel=0.01)
    for x in range(-16, 17, 8):
        for z in (-16, 16):
            if z == 16 and abs(x) < 5:
                continue
            w.box2(W("", "y"), (lo(x), lo(F + 9), lo(z)), (hi(x), hi(F + 12), hi(z)), bevel=0.008)
            w.box2(W("", "y"), (lo(z), lo(F + 9), lo(x)), (hi(z), hi(F + 12), hi(x)), bevel=0.008)
    for i in range(6):                                     # gable tin roof
        x0 = lo(-18) + i * (hi(18) - lo(-18)) / 6 - 0.01
        x1 = x0 + (hi(18) - lo(-18)) / 6 + 0.02
        for sz in (-1, 1):
            ya, yb = c(T), c(T + 6)
            za, zb = sz * hi(18), 0.0
            ang = math.atan2(yb - ya, zb - za)
            Lr = math.hypot(zb - za, yb - ya)
            mt = Matrix.Translation((0, (ya + yb) / 2, (za + zb) / 2)) @ Matrix.Rotation(-ang, 4, "X")
            K.corrugated(s, "tin_rust" if (i + sz) % 3 else "tin", x0, x1, -Lr / 2, Lr / 2, 0.018, 0.1, 0.004, mt, samples_per_period=4)
    for x in (-4, 4):                                      # ladder
        w.box2(W("dark", "y"), (lo(x), lo(0), lo(17)), (hi(x), hi(F + 12), hi(17)), bevel=0.01)
    for y in range(3, F + 1, 4):
        w.cyl(W("light", "x"), 0.022, 0.56, (0, c(y), c(17)), "x", 8, 0.002)
    fe.box2("steel", (lo(11), lo(F + 14), lo(15)), (hi(13), hi(F + 16), hi(17)), bevel=0.02)            # searchlight
    fe.cyl("steel_dark", 0.02, 0.1, (c(12), lo(F + 14) - 0.04, c(16)), "y", 8, 0.0)
    g.cyl("lamp", 0.1, 0.02, (c(12), c(F + 15), hi(17) + 0.01), "z", 16, 0.004)
    w.box2("rig_green", (lo(-14), lo(F + 1), lo(-14)), (hi(-10), hi(F + 4), hi(-11)), bevel=0.015)        # ammo crate
    w.box2("cloth_cream", (lo(-14) - 0.004, c(F + 2.5) - 0.03, lo(-14) - 0.004), (hi(-10) + 0.004, c(F + 2.5) + 0.03, hi(-11) + 0.004), bevel=0)
    return a


def landmine():
    a = Asset("landmine", "Defence")
    fe = a.p("Iron")
    fe.lathe("rig_green", [(0.0, lo(0)), (3.5 * VS, lo(0)), (3.6 * VS, lo(0) + 0.03), (3.2 * VS, hi(0)), (0.0, hi(0))], (0, 0, 0), "y", 24)
    fe.lathe("steel_dark", [(0.0, hi(0)), (1.8 * VS, hi(0)), (1.6 * VS, hi(1)), (0.0, hi(1))], (0, 0, 0), "y", 18)
    fe.cyl("steel", 0.006, hi(4) - hi(1), (0, (hi(1) + hi(4)) / 2, 0), "y", 6, 0.0)
    fe.box2("cloth_red", (c(2) - 0.01, hi(0), -0.02), (c(2) + 0.06, hi(1), 0.02), bevel=0.003)
    r = K.rng(3795)
    for k in range(10):
        an = r.uniform(0, math.tau)
        fe.rock("soil_dry", 0.03, (math.cos(an) * 0.22, hi(0) - 0.02, math.sin(an) * 0.22), seed=k, squash=0.5, detail=1)
    return a


def tripwire():
    a = Asset("tripwire", "Defence")
    w, fe = a.p("Wood"), a.p("Iron")
    for x in (-12, 12):
        w.add(K.kit.bm_cyl(0.035, hi(4) - lo(0), 6, 0.004, 0.012), W("dark", "y"), Matrix.Translation((c(x), (hi(4) + lo(0)) / 2, 0)) @ K._axis_mtx("y"))
    fe.rod("chrome", (c(-12), c(2), 0), (c(11), c(2), 0), 0.0025, 4)
    fe.cyl("crimson", 0.9 * VS + 0.01, hi(6) - lo(3), (c(12), (lo(3) + hi(6)) / 2, c(1)), "y", 12, 0.004)
    fe.cyl("ochre", 0.9 * VS + 0.012, VS, (c(12), c(7), c(1)), "y", 12, 0.004)
    fe.torus("chrome", 0.02, 0.004, (c(11), c(3), 0), "z", 10, 4)
    for y in (3.5, 5.5):
        fe.torus("steel_dark", 0.9 * VS + 0.015, 0.005, (c(12), c(y), c(0.5)), "y", 12, 4)
    return a


def gate_frame():
    a = Asset("gate_frame", "Structure")
    cc, fe, g = a.p("Concrete"), a.p("Iron"), a.p("Glass")
    for sx in (-1, 1):
        x0, x1 = (lo(-28), hi(-25)) if sx < 0 else (lo(25), hi(28))
        cc.box2("conc", (x0, lo(0), lo(-2)), (x1, hi(35), hi(2)), bevel=0.02)
        cc.box2("conc_light", (x0 - 0.04, lo(36), lo(-3)), (x1 + 0.04, hi(37), hi(3)), bevel=0.02)
        xi = hi(-25) if sx < 0 else lo(25)
        for k in range(8):
            fe.box2("hazard" if k % 2 == 0 else "black", (xi - 0.004 if sx > 0 else xi - 0.006, lo(2) + k * 0.24, lo(-2)), (xi + 0.006 if sx < 0 else xi + 0.004, lo(2) + (k + 1) * 0.24, hi(2)), bevel=0)
    fe.box2("steel", (lo(-25), lo(20), lo(3)), (hi(-25), hi(26), hi(6)), bevel=0.01)
    fe.cyl("chrome", 0.8 * VS + 0.01, hi(25) - lo(21), (c(-24), (lo(21) + hi(25)) / 2, c(5.5)), "y", 12, 0.004)
    g.lathe("lamp_amber", [(0.0, 0.0), (0.08, 0.0), (0.08, 0.04), (0.05, 0.14), (0.0, 0.16)], (c(26.5), hi(37), c(0.5)), "y", 14)
    return a


def motorised_gate():
    a = Asset("motorised_gate", "Defence")
    cc, fe = a.p("Concrete"), a.p("Iron")
    cc.box2("conc", (lo(28), lo(0), lo(5)), (hi(35), hi(0), hi(10)), bevel=0.012)
    fe.box2("rig_green", (lo(29), lo(1), lo(6)), (hi(34), hi(7), hi(9)), bevel=0.025)
    for k in range(6):
        fe.box2("hazard" if k % 2 == 0 else "black", (lo(29) + k * 0.08, lo(1), hi(9) - 0.004), (lo(29) + (k + 1) * 0.08, hi(2), hi(9) + 0.004), bevel=0)
    fe.cyl("lamp_red", 0.025, 0.02, (c(31), c(5), hi(9) + 0.01), "z", 10, 0.003)
    fe.cyl("lamp_green", 0.025, 0.02, (c(33), c(5), hi(9) + 0.01), "z", 10, 0.003)
    fe.box2("steel_dark", (lo(-25), lo(0), lo(3)), (hi(76), hi(0), hi(4)), bevel=0.006)                    # ground rail
    for x in range(-24, 77, 8):
        fe.box2("chrome", (c(x) - 0.02, hi(0), lo(3) + 0.03), (c(x) + 0.02, hi(0) + 0.006, hi(4) - 0.03), bevel=0)
    # the leaf (mover: MotorGate slides it along +x)
    lf = a.p("Leaf", "Iron", mover=True)
    for k in range(16):
        lf.box2("hazard" if k % 2 == 0 else "black", (lo(-24) + k * (hi(24) - lo(-24)) / 16, lo(1), lo(3)), (lo(-24) + (k + 1) * (hi(24) - lo(-24)) / 16, hi(3), hi(4)), bevel=0.004)
    lf.box2("steel", (lo(-24), lo(22), lo(3)), (hi(24), hi(23), hi(4)), bevel=0.01)
    for x in range(-21, 22, 3):
        lf.cyl("steel_dark", 0.025, hi(21) - lo(4), (c(x), (lo(4) + hi(21)) / 2, c(3)), "y", 8, 0.002)
    for x in (-24, 24):
        lf.box2("steel", (lo(x), lo(4), lo(3)), (hi(x), hi(21), hi(4)), bevel=0.01)
    lf.beam("steel_dark", (c(-23), c(4), c(4)), (c(23), c(21), c(4)), 0.05, 0.04)
    for x in range(-23, 24, 3):
        lf.add(K.kit.bm_cyl(0.025, 0.14, 4, 0.0, 0.0), "chrome", Matrix.Translation((c(x), hi(23) + 0.07, c(3))) @ K._axis_mtx("y"))
    for x in (-18, 18):
        lf.cyl("black", 0.05, 0.06, (c(x), lo(1) + 0.02, c(3.5)), "z", 12, 0.004)
    return a


def mg_nest():
    a = Asset("mg_nest", "Defence")
    sd, fe, w = a.p("Sand"), a.p("Iron"), a.p("Wood")
    for course in range(6):                                # horseshoe of bags, open at the back
        y0 = course * 2
        rr = 12.8 if course < 4 else 12.0
        n = 13 if course < 4 else 12
        for k in range(n):
            a0 = -125 + (k + (0.5 if course % 2 else 0)) * 250 / n
            if a0 > 125:
                continue
            an = math.radians(a0)
            cx, cz = math.sin(an) * rr * VS, math.cos(an) * rr * VS
            L = rr * VS * math.radians(250 / n) * 1.04
            Wd = (4.4 if course < 4 else 3.0) * VS
            st = [(-L / 2 + 0.01, 0, 0.0, Wd * 0.3, VS * 0.6), (-L / 2 + 0.05, 0, 0.0, Wd * 0.48, VS * 0.95), (0.0, 0, 0.006, Wd * 0.5, VS),
                  (L / 2 - 0.05, 0, 0.0, Wd * 0.48, VS * 0.95), (L / 2 - 0.01, 0, 0.0, Wd * 0.3, VS * 0.6)]
            bm = K.kit.bm_loft([[(t, c2 + h2 * math.copysign(abs(math.sin(q / 8 * math.tau)) ** (2 / 2.6), math.sin(q / 8 * math.tau)), c1 + h1 * math.copysign(abs(math.cos(q / 8 * math.tau)) ** (2 / 2.6), math.cos(q / 8 * math.tau)))
                                  for q in range(8)] for (t, c1, c2, h1, h2) in st], True)
            sd.add(bm, "sandbag" if (k + course) % 3 else "burlap", Matrix.Translation((cx, lo(y0) + VS, cz)) @ Matrix.Rotation(an + math.pi / 2, 4, "Y"))
    fe.cyl("steel", 0.03, hi(9) - lo(0), (0, (lo(0) + hi(9)) / 2, c(3)), "y", 10, 0.003)
    for foot in ((-4, 0, 0), (4, 0, 0), (0, 0, 7)):
        fe.rod("steel_dark", tuple(c(v) for v in foot), (0, c(6), c(3)), 0.016, 6)
    w.box2("rig_green", (lo(-3), lo(0), lo(-9)), (hi(3), hi(5), hi(-6)), bevel=0.02)
    w.box2("cloth_cream", (lo(-3) - 0.004, c(2.5) - 0.03, lo(-9) - 0.004), (hi(3) + 0.004, c(2.5) + 0.03, hi(-6) + 0.004), bevel=0)
    for (x0, x1, z0, z1) in ((8, 10, -6, -5), (6, 8, -9, -8)):
        fe.box2("rig_green", (lo(x0), lo(0), lo(z0)), (hi(x1), hi(2), hi(z1)), bevel=0.012)
        fe.box2("steel_dark", (lo(x0) + 0.03, hi(2), c(z0 + 0.5) - 0.01), (hi(x1) - 0.03, hi(2) + 0.02, c(z0 + 0.5) + 0.01), bevel=0.003)
    # the gun on its cradle (AutoTurret head at (0, 0.86, 0.24))
    piv = (0.0, 0.86, 0.24)
    hd = a.p("Head", "Iron", pivot=piv, mover=True)
    P = lambda x, y, z: (piv[0] + c(x), piv[1] + c(y), piv[2] + c(z))
    hd.box2("black", P(-1.4, -0.4, -4.4), P(1.4, 2.4, 3.4), bevel=0.012)
    hd.cyl("black", 1.0 * VS + 0.01, c(13), P(0, 1, 10), "z", 14, 0.004)
    for k in range(6):
        hd.torus("steel_dark", 1.0 * VS + 0.012, 0.004, P(0, 1, 5 + k * 2), "z", 12, 3)
    hd.cyl("black", 0.03, c(2.5), P(0, 1, 17.6), "z", 10, 0.003)
    hd.box2("rig_green", P(-3.4, -1.4, -1.4), P(-1.6, 1.4, 1.4), bevel=0.01)
    hd.box2("steel_dark", P(-1, -2.4, -1), P(1, -0.6, 1), bevel=0.008)
    for x in (-2, 2):
        hd.rod("steel_dark", P(x, 0.5, -5), P(x, 0.5, -7), 0.012, 6)
    hd.box2("chrome", P(-0.2, 2.4, -1.2), P(0.2, 3.2, -0.8), bevel=0.003)
    return a


def auto_turret():
    a = Asset("auto_turret", "Defence")
    fe = a.p("Iron")
    for k in range(3):
        an = k * math.tau / 3
        fe.rod("steel_dark", (math.sin(an) * c(8), lo(0) + 0.03, math.cos(an) * c(8)), (0, c(9), 0), 0.028, 8)
        fe.cyl("steel_dark", 0.06, 0.03, (math.sin(an) * c(8), lo(0) + 0.015, math.cos(an) * c(8)), "y", 12, 0.004)
    fe.cyl("steel", 2.8 * VS, hi(11) - lo(7), (0, (lo(7) + hi(11)) / 2, 0), "y", 20, 0.012)
    fe.box2("rig_green", (lo(-3), lo(0), lo(-3)), (hi(3), hi(3), hi(3)), bevel=0.025)
    fe.cyl("lamp_amber", 0.025, 0.02, (0, c(2), hi(3) + 0.008), "z", 10, 0.003)
    fe.tube("black", [(c(3), c(1), 0), (c(3.5), c(5), 0), (c(1), c(8), 0)], 0.012, 6)
    piv = (0.0, 0.92, 0.0)                                # head (yaw/pitch by AutoTurret)
    hd = a.p("Head", "Iron", pivot=piv, mover=True)
    P = lambda x, y, z: (piv[0] + c(x), piv[1] + c(y), piv[2] + c(z))
    hd.box2("rig_green", P(-3.4, -0.4, -4.4), P(3.4, 5.4, 4.4), bevel=0.03)
    for x in (-2, 2):
        hd.cyl("black", 0.8 * VS + 0.01, c(11), P(x, 3, 10.5), "z", 12, 0.004)
        hd.cyl("black", 0.05, 0.06, P(x, 3, 16), "z", 12, 0.004)
    hd.box2("cloth_olive", P(3.6, -0.4, -3.4), P(6.4, 3.4, 2.4), bevel=0.015)
    hd.cyl("lamp_red", 0.04, 0.03, P(0, 5.5, 2.6), "z", 12, 0.006)
    hd.box2("black", P(-1.4, 4.6, 1.6), P(1.4, 6.4, 3.4), bevel=0.01)
    return a


def alarm_bell():
    a = Asset("alarm_bell", "Defence")
    w, cu, fe, cl = a.p("Wood"), a.p("Copper"), a.p("Iron"), a.p("Cloth")
    for x in (-6, 6):
        w.box2(W("dark", "y"), (lo(x), lo(0), lo(0)), (hi(x), hi(30), hi(0)), bevel=0.012)
        w.beam(W("", "y"), (c(x), lo(0), c(-3)), (c(x), c(6), 0), 0.05, 0.04)
        w.beam(W("", "y"), (c(x), lo(0), c(3)), (c(x), c(6), 0), 0.05, 0.04)
    w.box2(W("", "x"), (lo(-7), lo(31), lo(-1)), (hi(7), hi(32), hi(1)), bevel=0.015)
    cu.lathe("brass", [(0.0, hi(30)), (0.08, hi(30)), (0.12, c(28)), (0.16, c(25)), (0.24, c(23)), (0.3, lo(22) + 0.02), (0.31, lo(22)), (0.28, lo(22) + 0.01), (0.0, hi(29))],
             (0, 0, 0), "y", 28, close=False)
    cu.torus("bronze" if "bronze" in K.M else "copper", 0.3, 0.012, (0, lo(22) + 0.012, 0), "y", 28, 5)
    fe.box2("steel_dark", (lo(-1), hi(30), -0.03), (hi(1), lo(31), 0.03), bevel=0.004)
    fe.rod("steel_dark", (0, hi(29), 0), (0, c(21), 0), 0.008, 5)
    fe.sphere("steel_dark", 0.045, (0, c(20.5), 0), (1, 1, 1), 10, 6)
    cl.tube("rope", [(0, c(20), 0), (0.01, c(12), 0.01), (0, c(4), 0)], 0.012, 6)
    cl.sphere("rope", 0.025, (0, c(4), 0), (1, 1.4, 1), 8, 5)
    return a


def claim_flag():
    a = Asset("claim_flag", "Defence")
    st, fe, cl = a.p("Stone"), a.p("Iron"), a.p("Cloth")
    r = K.rng(1790)
    for k in range(7):                                      # cairn base
        an = k / 7 * math.tau
        st.rock(["stone", "rock", "rock_grey"][k % 3], 0.13, (math.cos(an) * 0.17, lo(0) + 0.08, math.sin(an) * 0.17), seed=k, squash=0.7, detail=1)
    st.rock("stone", 0.13, (0, lo(1) + 0.1, 0), seed=9, squash=0.8, detail=1)
    fe.cyl("steel", 0.022, hi(60) - lo(3), (0, (lo(3) + hi(60)) / 2, 0), "y", 10, 0.003)
    fe.sphere("chrome", 0.04, (0, c(61), 0), (1, 1, 1), 10, 6)
    n, m = 20, 15
    for j in range(m):                                      # waving flag with a skull (cells coloured per voxel rule)
        for i in range(n):
            x0, x1 = c(1 + i) - VS / 2, c(1 + i) + VS / 2
            y0, y1 = c(44 + j) - VS / 2, c(44 + j) + VS / 2
            z0 = math.sin((1 + i) * 0.45) * 1.2 * VS
            z1 = math.sin((2 + i) * 0.45) * 1.2 * VS
            cx, cy = (1 + i) - 11, (44 + j) - 51
            skull = (cx * cx + cy * cy * 2 < 16 and cy >= -1) or (abs(cx) <= 2 and -4 <= cy < -1 and (cx & 1) == 0)
            eye = cy == 1 and cx in (-2, 2)
            mm = "black" if eye else "cloth_cream" if skull else "cloth_red"
            cl.quad(mm, (x0, y0, z0), (x1, y0, z1), (x1, y1, z1), (x0, y1, z0), 0.008)
    return a


def spike_wall():
    a = Asset("spike_wall", "Defence")
    w, fe = a.p("Wood"), a.p("Iron")
    w.box2(W("dark", "x"), (lo(-12), lo(0), lo(-2)), (hi(12), hi(2), hi(1)), bevel=0.02)
    for x in range(-11, 12, 3):
        lean = 9 if (x & 1) == 0 else 7
        p0, p1 = (c(x), c(1), c(-1)), (c(x), c(14), c(lean - 1))
        w.add(K.kit.bm_cyl(0.06, (Vector(p1) - Vector(p0)).length, 7, 0.004, 0.05), W("", "y"),
              Matrix.Translation((Vector(p0) + Vector(p1)) / 2) @ (Vector(p1) - Vector(p0)).to_track_quat("Z", "Y").to_matrix().to_4x4())
        tip0, tip1 = Vector(p1), Vector((c(x), c(17), c(lean + 1)))
        fe.add(K.kit.bm_cyl(0.035, (tip1 - tip0).length, 6, 0.0, 0.002), "rust",
               Matrix.Translation((tip0 + tip1) / 2) @ (tip1 - tip0).to_track_quat("Z", "Y").to_matrix().to_4x4())
        fe.torus("steel_dark", 0.055, 0.008, (c(x), c(3), c(-0.6)), "y", 10, 4)
    return a


def barbed_wire():
    a = Asset("barbed_wire", "Defence")
    w, fe = a.p("Wood"), a.p("Iron")
    for x in (-12, 0, 12):
        w.add(K.kit.bm_cyl(0.04, hi(12) - lo(0), 7, 0.004, 0.035), W("dark", "y"), Matrix.Translation((c(x), (hi(12) + lo(0)) / 2, 0)) @ K._axis_mtx("y"))
    pts = []
    for i in range(0, 261, 2):                              # the concertina coil
        an = i * 0.42
        pts.append((-12 * VS + i * 24 * VS / 260, c(6) + 5 * VS * math.sin(an), 5 * VS * math.cos(an)))
    fe.tube("chrome", pts, 0.004, 4, caps=False)
    for i in range(0, 261, 7):                              # barbs
        an = i * 0.42
        q = Vector((-12 * VS + i * 24 * VS / 260, c(6) + 5 * VS * math.sin(an), 5 * VS * math.cos(an)))
        fe.rod("chrome", q - Vector((0.02, 0.015, 0)), q + Vector((0.02, 0.015, 0)), 0.003, 3)
    return a


def saw_bench():
    a = Asset("saw_bench", "Industry")
    w, pl, fe = a.p("Wood"), a.p("Plank"), a.p("Iron")
    for x in (-8, 8):
        for sz in (-1, 1):
            w.beam(W("dark", "y"), (c(x), lo(0), sz * c(4)), (c(x), c(9.5), 0), 0.06, 0.05)
        w.box2(W("dark", "z"), (lo(x), lo(4), lo(-2)), (hi(x), hi(4), hi(2)), bevel=0.006)
    w.box2(W("", "x"), (lo(-10), lo(10), lo(-2)), (hi(10), hi(10), hi(2)), bevel=0.01)
    R = 2.9 * VS
    for (x0, x1) in ((lo(-9), c(2) - 0.012), (c(2) + 0.012, hi(9))):                # the log, cut through at the kerf
        w.cyl("bark", R, x1 - x0, ((x0 + x1) / 2, c(13), 0), "x", 16, 0.01)
    for x in (lo(-9), hi(9)):
        w.cyl(W("light", "x"), R - 0.01, 0.004, (x, c(13), 0), "x", 16, 0)
    fe.box2("chrome", (c(2) - 0.003, c(12), lo(-4)), (c(2) + 0.003, c(12) + 0.08, hi(4)), bevel=0)
    fe.tube("crimson", [(c(2), c(12), c(-4)), (c(2), c(19), c(-2)), (c(2), c(19), c(2)), (c(2), c(12), c(4))], 0.018, 8)
    for k in range(4):                                      # sawn planks
        pl.box2(K.WOODS["z"][k % 3], (lo(11), lo(0) + k * 0.075, lo(-3)), (hi(17), lo(0) + (k + 1) * 0.075 - 0.004, hi(3)), bevel=0.006)
    r = K.rng(3746)
    for k in range(30):
        w.rock("wood_light_x", 0.015, (r.uniform(-0.6, 0.6), lo(0) + 0.003, r.uniform(-0.3, 0.3)), seed=k, squash=0.3, detail=1)
    return a


def sawmill():
    a = Asset("sawmill", "Industry")
    fe, w, pl = a.p("Iron"), a.p("Wood"), a.p("Plank")
    for x in (-16, -5, 5, 16):
        for z in (-4, 4):
            fe.box2("steel_dark", (lo(x), lo(0), lo(z)), (hi(x), hi(9), hi(z)), bevel=0.008)
    fe.box2("steel", (lo(-17), lo(10), lo(-5)), (hi(17), hi(10), hi(5)), bevel=0.012)
    for z in (-5, 5):
        fe.box2("chrome", (lo(-17), lo(11), lo(z) + 0.02), (hi(17), lo(11) + 0.03, hi(z) - 0.02), bevel=0.004)
    blade = []
    for k in range(48):                                    # toothed circular blade
        an = k / 48 * math.tau
        rr = 6.2 * VS if k % 2 == 0 else 5.6 * VS
        blade.append((math.cos(an) * rr, c(11) + math.sin(an) * rr))
    fe.prism("chrome", blade, -0.006, 0.006, plane="xy")
    fe.cyl("steel_dark", 1.5 * VS, 0.03, (0, c(11), 0), "z", 14, 0.004)
    fe.add(K.kit.bm_torus(7 * VS, 0.035, 24, 6), "hazard", Matrix.Translation((0, c(11), 0)) @ K._axis_mtx("z") @ Matrix.Diagonal((1, 1, 1.6, 1)))
    fe.box2("rig_green", (lo(-4), lo(2), lo(-3)), (hi(4), hi(7), hi(3)), bevel=0.03)
    fe.cyl("chrome", 1.2 * VS, VS, (c(3), c(5), c(4)), "z", 14, 0.004)
    fe.rod("black", (c(3), c(5), c(4)), (0, c(10), c(1)), 0.012, 6)
    fe.box2("black", (lo(16), lo(5), lo(5)), (hi(16), hi(7), hi(6)), bevel=0.01)
    fe.cyl("lamp_red", 0.02, 0.02, (c(16), c(6), hi(6) + 0.01), "z", 8, 0.002)
    w.cyl("bark", 2.9 * VS, hi(-8) - lo(-16), ((lo(-16) + hi(-8)) / 2, c(14), 0), "x", 16, 0.01)
    for x in (lo(-16), hi(-8)):
        w.cyl(W("light", "x"), 2.9 * VS - 0.01, 0.004, (x, c(14), 0), "x", 16, 0)
    for k in range(3):
        pl.box2(K.WOODS["x"][k % 3], (lo(9), lo(11) + k * 0.075, lo(-3)), (hi(16), lo(11) + (k + 1) * 0.075 - 0.004, hi(3)), bevel=0.006)
    r = K.rng(3758)
    for k in range(30):
        w.rock("wood_light_x", 0.015, (r.uniform(-0.4, 0.4), lo(0) + 0.003, r.uniform(-0.25, 0.25)), seed=k, squash=0.3, detail=1)
    return a


def brick_mould():
    a = Asset("brick_mould", "Industry")
    w, cl, fe = a.p("Wood"), a.p("Clay"), a.p("Iron")
    for x in (-8, 8):
        for z in (-4, 4):
            w.box2(W("dark", "y"), (lo(x), lo(0), lo(z)), (hi(x), hi(6), hi(z)), bevel=0.008)
    for k in range(4):
        z0 = lo(-5) + k * (hi(5) - lo(-5)) / 4
        w.box2(K.WOODS["x"][k % 3], (lo(-9), lo(7), z0 + 0.003), (hi(9), hi(7), z0 + (hi(5) - lo(-5)) / 4 - 0.003), bevel=0.006)
    w.box2(W("light", "x"), (lo(-7), lo(8), lo(-2)), (hi(-7) + 0.0, hi(9), hi(2)), bevel=0.006)
    w.box2(W("light", "x"), (lo(7), lo(8), lo(-2)), (hi(7), hi(9), hi(2)), bevel=0.006)
    for z in (-2, 2):
        w.box2(W("light", "x"), (lo(-7), lo(8), lo(z)), (hi(7), hi(9), hi(z)), bevel=0.006)
    for cx in (-4, 0, 4):
        w.box2(W("light", "z"), (c(cx + 2) - 0.02, lo(8), lo(-2)), (c(cx + 2) + 0.02, hi(9), hi(2)), bevel=0.004) if cx < 4 else None
        cl.box2("clay", (lo(cx - 1), lo(8), lo(-1)), (hi(cx + 1), hi(9) - 0.005, hi(1)), bevel=0.01)
    cl.rock("clay", 0.1, (c(-3.5), hi(7) + 0.04, c(3.5)), seed=3, squash=0.6, detail=1)
    fe.lathe("steel", [(0.0, lo(0)), (0.22, lo(0)), (0.25, hi(4)), (0.24, hi(4)), (0.2, lo(0) + 0.02), (0.0, lo(0) + 0.02)], (c(-13), 0, 0), "y", 18, close=False)
    cl.cyl("clay", 0.2, 0.02, (c(-13), hi(3), 0), "y", 18, 0)
    fe.lathe("steel", [(0.0, lo(0)), (0.11, lo(0)), (0.13, hi(3)), (0.12, hi(3)), (0.1, lo(0) + 0.01), (0.0, lo(0) + 0.01)], (c(13), 0, c(3)), "y", 14, close=False)
    fe.cyl("water", 0.12, 0.01, (c(13), hi(2), c(3)), "y", 14, 0)
    w.box2(K.wood("dark", "x"), (lo(11), lo(0), lo(-5)), (hi(17), hi(0), hi(-1)), bevel=0.006)
    for x in (11, 14):
        for z in (-5, -3):
            cl.box2("clay", (lo(x), hi(0), lo(z)), (hi(x + 1), hi(2), hi(z)), bevel=0.01)
    return a


TILES = [("furn_defence_walls", "Sandbag wall, spike wall, barbed wire, landmine, tripwire", [lambda: [sandbag_wall(), spike_wall(), barbed_wire(), landmine(), tripwire()]]),
         ("furn_defence_guns", "MG nest (+Head gun), auto turret (+Head), alarm bell, claim flag", [lambda: [mg_nest(), auto_turret(), alarm_bell(), claim_flag()]]),
         ("furn_gate", "Gate frame + motorised sliding gate (drive + Leaf)", [lambda: [gate_frame(), motorised_gate()]]),
         ("furn_watchtower", "Watchtower (plank platform, tin roof, ladder, searchlight)", [lambda: [watchtower()]]),
         ("furn_material_stations", "Saw bench, sawmill, brick mould", [lambda: [saw_bench(), sawmill(), brick_mould()]])]

if __name__ == "__main__":
    run(TILES, "defence_")
