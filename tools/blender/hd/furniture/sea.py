"""HD sea pieces of FurnitureLibrary.Sea.cs: slipway, air compressor, O2 rack, sea dome, sea tunnel, shore entrance
(0.16 m templates: Shell = pressure hull / walls, Detail, Glass) and the docking collar.

blender -b -P sea.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fcommon import K, Asset, Vector, Matrix, lo, hi, c, W, knob, screws, leg_square, drawer_front, bulb, run, VS  # noqa: E402

V2 = 0.16


def L(i):
    return lo(i, V2)


def H(i):
    return hi(i, V2)


def C(i):
    return c(i, V2)


def sea_dome():
    a = Asset("sea_dome", "Structure", kind="building", voxel=V2)
    sh, de, g = a.p("Shell", "Iron"), a.p("Detail", "Iron"), a.p("Glass")
    R = 22 * V2 + 0.06
    sh.cyl("steel_dark", (21 + 0.5) * V2, V2, (0, 0, 0), "y", 48, 0.01)                               # floor
    for k in range(-3, 4):
        hl = math.sqrt(max(0.0, (3.3) ** 2 - (k * 6 * V2) ** 2))
        de.box2("steel", (k * 6 * V2 - 0.02, H(0) - 0.005, -hl), (k * 6 * V2 + 0.02, H(0) + 0.006, hl), bevel=0)
        de.box2("steel", (-hl, H(0) - 0.005, k * 6 * V2 - 0.02), (hl, H(0) + 0.006, k * 6 * V2 + 0.02), bevel=0)
    # hemisphere shell in latitude bands, door gap at +z (|x| <= 4, y <= 14)
    nb, ns = 12, 40
    door_a = math.asin(min(1.0, 4.5 * V2 / R))
    for i in range(nb):
        t0, t1 = i / nb * math.pi / 2, (i + 1) / nb * math.pi / 2
        for j in range(ns):
            p0, p1 = j / ns * math.tau, (j + 1) / ns * math.tau
            pm = (p0 + p1) / 2
            ym = R * math.sin((t0 + t1) / 2)
            xm, zm = R * math.cos((t0 + t1) / 2) * math.cos(pm), R * math.cos((t0 + t1) / 2) * math.sin(pm)
            if zm > 0 and abs(xm) < 4.5 * V2 and ym < 14.5 * V2:
                continue
            quad = []
            for (t, p) in ((t0, p0), (t0, p1), (t1, p1), (t1, p0)):
                quad.append(Vector((R * math.cos(t) * math.cos(p), R * math.sin(t), R * math.cos(t) * math.sin(p))))
            port = 9 * V2 <= ym <= 11.5 * V2 and (j % 5 == 2)
            n = sum(quad, Vector()) / 4
            n.normalize()
            inner = [q - n * 0.1 for q in quad]
            mat = "steel" if i > 1 else ("hazard" if ((j // 2) % 2 == 0) else "black")
            (g if port else sh).add(K.kit.bm_loft([[tuple(q) for q in inner], [tuple(q) for q in quad]]), "glass" if port else mat)
            if port:
                de.add(K.kit.bm_torus(0.13, 0.025, 14, 5), "brass", Matrix.Translation(sum(quad, Vector()) / 4) @ n.to_track_quat("Z", "Y").to_matrix().to_4x4())
    for i in range(2, nb, 3):                               # rivet rows
        t = i / nb * math.pi / 2
        for j in range(0, 40):
            p = j / 40 * math.tau
            q = Vector((R * math.cos(t) * math.cos(p), R * math.sin(t), R * math.cos(t) * math.sin(p)))
            if q.z > 0 and abs(q.x) < 4.6 * V2 and q.y < 14.6 * V2:
                continue
            de.sphere("chrome", 0.025, tuple(q * 1.004), (1, 1, 1), 6, 4)
    # door frame (ochre) around the opening
    for x in (-5, 5):
        de.box2("ochre", (C(x) - 0.09, H(0), R * math.cos(math.asin(min(1, abs(C(x)) / R))) - 0.3), (C(x) + 0.09, H(15), R * math.cos(math.asin(min(1, abs(C(x)) / R))) + 0.05), bevel=0.02)
    de.box2("ochre", (C(-5) - 0.09, H(14), R * 0.92 - 0.3), (C(5) + 0.09, H(15) + 0.06, R * 0.92 + 0.05), bevel=0.02)
    de.cyl("lamp", 0.16, 0.05, (0, R - 0.12, 0), "y", 16, 0.01)
    return a


def sea_tunnel():
    a = Asset("sea_tunnel", "Structure", kind="building", voxel=V2)
    sh, de, g = a.p("Shell", "Iron"), a.p("Detail", "Iron"), a.p("Glass")
    R, Cy = 10 * V2 + 0.08, 10 * V2
    z0, z1 = L(-19), H(19)
    ns = 36
    for j in range(ns):                                     # tube shell above the ground line
        p0, p1 = j / ns * math.tau, (j + 1) / ns * math.tau
        pts = [Vector((math.cos(p) * R, Cy + math.sin(p) * R, 0)) for p in (p0, p1)]
        if min(q.y for q in pts) < H(0) - 0.08:
            continue
        for k in range(5):
            za = z0 + k * (z1 - z0) / 5
            zb = za + (z1 - z0) / 5
            pm = (p0 + p1) / 2
            port = (k in (1, 3)) and abs(math.sin(pm)) < 0.2 and abs(math.cos(pm)) > 0.9
            nrm = Vector((math.cos(pm), math.sin(pm), 0))
            outer = [pts[0] + Vector((0, 0, za)), pts[1] + Vector((0, 0, za)), pts[1] + Vector((0, 0, zb)), pts[0] + Vector((0, 0, zb))]
            inner = [q - nrm * 0.1 for q in outer]
            (g if port else sh).add(K.kit.bm_loft([[tuple(q) for q in inner], [tuple(q) for q in outer]]), "glass" if port else "steel")
    for zi in range(-19, 20, 8):                            # ribs
        pts = [(math.cos(k / 32 * math.tau) * (R + 0.02), Cy + math.sin(k / 32 * math.tau) * (R + 0.02)) for k in range(33)]
        for (p0, p1) in zip(pts, pts[1:]):
            if min(p0[1], p1[1]) < H(0) - 0.08:
                continue
            de.beam("steel_dark", (p0[0], p0[1], C(zi)), (p1[0], p1[1], C(zi)), 0.14, 0.12, bevel=0.0)
    sh.box2("steel_dark", (L(-7), L(1), z0), (H(7), H(1), z1), bevel=0.01)                              # walkway
    for zi in range(-19, 20, 4):
        de.box2("black", (L(-7) + 0.04, H(1) - 0.004, C(zi) - 0.03), (H(7) - 0.04, H(1) + 0.004, C(zi) + 0.03), bevel=0)
    for x in (-7, 7):
        de.box2("hazard", (C(x) - 0.05, H(1), z0), (C(x) + 0.05, H(1) + 0.01, z1), bevel=0)
    de.cyl("lamp", 0.1, 0.04, (0, Cy + R - 0.12, 0), "y", 14, 0.006)
    de.rod("steel_dark", (R - 0.25, Cy + 0.8, z0), (R - 0.25, Cy + 0.8, z1), 0.03, 8)
    de.rod("crimson", (R - 0.35, Cy + 0.95, z0), (R - 0.35, Cy + 0.95, z1), 0.02, 8)
    return a


def shore_entrance():
    a = Asset("shore_entrance", "Structure", kind="building", voxel=V2)
    sh, de, cc = a.p("Shell", "Iron"), a.p("Detail", "Iron"), a.p("Concrete")
    R = 10 * V2 + 0.08
    axis = lambda z: 10.0 - max(0.0, z) * 0.34
    zs = list(range(0, 101, 5))
    ns = 32
    for zi0, zi1 in zip(zs, zs[1:]):
        for j in range(ns):
            p0, p1 = j / ns * math.tau, (j + 1) / ns * math.tau
            quad = []
            ok = True
            for (zi, p) in ((zi0, p0), (zi0, p1), (zi1, p1), (zi1, p0)):
                cy = axis(zi) * V2
                y = cy + math.sin(p) * R
                if y < (axis(zi) - 8) * V2 - 0.1:
                    ok = False
                quad.append(Vector((math.cos(p) * R, y, C(zi))))
            if not ok:
                continue
            pm = (p0 + p1) / 2
            nrm = Vector((math.cos(pm), math.sin(pm), 0))
            inner = [q - nrm * 0.1 for q in quad]
            sh.add(K.kit.bm_loft([[tuple(q) for q in inner], [tuple(q) for q in quad]]), "steel" if (zi0 // 5) % 2 else "steel_dark")
    for zi in range(0, 101, 10):                            # ribs
        cy = axis(zi) * V2
        pts = [(math.cos(k / 28 * math.tau) * (R + 0.02), cy + math.sin(k / 28 * math.tau) * (R + 0.02)) for k in range(29)]
        for (p0, p1) in zip(pts, pts[1:]):
            if min(p0[1], p1[1]) < (axis(zi) - 8) * V2 - 0.05:
                continue
            de.beam("black", (p0[0], p0[1], C(zi)), (p1[0], p1[1], C(zi)), 0.14, 0.12, bevel=0.0)
    for zi0, zi1 in zip(zs, zs[1:]):                        # sloping ramp floor with grip strips
        y0, y1 = (axis(zi0) - 8) * V2, (axis(zi1) - 8) * V2
        sh.panel("steel_dark", (C(-7), y0, C(zi0)), (C(7), y0, C(zi0)), (C(7), y1, C(zi1)), 0.06)
        de.box2("ochre", (C(-7), y0 + 0.03, C(zi0) - 0.03), (C(7), y0 + 0.045, C(zi0) + 0.03), bevel=0)
    # the hut on the beach (concrete walls, ochre band, tin roof, red sign, door inland at -z)
    hut = [(L(-4), H(4), L(1), H(14))]
    K.slab_xy(cc, "conc_light", L(-11), H(11), L(1), H(18), L(-10), H(-10), hut, bevel=0.02)
    for x in (-11, 11):
        cc.box2("conc_light", (L(x), L(1), L(-10)), (H(x), H(18), H(0)), bevel=0.02)
    cc.box2("conc", (L(-11), L(0), L(-10)), (H(11), H(0), H(0)), bevel=0.02)
    for (x0, x1, z0, z1) in ((L(-11) - 0.005, H(11) + 0.005, L(-10) - 0.005, L(-10) + 0.01), (L(-11) - 0.005, L(-11) + 0.01, L(-10), H(0)), (H(11) - 0.01, H(11) + 0.005, L(-10), H(0))):
        de.box2("ochre", (x0, L(12), z0), (x1, H(12), z1), bevel=0)
    for i in range(6):
        K.corrugated(de, ["tin", "tin_rust"][i % 2], L(-12) + i * (H(12) - L(-12)) / 6 - 0.01, L(-12) + (i + 1) * (H(12) - L(-12)) / 6 + 0.01, L(-11), H(1), 0.025, 0.16, 0.006,
                     Matrix.Translation((0, C(19), 0)), samples_per_period=4)
    de.box2("crimson", (L(-3), L(16), L(-11)), (H(3), H(17), L(-11) + 0.04), bevel=0.01)
    de.box2("white", (L(-2), C(16.5) - 0.02, L(-11) - 0.003), (H(2), C(16.5) + 0.02, L(-11)), bevel=0)
    return a


def docking_collar():
    a = Asset("docking_collar", "Structure")
    fe = a.p("Iron")
    fe.lathe("steel", [(0.0, lo(0)), (hi(12), lo(0)), (hi(12), hi(1)), (hi(9), hi(1)), (hi(9), hi(8)), (hi(12), hi(8)), (hi(12), lo(9)), (hi(9) - 0.02, lo(9)), (hi(9) - 0.02, hi(1) + 0.01), (0.0, hi(1) + 0.01)], (0, 0, 0), "y", 40)
    fe.lathe("ochre", [(hi(9) - 0.02, lo(9)), (hi(12) + 0.01, lo(9)), (hi(12) + 0.01, hi(10) - 0.01), (hi(9) - 0.02, hi(10) - 0.01)], (0, 0, 0), "y", 40, close=False)
    fe.torus("rubber", hi(10.5) - 0.02, 0.035, (0, hi(10) - 0.01, 0), "y", 40, 6)
    for k in range(16):
        an = k / 16 * math.tau
        fe.cyl("chrome", 0.022, 0.04, (math.cos(an) * hi(11), hi(10), math.sin(an) * hi(11)), "y", 8, 0.004)
    for k in range(8):                                      # gussets
        an = k / 8 * math.tau + 0.2
        fe.box("steel_dark", (0.04, hi(8) - hi(1), 0.18), (math.cos(an) * (hi(12) + 0.0), (hi(1) + hi(8)) / 2, math.sin(an) * (hi(12) + 0.0)), r=(0, -math.degrees(an) + 90, 0), bevel=0.006)
    fe.torus("crimson", 3.3 * VS, 0.02, (0, c(11), 0), "y", 24, 6)                                       # hatch wheel
    for k in range(2):
        fe.box("crimson", (0.62, 0.03, 0.04), (0, c(11), 0), r=(0, 90 * k, 0), bevel=0.006)
    fe.cyl("steel_dark", 0.05, 0.05, (0, c(11), 0), "y", 12, 0.006)
    return a


def slipway():
    a = Asset("slipway", "Industry", kind="building", voxel=V2)
    w, fe = a.p("Wood"), a.p("Iron")
    ytop = lambda z: round(6 - z * 0.08)
    for zi in range(0, 76, 5):                             # sleepers
        y = ytop(zi)
        w.box2(K.WOODS["x"][(zi // 5) % 3], (L(-15), L(y), L(zi)), (H(15), H(y), H(zi + 1)), bevel=0.02)
    for zi in range(0, 76, 10):                            # piles
        y = ytop(zi)
        for x in (-15, 15):
            w.cyl(K.wood("dark", "y"), 0.1, H(y + 2) - L(y - 6), (C(x), (H(y + 2) + L(y - 6)) / 2, C(zi)), "y", 10, 0.01)
    for x in (-9, 9):                                      # rails on stringers
        pts0 = (C(x) + V2 / 2, L(ytop(0) + 1), L(0))
        pts1 = (C(x) + V2 / 2, L(ytop(75) + 1), H(75))
        fe.beam("steel", (pts0[0], pts0[1] + 0.05, pts0[2]), (pts1[0], pts1[1] + 0.05, pts1[2]), 0.1, 0.1, bevel=0.01)
        w.beam(K.wood("dark", "z"), (pts0[0], pts0[1] - 0.06, pts0[2]), (pts1[0], pts1[1] - 0.06, pts1[2]), 0.24, 0.14, bevel=0.01)
    w.box2(K.wood("", "y"), (L(-3), L(7), L(-4)), (H(3), H(16), H(-1)), bevel=0.03)                      # winch post
    fe.cyl("ochre", 2.5 * V2 + 0.04, H(5) - L(-5), (0, C(12), C(-2)), "x", 20, 0.02)                    # cable drum
    for x in (-5.4, 5.4):
        fe.cyl("steel_dark", 0.5, 0.04, (C(x), C(12), C(-2)), "x", 20, 0.006)
    fe.rod("black", (0, C(12) - 0.3, C(-2) + 0.25), (0, C(8), C(30)), 0.02, 6)
    fe.box2("steel_dark", (-0.2, L(5) + 0.1, C(30) - 0.15), (0.2, L(5) + 0.3, C(30) + 0.15), bevel=0.02)   # cradle shoe
    return a


def air_compressor():
    a = Asset("air_compressor", "Utility")
    fe, rb = a.p("Iron"), a.p("Rubber")
    fe.box2("steel_dark", (lo(-7), lo(0), lo(-5)), (hi(7), hi(1), hi(5)), bevel=0.012)
    R = 5 * VS
    fe.lathe("crimson", [(0.0, 0.0), (R * 0.6, 0.01), (R * 0.95, 0.08), (R, 0.2), (R, hi(4) - lo(-6) - 0.2), (R * 0.95, hi(4) - lo(-6) - 0.08), (R * 0.6, hi(4) - lo(-6) - 0.01), (0.0, hi(4) - lo(-6))],
             (lo(-6), c(6), 0), "x", 24)
    for x in (-4, 3):
        fe.box2("steel_dark", (c(x) - 0.04, hi(1), -0.25), (c(x) + 0.04, c(3), 0.25), bevel=0.006)
    fe.lathe("rig_green", [(0.0, lo(11)), (2.5 * VS + 0.04, lo(11)), (2.5 * VS + 0.04, hi(16) - 0.03), (0.06, hi(16)), (0.0, hi(16))], (c(-4), 0, 0), "y", 18)
    for k in range(6):                                      # motor cooling fins
        fe.torus("steel_dark", 2.5 * VS + 0.05, 0.008, (c(-4), lo(12) + k * 0.07, 0), "y", 18, 4)
    fe.cyl("white", 1.8 * VS, 0.04, (c(4), c(12), c(1)), "z", 18, 0.006)                                   # gauge
    fe.cyl("steel", 1.8 * VS + 0.01, 0.08, (c(4), c(12), c(0)), "z", 18, 0.006)
    fe.box2("black", (c(4) - 0.005, c(12), c(1.3)), (c(4) + 0.005, c(12) + 0.09, c(1.35)), bevel=0)
    fe.tube("steel", [(c(4), c(10.5), 0), (c(4), c(11.2), 0)], 0.02, 6)
    rb.tube("black", [(c(6), c(6), c(4)), (c(7.5), c(4), c(5.5)), (c(8.6), c(2), c(5.6)), (c(7), lo(1) + 0.03, c(4))], 0.03, 8)
    return a


def o2_rack():
    a = Asset("o2_rack", "Utility")
    fe = a.p("Iron")
    for x in (-8, 8):
        fe.box2("steel", (lo(x), lo(0), lo(-2)), (hi(x), hi(18), hi(-2)), bevel=0.008)
    for y in (1, 12):
        fe.box2("steel", (lo(-8), lo(y), lo(-2)), (hi(8), hi(y), hi(-2)), bevel=0.008)
    for x in (-5, -1, 3, 7):
        Rb = 1.6 * VS + 0.02
        fe.lathe("green", [(0.0, lo(2)), (Rb, lo(2)), (Rb, hi(14) - 0.05), (Rb * 0.5, hi(14) + 0.02), (0.03, hi(15)), (0.0, hi(15))], (c(x), 0, 0), "y", 18)
        fe.torus("cloth_cream", Rb + 0.002, 0.01, (c(x), c(6), 0), "y", 18, 4)
        fe.torus("cloth_cream", Rb + 0.002, 0.01, (c(x), c(12), 0), "y", 18, 4)
        fe.cyl("chrome", 0.03, 0.06, (c(x), hi(15) + 0.03, 0), "y", 10, 0.004)
        fe.cyl("chrome", 0.012, 0.06, (c(x), hi(15) + 0.04, 0.03), "z", 6, 0.0)
        fe.torus("steel_dark", Rb + 0.01, 0.008, (c(x), c(12), 0), "y", 18, 4)
    return a


TILES = [("furn_sea_dome", "Sea dome (riveted hemisphere, portholes, door)", [lambda: [sea_dome()]]),
         ("furn_sea_tunnel", "Sea tunnel + docking collar", [lambda: [sea_tunnel(), docking_collar()]]),
         ("furn_sea_shore", "Shore entrance (beach hut + sloping tube)", [lambda: [shore_entrance()]]),
         ("furn_sea_slipway", "Slipway, air compressor, O2 rack", [lambda: [slipway(), air_compressor(), o2_rack()]])]

if __name__ == "__main__":
    run(TILES, "sea_")
