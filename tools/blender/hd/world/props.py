"""HD world props and landmark pieces: BiomeProps (Fence0, Barrel0, Streetlight0, GasPump0), PropLibrary (Crate, Table),
landmarks (Mast0 radio mast, GuardHut0 + CheckpointBoom, Sandbags0, CarStack0/1, Tank0, Column0), market stalls
(NpcDirector.Stall: Stall_fuel / parts / food / salvage / scrap / build), the bounty board, the town campfire (+ one seat
log), runway lamp, tumbleweed and caltrop. Origin = template origin; sizes from the voxel templates.

blender -b -P props.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "furniture"))
import hdkit as K  # noqa: E402
from hdkit import Asset, Vector, Matrix  # noqa: E402
from vbuild import VB, rubble  # noqa: E402


def L(i, v):
    return (i - 0.5) * v


def H(i, v):
    return (i + 0.5) * v


def C(i, v):
    return i * v


# ------------------------------------------------------------------------------------------ small props
def fence0():
    a = Asset("Fence0", "Prop", kind="prop")
    w = a.p("Wood")
    v = 0.08
    for x in range(-24, 25, 12):
        w.add(K.kit.bm_cyl(0.05, H(14, v) - L(0, v), 7, 0.006, 0.045), K.wood("dark", "y"), Matrix.Translation((C(x, v), (H(14, v) + L(0, v)) / 2, 0)) @ K._axis_mtx("y"))
    r = K.rng(3)
    for y in (5, 11):
        for k in range(4):                                  # rails, one sagging, one split
            x0, x1 = C(-24 + k * 12, v), C(-12 + k * 12, v)
            sag = -0.06 if (k == 2 and y == 5) else 0.0
            w.beam(K.WOODS["x"][(k + y) % 3], (x0, C(y, v), 0.03), (x1, C(y, v) + sag, 0.03), 0.09, 0.03)
            K.nails(w, "steel_dark", [(x0 + 0.03, C(y, v), 0.05)], 0.006, "z", 0.004)
    return a


def barrel0():
    a = Asset("Barrel0", "Prop", kind="prop")
    s, g = a.p("Scrap"), a.p("Glass")
    v = 0.08
    R = 3.45 * v
    s.lathe("rust", [(0.0, L(0, v)), (R - 0.01, L(0, v)), (R, L(0, v) + 0.01), (R, L(4, v)), (R + 0.008, L(4, v) + 0.02), (R, L(4, v) + 0.04), (R, H(5, v) - 0.04),
                     (R + 0.008, H(5, v) - 0.02), (R, H(5, v)), (R, H(9, v) - 0.01), (R - 0.01, H(9, v)), (0.0, H(9, v) - 0.012)], (0, 0, 0), "y", 28)
    s.lathe("hazard", [(R + 0.002, L(4, v) + 0.045), (R + 0.002, H(5, v) - 0.045)], (0, 0, 0), "y", 28, close=False)
    for k in range(3):                                      # radiation trefoil
        an = k * math.tau / 3
        s.quad("black", (math.cos(an) * 0.01, C(4.5, v) + math.sin(an) * 0.01, -R - 0.006), (math.cos(an - 0.5) * 0.06, C(4.5, v) + math.sin(an - 0.5) * 0.06, -R - 0.006),
               (math.cos(an + 0.5) * 0.06, C(4.5, v) + math.sin(an + 0.5) * 0.06, -R - 0.006), (math.cos(an) * 0.02, C(4.5, v) + math.sin(an) * 0.02, -R - 0.006), 0.002)
    s.torus("rust", R - 0.012, 0.01, (0, H(9, v) - 0.006, 0), "y", 28, 5)
    g.add(K.kit.bm_rock(0.1, 3, 0.35, 1), "glow_green", Matrix.Translation((0.02, H(9, v), -0.03)) @ K._axis_mtx("y"))
    g.sphere("glow_green", 0.03, (R - 0.02, C(7, v), -0.12), (1, 2.5, 0.6), 8, 5)
    return a


def crate_prop():
    a = Asset("Crate", "Prop", kind="prop")
    w, s = a.p("Wood"), a.p("Scrap")
    v = 0.08
    for k in range(4):
        y0 = L(0, v) + k * 0.14 + 0.006
        for side in range(4):
            m = K.WOODS["x" if side < 2 else "z"][(k + side) % 3]
            if side == 0:
                w.box2(m, (L(-3, v) + 0.04, y0, H(3, v) - 0.03), (H(3, v) - 0.04, y0 + 0.128, H(3, v)), bevel=0.005)
            elif side == 1:
                w.box2(m, (L(-3, v) + 0.04, y0, L(-3, v)), (H(3, v) - 0.04, y0 + 0.128, L(-3, v) + 0.03), bevel=0.005)
            elif side == 2:
                w.box2(m, (L(-3, v), y0, L(-3, v) + 0.04), (L(-3, v) + 0.03, y0 + 0.128, H(3, v) - 0.04), bevel=0.005)
            else:
                w.box2(m, (H(3, v) - 0.03, y0, L(-3, v) + 0.04), (H(3, v), y0 + 0.128, H(3, v) - 0.04), bevel=0.005)
    w.box2(K.wood("dark", "x"), (L(-3, v) + 0.03, H(6, v) - 0.03, L(-3, v) + 0.03), (H(3, v) - 0.03, H(6, v), H(3, v) - 0.03), bevel=0.006)
    for x in (-3, 3):
        for z in (-3, 3):
            s.box2("steel", (L(x, v) - 0.002, L(0, v), L(z, v) - 0.002), (H(x, v) + 0.002, H(6, v), H(z, v) + 0.002), bevel=0.006)
    s.box2("lamp_amber", (L(-1, v), L(3, v) + 0.02, L(-3, v) - 0.006), (H(1, v), H(3, v) - 0.02, L(-3, v) + 0.002), bevel=0.003)   # amber stencil
    return a


def table_prop():
    a = Asset("Table", "Prop", kind="prop")
    w, g, s = a.p("Wood"), a.p("Glass"), a.p("Scrap")
    v = 0.08
    for k in range(4):
        z0 = L(-4, v) + k * (H(4, v) - L(-4, v)) / 4
        w.box2(K.WOODS["x"][k % 3], (L(-7, v), L(9, v), z0 + 0.003), (H(7, v), H(9, v), z0 + (H(4, v) - L(-4, v)) / 4 - 0.003), bevel=0.008)
    for x in (-6, 6):
        for z in (-3, 3):
            w.box2(K.wood("dark", "y"), (L(x, v), L(0, v), L(z, v)), (H(x, v), H(8, v), H(z, v)), bevel=0.008)
    g.lathe("glass", [(0.0, 0.0), (0.03, 0.0), (0.03, 0.12), (0.012, 0.16), (0.0, 0.16)], (C(2, v), H(9, v), 0), "y", 12)
    s.cyl("steel", 0.1, 0.012, (C(-3, v), H(9, v) + 0.006, C(-1, v)), "y", 16, 0.004)
    s.cyl("steel_dark", 0.035, 0.07, (C(-1, v), H(9, v) + 0.035, C(2, v)), "y", 12, 0.004)
    return a


def streetlight0():
    a = Asset("Streetlight0", "Prop", kind="prop")
    s, b = a.p("Scrap"), a.p("Bulb", "Glass")
    v = 0.08
    s.cyl("steel", 0.04, H(60, v) - L(0, v), (0, (H(60, v) + L(0, v)) / 2, 0), "y", 10, 0.003, r2=0.03)
    s.cyl("steel_dark", 0.07, 0.06, (0, L(0, v) + 0.03, 0), "y", 12, 0.004)
    s.tube("steel", [(0, C(59, v), 0), (0, C(60, v), 0.06), (0, C(60, v), C(9, v))], 0.025, 8)
    s.lathe("steel_dark", [(0.0, 0.0), (0.03, 0.0), (0.12, -0.06), (0.12, -0.08), (0.0, -0.05)], (0, H(60, v), C(10, v)), "y", 16)
    b.lathe("lamp_amber", [(0.0, 0.0), (0.08, 0.0), (0.06, -0.04), (0.0, -0.06)], (0, H(59, v), C(10, v)), "y", 14)
    return a


def gas_pump0():
    import utility                                         # the same pump as the furniture piece (shared grid)
    a = utility.gas_pump("GasPump0", "Prop")
    a.kind = "prop"
    return a


# ------------------------------------------------------------------------------------------ landmarks
def sandbags0():
    a = Asset("Sandbags0", "Prop", kind="prop", voxel=0.1)
    s = a.p("Cloth")
    v = 0.1
    for row in range(5):
        for b_ in range(-3, 4):
            x0 = b_ * 4 - 2 + (row % 2) * 2
            if x0 < -12 or x0 + 3 > 12:
                continue
            Lb, Wd, Hh = H(x0 + 3, v) - L(x0, v), H(2, v) - L(-2, v), 3 * v
            cx, cy = (H(x0 + 3, v) + L(x0, v)) / 2, L(row * 3, v) + Hh / 2
            st = [(L(x0, v) + 0.01, 0, cy, Wd * 0.3, Hh * 0.3), (L(x0, v) + 0.07, 0, cy, Wd * 0.48, Hh * 0.47), (cx, 0, cy + 0.006, Wd * 0.5, Hh * 0.5),
                  (H(x0 + 3, v) - 0.07, 0, cy, Wd * 0.48, Hh * 0.47), (H(x0 + 3, v) - 0.01, 0, cy, Wd * 0.3, Hh * 0.3)]
            s.superloft("sandbag" if (row + b_) % 3 else "burlap", st, 10, 2.6, axis="x")
    return a


def guard_hut0():
    a = Asset("GuardHut0", "Landmark", kind="building", voxel=0.12)
    b = VB(a, 0.12)
    v = 0.12
    w = b.shell("Wood")
    door = (L(-4, v), H(3, v), L(1, v), H(16, v))
    slit = (L(-8, v), H(8, v), L(12, v), H(14, v))
    for side in range(4):
        if side == 0:      # back (z -10): the slit window, road side
            K.boards_xy(w, K.WOODS["x"], L(-10, v), H(10, v), L(0, v), H(20, v), L(-10, v), H(-10, v), 0.36, [slit], vertical=False, seed=2111)
        elif side == 2:    # front (z +10): the doorway
            K.boards_xy(w, K.WOODS["x"], L(-10, v), H(10, v), L(0, v), H(20, v), L(10, v), H(10, v), 0.36, [door], vertical=False, seed=2112)
        else:
            x = 10 if side == 1 else -10
            for k in range(7):
                y0 = L(0, v) + k * (H(20, v) - L(0, v)) / 7
                w.box2(K.WOODS["z"][k % 3], (L(x, v), y0 + 0.004, L(-9, v)), (H(x, v), y0 + (H(20, v) - L(0, v)) / 7, H(9, v)), bevel=0.008)
    w.box2(K.wood("dark", "x"), (L(-9, v), L(0, v), L(-9, v)), (H(9, v), H(0, v), H(9, v)), bevel=0.006)
    for x in (-10, 10):
        for z in (-10, 10):
            w.box2(K.wood("dark", "y"), (L(x, v) - 0.01, L(0, v), L(z, v) - 0.01), (H(x, v) + 0.01, H(20, v), H(z, v) + 0.01), bevel=0.01)
    g = b.glass()
    g.box2("glass", (L(-8, v), L(12, v), C(-10, v) - 0.005), (H(8, v), H(14, v), C(-10, v) + 0.005), bevel=0)
    s = b.shell("Scrap")
    for i in range(6):
        x0 = L(-12, v) + i * (H(12, v) - L(-12, v)) / 6 - 0.01
        K.corrugated(s, "tin_rust" if i % 2 else "tin", x0, x0 + (H(12, v) - L(-12, v)) / 6 + 0.02, L(-12, v), H(12, v), 0.02, 0.1, 0.006,
                     Matrix.Translation((0, C(21.5, v), 0)), samples_per_period=4)
    de = b.detail("Scrap")
    for k in range(4):                                      # red / white sign on its legs
        de.box2("cloth_red" if k % 2 == 0 else "white", (L(-6, v) + k * (H(6, v) - L(-6, v)) / 4, L(23, v), L(0, v)), (L(-6, v) + (k + 1) * (H(6, v) - L(-6, v)) / 4, H(28, v), H(0, v)), bevel=0.006)
    for x in (-5, 5):
        de.box2("steel_dark", (L(x, v), L(22, v), L(0, v)), (H(x, v), H(23, v), H(0, v)), bevel=0.004)
    de.box2(K.wood("", "x"), (L(-8, v), C(11, v), C(-9, v)), (H(8, v), C(11, v) + 0.04, C(-7, v)), bevel=0.006)        # counter inside the slit
    de.cyl("steel_dark", 0.08, 0.2, (C(6, v), H(0, v) + 0.1, C(6, v)), "y", 14, 0.008)                               # stove
    return a


def checkpoint_boom():
    a = Asset("CheckpointBoom", "Landmark", kind="prop")
    w, s = a.p("Wood"), a.p("Scrap")
    v = 0.08
    for k in range(9):                                     # striped boom (red / white bands along z)
        z0 = L(0, v) + k * (H(99, v) - L(0, v)) / 9
        w.box2("cloth_red" if k % 2 == 0 else "white", (L(-1, v), L(-1, v), z0), (H(1, v), H(1, v), z0 + (H(99, v) - L(0, v)) / 9), bevel=0.012)
    s.box2("steel", (L(-2, v), L(-13, v), L(-2, v)), (H(2, v), H(2, v), H(2, v)), bevel=0.02)
    s.cyl("steel_dark", 0.06, 0.4, (0, 0, 0), "x", 14, 0.006)
    s.box2("steel_dark", (L(-2, v) - 0.03, L(-13, v), L(-2, v) - 0.03), (H(2, v) + 0.03, L(-11, v), H(2, v) + 0.03), bevel=0.01)
    s.cyl("lamp_red", 0.04, 0.03, (0, H(1, v), C(98, v)), "y", 10, 0.004)
    return a


def car_stack(vi):
    gid = "CarStack%d" % vi
    a = Asset(gid, "Landmark", kind="prop", voxel=0.12)
    s, g, rb = a.p("Scrap"), a.p("Glass"), a.p("Rubber")
    v = 0.12
    paints = ["rust", "navy", "crimson", "orange", "green", "white"]
    for i in range(3):
        paint = paints[(vi * 2 + i * 3) % 6]
        y0, off = i * 6, 1 if (i + vi) % 2 == 0 else -2
        r = K.rng(vi * 11 + i)
        # crushed body: a slab with bulged sides and dents, the flattened roof, door seams
        secs = []
        for k in range(9):
            x = L(-18 + off, v) + k * (H(18 + off, v) - L(-18 + off, v)) / 8
            hz = (H(7, v) - 0.02) * (0.86 if k in (0, 8) else 1.0) + r.uniform(-0.03, 0.02)
            y_lo, y_hi = L(y0, v) + r.uniform(0, 0.02), H(y0 + 4, v) - r.uniform(0, 0.05)
            secs.append([(x, y_lo, -hz), (x, y_lo, hz), (x, y_hi, hz * 0.97), (x, y_hi, -hz * 0.97)])
        s.loft(paint, secs)
        s.box2(paint, (L(-10 + off, v), L(y0 + 4, v), L(-5, v)), (H(6 + off, v), H(y0 + 5, v) - 0.03, H(5, v)), bevel=0.03)
        for x in (-6 + off, 2 + off):
            s.box2("black", (C(x, v) - 0.006, L(y0, v) + 0.06, L(-7, v) - 0.006), (C(x, v) + 0.006, H(y0 + 3, v), L(-7, v) + 0.004), bevel=0)
        g.box2("glass", (L(-9 + off, v), L(y0 + 3, v), L(-7, v) - 0.008), (H(5 + off, v), H(y0 + 3, v), L(-7, v) + 0.002), bevel=0)
        rb.cyl("rubber", 2.4 * v, 2 * v, (C(12.5 + off, v), C(y0 + 2, v), H(7, v) - 0.06), "z", 18, 0.02)
        rb.cyl("steel_dark", 1.2 * v, 2 * v + 0.01, (C(12.5 + off, v), C(y0 + 2, v), H(7, v) - 0.06), "z", 12, 0.01)
        for k in range(5):                                  # scrape marks / rust drips
            x = r.uniform(L(-17, v), H(17, v))
            s.box2("rust", (x, L(y0, v) + 0.05, L(-7, v) - 0.004), (x + r.uniform(0.05, 0.2), H(y0 + 4, v) - 0.05, L(-7, v)), bevel=0)
    return a


def tank0():
    a = Asset("Tank0", "Landmark", kind="building", voxel=0.2)
    s = a.p("Shell_Scrap", "Scrap")
    v = 0.2
    R = 12.5 * v + 0.05
    s.lathe("conc_light", [(R - 0.2, L(0, v)), (R, L(0, v)), (R, H(18, v)), (R - 0.2, H(18, v))], (0, 0, 0), "y", 40, close=False)
    s.lathe("white", [(0.0, L(19, v)), (R + 0.02, L(19, v)), (R + 0.02, L(19, v) + 0.05), (0.6, H(19, v) + 0.12), (0.0, H(19, v) + 0.14)], (0, 0, 0), "y", 40)
    s.lathe("hazard", [(R + 0.004, L(9, v)), (R + 0.004, H(10, v))], (0, 0, 0), "y", 40, close=False)
    for y in range(2, 18, 4):                               # weld seams
        s.torus("steel_dark", R + 0.002, 0.008, (0, C(y, v), 0), "y", 40, 3)
    for k in (1, 3):                                        # rust streaks
        s.box2("rust", (-0.6 + k * 0.4, L(1, v), -R - 0.006), (-0.55 + k * 0.4, H(8, v), -R + 0.002), bevel=0)
    for x in (-2, 2):                                       # ladder
        s.box2("steel_dark", (L(x, v), L(0, v), L(-13, v)), (H(x, v) - 0.1, H(20, v), H(-13, v) - 0.1), bevel=0.006)
    for y in range(0, 20, 2):
        s.rod("steel", (C(-2, v), C(y, v), C(-13, v) - 0.05), (C(2, v), C(y, v), C(-13, v) - 0.05), 0.015, 5)
    s.cyl("steel_dark", 0.3, 0.1, (1.2, H(19, v) + 0.1, 0.4), "y", 16, 0.01)
    a.parts["Shell_Scrap"].byte = "Scrap"
    return a


def column0():
    a = Asset("Column0", "Landmark", kind="building", voxel=0.2)
    s, c_ = a.p("Shell_Scrap", "Scrap"), a.p("Shell_Stone", "Stone")
    v = 0.2
    c_.box2("conc", (L(-8, v), L(-2, v), L(-8, v)), (H(8, v), H(0, v), H(8, v)), bevel=0.03)
    s.lathe("chrome", [(0.0, H(0, v)), (4.5 * v, H(0, v)), (4.5 * v, H(80, v) - 0.3), (2.5 * v, H(80, v)), (0.0, H(80, v))], (0, 0, 0), "y", 28)
    for y in range(4, 80, 6):
        s.torus("steel_dark", 4.5 * v + 0.004, 0.02, (0, C(y, v), 0), "y", 28, 4)
    for y in range(16, 73, 18):                             # platforms + rails
        s.lathe("steel_dark", [(4.5 * v, L(y, v)), (7.5 * v, L(y, v)), (7.5 * v, H(y, v)), (4.5 * v, H(y, v))], (0, 0, 0), "y", 28)
        for k in range(16):
            an = k / 16 * math.tau
            s.rod("rust", (math.cos(an) * 7.3 * v, H(y, v), math.sin(an) * 7.3 * v), (math.cos(an) * 7.3 * v, H(y, v) + 1.0, math.sin(an) * 7.3 * v), 0.015, 4)
        s.torus("rust", 7.3 * v, 0.018, (0, H(y, v) + 1.0, 0), "y", 28, 4)
    s.box2("rust", (L(6, v), L(0, v), L(-1, v)), (H(7, v), H(60, v), H(1, v)), bevel=0.02)
    for y in (20, 38, 56):
        s.tube("rust", [(C(6.5, v), C(y, v), 0), (C(5, v), C(y, v) + 0.2, 0)], 0.06, 8)
    for x in (-0.2, 0.2):                                   # cage ladder
        s.rod("steel_dark", (x, H(0, v), -4.6 * v), (x, H(76, v), -4.6 * v), 0.015, 4)
    for y in range(2, 76, 2):
        s.rod("steel_dark", (-0.2, C(y, v), -4.6 * v), (0.2, C(y, v), -4.6 * v), 0.01, 4)
    return a


def mast0():
    a = Asset("Mast0", "Landmark", kind="building", voxel=0.16)
    s, c_, w = a.p("Shell_Scrap", "Scrap"), a.p("Shell_Stone", "Stone"), a.p("Shell_Wood", "Wood")
    v, Ht = 0.16, 136
    c_.box2("conc", (L(-9, v), L(-2, v), L(-9, v)), (H(9, v), H(0, v), H(9, v)), bevel=0.03)
    wy = lambda y: C(7 + (1 - 7) * y / Ht, v)
    for sx in (-1, 1):
        for sz in (-1, 1):
            s.tube("steel", [(sx * wy(y), C(y, v), sz * wy(y)) for y in (0, Ht // 2, Ht)], [0.05, 0.04, 0.03], 6)
    for y in range(0, Ht + 1, 12):
        q = wy(y)
        for (p0, p1) in (((-q, -q), (q, -q)), ((q, -q), (q, q)), ((q, q), (-q, q)), ((-q, q), (-q, -q))):
            s.rod("steel_dark", (p0[0], C(y, v), p0[1]), (p1[0], C(y, v), p1[1]), 0.02, 4)
        if y + 12 <= Ht:
            q2 = wy(y + 12)
            for (a0, a1) in (((-q, -q), (q2, -q2)), ((q, q), (-q2, q2)), ((-q, q), (-q2, -q2)), ((q, -q), (q2, q2))):
                s.rod("steel_dark", (a0[0], C(y, v), a0[1]), (a1[0], C(y + 12, v), a1[1]), 0.014, 4)
    s.rod("chrome", (0, C(Ht, v), 0), (0, H(Ht + 14, v), 0), 0.02, 6)
    s.sphere("lamp_red", 0.1, (0, C(Ht + 15.5, v), 0), (1, 1, 1), 10, 6)
    for y in (80, 104):                                     # dishes on arms
        s.lathe("white", [(0.0, 0.0), (0.12, 0.02), (0.5, 0.18), (0.52, 0.2), (0.0, 0.05)], (0, C(y, v), C(-8.5, v) - 0.1), "z", 20, r=(180, 0, 0))
        s.rod("steel_dark", (0, C(y, v), C(-7, v)), (0, C(y, v), -wy(y)), 0.03, 6)
    w.box2(K.wood("", "y"), (L(9, v), L(1, v), L(-3, v)), (H(13, v), H(9, v), H(3, v)), bevel=0.02)
    w.box2("tin_rust", (L(9, v) - 0.02, L(10, v), L(-4, v)), (H(14, v), H(10, v), H(4, v)), bevel=0.01)
    w.box2(K.wood("dark", "y"), (H(13, v) - 0.01, L(1, v), C(-1, v)), (H(13, v) + 0.02, H(7, v), C(1, v)), bevel=0.006)
    s.tube("black", [(C(9, v), C(6, v), 0), (C(6, v), C(10, v), 0.3), (wy(30), C(30, v), 0.2)], 0.025, 5)
    return a


# ------------------------------------------------------------------------------------------ stalls, boards, fires
def stall(kind):
    gid = "Stall_" + kind
    a = Asset(gid, "Stall", kind="prop")
    w, cl, s, g = a.p("Wood"), a.p("Cloth"), a.p("Scrap"), a.p("Glass")
    v = 0.08
    canvas = {"fuel": "cloth_red", "parts": "cloth_blue", "food": "cloth_ochre"}.get(kind, "cloth_green")
    for k in range(5):                                      # table
        z0 = L(-4, v) + k * (H(4, v) - L(-4, v)) / 5
        w.box2(K.WOODS["x"][k % 3], (L(-14, v), L(10, v), z0 + 0.003), (H(14, v), H(10, v), z0 + (H(4, v) - L(-4, v)) / 5 - 0.003), bevel=0.006)
    for x in (-13, 13):
        for z in (-3, 3):
            w.box2(K.wood("dark", "y"), (L(x, v), L(0, v), L(z, v)), (H(x, v), H(9, v), H(z, v)), bevel=0.008)
    for x in (-15, 15):                                     # awning poles (back tall, front short)
        w.add(K.kit.bm_cyl(0.035, H(30, v) - L(0, v), 7, 0.0, 0.03), K.wood("dark", "y"), Matrix.Translation((C(x, v), (H(30, v) + L(0, v)) / 2, C(-14, v))) @ K._axis_mtx("y"))
        w.add(K.kit.bm_cyl(0.035, H(26, v) - L(0, v), 7, 0.0, 0.03), K.wood("dark", "y"), Matrix.Translation((C(x, v), (H(26, v) + L(0, v)) / 2, C(6, v))) @ K._axis_mtx("y"))
    for k in range(9):                                      # sloped striped awning
        x0 = L(-17, v) + k * (H(17, v) - L(-17, v)) / 9
        x1 = x0 + (H(17, v) - L(-17, v)) / 9
        m = canvas if k % 2 == 0 else "cloth_cream"
        cl.quad(m, (x0, H(30, v), L(-16, v)), (x1, H(30, v), L(-16, v)), (x1, H(30 - 24 // 6, v), H(8, v)), (x0, H(30 - 24 // 6, v), H(8, v)), 0.012)
        cl.box2(m, (x0, H(26, v) - 0.14, H(8, v) - 0.012), (x1, H(26, v), H(8, v)), bevel=0.002)
    r = K.rng(len(kind) * 7)
    if kind == "fuel":
        for i in range(4):                                  # jerry cans
            x = C(-9.5 + i * 6, v)
            s.box2("pump_red", (x - 0.12, H(10, v), C(-1.5, v) - 0.08), (x + 0.12, H(10, v) + 0.4, C(-1.5, v) + 0.12), bevel=0.02)
            s.box2("steel_dark", (x - 0.05, H(10, v) + 0.4, C(-1.5, v)), (x + 0.05, H(10, v) + 0.45, C(-1.5, v) + 0.04), bevel=0.006)
        s.lathe("rust", [(0.0, L(0, v)), (0.28, L(0, v)), (0.28, H(9, v)), (0.0, H(9, v))], (C(-18, v), 0, C(8, v)), "y", 18)
    elif kind == "parts":
        s.cyl("rubber", 0.33, 0.28, (C(-9.5, v), C(14, v), 0), "x", 20, 0.04)
        s.box2("steel", (L(-2, v), H(10, v), L(-2, v)), (H(3, v), H(14, v), H(2, v)), bevel=0.02)
        s.cyl("steel_dark", 0.08, 0.1, (C(0.5, v), H(14, v) + 0.05, 0), "y", 12, 0.006)
        s.box2("chrome", (L(6, v), H(10, v), L(-1, v)), (H(11, v), H(12, v), H(1, v)), bevel=0.012)
        s.cyl("rubber", 3.4 * v, 7 * v, (C(-18, v), C(3.5, v), 0), "z", 18, 0.03)
        s.cyl("steel_dark", 1.6 * v, 7 * v + 0.01, (C(-18, v), C(3.5, v), 0), "z", 12, 0.01)
    elif kind == "food":
        for i in range(6):
            g.lathe("glass", [(0.0, 0.0), (0.04, 0.0), (0.04, 0.12), (0.03, 0.15), (0.0, 0.15)], (C(-11.5 + i * 4, v), H(10, v), C(-0.5, v)), "y", 10)
            g.lathe("crimson" if i % 2 == 0 else "ochre", [(0.0, 0.004), (0.036, 0.004), (0.036, 0.09), (0.0, 0.09)], (C(-11.5 + i * 4, v), H(10, v), C(-0.5, v)), "y", 10)
        w.box2(K.wood("", "x"), (L(-18, v), L(0, v), L(0, v)), (H(-16, v), H(6, v), H(4, v)), bevel=0.02)
        for k in range(6):
            w.sphere("crimson" if k % 2 else "ochre", 0.05, (C(-17, v) + (k % 3) * 0.06 - 0.06, H(6, v) + 0.03, C(2, v) + (k // 3) * 0.08 - 0.04), (1, 1, 1), 8, 6)
    else:
        for i in range(12):                                 # salvage: odds and ends
            x = C(-12 + i * 2, v)
            if i % 3 == 0:
                s.cyl(r.choice(["rust", "steel", "chrome"]), 0.04, 0.1, (x, H(10, v) + 0.05, C((i * 5) % 5 - 2, v)), "y", 10, 0.004)
            else:
                s.box2(r.choice(["rust", "steel_dark", "copper"]), (x - 0.06, H(10, v), C((i * 5) % 5 - 2, v) - 0.05), (x + 0.06, H(10, v) + 0.06, C((i * 5) % 5 - 2, v) + 0.05), bevel=0.01)
    return a


def bounty_board():
    a = Asset("BountyBoard", "Prop", kind="prop")
    w, s, p = a.p("Wood"), a.p("Scrap"), a.p("Paper", "Wood")
    v = 0.08
    for x in (-9, 9):
        w.box2(K.wood("dark", "y"), (L(x, v), L(0, v), L(0, v)), (H(x, v), H(26, v), H(0, v)), bevel=0.01)
    for k in range(5):
        y0 = L(12, v) + k * (H(25, v) - L(12, v)) / 5
        w.box2(K.WOODS["x"][k % 3], (L(-10, v), y0 + 0.004, L(0, v) + 0.01), (H(10, v), y0 + (H(25, v) - L(12, v)) / 5, H(0, v)), bevel=0.006)
    s.quad("tin_rust", (L(-11, v), H(27, v), L(-1, v) - 0.08), (H(11, v), H(27, v), L(-1, v) - 0.08), (H(11, v), L(26, v), H(1, v) + 0.08), (L(-11, v), L(26, v), H(1, v) + 0.08), 0.012)
    rr = K.NetRandom(1504)
    for i in range(7):                                      # notices (same spots as the voxel board), lines of text
        x0, y0 = rr.next(-8, 5), rr.next(13, 21)
        paper = "ochre" if i % 3 == 0 else "paper"
        tilt = ((i * 37) % 9 - 4) * 1.5
        p.box(paper, (4 * v - 0.02, 4 * v - 0.02, 0.004), (C(x0 + 1.5, v), C(y0 + 1.5, v), H(0, v) + 0.004 + i * 0.0005), r=(0, 0, tilt), bevel=0)
        for k in range(3):
            p.box("black", (2 * v, 0.008, 0.002), (C(x0 + 1.5, v), C(y0 + 0.8 + k * 0.7, v), H(0, v) + 0.008 + i * 0.0005), r=(0, 0, tilt), bevel=0)
    for x in (-2, 2):
        s.sphere("cloth_red", 0.015, (C(x, v), C(24, v), H(0, v) + 0.01), (1, 1, 0.6), 8, 4)
    return a


def campfire_world():
    a = Asset("Campfire", "Prop", kind="prop")
    st, w, gl = a.p("Stone"), a.p("Wood"), a.p("Glow", "Wood")
    v = 0.08
    for i in range(14):
        an = i / 14 * math.tau
        st.rock("rock_grey" if i % 2 else "stone", 0.07 + (i & 1) * 0.02, (math.cos(an) * 5 * v, L(0, v) + 0.04, math.sin(an) * 5 * v), seed=i, squash=0.75, detail=1, floor=L(0, v))
    st.cyl("ash", 0.2, 0.02, (0, L(0, v) + 0.01, 0), "y", 16, 0.004)
    w.rod(K.wood("dark", "x"), (C(-3, v), C(1, v), C(-2, v)), (C(3, v), C(2, v), C(2, v)), 0.055, 8)
    w.rod(K.wood("", "x"), (C(-3, v), C(1, v), C(2, v)), (C(3, v), C(2, v), C(-2, v)), 0.055, 8)
    gl.add(K.kit.bm_rock(0.1, 2, 0.4, 1), "ember", Matrix.Translation((0, C(0.6, v), 0)) @ K._axis_mtx("y"))
    gl.lathe("flame", [(0.0, 0.0), (0.07, 0.02), (0.05, 0.1), (0.0, 0.24)], (0, C(1.4, v), 0), "y", 10)
    return a


def campfire_log():
    """One seat log of Campfire.BuildLogs (the game places one per seat round the ring)."""
    a = Asset("CampfireLog", "Prop", kind="prop", note="one of Campfire.BuildLogs' seat logs (template CampfireLogs<n>)")
    w = a.p("Wood")
    v = 0.08
    w.cyl("bark", 2.6 * v, 10 * v, (0, C(2, v), 0), "x", 14, 0.02)
    for x in (-5 * v, 5 * v):
        w.cyl(K.wood("light", "x"), 2.6 * v - 0.012, 0.01, (x, C(2, v), 0), "x", 14, 0)
    w.box2(K.wood("light", "x"), (-0.3, C(2, v) + 2.6 * v - 0.03, -0.08), (0.3, C(2, v) + 2.6 * v - 0.01, 0.08), bevel=0.004)   # sat-smooth top
    return a


def runway_lamp():
    a = Asset("RunwayLamp", "Prop", kind="prop")
    s, b = a.p("Scrap"), a.p("Bulb", "Glass")
    v = 0.08
    s.cyl("steel_dark", 0.03, H(2, v) - L(0, v), (0, (H(2, v) + L(0, v)) / 2, 0), "y", 10, 0.003)
    s.cyl("steel_dark", 0.07, 0.03, (0, L(0, v) + 0.015, 0), "y", 12, 0.004)
    b.lathe("lamp_amber", [(0.0, L(3, v)), (0.11, L(3, v)), (0.11, L(3, v) + 0.03), (0.07, H(3, v)), (0.0, H(3, v))], (0, 0, 0), "y", 14)
    s.torus("steel", 0.11, 0.008, (0, L(3, v) + 0.015, 0), "y", 14, 4)
    return a


def tumbleweed():
    a = Asset("Tumbleweed", "Prop", kind="vegetation")
    w = a.p("Wood")
    r = K.rng(5)
    R = 4.3 * 0.08
    for k in range(70):                                     # tangled arcs of dry twigs on a ball
        th, ph = r.uniform(0, math.tau), math.acos(r.uniform(-1, 1))
        d = Vector((math.sin(ph) * math.cos(th), math.cos(ph), math.sin(ph) * math.sin(th)))
        side = d.cross(Vector((0.3, 1, 0.1))).normalized()
        pts = []
        for j in range(4):
            t = (j / 3 - 0.5) * 1.2
            q = (d * math.cos(t) + side * math.sin(t)) * R * r.uniform(0.75, 1.0)
            pts.append(q)
        w.tube(r.choice(["deadwood", "wood_light_x", "hay"]), pts, 0.006, 3, caps=False)
    w.sway = lambda p: 0.0
    return a


def caltrop():
    a = Asset("Caltrop", "Prop", kind="prop", note="RoadHazards caltrop (0.04 m voxels)")
    fe = a.p("Iron")
    dirs = [Vector((0, 1, 0)), Vector((0.94, -0.33, 0)), Vector((-0.47, -0.33, 0.82)), Vector((-0.47, -0.33, -0.82))]
    for d in dirs:
        fe.add(K.kit.bm_cyl(0.012, 0.08, 6, 0.0, 0.001), "rust", Matrix.Translation(d * 0.04 + Vector((0, 0.03, 0))) @ d.to_track_quat("Z", "Y").to_matrix().to_4x4())
    return a


TILES = [("world_roadside", "Fence, waste barrel, crate, table, street light, gas pump", [lambda: [fence0(), barrel0(), crate_prop(), table_prop(), streetlight0(), gas_pump0()]], 3),
         ("world_checkpoint", "Checkpoint: guard hut, boom, sandbags", [lambda: [guard_hut0(), checkpoint_boom(), sandbags0()]], 3),
         ("world_scrapyard", "Scrapyard car stacks (2)", [lambda: [car_stack(0), car_stack(1)]], 2),
         ("world_refinery", "Refinery: storage tank, distillation column", [lambda: [tank0(), column0()]], 2),
         ("world_mast", "Radio mast (22 m) with equipment shed", [lambda: [mast0()]], 1),
         ("world_stalls", "Market stalls: fuel, parts, food, salvage / scrap / build", [lambda: [stall(k) for k in ("fuel", "parts", "food", "salvage", "scrap", "build")]], 3),
         ("world_town_misc", "Bounty board, town campfire + seat log, runway lamp, tumbleweed, caltrop", [lambda: [bounty_board(), campfire_world(), campfire_log(), runway_lamp(), tumbleweed(), caltrop()]], 3)]

if __name__ == "__main__":
    K.run_tiles(TILES, "world", HERE, "props_")
