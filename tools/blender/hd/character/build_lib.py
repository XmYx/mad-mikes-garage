"""Building skinned HD meshes from SDF fields: body, garment pieces, hair; per-character assembly."""
import math
import time

import bmesh
import bpy
import numpy as np
from scipy.ndimage import map_coordinates

import body as B
import hdlib as H
import materials as MAT

BODY_H = 0.005          # garment grid (m)
SKIN_H = 0.004          # body grid (m)
FINE_H = 0.0025         # head / trims grid


def log(*a):
    print("[hd-char]", *a, flush=True)


class Ctx:
    """A field-evaluation context: a grid around some bounds with the body SDF, nearest-part labels and helpers."""

    def __init__(self, hum, lo, hi, h):
        self.hum = hum
        self.grid = H.Grid(lo, hi, h)
        self.F, self.label, self.best = H.eval_prims(self.grid, hum.prims, hum.Wb)
        self._loc = {}
        self._pf = {}

    def part_F(self, part):
        """Distance to one part's own shape; F_p - best = how far a point is from belonging to that part (0 = its own)."""
        if part not in self._pf:
            F, _, _ = H.eval_prims(self.grid, [p for p in self.hum.prims if p.part == part and p.op == "add"], self.hum.Wb)
            self._pf[part] = F - self.best
        return self._pf[part]

    def loc(self, part):
        if part not in self._loc:
            self._loc[part] = self.grid.local(self.hum.Wb[part])
        return self._loc[part]

    def along(self, part):
        """Distance along the bone from its joint (metres), the game's HumanDesign.Along axis."""
        x, y, z = self.loc(part)
        hh = self.hum.h
        if part == "Pelvis":
            return 0.12 * hh - y, 0.2 * hh
        if part in ("Chest", "Head"):
            return y + 0 * x, (0.44 if part == "Chest" else 0.32) * hh
        if part.startswith("Foot"):
            return z + 0.06 + 0 * x, 0.26
        return -y + 0 * x, self.hum.skel[part][2]

    def cover(self, cov, margin=0.012):
        """Coverage field like ClothingDef.coverage: {part: (t0, t1)}; negative = covered. Part membership is the
        continuous 'distance to belonging' (so cuts between covered and bare parts are smooth surfaces)."""
        R = np.full(self.grid.shape, 1.0, np.float32)
        for part, (t0, t1) in cov.items():
            r = self.part_F(part) - margin
            u, L = self.along(part)
            if t0 > 0:
                r = np.maximum(r, t0 * L - u)
            if t1 < 1:
                r = np.maximum(r, u - t1 * L)
            R = np.minimum(R, r)
        return R

    def prims(self, prims, frame_of=None):
        F, _, _ = H.eval_prims(self.grid, prims, self.hum.Wb, frame_of=frame_of)
        return F

    def sample(self, G, P):
        idx = ((P - self.grid.lo) / self.grid.h).T
        return map_coordinates(G, idx, order=1, mode="constant", cval=1.0)


class Human:
    """One body (sex + face + height/build), its armature, base body mesh and caches of built pieces."""

    def __init__(self, key, sex, face, h, w):
        self.key, self.sex, self.face, self.h, self.w = key, sex, face, h, w
        self.skel = H.skeleton(h, w)
        self.Wb = H.fk(self.skel, H.BUILD_POSE)
        self.Wr = H.fk(self.skel)
        self.prims = B.body_prims(sex, face, h, w)
        self.pieces = {}
        self.stats = {}

    # ----------------------------------------------------------- generic piece
    def piece(self, name, field, lo, hi, res=BODY_H, tris=3000, allowed=None, smooth=2, sharp=None, keep=None,
              sym=True, hides=True, keep_tris=None, inner=-0.004):
        """field(ctx) -> SDF array. Builds the decimated, skinned mesh (rest pose) + its build-pose copy."""
        if name in self.pieces:
            return self.pieces[name]
        t0 = time.time()
        ctx = Ctx(self, lo, hi, res)
        G = field(ctx)
        V, Q = H.surface_nets(G, ctx.grid)
        if inner is not None and len(Q):
            # drop the faces lying on / under the skin (inner caps of a shell): only the visible outer layer remains
            fv = ctx.sample(ctx.F, V)
            Q = Q[(fv[Q] > inner).any(1)]
            used = np.unique(Q)
            remap = np.full(len(V), -1, np.int64); remap[used] = np.arange(len(used))
            V = V[used]; Q = remap[Q]
        if len(Q) == 0:
            log("EMPTY", name)
            self.pieces[name] = None
            return None
        kp = keep(V) if keep else None
        me = H.polish(V, Q, f"{self.key}_{name}", tris, smooth, kp, sym, keep_tris)
        Vb = H.mesh_arrays(me)
        Vg = H.b2g(Vb)
        Wt = H.skin_weights(Vg, self.prims, self.Wb, allowed)
        Vr = H.lbs(Vg, Wt, self.Wb, self.Wr)
        me.vertices.foreach_set("co", H.g2b(Vr).astype(np.float32).ravel())
        me.update()
        me.shade_smooth()
        if sharp:
            me.set_sharp_from_angle(angle=math.radians(sharp))
        covers = None
        if hides and getattr(self, "body_verts", None) is not None:
            covers = ctx.sample(G, self.body_verts) < -0.0035
        pc = dict(name=name, mesh=me, weights=Wt, build=Vb, covers=covers, tris=H.tri_count(me))
        self.pieces[name] = pc
        log(f"{self.key}/{name}: {pc['tris']} tris ({time.time() - t0:.1f}s)")
        return pc

    def bounds(self, parts, pad=0.06, extra=None):
        pts = []
        for p in parts:
            _, _, ln, sg = self.skel[p]
            W = self.Wb[p]
            for v in ((0, 0, 0), (0, sg * ln, 0), (0, 0, 0.22 if p.startswith("Foot") else 0)):
                pts.append(W[:3, :3] @ np.array(v) + W[:3, 3])
        pts = np.array(pts)
        lo, hi = pts.min(0) - pad, pts.max(0) + pad
        if extra is not None:
            lo = np.minimum(lo, extra[0]); hi = np.maximum(hi, extra[1])
        return lo, hi

    # ----------------------------------------------------------- body
    def build_body(self):
        lo = (-0.78, -0.03, -0.24); hi = (0.78, 1.86 * self.h, 0.26)
        hy = self.Wb["Head"][1, 3]
        def keep(V):
            k = V[:, 1] > hy + 0.1 * self.h
            for hd in ("HandL", "HandR"):
                c = self.Wb[hd][:3, 3]
                k |= np.linalg.norm(V - c, axis=1) < 0.15
            return k
        pc = self.piece("body", lambda c: c.F, lo, hi, SKIN_H, tris=15000, keep=keep, hides=False, keep_tris=6500, inner=None)
        self.body_verts = H.b2g(pc["build"])
        self.body = pc
        # paint attributes in head space (rest = build for the head: same transform)
        return pc

    def body_paint(self, beard):
        """Per-vertex multiply colour on the skin tone: lips, cheeks, nose tip, stubble."""
        P = self.body_verts
        Wh = self.Wb["Head"]
        q = (P - Wh[:3, 3]) @ Wh[:3, :3] / self.h
        x, y, z = q[:, 0], q[:, 1], q[:, 2]
        col = np.ones((len(P), 4))
        lips = (np.abs(x) < 0.025) & (y > 0.121) & (y < 0.15) & (z > 0.078)
        cheek = np.exp(-(((np.abs(x) - 0.045) / 0.022) ** 2 + ((y - 0.165) / 0.02) ** 2)) * (z > 0.03)
        nose = np.exp(-((x / 0.015) ** 2 + ((y - 0.166) / 0.015) ** 2)) * (z > 0.09)
        red = np.clip(cheek * 0.5 + nose * 0.45, 0, 1)[:, None]
        col[:, :3] = col[:, :3] * (1 - red * 0.12) + red * 0.12 * np.array([1.0, 0.7, 0.66])
        if beard == 1:
            st = (y < 0.176) & (y > 0.085) & (z > 0.005) & ~lips & ((y > 0.1) | (z > 0.035))
            col[st, :3] *= np.array([0.66, 0.63, 0.64])
        col[lips, :3] = np.array([0.8, 0.55, 0.52])
        mouth = lips & (np.abs(y - 0.1355) < 0.003)
        col[mouth, :3] = np.array([0.45, 0.28, 0.26])
        # nails: a little paler at the finger tips
        for hd in ("HandL", "HandR"):
            Wd = self.Wb[hd]
            qh = (P - Wd[:3, 3]) @ Wd[:3, :3]
            tip = qh[:, 1] < -0.14 * self.h
            col[tip, :3] = col[tip, :3] * 0.9 + 0.1
        return col


def make_object(name, me, arm_ob, material, coll=None, weights=None, copy_mesh=False):
    if copy_mesh:
        me = me.copy()
    ob = bpy.data.objects.new(name, me)
    H.link(ob, coll)
    ob.add_rest_position_attribute = True
    if material is not None:
        me.materials.clear()
        me.materials.append(material)
    if arm_ob is not None:
        if weights is not None and [g.name for g in ob.vertex_groups][:len(H.PARTS)] != H.PARTS:
            ob.vertex_groups.clear()
            for j, p in enumerate(H.PARTS):
                vg = ob.vertex_groups.new(name=p)
                col = weights[:, j]
                for val in np.unique(np.round(col[col > 0.001], 3)):
                    vg.add(np.nonzero(np.abs(col - val) < 0.0005)[0].tolist(), float(val), "REPLACE")
        elif weights is not None:
            pass
        ob.parent = arm_ob
        md = ob.modifiers.new("Armature", "ARMATURE")
        md.object = arm_ob
    return ob


def rigid_object(name, me, arm_ob, bone, material, coll=None):
    """Mesh fully weighted to one bone (eyes)."""
    ob = bpy.data.objects.new(name, me)
    H.link(ob, coll)
    ob.add_rest_position_attribute = True
    me.materials.append(material)
    vg = ob.vertex_groups.new(name=bone)
    vg.add(list(range(len(me.vertices))), 1.0, "REPLACE")
    ob.parent = arm_ob
    md = ob.modifiers.new("Armature", "ARMATURE"); md.object = arm_ob
    return ob


def eye_mesh(name, centre_g, r):
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=10, radius=r)
    for v in bm.verts:
        v.co = v.co   # authored around the origin; moved to the head below
    bm.to_mesh(me); bm.free()
    me.shade_smooth()
    return me


# ---------------------------------------------------------------- head extras (fine grid, head-local)
def head_box(hum, below=0.06, r=0.15):
    y = hum.Wb["Head"][1, 3]
    return np.array([-r, y - below, -r - 0.03]), np.array([r, y + 0.36 * hum.h, r])


def build_hair(hum, style, cut=None):
    if style == "Bald":
        return None
    name = f"hair_{style}" + (f"_cut{cut:.3f}" if cut else "")
    lo, hi = head_box(hum, 0.18 if style in ("Long", "Ponytail") else 0.06)
    def f(c):
        x, y, z = c.loc("Head")
        G = B.hair_field(style, x, y, z, c.F, hum.h)
        if cut:
            G = np.maximum(G, y - cut * hum.h)
        return G
    return hum.piece(name, f, lo, hi, FINE_H, tris=2600 if style in ("Long", "Ponytail") else 1800, allowed=["Head"], hides=False, inner=0.001)


def build_beard(hum, kind):
    if kind < 2:
        return None
    lo, hi = head_box(hum)
    def f(c):
        x, y, z = c.loc("Head")
        return B.beard_field(kind, x, y, z, c.F, hum.h)
    return hum.piece("beard", f, lo, hi, FINE_H, tris=1400, allowed=["Head"], hides=False, inner=0.001)


def build_brows(hum):
    lo, hi = head_box(hum)
    return hum.piece("brows", lambda c: c.prims(B.brow_prims(hum.face, hum.h)), lo, hi, FINE_H, tris=300, allowed=["Head"],
                     hides=False, smooth=1, inner=-0.0005)


def build_eyes(hum):
    """Two eyeball meshes in rest pose (head bone space = build pose space for the head)."""
    out = []
    Wr = hum.Wr["Head"]
    for i, cg in enumerate(B.eye_centres(hum.h)):
        me = bpy.data.meshes.new(f"{hum.key}_eye{i}")
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=12, radius=0.0125 * hum.h)
        bm.to_mesh(me); bm.free()
        me.shade_smooth()
        world = Wr[:3, :3] @ np.array(cg) + Wr[:3, 3]
        out.append((me, H.g2b(world)))
    return out
