"""Geometry, materials and part assembly for the HD road-car pack (Blender 5, headless).

Everything is authored in GAME VOXEL UNITS and GAME AXES (+X right, +Y up, +Z forward, 1 unit = 0.08 m), exactly like
the voxel designs, so every socket/size can be copied from Designs/*.cs. `Veh.build()` converts to Blender metres:
game (x, y, z) * 0.08  ->  Blender (-x, -z, y)   (front faces -Y, the car's right side is Blender -X; this is the
mirror the Unity FBX importer undoes, so models import with +Z forward and +X right).
"""
import math
import random

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector, noise

import render_common as rc

S = 0.08
CONV = Matrix(((-1, 0, 0), (0, 0, -1), (0, 1, 0)))
WEAR = 1.0            # global weathering amount used when materials are created (0 = factory fresh)
TAG = ""              # material-name suffix for a wear variant


# ============================================================================================ primitives (return bmesh)
def _faces(bm, quads, mi=0):
    for q in quads:
        q = list(dict.fromkeys(q))
        if len(q) < 3:
            continue
        try:
            f = bm.faces.new(q)
            f.material_index = mi
        except ValueError:
            pass


def revolve(prof, segs=24, origin=(0, 0, 0), axis=(1, 0, 0), mats=None, rfun=None, up=None):
    """Surface of revolution: prof = [(axial, radius)], mats[i] = tmp material index of segment i."""
    bm = bmesh.new()
    o = Vector(origin)
    ax = Vector(axis).normalized()
    u = Vector(up) if up else (Vector((0, 1, 0)) if abs(ax.y) < 0.9 else Vector((0, 0, 1)))
    u = (u - ax * u.dot(ax)).normalized()
    v = ax.cross(u)
    rings = []
    for i, (a, r) in enumerate(prof):
        if r <= 1e-5:
            c = bm.verts.new(o + ax * a)
            rings.append([c] * segs)
            continue
        ring = []
        for j in range(segs):
            t = 2 * math.pi * j / segs
            rr = rfun(i, t, a, r) if rfun else r
            ring.append(bm.verts.new(o + ax * a + (u * math.cos(t) + v * math.sin(t)) * rr))
        rings.append(ring)
    for i in range(len(prof) - 1):
        mi = mats[min(i, len(mats) - 1)] if mats else 0
        _faces(bm, [[rings[i][j], rings[i][(j + 1) % segs], rings[i + 1][(j + 1) % segs], rings[i + 1][j]] for j in range(segs)], mi)
    return bm


def circle(r, n=10):
    return [(math.cos(2 * math.pi * k / n) * r, math.sin(2 * math.pi * k / n) * r) for k in range(n)]


def rrect(w, h, r, n=3):
    """Rounded rectangle cross-section (w x h, corner radius r)."""
    pts = []
    r = min(r, w / 2 - 1e-4, h / 2 - 1e-4)
    for cx, cy, a0 in ((w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90), (-w / 2 + r, -h / 2 + r, 180), (w / 2 - r, -h / 2 + r, 270)):
        for k in range(n + 1):
            a = math.radians(a0 + 90 * k / n)
            pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    return pts


def sweep(path, prof=None, r=0.5, segs=10, caps=True, scales=None, up=None):
    """Sweep a closed 2D profile along a polyline (parallel-transport frames)."""
    P = [Vector(p) for p in path]
    n = len(P)
    prof = prof or circle(r, segs)
    T = [(P[min(i + 1, n - 1)] - P[max(i - 1, 0)]).normalized() for i in range(n)]
    upv = Vector(up) if up else (Vector((0, 1, 0)) if abs(T[0].y) < 0.95 else Vector((1, 0, 0)))
    N = [(upv - T[0] * upv.dot(T[0])).normalized()]
    for i in range(1, n):
        q = T[i - 1].rotation_difference(T[i])
        N.append((q @ N[i - 1]).normalized())
    bm = bmesh.new()
    rings = []
    for i in range(n):
        B = T[i].cross(N[i])
        s = scales[i] if scales else 1.0
        # miter: widen across the bend so tubes keep their thickness at corners
        mit = 1.0
        if 0 < i < n - 1:
            d1 = (P[i] - P[i - 1]).normalized(); d2 = (P[i + 1] - P[i]).normalized()
            mit = 1.0 / max(0.5, math.cos(d1.angle(d2) / 2)) if d1.length and d2.length else 1.0
        ring = []
        for x, y in prof:
            off = N[i] * x + B * y
            if mit > 1.001:
                bis = (P[i + 1] - P[i]).normalized() - (P[i] - P[i - 1]).normalized()
                if bis.length > 1e-6:
                    bis.normalize()
                    k = off.dot(bis)
                    off = off + bis * k * (mit - 1)
            ring.append(bm.verts.new(P[i] + off * s))
        rings.append(ring)
    m = len(prof)
    for i in range(n - 1):
        _faces(bm, [[rings[i][j], rings[i][(j + 1) % m], rings[i + 1][(j + 1) % m], rings[i + 1][j]] for j in range(m)])
    if caps:
        _faces(bm, [list(reversed(rings[0])), rings[-1]])
    return bm


def tube(path, r=0.5, segs=8, caps=True):
    return sweep(path, r=r, segs=segs, caps=caps)


def cyl(p0, p1, r, segs=12, caps=True):
    return sweep([p0, p1], r=r, segs=segs, caps=caps)


def rbox(c, size, bevel=0.0, segs=2, rot=None):
    """Box centred at c, size (x, y, z), bevelled edges, optional Euler rotation (radians, game axes)."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    b = min(bevel, min(size) * 0.48)
    if b > 1e-4:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=b, segments=segs, affect='EDGES', profile=0.5, clamp_overlap=True)
    M = Matrix.Translation(Vector(c)) @ (Euler(rot).to_matrix().to_4x4() if rot else Matrix())
    bmesh.ops.transform(bm, matrix=M, verts=bm.verts)
    return bm


def sphere(c, r, u=12, v=8, scale=(1, 1, 1)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=r)
    for vv in bm.verts:
        vv.co = Vector((vv.co.x * scale[0], vv.co.y * scale[1], vv.co.z * scale[2])) + Vector(c)
    return bm


def xform(bm, loc=(0, 0, 0), rot=None, scale=None):
    M = Matrix.Translation(Vector(loc))
    if rot:
        M = M @ Euler(rot).to_matrix().to_4x4()
    if scale:
        M = M @ Matrix.Diagonal((*scale, 1))
    bmesh.ops.transform(bm, matrix=M, verts=bm.verts)
    return bm


def mirror_x(bm):
    for v in bm.verts:
        v.co.x = -v.co.x
    bmesh.ops.reverse_faces(bm, faces=bm.faces)
    return bm


def join(*bms):
    """Merge temporary bmeshes (keeps their material indices)."""
    out = bmesh.new()
    me = _scratch()
    for b in bms:
        b.to_mesh(me)
        out.from_mesh(me)
        b.free()
    return out


_SCR = None


def _scratch():
    global _SCR
    try:
        if _SCR is not None and _SCR.name in bpy.data.meshes:
            return _SCR
    except ReferenceError:
        pass
    _SCR = bpy.data.meshes.new("_scratch")
    return _SCR


# ============================================================================================ parts / vehicles
class Part:
    """One game object: a bmesh in vehicle-local voxel units + material names; origin = pivot (voxel coords)."""

    def __init__(self, name, origin=(0, 0, 0), **props):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []
        self.origin = Vector(origin)
        self.props = props
        self.offset = Vector((0, 0, 0))     # exploded-view displacement (voxels)

    def m(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def put(self, bm, mat=None, mats=None):
        for f in bm.faces:
            if mats is not None:
                f.material_index = self.m(mats[min(f.material_index, len(mats) - 1)])
            elif mat is not None:
                f.material_index = self.m(mat)
        me = _scratch()
        bm.to_mesh(me)
        bm.free()
        self.bm.from_mesh(me)
        return self


class Veh:
    def __init__(self, name, ground):
        self.name = name
        self.ground = ground          # game y (voxels) of the ground under the tyres
        self.parts = {}
        self.order = []
        self.info = {}

    def part(self, name, origin=(0, 0, 0), **props):
        if name not in self.parts:
            self.parts[name] = Part(name, origin, **props)
            self.order.append(name)
        p = self.parts[name]
        if props:
            p.props.update(props)
        return p

    def build(self, coll=None, at=(0, 0, 0), smooth_angle=34.0):
        """Create Blender objects under a root empty (named like the prefab). Returns (root, objects)."""
        coll = coll or bpy.context.scene.collection
        root = bpy.data.objects.new(self.name, None)
        root.empty_display_size = 1.0
        coll.objects.link(root)
        root.location = Vector(at) + Vector((0, 0, -self.ground * S))
        root["game_ground_y_m"] = self.ground * S
        objs = []
        for name in self.order:
            p = self.parts[name]
            if not p.bm.faces:
                continue
            bm = p.bm
            for v in bm.verts:
                v.co = CONV @ ((v.co - p.origin) * S)
            bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
            thr = math.radians(smooth_angle)
            for f in bm.faces:
                f.smooth = True
            for e in bm.edges:
                if len(e.link_faces) == 2:
                    try:
                        e.smooth = e.calc_face_angle() < thr
                    except ValueError:
                        e.smooth = False
                else:
                    e.smooth = False
            me = bpy.data.meshes.new(p.name)
            bm.to_mesh(me)
            for mn in p.mats:
                me.materials.append(get_mat(mn))
            ob = bpy.data.objects.new(p.name, me)
            coll.objects.link(ob)
            ob.parent = root
            ob.location = CONV @ ((p.origin + p.offset) * S)
            for k, v in p.props.items():
                ob[k] = v
            ob["pivot_game_vox"] = list(p.origin)
            objs.append(ob)
        bpy.context.view_layer.update()
        return root, objs


def tris(objs):
    n = 0
    for o in objs:
        if o.type == "MESH":
            n += sum(len(p.vertices) - 2 for p in o.data.polygons)
    return n


# ============================================================================================ materials
MATDEF = {}


def defmat(name, kind, **kw):
    MATDEF[name] = (kind, kw)


def get_mat(name):
    full = name + TAG
    m = bpy.data.materials.get(full)
    if m:
        return m
    kind, kw = MATDEF.get(name, ("flat", {"col": "ff00ff"}))
    m = bpy.data.materials.new(full)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(b.outputs[0], out.inputs[0])
    m.use_backface_culling = False
    BUILDERS[kind](m, nt, b, **kw)
    return m


def _n(nt, typ, **props):
    n = nt.nodes.new(typ)
    for k, v in props.items():
        if k.startswith("i_"):
            n.inputs[k[2:].replace("_", " ")].default_value = v
        else:
            setattr(n, k, v)
    return n


def _l(nt, a, b):
    nt.links.new(a, b)


def _math(nt, op, a, b=None, clamp=False):
    n = _n(nt, "ShaderNodeMath", operation=op, use_clamp=clamp)
    for i, x in enumerate((a, b)):
        if x is None:
            continue
        if isinstance(x, (int, float)):
            n.inputs[i].default_value = x
        else:
            _l(nt, x, n.inputs[i])
    return n.outputs[0]


def _ramp(nt, fac, stops, interp="CONSTANT"):
    r = _n(nt, "ShaderNodeValToRGB")
    cr = r.color_ramp
    cr.interpolation = interp
    while len(cr.elements) < len(stops):
        cr.elements.new(0.5)
    for el, (pos, col) in zip(cr.elements, stops):
        el.position = pos
        el.color = rc.srgb(col) if isinstance(col, str) else col
    _l(nt, fac, r.inputs[0])
    return r.outputs[0]


def _mix(nt, fac, a, b):
    n = _n(nt, "ShaderNodeMix", data_type="RGBA")
    for idx, x in ((0, fac), (6, a), (7, b)):
        if isinstance(x, (int, float)):
            n.inputs[idx].default_value = x
        elif isinstance(x, tuple):
            n.inputs[idx].default_value = x
        else:
            _l(nt, x, n.inputs[idx])
    return n.outputs[2]


def _mixf(nt, fac, a, b):
    n = _n(nt, "ShaderNodeMix", data_type="FLOAT")
    for idx, x in ((0, fac), (2, a), (3, b)):
        if isinstance(x, (int, float)):
            n.inputs[idx].default_value = x
        else:
            _l(nt, x, n.inputs[idx])
    return n.outputs[0]


def _noise(nt, vec, scale, detail=2.0, rough=0.5, w=None):
    n = _n(nt, "ShaderNodeTexNoise", i_Scale=scale, i_Detail=detail, i_Roughness=rough)
    _l(nt, vec, n.inputs["Vector"])
    return n


def _geo(nt):
    g = _n(nt, "ShaderNodeNewGeometry")
    sep = _n(nt, "ShaderNodeSeparateXYZ")
    _l(nt, g.outputs["Position"], sep.inputs[0])
    nsep = _n(nt, "ShaderNodeSeparateXYZ")
    _l(nt, g.outputs["Normal"], nsep.inputs[0])
    return g, sep, nsep


def _hexes(ramp, idx):
    return [ramp[max(0, min(len(ramp) - 1, i))] for i in idx]


def _weather(nt, col, rough, wear, rust=True, dust=True, chips=None, rust_amt=1.0, seed=0.0, dust_h=0.6, up=True):
    """Layer chips, rust and dust over a colour; returns (colour socket, roughness socket).
    Rust gathers low on the body (sills, arches), dust is a stepped bottom-up gradient plus a soft film on top faces."""
    g, sep, nsep = _geo(nt)
    pos = g.outputs["Position"]
    off = _n(nt, "ShaderNodeVectorMath", operation="ADD")
    _l(nt, pos, off.inputs[0])
    off.inputs[1].default_value = (seed * 7.1, seed * 3.3, seed * 1.7)
    pos = off.outputs[0]
    if wear <= 0:
        return col, rough
    if chips:
        vor = _n(nt, "ShaderNodeTexVoronoi", i_Scale=6.0)
        _l(nt, pos, vor.inputs["Vector"])
        sel = _noise(nt, pos, 2.2, 2.0)
        chip = _math(nt, "LESS_THAN", vor.outputs["Distance"], 0.06 + 0.06 * wear)
        selm = _math(nt, "GREATER_THAN", sel.outputs["Fac"], 0.6 - 0.04 * wear)
        cm = _math(nt, "MULTIPLY", chip, selm)
        col = _mix(nt, cm, col, rc.srgb(chips))
    if rust:
        nr = _noise(nt, pos, 5.5, 5.0, 0.62)
        low = _n(nt, "ShaderNodeMapRange", clamp=True)
        _l(nt, sep.outputs["Z"], low.inputs["Value"])
        low.inputs["From Min"].default_value = 0.75
        low.inputs["From Max"].default_value = 0.05
        v = _math(nt, "ADD", _math(nt, "MULTIPLY", nr.outputs["Fac"], 0.7), _math(nt, "MULTIPLY", low.outputs["Result"], 0.42))
        thr = 0.83 - 0.17 * min(1.2, wear * rust_amt)
        rm = _math(nt, "GREATER_THAN", v, thr)
        rm2 = _math(nt, "GREATER_THAN", v, thr + 0.03)
        rcn = _noise(nt, pos, 11.0, 3.0)
        rcol = _ramp(nt, rcn.outputs["Fac"], [(0.0, "5e2c15"), (0.42, "80401d"), (0.6, "a4582a"), (0.72, "3f1e10")])
        col = _mix(nt, rm, col, rc.srgb("c47436"))      # bright rust halo, then the dark scale inside
        col = _mix(nt, rm2, col, rcol)
        rough = _mixf(nt, rm, rough, 0.92)
    if dust:
        d = _n(nt, "ShaderNodeMapRange", clamp=True)
        _l(nt, sep.outputs["Z"], d.inputs["Value"])
        d.inputs["From Min"].default_value = dust_h
        d.inputs["From Max"].default_value = 0.0
        d.inputs["To Max"].default_value = 0.85 * wear
        dn = _noise(nt, pos, 4.0, 2.0)
        dd = _math(nt, "MULTIPLY", d.outputs["Result"], _math(nt, "ADD", 0.7, _math(nt, "MULTIPLY", dn.outputs["Fac"], 0.6)))
        dq = _ramp(nt, dd, [(0.0, (0, 0, 0, 1)), (0.22, (0.3, 0.3, 0.3, 1)), (0.45, (0.58, 0.58, 0.58, 1)), (0.68, (0.82, 0.82, 0.82, 1))])
        dqf = _n(nt, "ShaderNodeRGBToBW")
        _l(nt, dq, dqf.inputs[0])
        fac = dqf.outputs[0]
        if up:
            upm = _n(nt, "ShaderNodeMapRange", clamp=True)
            _l(nt, nsep.outputs["Z"], upm.inputs["Value"])
            upm.inputs["From Min"].default_value = 0.55
            upm.inputs["From Max"].default_value = 1.0
            upm.inputs["To Max"].default_value = 0.2 * wear
            film = _math(nt, "MULTIPLY", upm.outputs["Result"], _math(nt, "ADD", 0.6, _math(nt, "MULTIPLY", dn.outputs["Fac"], 0.7)))
            fac = _math(nt, "MAXIMUM", fac, film)
        col = _mix(nt, fac, col, rc.srgb("b8987a"))
        rough = _mixf(nt, fac, rough, 0.95)
    return col, rough


def _set(nt, b, col, rough, metal=0.0):
    if isinstance(col, tuple):
        b.inputs["Base Color"].default_value = col
    else:
        _l(nt, col, b.inputs["Base Color"])
    if isinstance(rough, (int, float)):
        b.inputs["Roughness"].default_value = rough
    else:
        _l(nt, rough, b.inputs["Roughness"])
    b.inputs["Metallic"].default_value = metal


def _b_paint(m, nt, b, ramp, idx=(2, 3, 4), rough=0.42, rust=1.0, chips="8e8680", seed=0.0, dust=True):
    """Hand-painted car paint: flat ramp bands from the game's Pal ramp, then chips, rust, dust."""
    R = rc.PAL[ramp] if isinstance(ramp, str) else ramp
    c = [rc.srgb(h) for h in _hexes(R, idx)]
    lerp = lambda a, b, t: tuple(a[i] + (b[i] - a[i]) * t for i in range(4))
    g, sep, _ = _geo(nt)
    n1 = _noise(nt, g.outputs["Position"], 1.3, 1.0, 0.4)
    if WEAR > 0:      # sun-faded patches: a step darker / lighter than the body colour
        col = _ramp(nt, n1.outputs["Fac"], [(0.0, lerp(c[0], c[1], 0.5)), (0.33, c[1]), (0.68, lerp(c[1], c[2], 0.5))])
    else:
        col = _ramp(nt, n1.outputs["Fac"], [(0.0, c[1]), (0.75, lerp(c[1], c[2], 0.25))])
    col, r = _weather(nt, col, rough, WEAR, rust=rust > 0, chips=chips, rust_amt=rust, seed=seed)
    _set(nt, b, col, r)
    b.inputs["Coat Weight"].default_value = 0.25 * (1 - WEAR * 0.8)


def _b_flames(m, nt, b, seed=0.0, ground=0.0):
    """Monster truck: crimson with ochre hot-rod flames licking back from the nose (object coords)."""
    gg = _n(nt, "ShaderNodeNewGeometry")
    sep = _n(nt, "ShaderNodeSeparateXYZ")
    _l(nt, gg.outputs["Position"], sep.inputs[0])
    zv = _math(nt, "DIVIDE", _math(nt, "MULTIPLY", sep.outputs["Y"], -1.0), S)        # game z (voxels), vehicle at the origin
    yv = _math(nt, "ADD", _math(nt, "DIVIDE", sep.outputs["Z"], S), ground)              # game y (voxels)
    edge = _math(nt, "ADD", _math(nt, "ADD", _math(nt, "MULTIPLY", _math(nt, "SINE", _math(nt, "MULTIPLY", zv, 0.55)), 2.6), 3.5),
                 _math(nt, "MULTIPLY", _math(nt, "ADD", zv, 6.0), 0.2))
    h = _math(nt, "SUBTRACT", yv, 26.0)
    front = _math(nt, "GREATER_THAN", zv, -8.0)
    inner = _math(nt, "MULTIPLY", front, _math(nt, "LESS_THAN", h, _math(nt, "SUBTRACT", edge, 2.2)))
    outer = _math(nt, "MULTIPLY", front, _math(nt, "LESS_THAN", h, edge))
    g, gs, _ = _geo(nt)
    n1 = _noise(nt, g.outputs["Position"], 1.3, 1.0, 0.4)
    base = _ramp(nt, n1.outputs["Fac"], [(0.0, "7c1c22"), (0.40, "a02a2c"), (0.66, "c44038")])
    col = _mix(nt, outer, base, rc.srgb("c44038"))
    col = _mix(nt, inner, col, rc.srgb("e0ac40"))
    col, r = _weather(nt, col, 0.35, WEAR * 0.5, chips="4f4842", seed=seed)
    _set(nt, b, col, r)
    b.inputs["Coat Weight"].default_value = 0.4


def _b_metal(m, nt, b, ramp="metal", idx=(1, 2, 3), rough=0.55, metal=0.6, rust=1.0, seed=0.0):
    R = rc.PAL[ramp]
    c = _hexes(R, idx)
    g, sep, _ = _geo(nt)
    n1 = _noise(nt, g.outputs["Position"], 2.0, 2.0, 0.5)
    col = _ramp(nt, n1.outputs["Fac"], [(0.0, c[0]), (0.42, c[1]), (0.64, c[2])])
    col, r = _weather(nt, col, rough, WEAR, rust=rust > 0, rust_amt=rust, seed=seed)
    _set(nt, b, col, r, metal)


def _b_chrome(m, nt, b, rust=0.6, seed=0.0):
    g, sep, _ = _geo(nt)
    n1 = _noise(nt, g.outputs["Position"], 3.0, 2.0)
    col = _ramp(nt, n1.outputs["Fac"], [(0.0, "aeb1b8"), (0.45, "e3e5e8")])
    col, r = _weather(nt, col, 0.14, WEAR, rust=rust > 0, rust_amt=rust, seed=seed, dust_h=0.4)
    _set(nt, b, col, r, 1.0)


def _b_flat(m, nt, b, col="808080", rough=0.6, metal=0.0, emit=None, strength=1.0, dust=False):
    c = rc.srgb(col)
    if dust and WEAR > 0:
        cc, r = _weather(nt, c, rough, WEAR, rust=False)
        _set(nt, b, cc, r, metal)
    else:
        _set(nt, b, c, rough, metal)
    if emit:
        b.inputs["Emission Color"].default_value = rc.srgb(emit)
        b.inputs["Emission Strength"].default_value = strength


def _b_glass(m, nt, b, col="1a2330", alpha=0.42):
    m.surface_render_method = "BLENDED"
    m.use_transparency_overlap = False
    g, sep, _ = _geo(nt)
    d = _n(nt, "ShaderNodeMapRange", clamp=True)
    _l(nt, sep.outputs["Z"], d.inputs["Value"])
    d.inputs["From Min"].default_value = 1.6
    d.inputs["From Max"].default_value = 0.6
    d.inputs["To Max"].default_value = 0.5 * WEAR
    n1 = _noise(nt, g.outputs["Position"], 4.0, 3.0)
    streak = _math(nt, "MULTIPLY", d.outputs["Result"], _math(nt, "GREATER_THAN", n1.outputs["Fac"], 0.45))
    col = _mix(nt, streak, rc.srgb(col), rc.srgb("8a7660"))
    a = _math(nt, "ADD", alpha, _math(nt, "MULTIPLY", streak, 0.6))
    _set(nt, b, col, 0.06)
    _l(nt, a, b.inputs["Alpha"])
    b.inputs["Specular IOR Level"].default_value = 0.9


def _b_rubber(m, nt, b, col="1c1918"):
    g, sep, _ = _geo(nt)
    n1 = _noise(nt, g.outputs["Position"], 6.0, 2.0)
    c = _ramp(nt, n1.outputs["Fac"], [(0.0, "121010"), (0.45, col), (0.7, "282322")])
    c, r = _weather(nt, c, 0.86, WEAR, rust=False, dust_h=0.75, up=False)
    _set(nt, b, c, r)


def _b_wood(m, nt, b, seed=0.0):
    tc = _n(nt, "ShaderNodeTexCoord")
    wave = _n(nt, "ShaderNodeTexWave", wave_type="BANDS", bands_direction="Z", i_Scale=9.0, i_Distortion=0.6, i_Detail=2.0)
    _l(nt, tc.outputs["Object"], wave.inputs["Vector"])
    col = _ramp(nt, wave.outputs["Fac"], [(0.0, "56361e"), (0.3, "74492a"), (0.6, "93603a"), (0.85, "b07a4c")])
    col, r = _weather(nt, col, 0.6, WEAR, rust=False, seed=seed)
    _set(nt, b, col, r)


BUILDERS = {"paint": _b_paint, "metal": _b_metal, "chrome": _b_chrome, "flat": _b_flat, "glass": _b_glass,
            "rubber": _b_rubber, "wood": _b_wood, "flames": _b_flames}


def std_mats():
    defmat("chrome", "chrome")
    defmat("glass", "glass")
    defmat("seal", "flat", col="302d33", rough=0.7)
    defmat("seam", "flat", col="3a3230", rough=0.9)
    defmat("plastic", "flat", col="18171c", rough=0.62, dust=True)
    defmat("plastic_grey", "flat", col="302d33", rough=0.6, dust=True)
    defmat("rubber", "rubber")
    defmat("tyre_side", "rubber", col="232020")
    defmat("steel", "metal", ramp="metal", idx=(1, 2, 3), rust=1.2)
    defmat("steel_dark", "metal", ramp="metal", idx=(0, 1, 2), rust=0.8)
    defmat("rusty", "metal", ramp="rust", idx=(1, 2, 3), rough=0.85, metal=0.2, rust=1.5)
    defmat("underbody", "metal", ramp="metal", idx=(0, 0, 1), rough=0.8, metal=0.2, rust=1.4)
    defmat("wheelwell", "flat", col="0d0d10", rough=0.95)
    defmat("interior_trim", "flat", col="232127", rough=0.8)
    defmat("carpet", "flat", col="18171c", rough=1.0)
    defmat("leather", "paint", ramp="olive", idx=(0, 1, 2), rough=0.7, rust=0, chips=None)
    defmat("dash", "flat", col="18171c", rough=0.55)
    defmat("lamp_head", "flat", col="fff3c0", rough=0.08, emit="fff3c0", strength=0.8)
    defmat("lamp_yellow", "flat", col="ffd15a", rough=0.08, emit="ffd15a", strength=0.6)
    defmat("lamp_tail", "flat", col="b02818", rough=0.1, emit="b02818", strength=0.7)
    defmat("lamp_amber", "flat", col="f08a24", rough=0.1, emit="f08a24", strength=0.6)
    defmat("reflector", "chrome", rust=0.0)
    defmat("plate", "flat", col="eeeadc", rough=0.5, dust=True)
    defmat("plate_ink", "flat", col="18244a", rough=0.5)
    defmat("void", "flat", col="07070a", rough=1.0)
    defmat("grille_dark", "flat", col="0d0d10", rough=0.9)
    defmat("canvas", "paint", ramp="black", idx=(1, 2, 3), rough=0.95, rust=0, chips=None)
    defmat("wood", "wood")
    defmat("brass", "metal", ramp="bronze", idx=(2, 3, 3), rough=0.35, metal=0.9, rust=0.5)
    defmat("rim_bronze", "metal", ramp="bronze", idx=(1, 2, 3), rough=0.4, metal=0.7, rust=0.6)
    defmat("rim_steel", "metal", ramp="metal", idx=(2, 3, 3), rough=0.5, metal=0.6, rust=1.0)
    defmat("rim_black", "metal", ramp="black", idx=(1, 2, 3), rough=0.6, metal=0.3, rust=0.6)
    defmat("rim_silver", "chrome", rust=0.4)
    defmat("engine_block", "metal", ramp="metal", idx=(1, 2, 3), rough=0.6, metal=0.4, rust=0.7)
    defmat("engine_paint", "paint", ramp="crimson", idx=(1, 2, 3), rough=0.5, rust=0.6)
    defmat("jerry_olive", "paint", ramp="riggreen", idx=(2, 3, 4), rough=0.6, rust=0.8)
    defmat("jerry_red", "paint", ramp="crimson", idx=(2, 3, 4), rough=0.6, rust=0.8)
    defmat("jerry_sand", "paint", ramp="sand", idx=(2, 3, 4), rough=0.6, rust=0.8)
    defmat("hazard", "paint", ramp="ochre", idx=(3, 4, 4), rough=0.5, rust=0.5)
    defmat("cage_black", "paint", ramp="black", idx=(1, 2, 3), rough=0.5, rust=0.6, chips="6b625a")


# ============================================================================================ wheels
WHEELS = {
    # key: radius (voxels), width voxels (n -> x 0..n), rim radius, tread style, rim style
    "wheel_street": (5.4, 3, 3.3, "street", "bronze"),
    "wheel_offroad": (6.5, 4, 3.6, "blocks", "steel"),
    "wheel_mud": (6.8, 5, 3.7, "mud", "steel"),
    "wheel_small": (4.3, 2, 3.1, "street", "hubcap"),
    "wheel_compact": (3.9, 2, 2.8, "street", "hubcap"),
    "wheel_truck": (6.7, 4, 4.0, "truck", "truck"),
    "wheel_monster": (10.6, 8, 5.2, "monster", "beadlock"),
    "wheel_sport": (5.6, 4, 4.3, "street", "alloy"),
}


def wheel_bm(key, segs=None):
    """Right-side wheel: axle along +X, centred at the origin (tyre centre), outer face at +X. Returns tmp bmesh with
    tmp material indices 0 rubber, 1 tyre_side, 2 rim, 3 rim2 (accent), 4 hub chrome, 5 dark."""
    R, n, rr, tread, rim = WHEELS[key]
    w = n + 1.0
    hw = w / 2
    segs = segs or (96 if tread in ("mud", "monster", "blocks") else 72)
    deep = {"street": 0.12, "blocks": 0.28, "mud": 0.45, "truck": 0.3, "monster": 0.75}[tread]
    side_top = R - 0.55 - deep * 0.6
    prof = [(-hw + 0.35, rr - 0.15), (-hw + 0.05, rr + 0.35), (-hw - 0.08, rr + (side_top - rr) * 0.55), (-hw + 0.02, side_top),
            (-hw + 0.25, R - 0.25), (-hw + 0.6, R)]
    k = 7
    for i in range(1, k):
        prof.append((-hw + 0.6 + (w - 1.2) * i / k, R))
    prof += [(hw - 0.6, R), (hw - 0.25, R - 0.25), (hw - 0.02, side_top), (hw + 0.08, rr + (side_top - rr) * 0.55), (hw - 0.05, rr + 0.35),
             (hw - 0.35, rr - 0.15)]
    tread_idx = set(range(4, len(prof) - 4))
    N = segs
    rng = random.Random(hash(key) & 0xffff)

    def rfun(i, t, a, r):
        if i not in tread_idx:
            return r
        u = t / (2 * math.pi) * N
        x = a / hw                      # -1..1 across
        if tread == "street":
            g = abs(abs(x) - 0.38) < 0.11 or (int(u * 0.8 + (2 if x > 0 else 0)) % 6 == 0 and abs(x) < 0.85)
            return r - deep if g else r
        if tread == "blocks":
            blk = int(u / 3.0 + (0.5 if abs(x) > 0.45 else 0.0)) % 2
            if abs(x) < 0.08:
                return r - deep
            return r if blk == 0 else r - deep
        if tread == "truck":
            if abs(x) < 0.12 or abs(abs(x) - 0.55) < 0.08:
                return r - deep
            return r if int(u / 2.0 + (0.5 if x > 0 else 0)) % 2 == 0 else r - deep * 0.5
        # chevron lugs (mud / monster)
        ph = (u / (4.0 if tread == "mud" else 4.0) + abs(x) * 0.9 + (0.5 if x > 0 else 0.0)) % 1.0
        lug = ph < 0.42
        return r if lug else r - deep

    # side lugs on the shoulder for mud/monster
    bm = revolve(prof, N, mats=[1, 1, 1, 0] + [0] * (len(prof) - 9) + [0, 1, 1, 1, 1], rfun=rfun)
    # rim
    rp = rim
    lip = 0.25
    if rp == "bronze":
        rprof = [(hw - 0.35, rr - 0.15), (hw - 0.2, rr + 0.1), (hw - 0.05, rr - 0.05), (hw - 0.3, rr - 0.35), (hw - 1.4, rr * 0.82),
                 (hw - 1.5, rr * 0.4), (hw - 0.7, rr * 0.36), (hw - 0.55, 0.0)]
        rm = [2, 4, 2, 5, 5, 4, 4]
    elif rp == "steel":
        rprof = [(hw - 0.35, rr - 0.15), (hw - 0.15, rr + 0.1), (hw - 0.05, rr - 0.1), (hw - 0.5, rr - 0.4), (hw - 1.2, rr * 0.78),
                 (hw - 1.0, rr * 0.5), (hw - 0.75, rr * 0.42), (hw - 0.75, rr * 0.2), (hw - 0.4, 0.0)]
        rm = [2, 2, 2, 2, 2, 2, 4, 4]
    elif rp == "truck":
        rprof = [(hw - 0.35, rr - 0.15), (hw - 0.15, rr + 0.1), (hw - 0.05, rr - 0.1), (hw - 0.6, rr - 0.45), (hw - 1.6, rr * 0.72),
                 (hw - 1.4, rr * 0.45), (hw - 0.9, rr * 0.4), (hw - 0.6, rr * 0.18), (hw - 0.25, 0.0)]
        rm = [2, 2, 2, 2, 2, 2, 4, 4]
    elif rp == "beadlock":
        rprof = [(hw - 0.35, rr - 0.15), (hw + 0.15, rr + 0.25), (hw + 0.15, rr - 0.35), (hw - 0.4, rr - 0.6), (hw - 2.4, rr * 0.7),
                 (hw - 2.2, rr * 0.4), (hw - 1.2, rr * 0.35), (hw - 1.0, 0.0)]
        rm = [3, 3, 3, 2, 2, 2, 4]
    elif rp == "alloy":
        rprof = [(hw - 0.35, rr - 0.15), (hw - 0.15, rr + 0.1), (hw - 0.1, rr - 0.1), (hw - 0.4, rr - 0.3), (hw - 0.9, rr * 0.5),
                 (hw - 0.7, rr * 0.3), (hw - 0.5, 0.0)]
        rm = [2, 2, 2, 5, 2, 4]
    else:  # hubcap
        rprof = [(hw - 0.35, rr - 0.15), (hw - 0.2, rr + 0.1), (hw - 0.1, rr - 0.1), (hw - 0.35, rr - 0.3), (hw - 0.3, rr * 0.82),
                 (hw + 0.02, rr * 0.66), (hw + 0.12, rr * 0.3), (hw + 0.14, 0.0)]
        rm = [2, 2, 2, 2, 4, 4, 4]
    bms = [bm, revolve(rprof, 40, mats=rm)]
    # backing disc (hides the tyre inside) + brake drum
    bms.append(revolve([(-hw + 0.4, 0.0), (-hw + 0.4, rr - 0.1), (-hw + 0.3, rr - 0.1)], 24, mats=[5]))
    # rim detail: lug nuts, holes / spokes
    nl = 8 if rp in ("truck", "beadlock") else 5
    hubr = rr * (0.3 if rp != "beadlock" else 0.3)
    face_x = {"bronze": hw - 0.62, "steel": hw - 0.8, "truck": hw - 0.95, "beadlock": hw - 1.15, "alloy": hw - 0.55, "hubcap": hw + 0.1}[rp]
    if rp != "hubcap":
        for i in range(nl):
            a = 2 * math.pi * i / nl
            p = Vector((face_x, math.cos(a) * hubr, math.sin(a) * hubr))
            b2 = cyl(p, p + Vector((0.22, 0, 0)), 0.16, 6)
            for f in b2.faces:
                f.material_index = 4
            bms.append(b2)
    if rp == "bronze":                       # ten alternating spokes on a dark dish
        for i in range(10):
            a = 2 * math.pi * (i + 0.5) / 10
            c = Vector((hw - 1.0, math.cos(a) * rr * 0.62, math.sin(a) * rr * 0.62))
            b2 = rbox((0, 0, 0), (0.5, rr * 0.48, 0.42), 0.08, 1)
            xform(b2, c, (a - math.pi / 2, 0, 0))
            for f in b2.faces:
                f.material_index = 2
            bms.append(b2)
    if rp in ("steel", "truck"):             # ventilation holes in the disc
        nh = 8 if rp == "truck" else 6
        for i in range(nh):
            a = 2 * math.pi * (i + 0.5) / nh
            rh = rr * (0.62 if rp == "steel" else 0.58)
            dx = (rr * 0.6) * (1.2 / max(rr, 1)) * 0.0
            p = Vector((hw - (1.1 if rp == "steel" else 1.45), math.cos(a) * rh, math.sin(a) * rh))
            b2 = cyl(p, p + Vector((0.35, 0, 0)), rr * 0.14, 10)
            for f in b2.faces:
                f.material_index = 5
            bms.append(b2)
    if rp == "beadlock":                     # bolt ring
        for i in range(20):
            a = 2 * math.pi * i / 20
            p = Vector((hw + 0.1, math.cos(a) * (rr - 0.05), math.sin(a) * (rr - 0.05)))
            b2 = cyl(p, p + Vector((0.18, 0, 0)), 0.13, 6)
            for f in b2.faces:
                f.material_index = 4
            bms.append(b2)
    if rp == "hubcap":                       # chrome cap with a badge ring
        b2 = revolve([(hw + 0.15, rr * 0.25), (hw + 0.2, rr * 0.18), (hw + 0.2, 0.0)], 24, mats=[5, 5])
        bms.append(b2)
    if tread in ("mud", "monster"):          # shoulder lugs
        nlug = 24 if tread == "mud" else 22
        for i in range(nlug):
            a = 2 * math.pi * i / nlug
            for sgn in (-1, 1):
                rr2 = R - 0.6 - deep * 0.3
                p = Vector((sgn * (hw + 0.02), math.cos(a) * rr2, math.sin(a) * rr2))
                b2 = rbox((0, 0, 0), (0.3, deep * 1.6 + 0.4, 0.9 if tread == "mud" else 1.3), 0.08, 1)
                xform(b2, p, (a - math.pi / 2, 0, 0))
                for f in b2.faces:
                    f.material_index = 0
                bms.append(b2)
    return join(*bms)


WHEEL_MATS = ["rubber", "tyre_side", "rim_steel", "rim_black", "chrome", "void"]


def wheel_mats(key):
    rp = WHEELS[key][4]
    rim = {"bronze": "rim_bronze", "steel": "rim_steel", "truck": "rim_steel", "beadlock": "rim_black", "alloy": "rim_silver",
           "hubcap": "rim_steel"}[rp]
    acc = {"beadlock": "rim_silver"}.get(rp, "rim_black")
    hub = "chrome" if rp in ("hubcap", "alloy", "bronze") else "steel_dark"
    return ["rubber", "tyre_side", rim, acc, hub, "void"]


def add_wheels(veh, axles, steer_front=True):
    """axles: list of (tag, socket_x, y, z, key); creates Wheel_<tag>R / Wheel_<tag>L at axle centres."""
    for tag, sx, y, z, key in axles:
        R, n, rr, _, _ = WHEELS[key]
        cx = sx + n / 2.0
        for side, sgn in (("R", 1), ("L", -1)):
            bm = wheel_bm(key)
            if sgn < 0:
                mirror_x(bm)
            nm = "Wheel_%s%s" % (tag, side)
            p = veh.part(nm, (sgn * cx, y, z), socket=("wheel_front" if tag == "F" else "wheel_rear" if tag == "R" else "wheel_" + tag) + ("" if sgn > 0 else "_L"),
                         part=key, radius_m=R * S, width_m=(n + 1) * S, socket_inner_x_vox=sgn * sx, mirrored=sgn < 0)
            xform(bm, (sgn * cx, y, z))
            p.put(bm, mats=wheel_mats(key))
    veh.info["wheels"] = [(t, k, WHEELS[k][0] * S) for t, _, _, _, k in axles]


# ============================================================================================ small details
def lamp_round(c, r, normal=(0, 0, 1), lens="lamp_head", bezel="chrome", depth=0.5):
    """Round lamp facing `normal`: chrome bezel ring + domed lens; returns tmp bm with mats [bezel, lens, reflector]."""
    prof = [(-depth, 0.0), (-depth, r * 1.05), (0.05, r * 1.15), (0.25, r * 1.05), (0.25, r * 0.88), (0.12, r * 0.86),
            (0.25, r * 0.6), (0.32, 0.0)]
    bm = revolve(prof, 24, origin=c, axis=normal, mats=[0, 0, 0, 0, 0, 1, 1])
    return bm


def lamp_rect(c, w, h, normal=(0, 0, 1), depth=0.4, bevel=0.15):
    """Rectangular lamp: bezel box + lens slab. Returns bm with mats [bezel, lens]."""
    n = Vector(normal).normalized()
    rot = Vector((0, 0, 1)).rotation_difference(n).to_euler() if n.z > -0.999 else Euler((0, math.pi, 0))
    a = rbox((0, 0, -depth / 2 + 0.1), (w + 0.3, h + 0.3, depth), bevel, 1)
    b = rbox((0, 0, 0.12), (w, h, 0.2), bevel * 0.7, 1)
    for f in b.faces:
        f.material_index = 1
    bm = join(a, b)
    xform(bm, c, tuple(rot))
    return bm


def plate(c, w=5.0, h=1.6, normal=(0, 0, 1)):
    """Number plate with embossed glyph blocks; mats [plate, ink]."""
    n = Vector(normal).normalized()
    rot = Vector((0, 0, 1)).rotation_difference(n).to_euler() if n.z > -0.999 else Euler((0, math.pi, 0))
    parts = [rbox((0, 0, 0), (w, h, 0.12), 0.05, 1)]
    k = 6
    for i in range(k):
        x = -w / 2 + 0.5 + (w - 1.0) * (i + 0.5) / k
        gb = rbox((x, 0, 0.07), ((w - 1.0) / k * 0.62, h * 0.55, 0.06), 0.0)
        for f in gb.faces:
            f.material_index = 1
        parts.append(gb)
    bm = join(*parts)
    xform(bm, c, tuple(rot))
    return bm


def jerry_can(c, rot=(0, 0, 0)):
    """Jerry can lying on its side (0.47 x 0.34 x 0.165 m -> voxels). mats [body, ridge]."""
    L, H, T = 5.9, 4.2, 2.1
    parts = [rbox((0, 0, 0), (T, H, L), 0.35, 2)]
    for s in (-1, 1):
        x1 = rbox((s * T / 2, 0, 0), (0.15, H * 0.8, 0.3), 0.05, 1, rot=(0, 0, 0))
        xform(x1, (0, 0, 0), (math.radians(35), 0, 0))
        x2 = rbox((s * T / 2, 0, 0), (0.15, H * 0.8, 0.3), 0.05, 1)
        xform(x2, (0, 0, 0), (math.radians(-35), 0, 0))
        for b in (x1, x2):
            for f in b.faces:
                f.material_index = 0
        parts += [x1, x2]
    for z in (-1.2, 0, 1.2):                  # three handles on top
        h = tube([(0, H / 2, z - 0.4), (0, H / 2 + 0.5, z - 0.3), (0, H / 2 + 0.5, z + 0.3), (0, H / 2, z + 0.4)], 0.13, 6)
        for f in h.faces:
            f.material_index = 1
        parts.append(h)
    sp = cyl((0, H / 2 - 0.4, L / 2 - 0.4), (0, H / 2 + 0.3, L / 2 + 0.2), 0.45, 8)
    for f in sp.faces:
        f.material_index = 1
    parts.append(sp)
    bm = join(*parts)
    xform(bm, c, rot)
    return bm


def rivets(points, r=0.18):
    return join(*[sphere(p, r, 6, 4, (1, 1, 0.6)) for p in points]) if points else bmesh.new()


def seat_bm(x0, x1, f, seatZ, bucket=False, rear=False, back_h=7.6):
    """Seat from the voxel Interior(): cushion f+1..f+3 (z seatZ-4..seatZ), backrest f+4..f+11 at z seatZ-5."""
    cx = (x0 + x1) / 2
    w = x1 - x0 + 1
    parts = []
    cush = rbox((cx, f + 2.0, seatZ - 2), (w, 2.6, 5.0), 0.7, 3)
    back_h = min(6.0, back_h) if rear else back_h
    back = rbox((cx, f + 3.6 + back_h / 2, seatZ - 4.8), (w, back_h, 1.6), 0.6, 3, rot=(math.radians(-10), 0, 0))
    parts += [cush, back]
    if not rear:
        hr = rbox((cx, f + 3.6 + back_h + 1.3, seatZ - 5.4), (w * 0.6, 1.8, 1.0), 0.4, 2, rot=(math.radians(-10), 0, 0))
        parts.append(hr)
    if bucket:
        for s in (-1, 1):
            parts.append(rbox((cx + s * w / 2, f + 4.5, seatZ - 3.0), (0.6, 5.0, 4.0), 0.25, 2))
    return join(*parts)


def steering(dx, f, dashZ, r=2.2):
    """Steering wheel at the voxel position: ring centre (dx, f+8.5, dashZ-1), column into the dash."""
    c = Vector((dx, f + 8.5, dashZ - 1.0))
    n = Vector((0, 0.45, -1)).normalized()
    ring = revolve([(-0.15, r - 0.3), (0.15, r - 0.3), (0.15, r + 0.3), (-0.15, r + 0.3), (-0.15, r - 0.3)], 32, origin=c, axis=n)
    col = cyl(c - n * 0.2, c + Vector((0, -1.6, 3.0)), 0.3, 8)
    hub = revolve([(0.0, 0.0), (0.0, 0.7), (-0.3, 0.6), (-0.3, 0.0)], 12, origin=c, axis=n)
    sp = []
    for a in (0, 2.1, 4.2):
        u = Vector((math.cos(a), 0, 0)) + Vector((0, math.sin(a), 0))
        u = (u - n * u.dot(n)).normalized()
        sp.append(cyl(c, c + u * r, 0.15, 6))
    return join(ring, col, hub, *sp)


def mirror_bm(base, out, up=1.2, side=1):
    """Door mirror: stalk from `base` and a rounded housing; mats [housing, glass]."""
    b = Vector(base)
    head = b + Vector((side * out, up, -0.2))
    st = cyl(b, head, 0.18, 6)
    hs = rbox(head + Vector((side * 0.4, 0.3, 0.2)), (1.4, 1.0, 0.7), 0.3, 2)
    gl = rbox(head + Vector((side * 0.4, 0.3, -0.18)), (1.15, 0.78, 0.06), 0.0)
    for f in gl.faces:
        f.material_index = 1
    return join(st, hs, gl)


def wiper(p0, p1):
    a = cyl(p0, p1, 0.12, 5)
    b = cyl(p0 + Vector((0, 0.05, 0.05)), p1 + Vector((0, 0.05, 0.05)), 0.07, 4)
    return join(a, b)


def surface_hit(bvh, origin, direction, maxd=5000.0):
    hit = bvh.ray_cast(Vector(origin), Vector(direction).normalized(), maxd)
    return (hit[0], hit[1]) if hit[0] is not None else (None, None)
