"""CityKit (Runtime/World/RollingCity/CityKit.cs) for the hdkit assets: the city pieces are authored in the generator's
axes (x along = the piece's front, game +Z; y across = game -X; z up), so their C# builders port line for line.
Every primitive is built in generator space, mapped to game space (x, y, z) = (-gy, gz, gx) (a reflection, so the
winding is flipped) and added to an hdkit GP. Extra materials of the city look (diamond deck plate, dark rust,
stack paint, cyan glow) are registered with `mats()`.
"""
import math

import bmesh
from mathutils import Euler, Matrix, Vector

import hdkit as K

REF = Matrix(((0, -1, 0, 0), (0, 0, 1, 0), (1, 0, 0, 0), (0, 0, 0, 1)))   # generator -> game


def g2g(p):
    """Generator point -> game point."""
    return tuple(REF @ Vector((p[0], p[1], p[2], 1.0)))[:3]


def mats():
    """City materials on top of hdkit's (call at the start of every asset builder: run_tiles rebuilds M per tile)."""
    M = K.M
    if "deck_plate" in M:
        return
    T, S, P, H = K.kit.textured, K.kit.surface, K.kit.P, K.kit._hex
    M["deck_plate"] = T("CityDeckPlate", "tread", P("chrome", 1), P("metal", 2), dust=0.4, rough=0.45, bump=0.7, rot="X")
    M["rust_dark"] = S("CityRustDark", P("rust", 1), rust=0.7, dust=0.35, metal=0.25, rough=0.85, var=0.3, bump=0.8, streaks=0.4)
    M["stack_red"] = S("StackRedPaint", H("a8281c"), chips=0.45, chip_col=P("rust", 2), rust=0.35, dust=0.3, rough=0.5, streaks=0.5)
    M["stack_white"] = S("StackWhitePaint", P("cream", 3), chips=0.45, chip_col=P("rust", 2), rust=0.3, dust=0.35, rough=0.5, streaks=0.5)
    M["pipe"] = S("PipeGrey", H("6a6c6e"), rust=0.45, dust=0.35, metal=0.6, rough=0.6, var=0.2, streaks=0.4)
    M["glow_cyan"] = K.kit.emissive("LampCyanGlow", H("6af0ff"), 3.0)
    M["glow_sodium"] = K.kit.emissive("LampSodium", H("ffb15a"), 4.0)
    M["deck_paint"] = S("DeckYellow", H("d4a420"), chips=0.5, chip_col=P("metal", 1), rust=0.3, dust=0.4, rough=0.55)


class CK:
    """Generator-space primitives into one GP (`gp`); `use(gp)` switches the target."""

    def __init__(self, gp):
        self.gp = gp

    def use(self, gp):
        self.gp = gp
        return self

    def _add(self, bm, m, M=None, gp=None):
        T = REF @ (M if M is not None else Matrix.Identity(4))
        for v in bm.verts:
            v.co = T @ v.co
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
        (gp or self.gp).add(bm, m)

    # ---------------------------------------------------------------------------------------------- CityKit ports
    def box(self, m, size, loc, euler=(0, 0, 0), bevel=0.006, segs=2, gp=None):
        """Oriented box: size along its local axes, centre, rotation (Blender XYZ Euler, radians)."""
        M = Matrix.Translation(Vector(loc)) @ Euler(euler, "XYZ").to_matrix().to_4x4()
        self._add(K.kit.bm_box(size[0], size[1], size[2], bevel=bevel, segs=segs), m, M, gp)

    def span(self, m, x0, x1, y0, y1, z0, z1, bevel=0.006, segs=2, gp=None):
        x0, x1 = min(x0, x1), max(x0, x1)
        y0, y1 = min(y0, y1), max(y0, y1)
        if x1 - x0 <= 1e-3 or y1 - y0 <= 1e-3 or z1 - z0 <= 1e-3:
            return
        self.box(m, (x1 - x0, y1 - y0, z1 - z0), ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), bevel=bevel, segs=segs, gp=gp)

    def beam(self, m, p, q, t, t2=None, bevel=0.004, gp=None):
        """Square section t x t2 from p to q (to_track_quat('Z', 'Y') frame like CityKit.Beam)."""
        p, q = Vector(p), Vector(q)
        d = q - p
        L = d.length
        if L < 1e-3:
            return
        t2 = t if t2 is None else t2
        R = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
        M = Matrix.Translation((p + q) / 2) @ R
        self._add(K.kit.bm_box(t, t2, L, bevel=bevel), m, M, gp)

    def tube(self, m, p, q, r, segs=16, caps=True, bevel=0.0, r2=None, gp=None):
        p, q = Vector(p), Vector(q)
        d = q - p
        L = d.length
        if L < 1e-3:
            return
        R = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
        M = Matrix.Translation((p + q) / 2) @ R
        self._add(K.kit.bm_cyl(r, L, segs, bevel, r2, caps), m, M, gp)

    def lathe(self, m, base, axis, prof, segs=20, close=True, gp=None):
        """Revolve [(radius, distance along axis)] about `axis` from `base`."""
        a = Vector(axis).normalized()
        R = a.to_track_quat("Z", "Y").to_matrix().to_4x4()
        self._add(K.kit.bm_lathe(prof, segs, "Z", close), m, Matrix.Translation(Vector(base)) @ R, gp)

    def ring(self, m, c, axis, R, r, segs=24, rsegs=6, gp=None):
        a = Vector(axis).normalized()
        Q = a.to_track_quat("Z", "Y").to_matrix().to_4x4()
        self._add(K.kit.bm_torus(R, r, segs, rsegs), m, Matrix.Translation(Vector(c)) @ Q, gp)

    def ball(self, m, c, r, scale=(1, 1, 1), u=12, v=8, gp=None):
        self._add(K.kit.bm_sphere(r, u, v), m, Matrix.Translation(Vector(c)) @ Matrix.Diagonal((*scale, 1)), gp)

    def poly(self, m, pts, r=0.02, segs=8, gp=None):
        """A round rod along a polyline (cables, hoses, rails)."""
        self._add(K.kit.bm_tube([Vector(p) for p in pts], r, segs, True), m, None, gp)

    def cable(self, p, q, sag, n=10, t=0.025, m="steel_dark", gp=None):
        p, q = Vector(p), Vector(q)
        pts = [p.lerp(q, i / n) - Vector((0, 0, sag * 4 * (i / n) * (1 - i / n))) for i in range(n + 1)]
        self.poly(m, pts, t * 0.5, 6, gp)

    def ladder(self, p, q, w=0.5, step=0.3, m="steel", gp=None):
        p, q = Vector(p), Vector(q)
        d = q - p
        side = d.cross(Vector((0, 0, 1)))
        side = (side.normalized() if side.length_squared > 1e-6 else Vector((1, 0, 0))) * w * 0.5
        self.tube(m, p - side, q - side, 0.028, 8, gp=gp)
        self.tube(m, p + side, q + side, 0.028, 8, gp=gp)
        n = int(d.length / step)
        for i in range(1, n):
            c = p + d * (i * step / d.length)
            self.tube(m, c - side, c + side, 0.017, 6, gp=gp)

    def railing(self, pts, z, h=1.1, spacing=1.5, toe=True, gp=None):
        """Posts + top rail + mid rail (+ toe board) along (x, y) points at floor height z (CityKit.Railing)."""
        up = Vector((0, 0, h))
        for s in range(len(pts) - 1):
            p = Vector((pts[s][0], pts[s][1], z))
            q = Vector((pts[s + 1][0], pts[s + 1][1], z))
            n = max(1, int(math.ceil((q - p).length / spacing)))
            for i in range(n):
                a, b = p.lerp(q, i / n), p.lerp(q, (i + 1) / n)
                self.tube("steel", a, a + up, 0.032, 10, bevel=0.0, gp=gp)
                self.box("steel_dark", (0.12, 0.12, 0.012), (a.x, a.y, z + 0.006), gp=gp)              # base plate
                if i == n - 1:
                    self.tube("steel", b, b + up, 0.032, 10, gp=gp)
                    self.box("steel_dark", (0.12, 0.12, 0.012), (b.x, b.y, z + 0.006), gp=gp)
                self.tube("steel", a + up, b + up, 0.03, 10, gp=gp)
                self.tube("steel", a + up * 0.5, b + up * 0.5, 0.018, 8, gp=gp)
                if toe:
                    d = (b - a).normalized()
                    self.beam("deck_paint", a + Vector((0, 0, 0.08)) + d * 0.04, b + Vector((0, 0, 0.08)) - d * 0.04, 0.02, 0.15, 0.003, gp=gp)

    def rivets(self, m, pts, normal, r=0.014, h=0.01, gp=None):
        nrm = Vector(normal).normalized()
        for p in pts:
            p = Vector(p)
            self.tube(m, p, p + nrm * h, r, 6, gp=gp)


def P_(x, y, z):
    return Vector((x, y, z))
