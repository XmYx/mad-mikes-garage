"""HD town pieces of the planned towns (BiomeProps.Town.cs, 2026-10-13): street furniture (bench, bin, trash bags,
litter, phone booth, bus stop, dead traffic light, stop sign, hydrant, bollard, planter), park pieces (hedge, park
trees, fountain, statue, pond, the village well), graffiti walls, burning / oil drums, warehouses, the fuel-station
canopy, picket and chain-link fences and rubble. Template ids and voxel sizes as the game's (origin = the template
origin, the street side faces -Z); random layouts use the game's System.Random seeds (hdkit.NetRandom).

blender -b -P town.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import hdkit as K  # noqa: E402
from hdkit import Asset, Vector, Matrix  # noqa: E402


def L(i, v):
    return (i - 0.5) * v


def H(i, v):
    return (i + 0.5) * v


def C(i, v):
    return i * v


def mats():
    M = K.M
    if "park_green" in M:
        return
    S, P, Hx = K.kit.surface, K.kit.P, K.kit._hex
    M["park_green"] = S("ParkGreenPaint", Hx("34584a"), chips=0.5, chip_col=P("wood", 2), rust=0.15, dust=0.35, rough=0.6, streaks=0.3)
    M["park_green_metal"] = S("ParkGreenMetal", Hx("2a4a3a"), chips=0.45, chip_col=P("rust", 2), rust=0.35, dust=0.35, rough=0.5, streaks=0.4)
    M["binbag"] = S("BinBagPlastic", Hx("1c1c1e"), dust=0.25, rough=0.25, var=0.15, bump=0.5)
    M["booth_red"] = S("BoothRed", P("crimson", 3), chips=0.45, chip_col=P("metal", 2), rust=0.3, dust=0.3, rough=0.4, streaks=0.4)
    M["sign_navy"] = S("SignNavy", P("navy", 3), chips=0.4, chip_col=P("metal", 2), rust=0.25, dust=0.3, rough=0.45)
    M["bronze"] = S("BronzeVerdigris", P("bronze", 2), rust=0.0, dust=0.3, metal=0.8, rough=0.45, var=0.35, bump=0.4)
    M["verdigris"] = S("Verdigris", Hx("4e7c66"), dust=0.25, metal=0.2, rough=0.7, var=0.3, bump=0.5)
    M["poster"] = S("PosterOchre", P("ochre", 3), dust=0.35, rough=0.9, var=0.3, bump=0.2, streaks=0.5)
    M["pond"] = K.kit.glass("PondWater", tint=Hx("223836"))
    M["lens_red"] = S("LensRedDead", P("crimson", 1), dust=0.3, rough=0.15, var=0.05)
    M["lens_amber"] = S("LensAmberDead", P("ochre", 1), dust=0.3, rough=0.15, var=0.05)
    M["lens_green"] = S("LensGreenDead", P("moss", 1), dust=0.3, rough=0.15, var=0.05)
    M["canopy_cream"] = S("CanopyCream", P("cream", 2), chips=0.4, chip_col=P("rust", 2), rust=0.3, dust=0.4, rough=0.5, streaks=0.5)
    M["spray"] = [S("Spray%d" % i, c, dust=0.2, rough=0.6, var=0.15) for i, c in
                  enumerate([P("crimson", 4), P("ochre", 4), P("paleblue", 1), Hx("d07090"), P("moss", 4), P("cream", 4)])]


def new(gid, v, cat="Prop", kind="prop"):
    mats()
    return Asset(gid, cat, kind=kind, voxel=v)


def vbox(p, m, x0, y0, z0, x1, y1, z1, v, bevel=None, segs=2):
    lo_ = (L(min(x0, x1), v), L(min(y0, y1), v), L(min(z0, z1), v))
    hi_ = (H(max(x0, x1), v), H(max(y0, y1), v), H(max(z0, z1), v))
    b = min(hi_[i] - lo_[i] for i in range(3)) * 0.2 if bevel is None else bevel
    p.box2(m, lo_, hi_, bevel=min(b, 0.02), segs=segs)


# ------------------------------------------------------------------------------------------ street furniture
def bench():
    v = 0.08
    a = new("Bench0", v)
    s, w = a.p("Scrap"), a.p("Wood")
    for x in (-8, 8):                                                     # cast ends: front leg, back post, seat rail
        s.box2("park_green_metal", (L(x, v) + 0.01, 0, L(-3, v)), (H(x, v) - 0.01, H(5, v), H(-3, v)), bevel=0.012)
        s.tube("park_green_metal", [(C(x, v), 0.0, C(2, v)), (C(x, v), C(5, v), C(2.2, v)), (C(x, v), H(12, v), C(3, v))], 0.03, 8)
        s.box2("park_green_metal", (L(x, v) + 0.01, L(5, v), L(-3, v)), (H(x, v) - 0.01, H(5, v), H(2, v)), bevel=0.01)
        s.tube("park_green_metal", [(C(x, v), C(1, v), C(-3, v)), (C(x, v), C(4, v), C(0, v)), (C(x, v), C(5, v), C(2, v))], 0.012, 6)   # scroll
        for y in (0,):
            s.box2("park_green_metal", (L(x, v), 0, L(-3, v) - 0.02), (H(x, v), 0.015, H(-3, v) + 0.02), bevel=0.004)
    for z in (-3, -1, 1):
        w.box2("park_green", (L(-10, v), L(6, v), L(z, v) + 0.006), (H(10, v), H(6, v), H(z, v) - 0.006), bevel=0.012)
    for y in (7, 9, 11):
        if y == 9:                                                         # the broken slat (x > 3 gone): splintered end
            w.box2("park_green", (L(-10, v), L(y, v) + 0.005, L(2, v)), (H(3, v), H(y, v) - 0.005, H(2, v)), bevel=0.01)
            w.box2(K.wood("light", "x"), (H(3, v) - 0.004, L(y, v) + 0.012, L(2, v) + 0.01), (H(3, v) + 0.04, H(y, v) - 0.02, H(2, v) - 0.01), bevel=0.0)
            continue
        w.box2("park_green", (L(-10, v), L(y, v) + 0.005, L(2, v)), (H(10, v), H(y, v) - 0.005, H(2, v)), bevel=0.01)
    for x in (-8, 8):
        for z in (-3, -1, 1):
            s.cyl("steel_dark", 0.008, 0.004, (C(x, v) + (0.03 if x < 0 else -0.03), H(6, v), C(z, v)), "y", 6, 0.0)
    return a


def bin_():
    v = 0.08
    a = new("Bin0", v)
    s, c = a.p("Scrap"), a.p("Cloth")
    R, r0 = (3.2 + 0.5) * v * 0.92, 2.2 * v
    s.lathe("park_green_metal", [(0.0, 0.0), (R * 0.9, 0.0), (R, 0.04), (R, H(11, v) - 0.04), (R + 0.012, H(11, v) - 0.02), (R + 0.012, H(11, v)),
                                 (R - 0.02, H(11, v)), (R - 0.02, 0.06), (0.0, 0.06)], (0, 0, 0), "y", 24)
    for k in range(12):                                                    # slots
        an = k / 12 * math.tau
        s.box("black", (0.02, 0.36, 0.004), (math.cos(an) * (R + 0.001), C(5.5, v), math.sin(an) * (R + 0.001)), r=(0, -math.degrees(an) + 90, 0), bevel=0.0)
    s.torus("steel", R + 0.004, 0.012, (0, C(3, v), 0), "y", 24, 4)
    s.box2("steel_dark", (-0.03, 0, -R - 0.02), (0.03, H(11, v), -R + 0.01), bevel=0.004)          # post strap
    r = K.rng(2211)
    for i in range(10):                                                   # rubbish poking out
        an = r.uniform(0, math.tau)
        q = r.uniform(0, R * 0.7)
        m = ["binbag", "paper", "cardboard" if "cardboard" in K.M else "cloth_ochre", "binbag"][i % 4]
        c.rock(m, r.uniform(0.04, 0.08), (math.cos(an) * q, H(11, v) - 0.04 + r.uniform(0, 0.08), math.sin(an) * q), seed=i, squash=0.6, detail=1)
    return a


def trash_bags():
    v = 0.08
    a = new("TrashBags0", v)
    c, w = a.p("Cloth"), a.p("Wood")
    for b, (x, y, z) in enumerate(((-3, 3, 0), (3, 3, 1), (0, 3, 4), (1, 7, 1))):
        r = (2.6 if b == 3 else 3.4) * v + 0.03
        c.rock("binbag", r, (C(x, v), C(y, v) - 0.02, C(z, v)), seed=2220 + b, squash=0.82, detail=2, floor=0.0)
        top = C(y, v) + r * 0.78
        c.lathe("binbag", [(0.0, 0.0), (0.035, 0.0), (0.012, 0.05), (0.02, 0.09), (0.0, 0.1)], (C(x, v), top - 0.02, C(z, v)), "y", 8)   # the tie
        for k in range(2):
            an = b + k * 2.2
            c.box("binbag", (0.02, 0.06, 0.01), (C(x, v) + math.cos(an) * 0.03, top + 0.08, C(z, v) + math.sin(an) * 0.03), r=(30, math.degrees(an), 0), bevel=0.0)
    w.box2("cloth_ochre", (L(-7, v), 0, L(-4, v)), (H(-4, v), H(3, v) - 0.03, H(-1, v)), bevel=0.02)   # soggy box
    w.box2("cloth_ochre", (L(-7, v), H(3, v) - 0.04, L(-4, v) - 0.04), (L(-7, v) + 0.12, H(3, v) - 0.02, H(-1, v)), r=None, bevel=0.0)
    return a


def litter(i):
    v = 0.08
    a = new("Litter%d" % i, v)
    c, s, g = a.p("Cloth"), a.p("Scrap"), a.p("Glass")
    rnd = K.NetRandom(2230 + i)
    n = 7 if i == 0 else 4
    for k in range(n):
        x, z, w_, l = rnd.next(-7, 6), rnd.next(-7, 6), 1 + rnd.next(2), 2 + rnd.next(2)
        m = "paper" if i == 1 or k % 3 else "cloth_ochre"
        if i == 1:
            m = "paper"
        rr = K.rng(k + i * 10)
        c.box(m, ((w_ + 1) * v * 0.95, 0.006 + (0.03 if i == 1 else 0.0), (l + 1) * v * 0.95),
              ((L(x, v) + H(x + w_, v)) / 2, 0.006 + (0.015 if i == 1 else 0.0), (L(z, v) + H(z + l, v)) / 2), r=(rr.uniform(-6, 6), rr.uniform(0, 40), rr.uniform(-6, 6)), bevel=0.003)
        if i == 1:                                                          # newspaper: printed columns, folded
            for col in range(3):
                c.box("black", (0.012, 0.002, (l + 1) * v * 0.8), (L(x, v) + 0.03 + col * 0.03, 0.037, (L(z, v) + H(z + l, v)) / 2), bevel=0.0)
    for k in range(3 if i == 0 else 1):
        x, z = rnd.next(-6, 6), rnd.next(-6, 6)
        m = "crimson" if k == 0 else "alu"
        s.cyl(m, 0.033, 0.12, (C(x, v), 0.033, C(z, v)), "x", 12, 0.004)              # cans on their side
        s.cyl("alu", 0.03, 0.004, (C(x, v) + 0.062, 0.033, C(z, v)), "x", 12, 0.0)
    if i == 0:
        g.lathe("glass", [(0.0, 0.0), (0.035, 0.0), (0.036, 0.16), (0.014, 0.22), (0.013, 0.26), (0.0, 0.26)], (C(4, v), 0.036, C(-5, v) - 0.1), "z", 14)
    return a


def phone_booth():
    v = 0.08
    a = new("PhoneBooth0", v)
    s, g = a.p("Scrap"), a.p("Glass")
    vbox(s, "steel_dark", -5, 0, -5, 5, 0, 5, v)
    for x in (-5, 5):
        for z in (-5, 5):
            vbox(s, "booth_red", x, 1, z, x, 27, z, v, bevel=0.012)
    vbox(s, "booth_red", -5, 25, -5, 5, 28, 5, v, bevel=0.02)
    s.lathe("booth_red", [(0.0, 0.0), (5.6 * v, 0.0), (4.0 * v, 0.12), (0.0, 0.16)], (0, H(28, v), 0), "y", 4, r=(0, 45, 0))   # domed roof
    for z in (-5, 5):                                                        # sign bands
        s.box2("cream", (L(-3, v), L(26, v), L(z, v) - (0.006 if z < 0 else 0.0)), (H(3, v), H(26, v), H(z, v) + (0.006 if z > 0 else 0.0)), bevel=0.0)
    for x in (-5, 5):
        s.box2("cream", (L(x, v) - (0.006 if x < 0 else 0), L(26, v), L(-3, v)), (H(x, v) + (0.006 if x > 0 else 0), H(26, v), H(3, v)), bevel=0.0)
    for y in range(6, 25, 6):                                                # glazing bars
        for (x0, x1, z0, z1) in ((-4, 4, -5, -5), (-5, -5, -4, 4), (5, 5, -4, 4), (-4, 4, 5, 5)):
            vbox(s, "booth_red", x0, y, z0, x1, y, z1, v, bevel=0.006)
    r = K.rng(2241)
    for side in range(3):                                                    # panes (some smashed out)
        for row in range(4):
            y0, y1 = L(row * 6 + 1, v) + 0.04, H(row * 6 + 5, v)
            if r.random() < 0.18:
                continue
            if side == 0:
                g.box2("glass_clear", (L(-4, v), y0, C(-5, v) - 0.006), (H(4, v), y1, C(-5, v) + 0.006), bevel=0.0)
            else:
                xs = C(-5 if side == 1 else 5, v)
                g.box2("glass_clear", (xs - 0.006, y0, L(-4, v)), (xs + 0.006, y1, H(4, v)), bevel=0.0)
    vbox(s, "booth_red", -4, 2, 5, 4, 11, 5, v)                              # back panel low
    vbox(s, "black", -2, 12, 4, 2, 17, 4, v, bevel=0.012)                    # the phone
    s.box2("steel", (L(-1, v), C(13, v), L(4, v) - 0.004), (H(1, v), C(15.5, v), L(4, v) + 0.004), bevel=0.003)   # keypad
    s.tube("black", [(C(1.5, v), C(16, v), L(4, v) - 0.03), (C(2.2, v), C(15, v), L(4, v) - 0.06), (C(2.2, v), C(13, v), L(4, v) - 0.06)], 0.022, 8)   # handset
    s.tube("steel", [(C(1, v), C(13, v), L(4, v)), (C(0.5, v), C(10, v), L(4, v) - 0.05), (C(1.5, v), C(8, v), L(4, v) - 0.1)], 0.006, 5)   # cord dangling
    return a


def bus_stop():
    v = 0.08
    a = new("BusStop0", v)
    a.yaw = 180.0
    s, g, c, w = a.p("Scrap"), a.p("Glass"), a.p("Cloth"), a.p("Wood")
    for x in (-19, 19):
        for z in (-6, 7):
            s.box2("steel", (L(x, v) + 0.01, 0, L(z, v) + 0.01), (H(x, v) - 0.01, H(28, v), H(z, v) - 0.01), bevel=0.01)
            s.box2("steel_dark", (L(x, v) - 0.03, 0, L(z, v) - 0.03), (H(x, v) + 0.03, 0.012, H(z, v) + 0.03), bevel=0.003)
    s.box2("steel", (L(-21, v), L(29, v), L(-8, v)), (H(21, v), L(29, v) + 0.03, H(8, v)), bevel=0.008)
    K.corrugated(s, "tin", L(-21, v), H(21, v), L(-8, v), H(8, v), 0.012, 0.1, 0.004,
                 Matrix.Translation((0, L(29, v) + 0.05, 0)) @ Matrix(((1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 1, 0), (0, 0, 0, 1))), samples_per_period=4)
    s.box2("steel_dark", (L(-21, v), L(29, v) - 0.02, L(-8, v)), (H(21, v), H(30, v), L(-8, v) + 0.04), bevel=0.004)   # fascia
    for x in range(-18, 19, 6):                                             # glass back wall in frames, smashed panes
        s.box2("steel", (C(x, v) - 0.012, L(3, v), C(7, v) - 0.015), (C(x, v) + 0.012, H(26, v), C(7, v) + 0.015), bevel=0.003)
    for y in (3, 26):
        s.box2("steel", (L(-18, v), C(y, v) - 0.012, C(7, v) - 0.015), (H(18, v), C(y, v) + 0.012, C(7, v) + 0.015), bevel=0.003)
    for i in range(6):
        for j in range(3):
            x0 = -18 + i * 6
            if K.pal_hash(x0 // 6, (3 + j * 8) // 8, 0, 2253) < 0.25:
                if K.rng(i * 3 + j).random() < 0.6:
                    for kk in range(3):                                      # shards left in the frame
                        rr = K.rng(i * 9 + j * 3 + kk)
                        g.tri_plate("glass_clear", (C(x0, v) + rr.uniform(0, 0.4), C(3 + j * 8, v), C(7, v)), (C(x0 + 6, v) - rr.uniform(0, 0.3), C(3 + j * 8, v), C(7, v)),
                                    (C(x0 + 3, v) + rr.uniform(-0.2, 0.2), C(3 + j * 8, v) + rr.uniform(0.1, 0.4), C(7, v)), 0.006)
                continue
            g.box2("glass_clear", (C(x0, v) + 0.012, C(3 + j * 7.66, v), C(7, v) - 0.005), (C(x0 + 6, v) - 0.012, C(3 + (j + 1) * 7.66, v), C(7, v) + 0.005), bevel=0.0)
    s.box2("steel", (L(-19, v) - 0.01, L(4, v), L(-5, v)), (H(-19, v) + 0.01, H(24, v), H(5, v)), bevel=0.008)   # advert frame
    c.box2("poster", (H(-19, v) + 0.01, L(5, v), L(-4, v)), (H(-19, v) + 0.016, H(23, v), H(4, v)), bevel=0.0)
    c.box2("crimson", (H(-19, v) + 0.016, L(12, v), L(-2, v)), (H(-19, v) + 0.02, H(18, v), H(2, v)), bevel=0.0)
    c.box2("cream", (H(-19, v) + 0.016, L(20, v), L(-3, v)), (H(-19, v) + 0.02, H(21, v), H(3, v)), bevel=0.0)
    w.box2("park_green", (L(-14, v), L(6, v), L(3, v)), (H(14, v), H(6, v), H(6, v)), bevel=0.012)   # bench
    for x in (-12, 12):
        s.box2("steel_dark", (L(x, v), 0, L(5, v)), (H(x, v), L(6, v), H(5, v)), bevel=0.006)
    s.cyl("steel", 0.035, H(36, v), (C(23, v), H(36, v) / 2, C(-6, v)), "y", 10, 0.004)   # the sign
    s.cyl("sign_navy", 3.7 * v, 0.02, (C(23, v), C(34, v), C(-7, v)), "z", 24, 0.004)
    s.box2("cream", (C(23, v) - 0.26, C(34, v) - 0.06, C(-7, v) - 0.015), (C(23, v) + 0.26, C(34, v) + 0.06, C(-7, v) - 0.01), bevel=0.0)
    s.torus("steel", 3.7 * v, 0.008, (C(23, v), C(34, v), C(-7, v)), "z", 24, 4)
    return a


def traffic_light():
    v = 0.08
    a = new("TrafficLight0", v)
    s, g = a.p("Scrap"), a.p("Glass")
    s.cyl("steel", 0.065, H(46, v), (C(0.5, v), H(46, v) / 2, C(0.5, v)), "y", 14, 0.004)
    s.box2("steel_dark", (L(-1, v), 0, L(-1, v)), (H(2, v), H(1, v), H(2, v)), bevel=0.012)
    for (x, z) in ((-1, -1), (2, -1), (-1, 2), (2, 2)):
        s.cyl("chrome", 0.012, 0.02, (C(x, v), H(1, v) + 0.01, C(z, v)), "y", 6, 0.0)
    s.tube("steel", [(C(0.5, v), C(46, v), C(0.5, v)), (C(0.5, v), C(47, v), C(0, v)), (C(0.5, v), C(47, v), L(-12, v))], 0.05, 10)   # arm
    for z in (-11, 0):
        y0 = 34 if z == 0 else 37
        s.box2("black", (L(-1, v), L(y0, v), L(z - 1, v)), (H(2, v), H(y0 + 9, v), H(z + 1, v)), bevel=0.02)
        if z != 0:
            s.rod("steel", (C(0.5, v), H(y0 + 9, v), C(z, v)), (C(0.5, v), C(46.5, v), C(z, v)), 0.02, 6)
        for k, m in ((7, "lens_red"), (4, "lens_amber"), (1, "lens_green")):
            yy = C(y0 + k, v) + 0.04
            g.cyl(m, 0.1, 0.02, (C(0.5, v), yy, L(z - 1, v) - 0.008), "z", 18, 0.006)
            s.lathe("black", [(0.11, 0.0), (0.12, 0.0), (0.1, 0.12), (0.09, 0.12)], (C(0.5, v), yy + 0.02, L(z - 1, v) - 0.004), "z", 16, close=False, r=(0, 180, 0))   # visor
    s.box2("black", (L(2, v), L(22, v), L(0, v)), (H(4, v), H(26, v), H(1, v)), bevel=0.012)
    s.box2("cream", (H(4, v), C(24, v) - 0.03, C(0.5, v) - 0.03), (H(4, v) + 0.012, C(24, v) + 0.03, C(0.5, v) + 0.03), bevel=0.004)
    return a


def stop_sign():
    v = 0.08
    a = new("StopSign0", v)
    s = a.p("Scrap")
    s.box2("steel", (-0.03, 0, -0.03), (0.03, H(32, v) + 0.2, 0.03), bevel=0.006)
    oct_ = [(math.cos(math.radians(22.5 + k * 45)) * 5.6 * v * 1.08, C(33, v) + math.sin(math.radians(22.5 + k * 45)) * 5.6 * v * 1.08) for k in range(8)]
    s.prism("cream", oct_, C(-1, v) - 0.008, C(-1, v) + 0.004, plane="xy")
    inner = [(x * 0.88, C(33, v) + (y - C(33, v)) * 0.88) for x, y in oct_]
    s.prism("sign_red", inner, C(-1, v) - 0.012, C(-1, v) - 0.006, plane="xy")
    for k, x in enumerate((-3, -1, 1, 3)):                                   # S T O P strokes
        s.box2("cream", (C(x, v) - 0.03, C(33, v) - 0.06, C(-1, v) - 0.014), (C(x, v) + 0.03, C(33, v) + 0.06, C(-1, v) - 0.011), bevel=0.0)
    r = K.rng(2271)
    for k in range(9):                                                     # buckshot
        s.cyl("black", 0.008, 0.03, (r.uniform(-0.3, 0.3), C(33, v) + r.uniform(-0.3, 0.3), C(-1, v) - 0.004), "z", 6, 0.0)
    for y in (31, 35):
        s.box2("steel_dark", (-0.04, C(y, v) - 0.015, -0.02), (0.04, C(y, v) + 0.015, C(-1, v) + 0.004), bevel=0.003)
    return a


def hydrant():
    v = 0.08
    a = new("Hydrant0", v)
    s = a.p("Scrap")
    s.lathe("booth_red", [(0.0, 0.0), (2.7 * v, 0.0), (2.7 * v, 0.04), (2.0 * v, 0.06), (2.0 * v, H(8, v) - 0.04), (2.2 * v, H(8, v)), (0.0, H(8, v))], (0, 0, 0), "y", 20)
    s.lathe("ochre", [(0.0, 0.0), (1.6 * v, 0.0), (1.4 * v, H(10, v) - L(9, v) - 0.03), (0.4 * v, H(10, v) - L(9, v)), (0.0, H(10, v) - L(9, v))], (0, L(9, v), 0), "y", 16)
    s.cyl("ochre", 0.02, 0.05, (0, H(10, v) + 0.02, 0), "y", 5, 0.004)
    for sx in (-1, 1):
        s.cyl("booth_red", 1.3 * v, 2.4 * v, (sx * 2.2 * v, C(6, v), 0), "x", 14, 0.008)
        s.cyl("ochre", 1.2 * v, 0.03, (sx * 3.4 * v, C(6, v), 0), "x", 6, 0.004)
        s.torus("ochre", 0.02, 0.006, (sx * 3.6 * v, C(6, v) - 0.08, 0), "x", 8, 3)   # chain eye
    s.cyl("booth_red", 1.4 * v, 1.6 * v, (0, C(6, v), -2.0 * v), "z", 14, 0.008)
    s.cyl("ochre", 1.3 * v, 0.03, (0, C(6, v), -2.9 * v), "z", 6, 0.004)
    for i in range(6):
        an = i / 6 * math.tau
        s.cyl("steel", 0.012, 0.02, (math.cos(an) * 2.4 * v, 0.07, math.sin(an) * 2.4 * v), "y", 6, 0.0)
    return a


def bollard():
    v = 0.08
    a = new("Bollard0", v)
    s = a.p("Scrap")
    R = 1.9 * v
    s.lathe("steel", [(0.0, 0.0), (R, 0.0), (R, L(8, v)), (0.0, L(8, v))], (0, 0, 0), "y", 18)
    s.lathe("hazard", [(0.0, 0.0), (R + 0.002, 0.0), (R + 0.002, 2 * v), (0.0, 2 * v)], (0, L(8, v), 0), "y", 18)
    s.lathe("steel", [(0.0, 0.0), (R, 0.0), (R, 2 * v), (R * 0.9, 2 * v + 0.03), (0.0, 2 * v + 0.05)], (0, H(9, v), 0), "y", 18)
    s.cyl("cream", R + 0.003, 0.02, (0, C(8.5, v), 0), "y", 18, 0.0)
    return a


def planter():
    v = 0.08
    a = new("Planter0", v)
    st, w = a.p("Stone"), a.p("Wood")
    st.box2("conc", (L(-7, v), 0, L(-7, v)), (H(7, v), H(6, v), H(7, v)), bevel=0.02)
    st.box2("conc_dark", (L(-7, v) - 0.01, H(6, v) - 0.05, L(-7, v) - 0.01), (H(7, v) + 0.01, H(6, v), H(7, v) + 0.01), bevel=0.01)   # coping
    st.box2("soil", (L(-6, v), L(6, v), L(-6, v)), (H(6, v), H(6, v) + 0.01, H(6, v)), bevel=0.0)
    rnd = K.NetRandom(2302)
    for x in range(-6, 7):
        for z in range(-6, 7):
            if rnd.next_double() < 0.3:
                continue
            h = 1 + rnd.next(3)
            dead = rnd.next_double() < 0.25
            flower = (not dead) and rnd.next_double() < 0.35
            if flower:
                rnd.next(5)
            m = "scrub" if dead else "leaf"
            for t in range(3):
                an = (x * 7 + z * 13 + t * 2.1)
                w.box(m, (0.01, h * v * 1.1, 0.03), (C(x, v) + math.cos(an) * 0.02, H(6, v) + h * v * 0.5, C(z, v) + math.sin(an) * 0.02),
                      r=(math.cos(an) * 18, math.degrees(an), math.sin(an) * 18), bevel=0.0)
            if flower:
                w.sphere("flower_red" if (x + z) % 2 else "flower_yellow", 0.025, (C(x, v), H(6, v) + h * v + 0.02, C(z, v)), (1, 0.6, 1), 8, 5)
    return a


def hedge():
    v = 0.1
    a = new("Hedge0", v, "Vegetation", "vegetation")
    lf, br = a.p("Leaves", "Wood"), a.p("Branches", "Wood")
    r = K.rng(2310)
    for i in range(26):                                                   # clumps along the hedge, rounded top
        x = -1.2 + i * 2.4 / 25 + r.uniform(-0.04, 0.04)
        for j in range(2):
            y = 0.35 + j * 0.42 + r.uniform(-0.05, 0.05)
            m = "scrub" if r.random() < 0.3 else "leaf"
            lf.rock(m, 0.3 - j * 0.04, (x, y, r.uniform(-0.05, 0.05)), seed=i * 3 + j, squash=0.85, detail=2, scale=(0.8, 1.0, 1.0))
    for i in range(6):
        x = -1.1 + i * 0.44
        br.rod("bark", (x, 0.0, 0.0), (x + r.uniform(-0.1, 0.1), 0.4, r.uniform(-0.1, 0.1)), 0.03, 6)
    lf.sway = lambda p: max(0.0, min(1.0, 0.2 + 0.6 * p.y / 1.1))
    br.sway = lambda p: 0.0
    return a


def park_tree(vv):
    v = 0.16
    a = new("ParkTree%d" % vv, v, "Vegetation", "vegetation")
    tr, lf = a.p("Trunk", "Wood"), a.p("Crown", "Wood")
    top = 20 if vv == 0 else 16
    tr.tube("bark", [(0, 0, 0), (0.25 * v, top * 0.5 * v, 0), (0.5 * v, top * v, 0)], [(1.3 + 0.5) * v * 0.8, (1.3 + 0.5) * v * 0.65, (1.3 + 0.5) * v * 0.5], 12)
    for k in range(5):                                                    # root flare
        an = k / 5 * math.tau + vv
        tr.tube("bark", [(0, 0.12, 0), (math.cos(an) * 0.32, 0.0, math.sin(an) * 0.32)], [0.08, 0.02], 6)
    rnd = K.NetRandom(2321 + vv)
    crowns = [Vector((0, top + 6, 0))]
    for b in range(4):
        an = b * 1.57 + rnd.next_double() * 0.8
        end = Vector((math.cos(an) * 7.0, top + 2 + rnd.next(4), math.sin(an) * 7.0))
        tr.tube("bark", [(0, (top - 4 + b) * v, 0), tuple(end * v * 0.6 + Vector((0, (top - 4 + b) * v * 0.4, 0))), tuple(end * v)], [(0.7 + 0.5) * v * 0.6, 0.06, 0.03], 8)
        crowns.append(end + Vector((0, 3, 0)))
    r = K.rng(2322 + vv * 5)
    for ci, c in enumerate(crowns):
        rad = (8.0 if c.y > top + 5 else 6.0) * v
        if vv == 1 and c.x > 2:
            for t in range(3):                                            # the dead half: bare twigs
                an = r.uniform(0, math.tau)
                tr.rod("deadwood", tuple(c * v - Vector((0, 3 * v, 0))), tuple(c * v + Vector((math.cos(an) * 0.5, r.uniform(0.1, 0.5), math.sin(an) * 0.5))), 0.015, 5)
            continue
        for k in range(5):                                                # leafy clumps round each crown centre
            off = Vector((r.uniform(-1, 1), r.uniform(-0.6, 0.6), r.uniform(-1, 1))) * rad * 0.45
            lf.rock("leaf", rad * r.uniform(0.55, 0.75), tuple(c * v + off), seed=ci * 11 + k, squash=0.75, detail=2)
    Hh = (top + 14) * v
    tr.sway = lambda p: max(0.0, min(1.0, (p.y / Hh) ** 2.0))
    lf.sway = lambda p: max(0.0, min(1.0, 0.45 + 0.55 * p.y / Hh))
    return a


def fountain():
    v = 0.12
    a = new("Fountain0", v, kind="building")
    st, g, s = a.p("Stone"), a.p("Glass"), a.p("Scrap")
    st.lathe("conc", [(18.2 * v, H(4, v) - 0.02), (18.2 * v, L(0, v)), (20.2 * v, L(0, v)), (20.4 * v, H(4, v) - 0.04), (20.0 * v, H(4, v)), (18.4 * v, H(4, v))], (0, 0, 0), "y", 48)
    st.lathe("conc_dark", [(0.0, L(0, v)), (18.6 * v, L(0, v)), (18.6 * v, H(0, v)), (0.0, H(0, v))], (0, 0, 0), "y", 40)
    g.lathe("pond", [(0.0, 0.0), (18.25 * v, 0.0), (18.25 * v, 0.02), (0.0, 0.02)], (0, C(2, v), 0), "y", 40)
    st.lathe("conc", [(0.0, L(1, v)), (2.9 * v, L(1, v)), (2.6 * v, L(4, v)), (2.3 * v, L(10, v)), (2.8 * v, L(12, v)), (0.0, L(12, v))], (0, 0, 0), "y", 20)
    st.lathe("conc", [(2.0 * v, L(12, v) - 0.06), (7.3 * v, L(13, v)), (7.5 * v, H(13, v)), (6.0 * v, H(13, v)), (5.6 * v, L(13, v) + 0.04), (0.0, L(13, v) + 0.04)], (0, 0, 0), "y", 32)   # lower bowl
    g.lathe("pond", [(0.0, 0.0), (5.9 * v, 0.0), (5.9 * v, 0.015), (0.0, 0.015)], (0, C(13, v), 0), "y", 24)
    st.lathe("conc", [(0.0, L(13, v)), (1.8 * v, L(13, v)), (1.5 * v, L(18, v)), (1.9 * v, H(22, v)), (0.0, H(22, v))], (0, 0, 0), "y", 16)   # column
    st.lathe("conc", [(1.2 * v, L(18, v) - 0.04), (3.7 * v, L(18, v)), (3.9 * v, H(19, v)), (2.6 * v, H(19, v)), (2.4 * v, H(18, v)), (0.0, H(18, v))], (0, 0, 0), "y", 24)   # upper bowl
    s.lathe("bronze", [(0.0, L(23, v)), (1.0 * v, L(23, v)), (0.7 * v, C(24, v)), (1.2 * v, H(24, v)), (0.0, H(25, v))], (0, 0, 0), "y", 12)   # spout finial
    r = K.rng(2332)
    for k in range(24):                                                   # algae scum
        an = r.uniform(0, math.tau)
        q = r.uniform(10, 17.5) * v
        g.cyl("cloth_green", r.uniform(0.08, 0.25), 0.004, (math.cos(an) * q, C(2, v) + 0.022, math.sin(an) * q), "y", 10, 0.0)
    for k in range(5):                                                    # chipped rim chunks lying about
        an = math.radians(30 + k * 7)
        st.rock("conc", 0.07, (math.cos(an) * 21 * v, 0.03, math.sin(an) * 21 * v), seed=k, squash=0.6, detail=1)
    return a


def statue():
    v = 0.1
    a = new("Statue0", v, kind="building")
    st, s = a.p("Stone"), a.p("Scrap")
    st.box2("conc_dark", (L(-6, v), 0, L(-6, v)), (H(6, v), H(1, v), H(6, v)), bevel=0.02)
    st.box2("conc", (L(-5, v), L(2, v), L(-5, v)), (H(5, v), H(13, v) - 0.06, H(5, v)), bevel=0.02)
    st.box2("conc_light", (L(-5, v) - 0.04, H(13, v) - 0.08, L(-5, v) - 0.04), (H(5, v) + 0.04, H(13, v), H(5, v) + 0.04), bevel=0.015)   # cornice
    s.box2("bronze", (L(-3, v), L(7, v), L(-6, v) + 0.03), (H(3, v), H(10, v), L(-5, v)), bevel=0.01)   # plaque
    for i in range(4):
        s.box2("verdigris", (L(-2, v) + i * 0.1, C(8.5, v) - 0.004, L(-6, v) + 0.026), (L(-2, v) + i * 0.1 + 0.07, C(8.5, v) + 0.004, L(-6, v) + 0.03), bevel=0.0)
    for k, (x, y) in enumerate(((-4, 4), (-3, 5), (-2, 4), (2, 3), (3, 4), (4, 3))):   # tag on the plinth
        m = K.M["spray"][0] if y % 2 == 0 else K.M["spray"][2]
        st.box(m, (0.14, 0.06, 0.004), (C(x, v), C(y, v), L(-5, v) - 0.003), r=(0, 0, 25 * (k % 3 - 1)), bevel=0.0)
    # the figure: a man in a greatcoat, one arm raised (bronze, green with age)
    m = "bronze"
    for sx in (-1.5, 1.5):
        s.superloft(m, [(L(14, v), C(sx, v), 0, 0.075, 0.09), (C(18, v), C(sx, v), 0, 0.07, 0.08), (H(22, v), C(sx, v), 0, 0.08, 0.085)], 10, 2.2, axis="y")
        s.box2(m, (C(sx, v) - 0.08, L(14, v), -0.12), (C(sx, v) + 0.08, L(14, v) + 0.05, 0.1), bevel=0.02)   # boots
    s.superloft("verdigris", [(L(20, v), 0, 0, 0.3, 0.2), (C(23, v), 0, 0, 0.28, 0.19), (C(27, v), 0, 0, 0.27, 0.18), (H(31, v), 0, 0, 0.32, 0.2), (H(31, v) + 0.06, 0, 0, 0.12, 0.1)], 16, 2.4, axis="y")   # coat
    s.sphere(m, 0.13, (0, C(33.5, v), 0), (0.9, 1.1, 1.0), 14, 10)                                          # head
    s.cyl(m, 0.06, 0.1, (0, H(31, v) + 0.05, 0), "y", 10, 0.0)                                              # neck
    s.lathe("verdigris", [(0.0, 0.0), (0.15, 0.0), (0.15, 0.02), (0.1, 0.03), (0.1, 0.12), (0.0, 0.13)], (0, H(34, v) - 0.06, 0), "y", 14)   # hat
    s.tube(m, [(C(3, v), C(30, v), 0), (C(4.5, v), C(33.5, v), C(-1, v)), (C(6, v), C(37, v), C(-2, v))], [0.07, 0.06, 0.05], 10)   # raised arm
    s.sphere(m, 0.055, (C(6.2, v), C(37.6, v), C(-2.1, v)), (1, 1, 1), 10, 6)
    s.tube(m, [(C(-3, v), C(30, v), 0), (C(-3.8, v), C(27, v), C(-0.5, v)), (C(-4, v), C(24, v), C(-1, v))], [0.07, 0.06, 0.05], 10)
    s.rock("stack_white" if "stack_white" in K.M else "cream", 0.04, (0.03, C(35.2, v), 0.02), seed=3, squash=0.4, detail=1)   # pigeon mess
    return a


def pond():
    v = 0.12
    a = new("Pond0", v)
    st, g, w = a.p("Stone"), a.p("Glass"), a.p("Wood")
    n = 40
    rimpts = []
    for k in range(n):
        an = k / n * math.tau
        nz = 0.06 * math.sin(an * 3 + 1.3) + 0.04 * math.sin(an * 5 + 0.4)
        rimpts.append((math.cos(an) * 26 * v * (1 + nz), math.sin(an) * 22 * v * (1 + nz)))
    g.prism("pond", [(x * 0.9, z * 0.9) for x, z in rimpts], L(0, v), C(0, v) + 0.02)
    g.prism("soil", [(x * 0.9, z * 0.9) for x, z in rimpts], L(0, v) - 0.02, L(0, v))
    r = K.rng(2352)
    for k, (x, z) in enumerate(rimpts):                                  # kerb stones round the edge
        st.rock("rock_grey" if k % 3 else "conc", r.uniform(0.09, 0.14), (x * 0.94, 0.02, z * 0.94), seed=k, squash=0.55, detail=1, floor=L(0, v), yaw=r.uniform(0, 360))
    for k in range(18):                                                   # reed clumps at the margins
        an = r.uniform(0, math.tau)
        q = r.uniform(0.75, 0.86)
        cx, cz = math.cos(an) * 26 * v * q, math.sin(an) * 22 * v * q
        for t in range(5):
            hh = r.uniform(0.2, 0.55)
            ox, oz = r.uniform(-0.05, 0.05), r.uniform(-0.05, 0.05)
            w.rod("scrub", (cx + ox, 0.0, cz + oz), (cx + ox * 2 + r.uniform(-0.03, 0.03), hh, cz + oz * 2), 0.008, 4)
        if r.random() < 0.4:
            w.cyl("wood_dark_y", 0.012, 0.07, (cx, 0.45, cz), "y", 6, 0.004)   # bulrush head
    for k in range(10):                                                   # algae and lily pads
        an = r.uniform(0, math.tau)
        q = r.uniform(0.2, 0.7)
        g.cyl("cloth_green", r.uniform(0.06, 0.14), 0.006, (math.cos(an) * 26 * v * q, C(0, v) + 0.024, math.sin(an) * 22 * v * q), "y", 10, 0.0)
    w.sway = lambda p: max(0.0, min(1.0, p.y / 0.55))
    return a


def well():
    v = 0.1
    a = new("Well0", v, kind="building")
    st, w, s = a.p("Stone"), a.p("Wood"), a.p("Scrap")
    for row in range(3):                                                  # stone courses
        y0, y1 = L(row * 3, v), H(row * 3 + 2, v)
        st.lathe("stone" if row % 2 == 0 else "conc", [(5.5 * v, y0), (7.5 * v, y0), (7.5 * v, y1), (5.5 * v, y1)], (0, 0, 0), "y", 28, r=(0, row * 7, 0))
    st.lathe("conc_light", [(5.3 * v, H(8, v)), (7.8 * v, H(8, v)), (7.8 * v, H(8, v) + 0.06), (5.3 * v, H(8, v) + 0.06)], (0, 0, 0), "y", 28)   # cap
    st.cyl("black", 5.5 * v, 0.01, (0, H(0, v) - 0.3, 0), "y", 20, 0.0)                                   # the dark shaft
    for x in (-6, 6):
        w.box2(K.wood("dark", "y"), (L(x, v), L(9, v) - 0.3, -0.06), (H(x, v), H(28, v), 0.06), bevel=0.012)
    w.cyl(K.wood("", "x"), 1.5 * v, 12 * v, (0, C(20, v), 0), "x", 14, 0.008)                                 # windlass
    for k in range(5):
        w.torus("rope", 1.5 * v + 0.012, 0.012, (C(-2 + k * 0.6, v), C(20, v), 0), "x", 14, 4)
    w.tube("steel", [(H(6, v), C(20, v), 0), (H(6, v) + 0.08, C(20, v), 0), (H(6, v) + 0.08, C(20, v) + 0.18, 0), (H(6, v) + 0.18, C(20, v) + 0.18, 0)], 0.012, 6)   # crank
    for z in range(-6, 7):                                                # gable roof of red tiles (rows step up to the ridge)
        y = 25 + (6 - abs(z)) / 2
        w.box2("roof_red", (L(-8, v), C(y, v), L(z, v)), (H(8, v), C(y, v) + 0.04, H(z, v) + 0.03), bevel=0.006)
    w.box2(K.wood("dark", "x"), (L(-8, v), C(28, v), -0.05), (H(8, v), C(28, v) + 0.08, 0.05), bevel=0.01)   # ridge board
    s.lathe("steel", [(0.0, 0.0), (1.2 * v, 0.0), (1.6 * v, 4 * v), (1.5 * v, 4 * v), (1.1 * v, 0.01), (0.0, 0.01)], (C(2, v), L(12, v), 0), "y", 14, close=False)   # bucket
    s.torus("steel", 1.6 * v, 0.006, (C(2, v), L(12, v) + 4 * v + 0.05, 0), "z", 12, 3)
    w.rod("rope", (C(2, v), L(16, v), 0), (C(2, v), C(20, v) - 1.5 * v, 0), 0.008, 5)
    return a


def graffiti(vv):
    v = 0.1
    a = new("Graffiti%d" % vv, v, kind="building")
    st = a.p("Stone")
    rnd = K.NetRandom(2373 + vv)
    wall_m = "conc" if vv == 0 else "brick"
    tops = []
    for x in range(-16, 17):
        n = 0.5 + 0.5 * math.sin(x * 0.3 * 1.7 + vv * 2.1) * math.cos(x * 0.13 + vv)
        top = 20 + int(n * 4) - (x - 10 if x > 10 else 0)
        tops.append(top)
    x = -16
    while x <= 16:                                                       # columns of equal top merged into blocks
        t = tops[x + 16]
        x1 = x
        while x1 + 1 <= 16 and tops[x1 + 1 + 16] == t:
            x1 += 1
        st.box2(wall_m, (L(x, v), 0, L(-1, v)), (H(x1, v), H(t, v), H(1, v)), bevel=0.006)
        x = x1 + 1
    r = K.rng(2374 + vv)
    for x in range(12, 17):                                             # broken end: loose blocks at the foot
        st.rock(wall_m, 0.1, (C(x + 1, v) + r.uniform(0, 0.3), 0.04, r.uniform(-0.3, 0.3)), seed=x, squash=0.6, detail=1)
    sprays = K.M["spray"]
    c0, c1 = sprays[rnd.next(6)], sprays[rnd.next(6)]
    for i in range(6):                                                   # tags on the -Z face: letters + outline + drips
        cx, cy, w_, h_ = -13 + i * 5 + rnd.next(2), 6 + rnd.next(5), 1 + rnd.next(2), 4 + rnd.next(4)
        if cy + h_ > tops[cx + 16]:
            h_ = max(1, tops[cx + 16] - cy)
        st.box2(c1, (L(cx - w_, v), L(cy, v), L(-1, v) - 0.004), (H(cx + w_, v), H(cy + h_, v), L(-1, v) - 0.001), bevel=0.0)
        st.box2(c0, (L(cx - w_, v) + 0.025, L(cy, v) + 0.025, L(-1, v) - 0.006), (H(cx + w_, v) - 0.025, H(cy + h_, v) - 0.025, L(-1, v) - 0.004), bevel=0.0)
        dl = rnd.next(4)
        st.box2(c0, (C(cx, v) - 0.012, L(cy - 1 - dl, v), L(-1, v) - 0.005), (C(cx, v) + 0.012, L(cy, v), L(-1, v) - 0.002), bevel=0.0)
    if vv == 1:                                                         # the gang mark: a skull
        st.sphere("stack_white" if "stack_white" in K.M else "cream", 0.3, (C(8, v), C(16.5, v), L(-1, v) - 0.01), (1.0, 0.95, 0.03), 18, 10)
        for ex in (-2, 2):
            st.sphere("black", 0.07, (C(8 + ex, v), C(17, v), L(-1, v) - 0.016), (1.0, 1.0, 0.03), 10, 6)
        for tx in (-2, 0, 2):
            st.box2("black", (C(8 + tx, v) - 0.02, C(13.5, v), L(-1, v) - 0.016), (C(8 + tx, v) + 0.02, C(14.6, v), L(-1, v) - 0.012), bevel=0.0)
    return a


def drum(burning, seed):
    v = 0.08
    a = new("BurnBarrel0" if burning else "OilDrum0", v)
    s, w = a.p("Scrap"), a.p("Wood")
    paint = "rust" if burning else ["navy", "booth_red", "olive"][seed % 3]
    R = 3.8 * v * 0.92
    prof = [(0.0, 0.0), (R - 0.01, 0.0), (R, 0.012), (R, H(10, v) - 0.012), (R - 0.01, H(10, v))]
    prof += [(R - 0.015, H(10, v)), (R - 0.015, 0.1), (0.0, 0.1)] if burning else [(0.0, H(10, v) - 0.004)]
    s.lathe(paint, prof, (0, 0, 0), "y", 28)
    for y in (3, 7):                                                     # rolling hoops
        s.torus("steel" if not burning else "rust_dark" if "rust_dark" in K.M else "rust", R + 0.004, 0.012, (0, C(y, v), 0), "y", 28, 4)
    s.torus("steel" if not burning else "rust", R - 0.006, 0.01, (0, H(10, v) - 0.004, 0), "y", 28, 4)
    if not burning:
        s.cyl("steel", 0.03, 0.012, (R * 0.55, H(10, v) + 0.006, 0), "y", 10, 0.002)   # bung caps
        s.cyl("steel", 0.018, 0.012, (-R * 0.55, H(10, v) + 0.006, 0.05), "y", 8, 0.002)
        s.box2("cream", (-0.12, C(5, v) - 0.08, -R - 0.004), (0.12, C(5, v) + 0.08, -R + 0.01), bevel=0.0)   # label
        return a
    r = K.rng(2381)
    for k in range(12):                                                  # punched holes glowing
        an = k / 12 * math.tau + r.uniform(-0.2, 0.2)
        y = C(r.choice((2, 4, 5, 6, 8)), v)
        s.box("ember", (0.04, 0.035, 0.03), (math.cos(an) * R, y, math.sin(an) * R), r=(0, -math.degrees(an), 0), bevel=0.008)
    for k in range(7):                                                   # charred wood heap inside
        an = r.uniform(0, math.tau)
        w.rod(K.wood("dark", "x"), (math.cos(an) * 0.16, C(5, v), math.sin(an) * 0.16), (-math.cos(an) * 0.05, C(8.5, v) + r.uniform(0, 0.08), -math.sin(an) * 0.05), 0.03, 6)
    for k in range(6):
        an = r.uniform(0, math.tau)
        s.sphere("ember", 0.025, (math.cos(an) * 0.12, C(8.6, v), math.sin(an) * 0.12), (1, 0.6, 1), 6, 4)
    w.cyl("ash", R - 0.03, 0.04, (0, C(4, v), 0), "y", 20, 0.0)
    return a


# ------------------------------------------------------------------------------------------ buildings and fences
def warehouse(seed):
    v = 0.2
    a = new("Warehouse%d" % seed, v, "Building", "building")
    a.yaw = 160.0                                                         # preview: the street side
    st, s, g, w = a.p("Stone"), a.p("Scrap"), a.p("Glass"), a.p("Wood")
    wv, lv, hv = 32, 23, 24
    X0, X1, Z0, Z1 = L(-wv, v), H(wv, v), L(-lv, v), H(lv, v)
    st.box2("conc_paving", (X0, L(0, v), Z0), (X1, H(0, v), Z1), bevel=0.02)
    # plinth: two courses of concrete round the walls (not at the roller door / door)
    for (x0, x1, z0, z1) in ((X0, X1, Z1 - v, Z1), (X0, X0 + v, Z0, Z1), (X1 - v, X1, Z0, Z1), (X0, L(-10, v), Z0, Z0 + v), (H(10, v), L(15, v), Z0, Z0 + v), (H(18, v), X1, Z0, Z0 + v)):
        st.box2("conc", (x0, L(1, v), z0), (x1, H(2, v), z1), bevel=0.02)
    yw0, yw1 = L(3, v), H(hv, v)
    # corrugated walls (ribs vertical); the front has the roller door and a personnel door
    wall_m = "tin_rust_v" if seed == 1 else "tin_v"
    rot_wall_z = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, -1, 0, 0), (0, 0, 0, 1)))       # (u, h, v) -> (u, v, -h): ribs up the wall, face along z
    rot_wall_x = Matrix(((0, 1, 0, 0), (0, 0, 1, 0), (1, 0, 0, 0), (0, 0, 0, 1)))        # (u, h, v) -> (h, v, u): face along x
    K.corrugated(s, wall_m, X0, X1, yw0, yw1, 0.025, 0.2, 0.006, Matrix.Translation((0, 0, Z1 - 0.03)) @ rot_wall_z, samples_per_period=4)
    for xs in (X0 + 0.03, X1 - 0.03):
        K.corrugated(s, wall_m, Z0, Z1, yw0, yw1, 0.025, 0.2, 0.006, Matrix.Translation((xs, 0, 0)) @ rot_wall_x, samples_per_period=4)
    for (u0, u1, y0) in ((X0, L(-10, v), yw0), (H(10, v), L(15, v), yw0), (H(18, v), X1, yw0), (L(-10, v), H(10, v), H(16, v)), (L(15, v), H(18, v), H(10, v))):
        K.corrugated(s, wall_m, u0, u1, y0, yw1, 0.025, 0.2, 0.006, Matrix.Translation((0, 0, Z0 + 0.03)) @ rot_wall_z, samples_per_period=4)
    # roller door: slats (seed 1 jammed half open)
    y_lo = H(7, v) if seed == 1 else L(1, v)
    yy = y_lo
    while yy < H(16, v) - 0.01:
        s.box2("steel", (L(-10, v), yy + 0.005, Z0 + 0.04), (H(10, v), min(H(16, v), yy + 0.1) - 0.005, Z0 + 0.07), bevel=0.01)
        yy += 0.1
    s.box2("steel_dark", (L(-11, v), H(16, v), Z0 - 0.02), (H(11, v), H(17, v) + 0.1, Z0 + 0.3), bevel=0.02)   # roller box
    for x in (-11, 11):                                                   # guides + hazard posts
        s.box2("steel_dark", (L(x, v) + 0.05, L(1, v), Z0 - 0.02), (H(x, v) - 0.05, H(16, v), Z0 + 0.1), bevel=0.01)
        for k in range(8):
            s.box2("hazard" if k % 2 == 0 else "black", (L(x, v) + (0.0 if x < 0 else 0.1), L(3, v) + k * 0.33, Z0 - 0.025), (H(x, v) - (0.1 if x < 0 else 0.0), L(3, v) + (k + 1) * 0.33, Z0 - 0.015), bevel=0.0)
    s.box2("steel_dark", (L(15, v) - 0.05, L(1, v), Z0 - 0.02), (H(18, v) + 0.05, H(10, v) + 0.06, Z0 + 0.06), bevel=0.01)   # door frame
    s.box("rust", (0.6, 2.0, 0.05), (C(17, v) + 0.45, L(1, v) + 1.02, Z0 - 0.25), r=(0, 50, 0), bevel=0.01)   # door hanging open
    # gable roof: ridge along x at z = 0, rise (l + 1 - |z|) / 5 voxels
    ridge = H(hv + 1 + (lv + 1) / 5, v)
    eave = H(hv + 1, v)
    for sz in (-1, 1):
        ang = math.atan2(ridge - eave, Z1 + v)
        ln = math.hypot(ridge - eave, Z1 + v)
        mt = Matrix.Translation((0, (ridge + eave) / 2, sz * (Z1 + v) / 2)) @ Matrix.Rotation(sz * ang, 4, "X")
        roof_m = "tin_rust" if seed == 1 else "tin"
        if seed != 1:
            K.corrugated(s, roof_m, X0 - v, X1 + v, -ln / 2, ln / 2, 0.025, 0.2, 0.006, mt, samples_per_period=4)
        else:                                                             # the fallen-in bay (10 < x < 24, -6 < z < 10) left open
            ca = math.cos(ang)
            cut = (C(10, v) - (Z1 + v) / 2) / ca if sz > 0 else (-C(6, v) + (Z1 + v) / 2) / ca
            K.corrugated(s, roof_m, X0 - v, H(10, v), -ln / 2, ln / 2, 0.025, 0.2, 0.006, mt, samples_per_period=4)
            K.corrugated(s, roof_m, L(24, v), X1 + v, -ln / 2, ln / 2, 0.025, 0.2, 0.006, mt, samples_per_period=4)
            if sz > 0:
                K.corrugated(s, roof_m, H(10, v), L(24, v), cut, ln / 2, 0.025, 0.2, 0.006, mt, samples_per_period=4)
            else:
                K.corrugated(s, roof_m, H(10, v), L(24, v), -ln / 2, cut, 0.025, 0.2, 0.006, mt, samples_per_period=4)
            for zr in (-0.9, 0.0, 0.9):                                   # bare purlins across the hole
                yr = ridge - abs(zr) / (Z1 + v) * (ridge - eave) - 0.08
                s.box2("rust", (H(10, v), yr - 0.05, zr - 0.04), (L(24, v), yr + 0.03, zr + 0.04), bevel=0.008)
        for xk in range(-24, 27, 12):                                     # skylights (glass strips down the slope)
            g.box("glass_clear", (3 * v, 0.012, ln * 0.7), (C(xk + 1, v), (ridge + eave) / 2 + 0.035, sz * (Z1 + v) / 2), r=(sz * math.degrees(ang), 0, 0), bevel=0.0)
    if seed == 1:                                                         # a fallen-in roof bay: dark hole + hanging sheets
        s.box("rust", (2.6, 0.03, 1.8), (C(17, v), eave - 0.6, C(2, v)), r=(25, 10, 8), bevel=0.0)
        s.box("rust", (1.4, 0.03, 1.2), (C(13, v), 0.35, C(4, v)), r=(-6, 30, 4), bevel=0.0)
    for sx in (-1, 1):                                                    # gable end triangles
        xs = X1 - 0.02 if sx > 0 else X0 + 0.02
        s.tri_plate("tin_v", (xs, eave, Z0 - v), (xs, eave, Z1 + v), (xs, ridge, 0.0), 0.03)
    s.box2("steel_dark", (X0 - v, ridge - 0.05, -0.12), (X1 + v, ridge + 0.08, 0.12), bevel=0.01)   # ridge cap
    for zs in (Z0 - v, Z1 + v):                                           # gutters
        s.box2("steel_dark", (X0 - v, eave - 0.12, zs - 0.06 if zs < 0 else zs - 0.06), (X1 + v, eave - 0.04, zs + 0.06), bevel=0.01)
    s.box2("sign_navy", (L(-14, v), L(hv - 4, v), Z0 - v), (H(14, v), H(hv - 1, v), Z0 - 0.02), bevel=0.02)   # faded company sign
    for k in range(9):
        s.box2("cream", (L(-12, v) + k * 0.6, C(hv - 3, v) - 0.08, Z0 - v - 0.006), (L(-12, v) + k * 0.6 + 0.34, C(hv - 3, v) + 0.22, Z0 - v), bevel=0.0)
    for i in range(3):                                                    # pallets inside
        x0 = -24 + i * 6
        for t in range(3):
            w.box2(K.wood("light", "x"), (L(x0, v), L(1, v) + t * 0.05, L(10, v) + t * 0.33), (H(x0 + 4, v), L(1, v) + t * 0.05 + 0.04, L(10, v) + t * 0.33 + 0.1), bevel=0.006)
        w.box2(K.wood("", "z"), (L(x0, v), L(1, v) + 0.15, L(10, v)), (H(x0 + 4, v), L(1, v) + 0.17, H(14, v)), bevel=0.004)
    return a


def canopy():
    v = 0.16
    a = new("Canopy0", v, "Building", "building")
    s, g = a.p("Scrap"), a.p("Glass")
    for x in (-26, 26):
        for z in (-16, 16):
            s.box2("canopy_cream", (L(x, v), 0, L(z, v)), (H(x + 1, v), H(27, v), H(z + 1, v)), bevel=0.02)
            s.box2("conc", (L(x, v) - 0.1, 0, L(z, v) - 0.1), (H(x + 1, v) + 0.1, 0.3, H(z + 1, v) + 0.1), bevel=0.02)   # plinths
            s.box2("booth_red", (L(x, v) - 0.004, 1.0, L(z, v) - 0.004), (H(x + 1, v) + 0.004, 1.15, H(z + 1, v) + 0.004), bevel=0.0)
    s.box2("canopy_cream", (L(-30, v), L(28, v), L(-20, v)), (H(30, v), H(28, v), H(20, v)), bevel=0.01)
    r = K.rng(2421)
    for i in range(-6, 6):                                                # blown-out ceiling panels: dark holes
        for j in range(-4, 4):
            if K.pal_hash(i, 0, j, 2421) < 0.15 and abs(i * 5) < 28 and abs(j * 5) < 18:
                s.box2("black", (C(i * 5, v), L(28, v) - 0.004, C(j * 5, v)), (C(i * 5 + 5, v), L(28, v) + 0.01, C(j * 5 + 5, v)), bevel=0.0)
    for (x0, x1, z0, z1) in ((-30, 30, -20, -19), (-30, 30, 19, 20), (-30, -29, -20, 20), (29, 30, -20, 20)):   # fascia with red band
        s.box2("canopy_cream", (L(x0, v), L(29, v), L(z0, v)), (H(x1, v), H(31, v), H(z1, v)), bevel=0.01)
        s.box2("booth_red", (L(x0, v) - 0.004, L(30, v), L(z0, v) - 0.004), (H(x1, v) + 0.004, H(30, v), H(z1, v) + 0.004), bevel=0.0)
    for x in range(-20, 21, 10):                                          # dead strip lights
        g.box2("cream", (C(x - 1, v), L(27, v) + 0.06, C(-2, v)), (C(x + 1, v), H(27, v), C(2, v)), bevel=0.01)
    return a


def picket_fence():
    v = 0.08
    a = new("FencePicket0", v)
    w = a.p("Wood")
    for x in range(-24, 25, 3):
        if K.pal_hash(x, 0, 0, 2431) < 0.1:
            w.box(K.wood("light", "y"), (0.07, 0.86, 0.02), (C(x, v) + 0.2, 0.02, C(-2, v)), r=(90, 20, 0), bevel=0.004)   # kicked out, lying
            continue
        w.prism("white", [(L(x, v) + 0.005, 0.0), (H(x, v) - 0.005, 0.0), (H(x, v) - 0.005, H(10, v)), (C(x, v), H(11, v)), (L(x, v) + 0.005, H(10, v))], L(0, v), H(0, v), plane="xy")
    for y in (3, 8):
        w.box2(K.wood("", "x"), (L(-24, v), L(y, v) + 0.01, L(1, v)), (H(24, v), H(y, v) - 0.01, H(1, v)), bevel=0.008)
    for x in (-24, 0, 24):
        w.box2(K.wood("dark", "y"), (L(x, v), 0, L(1, v)), (H(x, v), H(9, v), H(1, v) + 0.02), bevel=0.01)
    return a


def chain_link():
    v = 0.08
    a = new("ChainLink0", v)
    s = a.p("Scrap")
    for x in (-24, -8, 8, 24):
        s.cyl("steel", 0.03, H(23, v), (C(x, v), H(23, v) / 2, 0), "y", 10, 0.004)
        s.cyl("steel_dark", 0.034, 0.02, (C(x, v), H(23, v), 0), "y", 10, 0.004)
    s.rod("steel", (L(-24, v), C(22, v), 0), (H(24, v), C(22, v), 0), 0.02, 8)
    s.rod("steel", (L(-24, v), C(1, v), 0), (H(24, v), C(1, v), 0), 0.008, 6)   # tension wire
    # diamond mesh: wires along x - y = c and x + y = c (every 3 voxels), cut round a hole near (6, 6)
    hole_c, hole_r = Vector((C(6, v), C(6, v))), 5.2 * v
    for sgn in (1, -1):
        for c0 in range(-24 - 21, 24 + 22, 3):
            pts = []
            for y in range(1, 22):
                x = c0 + sgn * y
                if x < -24 or x > 24:
                    if len(pts) > 1:
                        s.tube("steel", pts, 0.004, 4, caps=False)
                    pts = []
                    continue
                q = (C(x, v), C(y, v), 0.0)
                if (Vector(q[:2]) - hole_c).length < hole_r:
                    if len(pts) > 1:
                        s.tube("steel", pts, 0.004, 4, caps=False)
                    pts = []
                    continue
                pts.append(q)
            if len(pts) > 1:
                s.tube("steel", pts, 0.004, 4, caps=False)
    for x in range(-24, 24, 2):                                         # barbed wire with barbs
        s.rod("steel_dark", (C(x, v), C(24.5, v), 0), (C(x + 2, v), C(24.5, v), 0), 0.005, 4)
        s.rod("steel_dark", (C(x + 1, v) - 0.02, C(24.5, v) - 0.02, -0.02), (C(x + 1, v) + 0.02, C(24.5, v) + 0.02, 0.02), 0.003, 3)
    for x in (-24, -8, 8, 24):
        s.rod("steel_dark", (C(x, v), H(23, v), 0), (C(x, v), C(24.8, v), -0.08), 0.012, 6)   # barb arms
    return a


def rubble():
    v = 0.12
    a = new("Rubble0", v)
    st, s = a.p("Stone"), a.p("Scrap")
    rnd = K.NetRandom(2450)
    for i in range(9):
        cx, cz, r = rnd.next(-8, 9), rnd.next(-6, 7), 2 + rnd.next(3)
        m = "brick" if i % 3 == 0 else ("conc" if i % 2 else "conc_dark")
        rr = K.rng(i)
        for k in range(3):                                                # broken slabs and lumps
            off = Vector((rr.uniform(-0.5, 0.5), 0, rr.uniform(-0.5, 0.5))) * r * v
            st.rock(m, (r + 0.5) * v * rr.uniform(0.5, 0.8), (C(cx, v) + off.x, r * v * 0.25, C(cz, v) + off.z), seed=i * 5 + k, squash=0.55, detail=2, floor=L(0, v), yaw=rr.uniform(0, 360))
        if i % 3 == 0:
            st.box(m, (0.22, 0.07, 0.11), (C(cx, v) + 0.1, r * v * 0.6, C(cz, v)), r=(rr.uniform(-30, 30), rr.uniform(0, 90), rr.uniform(-30, 30)), bevel=0.01)
    s.rod("rust", (C(-3, v), C(1, v), C(-2, v)), (C(2, v), C(9, v), C(1, v)), 0.012, 6)   # rebar
    s.rod("rust", (C(4, v), C(1, v), C(2, v)), (C(7, v), C(7, v), C(-1, v)), 0.012, 6)
    s.tube("rust", [(C(7, v), C(7, v), C(-1, v)), (C(7.3, v), C(7.5, v), C(-0.6, v)), (C(7.1, v), C(8.2, v), C(-0.2, v))], 0.012, 6)
    return a


TILES = [("world_town_street", "Bench, bin, trash bags, litter, hydrant, bollard, stop sign, traffic light",
          [lambda: [bench(), bin_(), trash_bags(), litter(0), litter(1), hydrant(), bollard(), stop_sign(), traffic_light()]], 3),
         ("world_town_shelters", "Phone booth, bus stop, planter, burning drum, oil drum, picket fence, chain-link fence, rubble",
          [lambda: [phone_booth(), bus_stop(), planter(), drum(True, 0), drum(False, 1), picket_fence(), chain_link(), rubble()]], 3),
         ("world_town_park", "Hedge, park trees, fountain, statue, pond, village well",
          [lambda: [hedge(), park_tree(0), park_tree(1), fountain(), statue(), pond(), well()]], 3),
         ("world_town_walls", "Graffiti walls (concrete, brick + skull)",
          [lambda: [graffiti(0), graffiti(1)]], 2),
         ("world_town_warehouses", "Warehouses (roller door; the second one jammed and fallen in)",
          [lambda: [warehouse(0), warehouse(1)]], 2),
         ("world_town_canopy", "Fuel-station canopy",
          [lambda: [canopy()]], 1)]

if __name__ == "__main__":
    K.run_tiles(TILES, "world", HERE, "town_")
