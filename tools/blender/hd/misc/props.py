"""HD world props (BiomeProps.cs, PropLibrary.cs): buildings, roadside objects, a wreck shell, vegetation, rocks.

blender -b -P props.py -- <out_dir>
Origin = bottom centre (as the voxel templates), game axes (doors on the template's -Z side = Blender +Y). Buildings
are shown rotated 180 deg about Z in the preview so the street side faces the camera (root rotation only).
Every building is split in Shell (walls/slabs, carvable), Glass, Detail (frames, signs, junk) so the destruction
bake can voxelise the Shell (see the report)."""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit  # noqa: E402
import comps  # noqa: E402
from kit import V, Part, Vector, Matrix  # noqa: E402

M = {}


def extra(M):
    M["brick"] = kit.textured("Brick", "brick", kit._hex("823e28"), kit._hex("6e3422"), kit._hex("9a9080"), dust=0.35, rough=0.9, bump=0.6)
    M["brick_dark"] = kit.textured("BrickSoot", "brick", kit._hex("5a2a1c"), kit._hex("6e3422"), kit._hex("7a7064"), dust=0.3, rough=0.9, bump=0.6)
    M["conc"] = kit.textured("ConcreteWall", "concrete", kit._hex("6e6b66"), kit._hex("807c76"), kit._hex("908c86"), dust=0.4, rough=0.92, bump=0.6)
    M["conc_dark"] = kit.textured("ConcreteSlab", "concrete", kit._hex("5c5a56"), kit._hex("6e6b66"), dust=0.3, rough=0.92, bump=0.4)
    M["siding"] = kit.textured("FarmSiding", "planks", kit.P("wood", 2), kit.P("wood", 3), scale=1.0, dust=0.4, rough=0.85, bump=0.6, rot="Z")
    M["floor_planks"] = kit.textured("FloorPlanks", "planks", kit.P("wood", 1), kit.P("wood", 2), scale=1.0, dust=0.3, rough=0.85, bump=0.5, rot="X")
    M["roof_red"] = kit.textured("RoofRed", "corrugated", kit._hex("742e20"), kit._hex("8a3828"), kit.P("rust", 1), scale=1.0, dust=0.2, rough=0.65, bump=0.6, rot="X")
    M["tin"] = kit.textured("TinSheet", "corrugated", kit.P("metal", 2), kit.P("metal", 3), kit.P("rust", 2), scale=1.0, dust=0.3, rough=0.6, bump=0.6, rot="X")
    M["tin_rust"] = kit.textured("TinRust", "corrugated", kit.P("rust", 2), kit.P("rust", 3), kit.P("metal", 2), scale=1.0, dust=0.3, rough=0.75, bump=0.6, rot="X")
    M["awning"] = kit.textured("AwningStripes", "planks", kit._hex("8a2a1a"), kit.P("cream", 2), scale=1.0, dust=0.2, rough=0.9, bump=0.1, rot="X")
    M["sign_red"] = kit.surface("SignRed", kit._hex("8a2a1a"), chips=0.5, chip_col=kit.P("metal", 2), rust=0.3, dust=0.3, rough=0.5, streaks=0.5)
    M["pump"] = kit.surface("PumpCream", kit.P("cream", 3), chips=0.45, chip_col=kit.P("rust", 2), rust=0.3, dust=0.4, rough=0.45, streaks=0.4)
    M["pump_red"] = kit.surface("PumpRed", kit._hex("b02818"), chips=0.4, rust=0.25, dust=0.3, rough=0.4)
    M["wreck"] = kit.surface("WreckPaint", kit.P("paleblue", 1), chips=0.6, chip_col=kit.P("rust", 2), rust=0.75, dust=0.5, rough=0.7, var=0.25, streaks=0.7, bump=0.6)
    M["bark"] = kit.textured("Bark", "bark", kit.P("wood", 0), kit.P("wood", 1), kit.P("wood", 2), dust=0.3, rough=0.95, bump=1.0)
    M["deadwood"] = kit.textured("DeadWood", "bark", kit.P("rust", 0), kit._hex("6a5a4a"), kit._hex("8a7a68"), dust=0.4, rough=0.95, bump=1.0)
    M["pine"] = kit.textured("PineNeedles", "leaf", kit._hex("1e3a24"), kit._hex("2e5434"), kit._hex("46703e"), dust=0.25, rough=0.85, bump=0.8)
    M["scrub"] = kit.textured("Scrub", "leaf", kit._hex("4a5224"), kit._hex("6a6a30"), kit._hex("8a7a40"), dust=0.4, rough=0.9, bump=0.8)
    M["cactus"] = kit.textured("Cactus", "leaf", kit._hex("2e5a2a"), kit._hex("3e7034"), kit._hex("5a8a3e"), dust=0.4, rough=0.6, bump=0.6)
    M["rock"] = kit.textured("RockSand", "stone", kit.P("rust", 2), kit.P("sand", 2), kit.P("sand", 3), dust=0.3, rough=0.9, bump=0.8)
    M["rock_grey"] = kit.textured("RockGrey", "stone", kit._hex("5c5a56"), kit._hex("807c76"), kit._hex("908c86"), dust=0.4, rough=0.9, bump=0.8)
    M["ore_rust"] = kit.surface("OreIron", kit.P("rust", 3), dust=0.1, metal=0.3, rough=0.6, var=0.3, bump=0.8)
    M["ore_glow"] = kit.emissive("OreUranium", kit._hex("9cff3a"), 2.0)
    M["ooze"] = kit.emissive("Ooze", kit._hex("9cff3a"), 1.5)


# ------------------------------------------------------------------------------------------ building kit
class Wall:
    """An axis-aligned wall from a to b (Blender XY), outward normal n, built in bays so openings and ruin bites
    are cheap. Openings: (u0, u1, v0, v1) in metres along the wall / up from z0."""

    def __init__(self, a, b, n):
        self.a, self.b, self.n = Vector(a), Vector(b), Vector(n)
        self.d = (self.b - self.a).normalized()
        self.L = (self.b - self.a).length

    def pt(self, u, z, off=0.0):
        return self.a + self.d * u + self.n * off + Vector((0, 0, z))

    def slab(self, part, mat, u0, u1, z0, z1, t, off=0.0, bevel=0.0):
        if u1 - u0 < 1e-3 or z1 - z0 < 1e-3:
            return
        c = self.pt((u0 + u1) / 2, (z0 + z1) / 2, off)
        sx = (u1 - u0) if abs(self.d.x) > 0.5 else t
        sy = (u1 - u0) if abs(self.d.y) > 0.5 else t
        part.box(mat, (sx, sy, z1 - z0), c, bevel=bevel)

    def build(self, shell, mat, z0, z1, t, holes=(), keep=None, jag=None):
        """keep(u, z) -> False drops a piece (ruins); jag(u) lowers the top edge."""
        cuts = sorted({0.0, self.L} | {h[i] for h in holes for i in (0, 1)})
        for u0, u1 in zip(cuts, cuts[1:]):
            hs = sorted([h for h in holes if h[0] <= u0 + 1e-6 and h[1] >= u1 - 1e-6], key=lambda h: h[2])
            lo = z0
            spans = []
            for h in hs:
                spans.append((lo, z0 + h[2]))
                lo = z0 + h[3]
            spans.append((lo, z1))
            for va, vb in spans:
                if keep and not keep((u0 + u1) / 2, (va + vb) / 2):
                    continue
                if jag and vb >= z1 - 1e-6:
                    vb = z1 - jag((u0 + u1) / 2)
                self.slab(shell, mat, u0, u1, va, vb, t, -t / 2, bevel=0.0)


def windows(wall, shell, glass, detail, z0, holes, rnd, frame=None, broken=0.35):
    for (u0, u1, v0, v1) in holes:
        if v0 < 0.05:  # doorway: frame only
            for u in (u0, u1):
                wall.slab(detail, frame or M["wood_dark"], u - 0.05, u + 0.05, z0, z0 + v1, 0.06, 0.02)
            wall.slab(detail, frame or M["wood_dark"], u0 - 0.05, u1 + 0.05, z0 + v1, z0 + v1 + 0.08, 0.06, 0.02)
            continue
        wall.slab(detail, M["conc_dark"], u0 - 0.06, u1 + 0.06, z0 + v0 - 0.05, z0 + v0, 0.08, 0.05)  # sill
        fm = frame or M["wood"]
        for u in (u0 + 0.025, u1 - 0.025, (u0 + u1) / 2):
            wall.slab(detail, fm, u - 0.025, u + 0.025, z0 + v0, z0 + v1, 0.05, -0.06)
        for v in (v0 + 0.025, v1 - 0.025, (v0 + v1) / 2):
            wall.slab(detail, fm, u0, u1, z0 + v - 0.025, z0 + v + 0.025, 0.05, -0.06)
        if rnd.random() > broken:
            wall.slab(glass, M["glass"], u0, u1, z0 + v0, z0 + v1, 0.01, -0.07, bevel=0)
        else:
            if rnd.random() < 0.5:  # boarded up
                for k in range(3):
                    c = wall.pt((u0 + u1) / 2, z0 + v0 + (k + 0.5) * (v1 - v0) / 3, 0.03)
                    detail.box(M["wood_light"], (abs(wall.d.x) * (u1 - u0 + 0.1) + abs(wall.d.y) * 0.025, abs(wall.d.y) * (u1 - u0 + 0.1) + abs(wall.d.x) * 0.025, 0.12),
                               c, bevel=0.006)


def bay_holes(L, z_sill, h, w, spacing, door=None):
    """Window openings every `spacing` m along a wall of length L (centred), skipping a door (u_centre, width, height)."""
    n = max(1, int((L - 0.6) / spacing))
    holes = []
    start = (L - (n - 1) * spacing) / 2
    for i in range(n):
        c = start + i * spacing
        if door and abs(c - door[0]) < door[1] / 2 + w / 2 + 0.25:
            continue
        holes.append((c - w / 2, c + w / 2, z_sill, z_sill + h))
    if door:
        holes.append((door[0] - door[1] / 2, door[0] + door[1] / 2, 0.0, door[2]))
    return sorted(holes)


def box_walls(hw, hl):
    """Four walls of a rectangle (half sizes), each with outward normal; ordered front(-Y), right, back(+Y), left."""
    return [Wall((-hw, -hl, 0), (hw, -hl, 0), (0, -1, 0)), Wall((hw, -hl, 0), (hw, hl, 0), (1, 0, 0)),
            Wall((hw, hl, 0), (-hw, hl, 0), (0, 1, 0)), Wall((-hw, hl, 0), (-hw, -hl, 0), (-1, 0, 0))]


def rubble(p, rnd, centre, spread, n, mat, size=0.25):
    for i in range(n):
        a = rnd.random() * math.tau
        r = spread * math.sqrt(rnd.random())
        s = size * (0.4 + rnd.random())
        q = Vector(centre) + Vector((math.cos(a) * r, math.sin(a) * r, s * 0.25))
        p.add(kit.bm_rock(s, rnd.randint(0, 999), 0.55, 1), mat, q, (0, 0, rnd.random() * 6))


def finish(root_name, parts, yaw=math.pi):
    root = kit.empty(root_name)
    for p in parts:
        if p.faces:
            p.build(root)
    root.rotation_euler = (0, 0, yaw)
    return root


# ------------------------------------------------------------------------------------------ buildings
def brick_house():
    """BrickHouse0: 7.84 x 6.56 m footprint, two 2.9 m storeys, flat roof with a parapet (6.24 m)."""
    rnd = random.Random(11)
    shell, gl, det = Part("Shell"), Part("Glass"), Part("Detail")
    hw, hl = 3.92, 3.28
    st = 3.04
    shell.box(M["conc_dark"], (hw * 2 + 0.2, hl * 2 + 0.2, 0.16), (0, 0, 0.08), bevel=0.01)
    bite = lambda u, z, wi: not (wi == 3 and u < 1.9 and z > 4.3)  # a bitten corner upstairs
    for wi, w in enumerate(box_walls(hw, hl)):
        for f in range(2):
            z0 = 0.16 + f * st
            door = (w.L / 2, 1.12, 2.24) if (wi == 2 and f == 0) else None
            holes = bay_holes(w.L, 0.95, 1.25, 0.86, 1.6, door)
            w.build(shell, M["brick"], z0, z0 + st - 0.16, 0.24, holes, keep=lambda u, z, wi=wi: bite(u, z, wi),
                    jag=(lambda u: 0.0) if f == 0 else (lambda u, wi=wi: (0.5 + 0.4 * math.sin(u * 5)) if (wi == 3 and u < 2.5) else 0.0))
            windows(w, shell, gl, det, z0, [h for h in holes if not (wi == 3 and f == 1 and h[1] < 2.6)], rnd, M["wood"])
            w.slab(det, M["conc_dark"], 0, w.L, z0 + st - 0.2, z0 + st - 0.04, 0.3, -0.06)  # string course
    for f in range(1, 3):
        shell.box(M["conc_dark"], (hw * 2, hl * 2, 0.16), (0, 0, f * st + 0.08), bevel=0.01)
    for w in box_walls(hw, hl):  # parapet + coping
        w.slab(shell, M["brick"], 0, w.L, 2 * st + 0.16, 2 * st + 0.62, 0.24, -0.12)
        w.slab(det, M["conc_dark"], -0.06, w.L + 0.06, 2 * st + 0.62, 2 * st + 0.7, 0.32, -0.12)
    # stairs inside (visible through the door), striped awning, water tank + vent on the roof, gutter pipe
    for i in range(14):
        shell.box(M["wood"], (0.9, 0.26, 0.04), (hw / 2, 1.6 - i * 0.22, 0.2 + i * 0.21), bevel=0.005)
    det.box(M["awning"], (1.6, 0.9, 0.04), (0, hl + 0.48, 2.5), (-0.25, 0, 0), bevel=0.01)
    for s in (-1, 1):
        det.tube(M["steel"], [(s * 0.78, hl + 0.02, 2.2), (s * 0.78, hl + 0.88, 2.4)], 0.012, 6)
    det.cyl(M["olive"], 0.5, 1.0, (-1.8, -1.0, 2 * st + 0.75), "Z", 24, bevel=0.02)
    for s in (-1, 1):
        for t in (-1, 1):
            det.box(M["steel_dark"], (0.05, 0.05, 0.5), (-1.8 + s * 0.35, -1.0 + t * 0.35, 2 * st + 0.4), bevel=0.005)
    det.box(M["steel"], (0.5, 0.5, 0.4), (1.6, -1.6, 2 * st + 0.4), bevel=0.02)
    det.tube(M["rust"], [(hw + 0.08, hl - 0.2, 2 * st + 0.6), (hw + 0.08, hl - 0.2, 0.2)], 0.04, 8)
    det.tube(M["black"], [(hw + 0.02, -1.0, 4.0), (hw + 0.4, -1.4, 6.6), (hw + 0.5, -2.0, 6.9)], 0.008, 4, caps=False)
    rubble(det, rnd, (-hw - 0.8, 1.9, 0), 1.0, 26, M["brick"], 0.16)
    rubble(det, rnd, (-hw - 0.5, -2.0, 0), 0.6, 8, M["conc_dark"], 0.12)
    return finish("BrickHouse", [shell, gl, det])


def farmhouse():
    """Farmhouse0: 6.56 x 5.28 m, 2.88 m plank walls, gable roof along X (red corrugated), brick chimney."""
    rnd = random.Random(3)
    shell, gl, det = Part("Shell"), Part("Glass"), Part("Detail")
    hw, hl, h = 3.28, 2.64, 2.88
    shell.box(M["floor_planks"], (hw * 2 + 0.3, hl * 2 + 0.3, 0.2), (0, 0, 0.1), bevel=0.01)
    for s in (-1, 1):
        for t in (-1, 1):
            det.box(M["rock_grey"], (0.3, 0.3, 0.2), (s * hw, t * hl, 0.0), bevel=0.03)
    for wi, w in enumerate(box_walls(hw, hl)):
        door = (w.L / 2, 1.12, 2.24) if wi == 2 else None
        holes = bay_holes(w.L, 0.9, 1.2, 0.8, 1.6 if wi % 2 == 0 else 2.2, door)
        w.build(shell, M["siding"], 0.2, h, 0.12, holes)
        windows(w, shell, gl, det, 0.2, holes, rnd, M["cream"], 0.3)
        for u in (0.0, w.L):
            w.slab(det, M["cream"], u - 0.06, u + 0.06, 0.2, h, 0.14, 0.0)  # corner boards
    rise = 1.8
    for s in (-1, 1):  # gable ends
        tri = kit.bm_loft([[(s * hw + dx, y, z) for (y, z) in ((-hl, h), (hl, h), (0, h + rise))] for dx in (-0.06, 0.06)])
        shell.add(tri, M["siding"])
    for s in (-1, 1):  # roof planes
        ang = math.atan2(rise, hl + 0.3)
        length = math.hypot(hl + 0.35, rise + 0.2)
        shell.add(kit.bm_box(hw * 2 + 0.5, length, 0.04, bevel=0), M["roof_red"], (0, s * (hl + 0.35) / 2 - s * 0.02, h + rise / 2 + 0.05), (s * ang * -1 if s > 0 else ang, 0, 0))
    det.box(M["roof_red"], (hw * 2 + 0.5, 0.2, 0.08), (0, 0, h + rise + 0.08), bevel=0.02)
    det.box(M["brick_dark"], (0.5, 0.5, 2.8), (hw - 0.9, 0.9, h + 1.0), bevel=0.02)
    det.box(M["conc_dark"], (0.58, 0.58, 0.08), (hw - 0.9, 0.9, h + 2.42), bevel=0.01)
    # porch on the door side: deck, posts, lean-to roof, steps, chair
    det.box(M["floor_planks"], (3.0, 1.4, 0.12), (0, hl + 0.75, 0.16), bevel=0.01)
    for x in (-1.4, 1.4):
        det.box(M["wood"], (0.12, 0.12, 2.3), (x, hl + 1.38, 1.3), bevel=0.01)
    det.add(kit.bm_box(3.3, 1.6, 0.04, bevel=0), M["tin_rust"], (0, hl + 0.8, 2.55), (0.18, 0, 0))
    for i in range(2):
        det.box(M["wood"], (1.0, 0.3, 0.06), (0, hl + 1.6 + i * 0.28, 0.1 - i * 0.05), bevel=0.006)
    det.box(M["wood_light"], (0.45, 0.45, 0.05), (1.0, hl + 0.6, 0.62), bevel=0.01)
    det.box(M["wood_light"], (0.45, 0.05, 0.5), (1.0, hl + 0.38, 0.88), bevel=0.01)
    det.cyl(M["rust"], 0.28, 0.85, (-hw - 0.5, hl - 0.4, 0.43), "Z", 20, bevel=0.02)  # rain barrel
    det.tube(M["rust"], [(-hw - 0.05, hl - 0.4, h - 0.1), (-hw - 0.45, hl - 0.4, h - 0.4), (-hw - 0.5, hl - 0.4, 0.9)], 0.035, 8)
    return finish("Farmhouse", [shell, gl, det])


def shack():
    """Shack0: 2.64 x 2.96 m scrap shack, 2.7 m corrugated walls on timber posts, slanted tin roof."""
    rnd = random.Random(5)
    shell, gl, det = Part("Shell"), Part("Glass"), Part("Detail")
    hw, hl, h = 1.32, 1.48, 2.72
    shell.box(M["floor_planks"], (hw * 2, hl * 2, 0.08), (0, 0, 0.04), bevel=0.005)
    mats = [M["tin"], M["tin_rust"], M["tin"], M["tin_rust"]]
    for wi, w in enumerate(box_walls(hw, hl)):
        holes = []
        if wi == 2:
            holes = [(w.L / 2 - 0.44, w.L / 2 + 0.44, 0.0, 2.1)]
        if wi in (1, 3):
            holes = [(w.L / 2 - 0.36, w.L / 2 + 0.36, 1.12, 1.76)]
        # sheets: alternating panels, slightly skewed
        cuts = [0, w.L * 0.33, w.L * 0.66, w.L]
        for k in range(3):
            sub = Wall(w.pt(cuts[k], 0), w.pt(cuts[k + 1], 0), w.n)
            hh = [(a - cuts[k], b - cuts[k], c, d) for (a, b, c, d) in holes if a < cuts[k + 1] and b > cuts[k]]
            hh = [(max(0, a), min(sub.L, b), c, d) for (a, b, c, d) in hh]
            sub.build(shell, mats[(wi + k) % 4], 0.08, h - (k % 2) * 0.05, 0.03, hh)
        windows(w, shell, gl, det, 0.0, holes, rnd, M["wood_dark"], 0.6)
    for s in (-1, 1):
        for t in (-1, 1):
            det.box(M["wood_dark"], (0.1, 0.1, h + 0.2 - s * 0.24), (s * (hw - 0.05), t * (hl - 0.05), (h + 0.2 - s * 0.24) / 2), bevel=0.01)
    ang = math.atan2(0.5, hw * 2 + 0.3)
    shell.add(kit.bm_box(hw * 2 + 0.5, hl * 2 + 0.4, 0.03, bevel=0), M["tin_rust"], (0, 0, h + 0.27), (0, ang, 0))
    for y in (-hl, 0, hl):
        det.box(M["wood_dark"], (hw * 2 + 0.3, 0.08, 0.08), (0, y, h + 0.2), (0, ang, 0), bevel=0.006)
    for k in range(5):  # weights on the roof
        det.add(kit.bm_rock(0.12, k, 0.6, 1), M["rock_grey"], (rnd.uniform(-1, 1), rnd.uniform(-1.2, 1.2), h + 0.3 + 0.0), (0, ang, 0))
    det.tube(M["black"], [(-0.6, -0.8, h - 0.4), (-0.6, -0.8, h + 1.2)], 0.06, 10)
    det.box(M["canvas"], (0.9, 0.04, 2.0), (0.2, hl + 0.06, 1.05), (0, 0, 0.1), bevel=0.01)  # curtain door
    det.box(M["wood"], (0.6, 0.4, 0.4), (hw + 0.4, -0.6, 0.2), bevel=0.01)
    return finish("Shack", [shell, gl, det])


def shop():
    """Shop0: 12.2 x 9 m concrete store (3.4 m), storefront with big windows, sign board to 4.6 m, shelves."""
    rnd = random.Random(7)
    shell, gl, det = Part("Shell"), Part("Glass"), Part("Detail")
    hw, hl, h = 6.1, 4.5, 3.4
    shell.box(M["conc_dark"], (hw * 2 + 0.2, hl * 2 + 0.2, 0.2), (0, 0, 0.1), bevel=0.01)
    walls = box_walls(hw, hl)
    for wi, w in enumerate(walls):
        if wi == 2:  # storefront (template -Z)
            holes = [(0.4 + k * 1.6, 0.4 + k * 1.6 + 1.5, 0.6, 2.5) for k in range(7) if k != 3] + [(0.4 + 3 * 1.6, 0.4 + 3 * 1.6 + 1.5, 0.0, 2.5)]
        else:
            holes = bay_holes(w.L, 1.6, 0.7, 1.2, 3.0) if wi != 0 else [(w.L * 0.75, w.L * 0.75 + 1.0, 0.0, 2.2)]
        w.build(shell, M["conc"], 0.2, h, 0.25, sorted(holes))
        windows(w, shell, gl, det, 0.2, sorted(holes), rnd, M["steel_dark"], 0.45)
    shell.box(M["conc_dark"], (hw * 2 + 0.4, hl * 2 + 0.4, 0.22), (0, 0, h + 0.11), bevel=0.02)
    # sign board on posts + letters (blocky shapes), awning frame
    det.box(M["sign_red"], (8.0, 0.12, 1.0), (0, hl + 0.3, h + 0.75), bevel=0.02)
    for k in range(9):
        if k == 6:
            continue
        det.box(M["cream"], (0.5, 0.04, 0.6), (-3.4 + k * 0.85, hl + 0.38, h + 0.75), bevel=0.01)
    for x in (-3.6, 3.6):
        det.box(M["steel_dark"], (0.08, 0.3, 1.1), (x, hl + 0.15, h + 0.6), bevel=0.01)
    det.add(kit.bm_box(hw * 2 - 0.6, 1.2, 0.04, bevel=0), M["tin"], (0, hl + 0.62, 2.85), (-0.2, 0, 0))
    for x in (-5.4, -1.8, 1.8, 5.4):
        det.tube(M["steel"], [(x, hl + 1.2, 2.75), (x, hl + 1.2, 0.2)], 0.035, 8)
    for y in (-2.0, 0.0, 2.0):  # shelves
        for z in (0.5, 1.1, 1.7):
            det.box(M["wood"], (6.0, 0.4, 0.04), (-0.6, y, z), bevel=0.005)
        for x in (-3.6, -0.6, 2.4):
            det.box(M["steel_dark"], (0.04, 0.4, 1.8), (x, y, 0.95), bevel=0.004)
        for k in range(10):
            det.box(rnd.choice([M["crimson"], M["olive"], M["cream"], M["navy"]]), (0.22, 0.2, 0.26), (-3.3 + k * 0.6 + rnd.uniform(-0.1, 0.1), y, 1.25), bevel=0.02)
    det.box(M["wood"], (2.0, 0.6, 1.0), (4.4, -3.0, 0.7), bevel=0.02)  # counter
    det.box(M["steel"], (0.4, 0.3, 0.3), (4.2, -3.0, 1.35), bevel=0.02)
    det.box(M["steel"], (1.0, 1.0, 0.6), (-4.0, -2.6, h + 0.52), bevel=0.04)  # AC unit
    rubble(det, rnd, (2.4, hl + 1.0, 0), 1.0, 10, M["glass"], 0.06)
    return finish("Shop", [shell, gl, det])


def ruined_tower():
    """Tower0: 10.6 x 9 m concrete block, 3 storeys of 3.2 m (9.6 m), a corner bitten out of the top, rubble."""
    rnd = random.Random(13)
    shell, gl, det = Part("Shell"), Part("Glass"), Part("Detail")
    hw, hl, st = 5.3, 4.5, 3.2
    shell.box(M["conc_dark"], (hw * 2, hl * 2, 0.2), (0, 0, 0.1), bevel=0.01)
    # the bite: x < 0.6, y < 0 above 3.6 m (top two floors on the front-left), ragged
    def gone(x, y, z):
        return x < 0.4 + math.sin(z * 2.1) * 0.6 and y > -0.5 + math.cos(x * 1.7) * 0.5 and z > 3.6 + math.sin(x * 2.3 + y) * 0.9
    for wi, w in enumerate(box_walls(hw, hl)):
        for f in range(3):
            z0 = 0.2 + f * st
            door = (w.L / 2, 1.6, 2.4) if (wi == 2 and f == 0) else None
            holes = bay_holes(w.L, 0.9, 1.3, 1.3, 2.1, door)
            def keep(u, z, w=w):
                p = w.pt(u, z)
                return not gone(p.x, p.y, z)
            w.build(shell, M["conc"], z0, z0 + st - 0.2, 0.3, holes, keep=keep)
            hh = [hh for hh in holes if keep((hh[0] + hh[1]) / 2, z0 + hh[2] + 0.4)]
            windows(w, shell, gl, det, z0, hh, rnd, M["steel_dark"], 0.7)
    for f in range(1, 4):  # slabs, clipped by the bite in a grid of cells
        z = f * st + 0.1
        n = 8
        for i in range(n):
            for j in range(n):
                x0, x1 = -hw + i * hw * 2 / n, -hw + (i + 1) * hw * 2 / n
                y0, y1 = -hl + j * hl * 2 / n, -hl + (j + 1) * hl * 2 / n
                if gone((x0 + x1) / 2, (y0 + y1) / 2, z + 0.3):
                    continue
                shell.box(M["conc_dark"], (x1 - x0, y1 - y0, 0.2), ((x0 + x1) / 2, (y0 + y1) / 2, z), bevel=0.0)
    for i in range(6):  # rebar sticking out of the broken edge
        x = -0.2 + rnd.uniform(-0.5, 0.5)
        y = 0.4 + rnd.uniform(-1.5, 3.0)
        det.tube(M["rust"], [(x, y, st * 2 + 0.1), (x - 0.5 - rnd.random() * 0.4, y - 0.2, st * 2 + 0.3 + rnd.random() * 0.4)], 0.012, 5)
    for i in range(14):  # stairwell flight seen through the hole
        det.box(M["conc"], (1.0, 0.28, 0.2), (2.6, -1.6 + i * 0.25, 0.3 + i * 0.22), bevel=0.01)
    rubble(det, rnd, (-hw - 1.2, hl + 0.6, 0), 2.2, 40, M["conc"], 0.35)
    rubble(det, rnd, (-2.4, 1.5, st + 0.3), 1.6, 18, M["conc"], 0.25)
    det.tube(M["black"], [(hw + 0.02, -3.0, 9.2), (hw + 0.02, -3.0, 0.4)], 0.03, 6)
    return finish("Tower", [shell, gl, det])


# ------------------------------------------------------------------------------------------ roadside props
def fuel_pump():
    p = Part("GasPump")
    p.box(M["conc"], (0.88, 0.72, 0.16), (0, 0, 0.08), bevel=0.02)
    p.box(M["pump"], (0.56, 0.4, 1.12), (0, 0, 0.72), bevel=0.04)
    p.box(M["pump_red"], (0.58, 0.42, 0.24), (0, 0, 1.4), bevel=0.03)
    p.add(kit.superloft([(y, 0, 1.58, 0.29, 0.1) for y in (-0.2, 0.2)], 24, 2.0), M["pump"])
    p.box(M["black"], (0.36, 0.02, 0.26), (0, -0.205, 1.08), bevel=0.01)
    p.box(M["glass"], (0.32, 0.02, 0.22), (0, -0.212, 1.08), bevel=0.005)
    for k in range(3):
        p.box(M["cream"], (0.07, 0.01, 0.05), (-0.1 + k * 0.1, -0.222, 1.1), bevel=0.003)
    p.box(M["black"], (0.12, 0.16, 0.26), (0.34, -0.02, 0.8), bevel=0.02)
    p.box(M["steel"], (0.05, 0.16, 0.06), (0.36, -0.12, 0.88), (0.6, 0, 0), bevel=0.01)
    p.tube(M["black"], [(0.3, 0.1, 0.84), (0.45, 0.2, 0.5), (0.48, 0.15, 0.18), (0.42, -0.2, 0.16), (0.4, -0.18, 0.7)], 0.018, 8)
    p.box(M["cream"], (0.3, 0.01, 0.12), (0, -0.205, 1.42), bevel=0.003)
    return p.build()


def streetlight():
    p = Part("Streetlight")
    p.cyl(M["conc"], 0.22, 0.4, (0, 0, 0.2), "Z", 16, bevel=0.02)
    p.cyl(M["steel"], 0.085, 4.6, (0, 0, 2.5), "Z", 16, bevel=0.0, r2=0.055)
    p.tube(M["steel"], [(0, 0, 4.6), (0, 0, 4.75), (0, -0.15, 4.85), (0, -0.82, 4.86)], 0.045, 12)
    p.add(kit.superloft([(y, 0, 4.82, w, 0.07) for y, w in ((-0.62, 0.08), (-0.75, 0.14), (-0.95, 0.14), (-1.02, 0.08))], 20, 2.4), M["steel_dark"])
    p.box(M["lamp"], (0.18, 0.28, 0.02), (0, -0.84, 4.75), bevel=0.01)
    p.box(M["steel_dark"], (0.22, 0.14, 0.32), (0, 0.11, 2.6), bevel=0.02)
    p.tube(M["black"], [(0.04, 0.12, 2.8), (0.04, 0.1, 4.5)], 0.008, 4)
    return p.build()


def crate():
    p = Part("Crate")
    s = 0.56
    for z in range(4):
        for k, (sx, sy, x, y) in enumerate(((s, 0.02, 0, -s / 2), (s, 0.02, 0, s / 2), (0.02, s, -s / 2, 0), (0.02, s, s / 2, 0))):
            p.box(M["wood_light"] if (z + k) % 2 else M["wood"], (sx, sy, 0.13), (x, y, 0.07 + z * 0.14), bevel=0.004)
    p.box(M["wood"], (s - 0.02, s - 0.02, 0.02), (0, 0, s), bevel=0.003)
    for x in (-1, 1):
        for y in (-1, 1):
            p.box(M["steel"], (0.06, 0.06, s + 0.02), (x * (s / 2 - 0.01), y * (s / 2 - 0.01), s / 2), bevel=0.006)
    p.box(M["hazard"], (0.16, 0.01, 0.06), (0, -s / 2 - 0.012, s / 2), bevel=0.002)
    p.box(M["wood_dark"], (0.6, 0.06, 0.06), (0, -s / 2 - 0.02, s / 2), (0, math.radians(40), 0), bevel=0.005)
    return p.build()


def barrel():
    p = Part("Barrel")
    prof = [(0.0, 0.0), (0.26, 0.0), (0.265, 0.02), (0.26, 0.04), (0.265, 0.26), (0.275, 0.27), (0.265, 0.28), (0.265, 0.52), (0.275, 0.53), (0.265, 0.54),
            (0.265, 0.76), (0.26, 0.78), (0.265, 0.8), (0.24, 0.8), (0.24, 0.78), (0.0, 0.78)]
    p.lathe(M["rust"], prof, (0, 0, 0), "Z", 32)
    p.lathe(M["hazard"], [(0.268, 0.33), (0.268, 0.45)], (0, 0, 0), "Z", 32)
    for k in range(3):
        a = k / 3 * math.tau
        p.box(M["black"], (0.06, 0.02, 0.1), (math.cos(a) * 0.275, math.sin(a) * 0.275, 0.39), (0, 0, a + math.pi / 2), bevel=0.003)
    p.add(kit.bm_rock(0.1, 3, 0.4, 2), M["ooze"], (0.04, 0.02, 0.79))
    p.tube(M["ooze"], [(0.24, -0.05, 0.79), (0.27, -0.05, 0.7), (0.272, -0.06, 0.5)], [0.02, 0.016, 0.008], 6)
    p.cyl(M["steel"], 0.03, 0.02, (-0.12, 0.08, 0.79), "Z", 10)
    return p.build()


def roadside():
    objs = [fuel_pump(), streetlight(), crate(), barrel()]
    kit.grid(objs, 4, 0.5)
    return objs


def wreck():
    """Wreck shell (what Ruin() leaves of a sedan): rusted body, no wheels, sitting on its brake drums, smashed
    glass, hood sprung, doors gone on one side. 4.6 x 1.8 x 1.35 m."""
    root = kit.empty("WreckShell")
    rnd = random.Random(9)
    b = Part("Body")
    L, W = 4.6, 1.8
    st = []
    for y, zt, zb, hw in ((-2.3, 0.62, 0.32, 0.8), (-2.2, 0.78, 0.26, 0.86), (-1.4, 0.86, 0.24, 0.9), (-0.6, 0.92, 0.24, 0.9), (0.9, 0.92, 0.24, 0.9), (1.9, 0.9, 0.26, 0.88), (2.3, 0.82, 0.34, 0.82)):
        st.append((y, 0, (zt + zb) / 2, hw, (zt - zb) / 2))
    b.add(kit.superloft(st, 32, 3.4), M["wreck"])
    # cabin: pillars + roof (caved), no glass
    roof = kit.superloft([(y, 0, z, hw, 0.025) for y, z, hw in ((-0.55, 1.22, 0.62), (-0.3, 1.3, 0.7), (0.6, 1.26, 0.72), (1.05, 1.24, 0.66))], 24, 3.0)
    b.add(roof, M["wreck"])
    for s in (-1, 1):
        b.tube(M["wreck"], [(s * 0.86, -0.75, 0.92), (s * 0.66, -0.45, 1.25)], 0.04, 8)
        b.tube(M["wreck"], [(s * 0.88, 0.15, 0.92), (s * 0.72, 0.15, 1.28)], 0.035, 8)
        b.tube(M["wreck"], [(s * 0.86, 1.4, 0.92), (s * 0.66, 1.05, 1.24)], 0.04, 8)
    b.box(M["black"], (1.6, 2.2, 0.4), (0, 0.3, 0.75), bevel=0.04)  # gutted interior
    b.box(M["rust"], (0.7, 0.5, 0.5), (0.35, 0.6, 0.85), bevel=0.06)  # seat frame
    b.box(M["rust"], (0.7, 0.5, 0.5), (-0.35, -0.2, 0.85), bevel=0.06)
    b.add(kit.bm_box(1.6, 1.1, 0.03, bevel=0.0), M["wreck"], (0, -1.95, 1.05), (0.9, 0, 0.12))  # sprung hood
    b.box(M["rust"], (1.4, 0.8, 0.4), (0, -1.75, 0.55), bevel=0.04)  # engine bay junk
    for s in (-1, 1):
        for y in (-1.4, 1.45):
            b.cyl(M["rust"], 0.17, 0.12, (s * 0.72, y, 0.17), "X", 20, bevel=0.01)  # brake drums on the ground
            b.add(kit.bm_cyl(0.36, 0.3, 24, 0.0, None, False), M["black"], (s * 0.78, y, 0.3), (0, math.pi / 2, 0))  # arch shadow
    b.box(M["rust"], (1.8, 0.1, 0.16), (0, -2.38, 0.36), bevel=0.02)  # bumper
    b.box(M["rust"], (1.7, 0.12, 0.14), (0.05, 2.36, 0.4), (0, 0.08, 0), bevel=0.02)
    for s in (-1, 1):
        b.cyl(M["black"], 0.07, 0.02, (s * 0.6, -2.36, 0.62), "Y", 14)  # empty lamp sockets
    for i in range(14):  # glass shards on the ground
        b.add(kit.bm_box(0.06, 0.04, 0.004, bevel=0), M["glass"], (rnd.uniform(-1.4, 1.4), rnd.uniform(-1.8, 1.8), 0.005), (0, 0, rnd.random() * 6))
    b.build(root)
    door = Part("Door_L")  # one door lies on the ground
    door.add(kit.bm_box(1.0, 0.06, 0.62, bevel=0.02), M["wreck"])
    d = door.build(root)
    d.location = (-1.5, 0.4, 0.04)
    d.rotation_euler = (math.radians(88), 0, math.radians(12))
    return root


def tree_pine(seed=0):
    rnd = random.Random(seed)
    p = Part("Pine")
    h = 5.76
    p.tube(M["bark"], [(0, 0, -0.1), (0.02, 0.0, h * 0.35), (0, 0.03, h * 0.98)], [0.17, 0.12, 0.03], 12)
    for k in range(5):  # roots
        a = k / 5 * math.tau + 0.3
        p.tube(M["bark"], [(0, 0, 0.25), (math.cos(a) * 0.35, math.sin(a) * 0.35, 0.02)], [0.07, 0.03], 6)
    tiers = 7
    for t in range(tiers):
        f = t / tiers
        z = h * (0.22 + 0.75 * f)
        r = (1 - f) * h * 0.2 * 1.15 + 0.15
        cone = kit.bm_rock(1.0, seed * 10 + t, 1.0, 2)
        for v in cone.verts:
            q = v.co
            v.co = Vector((q.x * r, q.y * r, (q.z * 0.45 + 0.1) * r * 0.9 - abs(q.x * q.y) * 0.0))
            if v.co.z > 0:
                v.co.x *= max(0.15, 1 - v.co.z / (r * 0.6))
                v.co.y *= max(0.15, 1 - v.co.z / (r * 0.6))
        p.add(cone, M["pine"], (rnd.uniform(-0.05, 0.05), rnd.uniform(-0.05, 0.05), z), (0, 0, rnd.random() * 6))
    return p.build()


def tree_dead(seed=3):
    rnd = random.Random(seed)
    p = Part("DeadTree")
    h = 2.1
    p.tube(M["deadwood"], [(0, 0, -0.05), (0.03, 0.02, h * 0.5), (-0.02, 0.05, h)], [0.1, 0.07, 0.025], 10)
    for i in range(5):
        y = h * (0.42 + 0.12 * i)
        a = rnd.random() * math.tau
        ln = h * (0.25 + 0.3 * rnd.random())
        s = Vector((0, 0, y))
        e = s + Vector((math.cos(a) * ln, math.sin(a) * ln * 0.6, ln * 0.8))
        e2 = e + Vector((math.cos(a + 0.8) * ln * 0.4, math.sin(a + 0.8) * ln * 0.4, ln * 0.3))
        p.tube(M["deadwood"], [s, s.lerp(e, 0.5) + Vector((0, 0, 0.05)), e, e2], [0.045, 0.03, 0.015, 0.004], 6)
    return p.build()


def cactus(seed=1):
    p = Part("Cactus")
    h = 2.2

    def column(base, top, r):
        prof = [(r * 0.8, 0.0), (r, 0.05)] + [(r, (top - 0.05) * k / 4) for k in range(1, 4)] + [(r * 0.9, top - r * 0.4), (r * 0.5, top - r * 0.1), (0.0, top)]
        bm = kit.bm_lathe(prof, 14)
        for v in bm.verts:  # ribs
            ang = math.atan2(v.co.y, v.co.x)
            f = 1 + 0.08 * math.cos(ang * 7)
            v.co.x *= f
            v.co.y *= f
        p.add(bm, M["cactus"], base)

    column((0, 0, 0), h, 0.2)
    p.tube(M["cactus"], [(0, 0, h * 0.45), (0.38, 0, h * 0.45), (0.52, 0, h * 0.55)], 0.13, 12)
    column((0.52, 0, h * 0.5), h * 0.35, 0.13)
    p.tube(M["cactus"], [(0, 0, h * 0.62), (-0.32, 0.02, h * 0.62), (-0.42, 0.02, h * 0.68)], 0.11, 12)
    column((-0.42, 0.02, h * 0.64), h * 0.22, 0.11)
    for k in range(3):
        p.sphere(M["pump_red"], 0.035, (0.04 * k - 0.04, 0.05, h + 0.01), 8, 6)
    return p.build()


def bush(seed=1):
    rnd = random.Random(seed)
    p = Part("Bush")
    for k in range(7):
        a = k / 7 * math.tau
        r = 0.25 + rnd.random() * 0.2
        c = Vector((math.cos(a) * 0.3, math.sin(a) * 0.3, 0.25 + rnd.random() * 0.15))
        p.add(kit.bm_rock(r, seed * 7 + k, 0.8, 2), M["scrub"], c)
    p.add(kit.bm_rock(0.4, seed, 0.9, 2), M["scrub"], (0, 0, 0.45))
    for k in range(6):
        a = k / 6 * math.tau
        p.tube(M["deadwood"], [(0, 0, 0), (math.cos(a) * 0.4, math.sin(a) * 0.4, 0.4)], [0.025, 0.008], 5)
    return p.build()


def vegetation():
    objs = [tree_pine(), tree_dead(), cactus(), bush()]
    kit.grid(objs, 4, 0.4)
    return objs


def outcrop():
    """Ore outcrop (Ore_IronOre0): 2.5 x 1.1 x 2.3 m lump of stone shot through with ore veins; plus loose rocks."""
    p = Part("Ore_IronOre0")
    rk = kit.bm_rock(1.0, 4, 1.0, 4)
    for v in rk.verts:
        v.co = Vector((v.co.x * 1.25, v.co.y * 1.15, max(-0.05, v.co.z * 1.1)))
    p.add(rk, M["rock_grey"], (0, 0, 0))
    for k in range(5):  # veins: thin lens-shaped slabs breaking the surface
        a = k * 1.3
        ve = kit.bm_rock(0.5, 20 + k, 0.12, 2)
        p.add(ve, M["ore_rust"], (math.cos(a) * 0.8, math.sin(a) * 0.7, 0.35 + 0.12 * (k % 3)), (0.4 * (k % 2), 0.3, a))
    for k in range(6):
        a = k * 1.1 + 0.5
        p.add(kit.bm_rock(0.18 + 0.06 * (k % 3), 40 + k, 0.7, 2), M["ore_rust"] if k % 2 else M["rock_grey"], (math.cos(a) * 1.6, math.sin(a) * 1.4, 0.05))
    o = p.build()
    q = Part("Rock2")
    q.add(kit.bm_rock(0.6, 7, 0.7, 3), M["rock"], (0, 0, 0.15))
    q.add(kit.bm_rock(0.3, 8, 0.7, 2), M["rock"], (0.6, 0.3, 0.05))
    r = q.build()
    u = Part("Ore_UraniumOre0")
    rk = kit.bm_rock(0.8, 11, 0.9, 3)
    for v in rk.verts:
        v.co.z = max(-0.04, v.co.z)
    u.add(rk, M["rock_grey"])
    for k in range(4):
        u.add(kit.bm_rock(0.25, 50 + k, 0.2, 1), M["ore_glow"], (math.cos(k * 1.6) * 0.6, math.sin(k * 1.6) * 0.55, 0.3), (0.5, 0.2, k))
    g = u.build()
    objs = [o, r, g]
    kit.grid(objs, 3, 0.4)
    return objs


if __name__ == "__main__":
    kit.run_group([(brick_house, "prop_brickhouse", "Brick house (2 storeys, bitten corner)", "BrickHouse0/1"),
                   (farmhouse, "prop_farmhouse", "Farmhouse (plank walls, red tin gable)", "Farmhouse0/1"),
                   (shack, "prop_shack", "Scrap shack (tin sheets on posts)", "Shack0-2"),
                   (shop, "prop_shop", "Shop (storefront, sign, shelves)", "Shop0/1"),
                   (ruined_tower, "prop_tower", "Ruined concrete tower", "Tower0-3"),
                   (roadside, "prop_roadside", "Fuel pump, street light, crate, waste barrel", "GasPump0, Streetlight0, Crate, Barrel0"),
                   (wreck, "prop_wreck", "Wrecked car shell", "Ruin() wreck of a car prefab"),
                   (vegetation, "prop_vegetation", "Pine, dead tree, cactus, scrub bush", "Pine0-2, Tree0-3, Cactus0/1, Bush0-2"),
                   (outcrop, "prop_rocks", "Ore outcrops (iron, uranium) + boulder", "Ore_*, Rock0-3")], M, extra)
