"""HD vegetation, rocks, ore outcrops and haystack (BiomeProps.cs, PropLibrary.cs templates). Sizes come from the voxel
templates (ref_index.json bounds), origin = the template origin (trunk foot / bottom centre). Vegetation carries the
"Sway" colour attribute (0 at the base -> 1 at the tips) for the PixelVoxel wind sway.

blender -b -P vegetation.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hdkit as K  # noqa: E402
from hdkit import Asset, Vector, Matrix  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))


def box_of(gid):
    """Template bounds in metres: (x0, y0, z0), (x1, y1, z1)."""
    r = K.REF[gid]
    v = r["voxel"]
    return tuple((a - 0.5) * v for a in r["lo"]), tuple((b + 0.5) * v for b in r["hi"]), v


def sway_height(h, power=1.5, floor=0.0):
    return lambda p: max(0.0, min(1.0, (max(0.0, p.y - floor) / max(h - floor, 0.01)) ** power))


# ------------------------------------------------------------------------------------------ trees
def pine(i):
    gid = "Pine%d" % i
    a = Asset(gid, "Vegetation", kind="vegetation", voxel=0.16)
    lo, hi, v = box_of(gid)
    H, R = hi[1], hi[0]
    tr, nd = a.p("Trunk", "Wood"), a.p("Needles", "Wood")
    tr.lathe("bark", [(0.0, 0.0), (0.2, 0.0), (0.16, 0.12), (0.13, H * 0.35), (0.06, H * 0.8), (0.0, H * 0.82)], (0, 0, 0), "y", 10)
    for k in range(5):                                     # surface roots
        an = k / 5 * math.tau + i
        tr.tube("bark", [(0, 0.08, 0), (math.cos(an) * 0.32, 0.0, math.sin(an) * 0.32)], [0.07, 0.02], 5)
    r = K.rng(31 + i)
    tiers = 5 + i
    for t in range(tiers):                                 # stacked, drooping needle tiers with ragged rims
        f0 = t / tiers
        yb = H * (0.2 + 0.72 * f0)
        rr = R * 1.06 * (1.0 - f0 * 0.82) * (0.98 + 0.04 * r.random())
        ht = H * 0.26 * (1 - f0 * 0.35)
        n = 14
        rim = []
        for k in range(n):
            an = k / n * math.tau + t * 0.37
            q = rr * (0.82 + 0.18 * r.random())
            rim.append((math.cos(an) * q, yb - 0.08 - 0.08 * r.random(), math.sin(an) * q))
        tip = (r.uniform(-0.03, 0.03), yb + ht, r.uniform(-0.03, 0.03))
        mid = [(x * 0.55, (y + tip[1]) * 0.5 - 0.05, z * 0.55) for x, y, z in rim]
        bm = K.kit.bm_loft([rim, mid, [tip] * n], close_ends=True)
        nd.add(bm, "pine")
    nd.sway = lambda p: max(0.0, min(1.0, (p.y / H) ** 1.4 + 0.25 * (math.hypot(p.x, p.z) / R)))
    tr.sway = sway_height(H, 2.0)
    a.ref = gid
    return a


def palm(i):
    gid = "Palm%d" % i
    a = Asset(gid, "Vegetation", kind="vegetation", voxel=0.12)
    v = 0.12
    h = 44 + i * 6
    lean = 0.25 if i == 0 else -0.2
    tr, fr = a.p("Trunk", "Wood"), a.p("Fronds", "Wood")
    pts, radii = [], []
    for k in range(11):
        t = k / 10
        pts.append((lean * h * t * t * v, h * t * v, 0.0))
        radii.append((1.4 - t * 0.6) * v)
    tr.tube("palm_bark", pts, radii, 10)
    for k in range(1, 10):                                 # leaf-scar rings
        tr.torus("palm_bark", radii[k] + 0.006, 0.012, pts[k], "y", 10, 4)
    top = Vector(pts[-1])
    for k in range(3):
        tr.sphere("wood_dark_x", 0.07, tuple(top + Vector(((k - 1) * 0.1, -0.2, (k % 2) * 0.08))), (1, 1, 1), 10, 6)
    for k in range(8):                                     # fronds: arching rachis with drooping leaflets
        an = k / 9 * math.tau + i
        d = Vector((math.cos(an), 0, math.sin(an)))
        side = Vector((-d.z, 0, d.x))
        spine = []
        for s in range(8):
            u = s / 7
            spine.append(top + d * (u * h * v * 0.24) + Vector((0, h * v * 0.07 * math.sin(u * math.pi) - u * u * h * v * 0.14, 0)))
        fr.tube("palm_leaf", spine, [0.02 * (1 - u * 0.7) for u in [s / 7 for s in range(8)]], 5)
        left, right = [], []
        for s in range(1, 8, 1):
            u = s / 7
            wl = 0.22 * math.sin(u * math.pi) + 0.04
            p = spine[s]
            droop = Vector((0, -0.12 - 0.1 * u, 0))
            left.append((p, p + side * wl + droop))
            right.append((p, p - side * wl + droop))
        for half in (left, right):
            for (p0, q0), (p1, q1) in zip(half, half[1:]):
                fr.quad("palm_leaf", p0, p1, q1, q0, 0.006)
    H = h * v
    tr.sway = sway_height(H, 2.0)
    fr.sway = lambda p: max(0.0, min(1.0, 0.55 + 0.45 * ((p - top).length / (h * v * 0.24))))
    return a


def bush(i):
    gid = "Bush%d" % i
    a = Asset(gid, "Vegetation", kind="vegetation", voxel=0.1)
    lo, hi, v = box_of(gid)
    mat = ["pine", "scrub", "leaf"][i]
    br, lf = a.p("Twigs", "Wood"), a.p("Leaves", "Wood")
    r = K.rng(77 + i)
    R = (hi[0] - lo[0]) / 2
    H = hi[1]
    for k in range(6):                                     # woody stems
        an = k / 6 * math.tau + r.random()
        br.tube("deadwood", [(0, 0, 0), (math.cos(an) * R * 0.3, H * 0.4, math.sin(an) * R * 0.3), (math.cos(an) * R * 0.55, H * 0.7, math.sin(an) * R * 0.55)], [0.025, 0.015, 0.006], 5)
    n = 10 + i * 4
    for k in range(n):                                     # leaf clumps hugging an ellipsoid
        an = r.uniform(0, math.tau)
        el = r.uniform(0.0, 1.0)
        q = R * (0.35 + 0.45 * r.random())
        cx, cz = math.cos(an) * q * (1 - el * 0.4), math.sin(an) * q * (1 - el * 0.4)
        cy = 0.06 + el * (H - 0.16)
        rad = R * r.uniform(0.28, 0.42)
        lf.rock(mat, rad, (cx, cy, cz), seed=k + i * 31, squash=0.75, detail=1, yaw=r.uniform(0, 360))
    lf.sway = lambda p: max(0.0, min(1.0, 0.3 + 0.7 * p.y / H))
    br.sway = sway_height(H, 1.5)
    return a


def fern():
    a = Asset("Fern0", "Vegetation", kind="vegetation", voxel=0.08)
    f = a.p("Fronds", "Wood")
    v = 0.08
    for k in range(6):
        an = k / 6 * math.tau
        end = Vector((math.cos(an) * 9 * v, 5 * v, math.sin(an) * 9 * v * 0.55))
        mid = end * 0.6 + Vector((0, 4 * v, 0))
        spine = [Vector((0, 0, 0)), mid * 0.5 + Vector((0, 0.05, 0)), mid, (mid + end) / 2 + Vector((0, 0.02, 0)), end]
        f.tube("fern", spine, [0.012, 0.01, 0.008, 0.006, 0.003], 4)
        d = (end - Vector((0, 0, 0))).normalized()
        side = Vector((-d.z, 0, d.x)).normalized()
        for s in range(1, 9):                              # leaflets along the outer half
            u = s / 9
            p = spine[1] + (end - spine[1]) * u if u > 0.15 else spine[1]
            p = Vector(p)
            L = 0.11 * math.sin(u * math.pi) + 0.02
            for sg in (-1, 1):
                q = p + side * sg * L + Vector((0, -0.02, 0)) + d * 0.03
                f.tri_plate("fern", p, q, p + d * 0.05, 0.004)
    f.sway = lambda p: max(0.0, min(1.0, math.hypot(p.x, p.z) / 0.72))
    return a


def cactus(i):
    gid = "Cactus%d" % i
    a = Asset(gid, "Vegetation", kind="vegetation", voxel=0.1)
    c_ = a.p("Cactus", "Wood")
    v = 0.1
    h = 22 + i * 6

    def column(cx, cz, y0, y1, r, cap=True):
        secs = []
        n = 20
        ys = [y0 + (y1 - y0) * k / 8 for k in range(9)]
        for j, y in enumerate(ys):
            rr = r * (1.0 if j < 7 else (0.85 if j == 7 else 0.55))
            secs.append([(cx + math.cos(t / n * math.tau) * rr * (1 + 0.09 * math.cos(t * math.pi)), y, cz + math.sin(t / n * math.tau) * rr * (1 + 0.09 * math.cos(t * math.pi))) for t in range(n)])
        secs.append([(cx, y1 + r * 0.25, cz)] * n)
        c_.loft("cactus", secs)
    column(0, 0, 0, h * v, 2.6 * v)
    side = 1 if i == 0 else -1
    yA = h * 0.45 * v
    c_.tube("cactus", [(0, yA, 0), (side * 3 * v, yA - 0.04, 0), (side * 6 * v, yA + 0.1, 0)], 1.6 * v, 12)
    column(side * 6 * v, 0, yA, h * 0.75 * v, 1.8 * v)
    if i == 1:
        yB = h * 0.6 * v
        c_.tube("cactus", [(0, yB, 0), (2.5 * v, yB - 0.03, 0), (5 * v, yB + 0.08, 0)], 1.5 * v, 12)
        column(5 * v, 0, yB, h * 0.85 * v, 1.6 * v)
        for k in range(5):
            an = k / 5 * math.tau
            c_.sphere("flower_yellow", 0.035, (math.cos(an) * 0.08, h * v + 0.03, math.sin(an) * 0.08), (1, 0.6, 1), 8, 5)
    c_.sway = sway_height(h * v * 1.1, 2.0)
    return a


def log():
    a = Asset("Log0", "Vegetation", kind="vegetation", voxel=0.1)
    w = a.p("Wood")
    v = 0.1
    R = 2.6 * v + 0.03
    w.cyl("bark", R, 29 * v - 0.02, (0, 2.5 * v, 0), "x", 16, 0.02)
    for x in (-14.5 * v + 0.006, 14.5 * v - 0.006):
        w.cyl("wood_light_x", R - 0.02, 0.012, (x, 2.5 * v, 0), "x", 16, 0.0)
        for k in range(3):
            w.torus("wood_dark_x", 0.05 + k * 0.07, 0.006, (x + (0.006 if x > 0 else -0.006), 2.5 * v, 0), "x", 14, 3)
    w.tube("bark", [(0.4, 2.5 * v + 0.2, 0.05), (0.55, 2.5 * v + 0.45, 0.12)], [0.05, 0.02], 6)
    w.tube("bark", [(-0.6, 2.5 * v + 0.15, -0.1), (-0.8, 2.5 * v + 0.3, -0.22)], [0.04, 0.015], 6)
    w.rock("leaf", 0.12, (-0.2, 2.5 * v + R - 0.02, 0.02), seed=4, squash=0.3, detail=1, scale=(2.2, 1, 1))
    w.sway = lambda p: 0.0
    return a


def dead_tree(i):
    gid = "Tree%d" % i
    a = Asset(gid, "Vegetation", kind="vegetation", voxel=0.08)
    w = a.p("Wood")
    lo, hi, v = box_of(gid)
    H = hi[1]
    r = K.rng(i * 7 + 3)
    w.tube("deadwood", [(0, 0, 0), (0.01, H * 0.5, 0.0), (r.uniform(-0.03, 0.03), H, r.uniform(-0.03, 0.03))], [0.05, 0.03, 0.008], 7)
    for k in range(5):
        y = H * (0.45 + 0.1 * k)
        an = r.uniform(0, math.tau)
        L = H * (0.06 + 0.06 * r.random())
        s = Vector((0, y, 0))
        e = s + Vector((math.cos(an) * L * 0.6, L * 0.8, math.sin(an) * L * 0.4))
        w.tube("deadwood", [s, (s + e) / 2 + Vector((0, 0.02, 0)), e], [0.018, 0.012, 0.004], 5)
        e2 = e + Vector((math.cos(an + 0.8) * L * 0.3, L * 0.3, 0))
        w.tube("deadwood", [e, e2], [0.006, 0.002], 4)
    w.sway = sway_height(H, 2.0)
    return a


# ------------------------------------------------------------------------------------------ rocks, ores, hay
def rock(i):
    gid = "Rock%d" % i
    a = Asset(gid, "Prop", kind="prop", voxel=0.12)
    s = a.p("Stone")
    lo, hi, v = box_of(gid)
    W, D = hi[0] - lo[0], hi[2] - lo[2]
    s.rock("rock", 0.5, ((lo[0] + hi[0]) / 2, -0.12, (lo[2] + hi[2]) / 2), seed=11 + i, squash=1.0, detail=3,
           scale=(W * 0.92, (hi[1] + 0.12) * 1.6, D * 0.92), floor=lo[1])
    r = K.rng(i)
    for k in range(2 + i):
        an = r.uniform(0, math.tau)
        s.rock("rock", r.uniform(0.05, 0.08), (math.cos(an) * W * 0.42, 0.02, math.sin(an) * D * 0.42), seed=k + 40, squash=0.7, detail=1, floor=lo[1])
    return a


ORES = {"IronOre": "ore_rust", "CopperOre": "ore_copper", "TinOre": "ore_tin", "Bauxite": "ore_bauxite", "Coal": "ore_coal",
        "Sulfur": "ore_sulfur", "LeadOre": "ore_lead", "UraniumOre": "ore_uranium"}


def ore(name, vi):
    gid = "Ore_%s%d" % (name, vi)
    a = Asset(gid, "Prop", kind="prop", voxel=0.12)
    st, ov = a.p("Stone"), a.p(name)
    lo, hi, v = box_of(gid)
    W, H, D = hi[0] - lo[0], hi[1] - lo[1], hi[2] - lo[2]
    cx, cz = (lo[0] + hi[0]) / 2, (lo[2] + hi[2]) / 2
    seed = sum(map(ord, name)) + vi
    st.rock("rock_grey", 0.5, (cx, -0.25, cz), seed=seed, squash=1.0, detail=3, scale=(W * 0.92, (H + 0.25) * 1.55, D * 0.92), floor=lo[1])
    r = K.rng(seed)
    m = ORES[name]
    # veins: bands of crystals / nodules erupting through the surface along a tilted plane
    nrm = Vector((r.uniform(-0.4, 0.4), 1.0, r.uniform(-0.6, 0.6))).normalized()
    for k in range(30 + vi * 12):
        an = r.uniform(0, math.tau)
        el = r.uniform(0.15, 1.0)
        q = Vector((cx + math.cos(an) * W * 0.42 * math.sqrt(1 - el * el * 0.7), H * 0.85 * el, cz + math.sin(an) * D * 0.42 * math.sqrt(1 - el * el * 0.7)))
        if abs(nrm.dot(q - Vector((cx, H * 0.5, cz)))) > 0.22 and k % 3:
            continue
        if name in ("TinOre", "LeadOre", "UraniumOre", "Sulfur") and r.random() < 0.6:
            ov.add(K.kit.bm_cyl(r.uniform(0.03, 0.05), r.uniform(0.1, 0.22), 6, 0.0, 0.004), m,
                   Matrix.Translation(q) @ Vector((r.uniform(-0.5, 0.5), 1, r.uniform(-0.5, 0.5))).normalized().to_track_quat("Z", "Y").to_matrix().to_4x4())
        else:
            ov.rock(m, r.uniform(0.08, 0.17), tuple(q), seed=k + seed, squash=0.6, detail=1, yaw=r.uniform(0, 360), floor=lo[1])
    if name == "UraniumOre":
        g = a.p("Glow", name)
        for k in range(6):
            an = r.uniform(0, math.tau)
            g.sphere("ore_glow", 0.03, (cx + math.cos(an) * W * 0.3, H * r.uniform(0.4, 0.9), cz + math.sin(an) * D * 0.3), (1, 1, 1), 8, 5)
    return a


def haystack():
    a = Asset("Haystack0", "Prop", kind="prop", voxel=0.12)
    h = a.p("Cloth")
    lo, hi, v = box_of("Haystack0")
    R, H = hi[0], hi[1]
    h.lathe("hay", [(0.0, lo[1]), (R * 0.98, lo[1]), (R, 0.15), (R * 0.9, H * 0.45), (R * 0.65, H * 0.78), (R * 0.3, H * 0.96), (0.0, H)], (0, 0, 0), "y", 28)
    r = K.rng(9)
    for k in range(160):                                   # straw strands
        an = r.uniform(0, math.tau)
        t = r.uniform(0.05, 0.95)
        rr = (R * (1 - t ** 1.6)) + 0.005
        p = Vector((math.cos(an) * rr, H * t, math.sin(an) * rr))
        d = Vector((math.cos(an + r.uniform(-1, 1)), r.uniform(-0.6, 0.2), math.sin(an + r.uniform(-1, 1)))).normalized()
        h.rod("hay", p, p + d * r.uniform(0.06, 0.16), 0.005, 3)
    h.torus("rope", R * 0.62, 0.012, (0, H * 0.8, 0), "y", 20, 4)
    return a


TILES = [("world_pines", "Pines (3 heights), dead snags", [lambda: [pine(0), pine(1), pine(2)] + [dead_tree(i) for i in range(4)]], 7),
         ("world_palms", "Palms (2), fern, log", [lambda: [palm(0), palm(1), fern(), log()]], 4),
         ("world_desert_plants", "Bushes (3), cacti (2)", [lambda: [bush(0), bush(1), bush(2), cactus(0), cactus(1)]], 5),
         ("world_rocks", "Boulders Rock0-3, haystack", [lambda: [rock(i) for i in range(4)] + [haystack()]], 5),
         ("world_ores_a", "Ore outcrops: iron, copper, tin, bauxite (2 sizes)", [lambda: [ore(n, v) for n in ("IronOre", "CopperOre", "TinOre", "Bauxite") for v in (0, 1)]], 4),
         ("world_ores_b", "Ore outcrops: coal, sulfur, lead, uranium (2 sizes)", [lambda: [ore(n, v) for n in ("Coal", "Sulfur", "LeadOre", "UraniumOre") for v in (0, 1)]], 4)]

if __name__ == "__main__":
    K.run_tiles(TILES, "world", HERE, "vegetation_")
