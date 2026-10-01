"""HD modular kits for the procedural sites of SiteBuilder.cs (template ids site:{cell}:{piece} are generated per world, so
the HD replacement is a kit the builder can place along the same plan):

  Bunker (0.2 m voxels, 4 m cells, walls 13 voxels high): wall, outer wall (double thick), doorway, entry, pillar, roof
  cell (slab + sod, green / sand / fallout variants), ceiling lamp, pipe run, surface vent, surface hatch, ramp
  retaining wall (8 m, falling 3 m), entrance sandbags, blast door, corner rubble.
  Rock tunnel (0.25 m voxels, 8 m segments): side wall (strata), wall with an ore seam, arched roof, portal face,
  boulders, drip stones.
  Airfield (0.2 m voxels): Quonset hangar (18 x 14 m, open to the strip, man door at the back), radio hut with mast,
  windsock, fuel drums.
Kit pieces: origin at the cell / segment centre on the floor, +Z toward the entrance (bunker: the entrance is -Z).

blender -b -P sites.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import hdkit as K  # noqa: E402
from hdkit import Asset, Vector, Matrix  # noqa: E402

VB_ = 0.2      # bunker / airfield voxel
VT = 0.25      # tunnel voxel


def L(i, v=VB_):
    return (i - 0.5) * v


def H(i, v=VB_):
    return (i + 0.5) * v


def C(i, v=VB_):
    return i * v


# ------------------------------------------------------------------------------------------ bunker
def _wall_face(p, t0, t1, holes, outer, frame_half=None):
    """A bunker wall along x at z = 0 (voxels t -10..10, y 0..12): army-green dado to y 5, a steel strip at y 5, concrete above."""
    z0, z1 = L(0), H(0)
    for (a0, a1, b0, b1) in K.rect_cells(L(t0), H(t1), L(0), H(12), holes):
        lo_d, hi_d = b0, min(b1, L(5))
        if hi_d > lo_d:
            p.box2("rig_green", (a0, lo_d, z0), (a1, hi_d, z1), bevel=0.0)
        if b1 > L(5) and b0 < H(5):
            p.box2("steel_dark", (a0, max(b0, L(5)), z0 - 0.004), (a1, min(b1, H(5)), z1 + 0.004), bevel=0.0)
        if b1 > H(5):
            p.box2("conc", (a0, max(b0, H(5)), z0), (a1, b1, z1), bevel=0.0)
    if outer:
        for (a0, a1, b0, b1) in K.rect_cells(L(t0), H(t1), L(0), H(12), holes):
            p.box2("conc_dark", (a0, b0, H(0)), (a1, b1, H(1)), bevel=0.0)
    if frame_half is not None:                             # hazard-striped door frame (|t| == half + 1)
        for sx in (-1, 1):
            x = sx * (frame_half + 1)
            for k in range(6):
                y0 = L(1) + k * (H(11) - L(1)) / 6
                p.box2("hazard" if k % 2 == 0 else "black", (L(x), y0, z0 - 0.01), (H(x), y0 + (H(11) - L(1)) / 6, z1 + 0.01), bevel=0.0)
        for k in range(2 * frame_half + 3):
            x = -frame_half - 1 + k
            p.box2("hazard" if k % 2 == 0 else "black", (L(x), L(12), z0 - 0.01), (H(x), H(12), z1 + 0.01), bevel=0.0)


def bunker_wall(gid, kind):
    a = Asset(gid, "Site", kind="kit", voxel=VB_, note="SiteBuilder.Bunker wall kit")
    p = a.p("Shell_Stone", "Stone")
    holes = []
    half = None
    if kind == "doorway":
        half = 4
    elif kind == "entry":
        half = 7
    if half is not None:
        holes = [(L(-half), H(half), L(1), H(11))]
    _wall_face(p, -10, 10, holes, kind == "outer", half)
    if kind == "outer":
        d = a.p("Detail", "Scrap")
        d.cyl("rust", 0.07, H(9) - L(-9), (0, C(11), L(0) - 0.08), "x", 10, 0.004)
        for t in range(-7, 8, 7):
            d.cyl("cloth_red", 0.085, 0.06, (C(t), C(11), L(0) - 0.08), "x", 10, 0.004)
            d.box2("steel_dark", (C(t) - 0.02, C(11), L(0) - 0.04), (C(t) + 0.02, C(11) + 0.12, L(0)), bevel=0.0)
    return a


def bunker_pillar():
    a = Asset("Site_Bunker_Pillar", "Site", kind="kit", voxel=VB_)
    p = a.p("Shell_Stone", "Stone")
    p.box2("conc", (L(-1), L(0), L(-1)), (H(1), H(12), H(1)), bevel=0.02)
    p.box2("rig_green", (L(-1) - 0.005, L(0), L(-1) - 0.005), (H(1) + 0.005, H(5) - 0.2, H(1) + 0.005), bevel=0.0)
    for k in range(4):
        p.box2("hazard" if k % 2 == 0 else "black", (L(-1) - 0.008, L(1) + k * 0.15, L(-1) - 0.008), (H(1) + 0.008, L(1) + (k + 1) * 0.15, H(1) + 0.008), bevel=0.0)
    return a


def bunker_roof(gid, sod):
    a = Asset(gid, "Site", kind="kit", voxel=VB_, note="cell roof: slab y 13-14 + sod y 15 (4 m cell; overhangs add 3 voxels where the neighbour is earth)")
    s, so = a.p("Shell_Stone", "Stone"), a.p("Shell_Sand", "Sand")
    s.box2("conc_dark", (L(-10), L(13), L(-10)), (H(10), H(14), H(10)), bevel=0.0)
    so.box2(sod, (L(-10), L(15), L(-10)), (H(10), H(15) - 0.03, H(10)), bevel=0.0)
    r = K.rng(len(gid))
    for k in range(14):                                     # tufts / lumps on the sod
        x, z = r.uniform(-1.9, 1.9), r.uniform(-1.9, 1.9)
        so.rock(sod, r.uniform(0.08, 0.16), (x, H(15) - 0.04, z), seed=k, squash=0.4, detail=1, floor=H(15) - 0.05)
    return a


def bunker_lamp():
    a = Asset("Site_Bunker_Lamp", "Site", kind="kit", voxel=VB_)
    s, b = a.p("Scrap"), a.p("Bulb", "Glass")
    s.box2("steel_dark", (L(-1), L(12), L(-1)), (H(1), L(12) + 0.06, H(1)), bevel=0.01)
    for z in (-1, 1):
        s.box2("steel", (L(-1), L(12) - 0.06, C(z) - 0.02), (H(1), L(12), C(z) + 0.02), bevel=0.004)
    b.box2("lamp", (L(-1) + 0.03, L(12) - 0.05, L(0)), (H(1) - 0.03, L(12), H(0)), bevel=0.01)
    for x in range(-1, 2):
        s.rod("steel_dark", (C(x) * 0.8, L(12) - 0.06, L(-1) + 0.05), (C(x) * 0.8, L(12) - 0.06, H(1) - 0.05), 0.004, 4)
    return a


def bunker_surface(gid, hatch):
    a = Asset(gid, "Site", kind="kit", voxel=VB_)
    s = a.p("Scrap")
    if hatch:
        s.box2("steel", (L(-3), L(16), L(-3)), (H(3), H(16), H(3)), bevel=0.02)
        for k in range(3):
            s.box2("rust", (L(-3) + 0.02 + k * 0.48, H(16) - 0.004, L(-3) + 0.02), (L(-3) + 0.3 + k * 0.48, H(16) + 0.002, H(3) - 0.02), bevel=0)
        s.tube("rust", [(C(2), H(16), C(-1)), (C(2), H(16) + 0.08, C(-0.5)), (C(2), H(16) + 0.08, C(0.5)), (C(2), H(16), C(1))], 0.02, 6)
    else:
        s.cyl("rust", 1.8 * VB_, H(20) - L(16), (C(4), (H(20) + L(16)) / 2, C(-3)), "y", 16, 0.01)
        s.box2("steel", (L(2), L(21), L(-5)), (H(6), H(21), H(-1)), bevel=0.02)
        s.box2("steel_dark", (L(3), L(20), L(-4)), (H(5), H(20), H(-2)), bevel=0.01)
        for k in range(4):
            s.box2("black", (L(3), L(17) + k * 0.12, C(-3) - 1.8 * VB_ - 0.004), (H(5), L(17) + k * 0.12 + 0.05, C(-3) - 1.8 * VB_ + 0.004), bevel=0)
    return a


def bunker_ramp_wall():
    a = Asset("Site_Bunker_RampWall", "Site", kind="kit", voxel=VB_, note="8 m entrance ramp retaining wall; floor falls 3 m toward +z (the door)")
    s = a.p("Shell_Stone", "Stone")
    L8 = 8.0
    pts = [(-L8 / 2, 0.0), (L8 / 2, -3.0), (L8 / 2, H(16)), (-L8 / 2, H(16))]
    s.prism("conc", [(z, y) for z, y in pts], -VB_ / 2, VB_ / 2, plane="zy")
    s.box2("conc_light", (-VB_ / 2, H(16) - 0.04, -L8 / 2), (VB_ * 1.5, H(16) + 0.2, L8 / 2), bevel=0.02)
    for k in range(9):
        z = -L8 / 2 + k * L8 / 8
        if k % 2:                                           # formwork joints
            s.box2("conc_dark", (VB_ / 2 - 0.004, -3 * (k / 8), z - 0.012), (VB_ / 2 + 0.004, H(16), z + 0.012), bevel=0)
    return a


def bunker_sandbags():
    a = Asset("Site_Bunker_Sandbags", "Site", kind="kit", voxel=VB_, note="entrance sandbag row (13 voxels along the ramp top)")
    s = a.p("Cloth")
    for k in range(3):
        for j in range(7):
            z0 = L(0) + j * (H(12) - L(0)) / 7 + (k % 2) * 0.18
            if z0 + 0.36 > H(12) + 0.01:
                continue
            cx = C(2 + k)
            y = H(16) + (0.1 if k == 1 else 0.0)
            s.superloft("sandbag" if (j + k) % 3 else "burlap", [(z0 + 0.01, cx, y, 0.06, 0.04), (z0 + 0.06, cx, y, 0.1, 0.08), (z0 + 0.18, cx, y + 0.01, 0.11, 0.09),
                                                                 (z0 + 0.3, cx, y, 0.1, 0.08), (z0 + 0.35, cx, y, 0.06, 0.04)], 10, 2.6, axis="z")
    return a


def bunker_blast_door():
    a = Asset("Site_Bunker_BlastDoor", "Site", kind="kit", voxel=VB_, note="blast door leaf, swung open inside the entry (13 x 11 voxels)")
    s = a.p("Scrap")
    s.box2("rust", (C(0) - 0.08, L(1), L(0)), (C(0) + 0.08, H(11), H(12)), bevel=0.03)
    for k in range(8):                                      # diagonal hazard bands
        z0 = L(0) + k * 0.36
        if z0 + 1.0 < H(12):
            s.quad("hazard", (C(0) - 0.085, L(1), z0), (C(0) - 0.085, L(1), z0 + 0.14), (C(0) - 0.085, H(11), z0 + 0.14 + 1.0), (C(0) - 0.085, H(11), z0 + 1.0), 0.004)
    for y in (2, 9):
        s.cyl("steel_dark", 0.06, 0.2, (C(0), C(y), L(0) + 0.02), "y", 12, 0.01)
    s.torus("steel", 0.18, 0.025, (C(0) - 0.12, C(6), C(6)), "x", 18, 6)
    for k in range(4):
        s.box("steel", (0.03, 0.36, 0.03), (C(0) - 0.12, C(6), C(6)), r=(45 * k, 0, 0), bevel=0.004)
    return a


def bunker_rubble():
    a = Asset("Site_Bunker_Rubble", "Site", kind="kit", voxel=VB_)
    s = a.p("Stone")
    r = K.rng(7)
    for k in range(14):
        s.box(["conc", "conc_dark", "rock_grey"][k % 3], (r.uniform(0.15, 0.3), r.uniform(0.1, 0.2), r.uniform(0.15, 0.3)), (r.uniform(0, 0.5), H(0) + r.uniform(0.0, 0.25), r.uniform(0, 0.5)),
              r=(r.uniform(-20, 20), r.uniform(0, 90), r.uniform(-20, 20)), bevel=0.02, segs=1)
    s.rod("rust", (0.1, H(0) + 0.2, 0.2), (0.5, H(0) + 0.5, 0.3), 0.01, 4)
    return a


# ------------------------------------------------------------------------------------------ tunnel
def tunnel_wall(gid, ore):
    a = Asset(gid, "Site", kind="kit", voxel=VT, note="tunnel side wall, 8 m segment; inner face at x = 2.7 m (right side; mirror for the left)")
    s = a.p("Shell_Stone", "Stone")
    r = K.rng(len(gid) * 3)
    for k in range(8):                                     # strata slabs, each slightly offset
        y0 = k * 0.6
        dx = r.uniform(-0.08, 0.08)
        secs = []
        for j in range(9):
            z = -4 + j
            secs.append([(2.7 + dx + r.uniform(-0.04, 0.06), y0, z), (3.3, y0, z), (3.3, y0 + 0.6, z), (2.7 + dx + r.uniform(-0.04, 0.06), y0 + 0.6, z)])
        s.loft(["rock", "stone", "rock"][k % 3], secs)
    if ore:
        o = a.p(ore, ore)
        m = {"IronOre": "ore_rust", "CopperOre": "ore_copper", "Coal": "ore_coal"}.get(ore, "ore_rust")
        for k in range(26):
            o.rock(m, r.uniform(0.06, 0.12), (2.68, 0.4 + r.uniform(0, 1.6) + 0.3 * math.sin(k), -3.6 + k * 0.28), seed=k, squash=0.5, detail=1)
    return a


def tunnel_arch():
    a = Asset("Site_Tunnel_Arch", "Site", kind="kit", voxel=VT, note="arched roof (floor + 4.6 - x^2 * 0.1 in voxel metres), 8 m segment")
    s = a.p("Shell_Stone", "Stone")
    r = K.rng(5)
    n = 16
    for j in range(8):
        z0, z1 = -4 + j, -3 + j
        for i in range(n):
            x0, x1 = -2.8 + i * 5.6 / n, -2.8 + (i + 1) * 5.6 / n
            y0, y1 = 4.6 - x0 * x0 * 0.1 + 0.0, 4.6 - x1 * x1 * 0.1
            s.quad("rock" if (i + j) % 3 else "stone", (x0, y0, z0), (x1, y1, z0), (x1, y1, z1), (x0, y0, z1), 0.45)
    for k in range(10):                                     # drip stones
        x, z = r.uniform(-2.2, 2.2), r.uniform(-3.8, 3.8)
        y = 4.6 - x * x * 0.1 - 0.2
        s.add(K.kit.bm_cyl(0.07, r.uniform(0.2, 0.6), 6, 0.0, 0.005), "rock", Matrix.Translation((x, y - 0.2, z)) @ K._axis_mtx("y"))
    return a


def tunnel_portal():
    a = Asset("Site_Tunnel_Portal", "Site", kind="kit", voxel=VT, note="portal face: solid rock above the arch")
    s = a.p("Shell_Stone", "Stone")
    outline = [(-3.3, 0.0), (-2.8, 0.0)] + [(x, 4.6 - x * x * 0.1) for x in [-2.8 + k * 0.35 for k in range(17)]] + [(2.8, 0.0), (3.3, 0.0), (3.3, 6.5), (-3.3, 6.5)]
    s.prism("rock", outline, -0.25, 0.25, plane="xy")
    r = K.rng(9)
    for k in range(12):
        s.rock("rock", r.uniform(0.2, 0.4), (r.uniform(-3.2, 3.2), r.uniform(4.8, 6.4), -0.2), seed=k, squash=0.6, detail=1)
    for x in (-2.75, 2.75):                                 # timber portal set
        s.box2(K.wood("dark", "y"), (x - 0.1, 0.0, -0.35), (x + 0.1, 4.6 - x * x * 0.1, -0.15), bevel=0.02)
    s.box2(K.wood("dark", "x"), (-2.9, 3.8, -0.35), (2.9, 4.0, -0.15), bevel=0.02)
    return a


def tunnel_boulders():
    a = Asset("Site_Tunnel_Boulders", "Site", kind="kit", voxel=VT)
    s = a.p("Stone")
    r = K.rng(11)
    for k in range(4):
        s.rock("rock", r.uniform(0.25, 0.4), (2.3 - r.uniform(0, 0.4), 0.0, r.uniform(-0.5, 0.5)), seed=k, squash=0.75, detail=2, floor=0.0)
    return a


# ------------------------------------------------------------------------------------------ airfield
def airfield_hangar():
    a = Asset("Site_Airfield_Hangar", "Site", kind="building", voxel=VB_, note="Quonset hangar, axis along x, open toward the strip (-x), man door in the back wall (+x)")
    sh, de = a.p("Shell_Scrap", "Scrap"), a.p("Detail", "Scrap")
    R, Lh = 35 * VB_, 45
    x0, x1 = L(-Lh), H(Lh)
    n = 36
    for k in range(n):                                     # corrugated barrel vault (ribs along the arc run along x)
        a0, a1 = k / n * math.pi, (k + 1) / n * math.pi
        p0 = Vector((0, math.sin(a0) * R, math.cos(a0) * R))
        p1 = Vector((0, math.sin(a1) * R, math.cos(a1) * R))
        mid = (p0 + p1) / 2
        ang = math.degrees(math.atan2(p1.y - p0.y, p1.z - p0.z))
        width = (p1 - p0).length
        mt = Matrix.Translation(mid) @ K.rot(-ang, 0, 0) @ Matrix(((0, 0, 1, 0), (0, 1, 0, 0), (-1, 0, 0, 0), (0, 0, 0, 1)))
        K.corrugated(sh, "tin_rust" if (k // 3) % 2 else "tin", -width / 2 - 0.01, width / 2 + 0.01, x0, x1, 0.03, 0.2, 0.008, mt, samples_per_period=4)
    for xi in range(-Lh, Lh + 1, 15):                      # ribs
        pts = [(math.sin(k / 24 * math.pi) * (R - 0.08), math.cos(k / 24 * math.pi) * (R - 0.08)) for k in range(25)]
        for (q0, q1) in zip(pts, pts[1:]):
            de.beam("black", (C(xi), q0[0], q0[1]), (C(xi), q1[0], q1[1]), 0.16, 0.12, bevel=0.0)
    door = (L(-3), H(3), 0.0, H(10))
    for (za, zb, ya, yb) in K.rect_cells(-R, R, 0.0, R, [door]):        # back wall (half disc) with the man door
        ymax = math.sqrt(max(0.0, R * R - max(za * za, zb * zb)))
        if ymax <= ya:
            continue
        sh.box2("tin_v" if int(za * 2) % 2 else "tin_rust_v", (C(Lh) - 0.04, ya, za), (C(Lh) + 0.04, min(yb, ymax), zb), bevel=0.0)
    de.box2("steel_dark", (C(Lh) - 0.06, H(10), L(-4)), (C(Lh) + 0.06, H(11), H(4)), bevel=0.01)
    de.box(K.wood("", "y"), (0.05, H(10), 1.2), (C(Lh) - 0.4, H(10) / 2, C(3) + 0.3), r=(0, 70, 0), bevel=0.01)
    return a


def airfield_radio_hut():
    a = Asset("Site_Airfield_RadioHut", "Site", kind="building", voxel=VB_, note="concrete radio hut (13 x 13 voxels), door to the strip (-x), window, roof mast")
    sh, de, g = a.p("Shell_Concrete", "Concrete"), a.p("Detail", "Iron"), a.p("Glass")
    X0, X1, Z0, Z1 = L(0), H(12), L(0), H(12)
    for (u0, u1, v0, v1) in K.rect_cells(Z0, Z1, L(0), H(12), [(L(5), H(8), L(0), H(9))]):          # door wall at x0
        sh.box2("white", (X0, v0, u0), (X0 + VB_, v1, u1), bevel=0.0)
    sh.box2("white", (X1 - VB_, L(0), Z0), (X1, H(12), Z1), bevel=0.0)
    for (u0, u1, v0, v1) in K.rect_cells(X0 + VB_, X1 - VB_, L(0), H(12), [(L(4), H(8), L(6), H(9))]):   # window wall at z0
        sh.box2("white", (u0, v0, Z0), (u1, v1, Z0 + VB_), bevel=0.0)
    sh.box2("white", (X0 + VB_, L(0), Z1 - VB_), (X1 - VB_, H(12), Z1), bevel=0.0)
    sh.box2("conc_light", (X0 - 0.06, L(13), Z0 - 0.06), (X1 + 0.06, H(13), Z1 + 0.06), bevel=0.02)
    g.box2("glass", (L(4), L(6), C(0) - 0.01), (H(8), H(9), C(0) + 0.01), bevel=0)
    de.box2("steel_dark", (L(4) - 0.04, L(6) - 0.04, C(0) - 0.08), (H(8) + 0.04, L(6), C(0) + 0.02), bevel=0.0)
    de.cyl("steel", 0.04, H(30) - L(14), (C(10), (H(30) + L(14)) / 2, C(10)), "y", 10, 0.003)
    de.rod("steel", (C(8), C(28), C(10)), (C(12), C(28), C(10)), 0.02, 6)
    for k in range(3):
        an = k * math.tau / 3
        de.rod("steel_dark", (C(10), C(26), C(10)), (C(10) + math.cos(an) * 1.0, H(13), C(10) + math.sin(an) * 1.0), 0.005, 3)
    de.box2("white", (C(2), H(13), C(2)), (C(4), H(13) + 0.3, C(4)), bevel=0.02)
    return a


def airfield_windsock():
    a = Asset("Site_Airfield_Windsock", "Site", kind="kit", voxel=VB_)
    fe, cl = a.p("Iron"), a.p("Cloth")
    fe.cyl("steel", 0.045, H(26) - L(0), (0, (H(26) + L(0)) / 2, 0), "y", 10, 0.004)
    fe.torus("steel", 0.22, 0.015, (0, C(25), C(0.6)), "z", 16, 4)
    secs = []
    for k in range(9):
        t = k / 8
        rr = 0.22 * (1 - 0.6 * t)
        cz = C(0.6) + t * 1.8
        cy = C(25) - t * 0.5
        secs.append([(math.cos(q / 12 * math.tau) * rr, cy + math.sin(q / 12 * math.tau) * rr, cz) for q in range(12)])
    for k in range(8):
        cl.loft("cloth_ochre" if (k // 2) % 2 == 0 else "cloth_cream", [secs[k], secs[k + 1]], close=False)
    return a


def airfield_drums():
    a = Asset("Site_Airfield_Drums", "Site", kind="kit", voxel=VB_)
    s = a.p("Scrap")
    for i in range(4):
        dx, dz = -(i % 2) * 4 * VB_, (i // 2) * 4 * VB_
        m = "pump_red" if i % 2 == 0 else "rig_green"
        s.lathe(m, [(0.0, 0.0), (0.33, 0.0), (0.34, 0.3), (0.33, 0.32), (0.34, 0.62), (0.33, 0.9), (0.0, 0.9)], (dx, L(0), dz), "y", 20)
        s.cyl("steel_dark", 0.05, 0.02, (dx + 0.15, L(0) + 0.91, dz), "y", 10, 0.002)
    return a


TILES = [("site_bunker_walls", "Bunker kit: wall, outer wall (pipe run), doorway, entry, pillar", [lambda: [bunker_wall("Site_Bunker_Wall", "inner"), bunker_wall("Site_Bunker_WallOuter", "outer"),
                                                                                                     bunker_wall("Site_Bunker_Doorway", "doorway"), bunker_wall("Site_Bunker_Entry", "entry"), bunker_pillar()]], 3),
         ("site_bunker_roofs", "Bunker kit: roof cells (green, sand, fallout sod), lamp, vent, hatch", [lambda: [bunker_roof("Site_Bunker_Roof_Green", "leaf"), bunker_roof("Site_Bunker_Roof_Sand", "sand"),
                                                                                                             bunker_roof("Site_Bunker_Roof_Fallout", "scrub"), bunker_lamp(), bunker_surface("Site_Bunker_Vent", False), bunker_surface("Site_Bunker_Hatch", True)]], 3),
         ("site_bunker_entrance", "Bunker kit: ramp wall, sandbags, blast door, rubble", [lambda: [bunker_ramp_wall(), bunker_sandbags(), bunker_blast_door(), bunker_rubble()]], 4),
         ("site_tunnel", "Rock tunnel kit: wall, wall with iron / copper / coal seams, arch, portal, boulders", [lambda: [tunnel_wall("Site_Tunnel_Wall", None), tunnel_wall("Site_Tunnel_Wall_Iron", "IronOre"),
                                                                                                                         tunnel_wall("Site_Tunnel_Wall_Copper", "CopperOre"), tunnel_wall("Site_Tunnel_Wall_Coal", "Coal"),
                                                                                                                         tunnel_arch(), tunnel_portal(), tunnel_boulders()]], 4),
         ("site_airfield_hangar", "Airfield: Quonset hangar (18 x 14 m)", [lambda: [airfield_hangar()]], 1),
         ("site_airfield_misc", "Airfield: radio hut, windsock, fuel drums", [lambda: [airfield_radio_hut(), airfield_windsock(), airfield_drums()]], 3)]

if __name__ == "__main__":
    K.run_tiles(TILES, "world", HERE, "sites_")
