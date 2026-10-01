"""Lofted car body: lower tub + greenhouse, carved wheel arches, split into the game's cut parts.

Spec values are in game voxel units (see hdlib). The lower body is a closed loft of cross-section rings along Z (bottom,
tucked sill, side with an optional crease band, rounded shoulder, then either a crowned top (hood / deck) or an open tub
(cabin / pickup bed)). The greenhouse is a second loft standing on the belt (tumblehome, rounded roof edge, crowned roof)
whose faces are classified into windshield / rear / side glass and pillars. Ring stations are placed exactly on every
seam (doors, hood, trunk, windows) so cut lines are clean; panels get a dark inset seam and a little thickness.
"""
import math

import bmesh
import bpy
from mathutils import Vector, noise
from mathutils.bvhtree import BVHTree

import hdlib as H

SEC = {"well": 0, "bot": 1, "sill": 2, "side": 3, "crease": 4, "edge": 5, "top": 6, "ledge": 7, "tubwall": 8, "tubfloor": 9,
       "cap": 10, "capedge": 11, "gsill": 21, "gwin": 22, "gframe": 23, "gedge": 24, "garc": 25, "gcap": 26}


def poly(pts):
    pts = sorted(pts)

    def f(z):
        if z <= pts[0][0]:
            return pts[0][1]
        for (z0, y0), (z1, y1) in zip(pts, pts[1:]):
            if z0 <= z <= z1:
                return y0 if z1 == z0 else y0 + (y1 - y0) * (z - z0) / (z1 - z0)
        return pts[-1][1]
    return f


class G:
    """Greenhouse spec."""

    def __init__(self, z0, z1, top, base, base_ratio=0.85, roof_ratio=0.8, er=1.0, crown=0.4, sill=0.35, frame=1.0,
                 windshield=None, rear=None, roof=None, windows=(), open_windows=(), xp=6.0, rxp=None, hfull=None):
        self.z0, self.z1 = z0, z1
        self.top = poly(top) if isinstance(top, list) else top
        self.base = poly(base) if isinstance(base, list) else (lambda z, b=base: b)
        self.base_ratio, self.roof_ratio, self.er, self.crown, self.sill, self.frame = base_ratio, roof_ratio, er, crown, sill, frame
        self.windshield, self.rear, self.roof = windshield, rear, roof
        self.windows, self.open_windows = list(windows), list(open_windows)
        self.xp, self.rxp = xp, rxp if rxp is not None else xp
        if hfull is None:
            zs = [z0 + (z1 - z0) * i / 20 for i in range(21)]
            hfull = max(self.top(z) - self.base(z) for z in zs)
        self.hfull = max(1.0, hfull)


class Spec:
    def __init__(self, **kw):
        self.z0 = self.z1 = 0.0
        self.hw = 10.0
        self.nf = self.nr = 5.0
        self.pmin = 0.6
        self.bottom = self.top = None
        self.er, self.crown, self.sill, self.tuck = 1.2, 0.35, 1.3, 0.9
        self.crease = None            # (lo, hi, za, zb)
        self.accent = None            # (lo, hi, za, zb)
        self.tubs = []                # (za, zb, floor_y, inner_hw)
        self.g = None
        self.doors = []               # (name, za, zb, socket(x, y, z))
        self.hood = None              # (za, zb, hx, socket)
        self.trunk = None             # (za, zb, hx, name)
        self.tailgate = None          # ('green', z) | ('cap', z)
        self.wheels = []              # (z, y, R, socket_x)
        self.arch = 0.9               # arch clearance (voxels)
        self.arch_lift = 0.6
        self.mats = {}                # paint, roof, gpaint, sill, crease, accent
        self.dent = 0.12
        self.ds = 0.4
        self.chamfer = 0.45
        self.door_xmin = 0.0
        self.wells = True
        self.__dict__.update(kw)
        if isinstance(self.bottom, list):
            self.bottom = poly(self.bottom)
        if isinstance(self.top, list):
            self.top = poly(self.top)
        self.mats.setdefault("paint", "paint")
        self.mats.setdefault("roof", self.mats["paint"])
        self.mats.setdefault("gpaint", self.mats["paint"])
        self.mats.setdefault("sill", self.mats["paint"])
        self.mats.setdefault("crease", self.mats["paint"])
        self.mats.setdefault("accent", self.mats["paint"])
        self.mats.setdefault("cap", self.mats["paint"])

    def plan(self, z):
        if getattr(self, "planf", None):
            return self.planf(z)
        L = (self.z1 - self.z0) / 2
        u = (z - (self.z0 + self.z1) / 2) / L
        n = self.nf if u > 0 else self.nr
        a = min(abs(u), 0.999)
        return self.hw * max(self.pmin, (1 - a ** n) ** (1 / n))

    def tub(self, z):
        for za, zb, fy, wi in self.tubs:
            if za <= z <= zb:
                return fy, wi
        return None


# ---------------------------------------------------------------------------------------------------------------- rings
def lower_half(sp, z):
    yb, yt, w = sp.bottom(z), sp.top(z), sp.plan(z)
    tub = sp.tub(z)
    crown = 0.0 if tub else sp.crown
    yte = yt - crown
    Hh = max(0.2, yte - yb)
    er = min(sp.er, Hh * 0.35, w * 0.3)
    wb = w * sp.tuck
    h0 = yb + min(0.45, Hh * 0.1)
    h4 = yte - er
    h1 = yb + sp.sill
    if sp.crease and sp.crease[2] <= z <= sp.crease[3]:
        h2, h3 = sp.crease[0], sp.crease[1]
    else:
        h2 = h1 + (h4 - h1) * 0.62
        h3 = h2 + max(0.2, (h4 - h1) * 0.1)
    hs = [h0, h1, h2, h3, h4]
    for i in range(1, 5):
        hs[i] = max(hs[i], hs[i - 1] + 0.02)
    hs[4] = max(hs[4], h4)
    for i in range(3, -1, -1):
        hs[i] = min(hs[i], hs[i + 1] - 0.02)
    h0, h1, h2, h3, h4 = hs
    wt = w * 0.985
    P = [(0.0, yb), (wb * 0.5, yb), (wb, yb), (w * 0.975, h0), (w, h1)]
    S_ = ["bot", "bot", "sill", "sill"]
    for k in range(1, 5):
        P.append((w, h1 + (h2 - h1) * k / 4)); S_.append("side")
    P.append((w, h3)); S_.append("crease")
    for k in range(1, 3):
        t = k / 2
        P.append((w + (wt - w) * t, h3 + (h4 - h3) * t)); S_.append("side")
    for a in (30, 60, 90):
        r = math.radians(a)
        P.append((wt - er + er * math.cos(r), h4 + (yte - h4) * math.sin(r))); S_.append("edge")
    x0 = wt - er
    if tub:
        fy, wi = tub
        wi = min(wi, x0 - 0.15)
        fy = min(max(fy, yb + 0.6), yte - 0.3)        # floor humps over the wheel arches
        P.append((wi, yte)); S_.append("ledge")
        for k in range(1, 4):
            P.append((wi, yte + (fy - yte) * k / 3)); S_.append("tubwall")
        for k in range(1, 5):
            P.append((wi * (1 - k / 4), fy)); S_.append("tubfloor")
    else:
        for k in range(1, 9):
            x = x0 * (1 - k / 8)
            P.append((x, yte + crown * (1 - (x / x0) ** 2))); S_.append("top")
    return P, S_


def green_half(sp, z):
    g = sp.g
    yb, yt = g.base(z), g.top(z)
    w = sp.plan(z)
    w0, wr = w * g.base_ratio, w * g.roof_ratio
    Hh = max(0.02, yt - yb)
    crown = min(g.crown, Hh * 0.1)
    yte = yt - crown
    er = min(g.er, Hh * 0.3)
    hts = yte - er
    hs = yb + min(g.sill, Hh * 0.12)
    hwt = max(hs + 0.01, hts - min(g.frame, Hh * 0.15))

    def wy(y):
        t = min(1.0, max(0.0, (y - yb) / g.hfull))
        return w0 + (wr - w0) * t
    P = [(w0, yb), (wy(hs), hs)]
    S_ = ["gsill"]
    for k in range(1, 5):
        y = hs + (hwt - hs) * k / 4
        P.append((wy(y), y)); S_.append("gwin")
    P.append((wy(hts), hts)); S_.append("gframe")
    wt = wy(hts)
    for a in (30, 60, 90):
        r = math.radians(a)
        P.append((wt - er + er * math.cos(r), hts + (yte - hts) * math.sin(r))); S_.append("gedge")
    x0 = max(0.05, wt - er)
    for k in range(1, 7):
        x = x0 * (1 - k / 6)
        P.append((x, yte + crown * (1 - (x / x0) ** 2))); S_.append("garc")
    return P, S_


def full_ring(P, S_, closed):
    if closed:
        pts = P + [(-x, y) for x, y in reversed(P[1:-1])]
        secs = S_ + list(reversed(S_))
    else:  # right base -> centre -> left base
        pts = P + [(-x, y) for x, y in reversed(P[:-1])]
        secs = S_ + list(reversed(S_))
    return pts, secs


def loft(rings, closed, caps, cap_sec):
    """rings: [(z, pts, secs)] ordered by z. Returns bmesh with int face layer 'sec'."""
    bm = bmesh.new()
    lay = bm.faces.layers.int.new("sec")
    vr = [[bm.verts.new((x, y, z)) for x, y in pts] for z, pts, _ in rings]
    m = len(vr[0])
    for i in range(len(rings) - 1):
        secs = rings[i + 1][2] if rings[i + 1][2][0] == "capedge" else rings[i][2]
        for j in range(m if closed else m - 1):
            jn = (j + 1) % m
            q = list(dict.fromkeys([vr[i][j], vr[i][jn], vr[i + 1][jn], vr[i + 1][j]]))
            if len(q) < 3:
                continue
            try:
                f = bm.faces.new(q)
            except ValueError:
                continue
            f[lay] = SEC[secs[j]]
    for idx, on in ((0, caps[0]), (-1, caps[1])):
        if on:
            try:
                f = bm.faces.new(list(dict.fromkeys(vr[idx])))
                f[lay] = cap_sec
            except ValueError:
                pass
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-4, edges=bm.edges)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def stations(z0, z1, ds, keys, pairs=()):
    zs = set()
    n = max(2, int(math.ceil((z1 - z0) / ds)))
    for i in range(n + 1):
        zs.add(round(z0 + (z1 - z0) * i / n, 4))
    for k in keys:
        if z0 < k < z1:
            zs.add(round(k, 4))
    for k in pairs:
        for d in (-0.06, 0.06):
            if z0 < k + d < z1:
                zs.add(round(k + d, 4))
    zs = sorted(zs)
    keep = set(round(k, 4) for k in keys) | set(round(k + d, 4) for k in pairs for d in (-0.06, 0.06))
    out = []
    for z in zs:
        if out and z - out[-1] < 0.1 and z not in keep:
            continue
        if out and z - out[-1] < 0.1 and out[-1] not in keep and z in keep:
            out[-1] = z
            continue
        out.append(z)
    return out


def lower_rings(sp):
    keys = [sp.z0 + sp.chamfer, sp.z1 - sp.chamfer]
    for d in sp.doors:
        keys += [d[1], d[2]]
    if sp.hood:
        keys += [sp.hood[0], sp.hood[1]]
    if sp.trunk:
        keys += [sp.trunk[0], sp.trunk[1]]
    if sp.crease:
        keys += [sp.crease[2], sp.crease[3]]
    if sp.accent:
        keys += [sp.accent[2], sp.accent[3]]
    if sp.g:
        keys += [sp.g.z0, sp.g.z1]
    if sp.tailgate:
        keys.append(sp.tailgate[1])
    pairs = []
    for za, zb, _, _ in sp.tubs:
        pairs += [za, zb]
    keys += list(getattr(sp, "keys", []))
    zs = stations(sp.z0 + sp.chamfer, sp.z1 - sp.chamfer, sp.ds, keys, pairs)
    rings = []
    for z in zs:
        pts, secs = full_ring(*lower_half(sp, z), True)
        rings.append((z, pts, secs))
    # chamfered ends (rounded front / rear faces), only where the ring is not an open tub
    out = []
    for end, zc in ((0, sp.z0), (-1, sp.z1)):
        z, pts, secs = rings[end]
        if sp.tub(z) is None and sp.chamfer > 0:
            cy = sum(y for _, y in pts) / len(pts)
            k = 0.86
            sc = [(x * k, cy + (y - cy) * k) for x, y in pts]
            out.append((end, (zc, sc, ["capedge"] * len(secs))))
    for end, r in out:
        if end == 0:
            rings.insert(0, r)
        else:
            rings.append(r)
    return rings


def green_rings(sp):
    g = sp.g
    keys = []
    for zz in (g.windshield, g.rear, g.roof):
        if zz:
            keys += list(zz)
    for a, b in g.windows + g.open_windows:
        keys += [a, b]
    for d in sp.doors:
        keys += [d[1], d[2]]
    if sp.tailgate and sp.tailgate[0] == "green":
        keys.append(sp.tailgate[1])
    zs = stations(g.z0, g.z1, sp.ds, keys)
    return [(z, *full_ring(*green_half(sp, z), False)) for z in zs]


# ---------------------------------------------------------------------------------------------------------------- build
def _bisect_x(bm, xs):
    for x in xs:
        geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
        bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-4, plane_co=(x, 0, 0), plane_no=(1, 0, 0))


def _dents(bm, amp, seed):
    if amp <= 0:
        return
    bm.normal_update()
    off = Vector((seed * 1.37, seed * 0.71, seed * 2.11))
    for v in bm.verts:
        p = v.co * 0.11 + off
        d = noise.noise(p) * 0.5 + 0.5
        d = max(0.0, d - 0.55) * 2.2 + 0.25 * max(0.0, noise.noise(v.co * 0.35 + off) - 0.2)
        v.co -= v.normal * d * amp


def _boolean_arches(bm, sp):
    if not sp.wheels or not sp.wells:
        return bm
    me = bpy.data.meshes.new("_lower")
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new("_lower", me)
    bpy.context.scene.collection.objects.link(ob)
    parts = []
    for w in sp.wheels:
        z, y, R, sx = w[:4]
        spat = w[4] if len(w) > 4 else None
        tub = sp.tub(z)
        xin = max(sx - 0.6, (tub[1] + 0.25) if tub else 0.0)
        for s in (-1, 1):
            if spat is not None:     # spat: the body skirt covers the wheel above `spat`, only the bottom is open
                c = H.rbox((s * (xin + sp.hw + 3) / 2, (spat + y - R - 3) / 2, z), (sp.hw + 3 - xin, spat - (y - R - 3), 2 * (R + sp.arch)))
            else:
                c = H.revolve([(s * xin, 0.0), (s * xin, R + sp.arch), (s * (sp.hw + 3), R + sp.arch), (s * (sp.hw + 3), 0.0)], 40,
                              origin=(0, y + sp.arch_lift, z), axis=(1, 0, 0))
            parts.append(c)
    cb = H.join(*parts)
    cme = bpy.data.meshes.new("_cut")
    cb.to_mesh(cme)
    cb.free()
    cut = bpy.data.objects.new("_cut", cme)
    bpy.context.scene.collection.objects.link(cut)
    n0 = len(me.polygons)
    out = None
    for solver in ("EXACT", "FLOAT", "MANIFOLD"):
        ob.modifiers.clear()
        mod = ob.modifiers.new("arch", "BOOLEAN")
        mod.operation = "DIFFERENCE"
        mod.solver = solver
        mod.object = cut
        dg = bpy.context.evaluated_depsgraph_get()
        me2 = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
        ok = len(me2.polygons) > n0 * 0.6
        if ok:
            out = bmesh.new()
            out.from_mesh(me2)
            bpy.data.meshes.remove(me2)
            break
        print("[body] boolean %s failed (%d -> %d faces)" % (solver, n0, len(me2.polygons)), flush=True)
        bpy.data.meshes.remove(me2)
    if out is None:
        out = bmesh.new()
        out.from_mesh(me)
    for o in (ob, cut):
        bpy.data.objects.remove(o)
    for m in (me, cme):
        bpy.data.meshes.remove(m)
    return out


def thicken(bm, t):
    """Give a panel thickness: keep the skin, add an inner copy offset along -vertex normals plus edge walls."""
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    nrm = {tuple(round(c, 4) for c in v.co): v.normal.copy() for v in bm.verts}
    ret = bmesh.ops.extrude_face_region(bm, geom=list(bm.faces), use_keep_orig=True)
    for v in ret["geom"]:
        if isinstance(v, bmesh.types.BMVert):
            n = nrm.get(tuple(round(c, 4) for c in v.co))
            if n is not None:
                v.co -= n * t


def build(veh, sp, seed=1):
    """Build the body of `veh` from spec `sp`; returns a BVH (voxel units, before splitting) for placing details."""
    rings = lower_rings(sp)
    lower = loft(rings, True, (True, True), SEC["cap"])
    xs = []
    if sp.hood:
        xs += [-sp.hood[2], sp.hood[2]]
    if sp.trunk:
        xs += [-sp.trunk[2], sp.trunk[2]]
    _bisect_x(lower, xs)
    _dents(lower, sp.dent * H.WEAR, seed)
    lower = _boolean_arches(lower, sp)
    lay = lower.faces.layers.int.get("sec") or lower.faces.layers.int.new("sec")
    bm = lower
    if sp.g:
        green = loft(green_rings(sp), False, (True, True), SEC["gcap"])
        gx = [-sp.g.xp, sp.g.xp]
        if sp.g.rxp != sp.g.xp:
            gx += [-sp.g.rxp, sp.g.rxp]
        _bisect_x(green, gx)
        _dents(green, sp.dent * H.WEAR * 0.5, seed + 3)
        me = H._scratch()
        green.to_mesh(me)
        green.free()
        bm.from_mesh(me)
        lay = bm.faces.layers.int.get("sec")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    bvh = BVHTree.FromBMesh(bm)

    # ---- classify (id layers are added first: adding a layer later would invalidate the face keys below)
    play = bm.faces.layers.int.new("pid")
    mlay = bm.faces.layers.int.new("mid")
    lay = bm.faces.layers.int.get("sec")
    M = sp.mats
    g = sp.g
    inz = lambda z, r: r is not None and r[0] <= z <= r[1]

    def near_wheel(c):
        for w in sp.wheels:
            z, y, R, sx = w[:4]
            if abs(c.x) > sx - 1.0 and (c.y - y - sp.arch_lift) ** 2 + (c.z - z) ** 2 < (R + sp.arch + 1.0) ** 2:
                return True
        return False

    part_of, mat_of = {}, {}
    dele = []
    for f in bm.faces:
        s = f[lay]
        c = f.calc_center_median()
        side = "_R" if c.x > 0 else "_L"
        part, mat = "Body", M["paint"]
        if s == 0:
            mat = "wheelwell"
        elif s == SEC["bot"]:
            mat = "underbody"
        elif s == SEC["sill"]:
            mat = M["sill"]
        elif s in (SEC["tubwall"],):
            mat = "interior_trim"
        elif s == SEC["tubfloor"]:
            mat = "carpet"
        elif s in (SEC["cap"], SEC["capedge"]):
            mat = M["cap"]
        elif s == SEC["crease"] and sp.crease and inz(c.z, sp.crease[2:]):
            mat = M["crease"]
        if s in (SEC["side"], SEC["crease"]) and sp.accent and sp.accent[0] <= c.y <= sp.accent[1] and inz(c.z, sp.accent[2:]):
            mat = M["accent"]
        if s < 20:
            for name, za, zb, _ in sp.doors:
                if za <= c.z <= zb and s in (SEC["side"], SEC["crease"], SEC["edge"], SEC["ledge"]) and abs(c.x) > sp.door_xmin \
                        and not near_wheel(c):
                    part = name + side
            if sp.hood and s == SEC["top"] and inz(c.z, sp.hood[:2]) and abs(c.x) < sp.hood[2]:
                part = "Hood"
            if sp.trunk and s == SEC["top"] and inz(c.z, sp.trunk[:2]) and abs(c.x) < sp.trunk[2]:
                part = sp.trunk[3]
            if sp.tailgate and sp.tailgate[0] == "cap" and s in (SEC["cap"], SEC["capedge"]) and c.z < sp.tailgate[1]:
                part = "Tailgate"
        else:
            mat = M["gpaint"]
            if s in (SEC["gedge"], SEC["garc"]) and inz(c.z, g.roof):
                mat = M["roof"]
            glass = False
            if s == SEC["gwin"]:
                if any(a <= c.z <= b for a, b in g.open_windows):
                    dele.append(f)
                    continue
                glass = any(a <= c.z <= b for a, b in g.windows)
            elif s == SEC["garc"]:
                glass = (inz(c.z, g.windshield) and abs(c.x) < g.xp) or (inz(c.z, g.rear) and abs(c.x) < g.rxp)
            if glass:
                mat = "glass"
                part = "Glass"
            if s in (SEC["gsill"], SEC["gwin"], SEC["gframe"]):
                for name, za, zb, _ in sp.doors:
                    if za <= c.z <= zb:
                        part = name + side
            if sp.tailgate and sp.tailgate[0] == "green" and c.z < sp.tailgate[1] and s in (SEC["garc"], SEC["gedge"], SEC["gcap"]):
                part = "Tailgate"
        part_of[f] = part
        mat_of[f] = mat
    if dele:
        bmesh.ops.delete(bm, geom=dele, context="FACES_ONLY")

    # ---- glass rubber seals, then panel seams
    groups = {}
    for f, p in part_of.items():
        if f.is_valid and mat_of[f] == "glass":
            groups.setdefault(p, []).append(f)
    for p, fs in groups.items():
        res = bmesh.ops.inset_region(bm, faces=fs, thickness=0.16, depth=-0.14, use_even_offset=False)
        for nf in res["faces"]:
            part_of[nf] = p
            mat_of[nf] = "seal"
    panels = {}
    for f, p in part_of.items():
        if f.is_valid and p not in ("Body", "Glass"):
            panels.setdefault(p, []).append(f)
    for p, fs in panels.items():
        res = bmesh.ops.inset_region(bm, faces=fs, thickness=0.2, depth=-0.1, use_even_offset=False)
        for nf in res["faces"]:
            part_of[nf] = p
            mat_of[nf] = M.get("seam", "seam")

    # ---- split into parts (part / material ids ride along in face layers, so the copies keep them)
    names = sorted(set(part_of[f] for f in bm.faces if f in part_of) | {"Body"})
    allm = sorted(set(mat_of.values()) | {M["paint"]})
    for f in bm.faces:
        f[play] = names.index(part_of.get(f, "Body"))
        f[mlay] = allm.index(mat_of.get(f, M["paint"]))
    for name in names:
        pi = names.index(name)
        cp = bm.copy()
        pl, ml = cp.faces.layers.int.get("pid"), cp.faces.layers.int.get("mid")
        bmesh.ops.delete(cp, geom=[f for f in cp.faces if f[pl] != pi], context="FACES")
        if not cp.faces:
            cp.free()
            continue
        mats = []
        for cf in cp.faces:
            mn = allm[cf[ml]]
            if mn not in mats:
                mats.append(mn)
            cf.material_index = mats.index(mn)
        if name != "Body" and name != "Glass":
            thicken(cp, 0.25)
        origin = (0, 0, 0)
        props = {}
        base = name[:-2] if name[-2:] in ("_R", "_L") else name
        sgn = -1 if name.endswith("_L") else 1
        for dn, za, zb, sock in sp.doors:
            if base == dn:
                origin = (sgn * sock[0], sock[1], sock[2])
                props = {"socket": ("door" if dn == "Door" else "door_rear") + ("" if sgn > 0 else "_L"), "mirrored": sgn < 0}
        if name == "Hood" and sp.hood:
            origin = sp.hood[3]
            props = {"socket": "hood"}
        veh.part(name, origin, **props).put(cp, mats=mats)
    bm.free()
    return bvh
