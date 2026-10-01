"""Modelling kit for the HD heavy-vehicle pack (trucks, machines, trailers).

Coordinates are given in GAME VOXELS (1 voxel = 0.08 m, game axes +X right, +Y up, +Z forward), exactly like the
voxel designs in Assets/MadMax/Runtime/Designs, so sockets, pivots and colliders line up 1:1. `P()` maps them to
Blender metres following render_common's convention: game (x, y, z) -> Blender (x, -z, y) (Z up, front = -Y).
Continuous coordinates: a voxel box x0..x1 (inclusive centres) spans x0-0.5 .. x1+0.5; use `vbox` for that.

A `MB` (mesh builder) accumulates primitives into one bmesh with per-face material slots; `finish()` turns it into
one object whose origin is at the given pivot (socket / segment joint). Materials are procedural "hand-painted"
node trees (ramp variation, chips, rust streaks, cavity AO, dust towards the ground and on top faces).
"""
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import render_common as rc  # noqa: E402

S = 0.08
OUT = "/home/magix/PycharmProjects/MadMaxUnity/hd_preview/heavy"
BLEND_DIR = HERE


def P(x, y, z):
    return Vector((x * S, -z * S, y * S))


def D(x, y, z):
    """Game direction -> Blender direction (unnormalised)."""
    return Vector((x, -z, y))


# ------------------------------------------------------------------------------------------------ palette
CAT = ["6a5010", "9a7418", "c89a22", "e4bc34"]
TRACTOR_RED = ["5a1410", "8a2018", "b0302a", "d04a38"]
LIGHT = {"lightw": "fff3c0", "lighty": "ffd15a", "tail": "b02818", "amber": "f08a24"}
DUST = "a8865f"
DUST_DARK = "6e5238"


def ramp(name):
    if name == "cat":
        return CAT
    if name == "tractor":
        return TRACTOR_RED
    return rc.PAL[name]


# ------------------------------------------------------------------------------------------------ node helpers
class G:
    """Tiny node-graph helper."""

    def __init__(self, mat):
        self.nt = mat.node_tree
        self.n = self.nt.nodes
        self.l = self.nt.links
        self.x = -1400

    def node(self, kind, **kw):
        nd = self.n.new(kind)
        nd.location = (self.x, 0)
        self.x += 40
        for k, v in kw.items():
            setattr(nd, k, v)
        return nd

    def link(self, a, b):
        self.l.new(a, b)

    def val(self, v):
        nd = self.node("ShaderNodeValue")
        nd.outputs[0].default_value = v
        return nd.outputs[0]

    def rgb(self, hexs):
        nd = self.node("ShaderNodeRGB")
        nd.outputs[0].default_value = rc.srgb(hexs) if isinstance(hexs, str) else hexs
        return nd.outputs[0]

    def coords(self, scale=(1, 1, 1), space="Object"):
        tc = self.node("ShaderNodeTexCoord")
        mp = self.node("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = scale
        self.link(tc.outputs[space], mp.inputs["Vector"])
        return mp.outputs[0]

    def noise(self, vec, scale, detail=4.0, rough=0.55, distort=0.0):
        nd = self.node("ShaderNodeTexNoise")
        self.link(vec, nd.inputs["Vector"])
        nd.inputs["Scale"].default_value = scale
        nd.inputs["Detail"].default_value = detail
        nd.inputs["Roughness"].default_value = rough
        nd.inputs["Distortion"].default_value = distort
        return nd.outputs["Fac"]

    def mrange(self, v, a, b, c=0.0, d=1.0, clamp=True):
        nd = self.node("ShaderNodeMapRange")
        nd.clamp = clamp
        self.sock(v, nd.inputs["Value"])
        nd.inputs["From Min"].default_value = a
        nd.inputs["From Max"].default_value = b
        nd.inputs["To Min"].default_value = c
        nd.inputs["To Max"].default_value = d
        return nd.outputs["Result"]

    def math(self, op, a, b=None, c=None, clamp=False):
        nd = self.node("ShaderNodeMath", operation=op)
        nd.use_clamp = clamp
        self.sock(a, nd.inputs[0])
        if b is not None:
            self.sock(b, nd.inputs[1])
        if c is not None:
            self.sock(c, nd.inputs[2])
        return nd.outputs[0]

    def sock(self, v, inp):
        if isinstance(v, (int, float)):
            inp.default_value = v
        else:
            self.link(v, inp)

    def mix(self, a, b, fac, blend="MIX"):
        nd = self.node("ShaderNodeMix", data_type="RGBA", blend_type=blend)
        ins = {i.identifier: i for i in nd.inputs}
        self.sock(fac, ins["Factor_Float"])
        for v, key in ((a, "A_Color"), (b, "B_Color")):
            if isinstance(v, str):
                ins[key].default_value = rc.srgb(v)
            elif isinstance(v, tuple):
                ins[key].default_value = v
            else:
                self.link(v, ins[key])
        return {o.identifier: o for o in nd.outputs}["Result_Color"]

    def colramp(self, fac, hexes, interp="LINEAR", positions=None):
        nd = self.node("ShaderNodeValToRGB")
        cr = nd.color_ramp
        cr.interpolation = interp
        n = len(hexes)
        pos = positions or [i / max(1, n - 1) for i in range(n)]
        while len(cr.elements) < n:
            cr.elements.new(0.5)
        for e, h, p in zip(cr.elements, hexes, pos):
            e.position = p
            e.color = rc.srgb(h)
        self.link(fac, nd.inputs["Fac"])
        return nd.outputs["Color"]

    def geom(self):
        return self.node("ShaderNodeNewGeometry")

    def sep(self, vec):
        nd = self.node("ShaderNodeSeparateXYZ")
        self.link(vec, nd.inputs[0])
        return nd.outputs


def _bsdf(mat):
    b = mat.node_tree.nodes.get("Principled BSDF")
    return b


_MATS = {}


def surface_material(name, base_hexes, *, pattern=None, wear=0.25, rust=0.2, dirt=0.5, rough=0.55, metal=0.0,
                     scale=1.0, chip_hex=None, coat=0.0, top_dust=0.35, ao=0.45, streaks=None):
    """Hand-painted look: ramp variation + chips (primer/bare metal) + rust streaks + dust + cavity AO.
    pattern: None | ("stripes", hexA, hexB, width_m) | ("camo", [hex...]) | ("checker", hexA, hexB, size_m)."""
    if name in _MATS:
        return _MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    g = G(m)
    b = _bsdf(m)
    co = g.coords((scale, scale, scale))
    # ---- base colour: two-scale noise across the ramp (light to dark), hand-painted blotches
    n1 = g.noise(co, 1.6, 3.0, 0.5)
    n2 = g.noise(co, 0.35, 2.0, 0.5)
    nf = g.math("MULTIPLY_ADD", n1, 0.6, g.math("MULTIPLY", n2, 0.4))
    hexes = base_hexes
    mid = hexes[1:-1] if len(hexes) > 3 else hexes
    base = g.colramp(g.mrange(nf, 0.3, 0.7), mid)
    if pattern:
        kind = pattern[0]
        if kind == "stripes":
            wv = g.node("ShaderNodeTexWave", wave_type="BANDS", bands_direction="DIAGONAL")
            g.link(g.coords((1, 1, 1)), wv.inputs["Vector"])
            wv.inputs["Scale"].default_value = 1.0 / pattern[3]
            wv.inputs["Distortion"].default_value = 0.0
            st = g.mrange(wv.outputs["Fac"], 0.48, 0.52)
            base = g.mix(g.mix(pattern[1], base, 0.25), pattern[2], st)
        elif kind == "camo":
            cn = g.noise(g.coords((1, 1, 1)), 0.9, 2.0, 0.4, 0.3)
            cols = pattern[1]
            camo = g.colramp(cn, cols, "CONSTANT", [0.0, 0.42, 0.58][:len(cols)])
            base = g.mix(camo, base, 0.18, "OVERLAY")
        elif kind == "checker":
            ck = g.node("ShaderNodeTexChecker")
            g.link(g.coords((1, 1, 1)), ck.inputs["Vector"])
            ck.inputs["Scale"].default_value = 1.0 / pattern[3]
            ck.inputs["Color1"].default_value = rc.srgb(pattern[1])
            ck.inputs["Color2"].default_value = rc.srgb(pattern[2])
            base = ck.outputs["Color"]
    # top faces painted lighter (hand-painted key light), bottoms darker
    geo = g.geom()
    nz = g.sep(geo.outputs["Normal"])[2]
    lift = g.mrange(nz, -1.0, 1.0, -0.12, 0.14)
    base = g.mix(base, (1, 1, 1, 1), g.math("MAXIMUM", lift, 0.0))
    base = g.mix(base, (0, 0, 0, 1), g.math("MAXIMUM", g.math("MULTIPLY", lift, -1.0), 0.0))
    rough_s = g.val(rough)
    metal_s = g.val(metal)
    # ---- painted edge highlight on bevel strips (face-corner attribute "Edge", exported as vertex colour)
    ca = g.node("ShaderNodeVertexColor", layer_name="Edge")
    edge = g.sep(ca.outputs["Color"])[0]
    base = g.mix(base, g.mix(base, (1, 0.93, 0.8, 1), 0.45), g.math("MULTIPLY", edge, 0.8))
    # ---- chips
    if wear > 0:
        cn = g.noise(g.coords((scale, scale, scale)), 7.0, 6.0, 0.72, 0.15)
        chip = g.mrange(g.math("ADD", cn, g.math("MULTIPLY", edge, 0.12 + wear * 0.2)), 0.74 - wear * 0.22, 0.76 - wear * 0.22)
        under = g.mix(chip_hex or rc.PAL["metal"][2], rc.PAL["rust"][2], g.mrange(g.noise(co, 3.0), 0.45, 0.6))
        edge = g.mix(base, rc.PAL["metal"][0], g.mrange(cn, 0.70 - wear * 0.22, 0.74 - wear * 0.22, 0.0, 0.5))
        base = g.mix(edge, under, chip)
        rough_s = g.math("ADD", rough_s, g.math("MULTIPLY", chip, 0.15))
        metal_s = g.math("ADD", metal_s, g.math("MULTIPLY", chip, 0.35))
    # ---- rust streaks running down (Blender Z up: stretch the noise vertically)
    if rust > 0:
        sc = g.coords((5.0 * scale, 5.0 * scale, 0.45 * scale))
        rn = g.noise(sc, 3.0, 5.0, 0.6)
        rm = g.mrange(rn, 0.72 - rust * 0.25, 0.80 - rust * 0.25)
        rcol = g.colramp(g.noise(co, 6.0), rc.PAL["rust"][1:4])
        base = g.mix(base, rcol, g.math("MULTIPLY", rm, 0.85))
        rough_s = g.math("ADD", rough_s, g.math("MULTIPLY", rm, 0.3))
    # ---- streak decals (oil drips)
    if streaks:
        sc = g.coords((9.0, 9.0, 0.3))
        sn = g.noise(sc, 2.0, 3.0, 0.5)
        sm = g.mrange(sn, 0.78, 0.84, 0.0, 0.7)
        base = g.mix(base, streaks, sm)
    # ---- dust: towards the ground (world Z) and on up-facing surfaces
    if dirt > 0:
        wz = g.sep(geo.outputs["Position"])[2]
        low = g.mrange(wz, 0.0, 1.1, 1.0, 0.0)
        dn = g.noise(g.coords((2, 2, 2), "Object"), 4.0, 4.0, 0.6)
        lowm = g.math("MULTIPLY", g.math("MULTIPLY", low, g.mrange(dn, 0.3, 0.7, 0.4, 1.0)), dirt)
        base = g.mix(base, g.mix(DUST, DUST_DARK, g.noise(co, 9.0)), lowm)
        topm = g.math("MULTIPLY", g.mrange(nz, 0.55, 0.95), g.mrange(dn, 0.45, 0.75, 0.0, top_dust))
        base = g.mix(base, DUST, topm)
        rough_s = g.math("ADD", rough_s, g.math("MULTIPLY", g.math("ADD", lowm, topm), 0.4))
    # ---- cavity AO baked into the paint
    if ao > 0:
        aon = g.node("ShaderNodeAmbientOcclusion")
        aon.inputs["Distance"].default_value = 0.25
        aof = g.mrange(aon.outputs["AO"], 0.0, 1.0, 1.0 - ao, 1.0)
        base = g.mix(base, (0, 0, 0, 1), g.math("SUBTRACT", 1.0, aof))
    g.link(base, b.inputs["Base Color"])
    g.link(g.math("MINIMUM", rough_s, 1.0), b.inputs["Roughness"])
    g.link(g.math("MINIMUM", metal_s, 1.0), b.inputs["Metallic"])
    if coat:
        b.inputs["Coat Weight"].default_value = coat
        b.inputs["Coat Roughness"].default_value = 0.25
    _MATS[name] = m
    return m


def simple_material(name, hexs, rough=0.5, metal=0.0, emit=0.0, alpha=1.0, coat=0.0):
    if name in _MATS:
        return _MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = _bsdf(m)
    col = rc.srgb(hexs)
    b.inputs["Base Color"].default_value = col
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit:
        b.inputs["Emission Color"].default_value = col
        b.inputs["Emission Strength"].default_value = emit
    if alpha < 1:
        b.inputs["Alpha"].default_value = alpha
        m.surface_render_method = "DITHERED"
    if coat:
        b.inputs["Coat Weight"].default_value = coat
    _MATS[name] = m
    return m


def chrome_material(name="chrome", hexs="aeb1b8", rough=0.18):
    if name in _MATS:
        return _MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    g = G(m)
    b = _bsdf(m)
    co = g.coords()
    pit = g.noise(co, 12.0, 6.0, 0.7)
    col = g.mix(hexs, rc.PAL["rust"][2], g.mrange(pit, 0.70, 0.76, 0, 0.8))
    geo = g.geom()
    wz = g.sep(geo.outputs["Position"])[2]
    col = g.mix(col, DUST, g.math("MULTIPLY", g.mrange(wz, 0.0, 0.9, 0.6, 0.0), g.noise(co, 3.0)))
    g.link(col, b.inputs["Base Color"])
    g.link(g.mrange(pit, 0.6, 0.8, rough, 0.6), b.inputs["Roughness"])
    b.inputs["Metallic"].default_value = 1.0
    _MATS[name] = m
    return m


def glass_material(name="glass"):
    if name in _MATS:
        return _MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    g = G(m)
    b = _bsdf(m)
    co = g.coords()
    geo = g.geom()
    wz = g.sep(geo.outputs["Position"])[2]
    grime = g.math("MULTIPLY", g.noise(co, 5.0, 4.0, 0.6), g.mrange(wz, 0.6, 2.2, 0.9, 0.2))
    col = g.mix(rc.PAL["glass"][2], DUST, g.mrange(grime, 0.35, 0.7, 0.0, 0.6))
    g.link(col, b.inputs["Base Color"])
    g.link(g.mrange(grime, 0.3, 0.7, 0.04, 0.6), b.inputs["Roughness"])
    g.link(g.mrange(grime, 0.3, 0.7, 0.55, 0.95), b.inputs["Alpha"])
    b.inputs["Coat Weight"].default_value = 0.6
    m.surface_render_method = "DITHERED"
    _MATS[name] = m
    return m


def rubber_material(name="rubber", hexs=None):
    if name in _MATS:
        return _MATS[name]
    t = hexs or rc.PAL["tire"]
    return surface_material(name, t, wear=0.0, rust=0.0, dirt=0.9, rough=0.88, top_dust=0.55, ao=0.5, scale=1.5)


def MAT(key):
    """Material library by key."""
    if key in _MATS:
        return _MATS[key]
    k = key
    if k.startswith("paint_"):
        r = k[6:]
        args = dict(wear=0.32, rust=0.25, dirt=0.55)
        if r == "cat":
            args.update(wear=0.38, rust=0.22, coat=0.1)
        if r in ("cream", "ochre"):
            args.update(wear=0.3, rust=0.3)
        return surface_material(k, ramp(r), **args)
    table = {
        "steel": lambda: surface_material(k, rc.PAL["metal"], wear=0.2, rust=0.45, dirt=0.65, rough=0.62, metal=0.55, chip_hex=rc.PAL["chrome"][1]),
        "steel_dark": lambda: surface_material(k, ["0d0d10", "18171c", "24211f", "302d33"], wear=0.25, rust=0.4, dirt=0.75, rough=0.7, metal=0.4, chip_hex=rc.PAL["chrome"][1], streaks=rc.PAL["black"][0]),
        "steel_bare": lambda: surface_material(k, rc.PAL["chrome"][:3], wear=0.1, rust=0.35, dirt=0.4, rough=0.42, metal=0.85),
        "rust": lambda: surface_material(k, rc.PAL["rust"], wear=0.15, rust=0.5, dirt=0.5, rough=0.92, chip_hex=rc.PAL["rust"][0]),
        "black": lambda: surface_material(k, rc.PAL["black"], wear=0.25, rust=0.25, dirt=0.6, rough=0.65),
        "rubber": lambda: rubber_material(k),
        "tyre": lambda: rubber_material(k),
        "chrome": lambda: chrome_material(k),
        "chrome_dull": lambda: chrome_material(k, rc.PAL["chrome"][1], 0.35),
        "glass": lambda: glass_material(k),
        "void": lambda: simple_material(k, "07070a", 0.9),
        "lightw": lambda: simple_material(k, LIGHT["lightw"], 0.2, emit=4.0),
        "lighty": lambda: simple_material(k, LIGHT["lighty"], 0.2, emit=3.0),
        "tail": lambda: simple_material(k, LIGHT["tail"], 0.25, emit=2.5),
        "amber": lambda: simple_material(k, LIGHT["amber"], 0.25, emit=2.5),
        "lens": lambda: simple_material(k, "e8e2d0", 0.08, coat=1.0),
        "hazard": lambda: surface_material(k, CAT, pattern=("stripes", CAT[3], rc.PAL["black"][1], 0.11), wear=0.4, rust=0.2, dirt=0.5),
        "hazard_red": lambda: surface_material(k, rc.PAL["cream"], pattern=("stripes", rc.PAL["crimson"][3], rc.PAL["cream"][3], 0.11), wear=0.35, rust=0.25, dirt=0.4),
        "camo": lambda: surface_material(k, rc.PAL["moss"], pattern=("camo", [rc.PAL["black"][2], rc.PAL["moss"][2], rc.PAL["sand"][2]]), wear=0.3, rust=0.15, dirt=0.75),
        "wood": lambda: surface_material(k, rc.PAL["wood"], wear=0.0, rust=0.0, dirt=0.4, rough=0.8, scale=2.0, streaks=rc.PAL["wood"][0]),
        "wood_light": lambda: surface_material(k, rc.PAL["wood"][2:], wear=0.0, rust=0.0, dirt=0.3, rough=0.8, scale=2.0),
        "fabric": lambda: surface_material(k, rc.PAL["olive"], wear=0.0, rust=0.0, dirt=0.3, rough=0.95, scale=3.0),
        "fabric_blue": lambda: surface_material(k, rc.PAL["navy"], wear=0.0, rust=0.0, dirt=0.3, rough=0.95, scale=3.0),
        "leather": lambda: surface_material(k, rc.PAL["olive"][:3], wear=0.15, rust=0.0, dirt=0.2, rough=0.6, chip_hex=rc.PAL["olive"][3]),
        "mattress": lambda: surface_material(k, rc.PAL["cream"][:3], wear=0.0, rust=0.0, dirt=0.25, rough=0.95, scale=3.0, streaks=rc.PAL["olive"][2]),
        "red_cross": lambda: surface_material(k, rc.PAL["crimson"][2:], wear=0.2, rust=0.0, dirt=0.3, rough=0.5),
        "white_enamel": lambda: surface_material(k, rc.PAL["cream"][1:], wear=0.25, rust=0.3, dirt=0.3, rough=0.4),
        "asphalt": lambda: surface_material(k, ["0d0d10", "18171c", "232127"], wear=0.0, rust=0.0, dirt=0.2, rough=0.95),
        "grating": lambda: surface_material(k, rc.PAL["metal"], pattern=("checker", rc.PAL["metal"][3], rc.PAL["metal"][1], 0.06), wear=0.1, rust=0.4, dirt=0.6, rough=0.6, metal=0.5),
        "tread": lambda: surface_material(k, rc.PAL["metal"], pattern=("checker", rc.PAL["chrome"][1], rc.PAL["metal"][2], 0.05), wear=0.05, rust=0.35, dirt=0.6, rough=0.5, metal=0.7),
        "sign": lambda: simple_material(k, LIGHT["lighty"], 0.4, emit=1.2),
        "hose": lambda: surface_material(k, rc.PAL["black"], wear=0.0, rust=0.0, dirt=0.5, rough=0.7),
        "canvas": lambda: surface_material(k, rc.PAL["moss"][1:], wear=0.0, rust=0.0, dirt=0.5, rough=0.95, scale=2.0, top_dust=0.2),
        "oil": lambda: simple_material(k, "0a0806", 0.15),
        "hot_mix": lambda: surface_material(k, ["0d0d10", "18171c", "232127"], wear=0.0, rust=0.0, dirt=0.0, rough=0.35, ao=0.6),
        "grain": lambda: surface_material(k, rc.PAL["ochre"][2:], wear=0.0, rust=0.0, dirt=0.0, rough=0.9, scale=8.0),
        "soil": lambda: surface_material(k, rc.PAL["olive"][:3], wear=0.0, rust=0.0, dirt=0.0, rough=1.0, scale=4.0),
    }
    if k in table:
        return table[k]()
    raise KeyError(key)


# ------------------------------------------------------------------------------------------------ mesh builder
def _ident():
    return Matrix.Identity(4)


class MB:
    """Mesh builder in game-voxel coordinates. All primitives are transformed by `self.T` (a Blender-space frame)."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.edge = self.bm.loops.layers.float_color.new("Edge")   # 1 on bevel strips: painted edge highlights / wear
        self.edge_faces = set()
        self.mats = []
        self.T = _ident()

    # -- frames
    def frame(self, origin=(0, 0, 0), yaw=0.0, pitch=0.0, roll=0.0, mirror=False):
        """Set a local frame: game-space origin, game yaw (deg, + = clockwise from above, like Unity),
        pitch about game X (+ = nose down, like Unity), roll about game Z. Returns self for chaining."""
        T = Matrix.Translation(P(*origin))
        T = T @ Matrix.Rotation(math.radians(yaw), 4, "Z")
        T = T @ Matrix.Rotation(math.radians(-pitch), 4, "X")
        T = T @ Matrix.Rotation(math.radians(roll), 4, "Y")
        if mirror:
            T = T @ Matrix.Diagonal((-1, 1, 1, 1))
        self.T = T
        return self

    def reset(self):
        self.T = _ident()
        return self

    def mi(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def _post(self, verts, mat, bev=0.0, seg=1, angle=28.0):
        verts = list(verts)
        idx = self.mi(mat)
        faces = {f for v in verts for f in v.link_faces}
        for f in faces:
            f.material_index = idx
        if bev > 0:
            edges = {e for v in verts for e in v.link_edges}
            edges = [e for e in edges if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > math.radians(angle)]
            if edges:
                res = bmesh.ops.bevel(self.bm, geom=edges, offset=bev, offset_type="OFFSET", segments=seg, profile=0.5,
                                      affect="EDGES", clamp_overlap=True)
                self.edge_faces.update(res["faces"])
        return verts

    # -- primitives
    def box(self, lo, hi, mat, bev=0.012, seg=1, rot=None):
        """Axis-aligned (in the current frame) box from continuous game coords lo..hi. rot: optional Blender 4x4
        rotation about the box centre."""
        lo, hi = Vector(lo), Vector(hi)
        c = (lo + hi) / 2
        sz = hi - lo
        M = Matrix.Translation(P(*c))
        if rot is not None:
            M = M @ rot
        M = self.T @ M @ Matrix.Diagonal((abs(sz.x) * S, abs(sz.z) * S, abs(sz.y) * S, 1.0))
        r = bmesh.ops.create_cube(self.bm, size=1.0, matrix=M)
        return self._post(r["verts"], mat, bev, seg)

    def vbox(self, x0, y0, z0, x1, y1, z1, mat, bev=0.012, seg=1):
        """Inclusive voxel-centre box, exactly like VoxelGrid.Box."""
        return self.box((min(x0, x1) - .5, min(y0, y1) - .5, min(z0, z1) - .5),
                        (max(x0, x1) + .5, max(y0, y1) + .5, max(z0, z1) + .5), mat, bev, seg)

    def cyl(self, a, b, r, mat, seg=16, r2=None, bev=0.0, caps=True, bseg=1):
        a, b = P(*a), P(*b)
        d = b - a
        L = d.length
        if L < 1e-6:
            return []
        q = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
        M = self.T @ Matrix.Translation((a + b) / 2) @ q
        res = bmesh.ops.create_cone(self.bm, cap_ends=caps, cap_tris=False, segments=seg, radius1=r * S,
                                    radius2=(r if r2 is None else r2) * S, depth=L, matrix=M)
        return self._post(res["verts"], mat, bev, bseg, angle=40)

    def ball(self, c, r, mat, seg=10, rings=6, squash=1.0):
        M = self.T @ Matrix.Translation(P(*c)) @ Matrix.Diagonal((1, 1, squash, 1))
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=rings, radius=r * S, matrix=M)
        return self._post(res["verts"], mat)

    def lathe(self, profile, a, b, mat, seg=24, mats=None, cap0=False, cap1=False, phase=0.0):
        """Surface of revolution around the axis a->b. profile: [(radius_vox, t)] with t in 0..1 along a->b
        (or absolute voxels if t > 1 and len given). mats: per profile segment material keys."""
        A, B = P(*a), P(*b)
        ax = B - A
        L = ax.length
        z = ax.normalized()
        x = z.orthogonal().normalized()
        y = z.cross(x)
        rings = []
        for (r, t) in profile:
            ring = []
            for i in range(seg):
                an = 2 * math.pi * i / seg + phase
                p = A + z * (t * L) + (x * math.cos(an) + y * math.sin(an)) * (r * S)
                ring.append(self.bm.verts.new(self.T @ p))
            rings.append(ring)
        newf = []
        for j in range(len(rings) - 1):
            mk = (mats[j] if mats else mat)
            idx = self.mi(mk)
            for i in range(seg):
                f = self.bm.faces.new((rings[j][i], rings[j][(i + 1) % seg], rings[j + 1][(i + 1) % seg], rings[j + 1][i]))
                f.material_index = idx
                newf.append(f)
        for cap, ring in ((cap0, rings[0]), (cap1, rings[-1])):
            if cap:
                f = self.bm.faces.new(ring)
                f.material_index = self.mi(cap if isinstance(cap, str) else mat)
                newf.append(f)
        bmesh.ops.recalc_face_normals(self.bm, faces=newf)
        return [v for r in rings for v in r]

    def prism(self, profile, axis, a0, a1, mat, bev=0.012, seg=1, angle=28.0):
        """Extrude a 2D polygon. axis 'x': profile pts are (z, y) and the prism spans x a0..a1;
        'y': pts (x, z), spans y; 'z': pts (x, y), spans z. Coordinates are continuous game voxels."""
        def g3(u, v, w):
            if axis == "x":
                return (w, v, u)
            if axis == "y":
                return (u, w, v)
            return (u, v, w)
        bot = [self.bm.verts.new(self.T @ P(*g3(u, v, a0))) for (u, v) in profile]
        top = [self.bm.verts.new(self.T @ P(*g3(u, v, a1))) for (u, v) in profile]
        n = len(profile)
        fs = [self.bm.faces.new(bot[::-1]), self.bm.faces.new(top)]
        for i in range(n):
            fs.append(self.bm.faces.new((bot[i], bot[(i + 1) % n], top[(i + 1) % n], top[i])))
        bmesh.ops.recalc_face_normals(self.bm, faces=fs)
        return self._post(bot + top, mat, bev, seg, angle)

    def quad_strip(self, pts_a, pts_b, mat):
        """Ruled surface between two polylines (game coords)."""
        va = [self.bm.verts.new(self.T @ P(*p)) for p in pts_a]
        vb = [self.bm.verts.new(self.T @ P(*p)) for p in pts_b]
        idx = self.mi(mat)
        for i in range(len(va) - 1):
            f = self.bm.faces.new((va[i], va[i + 1], vb[i + 1], vb[i]))
            f.material_index = idx
        return va + vb

    def wall(self, axis, at, u0, u1, v0, v1, holes, thick, mat, bev=0.01, side=0):
        """Flat panel with rectangular holes. axis 'x': plane x=at, u=z, v=y; 'z': plane z=at, u=x, v=y;
        'y': plane y=at, u=x, v=z. holes: [(ua, ub, va, vb)]. thick in voxels, centred on `at` (side=+1/-1 grows
        outward/inward from at)."""
        us = sorted({u0, u1, *[min(max(h[i], u0), u1) for h in holes for i in (0, 1)]})
        vs = sorted({v0, v1, *[min(max(h[i], v0), v1) for h in holes for i in (2, 3)]})
        a0 = at - thick / 2 + side * thick / 2

        def g3(u, v, w):
            if axis == "x":
                return (w, v, u)
            if axis == "z":
                return (u, v, w)
            return (u, w, v)
        grid = {}
        for i, u in enumerate(us):
            for j, v in enumerate(vs):
                grid[i, j] = self.bm.verts.new(self.T @ P(*g3(u, v, a0)))
        faces = []
        for i in range(len(us) - 1):
            for j in range(len(vs) - 1):
                cu, cv = (us[i] + us[i + 1]) / 2, (vs[j] + vs[j + 1]) / 2
                if any(h[0] < cu < h[1] and h[2] < cv < h[3] for h in holes):
                    continue
                faces.append(self.bm.faces.new((grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1])))
        used = {v for f in faces for v in f.verts}
        for v in list(grid.values()):
            if v not in used:
                self.bm.verts.remove(v)
        ex = bmesh.ops.extrude_face_region(self.bm, geom=faces)
        nv = [e for e in ex["geom"] if isinstance(e, bmesh.types.BMVert)]
        off = self.T.to_3x3() @ P(*g3(0, 0, thick))
        bmesh.ops.translate(self.bm, verts=nv, vec=off)
        allv = list(used) + nv
        fs = list({f for v in allv for f in v.link_faces})
        bmesh.ops.recalc_face_normals(self.bm, faces=fs)
        return self._post(allv, mat, bev)

    def cut_plane(self, co, normal, mat=None):
        """Cut the whole mesh with a plane (game coords; normal points to the side that is removed) and cap it."""
        geom = self.bm.verts[:] + self.bm.edges[:] + self.bm.faces[:]
        res = bmesh.ops.bisect_plane(self.bm, geom=geom, dist=1e-5, plane_co=self.T @ P(*co),
                                     plane_no=(self.T.to_3x3() @ D(*normal)).normalized(), clear_outer=True)
        edges = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
        new = bmesh.ops.holes_fill(self.bm, edges=edges, sides=0)
        if mat:
            idx = self.mi(mat)
            for f in new["faces"]:
                f.material_index = idx
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces[:])

    # -- details
    def tube(self, pts, r, mat, seg=8, joints=True):
        for i in range(len(pts) - 1):
            self.cyl(pts[i], pts[i + 1], r, mat, seg)
            if joints and 0 < i:
                self.ball(pts[i], r * 1.02, mat, seg, 4)

    def hose(self, a, b, r, mat="hose", sag=2.0, n=6, seg=8):
        a, b = Vector(a), Vector(b)
        pts = []
        for i in range(n + 1):
            t = i / n
            p = a.lerp(b, t)
            p.y -= sag * 4 * t * (1 - t)
            pts.append(tuple(p))
        self.tube(pts, r, mat, seg)

    def rivets(self, pts, normal, r=0.28, mat="steel", h=0.18):
        nrm = Vector(normal).normalized()
        for p in pts:
            p = Vector(p)
            self.cyl(tuple(p), tuple(p + nrm * h), r, mat, 6, r2=r * 0.6)

    def lamp(self, c, normal, r, glow="lightw", bezel="chrome", depth=0.6, seg=12, square=False):
        c, n = Vector(c), Vector(normal).normalized()
        if square:
            # rectangular lamp: bezel box + lens slab, normal must be axis aligned
            ext = Vector((r, r * 0.65, r))
            for i in range(3):
                if abs(n[i]) > 0.5:
                    ext[i] = depth / 2
            self.box(tuple(c - ext), tuple(c + ext), bezel, 0.006)
            e2 = Vector([e * 0.8 if abs(n[i]) < 0.5 else 0.12 for i, e in enumerate(ext)])
            c2 = c + n * (depth / 2 + 0.06)
            self.box(tuple(c2 - e2), tuple(c2 + e2), glow, 0.0)
            return
        self.cyl(tuple(c - n * depth), tuple(c), r, bezel, seg)
        self.cyl(tuple(c), tuple(c + n * 0.25), r * 0.8, glow, seg, r2=r * 0.6)

    def ladder(self, x0, x1, z, y0, y1, step=3.0, mat="steel", r=0.35, normal_z=-1):
        for x in (x0, x1):
            self.cyl((x, y0, z), (x, y1, z), r, mat, 8)
        y = y0 + step * 0.5
        while y < y1:
            self.cyl((x0, y, z), (x1, y, z), r * 0.8, mat, 6)
            y += step
        # standoff brackets to the wall
        for y in (y0 + 1, y1 - 1):
            for x in (x0, x1):
                self.cyl((x, y, z), (x, y, z - normal_z * 1.0), r * 0.7, mat, 6)

    def grille(self, x0, x1, y0, y1, z, mat="chrome", frame="steel_dark", n=None, back="void", vertical=True):
        """Grille on a face with normal +Z at z (front face)."""
        self.box((x0, y0, z - 1.0), (x1, y1, z - 0.2), back, 0.0)
        span = (x1 - x0) if vertical else (y1 - y0)
        n = n or max(3, int(span / 1.2))
        for i in range(n + 1):
            t = i / n
            if vertical:
                x = x0 + 0.3 + t * (x1 - x0 - 0.6)
                self.box((x - 0.18, y0, z - 0.6), (x + 0.18, y1, z + 0.15), mat, 0.004)
            else:
                y = y0 + 0.3 + t * (y1 - y0 - 0.6)
                self.box((x0, y - 0.18, z - 0.6), (x1, y + 0.18, z + 0.15), mat, 0.004)
        b = 0.45
        self.box((x0 - b, y0 - b, z - 0.4), (x1 + b, y0, z + 0.35), frame, 0.006)
        self.box((x0 - b, y1, z - 0.4), (x1 + b, y1 + b, z + 0.35), frame, 0.006)
        self.box((x0 - b, y0, z - 0.4), (x0, y1, z + 0.35), frame, 0.006)
        self.box((x1, y0, z - 0.4), (x1 + b, y1, z + 0.35), frame, 0.006)

    def seam(self, a, b, normal, mat="black", w=0.12):
        """Panel line: a thin dark strip slightly proud of a surface, from a to b (game coords)."""
        a, b, n = Vector(a), Vector(b), Vector(normal).normalized()
        d = (b - a)
        lo = Vector([min(a[i], b[i]) for i in range(3)])
        hi = Vector([max(a[i], b[i]) for i in range(3)])
        for i in range(3):
            if abs(n[i]) > 0.5:
                lo[i] = a[i] - 0.02
                hi[i] = a[i] + 0.08 if n[i] > 0 else a[i] + 0.02
                if n[i] < 0:
                    lo[i] = a[i] - 0.08
            elif abs(d[i]) < 1e-6:
                lo[i] -= w
                hi[i] += w
        self.box(tuple(lo), tuple(hi), mat, 0.0)

    def text(self, s, at, normal, size, mat, extrude=0.06, align="CENTER", up=(0, 1, 0)):
        """Raised lettering (font converted to mesh) on a face; at/normal/up in game coords, size in voxels."""
        cu = bpy.data.curves.new("txt", "FONT")
        cu.body = s
        cu.size = size * S
        cu.extrude = extrude * S
        cu.align_x = align
        cu.align_y = "CENTER"
        ob = bpy.data.objects.new("txt", cu)
        bpy.context.scene.collection.objects.link(ob)
        dg = bpy.context.evaluated_depsgraph_get()
        me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
        bpy.data.objects.remove(ob)
        bpy.data.curves.remove(cu)
        n = D(*normal).normalized()
        u = D(*up).normalized()
        xa = u.cross(n)
        R = Matrix((xa, u, n)).transposed().to_4x4()
        M = self.T @ Matrix.Translation(P(*at) + n * (extrude * S)) @ R
        me.transform(M)
        tmp = bmesh.new()
        tmp.from_mesh(me)
        bpy.data.meshes.remove(me)
        idx = self.mi(mat)
        vmap = {}
        for v in tmp.verts:
            vmap[v.index] = self.bm.verts.new(v.co)
        for f in tmp.faces:
            try:
                nf = self.bm.faces.new([vmap[v.index] for v in f.verts])
                nf.material_index = idx
            except ValueError:
                pass
        tmp.free()

    def merge(self, other):
        """Append another MB's geometry (materials remapped)."""
        tmp = bmesh.new()
        me = bpy.data.meshes.new("tmp")
        other.bm.to_mesh(me)
        tmp.from_mesh(me)
        bpy.data.meshes.remove(me)
        remap = [self.mi(m) for m in other.mats]
        vmap = [self.bm.verts.new(v.co) for v in tmp.verts]
        for f in tmp.faces:
            nf = self.bm.faces.new([vmap[v.index] for v in f.verts])
            nf.material_index = remap[f.material_index] if f.material_index < len(remap) else 0

    def finish(self, name=None, parent=None, origin=None, props=None, smooth=35.0, local=False):
        """local=True: the geometry was authored relative to `origin` (part-local), so it is not shifted."""
        name = name or self.name
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=0.0002)
        on, off = (1.0, 1.0, 1.0, 1.0), (0.0, 0.0, 0.0, 1.0)
        for f in self.bm.faces:
            v = on if f in self.edge_faces else off
            for lp in f.loops:
                lp[self.edge] = v
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(MAT(m))
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        world = P(*origin) if origin is not None else Vector((0, 0, 0))
        if origin is not None and not local:
            me.transform(Matrix.Translation(-world))
        if smooth:
            me.shade_smooth()
            me.set_sharp_from_angle(angle=math.radians(smooth))
        attach(ob, parent, world)
        if props:
            for k, v in props.items():
                ob[k] = v
        return ob


def attach(ob, parent, world):
    """Parent without rotation: local position = world position - parent world position."""
    if parent is not None:
        ob.parent = parent
        pw = Vector((0, 0, 0))
        q = parent
        while q is not None:           # build-time hierarchy is unrotated / unscaled: world = sum of locations
            pw += q.location
            q = q.parent
        ob.location = world - pw
    else:
        ob.location = world


def empty(name, parent=None, origin=(0, 0, 0), props=None):
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_size = 0.3
    bpy.context.scene.collection.objects.link(ob)
    attach(ob, parent, P(*origin))
    bpy.context.view_layer.update()
    for k, v in (props or {}).items():
        ob[k] = v
    return ob


def instance(src, name, parent, origin, mirror=False, props=None):
    """Linked duplicate of a part mesh at a socket (mirror = left socket, localScale.x = -1 like the game)."""
    ob = bpy.data.objects.new(name, src.data)
    bpy.context.scene.collection.objects.link(ob)
    attach(ob, parent, P(*origin))
    if mirror:
        ob.scale.x = -1
    for k, v in (props or {}).items():
        ob[k] = v
    return ob


# ------------------------------------------------------------------------------------------------ 2D shapes
def rrect(x0, x1, y0, y1, r, n=4):
    """Rounded rectangle polygon (CCW)."""
    pts = []
    for (cx, cy, a0) in ((x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)):
        for i in range(n + 1):
            a = math.radians(a0 + 90 * i / n)
            pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    return pts


def rrect_top(x0, x1, y0, y1, r, n=4):
    """Rectangle with only the top corners rounded."""
    pts = [(x0, y0), (x1, y0)]
    for (cx, cy, a0) in ((x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90)):
        for i in range(n + 1):
            a = math.radians(a0 + 90 * i / n)
            pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    return pts


def arc_ring(cu, cv, r0, r1, a0, a1, n=12):
    """Annular sector polygon in (u, v), angles in degrees from +u towards +v."""
    outer = [(cu + math.cos(math.radians(a0 + (a1 - a0) * i / n)) * r1, cv + math.sin(math.radians(a0 + (a1 - a0) * i / n)) * r1) for i in range(n + 1)]
    inner = [(cu + math.cos(math.radians(a0 + (a1 - a0) * i / n)) * r0, cv + math.sin(math.radians(a0 + (a1 - a0) * i / n)) * r0) for i in range(n + 1)]
    return outer + inner[::-1]


# ------------------------------------------------------------------------------------------------ shared parts
def wheel_mesh(key, R, w, rim, kind="truck", rim_mat="steel", hub_mat="rust", lugs=None, seg=40):
    """Wheel part authored like the game: origin = mount point (inner face, axle centre), extends +X by w voxels.
    R/rim in voxels. kind: truck | small | track | tractor | tractor_front | offroad."""
    cache = bpy.data.meshes.get("W_" + key)
    if cache:
        return cache
    mb = MB("W_" + key)
    c0, c1 = (0.15, 0, 0), (w - 0.15, 0, 0)
    sh = min(1.4, R * 0.18)
    tread_r = R - (1.0 if kind in ("tractor", "tractor_front") else 0.55)
    prof = [(rim - 0.2, 0.0), (rim + 0.6, 0.0), (R - sh, 0.02), (tread_r - 0.1, 0.12), (tread_r, 0.25),
            (tread_r, 0.75), (tread_r - 0.1, 0.88), (R - sh, 0.98), (rim + 0.6, 1.0), (rim - 0.2, 1.0)]
    mb.lathe(prof, c0, c1, "tyre", seg=seg)
    # sidewall lettering ring (subtle raised band)
    mb.lathe([(rim + 1.1, 0.995), (rim + 1.6, 1.01), (rim + 2.0, 0.995)], c0, c1, "black", seg=seg)
    # tread lugs
    nl = lugs or (24 if kind == "truck" else 18)
    for i in range(nl):
        a = 2 * math.pi * i / nl
        if kind in ("tractor", "tractor_front"):
            # chevron bars: two angled bars meeting at the centre
            for side in (0, 1):
                xa = (0.1 if side == 0 else w * 0.5) + 0.0
                xb = w * 0.5 if side == 0 else w - 0.1
                da = (0.35 if side == 0 else 0.0) if kind == "tractor" else 0.0
                db = 0.0 if side == 0 else 0.35 if kind == "tractor" else 0.0
                p0 = (xa, math.sin(a + da) * tread_r, math.cos(a + da) * tread_r)
                p1 = (xb, math.sin(a + db) * tread_r, math.cos(a + db) * tread_r)
                _lug(mb, p0, p1, a, 1.15 if kind == "tractor" else 0.6, 0.9 if kind == "tractor" else 0.55)
        elif kind == "track":
            pass
        else:
            for k, (xa, xb) in enumerate(((0.2, w * 0.48), (w * 0.52, w - 0.2))):
                aa = a + (math.pi / nl if k else 0.0)
                p0 = (xa, math.sin(aa) * tread_r, math.cos(aa) * tread_r)
                p1 = (xb, math.sin(aa) * tread_r, math.cos(aa) * tread_r)
                _lug(mb, p0, p1, aa, 0.55 if kind != "small" else 0.35, 2 * math.pi * tread_r / nl * 0.42)
    # rim: dish, centre hub, nuts
    xr = w - 0.6
    mb.lathe([(rim - 0.2, 1.0), (rim, 0.75), (rim - 0.6, 0.62), (rim * 0.55, 0.6), (rim * 0.5, 0.0)],
             (0.3, 0, 0), (xr, 0, 0), rim_mat, seg=seg // 2 * 2, cap1=False)
    mb.lathe([(rim * 0.5, 0.0), (rim * 0.5, 1.0)], (xr - 0.6, 0, 0), (xr - 0.05, 0, 0), rim_mat, seg=20, cap1=rim_mat)
    hub = rim * 0.38
    mb.lathe([(hub, 0.0), (hub, 0.6), (hub * 0.75, 1.0), (hub * 0.3, 1.0)], (xr - 0.1, 0, 0), (xr + 0.9, 0, 0), hub_mat, seg=16, cap1=hub_mat)
    nn = 10 if kind == "truck" else 8 if kind.startswith("tractor") else 5
    for i in range(nn):
        a = 2 * math.pi * i / nn
        rr = rim * 0.46
        p = (xr, math.sin(a) * rr, math.cos(a) * rr)
        mb.cyl(p, (xr + 0.45, p[1], p[2]), 0.32, "chrome", 6)
    if kind in ("truck", "tractor"):
        # hand holes in the dish
        for i in range(6 if kind == "truck" else 8):
            a = 2 * math.pi * (i + 0.5) / (6 if kind == "truck" else 8)
            rr = rim * 0.72
            mb.cyl((xr - 0.5, math.sin(a) * rr, math.cos(a) * rr), (xr - 0.18, math.sin(a) * rr, math.cos(a) * rr), rim * 0.12, "void", 8)
    ob = mb.finish(name="W_" + key, smooth=40)
    me = ob.data
    bpy.data.objects.remove(ob)
    me.name = "W_" + key
    return me


def _lug(mb, p0, p1, a, h, wdt):
    """Tread lug block from p0 to p1 (across the tyre), sticking out radially by h voxels."""
    p0, p1 = Vector(p0), Vector(p1)
    radial = Vector((0, math.sin(a), math.cos(a)))
    tang = Vector((0, math.cos(a), -math.sin(a)))
    across = (p1 - p0)
    L = across.length
    xa = across.normalized()
    t2 = radial.cross(xa).normalized()
    c = (p0 + p1) / 2 + radial * (h / 2)
    # build a box with axes (xa, radial, t2) in game space, convert to Blender
    bx, by, bz = D(*xa).normalized(), D(*radial).normalized(), D(*t2).normalized()
    R = Matrix((bx, by, bz)).transposed().to_4x4()
    M = mb.T @ Matrix.Translation(P(*c)) @ R @ Matrix.Diagonal((L * S, h * S, wdt * S, 1))
    r = bmesh.ops.create_cube(mb.bm, size=1.0, matrix=M)
    mb._post(r["verts"], "tyre", 0.006)


def add_wheels(root, specs, kind_key="wheel_truck", R=6.7, w=5, rim=4.0, kind="truck", rim_mat="steel", dual=False):
    """specs: [(socket, x, y, z, mirror_pair)] like VehicleDesign.Socket. Creates Wheel_<socket>_R/_L instances."""
    me = wheel_mesh(kind_key, R, w, rim, kind, rim_mat)
    out = []
    for (sock, x, y, z, pair) in specs:
        base = sock.replace("wheel", "Wheel")
        out.append(instance_mesh(me, base + ("_R" if pair else ""), root, (x, y, z), False,
                                 {"socket": sock, "part": kind_key, "category": "Wheel", "radius_m": R * S}))
        if pair:
            out.append(instance_mesh(me, base + "_L", root, (-x, y, z), True,
                                     {"socket": sock + "_L", "part": kind_key, "category": "Wheel", "radius_m": R * S}))
    return out


def instance_mesh(me, name, parent, origin, mirror, props):
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    attach(ob, parent, P(*origin))
    if mirror:
        ob.scale.x = -1
    for k, v in props.items():
        ob[k] = v
    return ob


def track_assembly(root, side, x_in, width, z_front, z_rear, y_axle, R=4.3, top_y=None, name=None, links=None):
    """Crawler track for one side (side = +1 right, -1 left): belt of link plates with grousers around an idler
    (front) and drive sprocket (rear), road wheels and carrier rollers. Matches the game's wheel_track footprint:
    sockets at the inner face x_in, axle height y_axle, radius R."""
    mb = MB(name or ("Track_R" if side > 0 else "Track_L"))
    xs = (x_in, x_in + width)
    xc = (xs[0] + xs[1]) / 2 * side
    top = top_y if top_y is not None else y_axle + R
    # path: bottom straight (front->rear) at y_axle-R, rear arc, top straight at y_axle+R, front arc
    pts = []
    L1 = z_front - z_rear
    arc = math.pi * R
    total = 2 * L1 + 2 * arc
    n = links or int(total / 2.0)
    step = total / n
    for i in range(n):
        s = i * step
        if s < L1:                       # bottom, front -> rear
            p, ang = (z_front - s, y_axle - R), math.pi
        elif s < L1 + arc:               # rear arc going up
            t = (s - L1) / R
            p, ang = (z_rear - math.sin(t) * R, y_axle - math.cos(t) * R), math.pi + t
        elif s < 2 * L1 + arc:           # top, rear -> front
            p, ang = (z_rear + (s - L1 - arc), y_axle + R), 2 * math.pi
        else:
            t = (s - 2 * L1 - arc) / R
            p, ang = (z_front + math.sin(t) * R, y_axle + math.cos(t) * R), t
        pts.append((p, ang))
    for (z, y), ang in pts:
        # outward normal in (z,y) plane: bottom -> -y, rear arc -> -z ... ang measured from +y towards +z
        nz, ny = math.sin(ang), math.cos(ang)
        tz, ty = ny, -nz
        R4 = Matrix((D(1, 0, 0).normalized(), D(0, ny, nz).normalized(), D(0, ty, tz).normalized())).transposed().to_4x4()
        c = P(xc, y + ny * 0.2, z + nz * 0.2)
        M = Matrix.Translation(c) @ R4 @ Matrix.Diagonal(((width + 0.4) * S, 0.55 * S, step * 0.92 * S, 1))
        r = bmesh.ops.create_cube(mb.bm, size=1.0, matrix=M)
        mb._post(r["verts"], "steel_dark", 0.005)
        cg = P(xc, y + ny * 0.62, z + nz * 0.62)
        M = Matrix.Translation(cg) @ R4 @ Matrix.Diagonal(((width + 0.4) * S, 0.5 * S, 0.45 * S, 1))
        r = bmesh.ops.create_cube(mb.bm, size=1.0, matrix=M)
        mb._post(r["verts"], "steel", 0.004)
        # guide horn on the inside
        ch = P(xc, y - ny * 0.35, z - nz * 0.35)
        M = Matrix.Translation(ch) @ R4 @ Matrix.Diagonal((width * 0.25 * S, 0.6 * S, step * 0.5 * S, 1))
        r = bmesh.ops.create_cube(mb.bm, size=1.0, matrix=M)
        mb._post(r["verts"], "steel_dark", 0.0)
    xa, xb = xs[0] * side, xs[1] * side
    lo, hi = min(xa, xb), max(xa, xb)
    # idler (front): grooved wheel
    mb.lathe([(R * 0.5, 0), (R - 0.7, 0.05), (R - 0.7, 0.4), (R - 1.2, 0.45), (R - 1.2, 0.55), (R - 0.7, 0.6), (R - 0.7, 0.95), (R * 0.5, 1)],
             (lo + 0.4, y_axle, z_front), (hi - 0.4, y_axle, z_front), "steel", seg=24, cap0="steel", cap1="steel")
    # sprocket (rear): toothed wheel
    mb.lathe([(R * 0.4, 0), (R - 1.4, 0.0), (R - 1.4, 1.0), (R * 0.4, 1.0)],
             (lo + 0.6, y_axle, z_rear), (hi - 0.6, y_axle, z_rear), "steel", seg=24, cap0="rust", cap1="rust")
    nt = 11
    for i in range(nt):
        a = 2 * math.pi * i / nt
        yy, zz = math.cos(a), math.sin(a)
        mb.cyl(((lo + hi) / 2 - 0.5, y_axle + yy * (R - 1.4), z_rear + zz * (R - 1.4)),
               ((lo + hi) / 2 - 0.5, y_axle + yy * (R - 0.5), z_rear + zz * (R - 0.5)), 0.55, "steel_bare", 6, r2=0.3)
    # road wheels and carrier rollers
    nr = max(3, int((z_front - z_rear) / 7.5))
    for i in range(1, nr + 1):
        z = z_rear + (z_front - z_rear) * i / (nr + 1)
        mb.lathe([(1.0, 0), (R * 0.55, 0.0), (R * 0.55, 0.42), (R * 0.4, 0.5), (R * 0.55, 0.58), (R * 0.55, 1.0), (1.0, 1.0)],
                 (lo + 0.3, y_axle - R + 0.75 + R * 0.55, z), (hi - 0.3, y_axle - R + 0.75 + R * 0.55, z), "steel", seg=16, cap0="rust", cap1="rust")
    for z in (z_rear + (z_front - z_rear) * 0.33, z_rear + (z_front - z_rear) * 0.66):
        mb.cyl((lo + 0.8, y_axle + R - 1.1, z), (hi - 0.8, y_axle + R - 1.1, z), 0.9, "steel", 12)
    return mb.finish(parent=root, origin=(x_in * side, y_axle, z_rear),
                     props={"socket": "tracks", "part": "wheel_track", "category": "Wheel", "note": "replaces CrawlerTracks link/road-wheel visuals"})


# ------------------------------------------------------------------------------------------------ furniture (walk-in interiors)
def furniture(mb, kind, pos, yaw=0.0):
    """Interior furniture pieces in game-local units: origin = floor mount point, +Z front (FurnitureLibrary rules)."""
    mb.frame(pos, yaw)
    if kind == "bed":
        mb.vbox(-5, 0, -12, 5, 2, 12, "steel")
        mb.box((-5, 3, -12), (5, 5.5, 12), "mattress", 0.03, 2)
        mb.box((-4, 5.5, -11.5), (4, 6.8, -8), "fabric", 0.04, 2)          # pillow
        mb.box((-5.2, 4.8, -2), (5.2, 6.0, 12.3), "fabric_blue", 0.03, 2)    # blanket
        mb.box((-5.5, 0, -12.6), (5.5, 9, -11.8), "wood", 0.01)               # headboard
    elif kind == "fridge":
        mb.box((-4.5, 0, -3.5), (4.5, 22, 3.5), "white_enamel", 0.04, 2)
        mb.seam((-4.5, 14, 3.5), (4.5, 14, 3.5), (0, 0, 1))
        mb.box((2.8, 15, 3.5), (3.4, 20, 4.1), "chrome", 0.01)
        mb.box((2.8, 6, 3.5), (3.4, 12, 4.1), "chrome", 0.01)
    elif kind == "workbench":
        mb.box((-8, 10, -4), (8, 11, 4), "wood", 0.01)
        for x in (-7.5, 7.5):
            for z in (-3.5, 3.5):
                mb.box((x - 0.4, 0, z - 0.4), (x + 0.4, 10, z + 0.4), "steel", 0.0)
        mb.box((-7.5, 3, -3.5), (7.5, 3.6, 3.5), "wood", 0.0)
        mb.box((-6, 11, -1), (-2, 13, 2), "rust", 0.01)                       # vice / tool box
        mb.cyl((3, 11, 0), (3, 12.5, 0), 1.2, "steel_dark", 10)
        mb.box((-8, 11, -4), (8, 20, -3.5), "grating", 0.0)                   # pegboard
    elif kind == "locker":
        mb.box((-4, 0, -3), (4, 22, 3), "paint_olive", 0.02)
        mb.seam((0, 1, 3), (0, 21, 3), (0, 0, 1))
        for x in (-2, 2):
            for y in (17, 18, 19):
                mb.box((x - 1.2, y, 3), (x + 1.2, y + 0.3, 3.1), "void", 0.0)
    elif kind == "stove":
        mb.box((-4, 0, -3.5), (4, 11, 3.5), "white_enamel", 0.03)
        mb.box((-3, 2, 3.5), (3, 7, 3.7), "glass", 0.0)
        for x in (-2, 2):
            for z in (-1.5, 1.5):
                mb.cyl((x, 11, z), (x, 11.3, z), 1.2, "black", 10)
        mb.cyl((0, 11, -3.5), (0, 24, -3.5), 0.9, "steel_dark", 8)
    elif kind == "crate":
        mb.box((-4, 0, -4), (4, 7, 4), "wood", 0.03)
        for z in (-4.05, 4.05):
            mb.box((-4.1, 0.5, z - 0.1), (4.1, 1.2, z + 0.1), "wood_light", 0.0)
            mb.box((-4.1, 5.8, z - 0.1), (4.1, 6.5, z + 0.1), "wood_light", 0.0)
    elif kind == "sofa":
        mb.box((-10, 0, -3.5), (10, 4.5, 3.5), "fabric", 0.08, 2)
        mb.box((-10, 4.5, -3.5), (10, 10, -1.5), "fabric", 0.08, 2)
        for x in (-10, 8.5):
            mb.box((x, 4.5, -3.5), (x + 1.5, 7, 3.5), "fabric", 0.06, 2)
    elif kind == "table":
        mb.box((-6, 9.2, -4), (6, 10, 4), "wood_light", 0.01)
        mb.cyl((0, 0, 0), (0, 9.2, 0), 0.6, "chrome", 10)
        mb.cyl((0, 0, 0), (0, 0.3, 0), 3, "steel_dark", 12)
    elif kind == "sink":
        mb.box((-4, 0, -3.5), (4, 11, 3.5), "wood", 0.02)
        mb.box((-3, 10.5, -2.5), (3, 11.2, 2.5), "steel_bare", 0.01)
        mb.cyl((0, 11, -2.6), (0, 14, -2.6), 0.4, "chrome", 8)
        mb.cyl((0, 14, -2.6), (0, 14, -1.0), 0.35, "chrome", 8)
    elif kind == "lamp":       # hanging from the ceiling: pos is the ceiling point
        mb.cyl((0, 0, 0), (0, -2.5, 0), 0.15, "black", 6)
        mb.lathe([(0.3, 0), (2.2, 0.8), (2.4, 1.0)], (0, -2.5, 0), (0, -4.0, 0), "paint_olive", seg=14)
        mb.ball((0, -3.8, 0), 0.9, "lightw", 10, 6)
    elif kind == "shelf":       # wall shelf: pos on the wall, front faces +X after yaw
        mb.box((-6, 0, 0), (6, 0.5, 3), "wood", 0.01)
        mb.box((-6, 4, 0), (6, 4.5, 3), "wood", 0.01)
        for x in (-5.5, 5.5):
            mb.box((x - 0.25, -1, 0), (x + 0.25, 4.5, 0.4), "steel_dark", 0.0)
        mb.box((-5, 0.5, 0.5), (-2, 2.6, 2.5), "rust", 0.01)
        mb.cyl((2, 0.5, 1.5), (2, 3.2, 1.5), 0.9, "paint_crimson", 10)
        mb.box((-1, 4.5, 0.4), (1.5, 6.5, 2.6), "paint_navy", 0.01)
    mb.reset()


# ------------------------------------------------------------------------------------------------ export / stats
def tris(objs):
    dg = bpy.context.evaluated_depsgraph_get()
    total = 0
    for o in objs:
        if o.type != "MESH":
            continue
        e = o.evaluated_get(dg)
        me = e.to_mesh()
        me.calc_loop_triangles()
        total += len(me.loop_triangles)
        e.to_mesh_clear()
    return total


def tree(root):
    return [root] + list(root.children_recursive)


def dims(root):
    lo, hi = rc.bounds(tree(root))
    # Blender -> game metres: x, y(up)=z, z(fwd)=-y
    return {"width_m": round(hi.x - lo.x, 3), "height_m": round(hi.z - lo.z, 3), "length_m": round(hi.y - lo.y, 3)}


def save_and_render(root, label, replaces, extra_tiles=()):
    """Save <Name>.blend, render the tile, append stats to heavy_stats.json."""
    name = root.name
    objs = tree(root)
    stats = {"name": name, "label": label, "replaces": replaces, "tris": tris(objs), **dims(root),
             "objects": sorted(o.name for o in objs if o.type == "MESH")}
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, name + ".blend"), compress=True)
    rc.render_tile(name, objs, OUT)
    for fn in extra_tiles:
        fn()
    path = os.path.join(OUT, "_stats_" + name + ".json")
    with open(path, "w") as f:
        json.dump(stats, f, indent=1)
    print("STATS", json.dumps(stats))
    return stats


def new_scene():
    _MATS.clear()
    rc.reset_scene()
    sc = rc.setup_stage()
    return sc


def cutaway_tile(root, tile, cut_y, label, hide=("Roof",), azim=45.0):
    """Doll's-house cutaway like the game's isometric view: bisect every mesh of the vehicle at game height cut_y
    (voxels) and drop what is above; render as `tile`."""
    zcut = cut_y * S
    objs = tree(root)
    shown = []
    for o in objs:
        if o.type != "MESH":
            continue
        if any(o.name.startswith(h) for h in hide):
            continue
        mw = o.matrix_world
        lo = min((mw @ Vector(c)).z for c in o.bound_box)
        if lo >= zcut:
            continue
        hi = max((mw @ Vector(c)).z for c in o.bound_box)
        if hi <= zcut:
            shown.append(o)
            continue
        me = o.data.copy()
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.transform(mw)
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, zcut), plane_no=(0, 0, 1), clear_outer=True)
        bm.transform(mw.inverted())
        bm.to_mesh(me)
        bm.free()
        c = bpy.data.objects.new(o.name + "_cut", me)
        c.matrix_world = mw.copy()
        bpy.context.scene.collection.objects.link(c)
        shown.append(c)
    rc.render_tile(tile, shown, OUT, azim=azim)
    for o in shown:
        if o.name.endswith("_cut"):
            bpy.data.objects.remove(o)
