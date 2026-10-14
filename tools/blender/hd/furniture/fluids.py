"""HD pieces of FurnitureLibrary.Fluids.cs (makeshift pool, clay pond, storage drum, pond liner, standpipe tap,
transfer pump) and FurnitureLibrary.Winter.cs (block heater). Origin = mounting point, +Z front; each fills the voxel
bounds of the grid it replaces (0.08 m voxels).

blender -b -P fluids.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fcommon import K, Asset, Vector, Matrix, lo, hi, c, W, knob, screws, run, VS  # noqa: E402,F401
from fcommon import drape  # noqa: E402


def pool_tarp():
    a = Asset("pool_tarp", "Utility")
    w, cl, s = a.p("Wood"), a.p("Cloth"), a.p("Scrap")
    h, wall = 14, 6
    for side in range(4):                                  # plank walls, two boards high
        for k in range(2):
            y0, y1 = lo(0) + k * (hi(wall) - lo(0)) / 2 + 0.004, lo(0) + (k + 1) * (hi(wall) - lo(0)) / 2 - 0.004
            m = K.WOODS["x" if side < 2 else "z"][(k + side) % 3]
            if side < 2:
                z = -h if side == 0 else h
                w.box2(m, (lo(-h), y0, lo(z)), (hi(h), y1, hi(z)), bevel=0.008)
            else:
                x = -h if side == 2 else h
                w.box2(m, (lo(x), y0, lo(-h) + 0.02), (hi(x), y1, hi(h) - 0.02), bevel=0.008)
    for x in (-h, h):                                       # corner posts
        for z in (-h, h):
            w.box2(W("dark", "y"), (lo(x) - 0.01, lo(0), lo(z) - 0.01), (hi(x) + 0.01, hi(wall + 1), hi(z) + 0.01), bevel=0.01)
    # tarp: floor + walls inside, folded over the rim, sagging a little between the ties
    cl.box2("cloth_blue", (lo(-h + 1), lo(0), lo(-h + 1)), (hi(h - 1), lo(0) + 0.02, hi(h - 1)), bevel=0.01)
    for side in range(4):
        n = 12
        secs = []
        for i in range(n + 1):
            t = -1 + 2 * i / n
            sag = 0.025 * abs(math.sin(t * math.pi * 2.0))
            u = t * (hi(h - 1) - 0.01)
            prof = [(lo(-h + 1) + 0.03, lo(0) + 0.02), (lo(-h + 1) + 0.006, lo(1)), (lo(-h + 1), hi(wall) - sag),
                    (lo(-h) + 0.02, hi(wall) + 0.03 - sag), (lo(-h) - 0.02, hi(wall) + 0.02 - sag), (lo(-h) - 0.035, hi(wall) - 0.1 - sag)]
            top = [(d, y) for d, y in prof]
            bot = [(d + 0.012 * (1 if j < 3 else -1), y - 0.012) for j, (d, y) in enumerate(reversed(prof))]
            ring = top + bot
            if side == 0:
                secs.append([(u, y, d) for d, y in ring])
            elif side == 1:
                secs.append([(u, y, -d) for d, y in ring])
            elif side == 2:
                secs.append([(d, y, u) for d, y in ring])
            else:
                secs.append([(-d, y, u) for d, y in ring])
        cl.loft("cloth_blue", secs)
    for i in range(-h, h + 1, 7):                           # rope ties over the rim
        s.torus("rope", 0.035, 0.008, (c(i), hi(wall) - 0.02, hi(h) + 0.03), "x", 10, 4)
    s.rod("chrome", (0, c(0.5), hi(h)), (0, c(0.5), hi(h + 2)), 0.02, 10)  # drain spigot, red valve
    s.cyl("pump_red", 0.035, 0.016, (0, c(1.3), hi(h + 2) - 0.03), "y", 12, 0.003)
    return a


def clay_pond():
    a = Asset("clay_pond", "Utility")
    cl, st, w = a.p("Clay"), a.p("Stone"), a.p("Wood")
    h = 19
    # bed (muddy) and puddled-clay banks: a ring loft, rounded on top
    cl.box2("soil", (lo(-h + 3), lo(0), lo(-h + 3)), (hi(h - 3), hi(0), hi(h - 3)), bevel=0.02)
    prof = [(0.0, lo(0)), (0.0, hi(1)), (0.06, hi(3)), (0.16, hi(4) + 0.02), (0.26, hi(4) + 0.01), (0.32, hi(3)), (0.32, lo(0))]
    n = 40
    secs = []
    for i in range(n):
        an = i / n * math.tau
        # superellipse ring (square-ish like the voxel ring)
        co, si = math.cos(an), math.sin(an)
        e = 6.0
        rr = 1.0 / (abs(co) ** e + abs(si) ** e) ** (1 / e)
        R0 = hi(h - 3) + 0.0
        wob = 0.02 * math.sin(an * 7 + 1.3)
        secs.append([((R0 + d) * rr * co, y + wob * (1 if y > lo(1) else 0), (R0 + d) * rr * si) for d, y in prof])
    secs.append(secs[0])
    cl.loft("clay", secs, close=False)
    r = K.rng(4313)
    for i in range(18):                                     # stones on the banks
        an = r.uniform(0, math.tau)
        co, si = math.cos(an), math.sin(an)
        rr = 1.0 / (abs(co) ** 6 + abs(si) ** 6) ** (1 / 6)
        R = hi(h - 3) + 0.16
        st.rock("stone", r.uniform(0.05, 0.09), (R * rr * co, hi(4) + 0.02, R * rr * si), seed=i, squash=0.6, detail=1, yaw=r.uniform(0, 360))
    for k in range(4):                                      # reeds at one corner
        x, z = c(-h + 2) + k * 0.05, c(-h + 2.5) + (k % 2) * 0.05
        w.rod("leaf", (x, hi(0), z), (x + 0.03 * math.sin(k), hi(0) + 0.5 + 0.08 * k, z), 0.006, 4)
    w.box2(W("", "z"), (lo(-1), lo(0), lo(h - 2)), (hi(1), hi(1), hi(h + 3)), bevel=0.01)   # outlet trough + sluice board
    w.box2(W("dark", "x"), (lo(-2), lo(2), lo(h)), (hi(2), hi(4), hi(h)), bevel=0.008)
    w.box2(W("dark", "y"), (lo(-2) - 0.02, lo(0), lo(h) - 0.04), (lo(-2) + 0.02, hi(4) + 0.04, hi(h) + 0.04), bevel=0.005)
    w.box2(W("dark", "y"), (hi(2) - 0.02, lo(0), lo(h) - 0.04), (hi(2) + 0.02, hi(4) + 0.04, hi(h) + 0.04), bevel=0.005)
    return a


def storage_drum():
    a = Asset("storage_drum", "Utility")
    w, s = a.p("Wood"), a.p("Scrap")
    for k in range(5):                                      # pallet: top boards on runners
        x0 = lo(-4) + k * (hi(4) - lo(-4)) / 5
        w.box2(K.WOODS["z"][k % 3], (x0 + 0.004, lo(0) + 0.04, lo(-4)), (x0 + (hi(4) - lo(-4)) / 5 - 0.004, hi(0), hi(4)), bevel=0.004)
    for z in (-3.5, 0, 3.5):
        w.box2(W("dark", "x"), (lo(-4), lo(0), c(z) - 0.03), (hi(4), lo(0) + 0.04, c(z) + 0.03), bevel=0.004)
    R = 3.6 * VS + 0.04
    prof = [(0.0, lo(1)), (R - 0.01, lo(1)), (R, lo(1) + 0.01)]
    for y in (1, 4, 8, 11):                                 # rolling hoops
        yc = c(y)
        prof += [(R, yc - 0.03), (R + 0.012, yc - 0.012), (R + 0.012, yc + 0.012), (R, yc + 0.03)]
    prof += [(R, hi(11) - 0.01), (R + 0.006, hi(11)), (R - 0.03, hi(11) + 0.005), (R - 0.03, hi(11) - 0.01), (0.0, hi(11) - 0.01)]
    s.lathe("steel_olive", prof, (0, 0, 0), "y", 32)
    s.cyl("chrome", 0.025, 0.02, (c(1), hi(11) - 0.0, c(1)), "y", 10, 0.003)        # bungs
    s.cyl("chrome", 0.016, 0.02, (c(-2), hi(11) - 0.0, 0), "y", 8, 0.003)
    s.rod("brass", (0, c(1.5), R - 0.01), (0, c(1.5), hi(5)), 0.014, 8)              # tap, red handle
    s.rod("brass", (0, c(1.5), hi(5) - 0.01), (0, c(1.5) - 0.06, hi(5) - 0.01), 0.01, 6)
    s.box2("pump_red", (-0.035, c(2.6) - 0.01, hi(5) - 0.03), (0.035, c(2.6) + 0.01, hi(5) - 0.01), bevel=0.004)
    s.rod("brass", (0, c(1.5), hi(5) - 0.02), (0, c(2.6), hi(5) - 0.02), 0.006, 5)
    lab = K.texmat.image_material("TexDrumLabel", "labels", (1.2, 1.1, 0.4), 0.6, 0.25, False, "h")
    s.cyl(lab, R + 0.002, 0.14, (0, c(6.5), 0), "y", 32, 0.0, caps=False)            # hazard / contents label band
    return a


def pond_liner():
    a = Asset("pond_liner", "Utility")
    cl = a.p("Cloth")
    h = 18
    drape(cl, "cloth_blue", lo(-h) + 0.06, hi(h) - 0.06, lo(-h) + 0.06, hi(h) - 0.06, 0.008, 0.0, t=0.01, seed=11, amp=0.006, nx=18, nz=18)
    for (x0, x1, z0, z1) in ((lo(-h), hi(h), lo(-h), lo(-h) + 0.07), (lo(-h), hi(h), hi(h) - 0.07, hi(h)),
                             (lo(-h), lo(-h) + 0.07, lo(-h), hi(h)), (hi(h) - 0.07, hi(h), lo(-h), hi(h))):
        cl.box2("cloth_ochre", (x0, 0.0, z0), (x1, 0.016, z1), bevel=0.005)   # hemmed edge
    for x in (-h, h):                                       # brass grommets
        for z in range(-h + 2, h, 6):
            cl.torus("brass", 0.016, 0.004, (c(x) * 0.97, 0.017, c(z)), "y", 10, 4)
    return a


def water_tap():
    a = Asset("water_tap", "Utility")
    st, s = a.p("Stone"), a.p("Scrap")
    st.box2("conc", (lo(-2), lo(0), lo(-2)), (hi(2), hi(1), hi(2)), bevel=0.02)
    st.box2("conc_dark", (lo(-2) + 0.03, hi(1) - 0.01, lo(-2) + 0.03), (hi(2) - 0.03, hi(1) + 0.004, hi(2) - 0.03), bevel=0.0)
    s.cyl("steel", 0.026, hi(8) - hi(1), (0, (hi(1) + hi(8)) / 2, 0), "y", 12, 0.003)
    s.cyl("steel_dark", 0.036, 0.04, (0, hi(1) + 0.02, 0), "y", 12, 0.004)      # flange
    s.tube("steel", [(0, c(8), 0), (0, c(8) + 0.02, c(0.8)), (0, c(8), c(2.4)), (0, c(7.4), c(3))], 0.022, 10)   # goose-neck spout
    s.cyl("steel_dark", 0.03, 0.03, (0, c(7.2), c(3)), "y", 10, 0.004)
    s.cyl("brass", 0.02, 0.06, (0, c(8.8), 0), "y", 8, 0.003)                     # spindle
    hd = a.p("Handle", "Scrap", pivot=(0, c(9), 0), mover=True)                  # cross handle (turns)
    for (dx, dz) in ((1, 0), (0, 1)):
        hd.box2("pump_red", (-dx * c(1) - 0.016 * dz, c(9) - 0.015, -dz * c(1) - 0.016 * dx), (dx * c(1) + 0.016 * dz, c(9) + 0.015, dz * c(1) + 0.016 * dx), bevel=0.008)
    s.rod("steel_dark", (0, c(1), lo(-2)), (0, c(1), lo(-3)), 0.022, 8)          # pipe in
    s.box("rust", (0.12, 0.004, 0.08), (0.0, hi(1) + 0.005, c(2.2)), r=(0, 10, 0), bevel=0)  # wet stain
    return a


def transfer_pump():
    a = Asset("transfer_pump", "Utility")
    fe, s, rb, g = a.p("Iron"), a.p("Scrap"), a.p("Rubber"), a.p("Glass")
    fe.box2("steel_dark", (lo(-4), lo(0), lo(-3)), (hi(4), hi(0), hi(3)), bevel=0.01)          # skid
    for z in (-3, 3):
        fe.box2("black", (lo(-4), lo(0), lo(z) - 0.004), (hi(4), hi(0) + 0.01, lo(z) + 0.03), bevel=0.003)
    fe.add(K.kit.bm_cyl(2.6 * VS + 0.02, hi(1) - lo(-3), 24, 0.012), "pump_red", Matrix.Translation(((lo(-3) + hi(1)) / 2, c(3), 0)) @ K._axis_mtx("x"))  # volute
    s.box2("steel_olive", (lo(2), lo(1), lo(-2)), (hi(4), hi(5), hi(2)), bevel=0.02)        # motor with cooling fins
    for k in range(6):
        s.box2("steel_olive", (lo(2) - 0.004, lo(1) + 0.05 + k * 0.06, lo(-2) - 0.01), (hi(4) + 0.004, lo(1) + 0.07 + k * 0.06, hi(2) + 0.01), bevel=0.003)
    s.cyl("lamp_amber", 0.02, 0.02, (c(3), hi(5) + 0.01, 0), "y", 10, 0.003)
    g.cyl("steel", 0.05, 0.03, (c(-1), c(6), c(0.5)), "z", 16, 0.004)             # pressure gauge
    gd = K.texmat.decal_material("Pump_Gauge", "clock_face", (c(-1), c(6), c(0.5) + 0.016), (0.08, 0.08), plane="xy", tint=(1.0, 1.0, 0.9))
    g.cyl(gd, 0.042, 0.004, (c(-1), c(6), c(0.5) + 0.016), "z", 16, 0.0)
    s.rod("steel", (c(-1), c(4), 0), (c(-1), c(6) - 0.04, c(0.5)), 0.008, 5)
    pts = []                                                # coiled hose beside it
    for i in range(46):
        an = i / 12 * math.tau
        rr = 2.2 * VS + 0.03 * (i // 12)
        pts.append((c(-1) + math.cos(an) * rr, hi(0) + 0.04 + 0.012 * (i // 12), c(-5) + math.sin(an) * rr))
    rb.tube("rubber", pts, 0.022, 8, caps=True)
    rb.tube("rubber", [(c(-3), c(3), c(1)), (c(-3) + 0.04, c(1.5), c(3)), (0, c(1), c(6)), (0, c(1), c(10.5))], 0.024, 8)   # to the strainer
    s.box2("chrome", (lo(-1), lo(0), lo(11)), (hi(1), hi(1), hi(12)), bevel=0.03)
    for k in range(4):
        s.box2("black", (lo(-1) + 0.02, lo(0) + 0.02 + k * 0.04, hi(12) - 0.002), (hi(1) - 0.02, lo(0) + 0.035 + k * 0.04, hi(12) + 0.003), bevel=0)
    return a


def block_heater():
    a = Asset("block_heater", "Utility")
    w, fe, rb, cu = a.p("Wood"), a.p("Iron"), a.p("Rubber"), a.p("Copper")
    w.box2(W("dark", "y"), (lo(0), lo(0), lo(0)), (hi(1), hi(12), hi(1)), bevel=0.012)        # post
    fe.box2("rig_green", (lo(-2), lo(8), lo(2)), (hi(3), hi(13), hi(3)), bevel=0.02)            # outlet box
    fe.box2("steel_dark", (lo(-1) - 0.03, lo(14), lo(2) - 0.02), (hi(2) + 0.03, lo(14) + 0.03, hi(3) + 0.08), bevel=0.006, r=(-8, 0, 0))   # rain lid
    fe.box2("black", (lo(-1), lo(10), hi(3)), (hi(0), hi(11), hi(3) + 0.01), bevel=0.006)        # socket
    for x in (-0.03, 0.02):
        fe.box2("chrome", (c(-0.5) + x - 0.004, c(10.5) - 0.012, hi(3) + 0.01), (c(-0.5) + x + 0.004, c(10.5) + 0.012, hi(3) + 0.012), bevel=0)
    pl = a.p("Lamp", "Glass", pivot=(c(2), c(12), hi(3) + 0.01))                               # amber pilot lamp (own object: lit while heating)
    pl.sphere("lamp_amber", 0.022, (c(2), c(12), hi(3) + 0.01), (1, 1, 0.6), 10, 6)
    screws(fe, "chrome", [(c(-1.5), c(8.5), hi(3) + 0.002), (c(2.5), c(8.5), hi(3) + 0.002), (c(-1.5), c(12.5), hi(3) + 0.002), (c(2.5), c(12.5), hi(3) + 0.002)])
    cu.cyl("copper", 0.03, 0.04, (c(0.5), c(6.5), c(2)), "z", 10, 0.004)                     # cable gland
    pts = []                                                 # coiled orange cord hung on the post
    for i in range(30):
        an = i / 10 * math.tau
        pts.append((c(-1.5) + math.cos(an) * 0.07, c(6) - i * 0.006 + 0.06 * math.sin(an), c(4) + math.sin(an) * 0.03))
    rb.tube("ochre", [(c(0.5), c(6.5), c(2.5))] + pts + [(c(-3), c(2), c(5)), (c(-4), c(2.5), c(6.5))], 0.009, 6)
    rb.box2("black", (lo(-4), lo(2), lo(6)), (hi(-4), hi(3), hi(7)), bevel=0.01)              # plug
    return a


TILES = [("furn_fluids", "Makeshift pool, clay pond, storage drum, pond liner, standpipe tap, transfer pump",
          [lambda: [pool_tarp(), clay_pond(), storage_drum(), pond_liner(), water_tap(), transfer_pump()]]),
         ("furn_winter", "Block heater (power post with an outlet box)", [lambda: [block_heater()]])]

if __name__ == "__main__":
    run(TILES, "fluids_")
