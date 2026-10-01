"""HD pieces of FurnitureLibrary.Utilities.cs: solar still, desalinator, power switch / timer / light sensor / float
switch, load breaker, water valve, biogas digester and generator, chest freezer, ice box, root cellar.

blender -b -P utilities2.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fcommon import K, Asset, Vector, Matrix, lo, hi, c, W, knob, screws, leg_square, drawer_front, bulb, run, VS  # noqa: E402


def solar_still():
    a = Asset("solar_still", "Utility")
    w, s, g, st = a.p("Wood"), a.p("Scrap"), a.p("Glass"), a.p("Stone")
    for x in (-9, 9):
        for z in (-6, 6):
            w.box2(W("dark", "y"), (lo(x), lo(0), lo(z)), (hi(x), hi(3), hi(z)), bevel=0.01)
    for k in range(6):
        x0 = lo(-9) + k * (hi(9) - lo(-9)) / 6
        w.box2(K.WOODS["z"][k % 3], (x0 + 0.003, lo(3), lo(-6)), (x0 + (hi(9) - lo(-9)) / 6 - 0.003, hi(3), hi(6)), bevel=0.006)
    s.box2("steel_dark", (lo(-9), lo(4), lo(-6)), (hi(9), hi(5), hi(6)), bevel=0.01)
    s.box2("black", (lo(-8), hi(5) - 0.03, lo(-5)), (hi(8), hi(5) - 0.01, hi(5)), bevel=0)
    s.box2("water", (lo(-8), hi(5) - 0.015, lo(-5)), (hi(8), hi(5) - 0.005, hi(5)), bevel=0)
    for sx in (-1, 1):                                     # pitched glass roof on a frame
        g.panel("glass_clear", (lo(-9), hi(5) + 0.01, sx * hi(6)), (lo(-9), hi(9), 0.0), (hi(9), hi(9), 0.0), 0.006)
        s.rod("chrome", (lo(-9), hi(5) + 0.01, sx * hi(6)), (hi(9), hi(5) + 0.01, sx * hi(6)), 0.01, 6)
    s.rod("chrome", (lo(-9), hi(9), 0), (hi(9), hi(9), 0), 0.01, 6)
    for x in (-9, 9):
        s.rod("chrome", (c(x), hi(5), lo(-6)), (c(x), hi(9), 0), 0.008, 5)
        s.rod("chrome", (c(x), hi(5), hi(6)), (c(x), hi(9), 0), 0.008, 5)
    for z in (-7, 7):                                      # gutters + spout
        s.box2("chrome", (lo(-9), lo(5), lo(z)), (hi(9), hi(5) - 0.02, hi(z)), bevel=0.006)
    s.rod("chrome", (hi(9), hi(5), c(7)), (c(10), lo(3), c(7)), 0.012, 6)
    s.sphere("water", 0.012, (c(10), lo(2), c(7)), (1, 1.5, 1), 6, 4)
    st.rock("sand", 0.04, (0, hi(9) - 0.03, 0), seed=1, squash=0.7, detail=1)
    st.box2("cloth_cream", (lo(-4), hi(5) - 0.006, lo(0)), (hi(-3), hi(5), hi(1)), bevel=0)
    return a


def desalinator():
    a = Asset("desalinator", "Utility")
    fe, s, g = a.p("Iron"), a.p("Scrap"), a.p("Glass")
    fe.box2("steel_dark", (lo(-10), lo(0), lo(-5)), (hi(10), hi(1), hi(5)), bevel=0.012)
    for z in (-2, 2):                                      # membrane vessels
        fe.cyl("white", 2.4 * VS, hi(6) - lo(-9), ((lo(-9) + hi(6)) / 2, c(5), c(z * 1.2)), "x", 20, 0.02)
        for x in (-9, 6):
            fe.cyl("chrome", 2.7 * VS, 0.05, (c(x), c(5), c(z * 1.2)), "x", 20, 0.008)
    for x in (-6, 3):
        fe.box2("steel_dark", (c(x) - 0.03, hi(1), lo(-4)), (c(x) + 0.03, c(4), hi(4)), bevel=0.004)
    fe.box2("navy", (lo(7), lo(2), lo(-4)), (hi(10), hi(7), hi(4)), bevel=0.02)
    fe.box2("gen", (lo(8), lo(8), lo(-2)), (hi(10), hi(10), hi(2)), bevel=0.02)
    fe.cyl("lamp_amber", 0.02, 0.02, (c(8), hi(11) - 0.02, 0), "y", 8, 0.002)
    s.rod("chrome", (lo(-3), c(7), 0), (hi(2), c(7), 0), 0.014, 8)
    for x in (-3, 1):
        g.cyl("white", 0.05, 0.03, (c(x + 0.5), c(8.5), 0.02), "z", 14, 0.006)
        g.box2("black", (c(x + 0.5) - 0.003, c(8.5), 0.035), (c(x + 0.5) + 0.003, c(8.5) + 0.04, 0.038), bevel=0)
    s.cyl("blue", 1.2 * VS, hi(9) - lo(5), (c(4), c(3), (lo(5) + hi(9)) / 2), "z", 12, 0.006)
    s.box2("steel", (lo(-10), lo(2), lo(5)), (hi(-6), hi(3), hi(8)), bevel=0.01)
    s.box2("cloth_cream", (lo(-9), hi(3) - 0.02, lo(6)), (hi(-7), hi(3) + 0.004, hi(7)), bevel=0.006)
    return a


def switch_box(gid, kind):
    a = Asset(gid, "Utility")
    fe, cu, s, g = a.p("Iron"), a.p("Copper"), a.p("Scrap"), a.p("Glass")
    fe.box2("steel_dark", (lo(-1), lo(0), lo(-1)), (hi(1), hi(1), hi(1)), bevel=0.01)
    fe.cyl("steel_dark", 0.025, hi(3) - lo(2), (0, (lo(2) + hi(3)) / 2, 0), "y", 8, 0.003)
    col = {1: "white", 3: "blue"}.get(kind, "rig_green")
    fe.box2(col, (lo(-3), lo(4), lo(-1)), (hi(3), hi(10), hi(1)), bevel=0.02)
    fe.box2("steel", (lo(-3) - 0.006, lo(11), lo(-1) - 0.006), (hi(3) + 0.006, hi(11), hi(1) + 0.006), bevel=0.01)
    for (x, y) in ((-2.5, 4.5), (2.5, 4.5), (-2.5, 10.5), (2.5, 10.5)):
        screws(fe, "chrome", [(c(x), c(y), hi(1))], 0.008)
    cu.cyl("copper", 0.02, 0.05, (0, c(4) + 0.02, lo(-2) + 0.02), "z", 8, 0.003)
    if kind == 0:
        s.box2("black", (lo(-1), lo(6), hi(1)), (hi(1), hi(9), hi(2)), bevel=0.006)
        s.rod("chrome", (0, c(8), hi(2) - 0.01), (0, c(9.6), hi(3) + 0.02), 0.01, 6)
        s.sphere("crimson", 0.025, (0, c(10), hi(4) - 0.02), (1, 1, 1), 10, 6)
        s.cyl("lamp_amber", 0.014, 0.01, (c(2), c(10), hi(1) + 0.004), "z", 8, 0)
    elif kind == 1:
        g.cyl("enamel", 2.3 * VS, 0.01, (0, c(8), hi(1) + 0.006), "z", 24, 0.002)
        s.box2("black", (-0.004, c(8), hi(1) + 0.012), (0.004, c(10), hi(1) + 0.016), bevel=0)
        s.box2("black", (0.0, c(8) - 0.004, hi(1) + 0.012), (c(1), c(8) + 0.004, hi(1) + 0.016), bevel=0)
        for (x, y) in ((0, 10), (2, 8), (0, 6), (-2, 8)):
            s.cyl("crimson", 0.008, 0.03, (c(x) * 0.85, c(8) + (c(y) - c(8)) * 0.85, hi(1) + 0.02), "z", 6, 0)
    elif kind == 2:
        g.lathe("glass_clear", [(0.0, hi(11)), (0.11, hi(11)), (0.1, hi(12)), (0.06, hi(13)), (0.0, hi(13) + 0.01)], (0, 0, 0), "y", 16)
        g.sphere("lamp_amber", 0.025, (0, hi(12), 0), (1, 0.5, 1), 8, 5)
        s.cyl("lamp_amber", 0.014, 0.01, (c(2), c(9), hi(1) + 0.004), "z", 8, 0)
    else:
        s.cyl("chrome", 1.4 * VS + 0.01, hi(6) - lo(-6), (0, c(1), 0), "x", 14, 0.004)
        for x in (-6, 6):
            s.cyl("rust", 1.9 * VS, 0.03, (c(x), c(1), 0), "x", 14, 0.004)
        s.box2("black", (lo(-1), lo(6), hi(1)), (hi(1), hi(8), hi(2)), bevel=0.006)
        s.cyl("lamp_amber", 0.014, 0.01, (0, c(9), hi(1) + 0.004), "z", 8, 0)
        s.sphere("crimson", 0.035, (0, c(7), hi(2) + 0.03), (1, 1, 1), 10, 6)
    return a


def breaker():
    a = Asset("breaker", "Utility")
    w, fe, cu, g = a.p("Wood"), a.p("Iron"), a.p("Copper"), a.p("Glass")
    w.box2(W("dark", "y"), (lo(0) - 0.01, lo(0), lo(-1)), (hi(0) + 0.01, hi(8), hi(-1)), bevel=0.01)
    fe.box2("rig_green", (lo(-5), lo(6), lo(-1)), (hi(5), hi(16), hi(1)), bevel=0.02)
    for k in range(10):
        fe.box2("hazard" if k % 2 == 0 else "black", (lo(-5) + k * 0.088, lo(17), lo(-1)), (lo(-5) + (k + 1) * 0.088, hi(17), hi(1)), bevel=0)
    for i, lm in enumerate(("lamp_red", "lamp", "lamp_amber")):
        x = c(-3 + i * 3)
        fe.box2("black", (x - 0.03, lo(8), hi(1)), (x + 0.03, hi(12), hi(2)), bevel=0.004)
        fe.box2("chrome", (x - 0.025, lo(11), hi(2)), (x + 0.025, hi(13), hi(3)), bevel=0.008, r=(-20, 0, 0))
        g.cyl(lm, 0.025, 0.02, (x, c(15), hi(1) + 0.01), "z", 10, 0.004)
    for x in (-5, 5):
        cu.cyl("copper", 0.025, 0.08, (c(x), c(5.5), 0), "y", 8, 0.003)
    return a


def water_valve():
    a = Asset("water_valve", "Utility")
    fe = a.p("Iron")
    fe.box2("steel_dark", (lo(-2), lo(0), lo(-1)), (hi(2), hi(1), hi(1)), bevel=0.01)
    fe.cyl("steel", 1.4 * VS + 0.01, hi(7) - lo(-7), (0, c(3), 0), "x", 14, 0.004)
    for x in (-3, 3):
        fe.cyl("rust", 2.4 * VS, VS, (c(x), c(3), 0), "x", 18, 0.006)
        for k in range(6):
            an = k / 6 * math.tau
            fe.cyl("chrome", 0.012, VS + 0.02, (c(x), c(3) + math.cos(an) * 0.15, math.sin(an) * 0.15), "x", 6, 0)
    fe.box2("steel", (lo(-1), lo(4), lo(-1)), (hi(1), hi(7), hi(1)), bevel=0.02)
    fe.cyl("chrome", 0.016, hi(9) - lo(8), (0, (lo(8) + hi(9)) / 2, 0), "y", 8, 0.0)
    fe.torus("crimson", 2.6 * VS, 0.018, (0, c(10), 0), "y", 24, 6)
    for k in range(2):
        fe.box("crimson", (0.42, 0.02, 0.03), (0, c(10), 0), r=(0, 90 * k + 45, 0), bevel=0.004)
    fe.cyl("crimson", 0.03, 0.04, (0, c(10), 0), "y", 10, 0.004)
    return a


def biogas_digester():
    a = Asset("biogas_digester", "Utility", kind="building")
    s, w, cl, fe, g = a.p("Scrap"), a.p("Wood"), a.p("Clay"), a.p("Iron"), a.p("Glass")
    R = 9.5 * VS
    s.lathe("rig_green", [(0.0, lo(0)), (R, lo(0)), (R, hi(8)), (0.0, hi(8))], (0, 0, 0), "y", 32)
    s.lathe("green", [(R - 0.01, hi(8)), (R - 0.05, hi(8) + 0.12), (R * 0.6, hi(12)), (0.15, hi(13) + 0.02), (0.0, hi(13) + 0.02)], (0, 0, 0), "y", 32)
    for y in (3, 7):
        s.torus("rust", R + 0.01, 0.018, (0, c(y), 0), "y", 32, 5)
    w.rod(K.wood("", "y"), (c(-12), c(2), 0), (c(-8.3), c(8.6), 0), 1.6 * VS, 10)
    for k in range(3):
        y0 = lo(0) + k * 0.107
        for (z0, z1, x0, x1) in ((lo(-2), lo(-2) + 0.04, lo(-14), hi(-11)), (hi(2) - 0.04, hi(2), lo(-14), hi(-11)), (lo(-2), hi(2), lo(-14), lo(-14) + 0.04), (lo(-2), hi(2), hi(-11) - 0.04, hi(-11))):
            w.box2(K.WOODS["x"][k % 3], (x0, y0 + 0.003, z0), (x1, y0 + 0.104, z1), bevel=0.004)
    cl.box2("soil", (lo(-13) - 0.02, hi(2), lo(-1)), (hi(-12) + 0.02, hi(3), hi(1)), bevel=0.02)
    fe.cyl("steel", 0.04, hi(16) - lo(14), (0, (lo(14) + hi(16)) / 2, 0), "y", 10, 0.004)
    fe.tube("hazard", [(0, c(16), 0), (c(9), c(16), 0), (c(9), lo(0) + 0.05, 0)], 0.035, 10)
    for k in range(4):
        fe.torus("steel_dark", 0.04, 0.008, (c(1 + k * 2), c(16), 0), "x", 10, 4)
    g.cyl("white", 0.06, 0.03, (c(2.5), c(14.5), c(1.2)), "z", 14, 0.006)
    fe.box2("cloth_olive", (lo(8), lo(0), lo(-3)), (hi(11), hi(1) - 0.04, hi(-1)), bevel=0.02)
    fe.cyl("rust", 0.08, 0.24, (c(9), c(2), c(-2)), "x", 10, 0.006)
    return a


def biogas_generator():
    a = Asset("biogas_generator", "Utility")
    fe, cu, s, rb, g = a.p("Iron"), a.p("Copper"), a.p("Scrap"), a.p("Rubber"), a.p("Glass")
    fe.box2("steel_dark", (lo(-8), lo(0), lo(-4)), (hi(8), hi(1), hi(4)), bevel=0.012)
    fe.box2("green", (lo(-7), lo(2), lo(-3)), (hi(1), hi(8), hi(3)), bevel=0.03)
    for k in range(6):
        fe.box2("steel_dark", (lo(-6) + k * 0.12, hi(8) - 0.01, lo(-2)), (lo(-6) + k * 0.12 + 0.04, hi(8) + 0.03, hi(2)), bevel=0.004)
    fe.cyl("steel", 2.7 * VS, hi(6) - lo(2), ((lo(2) + hi(6)) / 2, c(4.5), 0), "x", 22, 0.02)
    cu.box2("copper", (lo(7), lo(4), lo(0)), (hi(7), hi(5), hi(1)), bevel=0.008)
    s.box2("rust", (lo(-6), lo(9), lo(-2)), (hi(-2), hi(10), hi(2)), bevel=0.015)
    s.cyl("steel_dark", 0.05, hi(14) - lo(9), (c(-1), (lo(9) + hi(14)) / 2, c(2)), "y", 10, 0.004)
    s.lathe("rust", [(0.0, 0.0), (0.07, 0.0), (0.05, 0.05), (0.0, 0.06)], (c(-1), hi(14), c(2)), "y", 10)
    s.tube("hazard", [(c(-8), c(4), c(-2)), (c(-8), c(9), c(-2))], 0.025, 8)
    s.box2("ochre", (lo(-9), lo(6), lo(-3)), (hi(-8), hi(7), hi(-1)), bevel=0.01)
    s.cyl("lamp_red", 0.018, 0.02, (c(-9), c(8), c(-2)), "y", 8, 0.002)
    rb.superloft("rubber", [(lo(-3), 0, c(4), 0.15, 0.08), (lo(-3) + 0.05, 0, c(4), 0.2, 0.2), (hi(3) - 0.05, 0, c(4), 0.2, 0.2), (hi(3), 0, c(4), 0.15, 0.08)], 18, 2.0, axis="z")
    g.cyl("lamp_amber", 0.014, 0.01, (c(1), c(8), hi(3)), "z", 8, 0)
    return a


def freezer():
    a = Asset("freezer", "Furniture")
    s = a.p("Scrap")
    s.box2("white", (lo(-7), lo(0) + 0.04, lo(-4)), (hi(7), lo(8) - 0.004, hi(4)), bevel=0.03)
    s.box2("black", (lo(-7) + 0.03, lo(0), lo(-4) + 0.03), (hi(7) - 0.03, lo(0) + 0.05, hi(4) - 0.03), bevel=0.004)
    s.box2("white", (lo(-7) - 0.006, lo(8), lo(-4) - 0.006), (hi(7) + 0.006, hi(11), hi(4) + 0.006), bevel=0.03)
    s.box2("cream", (lo(-7), lo(8) - 0.006, lo(-4)), (hi(7), lo(8) + 0.004, hi(4)), bevel=0)
    s.tube("chrome", [(c(-2), c(9), hi(4)), (c(-2), c(9), hi(5)), (c(2), c(9), hi(5)), (c(2), c(9), hi(4))], 0.012, 6)
    for k in range(4):
        s.box2("steel_dark", (lo(3), lo(1) + k * 0.075, hi(4) - 0.01), (hi(6), lo(1) + k * 0.075 + 0.035, hi(4) + 0.004), bevel=0.002)
    s.cyl("cloth_pale", 0.03, 0.02, (c(-5), c(4.5), hi(4) + 0.01), "z", 12, 0.003)
    s.cyl("lamp_amber", 0.012, 0.01, (c(-3), c(5), hi(4) + 0.004), "z", 8, 0)
    return a


def ice_box():
    a = Asset("ice_box", "Furniture")
    w, s = a.p("Wood"), a.p("Scrap")
    for k in range(3):
        y0 = lo(0) + k * (hi(8) - lo(0)) / 3
        w.box2(K.WOODS["x"][k % 3], (lo(-6), y0 + 0.004, lo(-4)), (hi(6), y0 + (hi(8) - lo(0)) / 3, hi(4)), bevel=0.012)
    s.box2("steel", (lo(-6) - 0.005, lo(9), lo(-4) - 0.005), (hi(6) + 0.005, hi(9), hi(4) + 0.005), bevel=0.012)
    for x in (-6, 6):
        for z in (-4, 4):
            s.box2("steel_dark", (c(x) - 0.045, lo(0), c(z) - 0.045), (c(x) + 0.045, hi(9) - 0.01, c(z) + 0.045), bevel=0.006)
    s.box2("chrome", (lo(-1), lo(7), hi(4)), (hi(1), hi(7), hi(4) + 0.02), bevel=0.006)
    s.rod("chrome", (c(5), c(1), hi(4)), (c(5), c(1), hi(5)), 0.012, 6)
    s.sphere("water", 0.01, (c(5), lo(1), hi(5) + 0.02), (1, 1.5, 1), 6, 4)
    return a


def root_cellar():
    a = Asset("root_cellar", "Furniture")
    st, w, s = a.p("Stone"), a.p("Wood"), a.p("Scrap")
    # earth bank stepping in to a sod top (smoothed), stone face with a door frame
    secs = []
    for (y, hx, hz0, hz1) in ((lo(0), hi(11), lo(-11), hi(7)), (c(3), hi(8.5), lo(-8.5), hi(6)), (c(6), hi(6.5), lo(-6), hi(3)), (hi(7), hi(5), lo(-5), hi(1))):
        secs.append([(-hx, y, hz0), (hx, y, hz0), (hx, y, hz1), (-hx, y, hz1)])
    st.loft("soil_dry", secs)
    st.box2("leaf", (lo(-5), hi(7) - 0.01, lo(-5)), (hi(5), hi(7) + 0.03, hi(1)), bevel=0.03)
    r = K.rng(4198)
    for k in range(20):
        x, z = r.uniform(-0.38, 0.38), r.uniform(-0.38, 0.06)
        w.rod("fern", (x, hi(7) + 0.02, z), (x + r.uniform(-0.03, 0.03), hi(7) + r.uniform(0.05, 0.12), z), 0.006, 4)
    for k in range(9):                                     # stone face (dry-laid blocks)
        row, col = k // 3, k % 3
        for side in (-1, 1):
            x0 = side * (hi(4) + col * 0.11)
            x1 = x0 + side * 0.105
            st.box2(["stone", "rock_grey", "rock"][(k + side) % 3], (min(x0, x1), lo(0) + row * 0.18, lo(7)), (max(x0, x1), lo(0) + row * 0.18 + 0.175, hi(8)), bevel=0.02, segs=1)
    st.box2("stone", (lo(-7), lo(5), lo(7)), (hi(7), hi(6), hi(8)), bevel=0.02)
    for k in range(5):                                     # slanted plank doors
        y = lo(0) + k * 0.08
        w.box2(K.WOODS["x"][k % 3], (lo(-4), y, lo(7) + (4 - k) / 2 * VS), (hi(4), y + 0.08, hi(7) + (4 - k) / 2 * VS + 0.02), bevel=0.006)
    w.box2(W("dark", "y"), (lo(0) + 0.02, lo(0), lo(7)), (hi(0) - 0.02, hi(4), hi(9)), bevel=0.004)
    for x in (-1, 1):
        s.torus("chrome", 0.03, 0.006, (c(x), c(2), hi(9)), "z", 10, 4)
    s.cyl("steel_dark", 0.04, hi(9) - lo(7), (c(-3), (lo(7) + hi(9)) / 2, c(-3)), "y", 10, 0.004)
    s.lathe("steel_dark", [(0.0, 0.0), (0.07, 0.0), (0.0, 0.04)], (c(-3), hi(9), c(-3)), "y", 10)
    return a


TILES = [("furn_water_sea", "Solar still, desalinator, water valve", [lambda: [solar_still(), desalinator(), water_valve()]]),
         ("furn_power_control", "Power switch, power timer, light sensor, float switch, load breaker", [lambda: [switch_box("power_switch", 0), switch_box("power_timer", 1), switch_box("light_sensor", 2), switch_box("float_switch", 3), breaker()]]),
         ("furn_biogas", "Biogas digester, biogas generator", [lambda: [biogas_digester(), biogas_generator()]]),
         ("furn_cold", "Chest freezer, ice box, root cellar", [lambda: [freezer(), ice_box(), root_cellar()]])]

if __name__ == "__main__":
    run(TILES, "utilities2_")
