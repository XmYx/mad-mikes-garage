"""Modelling kit for the HD misc group (bikes, aircraft, boats, parts, props, furniture).

Geometry is generated with bmesh primitives (bevels baked in, no live modifiers), merged per named part into one
mesh object with several material slots: what a game importer wants (Body, Wheel_*, Glass, Prop, Rotor ...).
Materials are procedural node trees in a hand-painted style (paint + chips + rust + dust, wood grain, canvas,
rubber, chrome, glass, concrete, brick). For Unity they are baked to textures (see the report).

Axes: Blender Z up, the asset's front faces Blender -Y (render_common convention). Game voxel coords (x right,
y up, z forward, 1 voxel = 0.08 m) convert with V(x, y, z).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import render_common as rc  # noqa: E402

VOX = 0.08


def V(x, y, z, s=VOX):
    """Game voxel coords -> Blender metres."""
    return Vector((x * s, -z * s, y * s))


def G(x, y, z):
    """Game metres -> Blender metres."""
    return Vector((x, -z, y))


# ----------------------------------------------------------------------------------------------- materials
def _hex(h):
    return rc.srgb(h)


def P(name, i=-1):
    return rc.pal(name, i)


def _mix_rgba(nt, fac, a, b):
    m = nt.nodes.new("ShaderNodeMix")
    m.data_type = "RGBA"
    ins = {s.identifier: s for s in m.inputs}
    if isinstance(fac, (int, float)):
        ins["Factor_Float"].default_value = fac
    else:
        nt.links.new(fac, ins["Factor_Float"])
    for key, v in (("A_Color", a), ("B_Color", b)):
        if isinstance(v, tuple):
            ins[key].default_value = v
        else:
            nt.links.new(v, ins[key])
    return [s for s in m.outputs if s.identifier == "Result_Color"][0]


def _math(nt, op, a, b=0.0, clamp=False):
    m = nt.nodes.new("ShaderNodeMath")
    m.operation = op
    m.use_clamp = clamp
    for i, v in enumerate((a, b)):
        if isinstance(v, (int, float)):
            m.inputs[i].default_value = v
        else:
            nt.links.new(v, m.inputs[i])
    return m.outputs[0]


def _ramp(nt, src, stops):
    r = nt.nodes.new("ShaderNodeValToRGB")
    nt.links.new(src, r.inputs[0])
    els = r.color_ramp.elements
    while len(els) > 2:
        els.remove(els[-1])
    for i, (pos, col) in enumerate(stops):
        e = els[i] if i < 2 else els.new(pos)
        e.position = pos
        e.color = col if len(col) == 4 else (*col, 1)
    return r.outputs[0]


def _coords(nt, scale=1.0, stretch=(1, 1, 1)):
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (scale * stretch[0], scale * stretch[1], scale * stretch[2])
    nt.links.new(tc.outputs["Object"], mp.inputs["Vector"])
    return mp.outputs[0], tc


def _noise(nt, vec, scale, detail=4.0, rough=0.55, distort=0.0, out="Fac", dim="3D"):
    n = nt.nodes.new("ShaderNodeTexNoise")
    n.noise_dimensions = dim
    n.inputs["Scale"].default_value = scale
    n.inputs["Detail"].default_value = detail
    n.inputs["Roughness"].default_value = rough
    n.inputs["Distortion"].default_value = distort
    nt.links.new(vec, n.inputs["W"] if dim == "1D" else n.inputs["Vector"])
    return n.outputs[out]


def _new_mat(name):
    m = bpy.data.materials.get(name)
    if m:
        return m, None
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    return m, m.node_tree


def surface(name, base, *, chips=0.0, chip_col=None, rust=0.0, dust=0.35, metal=0.0, rough=0.55, var=0.12,
            grain=None, bump=0.25, scale=1.0, streaks=0.0, emit=None, alpha=None, clearcoat=0.0, dark=None):
    """Hand-painted surface: base colour with soft blotchy variation, paint chips showing `chip_col`, rust patches,
    vertical rust streaks, dust settling low on the object. `base` is an RGBA (linear) tuple."""
    m, nt = _new_mat(name)
    if nt is None:
        return m
    b = nt.nodes["Principled BSDF"]
    vec, tc = _coords(nt, scale)
    dark = dark or tuple(c * 0.62 for c in base[:3]) + (1,)
    light = tuple(min(1, c * 1.25 + 0.01) for c in base[:3]) + (1,)
    blot = _noise(nt, vec, 2.2, 3, 0.5)
    col = _ramp(nt, blot, [(0.3, tuple(c * (1 - var) for c in base[:3]) + (1,)), (0.5, base),
                           (0.72, tuple(min(1, c * (1 + var * 1.6)) for c in base[:3]) + (1,))])
    rough_s = None
    if grain:  # (direction 'X'/'Y'/'Z', frequency, contrast): wood / brushed lines
        axis, freq, contrast = grain
        st = {"X": (0.15, 1, 1), "Y": (1, 0.15, 1), "Z": (1, 1, 0.15)}[axis]
        gv, _ = _coords(nt, scale, st)
        w = nt.nodes.new("ShaderNodeTexWave")
        w.wave_type = "RINGS"
        w.rings_direction = {"X": "X", "Y": "Y", "Z": "Z"}[axis]
        w.inputs["Scale"].default_value = freq
        w.inputs["Distortion"].default_value = 6.0
        w.inputs["Detail"].default_value = 3.0
        nt.links.new(gv, w.inputs["Vector"])
        g = _ramp(nt, w.outputs["Fac"], [(0.2, (1, 1, 1, 1)), (0.85, (1 - contrast, 1 - contrast, 1 - contrast, 1))])
        mm = nt.nodes.new("ShaderNodeMix")
        mm.data_type = "RGBA"
        mm.blend_type = "MULTIPLY"
        ins = {s.identifier: s for s in mm.inputs}
        ins["Factor_Float"].default_value = 1.0
        nt.links.new(col, ins["A_Color"])
        nt.links.new(g, ins["B_Color"])
        col = [s for s in mm.outputs if s.identifier == "Result_Color"][0]
    if chips > 0:
        cn = _noise(nt, vec, 9.0, 6, 0.7, 0.3)
        cmask = _ramp(nt, cn, [(0.72 - chips * 0.25, (0, 0, 0, 1)), (0.74 - chips * 0.25, (1, 1, 1, 1))])
        cc = chip_col or P("metal", 2)
        col = _mix_rgba(nt, cmask, col, cc)
    if rust > 0:
        rv, _ = _coords(nt, scale)
        rn = _noise(nt, rv, 3.2, 8, 0.65, 0.6)
        rmask = _ramp(nt, rn, [(0.66 - rust * 0.3, (0, 0, 0, 1)), (0.72 - rust * 0.3, (1, 1, 1, 1))])
        rcol = _ramp(nt, _noise(nt, rv, 14, 4, 0.6), [(0.35, P("rust", 1)), (0.65, P("rust", 3))])
        col = _mix_rgba(nt, rmask, col, rcol)
        rough_s = rmask
    if streaks > 0:
        sv, _ = _coords(nt, scale, (6.0, 6.0, 0.35))
        sn = _noise(nt, sv, 3.0, 2, 0.5)
        smask = _ramp(nt, sn, [(0.6 - streaks * 0.2, (0, 0, 0, 1)), (0.75, (0.75, 0.75, 0.75, 1))])
        col = _mix_rgba(nt, smask, col, P("rust", 2))
    if dust > 0:
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(tc.outputs["Object"], sep.inputs[0])
        low = _math(nt, "SUBTRACT", 0.45, sep.outputs["Z"], clamp=True)
        dn = _noise(nt, vec, 5, 5, 0.6)
        dmask = _math(nt, "MULTIPLY", _math(nt, "ADD", _math(nt, "MULTIPLY", low, 1.2), _math(nt, "MULTIPLY", dn, 0.5)), dust, clamp=True)
        dmask = _ramp(nt, dmask, [(0.45, (0, 0, 0, 1)), (0.95, (0.85, 0.85, 0.85, 1))])
        col = _mix_rgba(nt, dmask, col, _hex("b8946c"))
    nt.links.new(col, b.inputs["Base Color"])
    b.inputs["Metallic"].default_value = metal
    if rough_s is not None:
        rm = nt.nodes.new("ShaderNodeMapRange")
        rm.inputs["From Min"].default_value = 0
        rm.inputs["From Max"].default_value = 1
        rm.inputs["To Min"].default_value = rough
        rm.inputs["To Max"].default_value = 0.92
        sepc = nt.nodes.new("ShaderNodeRGBToBW")
        nt.links.new(rough_s, sepc.inputs[0])
        nt.links.new(sepc.outputs[0], rm.inputs["Value"])
        nt.links.new(rm.outputs[0], b.inputs["Roughness"])
        if metal > 0:
            mr = nt.nodes.new("ShaderNodeMapRange")
            mr.inputs["To Min"].default_value = metal
            mr.inputs["To Max"].default_value = 0.0
            nt.links.new(sepc.outputs[0], mr.inputs["Value"])
            nt.links.new(mr.outputs[0], b.inputs["Metallic"])
    else:
        b.inputs["Roughness"].default_value = rough
    if bump > 0:
        bn = _noise(nt, vec, 30, 6, 0.6)
        bp = nt.nodes.new("ShaderNodeBump")
        bp.inputs["Strength"].default_value = bump
        bp.inputs["Distance"].default_value = 0.02
        nt.links.new(bn, bp.inputs["Height"])
        nt.links.new(bp.outputs["Normal"], b.inputs["Normal"])
    if clearcoat:
        b.inputs["Coat Weight"].default_value = clearcoat
    if emit is not None:
        b.inputs["Emission Color"].default_value = emit
        b.inputs["Emission Strength"].default_value = 4.0
    if alpha is not None:
        b.inputs["Alpha"].default_value = alpha
        m.surface_render_method = "DITHERED"
    return m


def glass(name="Glass", tint=None):
    m, nt = _new_mat(name)
    if nt is None:
        return m
    b = nt.nodes["Principled BSDF"]
    vec, tc = _coords(nt, 1.0)
    dirt = _ramp(nt, _noise(nt, vec, 4, 5, 0.6), [(0.45, tint or P("glass", 2)), (0.75, _hex("8a7a64"))])
    nt.links.new(dirt, b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.08
    b.inputs["Metallic"].default_value = 0.3
    b.inputs["Alpha"].default_value = 0.72
    b.inputs["Coat Weight"].default_value = 0.6
    m.surface_render_method = "DITHERED"
    return m


def emissive(name, col, strength=5.0):
    m, nt = _new_mat(name)
    if nt is None:
        return m
    b = nt.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = col
    b.inputs["Emission Color"].default_value = col
    b.inputs["Emission Strength"].default_value = strength
    b.inputs["Roughness"].default_value = 0.2
    return m


def textured(name, kind, c1, c2, c3=None, scale=1.0, rough=0.8, dust=0.3, bump=0.4, rot=None):
    """Patterned surfaces: 'brick', 'planks', 'corrugated', 'concrete', 'canvas', 'stone', 'leaf', 'bark', 'tread', 'hay'."""
    m, nt = _new_mat(name)
    if nt is None:
        return m
    b = nt.nodes["Principled BSDF"]
    vec, tc = _coords(nt, scale)
    height = None
    if kind == "brick":
        br = nt.nodes.new("ShaderNodeTexBrick")
        br.inputs["Scale"].default_value = 1.0
        br.inputs["Mortar Size"].default_value = 0.012
        br.inputs["Brick Width"].default_value = 0.23
        br.inputs["Row Height"].default_value = 0.075
        br.offset = 0.5
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(tc.outputs["Object"], sep.inputs[0])
        comb = nt.nodes.new("ShaderNodeCombineXYZ")
        nt.links.new(_math(nt, "ADD", sep.outputs["X"], sep.outputs["Y"]), comb.inputs["X"])
        nt.links.new(sep.outputs["Z"], comb.inputs["Y"])
        nt.links.new(comb.outputs[0], br.inputs["Vector"])
        br.inputs["Color1"].default_value = c1
        br.inputs["Color2"].default_value = c2
        br.inputs["Mortar"].default_value = c3 or _hex("9a9080")
        col = br.outputs["Color"]
        height = _math(nt, "SUBTRACT", 1.0, br.outputs["Fac"])
        n = _noise(nt, vec, 6, 4, 0.6)
        col = _mix_rgba(nt, _ramp(nt, n, [(0.55, (0, 0, 0, 1)), (0.75, (0.6, 0.6, 0.6, 1))]), col, _hex("3a3028"))
    elif kind in ("planks", "corrugated", "canvas", "tread"):
        axis = rot or "X"
        w = nt.nodes.new("ShaderNodeTexWave")
        w.wave_type = "BANDS"
        w.bands_direction = axis
        w.wave_profile = "SAW" if kind == "planks" else "SIN"
        w.inputs["Scale"].default_value = {"planks": 5.0, "corrugated": 22.0, "canvas": 160.0, "tread": 40.0}[kind]
        w.inputs["Distortion"].default_value = 0.0 if kind != "planks" else 0.4
        nt.links.new(vec, w.inputs["Vector"])
        if kind == "planks":
            seam = _ramp(nt, w.outputs["Fac"], [(0.0, (0.15, 0.15, 0.15, 1)), (0.06, (1, 1, 1, 1)), (0.94, (1, 1, 1, 1)), (1.0, (0.3, 0.3, 0.3, 1))])
            gv, _ = _coords(nt, scale, {"X": (0.12, 1, 1), "Y": (1, 0.12, 1), "Z": (1, 1, 0.12)}.get(axis, (1, 1, 1)))
            gr = nt.nodes.new("ShaderNodeTexWave")
            gr.wave_type = "RINGS"
            gr.inputs["Scale"].default_value = 3.0
            gr.inputs["Distortion"].default_value = 8.0
            gr.inputs["Detail"].default_value = 3.0
            nt.links.new(gv, gr.inputs["Vector"])
            plank_id = _noise(nt, w.outputs["Color"], 40, 0, 0.5)
            base = _ramp(nt, plank_id, [(0.3, c1), (0.7, c2)])
            grain = _ramp(nt, gr.outputs["Fac"], [(0.3, (1, 1, 1, 1)), (0.9, (0.7, 0.7, 0.7, 1))])
            mm = nt.nodes.new("ShaderNodeMix")
            mm.data_type = "RGBA"
            mm.blend_type = "MULTIPLY"
            ins = {s.identifier: s for s in mm.inputs}
            ins["Factor_Float"].default_value = 1.0
            nt.links.new(base, ins["A_Color"])
            nt.links.new(grain, ins["B_Color"])
            col = [s for s in mm.outputs if s.identifier == "Result_Color"][0]
            mm2 = nt.nodes.new("ShaderNodeMix")
            mm2.data_type = "RGBA"
            mm2.blend_type = "MULTIPLY"
            ins = {s.identifier: s for s in mm2.inputs}
            ins["Factor_Float"].default_value = 1.0
            nt.links.new(col, ins["A_Color"])
            nt.links.new(seam, ins["B_Color"])
            col = [s for s in mm2.outputs if s.identifier == "Result_Color"][0]
            height = seam
        else:
            n = _noise(nt, vec, 3, 4, 0.6)
            col = _ramp(nt, n, [(0.3, c1), (0.7, c2)])
            if kind == "corrugated" and c3:
                sv, _ = _coords(nt, scale, (5, 5, 0.3))
                st = _ramp(nt, _noise(nt, sv, 2.5, 3, 0.5), [(0.5, (0, 0, 0, 1)), (0.7, (1, 1, 1, 1))])
                col = _mix_rgba(nt, st, col, c3)
            height = w.outputs["Fac"]
    elif kind in ("concrete", "stone"):
        n = _noise(nt, vec, 4 if kind == "concrete" else 1.4, 8, 0.65, 0.2)
        col = _ramp(nt, n, [(0.3, c1), (0.6, c2)] + ([(0.8, c3)] if c3 else []))
        vo = nt.nodes.new("ShaderNodeTexVoronoi")
        vo.feature = "DISTANCE_TO_EDGE"
        vo.inputs["Scale"].default_value = 2.5 if kind == "concrete" else 1.2
        nt.links.new(vec, vo.inputs["Vector"])
        crack = _ramp(nt, vo.outputs["Distance"], [(0.0, (0.25, 0.25, 0.25, 1)), (0.03, (1, 1, 1, 1))])
        mm = nt.nodes.new("ShaderNodeMix")
        mm.data_type = "RGBA"
        mm.blend_type = "MULTIPLY"
        ins = {s.identifier: s for s in mm.inputs}
        ins["Factor_Float"].default_value = 1.0
        nt.links.new(col, ins["A_Color"])
        nt.links.new(crack, ins["B_Color"])
        col = [s for s in mm.outputs if s.identifier == "Result_Color"][0]
        if kind == "stone":
            sep = nt.nodes.new("ShaderNodeSeparateXYZ")
            nt.links.new(vec, sep.inputs[0])
            strata = _ramp(nt, _noise(nt, sep.outputs["Z"], 3, 2, 0.5, dim="1D"), [(0.4, (0.85, 0.85, 0.85, 1)), (0.6, (1.1, 1.05, 1.0, 1))])
            mm = nt.nodes.new("ShaderNodeMix")
            mm.data_type = "RGBA"
            mm.blend_type = "MULTIPLY"
            ins = {s.identifier: s for s in mm.inputs}
            ins["Factor_Float"].default_value = 1.0
            nt.links.new(col, ins["A_Color"])
            nt.links.new(strata, ins["B_Color"])
            col = [s for s in mm.outputs if s.identifier == "Result_Color"][0]
        height = _math(nt, "MULTIPLY", n, vo.outputs["Distance"])
    else:  # leaf / bark / hay: blotchy two-three tone
        n = _noise(nt, vec, 8 if kind == "leaf" else 5, 6, 0.65, 0.5)
        col = _ramp(nt, n, [(0.35, c1), (0.6, c2)] + ([(0.8, c3)] if c3 else []))
        if kind == "bark":
            bv, _ = _coords(nt, scale, (8, 8, 0.6))
            height = _noise(nt, bv, 6, 4, 0.6)
            col = _mix_rgba(nt, _ramp(nt, height, [(0.4, (0, 0, 0, 1)), (0.6, (0.5, 0.5, 0.5, 1))]), col, c1)
        else:
            height = n
    if dust > 0:
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(tc.outputs["Object"], sep.inputs[0])
        low = _math(nt, "SUBTRACT", 0.5, sep.outputs["Z"], clamp=True)
        dn = _noise(nt, vec, 5, 5, 0.6)
        dmask = _math(nt, "MULTIPLY", _math(nt, "ADD", low, _math(nt, "MULTIPLY", dn, 0.4)), dust, clamp=True)
        col = _mix_rgba(nt, _ramp(nt, dmask, [(0.4, (0, 0, 0, 1)), (0.9, (0.8, 0.8, 0.8, 1))]), col, _hex("b8946c"))
    nt.links.new(col, b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = rough
    if height is not None and bump > 0:
        bp = nt.nodes.new("ShaderNodeBump")
        bp.inputs["Strength"].default_value = bump
        bp.inputs["Distance"].default_value = 0.02
        nt.links.new(height, bp.inputs["Height"])
        nt.links.new(bp.outputs["Normal"], b.inputs["Normal"])
    return m


def mats():
    """The shared material set (created once per scene)."""
    M = {}
    M["chrome"] = surface("Chrome", P("chrome", 2), rust=0.08, dust=0.2, metal=1.0, rough=0.16, var=0.05, bump=0.05)
    M["steel"] = surface("Steel", P("metal", 2), rust=0.3, dust=0.4, metal=0.85, rough=0.45, var=0.15, streaks=0.3)
    M["steel_dark"] = surface("SteelDark", P("metal", 1), rust=0.2, dust=0.35, metal=0.8, rough=0.5, var=0.12)
    M["iron"] = surface("CastIron", P("metal", 1), rust=0.25, dust=0.45, metal=0.6, rough=0.7, var=0.2, bump=0.6)
    M["alu"] = surface("Aluminium", _hex("8c8f94"), dust=0.35, metal=0.9, rough=0.35, var=0.1, bump=0.2)
    M["rust"] = surface("Rust", P("rust", 2), rust=0.6, dust=0.4, metal=0.2, rough=0.9, var=0.25, bump=0.8)
    M["black"] = surface("BlackPaint", P("black", 2), chips=0.3, dust=0.4, metal=0.2, rough=0.45, var=0.15)
    M["rubber"] = surface("Rubber", P("tire", 2), dust=0.3, rough=0.88, var=0.12, bump=0.5)
    M["tread"] = textured("TreadRubber", "tread", P("tire", 1), P("tire", 2), dust=0.3, rough=0.9, bump=0.8, rot="Z")
    M["glass"] = glass()
    M["lamp"] = emissive("LampWhite", _hex("fff2d0"), 3.0)
    M["lamp_amber"] = emissive("LampAmber", _hex("ffb040"), 3.0)
    M["lamp_red"] = emissive("LampRed", _hex("e02818"), 3.0)
    M["wood"] = surface("Wood", P("wood", 2), dust=0.35, rough=0.8, var=0.15, grain=("X", 4, 0.35))
    M["wood_dark"] = surface("WoodDark", P("wood", 1), dust=0.35, rough=0.85, var=0.15, grain=("X", 4, 0.35))
    M["wood_light"] = surface("WoodLight", P("wood", 3), dust=0.3, rough=0.75, var=0.12, grain=("X", 4, 0.3))
    M["canvas"] = textured("Canvas", "canvas", P("sand", 3), P("sand", 4), dust=0.3, rough=0.95, bump=0.2)
    M["leather"] = surface("Leather", P("wood", 1), dust=0.2, rough=0.55, var=0.25, bump=0.3)
    M["brass"] = surface("Brass", P("bronze", 3), rust=0.1, dust=0.3, metal=1.0, rough=0.3, var=0.1)
    M["copper"] = surface("Copper", P("bronze", 2), rust=0.15, dust=0.3, metal=1.0, rough=0.35, var=0.1)
    M["cream"] = surface("CreamPaint", P("cream", 2), chips=0.3, rust=0.15, dust=0.35, rough=0.5, var=0.1)
    M["concrete"] = textured("Concrete", "concrete", _hex("6e6b66"), _hex("8a857e"), _hex("9a958c"), dust=0.3, rough=0.92, bump=0.5)
    M["tarp"] = textured("Tarp", "canvas", P("olive", 2), P("olive", 3), dust=0.35, rough=0.95, bump=0.3)
    M["hazard"] = surface("HazardYellow", _hex("d4b020"), chips=0.4, rust=0.2, dust=0.35, rough=0.5)
    M["ochre"] = surface("OchrePaint", P("ochre", 3), chips=0.35, rust=0.2, dust=0.35, rough=0.5)
    M["crimson"] = surface("CrimsonPaint", P("crimson", 3), chips=0.3, rust=0.15, dust=0.3, rough=0.45, clearcoat=0.3)
    M["navy"] = surface("NavyPaint", P("navy", 2), chips=0.35, rust=0.2, dust=0.35, rough=0.5)
    M["olive"] = surface("OlivePaint", P("riggreen", 3), chips=0.35, rust=0.25, dust=0.4, rough=0.6)
    M["leaf"] = textured("Leaf", "leaf", P("moss", 1), P("moss", 3), P("olive", 3), dust=0.25, rough=0.8, bump=0.6)
    M["lamp_green"] = emissive("LampGreen", _hex("58e070"), 3.0)
    M["rope"] = textured("Rope", "canvas", P("sand", 2), P("sand", 4), dust=0.2, rough=0.95)
    return M


# ----------------------------------------------------------------------------------------------- geometry
def _bevel(bm, width, segs=2, angle=30.0):
    if width <= 0:
        return
    edges = [e for e in bm.edges if e.is_manifold and e.calc_face_angle(0) > math.radians(angle)]
    if edges:
        bmesh.ops.bevel(bm, geom=edges, offset=width, segments=segs, profile=0.5, affect="EDGES", clamp_overlap=True)


def bm_box(sx, sy, sz, bevel=0.0, segs=2):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz))
    _bevel(bm, min(bevel, min(sx, sy, sz) * 0.45), segs)
    return bm


def bm_cyl(r, h, segs=24, bevel=0.0, r2=None, caps=True):
    """Cylinder / cone along +Z, centred."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=caps, cap_tris=False, segments=segs, radius1=r, radius2=r if r2 is None else r2, depth=h)
    _bevel(bm, min(bevel, h * 0.45, r * 0.45), 2)
    return bm


def bm_sphere(r, u=16, v=10):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=r)
    return bm


def bm_lathe(profile, segs=32, axis="Z", close=False):
    """Revolve [(radius, height)] about +Z (or X / Y). close=True caps first/last if radius > 0."""
    bm = bmesh.new()
    rings = []
    for (r, h) in profile:
        ring = []
        for i in range(segs):
            a = i / segs * math.tau
            ring.append(bm.verts.new((math.cos(a) * r, math.sin(a) * r, h)))
        rings.append(ring)
    for a_ring, b_ring in zip(rings, rings[1:]):
        for i in range(segs):
            j = (i + 1) % segs
            bm.faces.new((a_ring[i], a_ring[j], b_ring[j], b_ring[i]))
    if close:
        for ring, flip in ((rings[0], True), (rings[-1], False)):
            if ring[0].co.xy.length > 1e-5:
                f = bm.faces.new(list(reversed(ring)) if flip else ring)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    _orient(bm, axis)
    return bm


def _orient(bm, axis):
    if axis == "X":
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, "Y"))
    elif axis == "Y":
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(-90), 3, "X"))


def bm_tube(points, r, segs=10, caps=True):
    """Sweep a circle along a polyline (parallel transport). r = float or list per point."""
    pts = [Vector(p) for p in points]
    radii = r if isinstance(r, (list, tuple)) else [r] * len(pts)
    bm = bmesh.new()
    rings = []
    tangent = (pts[1] - pts[0]).normalized()
    ref = Vector((0, 0, 1)) if abs(tangent.z) < 0.9 else Vector((1, 0, 0))
    nrm = tangent.cross(ref).normalized()
    for i, p in enumerate(pts):
        if i == 0:
            t = pts[1] - pts[0]
        elif i == len(pts) - 1:
            t = pts[-1] - pts[-2]
        else:
            t = (pts[i + 1] - pts[i]).normalized() + (pts[i] - pts[i - 1]).normalized()
        t.normalize()
        nrm = (nrm - t * nrm.dot(t)).normalized()
        bi = t.cross(nrm)
        ring = []
        for k in range(segs):
            a = k / segs * math.tau
            ring.append(bm.verts.new(p + (nrm * math.cos(a) + bi * math.sin(a)) * radii[i]))
        rings.append(ring)
    for a_ring, b_ring in zip(rings, rings[1:]):
        for k in range(segs):
            j = (k + 1) % segs
            bm.faces.new((a_ring[k], a_ring[j], b_ring[j], b_ring[k]))
    if caps:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def bm_loft(sections, close_ends=True):
    """Skin a list of equal-length closed loops (lists of 3D points)."""
    bm = bmesh.new()
    rings = [[bm.verts.new(p) for p in sec] for sec in sections]
    n = len(sections[0])
    for a_ring, b_ring in zip(rings, rings[1:]):
        for k in range(n):
            j = (k + 1) % n
            bm.faces.new((a_ring[k], a_ring[j], b_ring[j], b_ring[k]))
    if close_ends:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def bm_torus(R, r, segs=32, rsegs=10):
    prof = [(R + r * math.cos(a), r * math.sin(a)) for a in [i / rsegs * math.tau for i in range(rsegs + 1)]]
    return bm_lathe(prof, segs)


def bm_grid(pts2d, z0, z1):
    """Extrude a closed 2D polygon (XY) from z0 to z1."""
    return bm_loft([[(x, y, z0) for x, y in pts2d], [(x, y, z1) for x, y in pts2d]])


def bm_rock(r, seed=0, squash=0.7, detail=3):
    """Faceted lumpy rock / foliage blob: icosphere displaced by deterministic noise."""
    import mathutils.noise as mn
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=detail, radius=r)
    off = Vector((seed * 7.31, seed * 3.17, seed * 1.93))
    for v in bm.verts:
        n = mn.noise(v.co / r * 1.3 + off) * 0.28 + mn.noise(v.co / r * 3.1 + off) * 0.12
        v.co = v.co * (1 + n)
        v.co.z *= squash
    return bm


class Part:
    """Accumulates primitives into one mesh object with material slots."""

    def __init__(self, name):
        self.name = name
        self.verts = []
        self.faces = []
        self.fmat = []
        self.mats = []

    def add(self, bm, mat, loc=(0, 0, 0), rot=(0, 0, 0), scale=None, matrix=None):
        if mat not in self.mats:
            self.mats.append(mat)
        mi = self.mats.index(mat)
        if matrix is None:
            matrix = Matrix.Translation(Vector(loc)) @ Matrix.Rotation(rot[2], 4, "Z") @ Matrix.Rotation(rot[1], 4, "Y") @ Matrix.Rotation(rot[0], 4, "X")
            if scale is not None:
                matrix = matrix @ Matrix.Diagonal((*scale, 1))
        base = len(self.verts)
        bm.verts.index_update()
        for v in bm.verts:
            self.verts.append(tuple(matrix @ v.co))
        for f in bm.faces:
            self.faces.append(tuple(base + v.index for v in f.verts))
            self.fmat.append(mi)
        bm.free()
        return self

    # convenience wrappers (all take Blender-space centre / rotation)
    def box(self, mat, size, loc, rot=(0, 0, 0), bevel=0.006, segs=2):
        return self.add(bm_box(*size, bevel=bevel, segs=segs), mat, loc, rot)

    def cyl(self, mat, r, h, loc, axis="Z", segs=24, bevel=0.004, r2=None, rot=None, caps=True):
        bm = bm_cyl(r, h, segs, bevel, r2, caps)
        _orient(bm, axis)
        return self.add(bm, mat, loc, rot or (0, 0, 0))

    def tube(self, mat, pts, r, segs=10, caps=True):
        return self.add(bm_tube(pts, r, segs, caps), mat)

    def lathe(self, mat, profile, loc, axis="Z", segs=32, close=False, rot=(0, 0, 0)):
        return self.add(bm_lathe(profile, segs, axis, close), mat, loc, rot)

    def sphere(self, mat, r, loc, u=16, v=10, scale=None):
        return self.add(bm_sphere(r, u, v), mat, loc, scale=scale)

    def build(self, parent=None, smooth=True, angle=48.0, loc=None):
        me = bpy.data.meshes.new(self.name)
        me.from_pydata(self.verts, [], self.faces)
        me.validate(clean_customdata=False)
        for m in self.mats:
            me.materials.append(m)
        if self.fmat:
            me.polygons.foreach_set("material_index", self.fmat[:len(me.polygons)])
        me.update()
        if smooth:
            me.shade_smooth()
            try:
                me.set_sharp_from_angle(angle=math.radians(angle))
            except AttributeError:
                pass
        ob = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(ob)
        if parent is not None:
            ob.parent = parent
        if loc is not None:
            ob.location = loc
        return ob


def empty(name, loc=(0, 0, 0), parent=None):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = 0.3
    e.location = loc
    bpy.context.scene.collection.objects.link(e)
    if parent:
        e.parent = parent
    return e


def recentre(ob, pivot):
    """Move an object's origin to `pivot` (world) without moving its geometry (spinners pivot at their hub)."""
    pivot = Vector(pivot)
    ob.data.transform(Matrix.Translation(-(pivot - ob.matrix_world.translation)))
    ob.location = pivot if ob.parent is None else ob.parent.matrix_world.inverted() @ pivot


def tris(objs):
    n = 0
    for o in objs:
        for ob in [o] + list(o.children_recursive):
            if ob.type == "MESH":
                n += sum(len(p.vertices) - 2 for p in ob.data.polygons)
    return n


def dims(objs):
    lo, hi = rc.bounds([ob for o in objs for ob in [o] + list(o.children_recursive)])
    d = hi - lo
    # report in game terms: width (X), height (Z), length (Y)
    return round(d.x, 2), round(d.z, 2), round(d.y, 2)


# ----------------------------------------------------------------------------------------------- shared components
def car_wheel(name, M, R, rim_r, width, tread="street", rim="steel", rim_mat=None, loc=(0, 0, 0), side=1, parent=None, segs=40):
    """Car / truck wheel, axis along Blender X, outer face toward +X*side. R tyre radius, rim_r rim radius (m)."""
    p = Part(name)
    w = width
    td = {"street": 0.012, "sport": 0.006, "small": 0.012, "rain": 0.014, "offroad": 0.028, "mud": 0.04, "truck": 0.03, "aero": 0.008}.get(tread, 0.015)
    core = R - td
    sw = w * 0.5
    bulge = min((core - rim_r) * 0.18, w * 0.14)
    prof = [(rim_r - 0.005, -sw * 0.86), (rim_r + 0.01, -sw * 0.98), ((rim_r + core) * 0.5, -sw - bulge), (core - 0.02, -sw * 0.96),
            (core, -sw * 0.75), (core, sw * 0.75), (core - 0.02, sw * 0.96), ((rim_r + core) * 0.5, sw + bulge), (rim_r + 0.01, sw * 0.98), (rim_r - 0.005, sw * 0.86)]
    p.lathe(M["rubber"], prof, (0, 0, 0), "X", segs)
    # tread blocks
    tb = M["tread"]
    if tread in ("street", "small", "rain"):
        n = 36 if tread != "small" else 26
        for i in range(n):
            a = i / n * math.tau
            for row, off in ((-0.28, 0), (0.28, 0.5)):
                aa = a + off / n * math.tau
                bw = w * 0.32
                p.add(bm_box(bw, core * math.tau / n * 0.72, td * 2, bevel=td * 0.4, segs=1), tb,
                      (row * w, math.sin(aa) * (core + td * 0.0), math.cos(aa) * (core + td * 0.0)), (-aa, 0, 0))
            if tread == "rain":
                p.add(bm_box(w * 0.18, core * math.tau / n * 0.5, td * 2, bevel=td * 0.3, segs=1), tb, (0, math.sin(a) * core, math.cos(a) * core), (-a + 0.3, 0, 0))
    elif tread == "sport":
        for x in (-0.3, 0.3):
            p.lathe(tb, [(core + td, x * w - w * 0.12), (core + td, x * w + w * 0.12)], (0, 0, 0), "X", segs)
        p.lathe(tb, [(core + td * 0.6, -w * 0.12), (core + td * 0.6, w * 0.12)], (0, 0, 0), "X", segs)
    elif tread in ("offroad", "truck", "aero"):
        n = {"offroad": 22, "truck": 20, "aero": 18}[tread]
        for i in range(n):
            a = i / n * math.tau
            for row, off in ((-0.25, 0.0), (0.25, 0.5)):
                aa = a + off / n * math.tau
                bl = w * (0.46 if tread == "offroad" else 0.5)
                p.add(bm_box(bl, core * math.tau / n * 0.55, td * 2, bevel=td * 0.25, segs=1), tb,
                      (row * w * (1.05 if tread == "offroad" else 1.0), math.sin(aa) * core, math.cos(aa) * core), (-aa, 0, 0.18 if tread == "truck" else 0))
            if tread == "offroad":  # shoulder lugs
                for sgn in (-1, 1):
                    p.add(bm_box(td * 1.6, core * math.tau / n * 0.4, td * 2.4, bevel=td * 0.3, segs=1), tb,
                          (sgn * (sw + bulge * 0.4), math.sin(a + 0.08) * (core - td * 1.2), math.cos(a + 0.08) * (core - td * 1.2)), (-a - 0.08, 0, 0))
    elif tread == "mud":
        n = 14
        for i in range(n):
            a = i / n * math.tau
            for sgn in (-1, 1):
                aa = a + (0.5 / n * math.tau if sgn > 0 else 0)
                bm = bm_box(w * 0.46, core * math.tau / n * 0.36, td * 2, bevel=td * 0.2, segs=1)
                p.add(bm, tb, (sgn * w * 0.26, math.sin(aa) * core, math.cos(aa) * core), (-aa, 0, sgn * 0.35))
                p.add(bm_box(td * 1.7, core * math.tau / n * 0.4, td * 3.2, bevel=td * 0.25, segs=1), tb,
                      (sgn * (sw + bulge * 0.3), math.sin(aa) * (core - td * 1.5), math.cos(aa) * (core - td * 1.5)), (-aa, 0, 0))
    # rim barrel + face
    rm = rim_mat or M["steel"]
    face = sw * 0.35
    p.lathe(rm, [(rim_r - 0.004, -sw * 0.86), (rim_r + 0.008, -sw * 0.8), (rim_r - 0.012, -sw * 0.7), (rim_r - 0.014, sw * 0.6),
                 (rim_r + 0.008, sw * 0.8), (rim_r - 0.004, sw * 0.86), (rim_r - 0.02, sw * 0.82)], (0, 0, 0), "X", segs)
    hub_r = rim_r * 0.32
    if rim in ("steel", "truck"):
        dish = [(rim_r - 0.02, sw * 0.82), (rim_r * 0.82, face), (rim_r * 0.5, face + 0.01), (hub_r, face + 0.02), (hub_r * 0.4, face + 0.03), (0.0, face + 0.03)]
        if rim == "truck":
            dish = [(rim_r - 0.02, sw * 0.82), (rim_r * 0.9, -sw * 0.1), (rim_r * 0.5, -sw * 0.15), (hub_r * 1.1, -sw * 0.12), (hub_r * 0.8, sw * 0.4), (hub_r * 0.5, sw * 0.42), (0, sw * 0.42)]
        p.lathe(rm, dish, (0, 0, 0), "X", segs)
        holes = 8 if rim == "truck" else 6
        for i in range(holes):
            a = i / holes * math.tau + 0.3
            rr = rim_r * (0.66 if rim == "steel" else 0.62)
            x = face + 0.012 if rim == "steel" else -sw * 0.1
            p.add(bm_cyl(rim_r * 0.1, 0.012, 12), M["black"], (x, math.sin(a) * rr, math.cos(a) * rr), (0, math.radians(90), 0))
        p.lathe(M["chrome"], [(hub_r * 0.95, face + 0.015), (hub_r * 0.85, face + 0.04), (hub_r * 0.4, face + 0.05), (0, face + 0.052)], (0, 0, 0), "X", 20) if rim == "steel" else None
    else:
        p.lathe(rm, [(rim_r - 0.02, sw * 0.82), (rim_r - 0.03, sw * 0.5), (rim_r * 0.9, sw * 0.42)], (0, 0, 0), "X", segs)
        p.lathe(rm, [(hub_r, sw * 0.3), (hub_r, sw * 0.5), (hub_r * 0.6, sw * 0.56), (0, sw * 0.57)], (0, 0, 0), "X", 24, close=True)
        p.lathe(M["iron"], [(rim_r * 0.92, -sw * 0.5), (rim_r * 0.92, sw * 0.1), (0.0, sw * 0.1)], (0, 0, 0), "X", segs)  # dark inner disc
        n = {"5spoke": 5, "spoked": 10, "mag": 6, "beadlock": 8}.get(rim, 5)
        for i in range(n):
            a = i / n * math.tau
            mid = (rim_r * 0.9 + hub_r) * 0.5
            length = rim_r * 0.9 - hub_r
            p.add(bm_box(0.028, rim_r * (0.22 if n <= 6 else 0.1), length, bevel=0.008), rm, (sw * 0.42, math.sin(a) * mid, math.cos(a) * mid), (-a, 0, 0))
        if rim == "beadlock":
            p.lathe(rm, [(rim_r + 0.01, sw * 0.86), (rim_r + 0.012, sw + 0.012), (rim_r - 0.03, sw + 0.012), (rim_r - 0.03, sw * 0.86)], (0, 0, 0), "X", segs, close=False)
            for i in range(16):
                a = i / 16 * math.tau
                p.add(bm_cyl(0.007, 0.012, 8), M["chrome"], (sw + 0.016, math.sin(a) * (rim_r - 0.01), math.cos(a) * (rim_r - 0.01)), (0, math.radians(90), 0))
    nuts = 8 if rim == "truck" else 5
    for i in range(nuts):
        a = i / nuts * math.tau
        x = (face + 0.03) if rim == "steel" else (sw * 0.48 if rim != "truck" else sw * 0.45)
        p.add(bm_cyl(0.011, 0.02, 6, bevel=0.002), M["chrome"], (x, math.sin(a) * hub_r * 0.75, math.cos(a) * hub_r * 0.75), (0, math.radians(90), 0))
    ob = p.build(parent)
    if side < 0:
        ob.scale.x = -1
    ob.location = loc
    return ob


def moto_wheel(name, M, R, w, loc, knobby=False, chrome=False, whitewall=False, disc=True, parent=None, spokes=32):
    """Spoked motorcycle / bicycle wheel, axis along Blender X, centred on `loc`."""
    p = Part(name)
    tyre = max(0.035, w * 0.5) if w > 0.06 else 0.022
    core = R - (0.012 if knobby else 0.0)
    rim_r = core - tyre * 1.6
    arc = []
    for a in range(-180, 181, 20):
        t = math.radians(a) / 2  # -90..90 deg around the crown
        arc.append((core - tyre + math.cos(t) * tyre, math.sin(t) * w * 0.52))
    prof = [(rim_r + 0.003, -w * 0.3)] + arc + [(rim_r + 0.003, w * 0.3)]
    p.lathe(M["rubber"], prof, (0, 0, 0), "X", 40)
    if knobby:
        for i in range(40):
            a = i / 40 * math.tau
            for k, x in enumerate((-w * 0.28, w * 0.28)):
                aa = a + (k * 0.5 / 40) * math.tau
                p.add(bm_box(w * 0.3, 0.028, 0.022, bevel=0.004, segs=1), M["tread"], (x, math.sin(aa) * core, math.cos(aa) * core), (-aa, 0, 0))
            p.add(bm_box(w * 0.22, 0.026, 0.02, bevel=0.004, segs=1), M["tread"], (0, math.sin(a + 0.04) * (core + 0.004), math.cos(a + 0.04) * (core + 0.004)), (-a - 0.04, 0, 0))
    if whitewall:
        p.lathe(M["cream"], [(rim_r + tyre * 0.25, w * 0.47), (rim_r + tyre * 0.6, w * 0.5)], (0, 0, 0), "X", 40)
        p.lathe(M["cream"], [(rim_r + tyre * 0.25, -w * 0.47), (rim_r + tyre * 0.6, -w * 0.5)], (0, 0, 0), "X", 40)
    rm = M["chrome"] if chrome else M["steel"]
    rw = w * 0.36
    p.lathe(rm, [(rim_r + 0.008, -rw), (rim_r - 0.006, -rw * 0.8), (rim_r - 0.014, 0), (rim_r - 0.006, rw * 0.8), (rim_r + 0.008, rw), (rim_r + 0.003, rw * 0.5),
                 (rim_r + 0.003, -rw * 0.5)], (0, 0, 0), "X", 40, close=False)
    hub_w = max(0.06, w * 0.9)
    p.lathe(rm, [(0.012, -hub_w * 0.6), (0.032, -hub_w * 0.55), (0.03, -hub_w * 0.3), (0.022, 0), (0.03, hub_w * 0.3), (0.032, hub_w * 0.55), (0.012, hub_w * 0.6)],
            (0, 0, 0), "X", 20, close=True)
    for i in range(spokes):
        a = i / spokes * math.tau
        sgn = 1 if i % 2 else -1
        ha = a + sgn * 0.35
        p.tube(M["chrome"] if chrome else M["steel"], [(sgn * hub_w * 0.5, math.sin(ha) * 0.03, math.cos(ha) * 0.03), (sgn * 0.004, math.sin(a) * (rim_r - 0.012), math.cos(a) * (rim_r - 0.012))],
               0.0022 if w < 0.06 else 0.003, 4, caps=False)
    if disc:
        p.cyl(M["steel"], R * 0.42, 0.006, (-hub_w * 0.5 - 0.01, 0, 0), "X", 28, bevel=0.0015)
        for i in range(10):
            a = i / 10 * math.tau
            p.cyl(M["black"], 0.008, 0.008, (-hub_w * 0.5 - 0.01, math.sin(a) * R * 0.32, math.cos(a) * R * 0.32), "X", 8, bevel=0)
    return p.build(parent, loc=loc)


def finned_cylinder(p, M, base, top, r, fins=7, fin_r=None, mat=None, axis_vec=None):
    """Air-cooled cylinder barrel between two points with cooling fins (base/top Blender coords)."""
    base, top = Vector(base), Vector(top)
    d = top - base
    L = d.length
    rotq = d.to_track_quat("Z", "Y")
    mtx = lambda c: Matrix.Translation(c) @ rotq.to_matrix().to_4x4()
    p.add(bm_cyl(r, L, 20, bevel=0.002), mat or M["iron"], matrix=mtx(base + d * 0.5))
    fr = fin_r or r * 1.6
    for i in range(fins):
        t = (i + 0.5) / fins
        p.add(bm_cyl(fr - (0.004 if i % 2 else 0), L / fins * 0.32, 22, bevel=0.0015), mat or M["iron"], matrix=mtx(base + d * t))


def oriented(p, bm, mat, a, b):
    """Place a +Z-aligned primitive centred between a and b, pointing along a->b."""
    a, b = Vector(a), Vector(b)
    d = b - a
    q = d.to_track_quat("Z", "Y")
    p.add(bm, mat, matrix=Matrix.Translation((a + b) * 0.5) @ q.to_matrix().to_4x4())


def stage(out_dir):
    rc.reset_scene()
    rc.setup_stage()
    sc = bpy.context.scene
    try:
        sc.eevee.use_shadows = True
    except AttributeError:
        pass
    os.makedirs(out_dir, exist_ok=True)
    return sc


def out_dir():
    argv = sys.argv
    if "--" in argv and len(argv) > argv.index("--") + 1:
        return argv[argv.index("--") + 1]
    return "/home/magix/PycharmProjects/MadMaxUnity/hd_preview/misc"


def row(objs, gap=0.4, axis=0):
    """Lay root objects out in a row along X (by their bounds), centred on the origin."""
    widths = []
    for o in objs:
        lo, hi = rc.bounds([o] + list(o.children_recursive))
        widths.append((lo, hi))
    total = sum(hi[axis] - lo[axis] for lo, hi in widths) + gap * (len(objs) - 1)
    x = -total / 2
    for o, (lo, hi) in zip(objs, widths):
        delta = x - lo[axis]
        loc = list(o.location)
        loc[axis] += delta
        o.location = loc
        x += hi[axis] - lo[axis] + gap
    bpy.context.view_layer.update()


def save_blend(path):
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
    if os.path.exists(path + "1"):
        os.remove(path + "1")


def report(stats, path):
    import json
    old = []
    if os.path.exists(path):
        old = [s for s in json.load(open(path)) if s["name"] not in {t["name"] for t in stats}]
    json.dump(old + stats, open(path, "w"), indent=1)


def update_tiles(tiles, out):
    import json
    path = os.path.join(out, "tiles.json")
    cur = json.load(open(path)) if os.path.exists(path) else []
    names = {t["name"] for t in tiles}
    cur = [t for t in cur if t["name"] not in names] + tiles
    json.dump(cur, open(path, "w"), indent=1)


def superloft(stations, n=24, exp=2.6, close=True):
    """Superellipse cross-sections in the XZ plane stacked along Y: stations = [(y, cx, cz, half_w, half_h)].
    exp 2 = ellipse, higher = boxier. Good for tanks, pods, hulls, fuselages."""
    secs = []
    for (y, cx, cz, hw, hh) in stations:
        hw, hh = max(hw, 1e-4), max(hh, 1e-4)
        sec = []
        for k in range(n):
            a = k / n * math.tau
            c, s = math.cos(a), math.sin(a)
            x = hw * math.copysign(abs(c) ** (2 / exp), c)
            z = hh * math.copysign(abs(s) ** (2 / exp), s)
            sec.append((cx + x, y, cz + z))
        secs.append(sec)
    return bm_loft(secs, close)


def strip(part, mat, pts, width, thick, up=(0, 0, 1)):
    """A thin plate swept along a polyline (fenders, straps, mudguards): width across X-ish, thickness along `up`."""
    pts = [Vector(p) for p in pts]
    secs = []
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        if abs(t.x) < 0.9:
            side = (Vector((1, 0, 0)) - t * t.x).normalized()
        else:
            side = t.cross(Vector(up)).normalized()
        nrm = side.cross(t).normalized()
        hw, ht = width * 0.5, thick * 0.5
        secs.append([p - side * hw - nrm * ht, p + side * hw - nrm * ht, p + side * hw + nrm * ht, p - side * hw + nrm * ht])
    part.add(bm_loft(secs), mat)
    return part


def coil(part, mat, a, b, r, turns, wire, segs=6):
    """Helical spring from a to b."""
    a, b = Vector(a), Vector(b)
    d = b - a
    q = d.to_track_quat("Z", "Y").to_matrix()
    pts = []
    n = int(turns * 12)
    for i in range(n + 1):
        t = i / n
        ang = t * turns * math.tau
        pts.append(a + q @ Vector((math.cos(ang) * r, math.sin(ang) * r, 0)) + d * t)
    part.tube(mat, pts, wire, segs, caps=False)


def chain_loop(part, mat, c1, r1, c2, r2, wire=0.006):
    """Drive chain around two sprockets in a YZ plane (x from c1)."""
    c1, c2 = Vector(c1), Vector(c2)
    pts = []
    for k in range(13):
        a = math.pi / 2 + k / 12 * math.pi
        pts.append(c1 + Vector((0, math.cos(a) * r1 * (-1 if c1.y > c2.y else 1), math.sin(a) * r1)))
    for k in range(13):
        a = -math.pi / 2 + k / 12 * math.pi
        pts.append(c2 + Vector((0, math.cos(a) * r2 * (-1 if c1.y > c2.y else 1), math.sin(a) * r2)))
    pts.append(pts[0])
    part.tube(mat, pts, wire, 5, caps=False)


HERE = os.path.dirname(os.path.abspath(__file__))


def run_group(entries, M, extra=None, blend_prefix=""):
    """entries: (builder, tile_name, label, replaces). Each builder runs in a fresh stage, returns a root object
    (or a list of roots); the tile is rendered, stats recorded and the scene saved as misc/<tile_name>.blend."""
    import time
    out = out_dir()
    stats, tiles = [], []
    for fn, name, label, src in entries:
        t0 = time.time()
        stage(out)
        M.clear()
        M.update(mats())
        if extra:
            extra(M)
        roots = fn()
        roots = roots if isinstance(roots, (list, tuple)) else [roots]
        bpy.context.view_layer.update()
        objs = [o for r in roots for o in [r] + list(r.children_recursive)]
        rc.render_tile(name, objs, out)
        for o in bpy.context.scene.objects:
            o.hide_render = False
        stats.append({"name": name, "replaces": src, "tris": tris(roots), "dims_whl": dims(roots),
                      "objects": {r.name: sorted(c.name for c in r.children) for r in roots},
                      "per_root": {r.name: {"tris": tris([r]), "dims_whl": dims([r])} for r in roots}})
        tiles.append({"name": name, "label": label, "replaces": src})
        save_blend(os.path.join(HERE, blend_prefix + name + ".blend"))
        print("STAT", name, stats[-1]["tris"], stats[-1]["dims_whl"], "%.1fs" % (time.time() - t0))
    report(stats, os.path.join(out, "stats.json"))
    update_tiles(tiles, out)
    return stats


def grid(objs, cols=3, gap=0.35):
    """Lay root objects out in a grid (rows along X, rows stacked toward +Y), centred on the origin."""
    bb = [rc.bounds([o] + list(o.children_recursive)) for o in objs]
    rows = [list(range(i, min(i + cols, len(objs)))) for i in range(0, len(objs), cols)]
    colw = [max(bb[i][1].x - bb[i][0].x for i in range(c, len(objs), cols)) for c in range(min(cols, len(objs)))]
    y = 0.0
    for r in rows:
        depth = max(bb[i][1].y - bb[i][0].y for i in r)
        x = -(sum(colw) + gap * (len(colw) - 1)) / 2
        for k, i in enumerate(r):
            lo, hi = bb[i]
            o = objs[i]
            o.location.x += x + (colw[k] - (hi.x - lo.x)) / 2 - lo.x
            o.location.y += y + (depth - (hi.y - lo.y)) / 2 - lo.y
            x += colw[k] + gap
        y += depth + gap
    bpy.context.view_layer.update()
