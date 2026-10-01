"""HD structure pieces (FurnitureLibrary.cs, BuildPieces.cs, FurnitureLibrary.Base.cs / .Defence.cs; 0.08 m voxels).
Origin = the voxel grid origin (mounting point); every piece fills exactly the voxel bounds of the piece it replaces.

blender -b -P structure.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "world"))
import hdkit as K  # noqa: E402
from hdkit import Asset, Vector, Matrix  # noqa: E402

VS = 0.08


def lo(i):
    return (i - 0.5) * VS


def hi(i):
    return (i + 0.5) * VS


WIN = (lo(-5), hi(5), lo(12), hi(22))      # window opening |x| <= 5, y 12..22
DOOR = (lo(-6), hi(6), lo(-1), hi(27))     # doorway |x| <= 6, y <= 27


def _holes(hole):
    return [WIN] if hole == 1 else [DOOR] if hole == 2 else []


def _window(a, frame_m, z0=lo(1), z1=hi(1)):
    """Frame, cross mullions (x = 0, y = 17) and panes in the front voxel layer."""
    K.window_frame(a.p(_frame_part(a)), a.p("Glass"), WIN[0], WIN[1], WIN[2], WIN[3], (z0 + z1) / 2, frame_m,
                   mull_x=0.0, mull_y=17 * VS, depth=z1 - z0, bar=0.05)


def _frame_part(a):
    return "Wood"


def _door_trim(a, m=None, z=2):
    w = a.p("Wood")
    for x in (-7, 7):
        w.boxv(m or K.wood("dark", "y"), x, 0, z, x, 27, z, bevel=0.008)
    w.boxv(m or K.wood("dark", "x"), -7, 28, z, 7, 28, z, bevel=0.008)


# ------------------------------------------------------------------------------------------ wall styles
def wall_wood(gid, hole):
    a = Asset(gid, "Structure")
    w = a.p("Wood")
    holes = _holes(hole)
    K.boards_xy(w, K.WOODS["y"], lo(-12), hi(12), lo(0), hi(29), lo(0) + 0.02, hi(1), 0.32, holes, seed=hash(gid) % 999)
    for y in (2, 27):                                   # back battens
        K.slab_xy(w, K.wood("dark", "x"), lo(-12) + 0.04, hi(12) - 0.04, lo(y) - 0.05, hi(y) + 0.05, lo(0), lo(0) + 0.02, holes)
    for x in range(-12, 13, 4):
        for y in (2, 27):
            if not any(h[0] < x * VS < h[1] and h[2] < y * VS < h[3] for h in holes):
                K.nails(w, "steel_dark", [(x * VS + (0.16 if x < 12 else -0.16), y * VS, hi(1) + 0.002)], 0.007, "z", 0.006)
    if hole == 1:
        _window(a, K.wood("", "x"))
    if hole == 2:
        _door_trim(a)
    return a


def wall_brick(gid, hole, fired=False):
    a = Asset(gid, "Structure")
    s = a.p("Brick" if fired else "Stone")
    m = "brick_fired" if fired else "brick"
    holes = _holes(hole)
    K.slab_xy(s, m, lo(-12), hi(12), lo(0), hi(29), lo(0), hi(1), holes, bevel=0.006)
    if fired:   # soldier course on top
        s.boxv(m, -12, 27, 0, 12, 29, 1, bevel=0.006)
    if hole == 1:
        _window(a, K.wood("", "x"))
        c = a.p("Concrete")
        c.box2("conc", (lo(-6), lo(11), lo(0)), (hi(6), hi(11), hi(2) if fired else hi(1)), bevel=0.01)
    if hole == 2:
        if fired:
            a.p("Iron").box2("steel", (lo(-7), lo(28), lo(0)), (hi(7), hi(28), hi(1) + 0.004), bevel=0.006)
        else:
            _door_trim(a)
    _brick_relief(s, m, holes, hash(gid) % 1000, fired)
    return a


def _brick_relief(s, m, holes, seed, fired):
    """Proud / chipped bricks on both faces so the masonry reads in relief (bricks 0.24 x 0.075 like the texture)."""
    r = K.rng(seed)
    for k in range(70):
        x = r.uniform(lo(-12) + 0.15, hi(12) - 0.15)
        y = r.uniform(lo(0) + 0.1, hi(29) - 0.12)
        if any(h[0] - 0.14 < x < h[1] + 0.14 and h[2] - 0.06 < y < h[3] + 0.06 for h in holes):
            continue
        row = round(y / 0.0775)
        y = row * 0.0775 + 0.03
        x = round((x - (row % 2) * 0.125) / 0.25) * 0.25 + (row % 2) * 0.125
        if abs(x) > 0.86:
            continue
        front = r.random() < 0.6
        z0, z1 = (hi(1) - 0.004, hi(1) + 0.006 * r.random() + 0.002) if front else (lo(0) - 0.006 * r.random() - 0.002, lo(0) + 0.004)
        s.box2(m if r.random() < 0.85 else "brick_dark", (x - 0.115, y - 0.032, z0), (x + 0.115, y + 0.032, z1), bevel=0.003)
    for k in range(6):                                 # chipped corners at the ends
        x = r.choice([lo(-12), hi(12)])
        y = r.uniform(0.2, 2.1)
        s.box2("brick_dark", (x - 0.02 * (1 if x > 0 else -1) - 0.01, y, lo(0) + 0.02), (x + 0.01 * (-1 if x > 0 else 1), y + 0.06, hi(1) - 0.02), bevel=0.004)


def wall_concrete(gid, hole):
    a = Asset(gid, "Structure")
    c = a.p("Concrete")
    holes = _holes(hole)
    # form panels with V joints every 12 voxels (x = 0, y = 12, 24)
    c.box2("conc_dark", (lo(-12) + 0.01, lo(0), lo(0)), (hi(12) - 0.01, hi(29), lo(0) + 0.03), bevel=0)
    for (x0, x1) in ((lo(-12), 0.0), (0.0, hi(12))):
        for (y0, y1) in ((lo(0), 12 * VS), (12 * VS, 24 * VS), (24 * VS, hi(29))):
            K.slab_xy(c, "conc", x0 + 0.006, x1 - 0.006, y0 + 0.006, y1 - 0.006, lo(0) + 0.02, hi(1), holes, bevel=0.01)
    for x in (-8, 8):
        for y in (6, 18):
            if not holes or not (holes[0][0] < x * VS < holes[0][1] and holes[0][2] < y * VS < holes[0][3]):
                c.cyl("steel_dark", 0.016, 0.01, (x * VS, y * VS, hi(1)), "z", 8, 0.002)
    if hole == 2:
        _door_trim(a, "steel_dark")
        a.parts["Wood"].byte = "Iron"
    return a


def wall_plank(gid, hole):
    a = Asset(gid, "Structure")
    w = a.p("Plank")
    holes = _holes(hole)
    # horizontal sawn boards (3 voxels a row, shadow gap at y % 3 == 2, butt joints staggered) on studs
    for row in range(10):
        y0, y1 = lo(row * 3), hi(row * 3 + 1) + 0.02
        stagger = (row & 1) * 6
        joints = [lo(-12)] + [lo(x) for x in range(-12 + 1, 13) if (x + 12 + stagger) % 12 == 0] + [hi(12)]
        joints = sorted(set(joints))
        for xa, xb in zip(joints, joints[1:]):
            m = K.WOODS["x"][(row * 7 + int(xa * 10)) % 3]
            for (ca, cb, cc, cd) in K.rect_cells(xa + 0.004, xb - 0.004, y0, min(y1, hi(29)), holes):
                w.box2(m, (ca, cc, lo(1) + 0.012), (cb, cd, hi(1)), bevel=0.006)
    for x in (-12, -9, 9, 12):                         # studs + plates behind
        K.slab_xy(w, K.wood("dark", "y"), lo(x), hi(x), lo(0), hi(29), lo(0), lo(1) + 0.012, holes)
    K.slab_xy(w, K.wood("dark", "x"), lo(-12), hi(12), lo(0), hi(0), lo(0), lo(1) + 0.012, holes)
    K.slab_xy(w, K.wood("dark", "x"), lo(-12), hi(12), lo(29), hi(29), lo(0), lo(1) + 0.012, holes)
    for x in (-12, -9, 9, 12):
        for y in range(1, 29, 3):
            if not any(h[0] < x * VS < h[1] and h[2] < y * VS < h[3] for h in holes):
                K.nails(w, "steel", [(x * VS, y * VS, hi(1) + 0.002)], 0.008, "z", 0.006)
    if hole == 1:
        _window(a, K.wood("light", "x"))
        w.boxv(K.wood("light", "x"), -6, 11, 2, 6, 11, 2, bevel=0.008)
    if hole == 2:
        _door_trim(a)
    return a


def wall_panel(gid, hole):
    a = Asset(gid, "Structure")
    c, fe = a.p("Concrete"), a.p("Iron")
    holes = _holes(hole)
    inner = (lo(-11), hi(11), lo(0), hi(28))
    for (x0, x1, y0, y1) in K.rect_cells(*inner, holes + ([(lo(-7), hi(7), lo(-1), hi(28))] if hole == 2 else [])):
        c.box2("conc_light", (x0, y0, lo(0) + 0.005), (x1, y1, hi(1) - 0.005), bevel=0.004)
    for x in (-12, 12):
        fe.boxv("steel", x, 0, 0, x, 29, 1, bevel=0.006)
    fe.boxv("steel", -11, 29, 0, 11, 29, 1, bevel=0.006)
    for x in range(-8, 9, 8):
        for y in range(4, 28, 8):
            if not any(h[0] < x * VS < h[1] and h[2] < y * VS < h[3] for h in holes + ([(lo(-7), hi(7), lo(-1), hi(28))] if hole == 2 else [])):
                fe.cyl("steel_dark", 0.018, 0.012, (x * VS + 0.0, y * VS, hi(1) - 0.003), "z", 8, 0.002)
    for x in (-7, 7):
        fe.box2("chrome", (lo(x) + 0.01, lo(29) + 0.02, lo(0) + 0.03), (hi(x) - 0.01, hi(29) + 0.004, hi(1) - 0.03), bevel=0.004)
    if hole == 2:
        for x in (-7, 7):
            fe.boxv("steel", x, 0, 0, x, 28, 1, bevel=0.006)
        fe.boxv("steel", -7, 28, 0, 7, 28, 1, bevel=0.006)
    return a


def wall_scrap():
    a = Asset("wall_scrap", "Structure")
    s, w = a.p("Scrap"), a.p("Wood")
    for i in range(6):                                  # overlapping corrugated sheets, alternating rust
        x0, x1 = lo(-12) + i * 0.334 - (0.02 if i else 0), lo(-12) + (i + 1) * 0.334 + 0.02
        x1 = min(x1, hi(12))
        mm = Matrix.Translation((0, 0, 0.0)) @ K.rot(0, 0, 0)
        # ribs vertical: u = x, v = y; local h -> game z
        M = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
        K.corrugated(s, ["tin_v", "tin_rust_v"][i % 2], x0, x1, lo(0) + 0.01 * (i % 2), hi(24) - 0.02 * (i % 3), 0.012, 0.076, 0.004,
                     Matrix.Translation((0, 0, -0.02 + (i % 2) * 0.008)) @ M)
    for x in (-12, 12):
        w.boxv(K.wood("dark", "y"), x, 0, 1, x, 24, 1, bevel=0.008)
    w.boxv(K.wood("", "x"), -12, 12, 1, 12, 12, 1, bevel=0.008)
    for x in range(-11, 12, 3):
        K.nails(s, "steel", [(x * VS, 12 * VS, hi(1) + 0.002)], 0.007, "z", 0.005)
    return a


# ------------------------------------------------------------------------------------------ doors, floors
def door(gid, metal):
    a = Asset(gid, "Structure")
    if metal:
        d = a.p("Iron")
        d.boxv("steel", -6, 0, 0, 6, 27, 1, bevel=0.01)
        for y in (6, 21):
            d.boxv("rust", -6, y, 2, 6, y, 2, bevel=0.006)
        d.box2("steel_dark", (lo(-5), lo(9), hi(1)), (hi(5), hi(18), hi(1) + 0.012), bevel=0.004)    # kick panel
        for x in (-5, 5):
            for y in (6, 21):
                K.nails(d, "chrome", [(x * VS, y * VS, hi(2) + 0.002)], 0.01, "z", 0.008)
    else:
        d = a.p("Wood")
        K.boards_xy(d, K.WOODS["y"], lo(-6), hi(6), lo(0), hi(27), lo(0), hi(1), 0.17, seed=81)
        for y in (4, 23):
            d.boxv(K.wood("dark", "x"), -6, y, 2, 6, y, 2, bevel=0.008)
        d.beam(K.wood("dark", "y"), (lo(-5), 5 * VS, hi(2) - 0.02), (hi(5), 22 * VS, hi(2) - 0.02), 0.08, 0.04)
        for y in (4, 23):
            d.box2("steel_dark", (lo(-6), y * VS - 0.025, hi(2)), (lo(-6) + 0.3, y * VS + 0.025, hi(2) + 0.008), bevel=0.003)
    h = a.p("Scrap")
    h.cyl("chrome", 0.025, 0.02, (4.5 * VS, 12 * VS, hi(1) + 0.01), "z", 12, 0.003)
    h.rod("chrome", (4.5 * VS, 12 * VS, hi(1) + 0.03), (hi(5), 12 * VS, hi(2)), 0.012)
    h.box2("chrome", (lo(4), 12 * VS - 0.06, hi(1)), (hi(5), 12 * VS + 0.06, hi(1) + 0.006), bevel=0.002)
    return a


def floor(gid, style):
    a = Asset(gid, "Structure")
    if style == "concrete":
        c = a.p("Concrete")
        c.box2("conc_dark", (lo(-12) + 0.01, lo(0), lo(-12) + 0.01), (hi(12) - 0.01, lo(0) + 0.04, hi(12) - 0.01), bevel=0)
        for (x0, x1) in ((lo(-12), 0), (0, hi(12))):
            for (z0, z1) in ((lo(-12), 0), (0, hi(12))):
                c.box2("conc", (x0 + 0.006, lo(0) + 0.02, z0 + 0.006), (x1 - 0.006, hi(1), z1 - 0.006), bevel=0.01)
        r = K.rng(711)
        for k in range(5):                              # hairline cracks, a patched corner, a drain
            p0 = Vector((r.uniform(-0.9, 0.9), hi(1) + 0.001, r.uniform(-0.9, 0.9)))
            pts = [p0]
            for j in range(4):
                pts.append(pts[-1] + Vector((r.uniform(-0.12, 0.12), 0, r.uniform(-0.12, 0.12))))
            c.tube("conc_dark", pts, 0.004, 4, caps=False)
        c.box2("conc_dark", (0.5, hi(1) - 0.002, -0.85), (0.86, hi(1) + 0.003, -0.55), bevel=0.004)
        c.cyl("steel_dark", 0.07, 0.01, (-0.55, hi(1) - 0.002, 0.55), "y", 16, 0.003)
        for k in range(-2, 3):
            c.box2("black", (-0.6, hi(1) + 0.001, 0.55 + k * 0.024 - 0.006), (-0.5, hi(1) + 0.004, 0.55 + k * 0.024 + 0.006), bevel=0)
        return a
    w = a.p("Plank" if style == "plank" else "Wood")
    for z in range(-12, 13, 6 if style == "plank" else 8):     # joists under the boards
        w.box2(K.wood("dark", "x"), (lo(-12) + 0.02, lo(0), z * VS - 0.04), (hi(12) - 0.02, hi(0), z * VS + 0.04), bevel=0.006)
    if style == "plank":   # boards along z in 4-voxel lanes, staggered butt joints
        for lane in range(7):
            x0, x1 = lo(-12) + lane * 4 * VS, min(hi(12), lo(-12) + (lane + 1) * 4 * VS - 0.008)
            cut = lo(-12) + (8 if lane & 1 else 16) * VS
            for (za, zb) in ((lo(-12), cut - 0.004), (cut + 0.004, hi(12))):
                w.box2(K.WOODS["z"][lane % 3], (x0, lo(1), za), (x1, hi(1), zb), bevel=0.006)
            for z in range(-12, 13, 6):
                K.nails(w, "steel", [((x0 + x1) / 2, hi(1) + 0.002, z * VS)], 0.007, "y", 0.005)
    else:                    # boards along x, 3 voxels wide
        for k in range(9):
            z0 = lo(-12) + k * 3 * VS
            z1 = min(hi(12), z0 + 3 * VS) - 0.007
            w.box2(K.WOODS["x"][k % 3], (lo(-12), lo(1), z0), (hi(12), hi(1), z1), bevel=0.006)
            for x in (-11, 0, 11):
                K.nails(w, "steel_dark", [(x * VS, hi(1) + 0.002, (z0 + z1) / 2)], 0.007, "y", 0.005)
    return a


def plate(gid, wood):
    a = Asset(gid, "Structure")
    if wood:
        w = a.p("Wood")
        for k in range(5):
            x0 = lo(-6) + k * 0.208
            w.box2(K.WOODS["z"][k % 3], (x0 + 0.003, lo(0), lo(-6)), (x0 + 0.205, hi(0), hi(6)), bevel=0.006)
        return a
    s = a.p("Scrap")
    K.corrugated(s, "tin_rust", lo(-6), hi(6), lo(-6), hi(6), 0.01, 0.07, 0.004,
                 Matrix.Translation((0, 0.0, 0)) @ Matrix(((1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 1, 0), (0, 0, 0, 1))))
    s.box2("steel_dark", (lo(-6), lo(0), lo(-6)), (hi(6), lo(0) + 0.02, hi(6)), bevel=0.003)
    for x in (-5, 5):
        for z in (-5, 5):
            s.sphere("chrome", 0.03, (x * VS, hi(1) - 0.03, z * VS), (1, 0.8, 1), 10, 6)
            s.cyl("steel", 0.012, 0.06, (x * VS, hi(0) + 0.0, z * VS), "y", 8, 0.0)
    return a


def foundation(gid, stone):
    a = Asset(gid, "Structure")
    if stone:
        c, s = a.p("Concrete"), a.p("Stone")
        c.box2("conc_light", (lo(-12), lo(0), lo(-12)), (hi(12), hi(1), hi(12)), bevel=0.012)
        # dressed stone skirt in 3-voxel courses, 6-voxel blocks, staggered
        r = K.rng(17)
        for course in range(8):
            y0, y1 = lo(-24 + course * 3) + 0.005, hi(-24 + course * 3 + 2) - 0.005
            for side in range(4):
                n = 4
                for k in range(n):
                    off = (course % 2) * 0.12
                    u0 = -1.0 + k * 0.5 + off - (0.12 if course % 2 and k == 0 else 0)
                    u1 = min(1.0, u0 + 0.5 - 0.008)
                    u0 = max(-1.0, u0)
                    m = ["stone", "rock", "stone"][r.randrange(3)]
                    d = r.uniform(-0.006, 0.006)
                    bv = dict(bevel=0.012)
                    if side == 0:
                        s.box2(m, (u0, y0, lo(-12) + d), (u1, y1, lo(-12) + 0.14), bevel=0.012, segs=1)
                    elif side == 1:
                        s.box2(m, (u0, y0, hi(12) - 0.14), (u1, y1, hi(12) + d), bevel=0.012, segs=1)
                    elif side == 2:
                        s.box2(m, (lo(-12) + d, y0, u0 + 0.0), (lo(-12) + 0.14, y1, u1), bevel=0.012, segs=1)
                    else:
                        s.box2(m, (hi(12) - 0.14, y0, u0), (hi(12) + d, y1, u1), bevel=0.012, segs=1)
        if True:
            s.box2("conc_dark", (lo(-12) + 0.1, lo(-24), lo(-12) + 0.1), (hi(12) - 0.1, lo(-23), hi(12) - 0.1), bevel=0)
        return a
    w, fe = a.p("Wood"), a.p("Iron")
    for k in range(9):                                   # deck boards along z, 3 voxels wide
        x0 = lo(-12) + k * 3 * VS
        x1 = min(hi(12), x0 + 3 * VS) - 0.007
        w.box2(K.WOODS["z"][k % 3], (x0, lo(1), lo(-12)), (x1, hi(1), hi(12)), bevel=0.006)
    for (z0, z1) in ((-12, -11), (11, 12), (-1, 0)):     # joists
        w.box2(K.wood("dark", "x"), (lo(-12), lo(0), lo(z0)), (hi(12), hi(0), hi(z1)), bevel=0.008)
    for x in (-11, 0, 11):                               # posts 2 x 2 voxels
        for z in (-11, 0, 11):
            w.box2(K.wood("dark", "y"), (lo(x), lo(-24), lo(z)), (hi(x + 1), hi(-1), hi(z + 1)), bevel=0.012)
    for z in (-11, 11):                                  # braces
        for sx in (-1, 1):
            w.beam(K.wood("", "y"), (sx * 11 * VS, -22 * VS, z * VS + 0.04), (0.0, -2 * VS, z * VS + 0.04), 0.06, 0.05)
    for x in (-11, 12):
        for z in (-11, 12):
            fe.cyl("steel", 0.025, 0.02, (x * VS - 0.04 * (1 if x > 0 else -1) * 0, hi(1) + 0.004, z * VS), "y", 6, 0.003)
    return a


def ramp(gid, concrete):
    a = Asset(gid, "Structure")
    top = lambda z: round((z + 36) * 12 / 72)
    if concrete:
        c, fe = a.p("Concrete"), a.p("Iron")
        prof = [(lo(-36), lo(0)), (hi(36), lo(0)), (hi(36), hi(12)), (lo(-36), hi(0) - 0.02)]
        c.prism("conc_light", [(z, y) for z, y in prof], lo(-12), hi(12), plane="zy")
        slope = (hi(12) - hi(0) + 0.02) / (hi(36) - lo(-36))
        for z in range(-32, 36, 4):                      # grip grooves
            zz = z * VS
            y = hi(0) - 0.02 + (zz - lo(-36)) * slope
            c.beam("conc_dark", (lo(-12) + 0.06, y + 0.004, zz), (hi(12) - 0.06, y + 0.004, zz), 0.025, 0.012)
        for x0, x1 in ((-12, -11), (11, 12)):
            fe.box2("hazard", (lo(x0), lo(0), lo(-36)), (hi(x1), hi(0) + 0.01, hi(-35)), bevel=0.004)
        return a
    w = a.p("Wood")
    slope = (hi(12) - hi(0)) / (hi(36) - lo(-36))
    ang = math.degrees(math.atan(slope))
    L = math.hypot(hi(36) - lo(-36), hi(12) - hi(0))
    for k in range(25):                                 # deck boards across, 0.24 m
        z = lo(-36) + (k + 0.5) * (hi(36) - lo(-36)) / 25
        y = hi(0) - 0.04 + (z - lo(-36)) * slope
        w.box(K.WOODS["x"][k % 3], (2.0 - 0.06, 0.06, (hi(36) - lo(-36)) / 25 - 0.008), (0, y, z), r=(-ang, 0, 0), bevel=0.006)
    for x in (-12, 12):                                  # stringers
        pts = [(lo(-36), lo(0)), (hi(36), lo(0)), (hi(36), hi(12) - 0.06), (lo(-36), hi(0) - 0.06)]
        w.prism(K.wood("dark", "z"), pts, lo(x), hi(x), plane="zy")
    for z in range(-24, 37, 12):                         # cross supports
        w.box2(K.wood("dark", "x"), (lo(-11), lo(0), lo(z)), (hi(11), hi(top(z) - 1) - 0.04, hi(z)), bevel=0.008)
    return a


# ------------------------------------------------------------------------------------------ stairs, ladder, roofs
def stairs():
    a = Asset("stairs", "Structure")
    w = a.p("Wood")
    for i in range(15):
        z0, y = -20 + i * 8 // 3, i * 2
        w.box2(K.WOODS["x"][i % 3], (lo(-6), lo(y + 1) + 0.02, lo(z0)), (hi(6), hi(y + 1), hi(z0 + 3)), bevel=0.008)
        w.box2(K.wood("dark", "x"), (lo(-6) + 0.02, lo(y), lo(z0) + 0.02), (hi(6) - 0.02, lo(y + 1) + 0.02, lo(z0) + 0.05), bevel=0.004)   # riser
    for x in (-7, 7):                                    # stringers (sloped planks)
        a0, a1 = Vector((x * VS, 0.0, lo(-20))), Vector((x * VS, hi(30), hi(19)))
        sec = []
        for (zz, yy) in ((lo(-20), lo(0)), (lo(-20) + 0.24, lo(0)), (hi(19), hi(30) - 0.24), (hi(19), hi(30)), (hi(19) - 0.24, hi(30)), (lo(-20), lo(0) + 0.24)):
            sec.append((zz, yy))
        w.prism(K.wood("dark", "z"), sec, lo(x), hi(x), plane="zy")
    return a


def ladder():
    a = Asset("ladder", "Structure")
    w = a.p("Wood")
    for x in (-4, 4):
        w.box2(K.wood("dark", "y"), (lo(x) + 0.006, lo(0), lo(0) + 0.004), (hi(x) - 0.006, hi(30), hi(0) - 0.004), bevel=0.008)
    for y in range(3, 30, 4):
        w.cyl(K.wood("light", "x"), 0.02, (7 * VS), (0, y * VS, 0), "x", 10, 0.003)
        for x in (-4, 4):
            K.nails(w, "steel_dark", [(x * VS, y * VS, hi(0))], 0.006, "z", 0.004)
    return a


def roof(gid, slope):
    a = Asset(gid, "Structure")
    s = a.p("Scrap")
    if slope:
        y0, y1 = lo(0) + 0.022, hi(12) - 0.022
        dz = hi(12) - lo(-12)
        ang = math.atan2(y1 - y0, dz)
        L = math.hypot(dz, y1 - y0)
        mt = Matrix.Translation((0, (y0 + y1) / 2, 0)) @ Matrix.Rotation(-ang, 4, "X")
        for i in range(3):
            K.corrugated(s, ["tin_rust", "tin", "tin_rust"][i], lo(-12) + i * 0.66 - (0.01 if i else 0), lo(-12) + (i + 1) * 0.67, -L / 2, L / 2, 0.016, 0.076, 0.004, mt)
        for z in (-10, 0, 10):                            # purlins under the sheet
            yy = y0 + (z * VS - lo(-12)) / dz * (y1 - y0) - 0.045
            s.box2("wood_dark", (lo(-12), yy - 0.03, z * VS - 0.04), (hi(12), yy + 0.02, z * VS + 0.04), bevel=0.006)
        a.parts["Scrap"].byte = "Scrap"
        return a
    for i in range(3):
        K.corrugated(s, ["tin_rust", "tin", "tin_rust"][i], lo(-12) + i * 0.66 - (0.01 if i else 0), lo(-12) + (i + 1) * 0.67, lo(-12), hi(12), 0.016, 0.076, 0.004,
                     Matrix(((1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 1, 0), (0, 0, 0, 1))))
    for x in range(-11, 12, 4):
        for z in (-11, 0, 11):
            s.sphere("steel_dark", 0.012, (x * VS + 0.038, 0.022, z * VS), (1, 0.5, 1), 8, 4)
    return a


# ------------------------------------------------------------------------------------------ fences, gate, post
def fence(gid, wire):
    a = Asset(gid, "Structure")
    if wire:
        s = a.p("Scrap")
        for x in (-12, 0, 12):
            s.cyl("steel_dark", 0.035, hi(16) - lo(0), (x * VS, (hi(16) + lo(0)) / 2, 0), "y", 10, 0.004)
            s.sphere("steel_dark", 0.04, (x * VS, hi(16), 0), (1, 0.5, 1), 10, 5)
        for y in (1, 15):
            s.rod("chrome", (lo(-12), y * VS, 0), (hi(12), y * VS, 0), 0.006, 5)
        for k in range(-12, 16, 3):                      # chain link diagonals inside y 1..15
            for sgn in (1, -1):
                pts = []
                for t in (0.0, 1.0):
                    pass
                xa, ya = (k * VS, 1 * VS) if sgn == 1 else (k * VS, 15 * VS)
                xb, yb = xa + 14 * VS, (15 * VS if sgn == 1 else 1 * VS)
                # clip to the panel width
                if xb > hi(12):
                    f = (hi(12) - xa) / (xb - xa)
                    xb, yb = hi(12), ya + (yb - ya) * f
                if xa < lo(-12):
                    continue
                if xb - xa > 0.02:
                    s.rod("chrome", (xa, ya, 0), (xb, yb, 0), 0.0045, 4)
            # the mirrored set from the left edge
        for k in range(1, 15, 3):
            for sgn in (1, -1):
                ya = (1 + k) * VS if sgn == 1 else (15 - k) * VS
                yb = 15 * VS if sgn == 1 else 1 * VS
                xa = lo(-12) + 0.04
                xb = xa + abs(yb - ya)
                s.rod("chrome", (xa, ya, 0), (xb, yb, 0), 0.0045, 4)
        return a
    w = a.p("Wood")
    for x in (-12, 0, 12):
        w.boxv(K.wood("dark", "y"), x, 0, 0, x, 15, 0, bevel=0.01)
        w.add(K.kit.bm_cyl(0.057, 0.08, 4, 0.0, 0.0), K.wood("dark", "y"), Matrix.Translation((x * VS, hi(16) + 0.02, 0)) @ Matrix.Rotation(math.radians(45), 4, "Y") @ K._axis_mtx("y"))
    for (y0, y1) in ((5, 6), (12, 13)):
        for k, (xa, xb) in enumerate(((lo(-12), 0.5), (0.5, hi(12)))):
            w.box2(K.WOODS["x"][k], (xa + 0.004, lo(y0) + 0.01, lo(1)), (xb - 0.004, hi(y1) - 0.01, hi(1)), bevel=0.008)
        for x in (-12, 0, 12):
            K.nails(w, "steel_dark", [(x * VS, (y0 + y1) / 2 * VS, hi(1) + 0.002)], 0.007, "z", 0.005)
    return a


def gate():
    a = Asset("gate", "Structure")
    w, fe = a.p("Wood"), a.p("Iron")
    for (y0, y1) in ((2, 3), (12, 13)):
        w.box2(K.wood("", "x"), (lo(-12), lo(y0), lo(0)), (hi(12), hi(y1), hi(0)), bevel=0.008)
    for x in range(-12, 13, 4):
        w.box2(K.WOODS["y"][(x // 4) % 3], (lo(x) + 0.004, lo(1), lo(0) + 0.008), (hi(x) - 0.004, hi(15), hi(0) - 0.008), bevel=0.008)
    w.beam(K.wood("dark", "x"), (-12 * VS, 3 * VS, 0), (12 * VS, 12 * VS, 0), 0.07, 0.06)
    for y in (2.5, 12.5):                                # strap hinges at the hinge side (x = -0.96)
        fe.box2("steel_dark", (lo(-12), y * VS - 0.025, hi(0)), (lo(-12) + 0.32, y * VS + 0.025, hi(0) + 0.008), bevel=0.003)
    fe.box2("steel", (hi(11), 8 * VS - 0.03, lo(0) - 0.0), (hi(12), 8 * VS + 0.03, hi(0) + 0.0), bevel=0.003)   # latch
    return a


def post():
    a = Asset("post", "Structure")
    w = a.p("Wood")
    w.box2(K.wood("dark", "y"), (lo(-1), lo(0), lo(-1)), (hi(1), hi(29) - 0.08, hi(1)), bevel=0.02)
    w.add(K.kit.bm_cyl(0.17, 0.08, 4, 0.0, 0.0), K.wood("dark", "y"), Matrix.Translation((0, hi(29) - 0.03, 0)) @ Matrix.Rotation(math.radians(45), 4, "Y") @ K._axis_mtx("y"))
    for y in (6, 22):
        w.box2("steel_dark", (lo(-1) - 0.004, y * VS - 0.03, lo(-1) - 0.004), (hi(1) + 0.004, y * VS + 0.03, hi(1) + 0.004), bevel=0.003)
    return a


def barricade():
    a = Asset("barricade", "Structure")
    w, s = a.p("Wood"), a.p("Scrap")
    r = 0.6 * VS + 0.02
    w.rod(K.wood("", "y"), (-9 * VS, lo(0) + r, 0), (9 * VS, hi(12) - r, 0), r, 8)
    w.rod(K.wood("dark", "y"), (9 * VS, lo(0) + r, 0), (-9 * VS, hi(12) - r, 0), r, 8)
    w.rod(K.wood("light", "x"), (lo(-10) + 0.05, 6 * VS, 1 * VS), (hi(10) - 0.05, 6 * VS, 1 * VS), r, 8)
    for x in (-8, -3, 3, 8):
        s.tube("rust", [(x * VS, 8 * VS, 1 * VS), (x * VS, 10 * VS, 3 * VS), (x * VS, hi(12) - 0.02, hi(5) - 0.02)], [0.016, 0.012, 0.003], 6)
    s.rod("steel_dark", (-9 * VS, 6 * VS, 0.06), (9 * VS, 6 * VS, 0.06), 0.006, 4)
    return a


def jump_ramp():
    a = Asset("jump_ramp", "Structure")
    w, s = a.p("Wood"), a.p("Scrap")
    top = lambda z: round((z + 30) / 60 * 16)
    for z in range(-30, 31):
        if (z & 3) == 0 or z == -30:
            continue
    ys = [(z, top(z)) for z in range(-30, 31)]
    # deck as boards across x following the stepped voxel line, smoothed
    for k in range(30):
        z0, z1 = -30 + k * 2, -30 + k * 2 + 1
        y0, y1 = hi(top(z0)), hi(top(z1))
        ym = (y0 + y1) / 2
        ang = math.degrees(math.atan2(16 * VS, 60 * VS))
        w.box((hi(18) - lo(-18), 0.05, 2 * VS - 0.01), (0, ym - 0.03, (z0 + z1) / 2 * VS + 0.0), r=(-ang, 0, 0), m=None, bevel=0.006) if False else \
            w.box(K.WOODS["x"][k % 3], (hi(18) - lo(-18), 0.05, 2 * VS - 0.01), (0, ym - 0.03, (z0 + z1) / 2 * VS), r=(-ang, 0, 0), bevel=0.006)
    for x in (-15, 15):                                   # stringers
        w.prism(K.wood("dark", "z"), [(lo(-30), lo(0)), (hi(30), lo(0)), (hi(30), hi(16) - 0.07), (lo(-30), hi(0) - 0.06)], x * VS - 0.04, x * VS + 0.04, plane="zy")
    for z in (-15, 0, 15, 30):                            # steel trestles
        t = top(z)
        for x in (-17, 17):
            s.box2("steel_dark", (lo(x) + 0.01, lo(0), lo(z) + 0.01), (hi(x) - 0.01, hi(t - 1), hi(z) - 0.01), bevel=0.006)
        s.box2("rust", (lo(-17), lo(t - 1), lo(z) + 0.01), (hi(17), hi(t - 1) - 0.02, hi(z) - 0.01), bevel=0.006)
        s.beam("steel_dark", (-17 * VS, 0.05, z * VS), (17 * VS, hi(t - 1) - 0.04, z * VS), 0.04, 0.03)
    for i in range(12):                                   # hazard lip
        x0 = lo(-18) + i * (hi(18) - lo(-18)) / 12
        s.box2(["hazard", "black"][i % 2], (x0, lo(16), lo(30)), (x0 + (hi(18) - lo(-18)) / 12, hi(16), hi(30)), bevel=0.003)
    return a


# ------------------------------------------------------------------------------------------ base pieces
def garage_frame():
    a = Asset("garage_frame", "Structure")
    c, fe = a.p("Concrete"), a.p("Iron")
    K.slab_xy(c, "conc", lo(-24), hi(24), lo(0), hi(29), lo(0), hi(1), [(lo(-20), hi(20), lo(-1), hi(26))], bevel=0.012)
    fe.box2("steel", (lo(-21), lo(27), lo(2)), (hi(21), hi(29), hi(3)), bevel=0.02)          # roller drum housing
    fe.cyl("steel_dark", 0.08, hi(21) - lo(-21) + 0.02, (0, 28 * VS, 2.5 * VS), "x", 16, 0.006)
    for x in (-21, 21):
        for k in range(9):
            fe.box2(["hazard", "black"][k % 2], (lo(x), lo(k * 3), lo(2)), (hi(x), min(hi(26), lo(k * 3) + 0.24), hi(2)), bevel=0.003)
    return a


def roller_leaf(gid, garage):
    a = Asset(gid, "Structure")
    if garage:
        fe = a.p("Iron")
        for k in range(9):                                 # slats, 3 voxels each
            y0 = lo(0) + k * 3 * VS
            fe.box2(["steel", "steel_dark"][0 if k % 3 else 1], (lo(-20), y0 + 0.004, lo(2)), (hi(20), min(hi(26), y0 + 3 * VS) - 0.004, hi(2) - 0.02), bevel=0.012)
        fe.box2("black", (lo(-20), lo(0), lo(3)), (hi(20), hi(0), hi(3)), bevel=0.006)
        fe.box2("chrome", (lo(-2), 3 * VS - 0.02, lo(3)), (hi(2), 3 * VS + 0.02, hi(3)), bevel=0.006)
        return a
    w, fe = a.p("Wood"), a.p("Iron")
    for k in range(11):
        y0 = lo(12) + k * VS
        w.box(K.WOODS["x"][k % 2], (hi(6) - lo(-6), VS * 0.9, 0.05), (0, y0 + VS / 2, 2 * VS), r=(-12, 0, 0), bevel=0.006)
    fe.boxv("steel", -6, 12, 3, 6, 12, 3, bevel=0.006)
    return a


# ------------------------------------------------------------------------------------------ tiles
def T_walls_wood():
    return [wall_wood("wall_wood", 0), wall_wood("wall_wood_window", 1), wall_wood("doorway_wood", 2), door("door_wood", False)]


def T_walls_brick():
    return [wall_brick("wall_brick", 0), wall_brick("wall_brick_window", 1), wall_brick("doorway_brick", 2), door("door_metal", True)]


def T_walls_concrete():
    return [wall_concrete("wall_concrete", 0), wall_concrete("doorway_concrete", 2), wall_panel("wall_panel", 0), wall_panel("doorway_panel", 2)]


def T_walls_plank():
    return [wall_plank("wall_plank", 0), wall_plank("wall_plank_window", 1), wall_plank("doorway_plank", 2), floor("floor_plank", "plank")]


def T_walls_fired():
    return [wall_brick("wall_brick_fired", 0, True), wall_brick("wall_brick_fired_window", 1, True), wall_brick("doorway_brick_fired", 2, True), wall_scrap()]


def T_floors():
    return [floor("floor_wood", "wood"), floor("floor_concrete", "concrete"), plate("plate_wood", True), plate("plate_scrap", False),
            roof("roof_flat", False), roof("roof_slope", True)]


def T_foundations():
    return [foundation("foundation_wood", False), foundation("foundation_stone", True), ramp("ramp_wood", False), ramp("ramp_concrete", True)]


def T_access():
    return [stairs(), ladder(), jump_ramp()]


def T_fences():
    return [fence("fence_wood", False), fence("fence_wire", True), gate(), post(), barricade()]


def T_garage():
    return [garage_frame(), roller_leaf("garage_door", True), roller_leaf("shutter", False)]


TILES = [("furn_walls_wood", "Wood walls: wall, window, doorway + plank door", [T_walls_wood], 4),
         ("furn_walls_brick", "Brick walls: wall, window, doorway + metal door", [T_walls_brick], 4),
         ("furn_walls_concrete", "Concrete walls + prefab panels (wall, doorway)", [T_walls_concrete], 4),
         ("furn_walls_plank", "Sawn-plank walls (wall, window, doorway) + plank floor", [T_walls_plank], 4),
         ("furn_walls_fired", "Fired-brick walls (wall, window, doorway) + scrap wall", [T_walls_fired], 4),
         ("furn_floors_roofs", "Floors (wood, concrete), panels (wood, scrap), roofs (flat, sloped)", [T_floors]),
         ("furn_foundations", "Foundations (timber, stone), ramps (wood, concrete)", [T_foundations]),
         ("furn_access", "Stairs, ladder, jump ramp", [T_access]),
         ("furn_fences", "Fences (wood, wire), gate, post, barricade", [T_fences]),
         ("furn_garage", "Garage doorway, roller garage door, window shutter", [T_garage])]

if __name__ == "__main__":
    K.run_tiles(TILES, "furniture", os.path.dirname(os.path.abspath(__file__)), "structure_")
