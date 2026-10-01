"""HD human toolkit for the Mad Mike's Garage preview (Blender 5, numpy).

Everything is authored in GAME space (Unity: +X right, +Y up, +Z forward, metres) with the exact HumanRig skeleton
(HumanDesign.Skeleton), as signed-distance primitives per body part. Shapes are built in a separation pose (A-pose),
polygonised with naive surface nets, smoothed + decimated in Blender, skinned from the per-part distances and then
deformed back to the game's rest pose (arms hanging, every bone with identity rotation) - that rest pose is the bind
pose, so HumanAnimator's local Euler rotations drive the HD skeleton unchanged.

Game -> Blender axis map (what Unity's FBX importer undoes): (x, y, z) -> (-x, -z, y); the character faces Blender -Y.
"""
import math

import bpy
import numpy as np
from mathutils import Matrix

PARTS = ["Pelvis", "Chest", "Head", "UpperArmL", "UpperArmR", "ForearmL", "ForearmR", "HandL", "HandR",
         "ThighL", "ThighR", "ShinL", "ShinR", "FootL", "FootR"]
PI = {n: i for i, n in enumerate(PARTS)}
PARENT = {"Pelvis": None, "Chest": "Pelvis", "Head": "Chest", "UpperArmL": "Chest", "UpperArmR": "Chest",
          "ForearmL": "UpperArmL", "ForearmR": "UpperArmR", "HandL": "ForearmL", "HandR": "ForearmR",
          "ThighL": "Pelvis", "ThighR": "Pelvis", "ShinL": "ThighL", "ShinR": "ThighR", "FootL": "ShinL", "FootR": "ShinR"}
NEIGH = {p: set() for p in PARTS}
for _c, _p in PARENT.items():
    if _p:
        NEIGH[_c].add(_p); NEIGH[_p].add(_c)
NEIGH["ThighL"].add("ThighR"); NEIGH["ThighR"].add("ThighL")

# game -> blender
GB = np.array([[-1, 0, 0], [0, 0, -1], [0, 1, 0]], dtype=np.float64)


def g2b(v):
    return np.asarray(v, dtype=np.float64) @ GB.T


def b2g(v):
    return np.asarray(v, dtype=np.float64) @ GB      # GB is orthogonal: inverse = transpose


def srgb_lin(hexstr):
    c = [int(hexstr[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return tuple([x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [1.0])


# ---------------------------------------------------------------- skeleton (HumanDesign.Skeleton)
def skeleton(h=1.0, w=1.0):
    """part -> (parent, offset (game, parent-local), length, axis sign: +1 grows up the bone's +Y, -1 down)."""
    return {
        "Pelvis": (None, (0, 0.94 * h, 0), 0.1 * h, 1),
        "Chest": ("Pelvis", (0, 0.1 * h, 0), 0.44 * h, 1),
        "Head": ("Chest", (0, 0.44 * h, 0), 0.3 * h, 1),
        "UpperArmL": ("Chest", (-0.2 * w, 0.39 * h, 0), 0.29 * h, -1),
        "UpperArmR": ("Chest", (0.2 * w, 0.39 * h, 0), 0.29 * h, -1),
        "ForearmL": ("UpperArmL", (0, -0.29 * h, 0), 0.25 * h, -1),
        "ForearmR": ("UpperArmR", (0, -0.29 * h, 0), 0.25 * h, -1),
        "HandL": ("ForearmL", (0, -0.25 * h, 0), 0.09 * h, -1),
        "HandR": ("ForearmR", (0, -0.25 * h, 0), 0.09 * h, -1),
        "ThighL": ("Pelvis", (-0.09 * w, -0.02 * h, 0), 0.43 * h, -1),
        "ThighR": ("Pelvis", (0.09 * w, -0.02 * h, 0), 0.43 * h, -1),
        "ShinL": ("ThighL", (0, -0.43 * h, 0), 0.43 * h, -1),
        "ShinR": ("ThighR", (0, -0.43 * h, 0), 0.43 * h, -1),
        "FootL": ("ShinL", (0, -0.43 * h, 0), 0.08 * h, -1),
        "FootR": ("ShinR", (0, -0.43 * h, 0), 0.08 * h, -1),
    }


def unity_euler(x, y, z):
    """Quaternion.Euler(x, y, z) as a 3x3 matrix (z, then x, then y about the fixed axes)."""
    def rx(a):
        c, s = math.cos(a), math.sin(a); return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])
    def ry(a):
        c, s = math.cos(a), math.sin(a); return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
    def rz(a):
        c, s = math.cos(a), math.sin(a); return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])
    r = math.radians
    return ry(r(y)) @ rx(r(x)) @ rz(r(z))


def fk(skel, pose=None, pelvis_offset=(0, 0, 0)):
    """World 4x4 matrices (game space) per part. pose: part -> (x, y, z) Unity Euler degrees."""
    pose = pose or {}
    W = {}
    for p in PARTS:
        par, off, _, _ = skel[p]
        M = np.eye(4)
        M[:3, 3] = off
        if p == "Pelvis":
            M[:3, 3] = np.add(off, pelvis_offset)
        M[:3, :3] = unity_euler(*pose.get(p, (0, 0, 0)))
        W[p] = (W[par] @ M) if par else M
    return W


BUILD_POSE = {"UpperArmL": (0, 0, -38), "UpperArmR": (0, 0, 38), "ForearmL": (-6, 0, 0), "ForearmR": (-6, 0, 0),
              "ThighL": (0, 0, -7), "ThighR": (0, 0, 7), "FootL": (0, 0, 7), "FootR": (0, 0, -7)}


# ---------------------------------------------------------------- SDF primitives (evaluated in a part's local frame)
class Prim:
    """kind: sph(c,r) ell(c,rad) cap(a,b,ra,rb) box(c,half,round) cyl(c,r,halfh,round) tor(c,R,r)
    rot: optional 3x3 (columns = primitive axes in part-local coords). op: add|sub|int. k: blend radius."""

    def __init__(self, part, kind, k=0.02, op="add", rot=None, **kw):
        self.part, self.kind, self.k, self.op, self.rot, self.kw = part, kind, k, op, rot, kw

    def local_bounds(self):
        kw = self.kw
        if self.kind == "cap":
            a, b = np.array(kw["a"], float), np.array(kw["b"], float)
            r = max(kw["ra"], kw["rb"])
            return np.minimum(a, b) - r, np.maximum(a, b) + r
        c = np.array(kw["c"], float)
        if self.kind == "sph":
            e = kw["r"]
        elif self.kind == "ell":
            e = max(kw["rad"])
        elif self.kind == "box":
            e = float(np.linalg.norm(kw["half"])) + kw.get("round", 0)
        elif self.kind == "cyl":
            e = math.hypot(kw["r"], kw["halfh"]) + kw.get("round", 0)
        elif self.kind == "tor":
            e = kw["R"] + kw["r"]
        return c - e, c + e

    def eval(self, x, y, z):
        """x, y, z: part-local coordinate arrays (broadcastable)."""
        kw = self.kw
        if self.kind == "cap":
            a = np.array(kw["a"], float); b = np.array(kw["b"], float); ba = b - a
            px, py, pz = x - a[0], y - a[1], z - a[2]
            t = np.clip((px * ba[0] + py * ba[1] + pz * ba[2]) / float(ba @ ba), 0, 1)
            dx, dy, dz = px - ba[0] * t, py - ba[1] * t, pz - ba[2] * t
            return np.sqrt(dx * dx + dy * dy + dz * dz) - (kw["ra"] + (kw["rb"] - kw["ra"]) * t)
        c = kw["c"]
        qx, qy, qz = x - c[0], y - c[1], z - c[2]
        if self.rot is not None:
            R = np.asarray(self.rot)
            qx, qy, qz = (qx * R[0, 0] + qy * R[1, 0] + qz * R[2, 0], qx * R[0, 1] + qy * R[1, 1] + qz * R[2, 1],
                          qx * R[0, 2] + qy * R[1, 2] + qz * R[2, 2])
        if self.kind == "sph":
            return np.sqrt(qx * qx + qy * qy + qz * qz) - kw["r"]
        if self.kind == "ell":
            rx, ry, rz = kw["rad"]
            k0 = np.sqrt((qx / rx) ** 2 + (qy / ry) ** 2 + (qz / rz) ** 2)
            k1 = np.sqrt((qx / rx / rx) ** 2 + (qy / ry / ry) ** 2 + (qz / rz / rz) ** 2)
            return k0 * (k0 - 1.0) / np.maximum(k1, 1e-9)
        if self.kind == "box":
            hx, hy, hz = kw["half"]; rr = kw.get("round", 0)
            dx, dy, dz = np.abs(qx) - hx + rr, np.abs(qy) - hy + rr, np.abs(qz) - hz + rr
            out = np.sqrt(np.maximum(dx, 0) ** 2 + np.maximum(dy, 0) ** 2 + np.maximum(dz, 0) ** 2)
            return out + np.minimum(np.maximum(dx, np.maximum(dy, dz)), 0) - rr
        if self.kind == "cyl":   # axis = local y
            rr = kw.get("round", 0)
            dr = np.sqrt(qx * qx + qz * qz) - kw["r"] + rr
            dh = np.abs(qy) - kw["halfh"] + rr
            return np.minimum(np.maximum(dr, dh), 0) + np.sqrt(np.maximum(dr, 0) ** 2 + np.maximum(dh, 0) ** 2) - rr
        if self.kind == "tor":   # ring in the local xz plane
            q = np.sqrt(qx * qx + qz * qz) - kw["R"]
            return np.sqrt(q * q + qy * qy) - kw["r"]
        raise ValueError(self.kind)


def rot_x(deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])


def rot_y(deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])


def rot_z(deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


def smin(a, b, k):
    if k <= 0:
        return np.minimum(a, b)
    hh = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b + (a - b) * hh - k * hh * (1 - hh)


def smax(a, b, k):
    return -smin(-a, -b, k)


# ---------------------------------------------------------------- grids
class Grid:
    def __init__(self, lo, hi, h):
        self.h = h
        self.lo = np.array(lo, float)
        self.shape = tuple(int(math.ceil((hi[i] - lo[i]) / h)) + 1 for i in range(3))
        self.ax = [self.lo[i] + np.arange(self.shape[i]) * h for i in range(3)]

    def slices(self, wlo, whi):
        sl = []
        for i in range(3):
            a = max(0, int(math.floor((wlo[i] - self.lo[i]) / self.h)))
            b = min(self.shape[i], int(math.ceil((whi[i] - self.lo[i]) / self.h)) + 1)
            if b <= a:
                return None
            sl.append(slice(a, b))
        return tuple(sl)

    def local(self, W, sl=None):
        """Coordinates of grid points (optionally a slice) in the frame W (4x4 world-from-local)."""
        sl = sl or (slice(None),) * 3
        gx = self.ax[0][sl[0]][:, None, None] - W[0, 3]
        gy = self.ax[1][sl[1]][None, :, None] - W[1, 3]
        gz = self.ax[2][sl[2]][None, None, :] - W[2, 3]
        R = W[:3, :3]
        return (gx * R[0, 0] + gy * R[1, 0] + gz * R[2, 0], gx * R[0, 1] + gy * R[1, 1] + gz * R[2, 1],
                gx * R[0, 2] + gy * R[1, 2] + gz * R[2, 2])


def world_bounds(prim, W, pad):
    lo, hi = prim.local_bounds()
    cs = np.array([[x, y, z] for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])])
    w = cs @ W[:3, :3].T + W[:3, 3]
    return w.min(0) - pad, w.max(0) + pad


def eval_prims(grid, prims, W, F=None, label=None, best=None, frame_of=None):
    """Smooth-union the 'add' prims into F (and track the nearest part label), then apply 'sub'/'int' prims.
    frame_of(prim) -> 4x4 world frame (default: W[prim.part])."""
    if F is None:
        F = np.full(grid.shape, 1.0, np.float32)
    if label is None:
        label = np.full(grid.shape, 255, np.uint8)
    if best is None:
        best = np.full(grid.shape, 1.0, np.float32)
    frame_of = frame_of or (lambda pr: W[pr.part])
    for pr in prims:
        if pr.op != "add":
            continue
        Wp = frame_of(pr)
        wlo, whi = world_bounds(pr, Wp, pr.k + 3 * grid.h)
        sl = grid.slices(wlo, whi)
        if sl is None:
            continue
        x, y, z = grid.local(Wp, sl)
        d = pr.eval(x, y, z).astype(np.float32)
        F[sl] = smin(F[sl], d, pr.k)
        b = best[sl]; lb = label[sl]
        m = d < b
        b[m] = d[m]
        if pr.part in PI:
            lb[m] = PI[pr.part]
    for pr in prims:
        if pr.op == "add":
            continue
        Wp = frame_of(pr)
        if pr.op == "sub":
            wlo, whi = world_bounds(pr, Wp, pr.k + 3 * grid.h)
            sl = grid.slices(wlo, whi)
            if sl is None:
                continue
            x, y, z = grid.local(Wp, sl)
            F[sl] = smax(F[sl], -pr.eval(x, y, z).astype(np.float32), pr.k)
        else:   # int: intersect everywhere
            x, y, z = grid.local(Wp)
            F[...] = smax(F, pr.eval(x, y, z).astype(np.float32), pr.k)
    return F, label, best


def eval_points(P, prims, W, frame_of=None):
    """Raw (unblended) minimum distance per part at points P (N,3 game space) -> (N, len(PARTS))."""
    frame_of = frame_of or (lambda pr: W[pr.part])
    D = np.full((len(P), len(PARTS)), 1.0)
    for pr in prims:
        if pr.op != "add" or pr.part not in PI:
            continue
        Wp = frame_of(pr)
        q = (P - Wp[:3, 3]) @ Wp[:3, :3]
        d = pr.eval(q[:, 0], q[:, 1], q[:, 2])
        j = PI[pr.part]
        D[:, j] = np.minimum(D[:, j], d)
    return D


# ---------------------------------------------------------------- surface nets
_CORN = np.array([[i, j, k] for i in (0, 1) for j in (0, 1) for k in (0, 1)])
_EDGES = [(a, b) for a in range(8) for b in range(a + 1, 8) if np.abs(_CORN[a] - _CORN[b]).sum() == 1]


def surface_nets(F, grid):
    """Naive surface nets: quads around the iso-0 surface of F (negative inside). Returns verts (game), quads."""
    ins = F < 0
    nx, ny, nz = F.shape
    cnt = np.zeros((nx - 1, ny - 1, nz - 1), np.uint8)
    for c in _CORN:
        cnt += ins[c[0]:nx - 1 + c[0], c[1]:ny - 1 + c[1], c[2]:nz - 1 + c[2]]
    act = (cnt > 0) & (cnt < 8)
    ci = np.argwhere(act)
    if len(ci) == 0:
        return np.zeros((0, 3)), np.zeros((0, 4), np.int64)
    vid = np.full(cnt.shape, -1, np.int64)
    vid[act] = np.arange(len(ci))
    vals = np.stack([F[ci[:, 0] + c[0], ci[:, 1] + c[1], ci[:, 2] + c[2]] for c in _CORN], 1).astype(np.float64)
    acc = np.zeros((len(ci), 3)); n = np.zeros(len(ci))
    for a, b in _EDGES:
        fa, fb = vals[:, a], vals[:, b]
        m = (fa < 0) != (fb < 0)
        t = np.where(m, fa / np.where(m, fa - fb, 1), 0)
        pos = _CORN[a][None, :] + t[:, None] * (_CORN[b] - _CORN[a])[None, :]
        acc[m] += pos[m]; n[m] += 1
    verts = grid.lo + (ci + acc / np.maximum(n, 1)[:, None]) * grid.h
    quads = []
    for ax in range(3):
        u, v = (ax + 1) % 3, (ax + 2) % 3
        e0 = [slice(None)] * 3; e1 = [slice(None)] * 3
        e0[ax] = slice(0, F.shape[ax] - 1); e1[ax] = slice(1, F.shape[ax])
        d = ins[tuple(e0)] != ins[tuple(e1)]
        idx = np.argwhere(d)
        ok = (idx[:, u] >= 1) & (idx[:, v] >= 1) & (idx[:, u] <= F.shape[u] - 2) & (idx[:, v] <= F.shape[v] - 2)
        idx = idx[ok]
        inside_low = ins[idx[:, 0], idx[:, 1], idx[:, 2]]
        cells = []
        for du, dv in ((-1, -1), (0, -1), (0, 0), (-1, 0)):
            c = idx.copy(); c[:, u] += du; c[:, v] += dv
            cells.append(vid[c[:, 0], c[:, 1], c[:, 2]])
        q = np.stack(cells, 1)
        q[~inside_low] = q[~inside_low][:, ::-1]
        quads.append(q)
    q = np.concatenate(quads)
    q = q[(q >= 0).all(1)]
    return verts, q


# ---------------------------------------------------------------- Blender mesh helpers
def mesh_from_numpy(name, verts_b, faces):
    me = bpy.data.meshes.new(name)
    nv = len(verts_b)
    me.vertices.add(nv)
    me.vertices.foreach_set("co", np.asarray(verts_b, np.float32).ravel())
    if isinstance(faces, np.ndarray):
        sizes = np.full(len(faces), faces.shape[1], np.int32)
        flat = faces.ravel().astype(np.int32)
    else:
        sizes = np.array([len(f) for f in faces], np.int32)
        flat = np.concatenate([np.asarray(f, np.int32) for f in faces])
    me.loops.add(len(flat))
    me.loops.foreach_set("vertex_index", flat)
    me.polygons.add(len(sizes))
    starts = np.concatenate([[0], np.cumsum(sizes)[:-1]]).astype(np.int32)
    me.polygons.foreach_set("loop_start", starts)
    me.update(calc_edges=True)
    me.validate()
    return me


def mesh_arrays(me):
    v = np.zeros(len(me.vertices) * 3, np.float32)
    me.vertices.foreach_get("co", v)
    return v.reshape(-1, 3).astype(np.float64)


def tri_count(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


def link(obj, coll=None):
    (coll or bpy.context.scene.collection).objects.link(obj)
    return obj


def polish(verts_g, quads, name, target_tris, smooth=2, keep=None, sym=True, keep_tris=None):
    """Surface-nets quads -> smoothed, decimated Blender mesh (still in build pose).
    keep: per-vertex bool 'detail region' which is decimated separately down to keep_tris (faces, hands)."""
    vb = g2b(verts_g)
    me = mesh_from_numpy(name + "_raw", vb, quads[:, ::-1])    # mirror axis map flips the winding
    ob = bpy.data.objects.new(name + "_raw", me)
    link(ob)
    if smooth:
        m = ob.modifiers.new("smooth", "SMOOTH"); m.factor = 0.6; m.iterations = smooth
    tris = 2 * len(quads)
    def dec(nm, ratio, group=None):
        m = ob.modifiers.new(nm, "DECIMATE")
        m.decimate_type = "COLLAPSE"
        m.ratio = min(1.0, ratio)
        m.use_collapse_triangulate = True
        if sym:
            m.use_symmetry = True; m.symmetry_axis = "X"
        if group:
            m.vertex_group = group; m.vertex_group_factor = 1.0
    if target_tris and tris > target_tris:
        if keep is not None and keep_tris and keep.any():
            kd = np.asarray(keep, bool)
            ga = ob.vertex_groups.new(name="rest"); ga.add(np.nonzero(~kd)[0].tolist(), 1.0, "REPLACE")
            gb = ob.vertex_groups.new(name="detail"); gb.add(np.nonzero(kd)[0].tolist(), 1.0, "REPLACE")
            nd = 2 * kd.sum(); nb = tris - nd
            tb = max(target_tris - keep_tris, 200)
            dec("dec_rest", (tb + nd) / tris, "rest")
            dec("dec_detail", (tb + keep_tris) / (tb + nd), "detail")
        else:
            dec("dec", target_tris / tris)
    dg = bpy.context.evaluated_depsgraph_get()
    out = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    out.name = name
    bpy.data.objects.remove(ob); bpy.data.meshes.remove(me)
    import bmesh
    bm = bmesh.new(); bm.from_mesh(out)
    dl = bm.verts.layers.deform.active
    if dl is not None:
        bm.verts.layers.deform.remove(dl)
    bm.to_mesh(out); bm.free()
    return out


# ---------------------------------------------------------------- skinning
def skin_weights(P, prims, W, allowed=None, tau=0.012, cut=0.045):
    """Per-vertex weights over PARTS from the raw part distances: nearest part + its neighbours, soft falloff."""
    D = eval_points(P, prims, W)
    if allowed is not None:
        mask = np.array([p in allowed for p in PARTS])
        D[:, ~mask] = 9.0
    best = D.argmin(1)
    Wt = np.zeros_like(D)
    for i, p in enumerate(PARTS):
        rows = best == i
        if not rows.any():
            continue
        cand = [i] + [PI[n] for n in NEIGH[p] if allowed is None or n in allowed]
        dm = D[rows][:, [i]]
        for j in cand:
            dd = D[rows, j] - dm[:, 0]
            Wt[rows, j] = np.where(dd < cut, np.exp(-dd / tau), 0)
    Wt /= Wt.sum(1, keepdims=True)
    # keep the 3 strongest
    order = np.argsort(-Wt, 1)
    Wt[np.arange(len(Wt))[:, None], order[:, 3:]] = 0
    Wt /= Wt.sum(1, keepdims=True)
    return Wt


def lbs(P, Wt, Wfrom, Wto):
    """Linear blend skinning of game-space points from pose Wfrom to pose Wto."""
    out = np.zeros_like(P)
    for j, p in enumerate(PARTS):
        w = Wt[:, j]
        if not w.any():
            continue
        M = Wto[p] @ np.linalg.inv(Wfrom[p])
        out += w[:, None] * (P @ M[:3, :3].T + M[:3, 3])
    return out


# ---------------------------------------------------------------- armature
def make_armature(name, skel, coll=None):
    Wr = fk(skel)
    arm = bpy.data.armatures.new(name)
    ob = bpy.data.objects.new(name, arm)
    link(ob, coll)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode="EDIT")
    root = arm.edit_bones.new("Root")
    root.head = (0, 0, 0); root.tail = (0, 0, 0.2)
    eb = {"Root": root}
    for p in PARTS:
        par, _, ln, sgn = skel[p]
        b = arm.edit_bones.new(p)
        hd = g2b(Wr[p][:3, 3])
        tl = g2b(Wr[p][:3, 3] + np.array([0, sgn * ln, 0]))
        if p.startswith("Foot"):
            tl = g2b(Wr[p][:3, 3] + np.array([0, -0.045, 0.11]))
        b.head = hd.tolist(); b.tail = tl.tolist(); b.roll = 0
        b.parent = eb[par] if par else root
        b.use_connect = False
        eb[p] = b
    bpy.ops.object.mode_set(mode="OBJECT")
    return ob


def pose_armature(arm_ob, pose, pelvis_offset=(0, 0, 0)):
    """Apply HumanAnimator-style local Euler rotations (game convention, identity rest) to the Blender armature."""
    for pb in arm_ob.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
    for p, e in pose.items():
        pb = arm_ob.pose.bones[p]
        Rg = unity_euler(*e)
        Rb = GB @ Rg @ GB.T
        B = np.array(arm_ob.data.bones[p].matrix_local.to_3x3())
        basis = B.T @ Rb @ B
        pb.rotation_quaternion = Matrix(basis.tolist()).to_quaternion()
    if any(pelvis_offset):
        B = np.array(arm_ob.data.bones["Pelvis"].matrix_local.to_3x3())
        arm_ob.pose.bones["Pelvis"].location = (B.T @ g2b(pelvis_offset)).tolist()
    bpy.context.view_layer.update()
