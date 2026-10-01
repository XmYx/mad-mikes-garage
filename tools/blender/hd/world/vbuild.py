"""Building helpers for the HD world templates: walls, slabs, stairs and openings that follow the voxel builders in
BiomeProps.cs (Walls / Slab / Stairs) exactly, so the HD Shell voxelises back to the same structure.

Parts: Shell_<Material> (walls, slabs, roofs: voxelised for destruction and collision at bake time; custom prop
shell = True), Detail (frames, trims, signs, junk), Glass (panes)."""
import math

import hdkit as K
from hdkit import Vector, Matrix


class VB:
    def __init__(self, asset, vs):
        self.a, self.vs = asset, vs

    # voxel index -> metres --------------------------------------------------------------------------
    def lo(self, i):
        return (i - 0.5) * self.vs

    def hi(self, i):
        return (i + 0.5) * self.vs

    def c(self, i):
        return i * self.vs

    def shell(self, byte):
        p = self.a.p("Shell_" + byte, byte)
        self.a.extra.setdefault("shell_parts", "")
        if ("Shell_" + byte) not in self.a.extra["shell_parts"]:
            self.a.extra["shell_parts"] += ("," if self.a.extra["shell_parts"] else "") + "Shell_" + byte
        return p

    def detail(self, byte="Scrap"):
        return self.a.p("Detail", byte)

    def glass(self):
        return self.a.p("Glass", "Glass")

    # walls ------------------------------------------------------------------------------------------
    def openings(self, w, l, side, y0, door, window_every, door_half=3, door_h=13):
        """Opening rectangles (u0, u1, v0, v1) in voxel indices (inclusive) on a side of Walls(): side 0 = z -l (front),
        1 = x +w, 2 = z +l, 3 = x -w. u runs along x for the z sides and along z for the x sides."""
        L = w if side in (0, 2) else l
        ops = []
        if door and side == 0:
            ops.append((-door_half, door_half, y0, y0 + door_h, "door"))
        if window_every > 0:
            limit = (l if side in (1, 3) else w) - 2
            cols = [a for a in range(-L, L + 1) if abs(a) % window_every <= 1 and abs(a) < limit]
            spans = []
            for a in cols:
                if spans and a == spans[-1][1] + 1:
                    spans[-1][1] = a
                else:
                    spans.append([a, a])
            for (u0, u1) in spans:
                if door and side == 0 and not (u1 < -door_half or u0 > door_half):
                    continue
                ops.append((u0, u1, y0 + 6, y0 + 11, "window"))
        return ops

    def walls(self, w, l, y0, y1, byte, mat, seed, door, window_every, frame_mat=None, cut=None, sill=None, glass_side_seed=None,
              thick=None, window_rule="hash"):
        """Walls(g, w, l, y0, y1, mat, paint, seed, door, windowEvery) with HD openings. `cut(side, u0, u1, v0, v1)` returns
        extra holes (ruins) in voxel indices for a side."""
        sh, de, gl = self.shell(byte), self.detail("Wood"), self.glass()
        t = self.vs if thick is None else thick
        for side in range(4):
            ops = self.openings(w, l, side, y0, door, window_every)
            holes = [(o[0], o[1], o[2], o[3]) for o in ops]
            if cut:
                holes += cut(side)
            if side in (0, 2):
                u0, u1 = -w + 1, w - 1           # the x = +-w sides own the corners
                zf = -l if side == 0 else l
            else:
                u0, u1 = -l, l
                xf = w if side == 1 else -w
            rects = K.rect_cells(self.lo(u0), self.hi(u1), self.lo(y0), self.hi(y1),
                                 [(self.lo(h[0]), self.hi(h[1]), self.lo(h[2]), self.hi(h[3])) for h in holes])
            for (a0, a1, b0, b1) in rects:
                if side in (0, 2):
                    sh.box2(mat, (a0, b0, self.lo(zf)), (a1, b1, self.hi(zf)), bevel=0.0)
                else:
                    sh.box2(mat, (self.lo(xf), b0, a0), (self.hi(xf), b1, a1), bevel=0.0)
            for o in ops:
                self.fit_opening(side, w, l, o, seed if glass_side_seed is None else glass_side_seed, frame_mat, sill, window_rule)

    def fit_opening(self, side, w, l, o, seed, frame_mat, sill, rule):
        """Frame (and pane / boards) in a wall opening."""
        de, gl = self.detail("Wood"), self.glass()
        u0, u1, v0, v1, kind = o
        vs = self.vs
        fm = frame_mat or K.wood("dark", "y")
        out = -1 if side in (0, 3) else 1
        if side in (0, 2):
            zf = -l if side == 0 else l

            def P(u, v, d):
                return (u, v, self.c(zf) + out * d)
        else:
            xf = w if side == 1 else -w

            def P(u, v, d):
                return (self.c(xf) + out * d, v, u)
        U0, U1, V0, V1 = self.lo(u0), self.hi(u1), self.lo(v0), self.hi(v1)
        ft = 0.05
        bars = [((U0, V0), (U0 + ft, V1)), ((U1 - ft, V0), (U1, V1)), ((U0, V1 - ft), (U1, V1))]
        if kind == "window":
            bars.append(((U0, V0), (U1, V0 + ft)))
        for (pa, pb) in bars:
            p0, p1 = P(pa[0], pa[1], -vs * 0.5), P(pb[0], pb[1], vs * 0.5 + 0.012)
            de.box2(fm, tuple(map(min, p0, p1)), tuple(map(max, p0, p1)), bevel=0.0)
        if kind == "door":
            return
        if sill:
            p0, p1 = P(U0 - 0.04, V0 - 0.05, -0.0), P(U1 + 0.04, V0, vs * 0.5 + 0.05)
            de.box2(sill, tuple(map(min, p0, p1)), tuple(map(max, p0, p1)), bevel=0.0)
        # glass state from the voxel rule: share of glass voxels in the opening
        n = tot = 0
        for u in range(u0, u1 + 1):
            for v in range(v0, v1 + 1):
                if side in (0, 2):
                    x, z = u, (-l if side == 0 else l)
                else:
                    x, z = (w if side == 1 else -w), u
                tot += 1
                n += K.pal_hash(x, v, z, seed) > 0.35
        share = n / max(1, tot)
        mid = (U0 + U1) / 2
        if share > 0.62:
            p0, p1 = P(U0 + ft * 0.5, V0 + ft * 0.5, -0.004), P(U1 - ft * 0.5, V1 - ft * 0.5, 0.004)
            gl.box2("glass", tuple(map(min, p0, p1)), tuple(map(max, p0, p1)), bevel=0)
            if U1 - U0 > 0.3:
                p0, p1 = P(mid - 0.015, V0, -0.01), P(mid + 0.015, V1, 0.02)
                de.box2(fm, tuple(map(min, p0, p1)), tuple(map(max, p0, p1)), bevel=0.0)
        elif share > 0.3:                                    # broken pane: a few shards left in the frame
            r = K.rng(int(seed * 7 + u0 * 13 + v0))
            for k in range(3):
                ua = U0 + ft + r.random() * (U1 - U0 - 2 * ft) * 0.6
                va = V0 + ft if k % 2 == 0 else V1 - ft
                ub = ua + (U1 - U0) * 0.35
                vb = va + (V1 - V0) * (0.45 if k % 2 == 0 else -0.45)
                gl.tri_plate("glass", P(ua, va, 0), P(ub, va, 0), P((ua + ub) / 2 + 0.02, vb, 0), 0.006)
        elif (u0 * 3 + v0) % 2 == 0:                         # boarded up
            r = K.rng(int(seed + u0 * 5 + v0))
            for k in range(2):
                vm = V0 + (V1 - V0) * (0.3 + 0.4 * k)
                ang = r.uniform(-12, 12)
                p0, p1 = P(U0 - 0.05, vm - 0.05, vs * 0.5 + 0.012), P(U1 + 0.05, vm + 0.05, vs * 0.5 + 0.035)
                lo_, hi_ = tuple(map(min, p0, p1)), tuple(map(max, p0, p1))
                cen = tuple((a + b) / 2 for a, b in zip(lo_, hi_))
                size = tuple(b - a for a, b in zip(lo_, hi_))
                de.box(K.wood("light", "x" if side in (0, 2) else "z"), size, cen, r=(0, 0, ang) if side in (0, 2) else (ang, 0, 0), bevel=0.0)

    # slabs, stairs, roofs ---------------------------------------------------------------------------
    def slab(self, w, l, y, byte, mat, holes=(), cut=None):
        sh = self.shell(byte)
        hz = [(self.lo(h[0]), self.hi(h[1]), self.lo(h[2]), self.hi(h[3])) for h in holes]
        for (a0, a1, b0, b1) in K.rect_cells(self.lo(-w), self.hi(w), self.lo(-l), self.hi(l), hz):
            sh.box2(mat, (a0, self.lo(y), b0), (a1, self.hi(y), b1), bevel=0.0)

    def stairs(self, cx, half_w, z0, floor_y, rise, byte, mat, tread=None):
        """Stairs(g, cx, halfW, z0, floorY, rise): solid stepped flight, one voxel rise per voxel run."""
        sh = self.shell(byte)
        pts = [(self.lo(z0), self.lo(floor_y + 1))]
        for i in range(rise):
            pts.append((self.lo(z0 + i), self.hi(floor_y + 1 + i)))
            pts.append((self.hi(z0 + i), self.hi(floor_y + 1 + i)))
        pts.append((self.hi(z0 + rise - 1), self.lo(floor_y + 1)))
        sh.prism(mat, pts, self.lo(cx - half_w), self.hi(cx + half_w), plane="zy")
        if tread:
            de = self.detail("Wood")
            for i in range(0, rise, 1):
                de.box2(tread, (self.lo(cx - half_w), self.hi(floor_y + 1 + i) - 0.005, self.lo(z0 + i)), (self.hi(cx + half_w), self.hi(floor_y + 1 + i) + 0.01, self.lo(z0 + i) + 0.04), bevel=0.003)

    def stair_hole(self, cx, half_w, z0, rise):
        return (cx - half_w - 1, cx + half_w + 1, z0 - 1, z0 + rise - 1)


def rubble(p, mats, rnd, centre, spread, n, size=0.2, floor=0.0):
    for i in range(n):
        an = rnd.random() * math.tau
        r = spread * math.sqrt(rnd.random())
        s = size * (0.4 + rnd.random())
        q = Vector(centre) + Vector((math.cos(an) * r, s * 0.2, math.sin(an) * r))
        p.rock(mats[i % len(mats)], s, tuple(q), seed=rnd.randint(0, 999), squash=0.55, detail=1, yaw=rnd.random() * 360, floor=floor)
