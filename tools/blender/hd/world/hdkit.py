"""Shared kit for the HD world / furniture / animal assets (Blender 5, run headless).

Everything is modelled in GAME space: metres, x right, y up, z front (the voxel code's axes), so a voxel box
`g.Box(x0, y0, z0, x1, y1, z1)` of a grid with voxel size `vs` transcribes 1:1 to `p.boxv(mat, x0, y0, z0, x1, y1, z1)`.
`GP.build` converts to Blender with game (x, y, z) -> Blender (-x, -z, y) - the mapping Unity's FBX importer undoes
(Blender -Y forward, Z up; Unity flips X) - and flips the winding because that map is a reflection.

Asset layout (see COVERAGE.md / PIPELINE.md):
  <game id>            root empty; custom props: game_id, category, kind, voxel, ref_dims, materials{object: byte}
    <Material/role>    mesh objects (furniture: one per voxel material byte - Wood, Scrap, Glass, Cloth...;
                       buildings: Shell (voxelised for destruction/collision), Detail, Glass; movers: Rotor, Leaf...)
Each mesh object carries `material_name` / `material_byte` (MadMax.Items.ResourceType) and a smart-UV map "UVMap".
Vegetation carries a colour attribute "Sway" (0 at the base -> 1 at the tips). Lamps use LampWhite/LampAmber/LampRed.
"""
import json
import math
import os
import random
import sys
import time

import bmesh
import bpy
from mathutils import Matrix, Vector

HD = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, HD)
sys.path.insert(0, os.path.join(HD, "misc"))
import render_common as rc  # noqa: E402
import kit  # noqa: E402

PREVIEW = "/home/magix/PycharmProjects/MadMaxUnity/hd_preview"
REF = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "ref_index.json")))

# MadMax.Items.ResourceType (append-only enum)
RES = ["None", "Scrap", "Wood", "Stone", "Glass", "Rubber", "Cloth", "Fuel", "Oil", "Coolant", "Sand", "Clay", "Laterite",
       "Rubble", "Slag", "IronOre", "CopperOre", "TinOre", "Bauxite", "Silica", "Iron", "Copper", "Bronze", "Aluminium",
       "Charcoal", "Asphalt", "Concrete", "Lime", "Water", "DirtyWater", "Ethanol", "Hide", "Leather", "Gunpowder", "Sulfur",
       "CrudeOil", "Diesel", "Tar", "SeedOil", "Coal", "LeadOre", "Lead", "Acid", "UraniumOre", "Gravel", "Steel", "Wool",
       "Honey", "Beeswax", "Hay", "Feed", "SeaWater", "Biogas", "Brick", "Plank", "Thread", "GoldOre", "Gold", "Salt"]

BUDGET = {"furniture": (500, 8000), "building": (2000, 20000), "prop": (300, 8000), "vegetation": (300, 3000),
          "animal": (2000, 12000), "kit": (100, 20000)}

P = kit.P
hexc = kit._hex
M = {}


# ------------------------------------------------------------------------------------------------ materials
def materials():
    M.clear()
    M.update(kit.mats())
    T, S = kit.textured, kit.surface
    M["planks"] = T("WallPlanks", "planks", P("wood", 2), P("wood", 3), dust=0.35, rough=0.85, bump=0.6, rot="X")
    M["planks_v"] = T("PlanksVertical", "planks", P("wood", 2), P("wood", 3), dust=0.4, rough=0.85, bump=0.6, rot="Z")
    M["floor"] = T("FloorBoards", "planks", P("wood", 1), P("wood", 2), dust=0.3, rough=0.85, bump=0.5, rot="X")
    M["brick"] = T("Brick", "brick", hexc("823e28"), hexc("6e3422"), hexc("9a9080"), dust=0.35, rough=0.9, bump=0.6)
    M["brick_dark"] = T("BrickSoot", "brick", hexc("5a2a1c"), hexc("6e3422"), hexc("7a7064"), dust=0.3, rough=0.9, bump=0.6)
    M["brick_fired"] = T("BrickFired", "brick", hexc("9a4a30"), hexc("b05a38"), hexc("a8a090"), dust=0.3, rough=0.9, bump=0.6)
    M["conc"] = S("ConcreteCast", hexc("7a7670"), dust=0.45, rough=0.92, var=0.16, bump=0.45, streaks=0.18, scale=1.4)
    M["conc_dark"] = S("ConcreteDark", hexc("5c5a56"), dust=0.4, rough=0.92, var=0.18, bump=0.4, streaks=0.12, scale=1.4)
    M["conc_light"] = S("ConcreteLight", hexc("948f86"), dust=0.4, rough=0.9, var=0.14, bump=0.35, streaks=0.15, scale=1.4)
    M["conc_paving"] = T("ConcretePaving", "concrete", hexc("6e6b66"), hexc("807c76"), hexc("908c86"), scale=2.5, dust=0.4, rough=0.92, bump=0.6)
    M["stone"] = T("FieldStone", "stone", P("sand", 1), P("sand", 3), hexc("5c5a56"), dust=0.2, rough=0.9, bump=0.8)
    M["rock"] = T("RockSand", "stone", P("rust", 2), P("sand", 2), P("sand", 3), dust=0.3, rough=0.9, bump=0.8)
    M["rock_grey"] = T("RockGrey", "stone", hexc("5c5a56"), hexc("807c76"), hexc("908c86"), dust=0.4, rough=0.9, bump=0.8)
    M["tin"] = T("TinSheet", "corrugated", P("metal", 2), P("metal", 3), P("rust", 2), dust=0.3, rough=0.6, bump=0.6, rot="X")
    M["tin_v"] = T("TinSheetV", "corrugated", P("metal", 2), P("metal", 3), P("rust", 2), dust=0.3, rough=0.6, bump=0.6, rot="Z")
    M["tin_rust"] = T("TinRust", "corrugated", P("rust", 2), P("rust", 3), P("metal", 2), dust=0.3, rough=0.75, bump=0.6, rot="X")
    M["tin_rust_v"] = T("TinRustV", "corrugated", P("rust", 2), P("rust", 3), P("metal", 2), dust=0.3, rough=0.75, bump=0.6, rot="Z")
    M["roof_red"] = T("RoofRed", "corrugated", hexc("742e20"), hexc("8a3828"), P("rust", 1), dust=0.2, rough=0.65, bump=0.6, rot="X")
    M["siding"] = T("FarmSiding", "planks", P("wood", 2), P("wood", 3), dust=0.4, rough=0.85, bump=0.6, rot="Z")
    M["awning"] = T("AwningStripes", "planks", hexc("8a2a1a"), P("cream", 2), dust=0.2, rough=0.9, bump=0.1, rot="X")
    M["sign_red"] = S("SignRed", hexc("8a2a1a"), chips=0.5, chip_col=P("metal", 2), rust=0.3, dust=0.3, rough=0.5, streaks=0.5)
    M["white"] = S("WhitePaint", P("cream", 3), chips=0.35, chip_col=P("metal", 2), rust=0.2, dust=0.35, rough=0.5, streaks=0.3)
    M["enamel"] = S("Enamel", P("cream", 3), chips=0.25, chip_col=P("black", 1), rust=0.05, dust=0.2, rough=0.25, var=0.05)
    M["pump_red"] = S("PumpRed", hexc("b02818"), chips=0.4, rust=0.25, dust=0.3, rough=0.4)
    M["green"] = S("GreenPaint", P("moss", 2), chips=0.4, rust=0.25, dust=0.35, rough=0.55)
    M["blue"] = S("BluePaint", P("paleblue", 1), chips=0.45, chip_col=P("rust", 2), rust=0.3, dust=0.35, rough=0.5)
    M["orange"] = S("OrangePaint", hexc("c8641c"), chips=0.4, rust=0.25, dust=0.35, rough=0.5)
    M["blanket"] = T("ArmyBlanket", "canvas", P("riggreen", 3), P("riggreen", 4), dust=0.1, rough=0.95, bump=0.4)
    M["sheet"] = T("Sheet", "canvas", P("cream", 1), P("cream", 2), dust=0.1, rough=0.95, bump=0.2)
    M["cloth_red"] = T("ClothRed", "canvas", P("crimson", 2), P("crimson", 3), dust=0.2, rough=0.95, bump=0.2)
    M["cloth_blue"] = T("ClothBlue", "canvas", P("navy", 2), P("navy", 3), dust=0.2, rough=0.95, bump=0.2)
    M["cloth_ochre"] = T("ClothOchre", "canvas", P("ochre", 3), P("ochre", 4), dust=0.2, rough=0.95, bump=0.2)
    M["cloth_green"] = T("ClothGreen", "canvas", hexc("5a6a2a"), hexc("6a7a34"), dust=0.2, rough=0.95, bump=0.2)
    M["cloth_olive"] = T("ClothOlive", "canvas", P("olive", 2), P("olive", 3), dust=0.25, rough=0.95, bump=0.35)
    M["cloth_cream"] = T("ClothCream", "canvas", P("cream", 2), P("cream", 3), dust=0.25, rough=0.95, bump=0.25)
    M["cloth_pale"] = T("ClothPaleBlue", "canvas", P("paleblue", 1), P("paleblue", 3), dust=0.15, rough=0.9, bump=0.2)
    M["rig_green"] = S("RigGreenPaint", P("riggreen", 3), chips=0.35, rust=0.25, dust=0.4, rough=0.55)
    M["burlap"] = T("Burlap", "canvas", hexc("8a7448"), hexc("a08a58"), dust=0.3, rough=0.98, bump=0.6)
    M["hay"] = T("Hay", "hay", hexc("a4802e"), hexc("d4ae48"), hexc("8a6a24"), dust=0.2, rough=0.95, bump=0.8) if _has_kind("hay") else S("Hay", hexc("bc9638"), dust=0.2, rough=0.95, var=0.3, bump=0.9)
    M["ash"] = S("Ash", P("black", 2), dust=0.0, rough=0.95, var=0.4, bump=0.8)
    M["soil"] = S("Soil", hexc("4a3020"), dust=0.0, rough=0.98, var=0.35, bump=1.0)
    M["soil_dry"] = S("SoilDry", hexc("7a5a3a"), dust=0.2, rough=0.98, var=0.3, bump=1.0)
    M["sand"] = S("SandFill", P("sand", 3), dust=0.0, rough=0.98, var=0.2, bump=0.6)
    M["gravel"] = T("Gravel", "stone", hexc("6e6b66"), hexc("8a857e"), hexc("4a4846"), scale=3.0, dust=0.2, rough=0.95, bump=1.0)
    M["asphalt"] = T("Asphalt", "concrete", hexc("2a2826"), hexc("34322f"), hexc("3e3b38"), dust=0.2, rough=0.95, bump=0.4)
    M["water"] = kit.glass("Water", tint=hexc("2c4a52"))
    M["ember"] = kit.emissive("Ember", hexc("ff7a20"), 6.0)
    M["flame"] = kit.emissive("Flame", hexc("ffc040"), 8.0)
    M["glow_green"] = kit.emissive("GlowGreen", hexc("9cff3a"), 2.0)
    M["screen"] = kit.emissive("Screen", hexc("3a5a50"), 0.5)
    M["glass_clear"] = kit.glass("GlassClear", tint=P("paleblue", 2))
    M["glass_clear"].node_tree.nodes["Principled BSDF"].inputs["Alpha"].default_value = 0.38
    M["solar"] = T("SolarCells", "corrugated", P("navy", 0), P("navy", 2), P("chrome", 1), scale=3.0, dust=0.2, rough=0.15, bump=0.2, rot="Y")
    M["plaster"] = S("Plaster", P("cream", 1), dust=0.2, rough=0.9, var=0.15, bump=0.4)
    M["pegboard"] = S("Pegboard", P("wood", 3), dust=0.2, rough=0.8, var=0.15, bump=0.3)
    M["gen"] = S("GenOlive", P("olive", 2), chips=0.45, rust=0.3, dust=0.4, rough=0.55, streaks=0.4)
    M["paper"] = S("Paper", P("cream", 3), dust=0.2, rough=0.95, var=0.1, bump=0.2)
    M["clay"] = S("Clay", hexc("a8603c"), dust=0.25, rough=0.9, var=0.2, bump=0.5)
    M["terracotta"] = S("Terracotta", hexc("b0603a"), dust=0.25, rough=0.85, var=0.15, bump=0.3)
    M["bone"] = S("Bone", P("cream", 2), dust=0.3, rough=0.7, var=0.15, bump=0.4)
    M["sandbag"] = T("SandbagCloth", "canvas", hexc("9a8458"), hexc("b09a68"), dust=0.4, rough=0.98, bump=0.6)
    # ores (BiomeProps.OreRamps)
    M["ore_rust"] = S("OreIron", P("rust", 3), dust=0.1, metal=0.3, rough=0.6, var=0.3, bump=0.8)
    M["ore_copper"] = S("OreCopper", P("moss", 3), dust=0.1, metal=0.4, rough=0.5, var=0.3, bump=0.8)
    M["ore_tin"] = S("OreTin", P("chrome", 2), dust=0.1, metal=0.6, rough=0.45, var=0.2, bump=0.8)
    M["ore_bauxite"] = S("OreBauxite", P("ochre", 2), dust=0.1, rough=0.8, var=0.3, bump=0.8)
    M["ore_coal"] = S("OreCoal", P("black", 1), dust=0.05, metal=0.1, rough=0.35, var=0.2, bump=0.9)
    M["ore_sulfur"] = S("OreSulfur", P("ochre", 4), dust=0.05, rough=0.7, var=0.2, bump=0.7)
    M["ore_lead"] = S("OreLead", P("metal", 3), dust=0.1, metal=0.6, rough=0.4, var=0.2, bump=0.8)
    M["ore_uranium"] = S("OreUranium", P("moss", 4), dust=0.1, rough=0.5, var=0.3, bump=0.8)
    M["ore_glow"] = kit.emissive("OreGlow", hexc("9cff3a"), 2.0)
    M["gold"] = S("Gold", P("ochre", 4), metal=1.0, rough=0.25, var=0.1, dust=0.0, bump=0.2)
    # vegetation
    M["bark"] = T("Bark", "bark", P("wood", 0), P("wood", 1), P("wood", 2), dust=0.3, rough=0.95, bump=1.0)
    M["deadwood"] = T("DeadWood", "bark", P("rust", 0), hexc("6a5a4a"), hexc("8a7a68"), dust=0.4, rough=0.95, bump=1.0)
    M["palm_bark"] = T("PalmBark", "bark", hexc("5a4630"), hexc("7a6044"), hexc("94785a"), dust=0.3, rough=0.95, bump=1.0)
    M["pine"] = T("PineNeedles", "leaf", hexc("16241a"), hexc("283e28"), hexc("344e30"), dust=0.25, rough=0.85, bump=0.8)
    M["leaf"] = T("LeafGreen", "leaf", hexc("2a5a1c"), hexc("46862c"), hexc("5a9e36"), dust=0.25, rough=0.8, bump=0.6)
    M["scrub"] = T("Scrub", "leaf", hexc("3a4420"), hexc("5a642e"), hexc("6c7436"), dust=0.4, rough=0.9, bump=0.8)
    M["palm_leaf"] = T("PalmFrond", "leaf", hexc("2e5a24"), hexc("467a30"), hexc("6a8a3c"), dust=0.3, rough=0.8, bump=0.5)
    M["cactus"] = T("Cactus", "leaf", hexc("2e4a26"), hexc("3a5c2e"), hexc("5c8442"), dust=0.4, rough=0.6, bump=0.6)
    M["fern"] = T("Fern", "leaf", hexc("2a5a1c"), hexc("367024"), hexc("5a9e36"), dust=0.2, rough=0.8, bump=0.5)
    for tone, i in (("", 2), ("_light", 3), ("_dark", 1), ("_grey", -1)):
        for ax, bax in (("x", "X"), ("y", "Z"), ("z", "Y")):
            base = hexc("7a6a58") if tone == "_grey" else P("wood", i)
            M["wood%s_%s" % (tone, ax)] = S("Wood%s_%s" % (tone.title().strip("_"), ax.upper()), base, dust=0.35, rough=0.82,
                                              var=0.16, grain=(bax, 4, 0.38 if tone != "_grey" else 0.25))
    M["flower_red"] = S("FlowerRed", hexc("c83020"), dust=0.1, rough=0.7)
    M["flower_yellow"] = S("FlowerYellow", hexc("d4b020"), dust=0.1, rough=0.7)
    return M


def _has_kind(k):
    import inspect
    return ("'%s'" % k) in inspect.getsource(kit.textured)


def mat(key):
    return M[key]


def wood(tone="", axis="x"):
    """Wood with the grain along a game axis: tone '', 'light', 'dark', 'grey'."""
    return "wood%s_%s" % ("_" + tone if tone else "", axis)


WOODS = {ax: [wood("", ax), wood("light", ax), wood("dark", ax)] for ax in "xyz"}


# ------------------------------------------------------------------------------------------------ geometry
C = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))   # game -> Blender (a reflection)


def g2b(v):
    v = Vector(v)
    return Vector((-v.x, -v.z, v.y))


def rot(rx=0.0, ry=0.0, rz=0.0):
    """Game-space rotation (degrees about game x, y, z; applied z, then x, then y like Unity's Euler)."""
    return (Matrix.Rotation(math.radians(ry), 4, "Y") @ Matrix.Rotation(math.radians(rx), 4, "X")
            @ Matrix.Rotation(math.radians(rz), 4, "Z"))


def _axis_mtx(axis):
    """Primitives are made along +Z (bmesh); orient them along game axis 'x' / 'y' / 'z'."""
    if axis == "y":
        return Matrix.Rotation(math.radians(-90), 4, "X")
    if axis == "x":
        return Matrix.Rotation(math.radians(90), 4, "Y")
    return Matrix.Identity(4)


class GP:
    """A mesh object assembled from primitives in game space."""

    def __init__(self, name, byte="Scrap", pivot=(0, 0, 0)):
        self.name, self.byte, self.pivot = name, byte, Vector(pivot)
        self.verts, self.faces, self.fmat, self.mats = [], [], [], []
        self.sway = None   # callable(game_pos) -> weight, for vegetation

    def add(self, bm, m, matrix=None):
        if isinstance(m, str):
            m = M[m]
        if m not in self.mats:
            self.mats.append(m)
        mi = self.mats.index(m)
        matrix = matrix or Matrix.Identity(4)
        base = len(self.verts)
        bm.verts.index_update()
        for v in bm.verts:
            self.verts.append(matrix @ v.co)
        for f in bm.faces:
            self.faces.append(tuple(base + v.index for v in f.verts))
            self.fmat.append(mi)
        bm.free()
        return self

    @staticmethod
    def T(loc=(0, 0, 0), r=None):
        m = Matrix.Translation(Vector(loc))
        if r is not None:
            m = m @ (r if isinstance(r, Matrix) else rot(*r))
        return m

    # boxes -----------------------------------------------------------------------------------------
    def box(self, m, size, c, r=None, bevel=0.006, segs=2):
        return self.add(kit.bm_box(*size, bevel=bevel, segs=segs), m, self.T(c, r))

    def box2(self, m, lo, hi, bevel=0.006, r=None, segs=2):
        lo, hi = Vector(lo), Vector(hi)
        return self.box(m, tuple(hi - lo), tuple((lo + hi) / 2), r, bevel, segs)

    def boxv(self, m, x0, y0, z0, x1, y1, z1, vs=0.08, bevel=None, inset=0.0):
        """Inclusive voxel-index box (like VoxelGrid.Box) in a grid of voxel size vs."""
        x0, x1 = min(x0, x1), max(x0, x1)
        y0, y1 = min(y0, y1), max(y0, y1)
        z0, z1 = min(z0, z1), max(z0, z1)
        lo = Vector(((x0 - 0.5) * vs + inset, (y0 - 0.5) * vs + inset, (z0 - 0.5) * vs + inset))
        hi = Vector(((x1 + 0.5) * vs - inset, (y1 + 0.5) * vs - inset, (z1 + 0.5) * vs - inset))
        b = min(hi - lo) * 0.18 if bevel is None else bevel
        return self.box2(m, lo, hi, bevel=min(b, 0.02))

    # round things ---------------------------------------------------------------------------------
    def cyl(self, m, r, h, c, axis="y", segs=20, bevel=0.004, r2=None, caps=True, r_extra=None):
        bm = kit.bm_cyl(r, h, segs, bevel, r2, caps)
        mt = self.T(c) @ (r_extra if r_extra is not None else Matrix.Identity(4)) @ _axis_mtx(axis)
        return self.add(bm, m, mt)

    def cylv(self, m, axis, ca, cb, r, a0, a1, vs=0.08, segs=20, bevel=0.004, r_in=None):
        """Voxel cylinder like VoxelGrid.CylX/Y/Z: (ca, cb) = the two other coords of the axis, a0..a1 inclusive."""
        h = (a1 - a0 + 1) * vs
        mid = (a0 + a1) / 2 * vs
        ca, cb = ca * vs, cb * vs
        c = {"x": (mid, ca, cb), "y": (ca, mid, cb), "z": (ca, cb, mid)}[axis]
        rr = (r + 0.5) * vs
        if r_in:
            return self.tube_ring(m, rr, (r_in + 0.5) * vs, h, c, axis, segs)
        return self.cyl(m, rr, h, c, axis, segs, bevel)

    def tube_ring(self, m, R, r_in, h, c, axis="y", segs=24):
        prof = [(r_in, -h / 2), (R, -h / 2), (R, h / 2), (r_in, h / 2), (r_in, -h / 2)]
        bm = kit.bm_lathe(prof, segs)
        return self.add(bm, m, self.T(c) @ _axis_mtx(axis))

    def tube(self, m, pts, r, segs=10, caps=True):
        return self.add(kit.bm_tube([Vector(p) for p in pts], r, segs, caps), m)

    def rod(self, m, a, b, r, segs=8, caps=True):
        return self.tube(m, [a, b], r, segs, caps)

    def beam(self, m, a, b, w, t=None, up=(0, 1, 0), bevel=0.004):
        """Square-section member from a to b (w wide, t thick)."""
        a, b = Vector(a), Vector(b)
        d = b - a
        L = d.length
        t = t or w
        q = d.to_track_quat("Z", "Y" if abs(d.normalized().y) < 0.95 else "X")
        return self.add(kit.bm_box(w, t, L, bevel=bevel), m, Matrix.Translation((a + b) / 2) @ q.to_matrix().to_4x4())

    def sphere(self, m, r, c, scale=(1, 1, 1), u=16, v=10):
        return self.add(kit.bm_sphere(r, u, v), m, self.T(c) @ Matrix.Diagonal((*scale, 1)))

    def lathe(self, m, profile, c=(0, 0, 0), axis="y", segs=24, close=True, r=None):
        """profile [(radius, height)] around the game axis."""
        bm = kit.bm_lathe(profile, segs, "Z", close)
        return self.add(bm, m, self.T(c, r) @ _axis_mtx(axis))

    def torus(self, m, R, r, c, axis="y", segs=24, rsegs=8):
        return self.add(kit.bm_torus(R, r, segs, rsegs), m, self.T(c) @ _axis_mtx(axis))

    def rock(self, m, r, c, seed=0, squash=0.7, detail=2, scale=(1, 1, 1), yaw=0.0, floor=None):
        """Lumpy rock / blob; `floor` (game y) flattens everything below it (rocks sitting on the ground)."""
        bm = kit.bm_rock(r, seed, 1.0, detail)
        mt = self.T(c, (0, yaw, 0)) @ _axis_mtx("y") @ Matrix.Diagonal((scale[0], scale[2], scale[1] * squash, 1))
        if floor is None:
            return self.add(bm, m, mt)
        for v in bm.verts:
            w = mt @ v.co
            if w.y < floor:
                w.y = floor
            v.co = w
        return self.add(bm, m)

    def prism(self, m, pts2d, y0, y1, plane="xz"):
        """Extrude a polygon given in the xz plane (game) from y0 to y1 (plane 'xy': polygon in xy, extruded along z)."""
        if plane == "xz":
            secs = [[(x, y0, z) for x, z in pts2d], [(x, y1, z) for x, z in pts2d]]
        elif plane == "xy":
            secs = [[(x, y, y0) for x, y in pts2d], [(x, y, y1) for x, y in pts2d]]
        else:  # zy: polygon (z, y), extruded along x
            secs = [[(y0, y, z) for z, y in pts2d], [(y1, y, z) for z, y in pts2d]]
        return self.add(kit.bm_loft(secs), m)

    def loft(self, m, sections, close=True):
        return self.add(kit.bm_loft(sections, close), m)

    def superloft(self, m, stations, n=20, exp=2.4, axis="z"):
        """Superellipse sections stacked along a game axis: stations [(t, c1, c2, h1, h2)].
        axis z: (z, x, y, half_w, half_h); axis y: (y, x, z, half_w, half_d); axis x: (x, z, y, half_d, half_h)."""
        secs = []
        for (t, c1, c2, h1, h2) in stations:
            h1, h2 = max(h1, 1e-4), max(h2, 1e-4)
            sec = []
            for k in range(n):
                a = k / n * math.tau
                co, si = math.cos(a), math.sin(a)
                u = h1 * math.copysign(abs(co) ** (2 / exp), co)
                w = h2 * math.copysign(abs(si) ** (2 / exp), si)
                sec.append({"z": (c1 + u, c2 + w, t), "y": (c1 + u, t, c2 + w), "x": (t, c2 + w, c1 + u)}[axis])
            secs.append(sec)
        return self.add(kit.bm_loft(secs, True), m)

    def panel(self, m, a, b, c, t=0.02):
        """Quad plate through corners a, b, c (d = a + c - b), thickness t along its normal."""
        a, b, c = Vector(a), Vector(b), Vector(c)
        d = a + c - b
        n = (b - a).cross(c - b).normalized() * (t / 2)
        secs = [[a - n, b - n, c - n, d - n], [a + n, b + n, c + n, d + n]]
        return self.add(kit.bm_loft(secs), m)

    def quad(self, m, a, b, c, d, t=0.02):
        """Plate through four corners (any quad), thickness t along its normal."""
        a, b, c, d = Vector(a), Vector(b), Vector(c), Vector(d)
        n = (c - a).cross(d - b).normalized() * (t / 2)
        return self.add(kit.bm_loft([[a - n, b - n, c - n, d - n], [a + n, b + n, c + n, d + n]]), m)

    def tri_plate(self, m, a, b, c, t=0.02):
        a, b, c = Vector(a), Vector(b), Vector(c)
        n = (b - a).cross(c - a).normalized() * (t / 2)
        return self.add(kit.bm_loft([[a - n, b - n, c - n], [a + n, b + n, c + n]]), m)

    # build ----------------------------------------------------------------------------------------
    def empty(self):
        return not self.faces

    def build(self, parent=None, smooth=True, angle=40.0, name=None):
        name = name or self.name
        verts = [tuple(C @ (v - self.pivot)) for v in self.verts]
        faces = [tuple(reversed(f)) for f in self.faces]
        me = bpy.data.meshes.new(name)
        me.from_pydata(verts, [], faces)
        me.validate(clean_customdata=False)
        for m in self.mats:
            me.materials.append(m)
        me.polygons.foreach_set("material_index", self.fmat[:len(me.polygons)])
        me.update()
        if smooth:
            me.shade_smooth()
            try:
                me.set_sharp_from_angle(angle=math.radians(angle))
            except AttributeError:
                pass
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        ob.location = C @ self.pivot
        if parent is not None:
            ob.parent = parent
        ob["material_name"] = self.byte
        ob["material_byte"] = RES.index(self.byte) if self.byte in RES else 0
        if self.sway is not None:
            ws = [max(0.0, min(1.0, self.sway(v))) for v in self.verts]
            ca = me.color_attributes.new("Sway", "FLOAT_COLOR", "POINT")
            ca.data.foreach_set("color", [c for w in ws for c in (w, w, w, w)])
        return ob


# ------------------------------------------------------------------------------------------------ assets
class Asset:
    """One game asset: root empty named exactly like the game id + sub-objects."""

    def __init__(self, gid, category, kind="furniture", voxel=0.08, ref=None, note="", yaw=0.0):
        self.gid, self.category, self.kind, self.voxel, self.note = gid, category, kind, voxel, note
        self.yaw = yaw          # preview-only turn (degrees about game y) so the interesting side faces the camera
        self.parts = {}
        self.order = []
        self.ref = ref if ref is not None else gid
        self.extra = {}
        self.movers = set()

    def p(self, name, byte=None, pivot=(0, 0, 0), mover=False):
        """Get / create the sub-object `name` (default byte: the name if it is a ResourceType). Movers (rotors, leaves,
        heads posed by the game at their pivot) are left out of the size check against the voxel template."""
        if mover:
            self.movers.add(name)
        if name not in self.parts:
            self.parts[name] = GP(name, byte or (name if name in RES else "Scrap"), pivot)
            self.order.append(name)
        return self.parts[name]

    def build(self):
        root = bpy.data.objects.new(self.gid, None)
        root.empty_display_size = 0.4
        bpy.context.scene.collection.objects.link(root)
        mats = {}
        objs = []
        for n in self.order:
            gp = self.parts[n]
            if gp.empty():
                continue
            ob = gp.build(root, name=n)          # Blender may suffix ".001" in multi-asset files: "part" is authoritative
            ob["part"] = n
            ob["mover"] = n in self.movers
            ob["shell"] = n.startswith("Shell")
            mats[n] = gp.byte
            objs.append(ob)
        root["game_id"] = self.gid
        root["category"] = self.category
        root["kind"] = self.kind
        root["voxel"] = self.voxel
        root["materials"] = json.dumps(mats)
        r = REF.get(self.ref)
        if r:
            root["ref_dims"] = r["dims"]
        for k, v in self.extra.items():
            root[k] = v
        self.root, self.objs = root, objs
        return root


def smart_uv(objs):
    for ob in objs:
        if ob.type != "MESH" or not ob.data.polygons:
            continue
        bpy.ops.object.select_all(action="DESELECT")
        bpy.context.view_layer.objects.active = ob
        ob.select_set(True)
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.01)
        bpy.ops.object.mode_set(mode="OBJECT")
        ob.select_set(False)


def tris_of(root):
    n = 0
    for ob in [root] + list(root.children_recursive):
        if ob.type == "MESH":
            n += sum(len(p.vertices) - 2 for p in ob.data.polygons)
    return n


def game_dims(root, skip_movers=True):
    """Bounds of the asset in game axes (x, y, z) metres, relative to the root (movers left out)."""
    lo = Vector((1e9, 1e9, 1e9)); hi = -lo
    inv = root.matrix_world.inverted()
    for ob in root.children_recursive:
        if ob.type != "MESH" or (skip_movers and ob.get("mover")):
            continue
        mw = inv @ ob.matrix_world
        for v in ob.data.vertices:
            w = mw @ v.co
            g = Vector((-w.x, w.z, -w.y))
            lo = Vector(map(min, lo, g)); hi = Vector(map(max, hi, g))
    return lo, hi


# ------------------------------------------------------------------------------------------------ running groups
HERE = os.path.dirname(os.path.abspath(__file__))


def out_dir(default):
    argv = sys.argv
    if "--" in argv:
        rest = argv[argv.index("--") + 1:]
        skip = False
        for a in rest:
            if skip:
                skip = False
                continue
            if a.startswith("-"):
                skip = True
                continue
            return a
    return default


def only_filter():
    """`-- <out> -only tile1,tile2` limits the run to some tiles."""
    argv = sys.argv
    if "-only" in argv:
        return set(argv[argv.index("-only") + 1].split(","))
    return None


def layout(roots, cols=None, gap=0.5):
    cols = cols or max(1, int(math.ceil(math.sqrt(len(roots)))))
    kit.grid(roots, cols, gap)


def run_tiles(tiles, group, script_dir, blend_prefix="", cols=None):
    """tiles: [(tile_name, label, [builder fn -> Asset or [Asset]])]. Each tile: fresh stage, build, smart UV, layout,
    render (detailed + pixel), save <script_dir>/<blend_prefix><tile>.blend, record coverage/stats."""
    out = out_dir(os.path.join(PREVIEW, group))
    only = only_filter()
    os.makedirs(out, exist_ok=True)
    cov_path = os.path.join(script_dir, "coverage_%s.json" % (blend_prefix.rstrip("_") or group))
    cov = json.load(open(cov_path)) if os.path.exists(cov_path) else {}
    tile_rows = []
    for item in tiles:
        tname, label, fns = item[0], item[1], item[2]
        tcols = item[3] if len(item) > 3 else cols
        if only and tname not in only:
            continue
        t0 = time.time()
        kit.stage(out)
        materials()
        assets = []
        for fn in fns:
            a = fn()
            assets += a if isinstance(a, (list, tuple)) else [a]
        roots = [a.build() for a in assets]
        bpy.context.view_layer.update()
        smart_uv([o for a in assets for o in a.objs])
        for a, r in zip(assets, roots):
            if a.yaw:
                r.rotation_euler.z = math.radians(-a.yaw)      # preview only; reset before saving
        bpy.context.view_layer.update()
        layout(roots, tcols)
        objs = [o for r in roots for o in [r] + list(r.children_recursive)]
        rc.render_tile(tname, objs, out)
        for o in bpy.context.scene.objects:
            o.hide_render = False
        blend = os.path.join(script_dir, blend_prefix + tname + ".blend")
        for a, r in zip(assets, roots):
            r.rotation_euler.z = 0.0
        bpy.context.view_layer.update()
        ids = []
        for a, r in zip(assets, roots):
            lo, hi = game_dims(r)
            d = hi - lo
            tri = tris_of(r)
            lo_b, hi_b = BUDGET.get(a.kind, (0, 1e9))
            ref = REF.get(a.ref)
            dev = None
            if ref:
                rd = ref["dims"]
                dev = round(max(abs(d[i] - rd[i]) for i in range(3)), 3)      # metres
            cov[a.gid] = {"blend": os.path.relpath(blend, HD), "tile": tname, "group": group, "category": a.category,
                          "kind": a.kind, "tris": tri, "dims": [round(x, 3) for x in d],
                          "ref_dims": ref["dims"] if ref else None, "dim_dev": dev,
                          "budget_ok": lo_b <= tri <= hi_b, "objects": [o.name for o in a.objs], "note": a.note}
            ids.append(a.gid)
            flag = "" if lo_b <= tri <= hi_b else " BUDGET!"
            flag += "" if dev is None or dev <= 0.035 else " DIMS! %s vs %s" % ([round(x, 2) for x in d], ref["dims"])
            print("ASSET %-26s %6d tris  %s%s" % (a.gid, tri, [round(x, 2) for x in d], flag))
        kit.save_blend(blend)
        tile_rows.append({"name": tname, "label": label, "replaces": ", ".join(ids)})
        print("TILE", tname, len(assets), "%.1fs" % (time.time() - t0))
    json.dump(cov, open(cov_path, "w"), indent=1, sort_keys=True)
    kit.update_tiles(tile_rows, out)


# ------------------------------------------------------------------------------------------------ helpers
def V(vs):
    """Voxel index -> metres for a grid of voxel size vs: V(0.08)(x) = x * 0.08."""
    return lambda *a: tuple(x * vs for x in a) if len(a) > 1 else a[0] * vs


def rng(seed):
    return random.Random(seed)


def rect_cells(x0, x1, y0, y1, holes):
    """Split rectangle [x0,x1]x[y0,y1] minus axis-aligned holes [(hx0,hx1,hy0,hy1)] into rectangles (row strips)."""
    xs = sorted({x0, x1} | {min(max(h[i], x0), x1) for h in holes for i in (0, 1)})
    ys = sorted({y0, y1} | {min(max(h[i], y0), y1) for h in holes for i in (2, 3)})
    cells = []
    for ya, yb in zip(ys, ys[1:]):
        row = []
        for xa, xb in zip(xs, xs[1:]):
            cx, cy = (xa + xb) / 2, (ya + yb) / 2
            inside = any(h[0] < cx < h[1] and h[2] < cy < h[3] for h in holes)
            if inside or xb - xa < 1e-5 or yb - ya < 1e-5:
                if row:
                    cells.append(row)
                    row = []
                continue
            if row and abs(row[-1][1] - xa) < 1e-6:
                row[-1] = (row[-1][0], xb, ya, yb)
            else:
                if row:
                    cells.append(row)
                row = [(xa, xb, ya, yb)]
        if row:
            cells.append(row)
    return [c for row in cells for c in (row if isinstance(row, list) else [row])]


def slab_xy(gp, m, x0, x1, y0, y1, z0, z1, holes=(), bevel=0.004):
    """A wall slab in the game xy plane (z = thickness) with rectangular holes."""
    for (a, b, c, d) in rect_cells(x0, x1, y0, y1, holes):
        gp.box2(m, (a, c, z0), (b, d, z1), bevel=bevel)


def boards_xy(gp, mats, x0, x1, y0, y1, z0, z1, width, holes=(), gap=0.006, vertical=True, seed=0, bevel=0.005, jitter=0.004):
    """Individual boards covering a wall face (vertical or horizontal), cut around holes; mats cycles per board."""
    r = random.Random(seed)
    mats = mats if isinstance(mats, (list, tuple)) else [mats]
    if vertical:
        n = max(1, round((x1 - x0) / width))
        w = (x1 - x0) / n
        for i in range(n):
            a, b = x0 + i * w + gap / 2, x0 + (i + 1) * w - gap / 2
            dz = r.uniform(-jitter, jitter)
            for (ca, cb, cc, cd) in rect_cells(a, b, y0, y1, holes):
                gp.box2(r.choice(mats), (ca, cc, z0 + dz), (cb, cd, z1 + dz), bevel=bevel)
    else:
        n = max(1, round((y1 - y0) / width))
        w = (y1 - y0) / n
        for i in range(n):
            a, b = y0 + i * w + gap / 2, y0 + (i + 1) * w - gap / 2
            dz = r.uniform(-jitter, jitter)
            for (ca, cb, cc, cd) in rect_cells(x0, x1, a, b, holes):
                gp.box2(r.choice(mats), (ca, cc, z0 + dz), (cb, cd, z1 + dz), bevel=bevel)


def nails(gp, m, pts, r=0.008, axis="z", h=0.006):
    for p in pts:
        gp.cyl(m, r, h, p, axis, 6, 0.0)


def window_frame(gp, glass_gp, x0, x1, y0, y1, z, frame_m, mull_x=None, mull_y=None, depth=0.06, bar=0.04, glass_m="glass"):
    """Frame + cross mullions + a pane in the opening (game xy plane, centred at depth z)."""
    t = depth
    for xa, xb in ((x0, x0 + bar), (x1 - bar, x1)):
        gp.box2(frame_m, (xa, y0, z - t / 2), (xb, y1, z + t / 2), bevel=0.004)
    for ya, yb in ((y0, y0 + bar), (y1 - bar, y1)):
        gp.box2(frame_m, (x0, ya, z - t / 2), (x1, yb, z + t / 2), bevel=0.004)
    if mull_x is not None:
        gp.box2(frame_m, (mull_x - bar / 2, y0, z - t / 2 + 0.005), (mull_x + bar / 2, y1, z + t / 2 - 0.005), bevel=0.003)
    if mull_y is not None:
        gp.box2(frame_m, (x0, mull_y - bar / 2, z - t / 2 + 0.005), (x1, mull_y + bar / 2, z + t / 2 - 0.005), bevel=0.003)
    if glass_gp is not None:
        glass_gp.box2(glass_m, (x0 + bar * 0.5, y0 + bar * 0.5, z - 0.004), (x1 - bar * 0.5, y1 - bar * 0.5, z + 0.004), bevel=0)


def corrugated(gp, m, u0, u1, v0, v1, amp=0.012, period=0.076, t=0.004, matrix=None, samples_per_period=6):
    """Corrugated sheet in local (u across the ribs, h up, v along the ribs) -> game space via `matrix`.
    Ribs run along v. Occupies h in [-amp - t/2, amp + t/2]."""
    n = max(2, int(round((u1 - u0) / period * samples_per_period)))
    top, bot = [], []
    for i in range(n + 1):
        u = u0 + (u1 - u0) * i / n
        h = amp * math.sin((u - u0) / period * math.tau)
        top.append((u, h + t / 2))
        bot.append((u, h - t / 2))
    loop = top + list(reversed(bot))
    secs = [[(u, h, v0) for u, h in loop], [(u, h, v1) for u, h in loop]]
    return gp.add(kit.bm_loft(secs), m, matrix or Matrix.Identity(4))


def stripes(gp, mats, a, b, w, t, n, axis_up=(0, 1, 0)):
    """n alternating boxes from a to b (hazard stripes / banding), width w, thickness t."""
    a, b = Vector(a), Vector(b)
    for i in range(n):
        p0 = a + (b - a) * (i / n)
        p1 = a + (b - a) * ((i + 1) / n)
        gp.beam(mats[i % len(mats)], p0, p1, w, t)


# ------------------------------------------------------------------------------------------------ game RNG / hash ports
def pal_hash(x, y, z, seed=0):
    """MadMax.Voxel.Pal.Hash (uint arithmetic)."""
    M = 0xFFFFFFFF
    h = ((x * 73856093) & M) ^ ((y * 19349663) & M) ^ ((z * 83492791) & M) ^ ((seed * 668265263) & M)
    h ^= h >> 13
    h = (h * 0x5bd1e995) & M
    h ^= h >> 15
    h = (h * 0x27d4eb2d) & M
    h ^= h >> 16
    return (h & 0xFFFFFF) / 16777215.0


class NetRandom:
    """System.Random(seed) (the legacy subtractive generator Mono / .NET use for seeded instances)."""
    MBIG, MSEED = 2147483647, 161803398

    def __init__(self, seed):
        sub = 2147483647 if seed == -2147483648 else abs(seed)
        mj = self.MSEED - sub
        sa = [0] * 56
        sa[55] = mj
        mk = 1
        for i in range(1, 55):
            ii = (21 * i) % 55
            sa[ii] = mk
            mk = mj - mk
            if mk < 0:
                mk += self.MBIG
            mj = sa[ii]
        for _ in range(1, 5):
            for i in range(1, 56):
                sa[i] -= sa[1 + (i + 30) % 55]
                if sa[i] < 0:
                    sa[i] += self.MBIG
        self.sa, self.inext, self.inextp = sa, 0, 21

    def _sample_int(self):
        a = self.inext + 1
        if a >= 56:
            a = 1
        b = self.inextp + 1
        if b >= 56:
            b = 1
        r = self.sa[a] - self.sa[b]
        if r == self.MBIG:
            r -= 1
        if r < 0:
            r += self.MBIG
        self.sa[a] = r
        self.inext, self.inextp = a, b
        return r

    def next_double(self):
        return self._sample_int() * (1.0 / self.MBIG)

    def next(self, lo_or_max, hi=None):
        if hi is None:
            return int(self.next_double() * lo_or_max)
        return int(self.next_double() * (hi - lo_or_max)) + lo_or_max
