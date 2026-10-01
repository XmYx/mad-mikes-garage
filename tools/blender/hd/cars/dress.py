"""Shared dressing for every car body: lamps, grille, plates, mirrors, handles, wipers, gutters, aerial, interior, seats,
engine and the default mounted parts. All positions in game voxel units (they come straight from the voxel designs)."""
import math

from mathutils import Vector

import body as B
import hdlib as H
import parts as P
from hdlib import cyl, join, rbox, xform


def front_point(bvh, x, y, z1, back=False):
    """Surface point/normal on the nose (or tail) at (x, y)."""
    if back:
        hit, n = H.surface_hit(bvh, (x, y, -400), (0, 0, 1))
    else:
        hit, n = H.surface_hit(bvh, (x, y, 400), (0, 0, -1))
    if hit is None:
        return Vector((x, y, -z1 if back else z1)), Vector((0, 0, -1 if back else 1))
    return hit, n


def side_point(bvh, y, z, sgn=1):
    hit, n = H.surface_hit(bvh, (sgn * 200, y, z), (-sgn, 0, 0))
    if hit is None:
        return None, None
    return hit, n


def put(veh, name, bm, mats, **props):
    veh.part(name, props.pop("origin", (0, 0, 0)), **props).put(bm, mats=mats)


def lamps(veh, bvh, sp, front=(), rear=()):
    """front/rear: [(kind, x, y, w, h, mat)] with x > 0 (mirrored), or x == 0 (centre). kind: round | rect."""
    for lst, back in ((front, False), (rear, True)):
        for kind, x, y, w, h, mat in lst:
            for sgn in ((1, -1) if x > 0 else (1,)):
                p, n = front_point(bvh, sgn * x, y, sp.z1, back)
                n = (n + Vector((0, 0, -1.5 if back else 1.5))).normalized()
                if kind == "round":
                    bm = H.lamp_round(p - n * 0.05, w / 2, tuple(n), depth=0.6)
                    put(veh, "Lights", bm, ["chrome", mat, "reflector"])
                else:
                    if w > 6:          # full-width light bars sit flat on the panel
                        n = Vector((0, 0, -1 if back else 1))
                    bm = H.lamp_rect(p + n * 0.02, w, h, tuple(n), depth=0.5, bevel=min(w, h) * 0.18)
                    put(veh, "Lights", bm, ["chrome" if mat == "lamp_head" else "plastic", mat])


def grille(veh, bvh, sp, gw, y0, y1, style="bars", mat="chrome", frame="chrome"):
    yc, hh = (y0 + y1) / 2, (y1 - y0)
    p, n = front_point(bvh, 0, yc, sp.z1)
    z = p.z
    parts = [rbox((0, yc, z - 0.2), (2 * gw, hh, 0.8), 0.1, 1)]
    for f in parts[0].faces:
        f.material_index = 1
    fr = []
    for a, b in (((-gw, y0), (gw, y0)), ((-gw, y1), (gw, y1)), ((-gw, y0), (-gw, y1)), ((gw, y0), (gw, y1))):
        fr.append(cyl((a[0], a[1], z + 0.15), (b[0], b[1], z + 0.15), 0.28, 6))
    for b in fr:
        for f in b.faces:
            f.material_index = 0
    parts += fr
    if style == "bars":
        k = max(2, int(hh / 0.9))
        for i in range(1, k):
            y = y0 + hh * i / k
            b = rbox((0, y, z + 0.05), (2 * gw - 0.4, 0.22, 0.35), 0.05, 1)
            for f in b.faces:
                f.material_index = 2
            parts.append(b)
    elif style == "vertical":
        k = max(3, int(2 * gw / 0.9))
        for i in range(1, k):
            x = -gw + 2 * gw * i / k
            b = rbox((x, yc, z + 0.05), (0.22, hh - 0.3, 0.35), 0.05, 1)
            for f in b.faces:
                f.material_index = 2
            parts.append(b)
    else:  # mesh
        for i in range(1, int(hh / 0.6)):
            b = rbox((0, y0 + i * 0.6, z), (2 * gw - 0.3, 0.12, 0.2), 0.0)
            for f in b.faces:
                f.material_index = 2
            parts.append(b)
    put(veh, "Body", join(*parts), [frame, "grille_dark", mat])


def plate_at(veh, bvh, sp, y, back, part="Body", w=5.0, h=1.6, z=None):
    if z is None:
        p, n = front_point(bvh, 0, y, sp.z1, back)
    else:
        p, n = Vector((0, y, z)), Vector((0, 0, -1 if back else 1))
    n = Vector((0, 0, -1 if back else 1))
    put(veh, part, H.plate(p + n * 0.1, w, h, tuple(n)), ["plate", "plate_ink"])


def door_dressing(veh, bvh, sp, belt, mirror_z=None, handle_y=None, mirror_part=None):
    """Mirrors on the front doors (or `mirror_part`), handles on every door."""
    for name, za, zb, sock in sp.doors:
        for sgn, sfx in ((1, "_R"), (-1, "_L")):
            hy = handle_y if handle_y is not None else belt - 1.4
            p, n = side_point(bvh, hy, za + 1.6, sgn)
            if p is not None:
                hb = rbox(p + Vector((sgn * 0.15, 0, 0)), (0.35, 0.4, 1.6), 0.12, 1)
                put(veh, name + sfx, hb, ["chrome"])
            if name == "Door" and mirror_z is not False:
                mz = zb - 1.6 if mirror_z is None else mirror_z
                p, n = side_point(bvh, belt + 0.4, mz, sgn)
                if p is not None:
                    bm = H.mirror_bm(p - Vector((sgn * 0.2, 0, 0)), 1.0, 0.8, sgn)
                    put(veh, mirror_part or (name + sfx), bm, ["chrome" if mirror_part is None else "plastic", "glass"])


def wipers(veh, sp, y, z, cw):
    put(veh, "Body", join(H.wiper(Vector((-0.8, y, z)), Vector((-cw + 1.5, y + 0.9, z - 1.0))),
                          H.wiper(Vector((cw - 3.5, y, z)), Vector((1.0, y + 0.9, z - 1.0)))), ["plastic"])


def gutters(veh, sp, y, za, zb, x):
    put(veh, "Body", join(*[H.tube([(s * x, y, za), (s * x, y, zb)], 0.18, 6) for s in (-1, 1)]), ["chrome"])


def aerial(veh, x, y, z, h=6.0):
    put(veh, "Body", join(cyl((x, y, z), (x, y + h, z - 0.5), 0.06, 4), cyl((x, y - 0.2, z), (x, y + 0.2, z), 0.25, 8)), ["chrome"])


def interior(veh, f, dashZ, seatZ, dx, cw=8, rear_bench=False, bucket=False, rear_off=10, dash_w=8, back_h=7.6):
    """Interior(): dashboard, wheel, two seats, optional rear bench (voxel layout from VehicleDesigns.Interior)."""
    dash = join(rbox((0, f + 6.6, dashZ + 1.5), (2 * dash_w + 1, 3.2, 4.0), 0.6, 2),
                rbox((dx, f + 8.4, dashZ + 0.3), (3.6, 1.0, 1.6), 0.35, 2))
    for fc in dash.faces:
        fc.material_index = 0
    gauges = []
    for i, gx in enumerate((dx - 0.8, dx + 0.8)):
        g = H.revolve([(0, 0), (0, 0.55), (0.05, 0.6), (0.05, 0)], 12, origin=(gx, f + 8.2, dashZ - 0.55), axis=(0, 0.3, -1))
        for fc in g.faces:
            fc.material_index = 2
        gauges.append(g)
    st = H.steering(dx, f, dashZ)
    for fc in st.faces:
        fc.material_index = 1
    lever = cyl((0, f + 1.5, dashZ - 2.5), (0, f + 4.5, dashZ - 3.2), 0.12, 6)
    knob = H.sphere((0, f + 4.6, dashZ - 3.25), 0.35, 8, 6)
    for b in (lever, knob):
        for fc in b.faces:
            fc.material_index = 1
    put(veh, "Interior", join(dash, st, lever, knob, *gauges), ["dash", "plastic", "chrome"])
    put(veh, "Seat_Driver", H.seat_bm(dx - 2, dx + 2, f, seatZ, bucket, back_h=back_h), ["leather"], origin=(dx, f + 1, seatZ - 2))
    put(veh, "Seat_Passenger", H.seat_bm(-dx - 2, -dx + 2, f, seatZ, bucket, back_h=back_h), ["leather"], origin=(-dx, f + 1, seatZ - 2))
    if rear_bench:
        rz = seatZ - rear_off
        put(veh, "Interior", H.seat_bm(-(cw - 2), cw - 2, f, rz, rear=True, back_h=min(6.0, back_h)), ["leather"])


def mount(veh, name, socket, key, pos, bm, mats, mirrored=False, paint="paint"):
    """Place a part at its socket; mirrored sockets get a mirrored copy on the left (like localScale.x = -1)."""
    mats = [paint if m == "paint" else m for m in mats]
    pos = Vector(pos)
    if mirrored:
        me = H._scratch()
        bm.to_mesh(me)
        import bmesh
        bl = bmesh.new()
        bl.from_mesh(me)
        H.mirror_x(bl)
        xform(bl, (-pos.x, pos.y, pos.z))
        veh.part(name + "_L", (-pos.x, pos.y, pos.z), socket=socket + "_L", part=key, mirrored=True).put(bl, mats=mats)
        name = name + "_R"
    xform(bm, pos)
    veh.part(name, pos, socket=socket, part=key, mirrored=False).put(bm, mats=mats)


def engine_at(veh, key, pos):
    bm, mats = P.engine(key)
    mount(veh, "Engine", "engine", key, pos, bm, mats)


def tail_glass(veh, x, y0, y1, z, back=True, part="Glass"):
    """Flat window on an end face (pickup cab back, wagon tailgate, box rear)."""
    s = -1 if back else 1
    g = rbox((0, (y0 + y1) / 2, z + s * 0.12), (2 * x, y1 - y0, 0.2), 0.35, 2)
    seal = rbox((0, (y0 + y1) / 2, z + s * 0.02), (2 * x + 0.5, y1 - y0 + 0.5, 0.18), 0.4, 2)
    for f in seal.faces:
        f.material_index = 1
    put(veh, part, join(g, seal), ["glass", "seal"])


def flare(veh, z, y, r0, r1, x, mat="plastic", part="Body", thick=1.0, a0=0, a1=180, ymax=None):
    """Wheel-arch flare: a wide half ring around the arch on both sides (bulges out from the body side at x).
    `ymax` flattens the crown so the flare follows the fender line instead of hooping over it."""
    for s in (-1, 1):
        pts = []
        for i in range(a0, a1 + 1, 6):
            a = math.radians(i)
            yy = y + math.sin(a) * (r0 + r1) / 2
            if ymax is not None:
                yy = min(yy, ymax)
            pts.append((s * (x + thick * 0.35), yy, z + math.cos(a) * (r0 + r1) / 2))
        bm = H.sweep(pts, H.rrect(thick, r1 - r0, min(thick, r1 - r0) * 0.45, 3), up=(1, 0, 0))
        put(veh, part, bm, [mat])
