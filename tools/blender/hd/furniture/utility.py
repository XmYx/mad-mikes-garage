"""HD utility and industry pieces of FurnitureLibrary.cs / BuildPieces.cs (power, water, stations; 0.08 m voxels).
Moving parts are separate objects pivoting where the game puts them (windmill Rotor at (0, 5.76, 0.3)).

blender -b -P utility.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fcommon import K, Asset, Vector, Matrix, lo, hi, c, W, knob, screws, leg_square, drawer_front, bulb, run, VS  # noqa: E402


def spool(gid, cable):
    """Pseudo pieces (cable / pipe tools): the spool shown in the build menu (Spool(): CylX r3, x -2..2)."""
    a = Asset(gid, "Utility")
    p = a.p("Copper" if cable else "Scrap")
    R = 3.5 * VS
    for x in (-2, 2):
        p.cyl(K.wood("", "x") if cable else "steel", R, VS, (c(x), c(3), 0), "x", 24, 0.006)
    p.cyl(K.wood("dark", "x"), 0.06, 0.32, (0, c(3), 0), "x", 12, 0.004)
    if cable:
        for k in range(5):
            p.torus("copper" if k % 2 else "black", 0.11 + k * 0.03, 0.016, (0, c(3), 0), "x", 24, 6)
        for k in range(-3, 4):
            p.torus("black", 0.21, 0.016, (k * 0.032, c(3), 0), "x", 24, 6)
    else:
        for k in range(-3, 4):
            p.torus("steel_dark", 0.18, 0.025, (k * 0.04, c(3), 0), "x", 24, 6)
        p.rod("steel_dark", (0.12, c(3) + 0.2, 0), (0.12, c(3) + 0.28, 0.1), 0.025, 8)
    return a


def generator():
    a = Asset("generator", "Utility")
    fe, s, cu = a.p("Iron"), a.p("Scrap"), a.p("Copper")
    for z in (-4, 4):                                     # skid rails
        fe.box2("steel_dark", (lo(-7), lo(0), lo(z) + 0.0), (hi(7), hi(1), hi(z)), bevel=0.008)
    for x in (-6, 0, 6):
        fe.box2("steel_dark", (c(x) - 0.04, lo(1), lo(-4)), (c(x) + 0.04, hi(1), hi(4)), bevel=0.006)
    fe.box2("gen", (lo(-6), lo(2), lo(-3)), (hi(3), hi(8), hi(3)), bevel=0.03, segs=3)       # engine block
    for k in range(5):                                    # cooling fins
        fe.box2("iron", (lo(-5) + k * 0.1, hi(8) - 0.01, lo(-2)), (lo(-5) + k * 0.1 + 0.035, hi(8) + 0.04, hi(2)), bevel=0.004)
    fe.cyl("steel", 0.2, hi(6) - lo(4), ((lo(4) + hi(6)) / 2, c(4.5), 0), "x", 24, 0.012)  # alternator
    for k in range(12):
        an = k / 12 * math.tau
        fe.box("steel_dark", (hi(6) - lo(4) - 0.04, 0.012, 0.02), ((lo(4) + hi(6)) / 2, c(4.5) + math.sin(an) * 0.205, math.cos(an) * 0.205), r=(math.degrees(an), 0, 0), bevel=0)
    fe.box2("black", (lo(4), lo(7), lo(-1)), (hi(4), hi(11), hi(-1)), bevel=0.01)          # control box on a post
    for k, m in enumerate(("hazard", "chrome", "lamp_red")):
        knob(fe, m, (c(4) - 0.04 + k * 0.04, c(10), hi(-1) + 0.006), 0.012, "z", 0.016)
    s.box2("pump_red", (lo(-5), lo(9), lo(-2)), (hi(-1), hi(11), hi(2)), bevel=0.04, segs=3)   # fuel tank
    s.cyl("black", 0.03, 0.03, (c(-3), hi(11) + 0.01, 0), "y", 12, 0.004)
    s.tube("rust", [(lo(-6), c(6), c(2)), (lo(-6) - 0.04, c(6.5), c(2.5)), (lo(-6) - 0.04, hi(11), c(2.5))], 0.025, 8)   # exhaust
    cu.box2("copper", (lo(-7), lo(4), lo(0)), (hi(-7), hi(5), hi(1)), bevel=0.01)
    cu.tube("black", [(c(-7), c(4.5), c(0.5)), (c(-7.4), c(3), c(1.5)), (c(-7), lo(1), c(3.5))], 0.012, 6)
    return a


def windmill():
    a = Asset("windmill", "Utility")
    fe, s = a.p("Iron"), a.p("Scrap")
    # lattice tower: four legs narrowing, girths every 10 voxels, X braces
    def w_at(y):
        return max(1, 6 - y // 14)
    legs = []
    for sx in (-1, 1):
        for sz in (-1, 1):
            pts = [(sx * c(w_at(y)), c(y), sz * c(w_at(y))) for y in (0, 14, 28, 42, 56, 70)]
            fe.tube("steel", pts, 0.022, 6)
    for y in range(0, 71, 10):
        w = c(w_at(y))
        for (p0, p1) in (((-w, -w), (w, -w)), ((w, -w), (w, w)), ((w, w), (-w, w)), ((-w, w), (-w, -w))):
            fe.rod("steel_dark", (p0[0], c(y), p0[1]), (p1[0], c(y), p1[1]), 0.012, 5)
    for y in range(0, 70, 10):
        w0, w1 = c(w_at(y)), c(w_at(y + 10))
        for side in range(4):
            if side == 0:
                A, B, C_, D = (-w0, -w0), (w1, -w1), (w0, -w0), (-w1, -w1)
            elif side == 1:
                A, B, C_, D = (w0, -w0), (w1, w1), (w0, w0), (w1, -w1)
            elif side == 2:
                A, B, C_, D = (w0, w0), (-w1, w1), (-w0, w0), (w1, w1)
            else:
                A, B, C_, D = (-w0, w0), (-w1, -w1), (-w0, -w0), (-w1, w1)
            fe.rod("steel_dark", (A[0], c(y), A[1]), (B[0], c(y + 10), B[1]), 0.008, 4)
            fe.rod("steel_dark", (C_[0], c(y), C_[1]), (D[0], c(y + 10), D[1]), 0.008, 4)
    for sx in (-1, 1):
        for sz in (-1, 1):
            fe.box2("conc", (sx * c(6) - 0.08, lo(0), sz * c(6) - 0.08), (sx * c(6) + 0.08, lo(0) + 0.06, sz * c(6) + 0.08), bevel=0.01)
    # nacelle + tail vane
    fe.superloft("white", [(lo(-3), 0, c(71.5), 0.12, 0.12), (lo(-3) + 0.06, 0, c(71.5), 0.18, 0.15), (hi(3) - 0.08, 0, c(71.5), 0.18, 0.15), (hi(3), 0, c(71.5), 0.1, 0.1)], 18, 2.4)
    fe.cyl("steel_dark", 0.06, 0.12, (0, lo(70) + 0.02, 0), "y", 12, 0.005)
    s.rod("steel", (0, c(72), lo(-3)), (0, c(72), lo(-4)), 0.015, 6)
    s.panel("rust", (0, lo(71), lo(-8)), (0, hi(73) + 0.04, lo(-8)), (0, hi(73), hi(-4)), 0.012)
    # rotor (spins about +z at (0, 5.76, 0.3)): hub + three blades
    piv = (0.0, 5.76, 0.3)
    r = a.p("Rotor", "Scrap", pivot=piv, mover=True)
    r.lathe("white", [(0.0, 0.0), (0.09, 0.0), (0.1, 0.06), (0.06, 0.14), (0.0, 0.17)], piv, "z", 18)
    r.cyl("steel_dark", 0.06, 0.08, (piv[0], piv[1], piv[2] - 0.04), "z", 12, 0.004)
    for b in range(3):
        an = b * math.tau / 3
        d = Vector((math.cos(an), math.sin(an), 0))
        side = Vector((-d.y, d.x, 0))
        secs = []
        for k, (t, ch, tw) in enumerate(((0.1, 0.06, 30), (0.3, 0.16, 22), (0.9, 0.13, 12), (1.8, 0.07, 6))):
            cc = Vector(piv) + d * t + Vector((0, 0, 0.05))
            twr = math.radians(tw)
            a1 = cc + side * (ch * math.cos(twr)) + Vector((0, 0, ch * math.sin(twr) * 0.4))
            a2 = cc - side * (ch * math.cos(twr)) - Vector((0, 0, ch * math.sin(twr) * 0.4))
            n = Vector((0, 0, 0.012))
            secs.append([a1 - n, a2 - n, a2 + n, a1 + n])
        r.loft("white", secs)
    return a


def battery():
    a = Asset("battery", "Utility")
    cu, s = a.p("Copper"), a.p("Scrap")
    for i in range(3):
        x0, x1 = lo(-7 + i * 5), hi(-4 + i * 5)
        cu.box2("black", (x0 + 0.01, lo(0), lo(-3)), (x1 - 0.01, hi(7) - 0.02, hi(3)), bevel=0.015)
        cu.box2("steel_dark", (x0 + 0.005, hi(7) - 0.04, lo(-3) - 0.004), (x1 - 0.005, hi(7), hi(3) + 0.004), bevel=0.01)
        for k in range(4):
            cu.box2("black", (x0 + 0.02, lo(1) + k * 0.12, hi(3)), (x1 - 0.02, lo(1) + k * 0.12 + 0.02, hi(3) + 0.006), bevel=0.003)
        s.cyl("lamp_red", 0.022, 0.05, (c(-6 + i * 5), c(8) - 0.02, 0), "y", 10, 0.004)
        s.cyl("chrome", 0.022, 0.05, (c(-5 + i * 5), c(8) - 0.02, 0), "y", 10, 0.004)
        s.box2("white", (x0 + 0.04, c(4), hi(3) + 0.006), (x1 - 0.04, c(5.5), hi(3) + 0.009), bevel=0.001)
    for i in range(2):                                     # links
        s.tube("copper", [(c(-5 + i * 5), c(8) + 0.01, 0), (c(-3 + i * 5), c(8) + 0.06, 0), (c(-1 + i * 5), c(8) + 0.01, 0)], 0.008, 6)
    return a


def power_pole():
    a = Asset("power_pole", "Utility")
    w, g, fe = a.p("Wood"), a.p("Glass"), a.p("Iron")
    w.add(K.kit.bm_cyl(0.12, hi(60) - lo(0), 12, 0.006, 0.1), K.wood("dark", "y"), Matrix.Translation((0, (lo(0) + hi(60)) / 2, 0)) @ K._axis_mtx("y"))
    w.box2(K.wood("dark", "x"), (lo(-8), lo(56), lo(0) - 0.01), (hi(8), hi(57), hi(0) + 0.01), bevel=0.01)
    for sx in (-1, 1):
        w.beam(K.wood("dark", "x"), (sx * 0.05, c(52), 0.0), (sx * 0.4, c(56.4), 0.0), 0.05, 0.04)
        fe.cyl("steel", 0.008, 0.06, (c(7) * sx, c(57.5), 0), "y", 6, 0)
        g.lathe("glass", [(0.0, 0.0), (0.035, 0.0), (0.05, 0.03), (0.035, 0.05), (0.045, 0.08), (0.03, 0.1), (0.035, 0.13), (0.0, 0.14)], (c(7) * sx, lo(58), 0), "y", 12)
    for y in range(6, 50, 5):                              # climbing steps
        sx = 1 if (y // 5) % 2 else -1
        fe.rod("steel", (sx * 0.1, c(y), 0.0), (sx * 0.26, c(y), 0.0), 0.01, 5)
    fe.box2("hazard", (-0.13, 1.6, -0.13), (0.13, 1.66, 0.13), bevel=0.004)
    return a


def rain_collector():
    a = Asset("rain_collector", "Utility")
    s, fe = a.p("Scrap"), a.p("Iron")
    R = 5.5 * VS
    s.lathe("blue", [(0.0, lo(0)), (R, lo(0)), (R, hi(12)), (R - 0.01, hi(12)), (0.0, hi(12) - 0.01)], (0, 0, 0), "y", 32)
    for y in (0, 4, 8, 12):                               # bands
        s.torus("steel_dark", R + 0.004, 0.012, (0, c(y) + 0.02, 0), "y", 32, 5)
    s.lathe("steel", [(R - 0.02, hi(12) - 0.02), (R + 0.0, hi(12)), (hi(9) - 0.0, hi(16)), (hi(9) - 0.0, hi(16) - 0.015), (R - 0.02, hi(12) + 0.02), (0.06, hi(12) - 0.0)], (0, 0, 0), "y", 32, close=False)
    s.lathe("steel_dark", [(0.0, hi(12) - 0.02), (0.08, hi(12) - 0.02), (0.08, hi(12) + 0.01), (0.0, hi(12) + 0.01)], (0, 0, 0), "y", 16)
    for k in range(12):                                    # mesh over the funnel
        an = k / 12 * math.pi
        d = Vector((math.cos(an), 0, math.sin(an))) * (hi(9) - 0.02)
        s.rod("steel_dark", (-d.x, hi(16) - 0.01, -d.z), (d.x, hi(16) - 0.01, d.z), 0.003, 4)
    fe.rod("chrome", (0, c(2), c(5.2)), (0, c(2), hi(6)), 0.02, 8)
    fe.box2("pump_red", (-0.03, c(2) + 0.02, hi(6) - 0.03), (0.03, c(2) + 0.06, hi(6) - 0.01), bevel=0.004)
    return a


def water_tank():
    a = Asset("water_tank", "Utility")
    s = a.p("Scrap")
    for x in (-8, 8):
        for z in (-8, 8):
            s.box2("steel_dark", (lo(x) + 0.005, lo(0), lo(z) + 0.005), (hi(x) - 0.005, hi(14), hi(z) - 0.005), bevel=0.008)
            s.box2("steel_dark", (lo(x) - 0.02, lo(0), lo(z) - 0.02), (hi(x) + 0.02, lo(0) + 0.015, hi(z) + 0.02), bevel=0.004)
    for (p0, p1) in (((-8, -8), (8, -8)), ((8, -8), (8, 8)), ((8, 8), (-8, 8)), ((-8, 8), (-8, -8))):
        s.rod("steel_dark", (c(p0[0]), c(2), c(p0[1])), (c(p1[0]), c(13), c(p1[1])), 0.01, 5)
        s.rod("steel_dark", (c(p0[0]), c(13), c(p0[1])), (c(p1[0]), c(2), c(p1[1])), 0.01, 5)
    s.box2("steel_dark", (lo(-9), hi(14) - 0.04, lo(-9)), (hi(9), hi(14), hi(9)), bevel=0.006)
    R = 10.5 * VS
    s.lathe("paleblue" if "paleblue" in K.M else "blue", [(0.0, lo(15)), (R, lo(15)), (R, hi(32)), (R - 0.01, hi(32)), (0.0, hi(32))], (0, 0, 0), "y", 36)
    for y in (15, 20, 25, 30):
        s.torus("rust", R + 0.004, 0.014, (0, c(y), 0), "y", 36, 5)
    s.lathe("steel", [(0.0, lo(33)), (R + 0.01, lo(33)), (R + 0.01, lo(33) + 0.03), (0.3, hi(33)), (0.0, hi(33) + 0.01)], (0, 0, 0), "y", 36)
    s.cyl("steel_dark", 0.12, 0.04, (0.3, hi(33), 0.2), "y", 14, 0.006)
    s.tube("steel_dark", [(R - 0.04, lo(16), 0.1), (R - 0.0, lo(16), 0.1), (R - 0.0, lo(10), 0.1)], 0.03, 8)
    return a


def water_filter():
    a = Asset("filter", "Utility")
    s, g = a.p("Scrap"), a.p("Glass")
    R = 0.27
    s.lathe("sand", [(0.0, lo(0)), (R, lo(0)), (R, hi(4)), (0.0, hi(4))], (0, 0, 0), "y", 24)            # sand bed
    s.lathe("black", [(0.0, hi(4)), (R, hi(4)), (R, hi(10)), (0.0, hi(10))], (0, 0, 0), "y", 24)         # charcoal
    g.lathe("glass", [(0.0, hi(10)), (R, hi(10)), (R, hi(13) + 0.02), (0.0, hi(13) + 0.02)], (0, 0, 0), "y", 24)
    for y in (lo(0) + 0.02, hi(4), hi(10)):
        s.torus("steel_dark", R + 0.004, 0.012, (0, y, 0), "y", 24, 5)
    s.box2("steel", (lo(-4), lo(14), lo(-1)), (hi(4), hi(14), hi(1)), bevel=0.01)
    s.cyl("steel", R - 0.01, 0.03, (0, lo(14), 0), "y", 24, 0.004)
    s.rod("chrome", (0, lo(1), R - 0.01), (0, lo(1), R + 0.008), 0.014, 8)
    return a


def pump():
    a = Asset("pump", "Utility")
    fe = a.p("Iron")
    fe.box2("steel_dark", (lo(-5), lo(0), lo(-4)), (hi(5), hi(1), hi(4)), bevel=0.01)
    fe.cyl("blue", 3.3 * VS, hi(3) - lo(-4), ((lo(-4) + hi(3)) / 2, c(5), 0), "x", 24, 0.02)               # motor
    for k in range(10):
        fe.box2("steel_dark", (lo(-4) + 0.04 + k * 0.06, c(5) + 0.25, -0.02), (lo(-4) + 0.06 + k * 0.06, c(5) + 0.29, 0.02), bevel=0.002)
    fe.cyl("steel_dark", 0.25, 0.03, (lo(-4) + 0.015, c(5), 0), "x", 24, 0.004)
    fe.lathe("steel", [(0.0, 0.0), (0.2, 0.0), (0.22, 0.12), (0.12, 0.3), (0.0, 0.3)], (hi(3) - 0.02, c(5.5), 0), "x", 20)   # volute
    fe.box2("steel", (lo(4), lo(3), lo(-2)), (hi(7), hi(8), hi(2)), bevel=0.02)
    fe.cyl("steel", 1.3 * VS + 0.02, hi(4) - lo(-8), (c(-5), c(3), (lo(-8) + hi(4)) / 2), "z", 14, 0.004)  # suction pipe
    for z in (lo(-8) + 0.02, hi(4) - 0.04):
        fe.cyl("steel_dark", 0.16, 0.025, (c(-5), c(3), z), "z", 14, 0.004)
    fe.box2("black", (c(5) - 0.08, hi(8) - 0.01, -0.06), (c(5) + 0.08, hi(8) + 0.03, 0.06), bevel=0.008)
    return a


def gas_pump(gid, cat):
    a = Asset(gid, cat)
    s, g = a.p("Scrap"), a.p("Glass")
    s.box2("conc", (lo(-5), lo(0), lo(-4)), (hi(5), hi(1), hi(4)), bevel=0.02)
    s.box2("white", (lo(-3), lo(2), lo(-2)), (hi(3), hi(13), hi(2)), bevel=0.025)
    s.box2("pump_red", (lo(-3) - 0.004, lo(14), lo(-2) - 0.004), (hi(3) + 0.004, hi(16), hi(2) + 0.004), bevel=0.02)
    s.box2("white", (lo(-3), lo(17), lo(-2)), (hi(3), hi(20) - 0.04, hi(2)), bevel=0.02)
    s.lathe("white", [(0.0, 0.0), (0.2, 0.0), (0.2, 0.02), (0.0, 0.05)], (0, hi(20) - 0.05, 0), "y", 20)
    g.box2("glass", (lo(-2), lo(12), hi(2)), (hi(2), hi(15), hi(3)), bevel=0.01)                          # dial window
    s.box2("black", (lo(-2) + 0.02, lo(12) + 0.04, hi(2) - 0.01), (hi(2) - 0.02, hi(15) - 0.04, hi(2) + 0.02), bevel=0.003)
    for k in range(3):
        s.box2("white", (lo(-2) + 0.05 + k * 0.1, c(13.5) - 0.012, hi(2) + 0.02), (lo(-2) + 0.12 + k * 0.1, c(13.5) + 0.012, hi(2) + 0.024), bevel=0)
    s.box2("black", (lo(3), lo(8), lo(0)), (hi(4), hi(11), hi(1)), bevel=0.012)                            # holster
    s.tube("black", [(c(4), c(10), c(1)), (c(4.6), c(7), c(1.6)), (c(5.2), c(4), c(2)), (c(6), lo(3), c(2))], 0.022, 8)
    s.tube("steel", [(c(4), c(10.4), c(0.5)), (c(4), c(9), c(1.0))], 0.016, 8)
    for y in (4, 8):
        s.box2("rust", (lo(-3) - 0.002, c(y), hi(2) - 0.004), (lo(-1), c(y) + 0.06, hi(2) + 0.002), bevel=0.002)
    return a


def heater(gid, cooler):
    a = Asset(gid, "Utility")
    fe = a.p("Iron")
    fe.box2("white" if cooler else "steel_dark", (lo(-5), lo(0) + 0.03, lo(-2)), (hi(5), hi(8), hi(2)), bevel=0.03, segs=3)
    for x in (-4, 4):
        fe.box2("black", (c(x) - 0.06, lo(0), lo(-2) + 0.02), (c(x) + 0.06, lo(0) + 0.04, hi(2) - 0.02), bevel=0.006)
    for x in range(-4, 5, 2):
        fe.box2("cloth_pale" if cooler else "lamp_amber", (lo(x) + 0.01, lo(2), hi(2) - 0.01), (hi(x) - 0.01, hi(7), hi(3)), bevel=0.012)
    fe.box2("black", (lo(-5) + 0.03, hi(7) + 0.01, hi(2) - 0.005), (hi(5) - 0.03, hi(8) - 0.02, hi(2) + 0.006), bevel=0.003)
    knob(fe, "chrome", (c(4), c(7.6), hi(2) + 0.01), 0.016, "z", 0.016)
    knob(fe, "chrome", (c(3), c(7.6), hi(2) + 0.01), 0.016, "z", 0.016)
    if cooler:
        for k in range(6):
            fe.box2("steel_dark", (lo(-5) + 0.05 + k * 0.13, lo(1), lo(-2) - 0.004), (lo(-5) + 0.1 + k * 0.13, hi(7), lo(-2) + 0.004), bevel=0.002)
    return a


def sprinkler():
    a = Asset("sprinkler", "Utility")
    fe = a.p("Iron")
    fe.cyl("steel_dark", 0.06, 0.04, (0, lo(0) + 0.02, 0), "y", 12, 0.006)
    fe.cyl("steel", 0.018, hi(6) - lo(0), (0, (lo(0) + hi(6)) / 2, 0), "y", 10, 0.003)
    fe.cyl("brass", 0.03, 0.06, (0, c(6.5), 0), "y", 12, 0.004)
    for an in (0, 90):
        fe.add(K.kit.bm_tube([Vector((-0.2, 0, 0)), Vector((0.2, 0, 0))], 0.014, 8), "chrome", Matrix.Translation((0, c(7), 0)) @ Matrix.Rotation(math.radians(an), 4, "Y"))
    for k in range(4):
        an = k * math.pi / 2
        fe.add(K.kit.bm_tube([Vector((0.0, 0, 0)), Vector((0.0, 0.025, 0.03))], 0.01, 6), "brass", Matrix.Translation((math.cos(an) * 0.19, c(7), math.sin(an) * 0.19)))
    return a


def drip_line():
    a = Asset("drip_line", "Utility")
    rb, fe = a.p("Rubber"), a.p("Iron")
    rb.cyl("rubber", 0.022, hi(10) - lo(-10), (0, lo(0) + 0.024, 0), "z", 10, 0.0)
    for z in range(-9, 11, 3):
        rb.cyl("green", 0.028, 0.03, (0, lo(0) + 0.026, c(z)), "z", 10, 0.002)
        rb.sphere("water", 0.008, (0.0, lo(0) + 0.004, c(z) + 0.01), (1, 0.4, 1), 6, 4)
    fe.cyl("steel", 0.02, hi(3) - lo(0), (0, (lo(0) + hi(3)) / 2, c(-11)), "y", 10, 0.003)
    fe.rod("steel", (0, lo(0) + 0.025, c(-11)), (0, lo(0) + 0.025, lo(-10)), 0.02, 8)
    fe.cyl("crimson", 0.035, 0.02, (0, c(4), c(-11)), "y", 12, 0.003)
    fe.rod("steel", (0, c(3.6), c(-11)), (0, c(3.6), c(-11) - 0.04), 0.01, 6)
    return a


def well():
    a = Asset("well", "Utility")
    st, w, fe, g = a.p("Stone"), a.p("Wood"), a.p("Iron"), a.p("Glass")
    R0, R1 = 7.5 * VS, 5.2 * VS
    r = K.rng(1051)
    for course in range(3):                                   # fieldstone ring in 3 courses
        y0 = lo(0) + course * 0.24
        n = 14
        for k in range(n):
            a0 = (k + (course % 2) * 0.5) / n * math.tau
            a1 = a0 + math.tau / n * 0.92
            rm = (R0 + R1) / 2
            st.add(K.kit.bm_box((R0 - R1) * 0.95, 0.22, rm * (a1 - a0), bevel=0.03, segs=1), ["stone", "rock_grey", "rock"][r.randrange(3)],
                   Matrix.Translation((math.cos((a0 + a1) / 2) * rm, y0 + 0.115, math.sin((a0 + a1) / 2) * rm)) @ Matrix.Rotation(-(a0 + a1) / 2, 4, "Y"))
    st.lathe("conc_dark", [(R1 - 0.02, lo(0) + 0.02), (R0 - 0.06, lo(0) + 0.02), (R0 - 0.06, hi(8) - 0.04), (R1 - 0.02, hi(8) - 0.04)], (0, 0, 0), "y", 28)
    g.cyl("water", R1, 0.01, (0, lo(0) + 0.06, 0), "y", 24, 0)
    for k in range(5):                                        # half cover planks
        x0 = lo(-5) + k * (hi(5) - lo(-5)) / 5
        w.box2(K.WOODS["z"][k % 3], (x0 + 0.004, lo(8), lo(-5)), (x0 + (hi(5) - lo(-5)) / 5 - 0.004, hi(8), hi(0)), bevel=0.006)
    w.box2(K.wood("dark", "x"), (lo(-5), lo(8) - 0.04, c(-3) - 0.03), (hi(5), lo(8), c(-3) + 0.03), bevel=0.004)
    # cast-iron hand pump on the cover
    fe.lathe("crimson", [(0.0, lo(9)), (0.14, lo(9)), (0.14, lo(9) + 0.04), (0.11, lo(9) + 0.06), (0.11, hi(18) - 0.06), (0.13, hi(18) - 0.03), (0.13, hi(18)), (0.0, hi(18))], (0, 0, c(-3)), "y", 20)
    fe.tube("steel_dark", [(0, c(15), c(-2)), (0, c(15), c(1)), (0, c(14), c(1.4))], 0.025, 10)
    fe.box2("steel_dark", (lo(-1), lo(19), lo(-4)), (hi(1), hi(19), hi(-2)), bevel=0.01)
    fe.tube("steel", [(0, c(19), c(-3)), (0, c(21), c(-6.5)), (0, c(23), c(-10))], 0.02, 8)
    fe.sphere("black", 0.03, (0, c(23), c(-10)), (1, 1, 1), 8, 6)
    w.lathe(K.wood("", "y"), [(0.0, lo(0)), (0.13, lo(0)), (0.16, hi(3)), (0.15, hi(3)), (0.12, lo(0) + 0.02), (0.0, lo(0) + 0.02)], (c(9), 0, c(2)), "y", 16, close=False)
    for y in (lo(0) + 0.04, hi(2)):
        fe.torus("steel_dark", 0.13 + (y - lo(0)) * 0.12, 0.007, (c(9), y, c(2)), "y", 16, 4)
    fe.tube("steel_dark", [(c(9) - 0.15, hi(3), c(2)), (c(9), hi(3) + 0.12, c(2)), (c(9) + 0.15, hi(3), c(2))], 0.006, 5, caps=False)
    return a


def water_tower():
    a = Asset("water_tower", "Utility", kind="building")
    w, s, fe = a.p("Wood"), a.p("Scrap"), a.p("Iron")
    for x in (-10, 10):
        for z in (-10, 10):
            w.box2(K.wood("dark", "y"), (lo(x - 1), lo(0), lo(z - 1)), (hi(x), hi(38), hi(z)), bevel=0.012)
    for (p0, p1) in (((-10, 4, -10), (10, 34, -10)), ((10, 4, 10), (-10, 34, 10)), ((-10, 4, 10), (-10, 34, -10)), ((10, 4, -10), (10, 34, 10))):
        w.beam(K.wood("", "y"), tuple(c(v) - 0.04 for v in p0), tuple(c(v) - 0.04 for v in p1), 0.07, 0.05)
    for y in (20,):
        for (p0, p1) in (((-10, -10), (10, -10)), ((10, -10), (10, 10)), ((10, 10), (-10, 10)), ((-10, 10), (-10, -10))):
            w.beam(K.wood("", "x"), (c(p0[0]) - 0.04, c(y), c(p0[1]) - 0.04), (c(p1[0]) - 0.04, c(y), c(p1[1]) - 0.04), 0.08, 0.05)
    for k in range(10):                                        # deck boards
        x0 = lo(-12) + k * (hi(12) - lo(-12)) / 10
        w.box2(K.WOODS["z"][k % 3], (x0 + 0.004, lo(39), lo(-12)), (x0 + (hi(12) - lo(-12)) / 10 - 0.004, hi(39), hi(12)), bevel=0.006)
    R = 12.5 * VS
    n = 30                                                     # staves
    for k in range(n):
        an = k / n * math.tau
        w.add(K.kit.bm_box(R * math.tau / n * 1.0, 0.06, hi(60) - lo(40), bevel=0.006, segs=1), K.WOODS["y"][k % 3],
              Matrix.Translation((math.cos(an) * (R - 0.03), (lo(40) + hi(60)) / 2, math.sin(an) * (R - 0.03))) @ Matrix.Rotation(-an + math.pi / 2, 4, "Y") @ K._axis_mtx("y"))
    for y in (43, 50, 57):                                     # hoops
        fe.torus("steel_dark", R + 0.005, 0.014, (0, c(y), 0), "y", 36, 5)
    s.lathe("rust", [(0.0, hi(66) + 0.02), (0.06, hi(66) + 0.01), (R + 0.04, lo(61)), (R + 0.04, lo(61) - 0.02), (0.0, lo(61) + 0.02)], (0, 0, 0), "y", 32)
    for k in range(16):
        an = k / 16 * math.tau
        s.rod("rust", (math.cos(an) * 0.05, hi(66), math.sin(an) * 0.05), (math.cos(an) * (R + 0.04), lo(61) + 0.004, math.sin(an) * (R + 0.04)), 0.008, 4)
    for x in (-3, 3):                                          # ladder
        w.box2(K.wood("dark", "y"), (lo(x), lo(0), lo(-13)), (hi(x), hi(39), hi(-13)), bevel=0.008)
    for y in range(3, 39, 3):
        w.cyl(K.wood("light", "x"), 0.018, 0.4, (0, c(y), c(-13)), "x", 8, 0.002)
    fe.cyl("steel_dark", 0.04, hi(38) - lo(1), (0, (lo(1) + hi(38)) / 2, 0), "y", 10, 0.003)
    fe.cyl("steel_dark", 0.06, 0.05, (0, lo(1) + 0.03, 0), "y", 10, 0.003)
    return a


# ------------------------------------------------------------------------------------------ industry
def furnace(gid, arc):
    a = Asset(gid, "Industry")
    if arc:
        fe, cu, s = a.p("Iron"), a.p("Copper"), a.p("Scrap")
        R = 0.575
        fe.lathe("steel", [(0.0, lo(0)), (R - 0.03, lo(0)), (R, lo(0) + 0.06), (R, hi(14) - 0.06), (R - 0.04, hi(14)), (0.0, hi(14))], (0, 0, 0), "y", 32)
        for y in (2, 7, 12):
            fe.torus("steel_dark", R + 0.004, 0.02, (0, c(y), 0), "y", 32, 5)
        fe.lathe("iron", [(0.0, hi(14)), (R - 0.05, hi(14)), (R - 0.12, hi(14) + 0.1), (0.0, hi(14) + 0.12)], (0, 0, 0), "y", 28)
        for x in (-3, 0, 3):                                    # electrodes + clamps
            cu.cyl("copper", 0.05, hi(24) - lo(15), (c(x), (lo(15) + hi(24)) / 2, 0), "y", 12, 0.004)
            cu.box2("steel_dark", (c(x) - 0.06, c(21), -0.06), (c(x) + 0.06, c(22), 0.06), bevel=0.008)
            cu.tube("black", [(c(x), c(21.5), -0.06), (c(x) * 1.2, c(22), -0.3), (c(x) * 1.6, c(19), -R + 0.02)], 0.02, 6)
        s.box2("steel_dark", (lo(-3), lo(3), hi(6)), (hi(3), hi(7), hi(7)), bevel=0.012)        # tap door
        s.box2("lamp_amber", (lo(-2), lo(4), hi(7) - 0.01), (hi(2), hi(6), hi(7) + 0.004), bevel=0.006)
        s.tube("steel_dark", [(0, lo(4), hi(7)), (0, lo(4) - 0.02, hi(7) + 0.0)], 0.05, 8)
        return a
    st, s = a.p("Stone"), a.p("Scrap")
    r = K.rng(991)
    for course in range(7):                                    # tapering brick bee-hive in courses
        y0 = lo(0) + course * 0.24
        R0 = (7.5 - course * 4 / 7) * VS
        R1 = (7.5 - (course + 1) * 4 / 7) * VS
        st.lathe("brick" if course % 2 else "brick_dark", [(R0 - 0.2, y0), (R0, y0), (R1 + 0.005, y0 + 0.24), (R1 - 0.2, y0 + 0.24)], (0, 0, 0), "y", 28)
    st.lathe("brick_dark", [(0.12, hi(20) - 0.06), (0.24, hi(20) - 0.06), (0.24, hi(20)), (0.12, hi(20))], (0, 0, 0), "y", 20, close=False)
    for k in range(6):
        an = k / 6 * math.tau + 0.4
        st.rock("stone", 0.1, (math.cos(an) * 0.48, lo(0) + 0.05, math.sin(an) * 0.48), seed=k, squash=0.6, detail=1)
    st.box2("brick_dark", (lo(-3) - 0.04, lo(1) - 0.04, hi(5) - 0.02), (hi(3) + 0.04, hi(6) + 0.04, hi(6) + 0.04), bevel=0.01)   # mouth arch frame
    a.p("Glow", "Stone").box2("ember", (lo(-3), lo(2), hi(6) - 0.01), (hi(3), hi(6), hi(6) + 0.045), bevel=0.01)
    a.parts["Glow"].box2("lamp_amber", (lo(-2), lo(3), hi(6) + 0.04), (hi(2), hi(5), hi(6) + 0.05), bevel=0.004)
    s.box2("steel_dark", (lo(-3), lo(0), hi(6) + 0.02), (hi(3), lo(1), hi(7) - 0.0), bevel=0.006)
    return a


def kiln():
    a = Asset("kiln", "Industry")
    st, s = a.p("Stone"), a.p("Scrap")
    # brick barrel vault (semicircle in x-y, extruded along z), end walls
    n = 12
    for k in range(n):
        a0, a1 = k / n * math.pi, (k + 1) / n * math.pi
        R = hi(8)
        for (z0, z1, m) in ((lo(-6), -0.01, "brick"), (0.01, hi(6), "brick_dark")):
            p0 = Vector((math.cos(a0) * R, lo(0) + math.sin(a0) * (hi(12) - lo(0)), 0))
            p1 = Vector((math.cos(a1) * R, lo(0) + math.sin(a1) * (hi(12) - lo(0)), 0))
            pi0 = Vector((math.cos(a0) * (R - 0.16), lo(0) + math.sin(a0) * (hi(12) - lo(0) - 0.16), 0))
            pi1 = Vector((math.cos(a1) * (R - 0.16), lo(0) + math.sin(a1) * (hi(12) - lo(0) - 0.16), 0))
            secs = [[(q.x, q.y, z0) for q in (p0, p1, pi1, pi0)], [(q.x, q.y, z1) for q in (p0, p1, pi1, pi0)]]
            st.loft(m if k % 2 else ("brick_dark" if m == "brick" else "brick"), secs)
    for z in (lo(-6), hi(6) - 0.08):
        pts = [(math.cos(k / 16 * math.pi) * hi(8), lo(0) + math.sin(k / 16 * math.pi) * (hi(12) - lo(0))) for k in range(17)]
        if z > 0:   # front end wall with the stoke hole
            st.prism("brick", [(x, y) for x, y in pts if not (abs(x) < hi(2) and y < hi(5))] if False else pts, z, z + 0.08, plane="xy")
        else:
            st.prism("brick", pts, z, z + 0.08, plane="xy")
    st.box2("black", (lo(-2), lo(1), hi(6) - 0.002), (hi(2), hi(5), hi(6) + 0.006), bevel=0.01)
    a.p("Glow", "Stone").box2("ember", (lo(-2) + 0.02, lo(1) + 0.02, hi(6) + 0.004), (hi(2) - 0.02, hi(3), hi(6) + 0.01), bevel=0)
    st.box2("conc", (lo(-8), lo(0), lo(-6)), (hi(8), lo(0) + 0.04, hi(6)), bevel=0.01)
    s.cyl("rust", 1.6 * VS, hi(20) - lo(12) + 0.08, (c(5), (lo(12) + hi(20)) / 2 - 0.04, 0), "y", 14, 0.004)    # flue
    s.torus("rust", 1.6 * VS + 0.01, 0.012, (c(5), c(18), 0), "y", 14, 4)
    s.lathe("rust", [(0.0, 0.0), (0.2, 0.0), (0.0, 0.05)], (c(5), hi(20) - 0.06, 0), "y", 14)
    return a


def washplant():
    a = Asset("washplant", "Industry")
    fe, s, g = a.p("Iron"), a.p("Scrap"), a.p("Glass")
    for x in (-10, 10):
        for z in (-5, 5):
            fe.box2("steel_dark", (lo(x) + 0.01, lo(0), lo(z) + 0.01), (hi(x) - 0.01, hi(12 - (x + 10) // 4 - 1), hi(z) - 0.01), bevel=0.006)
    ang = math.degrees(math.atan2(5 * VS, 20 * VS))
    L = math.hypot(20 * VS, 5 * VS) + 0.08
    yc = c(12 - 2.5) + 0.02
    fe.box("steel", (L, 0.04, hi(5) - lo(-5)), (0, yc, 0), r=(0, 0, -ang), bevel=0.006)                        # sieve deck frame
    for k in range(-5, 6):
        fe.box("steel_dark", (L - 0.06, 0.012, 0.012), (0, yc + 0.026, c(k)), r=(0, 0, -ang), bevel=0)
    for k in range(-10, 11, 2):
        fe.box("steel_dark", (0.012, 0.012, hi(5) - lo(-5) - 0.04), (c(k) * 0.97, yc + 0.026 - c(k) * math.tan(math.radians(ang)) * 0.97, 0), r=(0, 0, 0), bevel=0)
    for z in (-5, 5):
        fe.box("steel", (L, 0.1, 0.03), (0, yc + 0.06, c(z)), r=(0, 0, -ang), bevel=0.004)
    s.prism("gen", [(lo(-12), lo(13)), (hi(-8), lo(13)), (hi(-8) + 0.06, hi(18)), (lo(-12) - 0.04, hi(18))], lo(-6), hi(6), plane="xy")   # hopper
    s.box2("steel_dark", (lo(-12) - 0.05, hi(18) - 0.03, lo(-6) - 0.01), (hi(-8) + 0.07, hi(18), hi(6) + 0.01), bevel=0.006)
    s.box2("rust", (lo(6), lo(0), lo(-4)), (hi(12), hi(4), hi(4)), bevel=0.02)                                 # tub
    g.box2("water", (lo(7), hi(4) - 0.04, lo(-3)), (hi(11), hi(4) - 0.02, hi(3)), bevel=0)
    s.box2("steel_dark", (lo(6) - 0.005, hi(4) - 0.02, lo(-4) - 0.005), (hi(12) + 0.005, hi(4), hi(4) + 0.005), bevel=0.004)
    s.cyl("steel_dark", 0.12, 0.2, (c(-10), c(6), lo(-5) - 0.02), "z", 14, 0.01)                              # shaker motor
    s.tube("black", [(c(-10), c(6) + 0.1, lo(-5) - 0.02), (c(-8), c(9), lo(-5) - 0.02)], 0.012, 6)
    return a


def mixer():
    a = Asset("mixer", "Industry")
    fe = a.p("Iron")
    fe.box2("steel_dark", (lo(-6), lo(0), lo(-3)), (hi(6), hi(2) - 0.04, hi(3)), bevel=0.012)
    for x in (-5, 5):
        fe.cyl("tread", 0.1, 0.07, (c(x), lo(0) + 0.1, hi(3) - 0.04), "z", 16, 0.01)
    for sx in (-1, 1):                                     # yoke arms up to the drum trunnions
        fe.beam("steel", (sx * c(3), hi(2) - 0.04, 0), (sx * 0.27, c(8), 0), 0.06, 0.04)
    # pear-shaped drum, axis tilted 35 deg toward +x in the x-y plane (fits the template's thin z)
    tilt = K.rot(0, 0, -35)
    base = Matrix.Translation((0.0, c(7), 0.0)) @ tilt
    prof = [(0.0, -0.3), (0.12, -0.3), (0.24, -0.22), (0.27, -0.05), (0.26, 0.12), (0.2, 0.3), (0.13, 0.42), (0.12, 0.45), (0.1, 0.43), (0.0, 0.38)]
    fe.add(K.kit.bm_lathe(prof, 28), "hazard", base @ K._axis_mtx("y"))
    fe.add(K.kit.bm_torus(0.275, 0.022, 32, 6), "steel_dark", base @ Matrix.Translation((0, -0.04, 0)) @ K._axis_mtx("y"))
    fe.add(K.kit.bm_torus(0.12, 0.016, 20, 6), "rust", base @ Matrix.Translation((0, 0.44, 0)) @ K._axis_mtx("y"))
    for k in range(8):                                     # spiral fin welds
        an = k / 8 * math.tau
        fe.add(K.kit.bm_box(0.016, 0.3, 0.04, bevel=0.003), "rust", base @ Matrix.Translation((math.cos(an) * 0.262, 0.04, math.sin(an) * 0.262)) @ Matrix.Rotation(-an, 4, "Y") @ Matrix.Rotation(0.4, 4, "Z"))
    fe.box2("black", (c(3), hi(2) - 0.04, lo(-3) + 0.02), (hi(6), c(4.5), lo(-1)), bevel=0.012)                 # motor
    fe.rod("steel", (c(-6), c(3), 0), (c(-7.4), c(5.5), 0), 0.012, 6)
    return a


def still():
    a = Asset("still", "Industry")
    cu, g = a.p("Copper"), a.p("Glass")
    cu.lathe("copper", [(0.0, lo(0)), (0.25, lo(0)), (0.34, lo(0) + 0.15), (0.35, hi(8)), (0.3, hi(10)), (0.2, hi(10) + 0.04), (0.0, hi(10) + 0.05)], (c(-4), 0, 0), "y", 28)
    cu.lathe("brass", [(0.0, lo(11)), (0.2, lo(11)), (0.22, c(12)), (0.15, hi(13)), (0.0, hi(13))], (c(-4), 0, 0), "y", 20)
    cu.tube("copper", [(c(-4), hi(13) - 0.02, 0), (c(-3), c(13.4), 0), (c(0), c(12.8), 0), (c(5), c(10.5), 0)], 0.035, 10)
    for k in range(5):                                     # worm coil in a barrel
        cu.torus("copper", 0.17, 0.02, (c(5), c(2) + k * 0.16, 0), "y", 18, 6)
    cu.lathe(K.wood("", "y"), [(0.0, lo(0)), (0.22, lo(0)), (0.24, hi(5)), (0.235, hi(5)), (0.2, lo(0) + 0.03), (0.0, lo(0) + 0.03)], (c(5), 0, 0), "y", 20, close=False)
    for y in (lo(0) + 0.06, hi(4)):
        cu.torus("steel_dark", 0.225 + (y - lo(0)) * 0.05, 0.008, (c(5), y, 0), "y", 20, 4)
    g.lathe("glass_clear" if "glass_clear" in K.M else "glass", [(0.0, lo(0)), (0.08, lo(0)), (0.09, hi(2)), (0.04, hi(3)), (0.035, hi(3) + 0.03), (0.0, hi(3) + 0.03)], (c(5), 0, 0.26), "y", 14)
    cu.cyl("steel_dark", 0.3, 0.06, (c(-4), lo(0) + 0.03, 0), "y", 20, 0.004)
    a.p("Glow", "Copper").add(K.kit.bm_rock(0.12, 4, 0.6, 1), "ember", Matrix.Translation((c(-4), lo(0) + 0.0, 0.0)) @ K._axis_mtx("y") @ Matrix.Diagonal((1.6, 1.6, 0.3, 1)))
    return a


def garage():
    a = Asset("garage", "Industry", kind="building")
    c_, fe = a.p("Concrete"), a.p("Iron")
    for (x0, x1) in ((lo(-25), 0.0), (0.0, hi(25))):
        for k in range(6):
            z0 = lo(-38) + k * (hi(38) - lo(-38)) / 6
            c_.box2("conc", (x0 + 0.006, lo(0), z0 + 0.006), (x1 - 0.006, hi(0), z0 + (hi(38) - lo(-38)) / 6 - 0.006), bevel=0.012)
    for x in (-25, 25):
        for z in (-38, 0, 38):
            fe.box2("steel", (lo(x), hi(0), lo(z)), (hi(x), lo(40), hi(z)), bevel=0.008)
            fe.box2("steel_dark", (lo(x) - 0.03, hi(0), lo(z) - 0.03), (hi(x) + 0.03, hi(0) + 0.02, hi(z) + 0.03), bevel=0.004)
    for x in (-25, 25):
        fe.box2("steel", (lo(x), lo(40) - 0.12, lo(-38)), (hi(x), lo(40), hi(38)), bevel=0.006)
    for z in (-38, 0, 38):
        fe.box2("steel", (lo(-25), lo(40) - 0.1, lo(z)), (hi(25), lo(40), hi(z)), bevel=0.006)
    for i in range(5):                                      # corrugated roof sheets
        K.corrugated(fe, ["tin_rust", "tin", "tin_rust"][i % 3], lo(-25) + i * (hi(25) - lo(-25)) / 5 - 0.01, lo(-25) + (i + 1) * (hi(25) - lo(-25)) / 5 + 0.01,
                     lo(-38), hi(38), 0.018, 0.08, 0.004, Matrix.Translation((0, c(40), 0)))
    fe.box2("gen", (lo(-24), hi(0), lo(30)), (hi(-18), hi(12), hi(37)), bevel=0.02)            # tool cabinet
    for k in range(4):
        drawer_front(fe, "gen", "chrome", lo(-24) + 0.02, hi(-18) - 0.02, hi(0) + 0.05 + k * 0.22, hi(0) + 0.05 + (k + 1) * 0.22, hi(37), 0.015)
    for k in range(10):                                      # lift strip
        z0 = lo(-20) + k * (hi(20) - lo(-20)) / 10
        fe.box2("hazard" if k % 2 == 0 else "black", (lo(-3), hi(0) - 0.004, z0), (hi(3), hi(1), z0 + (hi(20) - lo(-20)) / 10), bevel=0.004)
    fe.box2("steel_dark", (lo(-3) - 0.04, hi(0), lo(-20)), (lo(-3), hi(1) + 0.03, hi(20)), bevel=0.004)
    fe.box2("steel_dark", (hi(3), hi(0), lo(-20)), (hi(3) + 0.04, hi(1) + 0.03, hi(20)), bevel=0.004)
    return a


def tuning_bench():
    a = Asset("tuning_bench", "Industry")
    fe, g = a.p("Iron"), a.p("Glass")
    fe.box2("steel", (lo(-9), lo(10), lo(-4)), (hi(9), hi(11), hi(4)), bevel=0.012)
    for x in (-8, 8):
        for z in (-3, 3):
            fe.box2("steel_dark", (lo(x) + 0.01, lo(0), lo(z) + 0.01), (hi(x) - 0.01, hi(9), hi(z) - 0.01), bevel=0.006)
    fe.box2("steel_dark", (lo(-8), lo(3), lo(-3)), (hi(8), hi(3), hi(3)), bevel=0.006)
    fe.box2("crimson", (lo(5), lo(12), lo(-1)), (hi(7), hi(14) - 0.04, hi(1)), bevel=0.01)          # vise
    fe.box2("crimson", (lo(5) - 0.02, hi(14) - 0.06, lo(-1)), (hi(7) + 0.02, hi(14), lo(-1) + 0.05), bevel=0.006)
    fe.box2("crimson", (lo(5) - 0.02, hi(14) - 0.06, hi(1) - 0.05), (hi(7) + 0.02, hi(14), hi(1)), bevel=0.006)
    fe.rod("chrome", (c(6), c(13), hi(1)), (c(6), c(13), hi(1) + 0.08), 0.012, 8)
    fe.box2("pegboard", (lo(-9), lo(12), lo(-4)), (hi(9), hi(24), hi(-4)), bevel=0.008)              # tool board
    for k in range(7):
        x = c(-7) + k * 0.2
        fe.box2("chrome", (x - 0.012, c(16), hi(-4)), (x + 0.012, c(21), hi(-4) + 0.02), bevel=0.003)
    fe.box2("black", (lo(-7), lo(12), lo(-1)), (hi(-3), hi(17), hi(2)), bevel=0.02)                  # dyno monitor
    g.box2("screen", (lo(-6), lo(13), hi(2)), (hi(-4), hi(16), hi(3)), bevel=0.008)
    for k in range(6):
        g.box2("lamp_green", (lo(-6) + 0.03 + k * 0.035, c(14) + 0.04 * math.sin(k * 1.3), hi(3) - 0.002), (lo(-6) + 0.06 + k * 0.035, c(14) + 0.04 * math.sin(k * 1.3) + 0.01, hi(3) + 0.002), bevel=0)
    fe.tube("black", [(c(-5), lo(12), lo(-1)), (c(-5), lo(10) - 0.02, lo(-3)), (c(-9), lo(4), lo(-4))], 0.01, 6)
    return a


def paint_booth():
    a = Asset("paint_booth", "Industry")
    fe, w, s = a.p("Iron"), a.p("Wood"), a.p("Scrap")
    fe.box2("steel_dark", (lo(-8), lo(0), lo(-5)), (hi(8), hi(0), hi(5)), bevel=0.008)
    fe.lathe("crimson", [(0.0, 0.0), (0.18, 0.02), (0.27, 0.08), (0.28, 0.2), (0.28, hi(1) - lo(-7) - 0.2), (0.27, hi(1) - lo(-7) - 0.08), (0.18, hi(1) - lo(-7) - 0.02), (0.0, hi(1) - lo(-7))],
             (lo(-7), c(4), c(1)), "x", 24)
    for x in (-6, 0):
        fe.box2("steel_dark", (c(x) - 0.03, hi(0), c(1) - 0.2), (c(x) + 0.03, c(2), c(1) + 0.2), bevel=0.004)
    fe.box2("black", (lo(-6), lo(8), lo(-1)), (hi(-3), hi(10), hi(2)), bevel=0.02)                  # motor
    fe.cyl("steel", 0.05, 0.05, (c(-1.5), c(8.5), c(0)), "x", 16, 0.004)                             # gauge
    fe.cyl("white", 0.04, 0.006, (c(-1), c(8.5), c(0)), "x", 16, 0.0)
    for x in (3, 7):                                         # paint rack
        w.box2(K.wood("dark", "y"), (lo(x), lo(1), lo(-4)), (hi(x), hi(16), hi(-4)), bevel=0.006)
    for y in (6, 11, 16):
        w.box2(K.wood("", "x"), (lo(3), lo(y), lo(-5)), (hi(7), hi(y) - 0.03, hi(-3)), bevel=0.006)
    tins = ["crimson", "navy", "ochre", "green", "cream", "black"]
    for i in range(6):
        x, y = c(4 + (i % 3)) + 0.02, c(7 if i < 3 else 12) - 0.03
        s.cyl(tins[i], 0.05, 0.12, (x, y + 0.06, c(-4)), "y", 14, 0.006)
        s.cyl("steel", 0.05, 0.008, (x, y + 0.124, c(-4)), "y", 14, 0.002)
    s.tube("black", [(c(-2), c(6), c(2)), (c(0), c(5), c(4)), (c(2), c(3.5), c(5))], 0.012, 6)      # hose
    s.box2("chrome", (lo(2), lo(3), lo(5)), (hi(3), hi(4), hi(6)), bevel=0.01)                       # spray gun
    s.rod("chrome", (c(2.5), hi(4), c(5.5)), (c(3.2), hi(5), c(6)), 0.008, 6)
    s.sphere("crimson", 0.014, (c(3), c(6), c(6)), (1, 1.4, 1), 8, 6)
    return a


def T_power():
    return [generator(), battery(), power_pole(), spool("cable", True), spool("pipe", False), heater("heater", False), heater("aircon", True)]


def T_windmill():
    return [windmill()]


def T_water():
    return [rain_collector(), water_tank(), water_filter(), pump(), sprinkler(), drip_line()]


def T_wells():
    return [well(), water_tower(), gas_pump("fuel_pump", "Utility")]


def T_smelt():
    return [furnace("furnace", False), furnace("arc_furnace", True), kiln(), washplant(), mixer(), still()]


def T_garage():
    return [garage()]


def T_workshop():
    return [tuning_bench(), paint_booth()]


TILES = [("furn_power", "Generator, battery bank, power pole, cable + pipe spools, heater, air conditioner", [T_power]),
         ("furn_windmill", "Wind turbine (medium): lattice tower, nacelle, Rotor child", [T_windmill]),
         ("furn_water", "Rain collector, water tank, filter, water pump, sprinkler, drip line", [T_water]),
         ("furn_wells", "Well with hand pump, water tower, fuel pump", [T_wells]),
         ("furn_smelt", "Furnace, arc furnace, kiln, wash plant, cement mixer, still", [T_smelt]),
         ("furn_garage_station", "Garage (concrete pad, steel frame, tin roof)", [T_garage]),
         ("furn_workshop", "Tuning bench, paint station", [T_workshop])]

if __name__ == "__main__":
    run(TILES, "utility_")
