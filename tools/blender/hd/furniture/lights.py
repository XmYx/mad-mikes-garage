"""HD lighting pieces of FurnitureLibrary.Lights.cs: light switch (+ Lever at (0, 0.06, 0)), wall lamp, strip light,
pendant, highway (cobra) lamp, lantern post, pole lamp, floodlight mast, solar lamp. Each lamp's glass is its own
"Bulb" object (the game swaps it to an unlit glowing material), like the voxel "bulb" label. Wall / ceiling fixtures
are authored in the XZ plane with +Y out of the surface.

blender -b -P lights.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fcommon import K, Asset, Vector, Matrix, lo, hi, c, W, knob, screws, leg_square, drawer_front, bulb, run, VS  # noqa: E402


def light_switch():
    a = Asset("light_switch", "Furniture")
    s = a.p("Scrap")
    s.box2("enamel", (lo(-1), lo(0), lo(-2)), (hi(1), hi(0) - 0.02, hi(2)), bevel=0.012)
    for z in (-2, 2):
        s.cyl("chrome", 0.012, 0.006, (0, hi(0) - 0.02, c(z) * 0.8), "y", 8, 0.002)
    s.box2("cream", (-0.03, hi(0) - 0.022, -0.05), (0.03, hi(0) - 0.012, 0.05), bevel=0.003)
    lv = a.p("Lever", "Scrap", pivot=(0, 0.06, 0), mover=True)
    lv.box2("white", (-0.016, 0.06 - 0.02, -0.016), (0.016, 0.06 + 0.1, 0.016), bevel=0.008)
    return a


def light_wall():
    a = Asset("light_wall", "Furniture")
    cu, b = a.p("Copper"), a.p("Bulb", "Glass")
    cu.box2("brass", (lo(-1), lo(0), lo(-1)), (hi(1), hi(0), hi(1)), bevel=0.012)
    cu.tube("brass", [(0, hi(0), c(-1)), (0, c(1.5), c(-1)), (0, c(2), c(-0.6))], 0.012, 8)
    cu.lathe("brass", [(0.0, 0.0), (0.04, 0.0), (0.12, 0.08), (0.14, 0.1), (0.12, 0.1), (0.035, 0.012), (0.0, 0.012)], (0, c(2) - 0.04, c(1) - 0.04), "z", 18, r=(-90, 0, 0))
    b.lathe("lamp", [(0.0, 0.0), (0.03, 0.0), (0.075, 0.06), (0.07, 0.14), (0.03, 0.2), (0.0, 0.21)], (0, lo(2), c(1)), "y", 16)
    return a


def light_strip():
    a = Asset("light_strip", "Furniture")
    s, b = a.p("Scrap"), a.p("Bulb", "Glass")
    s.box2("white", (lo(-2), lo(0), lo(-7)), (hi(2), hi(0), hi(7)), bevel=0.012)
    for z in (-7, 7):
        s.box2("steel", (lo(-2), hi(0), lo(z)), (hi(2), hi(1), hi(z)), bevel=0.01)
    b.cyl("lamp", 0.035, hi(6) - lo(-6), (0, hi(0) + 0.04, 0), "z", 14, 0.006)
    s.box2("glass_clear", (lo(-2) + 0.02, hi(0), lo(-6)), (hi(2) - 0.02, hi(1), hi(6)), bevel=0.01)
    return a


def light_pendant():
    a = Asset("light_pendant", "Furniture")
    s, b = a.p("Scrap"), a.p("Bulb", "Glass")
    s.cyl("steel", 0.1, 0.04, (0, lo(0) + 0.02, 0), "y", 14, 0.006)
    s.rod("black", (0, lo(0) + 0.04, 0), (0, lo(8) + 0.03, 0), 0.006, 6)
    s.lathe("green", [(0.0, 0.0), (0.04, 0.0), (0.07, 0.03), (0.24, 0.12), (0.26, 0.15), (0.24, 0.15), (0.07, 0.045), (0.0, 0.045)], (0, lo(8), 0), "y", 24)
    s.lathe("enamel", [(0.0, 0.002), (0.065, 0.032), (0.23, 0.12), (0.0, 0.03)], (0, lo(8) + 0.008, 0), "y", 24)
    b.lathe("lamp_amber", [(0.0, 0.0), (0.03, 0.0), (0.06, 0.05), (0.05, 0.11), (0.0, 0.13)], (0, lo(9) - 0.0, 0), "y", 14)
    return a


def streetlamp_cobra():
    a = Asset("streetlamp_cobra", "Utility")
    cc, fe, b = a.p("Concrete"), a.p("Iron"), a.p("Bulb", "Glass")
    cc.box2("conc_light", (lo(-3), lo(0), lo(-2)), (hi(2), hi(2), hi(3)), bevel=0.02)
    fe.lathe("steel", [(0.0, hi(2)), (0.14, hi(2)), (0.14, hi(2) + 0.03), (0.1, hi(6)), (0.075, hi(6) + 0.04), (0.06, c(84)), (0.0, c(84))], (c(-0.5), 0, c(-0.5)), "y", 16)
    for k in range(4):
        an = k * math.pi / 2 + math.pi / 4
        fe.cyl("chrome", 0.014, 0.04, (c(-0.5) + math.cos(an) * 0.11, hi(2) + 0.03, c(-0.5) + math.sin(an) * 0.11), "y", 6, 0.002)
    fe.box2("steel_dark", (c(-0.5) - 0.05, c(40), c(-0.5) + 0.07), (c(-0.5) + 0.05, c(43), c(-0.5) + 0.09), bevel=0.006)
    pts = [(c(-0.5), c(83), c(-0.5))]
    for k in range(1, 13):
        t = k / 12
        pts.append((c(-0.5), c(84) + math.sin(t * math.pi / 2) * c(6.5), c(-0.5) + (1 - math.cos(t * math.pi / 2)) * c(8.5)))
    pts.append((c(-0.5), c(91), c(23)))
    fe.tube("steel", pts, 0.05, 10)
    fe.superloft("chrome", [(c(21.5), c(-0.5), c(90.2), 0.08, 0.06), (c(23), c(-0.5), c(90), 0.17, 0.12), (c(30), c(-0.5), c(90), 0.16, 0.12), (hi(32), c(-0.5), c(90.2), 0.06, 0.05)], 18, 2.4)
    b.superloft("lamp_amber", [(c(23), c(-0.5), c(88.6), 0.08, 0.02), (c(24), c(-0.5), c(88.2), 0.12, 0.06), (c(30), c(-0.5), c(88.4), 0.11, 0.05), (c(31), c(-0.5), c(88.8), 0.06, 0.02)], 14, 2.2)
    return a


def streetlamp_gas():
    a = Asset("streetlamp_gas", "Utility")
    fe, b = a.p("Iron"), a.p("Bulb", "Glass")
    X = c(-0.5)
    fe.lathe("black", [(0.0, lo(0)), (0.17, lo(0)), (0.17, 0.05), (0.14, 0.12), (0.15, hi(3) - 0.03), (0.09, hi(3)), (0.0, hi(3))], (X, 0, X), "y", 16)
    prof = [(0.07, hi(3))]
    for y in range(4, 31, 2):
        prof.append((0.07 - 0.015 * (y / 30) + (0.008 if y in (12, 28) else 0), c(y)))
    prof.append((0.0, c(30)))
    fe.lathe("black", [(0.0, hi(3))] + prof, (X, 0, X), "y", 12)
    for k in range(12):                                     # flutes
        an = k / 12 * math.tau
        fe.rod("black", (X + math.cos(an) * 0.066, c(5), X + math.sin(an) * 0.066), (X + math.cos(an) * 0.058, c(27), X + math.sin(an) * 0.058), 0.007, 4)
    for y in (12, 28):
        fe.torus("black", 0.08, 0.016, (X, c(y), X), "y", 14, 5)
    fe.rod("black", (lo(-4), c(30), X), (hi(3), c(30), X), 0.014, 6)            # ladder bar
    fe.box2("black", (lo(-3), lo(31), lo(-3)), (hi(2), hi(31), hi(2)), bevel=0.01)
    for x in (-3, 2):
        for z in (-3, 2):
            fe.box2("black", (lo(x) + 0.02, lo(32), lo(z) + 0.02), (hi(x) - 0.02, hi(37), hi(z) - 0.02), bevel=0.004)
    fe.add(K.kit.bm_loft([[(lo(-4), lo(38), lo(-4)), (hi(3), lo(38), lo(-4)), (hi(3), lo(38), hi(3)), (lo(-4), lo(38), hi(3))],
                          [(lo(-1), hi(40), lo(-1)), (hi(0), hi(40), lo(-1)), (hi(0), hi(40), hi(0)), (lo(-1), hi(40), hi(0))]]), "black")
    fe.lathe("brass", [(0.0, hi(40)), (0.03, hi(40)), (0.04, c(41.5)), (0.0, hi(42))], (X, 0, X), "y", 10)
    b.box2("lamp_amber", (lo(-2) + 0.01, lo(32), lo(-3) + 0.01), (hi(1) - 0.01, hi(37), hi(2) - 0.01), bevel=0.004)
    return a


def streetlamp_pole():
    a = Asset("streetlamp_pole", "Utility")
    w, s, g, b = a.p("Wood"), a.p("Scrap"), a.p("Glass"), a.p("Bulb", "Glass")
    X = c(-0.5)
    w.add(K.kit.bm_cyl(0.085, hi(64) - lo(0), 12, 0.006, 0.07), W("dark", "y"), Matrix.Translation((X, (hi(64) + lo(0)) / 2, X)) @ K._axis_mtx("y"))
    w.box2(W("", "z"), (lo(-1), lo(58), lo(1)), (hi(0), hi(59), hi(14)), bevel=0.012)
    w.beam(W("", "y"), (X, c(50), c(0.5)), (X, c(58), c(8)), 0.06, 0.05)
    for z in (4, 10):
        g.lathe("glass", [(0.0, 0.0), (0.025, 0.0), (0.035, 0.03), (0.025, 0.05), (0.03, 0.07), (0.0, 0.09)], (X, hi(59), c(z)), "y", 10)
    s.rod("black", (X, hi(59), c(10)), (c(0), c(53), c(13)), 0.005, 4)
    s.rod("black", (c(0), c(57), c(13)), (c(0), c(53), c(13)), 0.005, 4)
    s.lathe("green", [(0.0, 0.0), (0.03, 0.0), (0.06, -0.02), (0.18, -0.09), (0.19, -0.11), (0.17, -0.11), (0.05, -0.035), (0.0, -0.035)], (c(0), c(52.6), c(13)), "y", 20)
    b.lathe("lamp_amber", [(0.0, 0.0), (0.025, 0.0), (0.05, -0.04), (0.045, -0.09), (0.0, -0.11)], (c(0), c(52), c(13)), "y", 12)
    return a


def floodlight_mast():
    a = Asset("floodlight_mast", "Utility")
    cc, fe, b = a.p("Concrete"), a.p("Iron"), a.p("Bulb", "Glass")
    cc.box2("conc_light", (lo(-5), lo(0), lo(-5)), (hi(5), hi(1), hi(5)), bevel=0.02)
    for x in (-3, 3):
        for z in (-3, 3):
            fe.box2("ochre", (lo(x) + 0.01, lo(2), lo(z) + 0.01), (hi(x) - 0.01, hi(96), hi(z) - 0.01), bevel=0.008)
            fe.cyl("steel_dark", 0.05, 0.02, (c(x), lo(2) + 0.01, c(z)), "y", 8, 0.002)
    for y in range(8, 93, 7):
        for (p0, p1) in (((-3, -3), (3, -3)), ((3, -3), (3, 3)), ((3, 3), (-3, 3)), ((-3, 3), (-3, -3))):
            fe.rod("ochre", (c(p0[0]), c(y), c(p0[1])), (c(p1[0]), c(y), c(p1[1])), 0.012, 5)
        if y < 92:
            for (p0, p1) in (((-3, -3), (3, -3)), ((3, -3), (3, 3)), ((3, 3), (-3, 3)), ((-3, 3), (-3, -3))):
                fe.rod("rust", (c(p0[0]), c(y), c(p0[1])), (c(p1[0]), c(y + 7), c(p1[1])), 0.007, 4)
    fe.rod("black", (c(1), lo(2), c(-2)), (c(1), c(95), c(-2)), 0.012, 6)
    fe.box2("steel", (lo(-5), lo(97), lo(-5)), (hi(5), hi(97), hi(5)), bevel=0.01)
    for x in (-5, 5):
        for z in (-5, 5):
            fe.rod("steel_dark", (c(x), hi(97), c(z)), (c(x), hi(101), c(z)), 0.012, 5)
    for (x0, x1) in ((-6, -1), (1, 6)):
        fe.box2("chrome", (lo(x0), lo(98), lo(2)), (hi(x1), hi(103), hi(5)), bevel=0.02, r=(-10, 0, 0))
        b.box2("lamp", (lo(x0) + 0.04, lo(99), hi(5)), (hi(x1) - 0.04, hi(102), hi(6) - 0.02), bevel=0.01, r=(-10, 0, 0))
    return a


def streetlamp_solar():
    a = Asset("streetlamp_solar", "Utility")
    cc, fe, g, b = a.p("Concrete"), a.p("Iron"), a.p("Glass"), a.p("Bulb", "Glass")
    X = c(-0.5)
    cc.box2("conc_light", (lo(-2), lo(0), lo(-2)), (hi(1), hi(1), hi(1)), bevel=0.012)
    fe.cyl("chrome", 0.05, hi(46) - lo(2), (X, (lo(2) + hi(46)) / 2, X), "y", 12, 0.004)
    fe.box2("black", (lo(-3), lo(14), lo(-4)), (hi(2), hi(20), hi(-2)), bevel=0.02)
    for y in range(15, 20, 2):
        fe.box2("steel_dark", (lo(-3) - 0.004, c(y) - 0.006, lo(-4) - 0.004), (hi(2) + 0.004, c(y) + 0.006, hi(-2) + 0.004), bevel=0)
    fe.cyl("lamp_amber", 0.014, 0.01, (X, c(18), lo(-4) - 0.005), "z", 8, 0.0)
    fe.tube("chrome", [(X, c(44), X), (X, c(44), c(3)), (X, c(43.3), c(7))], 0.025, 8)
    fe.box2("black", (lo(-2), lo(42), lo(6)), (hi(1), hi(43), hi(11)), bevel=0.015)
    b.box2("lamp", (lo(-1), lo(41), lo(7)), (hi(0), lo(42) + 0.01, hi(10)), bevel=0.006)
    fe.cyl("chrome", 0.03, 0.12, (X, c(47.5), X), "y", 8, 0.003)
    ang = math.degrees(math.atan2(4 * VS, 12 * VS))
    g.box("alu", (hi(5) - lo(-6), 0.03, 13 * VS), (c(-0.5), c(50.5), c(-1)), r=(-ang, 0, 0), bevel=0.006)
    g.box("solar", (hi(5) - lo(-6) - 0.04, 0.008, 13 * VS - 0.04), (c(-0.5), c(50.5) + 0.016, c(-1)), r=(-ang, 0, 0), bevel=0)
    return a


TILES = [("furn_lights_indoor", "Light switch (+Lever), wall lamp, strip light, pendant lamp", [lambda: [light_switch(), light_wall(), light_strip(), light_pendant()]]),
         ("furn_lights_street", "Highway lamp, lantern post, pole lamp, floodlight mast, solar lamp", [lambda: [streetlamp_cobra(), streetlamp_gas(), streetlamp_pole(), floodlight_mast(), streetlamp_solar()]])]

if __name__ == "__main__":
    run(TILES, "lights_")
