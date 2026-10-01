"""HD world buildings (BiomeProps.cs Farmhouse / BrickHouse / Tower / Shop, PropLibrary.cs Shack): walls, slabs, stairs,
openings and ruin cuts follow the voxel builders exactly (vbuild.VB); Shell_* = what the destruction bake voxelises,
Detail = frames, trims, signs, awnings, junk, Glass = panes. Origin = template origin (bottom centre); doors face -Z.

blender -b -P buildings.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hdkit as K  # noqa: E402
from hdkit import Asset, Vector, Matrix  # noqa: E402
from vbuild import VB, rubble  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))


def farmhouse(seed):
    gid = "Farmhouse%d" % seed
    a = Asset(gid, "Building", kind="building", voxel=0.16, yaw=180)
    b = VB(a, 0.16)
    w, l, h = 20 + seed * 4, 16, 17
    b.slab(w, l, 0, "Wood", K.wood("dark", "x"))
    b.walls(w, l, 1, h, "Wood", "siding", seed, True, 7, frame_mat=K.wood("light", "y"), sill=K.wood("light", "x"))
    de, sh_r = b.detail("Wood"), b.shell("Scrap")
    for y in range(4, h + 1, 3):                          # clapboard shadow lines on the long faces
        for (x0, x1, z0, z1) in ((b.lo(-w), b.hi(w), b.lo(-l) - 0.012, b.lo(-l)), (b.lo(-w), b.hi(w), b.hi(l), b.hi(l) + 0.012)):
            de.box2(K.wood("dark", "x"), (x0, b.lo(y), z0), (x1, b.lo(y) + 0.02, z1), bevel=0)
    for x in (-w, w):                                     # corner boards
        for z in (-l, l):
            de.box2(K.wood("light", "y"), (b.lo(x) - 0.015, b.lo(1), b.lo(z) - 0.015), (b.hi(x) + 0.015, b.hi(h), b.hi(z) + 0.015), bevel=0.006)
    rise = lambda z: (l + 2 - abs(z)) * 2 // 3            # gable roof along x (red corrugated), ridge cap
    for sz in (-1, 1):
        za, zb = sz * b.hi(l + 2), 0.0
        ya, yb = b.lo(h + 1 + rise(l + 2)) + 0.02, b.hi(h + 1 + rise(0))
        ang = math.atan2(yb - ya, zb - za)
        Lr = math.hypot(zb - za, yb - ya)
        mt = Matrix.Translation((0, (ya + yb) / 2, (za + zb) / 2)) @ Matrix.Rotation(-ang, 4, "X")
        for i in range(8):
            x0 = b.lo(-w - 2) + i * (b.hi(w + 2) - b.lo(-w - 2)) / 8 - 0.01
            K.corrugated(sh_r, "roof_red" if i % 3 else "tin_rust", x0, x0 + (b.hi(w + 2) - b.lo(-w - 2)) / 8 + 0.02, -Lr / 2, Lr / 2, 0.02, 0.12, 0.006, mt, samples_per_period=4)
    sh_r.box2("tin_rust", (b.lo(-w - 2), b.hi(h + 1 + rise(0)) - 0.04, -0.12), (b.hi(w + 2), b.hi(h + 1 + rise(0)) + 0.03, 0.12), bevel=0.02)
    shw = b.shell("Wood")                                 # gable ends
    for x in (-w, w):
        pts = [(b.lo(-l), b.lo(h + 1)), (b.hi(l), b.lo(h + 1)), (0.0, b.hi(h + rise(0)) - 0.02)]
        shw.prism("siding", pts, b.lo(x), b.hi(x), plane="zy")
    for x in (-w, w):                                     # barge boards
        for sz in (-1, 1):
            de.beam(K.wood("light", "z"), (b.c(x) + (0.1 if x > 0 else -0.1), b.lo(h + 1), sz * b.hi(l + 2)), (b.c(x) + (0.1 if x > 0 else -0.1), b.hi(h + rise(0)), 0.0), 0.06, 0.05)
    sh_s = b.shell("Stone")                               # brick chimney through the roof
    sh_s.box2("brick", (b.lo(w - 6), b.lo(h - 2), b.lo(4)), (b.hi(w - 3), b.hi(h + 16), b.hi(7)), bevel=0.02)
    sh_s.box2("conc", (b.lo(w - 6) - 0.04, b.hi(h + 16) - 0.08, b.lo(4) - 0.04), (b.hi(w - 3) + 0.04, b.hi(h + 16), b.hi(7) + 0.04), bevel=0.01)
    de.cyl("black", 0.1, 0.08, (b.c(w - 4.5), b.hi(h + 16) + 0.04, b.c(5.5)), "y", 12, 0.006)
    # porch step, door swung in, lamp, rain barrel, firewood stack
    de.box2(K.wood("", "x"), (b.lo(-4), b.lo(0), b.lo(-l) - 0.3), (b.hi(4), b.lo(1) - 0.02, b.lo(-l)), bevel=0.01)
    de.box(K.wood("", "y"), (1.0, b.hi(14) - b.lo(1), 0.05), (b.lo(-3) + 0.4, (b.lo(1) + b.hi(14)) / 2, b.lo(-l) + 0.5), r=(0, 60, 0), bevel=0.01)
    de.cyl("lamp_amber", 0.05, 0.12, (b.c(5), b.c(15), b.lo(-l) - 0.08), "y", 10, 0.01)
    de.lathe(K.wood("", "y"), [(0.0, 0.0), (0.24, 0.0), (0.27, 0.42), (0.25, 0.84), (0.0, 0.84)], (b.c(-w + 3), b.lo(1), b.lo(-l) - 0.29), "y", 16)
    for k in range(3):
        de.torus("steel_dark", 0.265, 0.012, (b.c(-w + 3), b.lo(1) + 0.12 + k * 0.3, b.lo(-l) - 0.29), "y", 16, 4)
    r = K.rng(seed + 3)
    for k in range(14):
        de.cyl(K.wood("", "x"), r.uniform(0.05, 0.07), 0.5, (r.uniform(-1.5, -0.5), b.lo(1) + 0.06 + (k // 3) * 0.12, b.hi(l) + 0.08 + (k % 2) * 0.12), "x", 8, 0.006)
    return a


def brick_house(seed):
    gid = "BrickHouse%d" % seed
    a = Asset(gid, "Building", kind="building", voxel=0.16, yaw=180)
    b = VB(a, 0.16)
    w, l, st = 24 + seed * 3, 20, 18
    cut_box = (w - 10, st + 6, -l, w, st * 2 + 3, -l + 12) if seed == 1 else None   # collapsed corner (x0, y0, z0, x1, y1, z1)

    def ruin(y0, y1):
        def f(side):
            if not cut_box:
                return []
            x0, ya, z0, x1, yb, z1 = cut_box
            if side == 0:
                return [(x0, x1, max(ya, y0), min(yb, y1))]
            if side == 1:
                return [(z0, z1, max(ya, y0), min(yb, y1))]
            return []
        return f
    b.slab(w, l, 0, "Stone", "conc_dark")
    b.walls(w, l, 1, st, "Stone", "brick", seed, True, 8, frame_mat=K.wood("", "y"), sill="conc", cut=ruin(1, st))
    hole = b.stair_hole(w // 2, 3, -10, st + 1)
    b.slab(w, l, st + 1, "Stone", "conc_dark", holes=[hole])
    b.walls(w, l, st + 2, st * 2 + 1, "Stone", "brick", seed + 5, False, 8, frame_mat=K.wood("", "y"), sill="conc", cut=ruin(st + 2, st * 2 + 1))
    b.stairs(w // 2, 3, -10, 0, st + 1, "Wood", K.wood("", "x"), tread=K.wood("dark", "x"))
    roof_holes = [(cut_box[0], cut_box[3], cut_box[2], cut_box[5])] if cut_box else []
    b.slab(w + 1, l + 1, st * 2 + 2, "Stone", "conc_dark", holes=roof_holes)
    de = b.detail("Scrap")
    for k in range(5):                                   # striped awning over the door
        x0 = b.lo(-5) + k * (b.hi(5) - b.lo(-5)) / 5
        de.quad("cloth_red" if k % 2 == 0 else "cloth_cream", (x0, b.hi(15), b.lo(-l)), (x0 + (b.hi(5) - b.lo(-5)) / 5, b.hi(15), b.lo(-l)),
                (x0 + (b.hi(5) - b.lo(-5)) / 5, b.lo(15), b.lo(-l - 4)), (x0, b.lo(15), b.lo(-l - 4)), 0.012)
    for sx in (-1, 1):
        de.rod("steel_dark", (sx * b.hi(5), b.lo(15), b.lo(-l - 4)), (sx * b.hi(5), b.c(10), b.lo(-l)), 0.012, 6)
    for f in range(2):                                   # string courses
        y = b.hi(st * (f + 1) + f)
        for (x0, x1, z0, z1) in ((b.lo(-w) - 0.03, b.hi(w) + 0.03, b.lo(-l) - 0.03, b.lo(-l)), (b.lo(-w) - 0.03, b.hi(w) + 0.03, b.hi(l), b.hi(l) + 0.03),
                                 (b.lo(-w) - 0.03, b.lo(-w), b.lo(-l), b.hi(l)), (b.hi(w), b.hi(w) + 0.03, b.lo(-l), b.hi(l))):
            if cut_box and f == 1 and x1 > b.lo(w - 10) and z0 < b.lo(-l + 1):
                x1 = b.lo(w - 10)
            if cut_box and f == 1 and x0 >= b.hi(w) - 0.01:
                continue
            de.box2("conc", (x0, y - 0.1, z0), (x1, y, z1), bevel=0.006)
    de.box2("steel", (b.c(6), b.hi(st * 2 + 2), b.c(8)), (b.c(9), b.hi(st * 2 + 2) + 0.12, b.c(11)), bevel=0.02)      # roof hatch
    de.tube("rust", [(b.hi(w) - 0.04, b.hi(st * 2 + 2), b.hi(l) + 0.05), (b.hi(w) - 0.04, b.lo(1), b.hi(l) + 0.05)], 0.04, 8)
    r = K.rng(seed * 11)
    if cut_box:                                           # rubble below and on the floor of the collapse
        rubble(de, ["brick", "brick_dark", "conc_dark"], r, (b.c(w - 6), 0, b.c(-l - 1.5)), 0.7, 24, 0.14, floor=b.lo(0))
        rubble(de, ["brick", "brick_dark"], r, (b.c(w - 5), b.hi(st + 1), b.c(-l + 5)), 0.6, 14, 0.12, floor=b.hi(st + 1))
    rubble(de, ["brick", "conc_dark"], r, (b.c(-w) + 0.6, b.hi(0), b.c(l) - 1.0), 0.4, 8, 0.1, floor=b.hi(0))
    return a


def tower(seed):
    gid = "Tower%d" % seed
    floors = 3 + seed
    a = Asset(gid, "Building", kind="building", voxel=0.2, yaw=180)
    b = VB(a, 0.2)
    w, l, st = 26 + (seed % 2) * 6, 22, 15
    rnd = K.NetRandom(seed * 13 + 1)                      # the same bite as BiomeProps.Tower
    top = floors * (st + 1)
    bx, bz = rnd.next(-w, 0), rnd.next(-l, 0)
    by = top - st * (1 + rnd.next(2))
    bite = (bx, by, bz, bx + w, top + 2, bz + l)
    jag = K.rng(seed * 5)

    def cut_for(y0, y1):
        def f(side):
            x0, ya, z0, x1, yb, z1 = bite
            if yb < y0 or ya > y1:
                return []
            ya, yb = max(ya, y0), min(yb, y1)
            if side == 0 and z0 <= -l <= z1:
                return [(x0, x1, ya, yb)]
            if side == 2 and z0 <= l <= z1:
                return [(x0, x1, ya, yb)]
            if side == 1 and x0 <= w <= x1:
                return [(z0, z1, ya, yb)]
            if side == 3 and x0 <= -w <= x1:
                return [(z0, z1, ya, yb)]
            return []
        return f
    b.slab(w, l, 0, "Stone", "conc_dark")
    for f in range(floors):
        y0 = 1 + f * (st + 1)
        b.walls(w, l, y0, y0 + st - 1, "Stone", "conc", seed + f * 7, f == 0, 5, frame_mat="steel_dark", sill=None, cut=cut_for(y0, y0 + st - 1))
        holes = []
        if f < floors - 1:
            holes.append(b.stair_hole(w // 2 if f % 2 == 0 else 0, 3, -8, st + 1))
        ys = y0 + st
        if bite[1] <= ys <= bite[4]:
            holes.append((bite[0], bite[3], bite[2], bite[5]))
        b.slab(w, l, ys, "Stone", "conc_dark", holes=holes)
    for f in range(floors - 1):
        fy = f * (st + 1)
        cx = w // 2 if f % 2 == 0 else 0
        b.stairs(cx, 3, -8, fy, st + 1, "Stone", "conc")
    de = b.detail("Stone")
    for f in range(1, floors + 1):                         # floor bands, soot over some windows, hairline cracks
        y = b.hi(f * (st + 1))
        for (x0, x1, z0, z1) in ((b.lo(-w) - 0.02, b.hi(w) + 0.02, b.lo(-l) - 0.02, b.lo(-l)), (b.lo(-w) - 0.02, b.hi(w) + 0.02, b.hi(l), b.hi(l) + 0.02),
                                 (b.lo(-w) - 0.02, b.lo(-w), b.lo(-l), b.hi(l)), (b.hi(w), b.hi(w) + 0.02, b.lo(-l), b.hi(l))):
            de.box2("conc_dark", (x0, y - 0.14, z0), (x1, y, z1), bevel=0.0)
    cols = [a_ for a_ in range(-w + 3, w - 2) if abs(a_) % 5 == 0 and abs(a_) < w - 2]
    for f in range(floors):
        y0 = 1 + f * (st + 1)
        for k in range(4):
            x = jag.choice(cols)
            de.box2("ash", (b.lo(x) - 0.06, b.hi(y0 + 11), b.lo(-l) - 0.006), (b.hi(x + 1) + 0.06, b.hi(y0 + 11) + jag.uniform(0.3, 0.7), b.lo(-l) - 0.002), bevel=0)
        for k in range(2):
            p0 = Vector((jag.uniform(b.lo(-w), b.hi(w)), b.c(y0 + jag.randint(1, st - 1)), b.lo(-l) - 0.004))
            pts = [p0]
            for j in range(4):
                pts.append(pts[-1] + Vector((jag.uniform(-0.25, 0.25), jag.uniform(-0.3, 0.1), 0)))
            de.tube("black", pts, 0.008, 3, caps=False)
    r2 = K.NetRandom(seed * 13 + 1)                       # replay the rubble positions after the bite rolls
    r2.next(-w, 0); r2.next(-l, 0); r2.next(2)
    for i in range(60):
        x, z = r2.next(-w - 8, w + 8), r2.next(-l - 8, l + 8)
        if abs(x) < w and abs(z) < l:
            continue
        de.box(["conc_dark", "conc", "rock_grey"][i % 3], (0.18 + (i % 5) * 0.04, 0.12, 0.16 + (i % 3) * 0.05), (b.c(x), b.lo(0) + 0.06, b.c(z)), r=(i * 7 % 20 - 10, i * 37, i * 11 % 16 - 8), bevel=0.0)
        if r2.next_double() < 0.3:
            de.box("conc", (0.14, 0.1, 0.12), (b.c(x), b.c(1), b.c(z)), r=(5, i * 23, 0), bevel=0.0)
    for k in range(10):                                   # rebar standing out of the broken floors
        xx = b.c(bite[0]) + (k / 9) * (b.c(bite[3]) - b.c(bite[0]))
        zz = b.c(bite[2]) + jag.uniform(0, b.c(bite[5]) - b.c(bite[2]))
        de.rod("rust", (xx, b.hi(bite[1] - 1 if bite[1] > 1 else 1), zz), (xx + jag.uniform(-0.1, 0.1), b.hi(bite[1]) + jag.uniform(0.2, 0.6), zz + jag.uniform(-0.1, 0.1)), 0.01, 4)
    side_x = -w if bite[0] > -w + 4 else w                # rusted fire escape on the side the bite missed
    for f in range(1, floors - 1):
        y = b.hi(f * (st + 1))
        xo = b.c(side_x) + (0.6 if side_x > 0 else -0.6)
        de.box2("steel_dark", (min(xo, b.c(side_x)), y - 0.04, b.c(-6)), (max(xo, b.c(side_x)), y, b.c(6)), bevel=0.004)
        de.rod("rust", (xo, y, b.c(-6)), (xo, y + 0.9, b.c(-6)), 0.012, 5)
        de.rod("rust", (xo, y + 0.9, b.c(-6)), (xo, y + 0.9, b.c(6)), 0.012, 5)
        de.beam("rust", (xo - (0.2 if side_x > 0 else -0.2), y, b.c(-5)), (xo - (0.2 if side_x > 0 else -0.2), y + st * 0.2 + 0.2, b.c(5)), 0.3, 0.03)
    return a


def shop(seed):
    gid = "Shop%d" % seed
    a = Asset(gid, "Building", kind="building", voxel=0.2, yaw=180)
    b = VB(a, 0.2)
    w, l, h = 30, 22, 16
    b.slab(w, l, 0, "Stone", "conc_dark")
    sh, de, gl = b.shell("Stone"), b.detail("Scrap"), b.glass()
    for (x0, x1, z0, z1) in ((b.lo(w), b.hi(w), b.lo(-l), b.hi(l)), (b.lo(-w), b.hi(-w), b.lo(-l), b.hi(l)), (b.lo(-w + 1), b.hi(w - 1), b.lo(l), b.hi(l))):
        sh.box2("conc", (x0, b.lo(1), z0), (x1, b.hi(h), z1), bevel=0.0)
    store = [(b.lo(-w + 2), b.hi(w - 2), b.lo(3), b.hi(12))]
    for (a0, a1, c0, c1) in K.rect_cells(b.lo(-w + 1), b.hi(w - 1), b.lo(1), b.hi(h), store + [(b.lo(-3), b.hi(3), b.lo(1), b.hi(11))]):
        sh.box2("conc", (a0, c0, b.lo(-l)), (a1, c1, b.hi(-l)), bevel=0.0)
    for x in range(-w + 2, w - 1):                         # storefront mullions (x % 8 == 0), header (y 12), door posts
        if x % 8 == 0 and abs(x) > 3:
            de.box2("steel_dark", (b.lo(x), b.lo(1), b.lo(-l)), (b.hi(x), b.hi(12), b.hi(-l)), bevel=0.01)
    de.box2("steel_dark", (b.lo(-w + 2), b.lo(12), b.lo(-l)), (b.hi(w - 2), b.hi(12), b.hi(-l)), bevel=0.01)
    for x in (-4, 4):
        de.box2("steel_dark", (b.lo(x), b.lo(1), b.lo(-l)), (b.hi(x), b.hi(12), b.hi(-l)), bevel=0.01)
    xs = [x for x in range(-w + 2, w - 1) if x % 8 == 0]
    bounds = sorted(set([-w + 1] + xs + [-4, 4, w - 1]))
    for u0, u1 in zip(bounds, bounds[1:]):                  # panes between mullions (from the voxel glass share)
        if u0 >= -4 and u1 <= 4:
            continue
        n = tot = 0
        for x in range(u0 + 1, u1):
            for y in range(3, 12):
                tot += 1
                n += K.pal_hash(x, y, seed, 3) > 0.4
        if n / max(1, tot) > 0.5:
            gl.box2("glass", (b.hi(u0), b.lo(3), b.c(-l) - 0.01), (b.lo(u1), b.lo(12), b.c(-l) + 0.01), bevel=0)
        else:
            r = K.rng(u0 + seed)
            gl.tri_plate("glass", (b.hi(u0), b.lo(3), b.c(-l)), (b.hi(u0) + 0.4 * r.random() + 0.2, b.lo(3), b.c(-l)), (b.hi(u0), b.lo(3) + 0.6 + 0.6 * r.random(), b.c(-l)), 0.008)
    b.slab(w + 1, l + 1, h + 1, "Stone", "conc_dark")
    sign = "sign_red" if seed % 2 == 0 else "navy"
    de.box2(sign, (b.lo(-20), b.lo(h + 2), b.lo(-l - 1)), (b.hi(20), b.hi(h + 7), b.hi(-l - 1)), bevel=0.02)
    for x in range(-18, 19, 4):                            # sign letters
        de.box2("cloth_cream", (b.lo(x) + 0.02, b.lo(h + 3), b.lo(-l - 1) - 0.02), (b.hi(x) - 0.02, b.hi(h + 6), b.lo(-l - 1)), bevel=0.006)
    for x in (-18, 0, 18):
        de.rod("steel_dark", (b.c(x), b.lo(h + 2), b.c(-l - 1)), (b.c(x), b.hi(h + 1), b.c(-l + 1)), 0.015, 5)
    for k in range(10):                                    # tin awning over the storefront
        x0 = b.lo(-w + 2) + k * (b.hi(w - 2) - b.lo(-w + 2)) / 10
        de.quad("tin_rust" if k % 3 == 0 else "tin", (x0, b.hi(13), b.lo(-l)), (x0 + (b.hi(w - 2) - b.lo(-w + 2)) / 10, b.hi(13), b.lo(-l)),
                (x0 + (b.hi(w - 2) - b.lo(-w + 2)) / 10, b.lo(13) - 0.03, b.lo(-l - 1)), (x0, b.lo(13) - 0.03, b.lo(-l - 1)), 0.012)
    w_ = b.shell("Wood")                                   # shelves + counter (voxel Wood)
    for z in (-8, 0, 8):
        for y in range(1, 9, 3):
            w_.box2(K.wood("", "x"), (b.lo(-18), b.lo(y), b.lo(z)), (b.hi(18), b.hi(y) - 0.08, b.hi(z + 1)), bevel=0.01)
        for x in (-18, 0, 18):
            w_.box2(K.wood("dark", "y"), (b.lo(x), b.lo(1), b.lo(z)), (b.lo(x) + 0.06, b.hi(8), b.hi(z + 1)), bevel=0.006)
    w_.box2(K.wood("", "x"), (b.lo(18), b.lo(1), b.lo(14)), (b.hi(28), b.hi(5), b.hi(18)), bevel=0.02)
    r = K.rng(seed + 50)
    for k in range(40):                                    # tins and boxes left on the shelves
        z = r.choice((-8, 0, 8))
        y = r.choice((1, 4, 7))
        x = r.uniform(-3.4, 3.4)
        if r.random() < 0.5:
            de.cyl(r.choice(["crimson", "ochre", "steel", "navy"]), 0.05, 0.12, (x, b.hi(y) - 0.02, b.c(z) + 0.1), "y", 10, 0.004)
        else:
            de.box2(r.choice([K.wood("light", "x"), "cloth_cream", "burlap"]), (x - 0.08, b.hi(y) - 0.08, b.c(z)), (x + 0.08, b.hi(y) + 0.06, b.c(z) + 0.2), bevel=0.01)
    de.box2("white", (b.c(10), b.hi(h + 1), b.c(6)), (b.c(14), b.hi(h + 1) + 0.6, b.c(10)), bevel=0.02)       # rooftop AC unit
    de.tube("rust", [(b.hi(w) - 0.05, b.hi(h + 1), b.hi(l) + 0.05), (b.hi(w) - 0.05, b.lo(1), b.hi(l) + 0.05)], 0.04, 8)
    return a


def shack(seed):
    gid = "Shack%d" % seed
    a = Asset(gid, "Building", kind="building", voxel=0.08, yaw=180)
    b = VB(a, 0.08)
    w, l, h = 16 + seed * 3, 18 + seed * 2, 34
    sh, shw, de, gl = b.shell("Scrap"), b.shell("Wood"), b.detail("Wood"), b.glass()
    shw.box2(K.wood("dark", "x"), (b.lo(-w), b.lo(0), b.lo(-l)), (b.hi(w), b.hi(0), b.hi(l)), bevel=0.006)
    door = (b.lo(-5), b.hi(5), b.lo(0), b.hi(26))
    win = (b.lo(-4), b.hi(4), b.lo(14), b.hi(22))
    M = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
    for side in range(4):                                  # corrugated sheets on the frame, cut round the openings
        if side in (0, 2):
            zf = -l if side == 0 else l
            for (u0, u1, v0, v1) in K.rect_cells(b.lo(-w + 1), b.hi(w - 1), b.hi(0), b.hi(h), [door] if side == 0 else []):
                K.corrugated(sh, "tin_rust_v" if (int(u0 * 7) + side) % 3 == 0 else "tin_v", u0, u1, v0, v1, 0.014, 0.076, 0.004,
                             Matrix.Translation((0, 0, b.c(zf))) @ M, samples_per_period=4)
        else:
            xf = w if side == 1 else -w
            for (u0, u1, v0, v1) in K.rect_cells(b.lo(-l), b.hi(l), b.hi(0), b.hi(h), [win]):
                K.corrugated(sh, "tin_rust_v" if (int(u0 * 7) + side) % 3 == 0 else "tin_v", u0, u1, v0, v1, 0.014, 0.076, 0.004,
                             Matrix.Translation((b.c(xf), 0, 0)) @ Matrix(((0, 1, 0, 0), (0, 0, 1, 0), (1, 0, 0, 0), (0, 0, 0, 1))), samples_per_period=4)
            for (z0, z1, y0, y1) in ((b.lo(-5), b.hi(-5), b.lo(13), b.hi(23)), (b.lo(5), b.hi(5), b.lo(13), b.hi(23)), (b.lo(-5), b.hi(5), b.lo(13), b.hi(13)), (b.lo(-5), b.hi(5), b.lo(23), b.hi(23))):
                shw.box2(K.wood("", "y"), (b.lo(xf) - 0.02, y0, z0), (b.hi(xf) + 0.02, y1, z1), bevel=0.006)
            gl.box2("glass", (b.c(xf) - 0.005, b.lo(14), b.lo(-4)), (b.c(xf) + 0.005, b.hi(22), b.hi(4)), bevel=0)
            de.box2(K.wood("", "y"), (b.c(xf) - 0.02, b.lo(14), -0.02), (b.c(xf) + 0.02, b.hi(22), 0.02), bevel=0.004)
    for x in (-w, w):                                      # corner posts + door frame
        for z in (-l, l):
            shw.box2(K.wood("dark", "y"), (b.lo(x) - 0.01, b.lo(0), b.lo(z) - 0.01), (b.hi(x) + 0.01, b.hi(h), b.hi(z) + 0.01), bevel=0.008)
    for x in (-6, 6):
        shw.box2(K.wood("", "y"), (b.lo(x), b.lo(0), b.lo(-l) - 0.01), (b.hi(x), b.hi(27), b.hi(-l) + 0.01), bevel=0.008)
    shw.box2(K.wood("", "x"), (b.lo(-6), b.lo(27), b.lo(-l) - 0.01), (b.hi(6), b.hi(27), b.hi(-l) + 0.01), bevel=0.008)
    for y in (2, 18, 33):                                  # girts
        for (x0, x1, z0, z1) in ((b.lo(-w), b.hi(w), b.hi(l) + 0.01, b.hi(l) + 0.05), (b.lo(-w), b.lo(-6), b.lo(-l) - 0.05, b.lo(-l) - 0.01), (b.hi(6), b.hi(w), b.lo(-l) - 0.05, b.lo(-l) - 0.01)):
            de.box2(K.wood("dark", "x"), (x0, b.lo(y), z0), (x1, b.hi(y), z1), bevel=0.004)
    ya, yb = b.lo(h + 1), b.hi(h + 1 + (2 * w + 4) // 6)   # slanted tin roof rising toward +x, on beams
    xa, xb = b.lo(-w - 2), b.hi(w + 2)
    ang = math.atan2(yb - ya, xb - xa)
    Lr = math.hypot(xb - xa, yb - ya)
    mt = Matrix.Translation(((xa + xb) / 2, (ya + yb) / 2, 0)) @ Matrix.Rotation(ang, 4, "Z") @ Matrix(((0, 0, 1, 0), (0, 1, 0, 0), (-1, 0, 0, 0), (0, 0, 0, 1)))
    n = 6
    for i in range(n):
        z0 = b.lo(-l - 2) + i * (b.hi(l + 2) - b.lo(-l - 2)) / n - 0.01
        K.corrugated(sh, "tin_rust" if i % 2 else "tin", z0, z0 + (b.hi(l + 2) - b.lo(-l - 2)) / n + 0.02, -Lr / 2, Lr / 2, 0.016, 0.1, 0.004, mt, samples_per_period=4)
    for z in (-l, 0, l):
        shw.beam(K.wood("dark", "x"), (xa, ya - 0.06, b.c(z)), (xb, yb - 0.06, b.c(z)), 0.08, 0.06)
    for x in (-w, w):
        for z in (-l, l):
            yt = b.hi(h + 1 + (x + w + 2) // 6)
            shw.box2(K.wood("dark", "y"), (b.lo(x), b.hi(h), b.lo(z)), (b.hi(x), yt - 0.04, b.hi(z)), bevel=0.006)
    de.cyl("rubber", 0.3, 0.2, (b.c(w) - 0.45, b.hi(0) + 0.1, b.c(-l) + 0.5), "y", 18, 0.04)    # junk inside
    de.box2(K.wood("", "x"), (b.c(-w) + 0.15, b.hi(0), b.c(l) - 0.6), (b.c(-w) + 0.65, b.hi(0) + 0.45, b.c(l) - 0.1), bevel=0.02)
    de.lathe("rust", [(0.0, 0.0), (0.26, 0.0), (0.27, 0.86), (0.0, 0.86)], (b.c(w) - 0.4, b.hi(0), b.c(l) - 0.4), "y", 18)
    return a


TILES = [("world_farmhouses", "Farmhouses (plank walls, red tin gable, brick chimney)", [lambda: [farmhouse(0), farmhouse(1)]], 2),
         ("world_brickhouses", "Brick houses (2 storeys; #1 with the collapsed corner)", [lambda: [brick_house(0), brick_house(1)]], 2),
         ("world_towers_a", "Ruined concrete towers (3, 4 floors)", [lambda: [tower(0), tower(1)]], 2),
         ("world_towers_b", "Ruined concrete towers (5, 6 floors)", [lambda: [tower(2), tower(3)]], 2),
         ("world_shops", "Shops (storefront, sign, shelves, counter)", [lambda: [shop(0), shop(1)]], 2),
         ("world_shacks", "Scrap shacks (tin sheets on posts, slanted roof)", [lambda: [shack(0), shack(1), shack(2)]], 3)]

if __name__ == "__main__":
    K.run_tiles(TILES, "world", HERE, "buildings_")
