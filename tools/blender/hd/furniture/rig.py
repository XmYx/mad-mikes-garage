"""HD moving-base pieces (FurnitureLibrary.Rig.cs, build category VEHICLE, 2026-10-13): rig frames S / M / L / XL and
their extensions + the outrigger deck (box rails, C-channel members, cross members, chequer plate or plank-and-strap
deck, hazard edges, tie-down eyes, end lamps), axle mounts S / M / L, track mounts (gearbox + hydraulic motor), the
engine bay, driver's seat, fuel tanks 200 / 800 L, coilovers, air suspension and the walking-beam bogie. Same voxel
layout as the game grids (0.08 m; L / XL frames 0.16 m), origin = the grid origin.

blender -b -P rig.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fcommon import K, Asset, run, VS  # noqa: E402
import citykit as CK_  # noqa: E402


def L(i, v=VS):
    return (i - 0.5) * v


def H(i, v=VS):
    return (i + 0.5) * v


def C(i, v=VS):
    return i * v


def new(gid, voxel=VS, kind="furniture"):
    CK_.mats()
    if "rig_paint" not in K.M:
        S, P = K.kit.surface, K.kit.P
        K.M["rig_paint"] = S("RigGreenChassis", P("riggreen", 3), chips=0.45, chip_col=P("metal", 2), rust=0.35, dust=0.45, rough=0.55, streaks=0.4)
        K.M["rig_ochre"] = S("RigOchre", P("ochre", 3), chips=0.45, chip_col=P("metal", 2), rust=0.35, dust=0.45, rough=0.5, streaks=0.35)
        K.M["rig_navy"] = S("RigNavy", P("navy", 2), chips=0.4, rust=0.25, dust=0.35, rough=0.5)
        K.M["coil_red"] = S("CoilRed", P("crimson", 3), chips=0.25, dust=0.25, rough=0.35, clearcoat=0.4)
    return Asset(gid, "Vehicle", kind=kind, voxel=voxel)


def vbox(p, m, x0, y0, z0, x1, y1, z1, v=VS, bevel=None):
    lo_ = (L(min(x0, x1), v), L(min(y0, y1), v), L(min(z0, z1), v))
    hi_ = (H(max(x0, x1), v), H(max(y0, y1), v), H(max(z0, z1), v))
    b = min(hi_[i] - lo_[i] for i in range(3)) * 0.2 if bevel is None else bevel
    p.box2(m, lo_, hi_, bevel=min(b, 0.02))


def tube(p, m, a, b, r, v=VS, segs=10):
    p.rod(m, tuple(x * v for x in a), tuple(x * v for x in b), (r + 0.5) * v, segs)


# ------------------------------------------------------------------------------------------ frames
def frame(gid, hx, hz, coarse, ext):
    v = 0.16 if coarse else VS
    a = new(gid, v, kind="building" if coarse else "furniture")
    fe, w = a.p("Iron"), a.p("Wood")
    deckY = 2 if coarse else 4
    top = deckY - 1
    rail = "rig_paint"
    # perimeter box rail (box section: a little inset groove on the outside)
    for x in (-hx, hx):
        vbox(fe, rail, x, 0, -hz, x, top, hz, v)
        xo = C(x, v) + (0.5 * v if x > 0 else -0.5 * v)
        fe.box2("steel_dark", (xo - 0.004, C(top, v) * 0.5 - 0.008, L(-hz, v) + 0.03), (xo + 0.004, C(top, v) * 0.5 + 0.008, H(hz, v) - 0.03), bevel=0)
    for z in (-hz, hz):
        vbox(fe, rail, -hx, 0, z, hx, top, z, v)
    ix = max(3, hx * 4 // 9)
    for sx in (-1, 1):                                            # main C-channels
        x0 = sx * ix
        vbox(fe, "steel", x0, 0, -hz, x0, top, hz, v)
        vbox(fe, "steel_dark", x0 - sx, 0, -hz, x0 - sx, 0, hz, v)
        vbox(fe, "steel_dark", x0 - sx, top, -hz, x0 - sx, top, hz, v)
    step = 5 if coarse else 9
    z, kk = -hz + step, 0
    while z <= hz - step // 2:
        vbox(fe, "rust_dark" if coarse else "steel", -hx, 0 if coarse else 1, z, hx, top, z, v)
        if kk % 2 == 0 and z + step < hz:
            for sx in (-1, 1):
                tube(fe, "rust", (sx * ix, 0.5, z), (sx * (hx - 1), 0.5, z + step), 0.5 if not coarse else 0.3, v, 8)
        z += step
        kk += 1
    if not coarse:                                                # lightening holes
        for z in range(-hz + 3, hz - 2, 6):
            for x in (-hx, hx):
                fe.cyl("black", 0.024, 0.09, (C(x, v), C(1, v), C(z, v)), "x", 10, 0.0)
    for z in range(-hz + 1, hz, 2 if coarse else 3):              # rivets along the outer rails
        for x in (-hx, hx):
            fe.cyl("chrome", 0.011, 0.012, (C(x, v) + (0.5 * v if x > 0 else -0.5 * v), C(top, v) * 0.6, C(z, v)), "x", 6, 0.0)
    # deck
    if not coarse:
        fe.box2("deck_plate", (L(-hx, v), L(deckY, v), L(-hz, v)), (H(hx, v), H(deckY, v), H(hz, v)), bevel=0.01)
        for z in range(-hz + 12, hz - 3, 12):                     # plate welds
            fe.box2("steel_dark", (L(-hx, v) + 0.02, H(deckY, v) - 0.002, C(z, v) - 0.008), (H(hx, v) - 0.02, H(deckY, v) + 0.004, C(z, v) + 0.008), bevel=0)
    else:
        rr = K.rng(len(gid) * 31 + hx)
        for z in range(-hz, hz + 1):
            if ((z + hz) % 7) == 0:
                vbox(fe, "steel", -hx, deckY, z, hx, deckY, z, v, bevel=0.006)              # steel strap, bolted
                for x in range(-hx + 1, hx, 4):
                    fe.cyl("chrome", 0.018, 0.012, (C(x, v), H(deckY, v) + 0.004, C(z, v)), "y", 6, 0.0)
                continue
            xs = [-hx] + [x for x in range(-hx + 1, hx + 1) if (x + z * 5) % 11 == 0] + [hx + 1]   # staggered butt joints
            for i in range(len(xs) - 1):
                x0, x1 = xs[i], xs[i + 1] - 1
                if x1 < x0:
                    continue
                w.box2(K.WOODS["x"][rr.randrange(3)], (L(x0, v) + 0.004, L(deckY, v) + rr.uniform(-0.006, 0.0), L(z, v) + 0.005),
                       (H(x1, v) - 0.004, H(deckY, v) - rr.uniform(0.0, 0.006), H(z, v) - 0.005), bevel=0.008, segs=1)
    hz_m, hx_m, dy = H(hz, v), H(hx, v), H(deckY, v)
    for (x0, x1, z0, z1, along) in ((-hx_m, hx_m, -hz_m, -hz_m + 0.08, "x"), (-hx_m, hx_m, hz_m - 0.08, hz_m, "x"),
                                    (-hx_m, -hx_m + 0.08, -hz_m, hz_m, "z"), (hx_m - 0.08, hx_m, -hz_m, hz_m, "z")):
        n = max(1, int(round((x1 - x0 if along == "x" else z1 - z0) / 0.24)))
        for i in range(n):
            if along == "x":
                a0, a1 = x0 + (x1 - x0) * i / n, x0 + (x1 - x0) * (i + 1) / n
                fe.box2("deck_paint" if i % 2 == 0 else "black", (a0, dy, z0), (a1, dy + 0.006, z1), bevel=0)
            else:
                a0, a1 = z0 + (z1 - z0) * i / n, z0 + (z1 - z0) * (i + 1) / n
                fe.box2("deck_paint" if i % 2 == 0 else "black", (x0, dy, a0), (x1, dy + 0.006, a1), bevel=0)
    eye = 9 if coarse else 18
    for z in range(-hz + 3, hz - 2, eye):                         # tie-down eyes
        for x in (-hx + 1, hx - 1):
            fe.torus("steel", 0.035, 0.01, (C(x, v), dy + 0.03, C(z, v)), "x", 12, 4)
            fe.box2("steel_dark", (C(x, v) - 0.03, dy, C(z, v) - 0.04), (C(x, v) + 0.03, dy + 0.012, C(z, v) + 0.04), bevel=0.003)
    if not ext:                                                   # tow eyes and end lamps
        for x in (-ix, ix):
            for zz in (-hz, hz):
                s = 1 if zz > 0 else -1
                fe.torus("steel", 0.05, 0.014, (C(x, v), C(0, v), C(zz, v) + s * 0.5 * v + s * 0.04), "x", 12, 4)
        for x in (-hx + 2, hx - 2):
            x2 = x + (-1 if x > 0 else 1)
            for zz, m1, m2 in ((hz, "lamp", "lamp_amber"), (-hz, "lamp_red", "lamp_amber")):
                s = 1 if zz > 0 else -1
                fe.box2("black", (C(min(x, x2), v) - 0.06, C(top, v) - 0.05, C(zz, v) + s * 0.5 * v - 0.01), (C(max(x, x2), v) + 0.06, C(top, v) + 0.05, C(zz, v) + s * 0.5 * v + s * 0.02), bevel=0.006)
                fe.cyl(m1, 0.035, 0.012, (C(x, v), C(top, v), C(zz, v) + s * 0.5 * v + s * 0.022), "z", 12, 0.002)
                fe.cyl(m2, 0.028, 0.012, (C(x2, v), C(top, v), C(zz, v) + s * 0.5 * v + s * 0.022), "z", 12, 0.002)
    for z in range(-hz + 4, hz - 3, 14 if coarse else 28):        # amber side markers
        for x in (-hx, hx):
            s = 1 if x > 0 else -1
            fe.box2("lamp_amber", (C(x, v) + s * 0.5 * v - 0.008, C(0 if coarse else 2, v) - 0.025, C(z, v) - 0.035), (C(x, v) + s * 0.5 * v + 0.008, C(0 if coarse else 2, v) + 0.025, C(z, v) + 0.035), bevel=0.004)
    return a


# ------------------------------------------------------------------------------------------ running gear mounts
def mount(gid, track, grade):
    a = new(gid)
    fe = a.p("Iron")
    r = 2.5 + grade * 1.4
    R = math.ceil(r)
    vbox(fe, "steel", -R - 1, -1, -3, R + 1, -1, 3)               # pad under the frame
    for x in (-R - 0.5, R + 0.5):
        for z in (-2, 2):
            fe.cyl("chrome", 0.014, 0.02, (C(x), C(-0.5) + 0.006, C(z)), "y", 6, 0.0)
    if not track:
        paint = "rig_paint"
        x1 = math.ceil(r * 0.7)
        fe.lathe(paint, [(0.0, 0.0), ((r + 0.2) * VS, 0.0), ((r + 0.5) * VS, 0.25 * (2 * x1 + 1) * VS), ((r + 0.5) * VS, 0.75 * (2 * x1 + 1) * VS),
                         ((r + 0.2) * VS, (2 * x1 + 1) * VS), (0.0, (2 * x1 + 1) * VS)], (L(-x1), 0, 0), "x", 24)     # pumpkin
        # axle tubes stub out both sides (the wheel sockets sit at the frame's sides)
        fe.cyl("steel_dark", (r * 0.45 + 0.5) * VS, (2 * R + 6) * VS, (0, 0, 0), "x", 16, 0.006)
        for s in (-1, 1):
            fe.cyl(paint, (r * 0.62 + 0.5) * VS, 0.05, (s * (R + 2.5) * VS, 0, 0), "x", 16, 0.006)
        fe.cyl(paint, (r * 0.55 + 0.5) * VS, H(R + 3) - 0, (0, 0, H(R + 3) / 2), "z", 18, 0.006, r2=(r * 0.4 + 0.5) * VS)   # pinion nose
        fe.cyl("chrome", (r * 0.35 + 0.5) * VS, VS, (0, 0, C(R + 4)), "z", 14, 0.004)                      # yoke
        for s in (-1, 1):
            fe.box2("steel", (s * 0.02 - 0.012, -0.05, C(R + 4) + 0.03), (s * 0.02 + 0.012, 0.05, C(R + 4) + 0.08), bevel=0.003)
        bolts = 8 + grade * 2
        for i in range(bolts):
            an = i / bolts * math.tau
            fe.cyl("chrome", 0.012, 0.016, (math.cos(an) * r * 0.8 * VS, math.sin(an) * r * 0.8 * VS, L(-R - 1) - 0.004), "z", 6, 0.002)
        fe.lathe("steel", [(0.0, 0.0), ((r * 0.8 + 0.5) * VS, 0.0), ((r * 0.8 + 0.4) * VS, 0.6 * VS), (0.0, 1.0 * VS)], (0, 0, H(-R - 1)), "z", 24, r=(180, 0, 0))   # cover
        fe.cyl("chrome", 0.02, 0.05, (0, C(R) + 0.02, C(1)), "y", 8, 0.004)                                 # breather
        fe.cyl("ochre", 0.02, 0.02, (0, C(-math.ceil(r * 0.6)), C(-R) - 0.06), "z", 8, 0.002)             # drain plug
    else:
        paint = "rig_ochre"
        h = R + 1
        vbox(fe, paint, -h, -h, -h, h, -2, h)                                                              # gearbox case
        for x in range(-h + 1, h, 2):
            vbox(fe, "steel", x, -h - 1, -h + 1, x, -h - 1, h - 1, bevel=0.004)                             # fins
        fe.box2("steel_dark", (L(-h) - 0.01, C(-2) - 0.02, L(-h) - 0.01), (H(h) + 0.01, C(-2) + 0.02, H(h) + 0.01), bevel=0.006)   # flange
        for (x, z) in ((-h, -h), (h, -h), (-h, h), (h, h), (0, -h), (0, h), (-h, 0), (h, 0)):
            fe.cyl("chrome", 0.012, 0.02, (C(x) * 0.95, C(-2) + 0.02, C(z) * 0.95), "y", 6, 0.0)
        vbox(fe, "black", -1, -h + 1, h + 1, 1, -2, h + 2)                                                 # hydraulic motor
        fe.cyl("black", 1.6 * VS, 2 * VS, (0, C(-h / 2 - 0.5), C(h + 2.5)), "z", 16, 0.008)
        for s in (-0.5, 0.5):
            fe.tube("black", [(C(s), C(-2), C(h + 2)), (C(s), C(-1.5), C(h + 4)), (C(s * 2), C(-1), C(h + 6))], 0.02, 8)
        for x in (-h, h):
            fe.cyl("chrome", 0.03, 0.03, (C(x) + (0.02 if x > 0 else -0.02), C(-h / 2 - 1), 0), "x", 10, 0.004)   # output shafts
        fe.box2("deck_paint", (L(-h) - 0.004, C(-h + 1) - 0.02, H(h) - 0.08), (H(h) + 0.004, C(-h + 1) + 0.02, H(h) + 0.004), bevel=0)
    return a


# ------------------------------------------------------------------------------------------ drivetrain and cab
def engine_bay():
    a = new("rig_engine_bay")
    fe, rb = a.p("Iron"), a.p("Rubber")
    for x in (-7, 6):
        vbox(fe, "steel", x, 0, -2, x + 1, 1, 18)
    vbox(fe, "steel", -7, 0, -2, 7, 0, -1)
    vbox(fe, "steel", -7, 0, 17, 7, 0, 18)
    for x in (-6, 5):
        for z in (2, 12):
            rb.cyl("rubber", 0.07, 0.05, (C(x + 0.5), C(2) - 0.01, C(z + 0.5)), "y", 12, 0.01)
            fe.cyl("steel_dark", 0.08, 0.012, (C(x + 0.5), C(2) + 0.025, C(z + 0.5)), "y", 12, 0.002)
            fe.cyl("chrome", 0.012, 0.03, (C(x + 0.5), C(2) + 0.045, C(z + 0.5)), "y", 6, 0.0)
    vbox(fe, "rig_paint", -8, 0, -4, 8, 12, -3)                                                              # firewall
    for x in range(-7, 8, 3):
        fe.cyl("chrome", 0.01, 0.01, (C(x), C(11.5), L(-4) - 0.003), "z", 6, 0.0)
    vbox(fe, "black", -6, 7, -2, -3, 10, -2)                                                                 # fuse box
    fe.box2("coil_red", (C(-5) - 0.025, C(9) - 0.025, H(-2)), (C(-5) + 0.025, C(9) + 0.025, H(-2) + 0.012), bevel=0.004)
    fe.box2("ochre", (C(-4) - 0.025, C(9) - 0.025, H(-2)), (C(-4) + 0.025, C(9) + 0.025, H(-2) + 0.012), bevel=0.004)
    fe.box2("steel", (L(-7), L(0), L(19)), (H(7), H(13), H(20)), bevel=0.012)                                # radiator frame
    fe.box2("fins" if "fins" in K.M else "steel_dark", (L(-6), L(1), L(19) - 0.004), (H(6), H(12), H(20) + 0.004), bevel=0)
    for y in range(1, 13):
        fe.box2("black", (L(-6), C(y) - 0.004, H(20) + 0.002), (H(6), C(y) + 0.004, H(20) + 0.008), bevel=0)
    fe.cyl("chrome", 0.035, 0.03, (C(4), H(13) + 0.015, C(19.5)), "y", 10, 0.004)                             # cap
    fe.lathe("black", [(5.0 * VS, 0.0), (6.5 * VS, 0.0), (6.5 * VS, 2 * VS), (5.0 * VS, 2 * VS)], (0, C(7), L(17)), "z", 28)   # fan shroud
    for i in range(7):                                                                                        # fan blades
        an = i / 7 * math.tau
        fe.box("steel_dark", (0.06, 0.32, 0.01), (math.cos(an) * 0.2, C(7) + math.sin(an) * 0.2, C(17.5)), r=(0, 0, math.degrees(an) + 90), bevel=0.003)
    fe.cyl("steel_dark", 0.06, 0.06, (0, C(7), C(17.5)), "z", 12, 0.004)
    vbox(fe, "black", 9, 0, -2, 12, 4, 4)                                                                    # battery
    fe.cyl("coil_red", 0.025, 0.03, (C(10), H(4) + 0.015, C(-1)), "y", 8, 0.003)
    fe.cyl("steel", 0.025, 0.03, (C(11), H(4) + 0.015, C(3)), "y", 8, 0.003)
    fe.tube("black", [(C(10), H(4) + 0.03, C(-1)), (C(8), C(8), C(-2)), (C(3), C(11), C(-3))], 0.018, 8)    # loom
    fe.tube("black", [(C(-7), C(2), C(18)), (C(-7), C(6), C(10)), (C(-6), C(10), C(-3))], 0.022, 8)          # hoses
    return a


def driver_seat():
    a = new("rig_driver_seat")
    fe, cl, gl = a.p("Iron"), a.p("Cloth"), a.p("Glass")
    vbox(fe, "deck_plate", -4, 0, -4, 4, 0, 10)                                                              # floor plate
    vbox(fe, "steel_dark", -2, 1, -3, 2, 4, 1)                                                               # pedestal
    fe.tube("chrome", [(C(-1.5), C(4), C(-1)), (C(1.5), C(1.5), C(-1))], 0.01, 6)
    cl.box2("leather", (L(-3), L(5), L(-4)), (H(3), H(6), H(2)), bevel=0.04)                                 # cushion
    cl.box2("leather", (L(-3), L(7), L(-5)), (H(3), H(14), H(-4)), bevel=0.04)                               # back
    for x in range(-2, 3, 2):                                                                               # pleats
        cl.box2("black", (C(x) - 0.006, H(6) - 0.004, L(-3)), (C(x) + 0.006, H(6) + 0.004, H(1)), bevel=0)
        cl.box2("black", (C(x) - 0.006, L(8), H(-4) - 0.002), (C(x) + 0.006, H(13), H(-4) + 0.006), bevel=0)
    for x in (-4, 4):
        cl.box2("black", (L(x), L(6), L(-4)), (H(x), H(9), H(1)), bevel=0.025)                               # bolsters
    fe.rod("steel", (0, C(1), C(9)), (0, C(11), C(6)), 0.03, 10)                                             # column
    fe.torus("black", 3 * VS, 0.018, (0, C(11), C(6)), "z", 28, 8)                                          # wheel rim (tilted below)
    for i in range(3):
        an = i / 3 * math.tau + math.pi / 2
        fe.rod("steel", (0, C(11), C(6)), (math.cos(an) * 3 * VS, C(11) + math.sin(an) * 3 * VS, C(6)), 0.01, 6)
    fe.cyl("chrome", 0.04, 0.03, (0, C(11), C(6) - 0.01), "z", 10, 0.004)
    vbox(fe, "chrome", -3, 1, 7, -2, 2, 8, bevel=0.006)                                                      # pedals
    vbox(fe, "chrome", 1, 1, 7, 2, 2, 8, bevel=0.006)
    vbox(fe, "black", -4, 12, 8, 4, 15, 9)                                                                   # gauge pod
    for (x, m) in ((-2, "dial_glow"), (0, "lamp_amber"), (2, "lamp"), (3, "lamp_red")):
        gl.cyl(m, 0.025 if x != 3 else 0.015, 0.008, (C(x), C(14) - (0.06 if x == 3 else 0), H(9) + 0.004), "z", 12, 0.0)
    for x in (-2, 0):
        fe.torus("chrome", 0.03, 0.005, (C(x), C(14), H(9) + 0.004), "z", 12, 3)
    hoop = "rig_ochre"
    for x in (-5, 5):
        fe.rod(hoop, (C(x), 0, C(-7)), (C(x), C(19), C(-7)), 0.045, 12)
        fe.box2("steel_dark", (C(x) - 0.06, L(0), C(-7) - 0.06), (C(x) + 0.06, L(0) + 0.012, C(-7) + 0.06), bevel=0.003)
    fe.tube(hoop, [(C(-5), C(19), C(-7)), (C(-4.5), C(20), C(-7)), (C(4.5), C(20), C(-7)), (C(5), C(19), C(-7))], 0.045, 12)
    fe.rod(hoop, (C(-5), C(10), C(-7)), (C(5), C(10), C(-7)), 0.03, 8)
    return a


def fuel_tank(gid, r, length, seed):
    a = new(gid)
    fe = a.p("Iron")
    cy = r + 1
    rr = (r + 0.4 + 0.5) * VS
    z0, z1 = L(-length // 2), H(length // 2)
    fe.lathe("rig_ochre", [(0.0, 0.0), (rr * 0.92, 0.0), (rr, 0.03), (rr, z1 - z0 - 0.03), (rr * 0.92, z1 - z0), (0.0, z1 - z0)], (0, C(cy), z0), "z", 28)
    fe.cyl("coil_red", rr + 0.004, 3 * VS, (0, C(cy), 0), "z", 28, 0.0)                                      # painted band
    for z in (-length // 2 + 3, length // 2 - 3):
        vbox(fe, "steel", -r, 0, z - 1, r, 1, z + 1)                                                           # saddle
        fe.torus("black", rr + 0.012, 0.012, (0, C(cy), C(z)), "z", 28, 4)                                    # strap
        fe.cyl("chrome", 0.012, 0.04, (rr + 0.02, C(1.5), C(z)), "x", 6, 0.0)
    nz = C(length // 2 - 4)
    fe.cyl("chrome", (1.2 + 0.5) * VS * 0.7, 3 * VS, (0, C(cy + r) + 1.5 * VS, nz), "y", 14, 0.004)       # filler neck
    fe.cyl("coil_red", (1.6 + 0.5) * VS * 0.7, 0.04, (0, C(cy + r + 3), nz), "y", 16, 0.006)                # cap
    fe.torus("steel", 0.012, 0.004, (0.05, C(cy + r + 3), nz), "x", 8, 3)
    fe.cyl("chrome", 0.015, 0.05, (0, C(1), L(-length // 2 + 1)), "z", 8, 0.002)                           # drain cock
    fe.torus("rust_dark", rr - 0.002, 0.006, (0, C(cy), C(-length // 4)), "z", 28, 3)
    return a


def coilovers():
    a = new("rig_suspension_light")
    fe = a.p("Iron")
    vbox(fe, "steel", -7, 0, -3, 7, 0, 3)
    for x in (-4, 4):
        fe.cyl("chrome", 0.8 * VS + 0.01, H(12) - L(1), (C(x), (H(12) + L(1)) / 2, 0), "y", 12, 0.004)
        fe.cyl("black", 1.4 * VS, H(6) - L(1), (C(x), (H(6) + L(1)) / 2, 0), "y", 14, 0.006)                  # damper body
        pts = []
        turns, n = 4.0, 64
        for i in range(n + 1):                                                                              # the coil
            t = i / n
            an = t * turns * math.tau
            pts.append((C(x) + math.cos(an) * 1.85 * VS, L(3) + t * (H(10) - L(3)), math.sin(an) * 1.85 * VS))
        fe.tube("coil_red", pts, 0.022, 8)
        for y in (2, 11):
            fe.cyl("rig_ochre", 2.4 * VS + 0.02, VS, (C(x), C(y), 0), "y", 18, 0.006)                         # spring seats
        vbox(fe, "steel", x - 1, 12, -1, x + 1, 13, 1, bevel=0.01)
        fe.cyl("chrome", 0.02, 0.2, (C(x), C(12.5), 0), "z", 8, 0.002)
    return a


def airbags():
    a = new("rig_suspension_heavy")
    fe, rb = a.p("Iron"), a.p("Rubber")
    vbox(fe, "steel", -9, 0, -4, 9, 0, 4)
    for x in (-5, 5):
        prof = [(0.0, L(1))]
        for i in range(3):                                                                                   # three convolutions
            y0 = L(1) + i * 3 * VS
            prof += [(2.7 * VS + 0.04, y0 + 0.01), (3.4 * VS + 0.04, y0 + 1.5 * VS), (2.7 * VS + 0.04, y0 + 3 * VS - 0.01)]
        prof.append((0.0, H(9)))
        rb.lathe("rubber", prof, (C(x), 0, 0), "y", 24)
        for i in range(1, 3):
            rb.torus("steel_dark", 2.75 * VS + 0.04, 0.01, (C(x), L(1) + i * 3 * VS, 0), "y", 24, 4)          # girdle rings
        fe.cyl("steel", 3.4 * VS + 0.04, VS, (C(x), C(10), 0), "y", 24, 0.006)
        fe.cyl("chrome", 0.012, 0.02, (C(x) + 0.12, H(10) + 0.01, 0), "y", 6, 0.0)
    fe.lathe("rig_navy", [(0.0, 0.0), (2.0 * VS, 0.0), (2.1 * VS, 0.04), (2.1 * VS, 17 * VS - 0.04), (2.0 * VS, 17 * VS), (0.0, 17 * VS)], (L(-8), C(1), C(-3)), "x", 20)   # reservoir
    for x in (-6, 6):
        fe.torus("black", 2.15 * VS, 0.008, (C(x), C(1), C(-3)), "x", 20, 4)
    vbox(fe, "chrome", -1, 1, 2, 1, 3, 3, bevel=0.008)                                                      # valve block
    fe.cyl("black", 0.025, 0.03, (0, C(3) + 0.06, C(2.5)), "y", 10, 0.004)
    for s in (-5, 5):
        fe.tube("black", [(0, C(3), C(3)), (C(s * 0.5), C(6), C(3)), (C(s), C(8), C(2))], 0.012, 6)
    fe.tube("black", [(0, C(2), C(2)), (0, C(1.5), C(0)), (0, C(1), C(-1.5))], 0.012, 6)
    return a


def bogie():
    a = new("rig_suspension_crawler")
    fe, rb = a.p("Iron"), a.p("Rubber")
    vbox(fe, "steel", -2, 0, -2, 2, 6, 2)                                                                    # pedestal
    fe.cyl("steel_dark", (2.6 + 0.5) * VS, 7 * VS, (0, C(7), 0), "x", 20, 0.008)                           # pivot boss
    fe.cyl("chrome", (1.0 + 0.5) * VS, 9 * VS, (0, C(7), 0), "x", 12, 0.004)
    for s in (-1, 1):
        fe.cyl("steel", 0.05, 0.02, (s * 4.6 * VS, C(7), 0), "x", 8, 0.003)
    fe.box2("rig_ochre", (L(-1), L(5), L(-14)), (H(1), H(8), H(14)), bevel=0.02)                            # equaliser
    for z in range(-12, 13, 6):
        fe.cyl("chrome", 0.01, 0.01, (L(-1) - 0.002, C(6.5), C(z)), "x", 6, 0.0)
        fe.cyl("chrome", 0.01, 0.01, (H(1) + 0.002, C(6.5), C(z)), "x", 6, 0.0)
    for z in (-12, 12):
        for (x0, x1) in ((-4, -2), (2, 4)):
            cx = (L(x0) + H(x1)) / 2
            rb.cyl("rubber", (3.5 + 0.5) * VS, H(x1) - L(x0), (cx, C(3), C(z)), "x", 24, 0.02)              # solid road wheels
            fe.cyl("steel", (2.0 + 0.5) * VS, H(x1) - L(x0) + 0.01, (cx, C(3), C(z)), "x", 20, 0.006)
            for i in range(6):
                an = i / 6 * math.tau
                fe.cyl("chrome", 0.01, H(x1) - L(x0) + 0.02, (cx, C(3) + math.sin(an) * 1.4 * VS, C(z) + math.cos(an) * 1.4 * VS), "x", 6, 0.0)
        fe.cyl("chrome", (1.2 + 0.5) * VS, 11 * VS, (0, C(3), C(z)), "x", 12, 0.004)
        fe.box2("rig_ochre", (L(-1), C(3), C(z) - 0.08), (H(1), L(5) + 0.02, C(z) + 0.08), bevel=0.01)       # hangers
    return a


TILES = [("furn_rig_frames_small", "Rig frames S / M, extensions S / M, outrigger deck",
          [lambda: [frame("rig_frame_S", 14, 24, False, False), frame("rig_frame_M", 18, 37, False, False),
                    frame("rig_frame_ext_S", 14, 12, False, True), frame("rig_frame_ext_M", 18, 12, False, True),
                    frame("rig_outrigger", 7, 12, False, True)]], 3),
         ("furn_rig_frames_big", "Rig frames L / XL and their extensions (plank and strap decks)",
          [lambda: [frame("rig_frame_L", 12, 28, True, False), frame("rig_frame_XL", 18, 43, True, False),
                    frame("rig_frame_ext_L", 12, 9, True, True), frame("rig_frame_ext_XL", 18, 12, True, True)]], 2),
         ("furn_rig_mounts", "Axle mounts S / M / L, track mount, heavy track mount",
          [lambda: [mount("rig_axle_S", False, 0), mount("rig_axle_M", False, 1), mount("rig_axle_L", False, 2),
                    mount("rig_track_mount", True, 1), mount("rig_track_mount_heavy", True, 2)]], 3),
         ("furn_rig_drive", "Engine bay, driver's seat, fuel tanks 200 / 800 L, coilovers, air suspension, walking-beam bogie",
          [lambda: [engine_bay(), driver_seat(), fuel_tank("rig_fuel_tank", 4, 14, 2501), fuel_tank("rig_fuel_tank_big", 7, 24, 2502),
                    coilovers(), airbags(), bogie()]], 4)]

def _layout_on_floor(roots, cols=None, gap=0.5):
    """Preview layout: mounts hang below their origin; lift every asset onto the stage floor (the exporter resets the
    root to the origin, so this only moves the tile render)."""
    _layout(roots, cols, gap)
    K.bpy.context.view_layer.update()
    for r in roots:
        lo_, _ = K.rc.bounds([r] + list(r.children_recursive))
        if lo_.z < 0:
            r.location.z -= lo_.z
    K.bpy.context.view_layer.update()


_layout = K.layout
K.layout = _layout_on_floor

if __name__ == "__main__":
    run(TILES, "rig_")
