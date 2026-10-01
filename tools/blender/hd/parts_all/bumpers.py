"""HD bumpers, rams, rear bumpers and side armour. Origin = the bumper / armour socket; front parts reach forward
(game +Z = kit -Y), rear parts backward; side armour is authored for the right side (game +X out).

blender -b -P tools/blender/hd/parts_all/bumpers.py [-- ids]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pa  # noqa: E402
from pa import M, Part, Vector, Matrix, V, kit, bm_box, bm_cyl  # noqa: E402
import comps  # noqa: E402
import parts as mp  # noqa: E402  (misc/parts.py builders)

mp.M = M
F = "bumpers"


def chrome_bar(key, rear):
    """Chrome bumper: rolled blade, wrap-around ends, rubber overriders, mounting irons back to the chassis."""
    p = Part(key)
    s = 1 if rear else -1                                   # kit y of "outward"
    w = 2.0
    prof = [(-0.07, -0.0), (-0.05, 0.03), (0.0, 0.045), (0.05, 0.03), (0.075, 0.0)]
    secs = []
    n = 26
    for i in range(n + 1):
        t = i / n
        x = -w / 2 + t * w
        wrap = max(0.0, abs(x) - 0.84) / 0.16
        y0 = -s * 0.0 + s * wrap * wrap * 0.1 * -1 * -1
        sec = [(x, s * (0.02 + dy) + y0 * 0, 0.08 + dz) for dz, dy in prof]
        secs.append(sec)
    blade = kit.bm_loft([[(x, y + (s * -1) * 0.0, z) for (x, y, z) in sec] for sec in secs], close_ends=True)
    p.add(blade, M["chrome"])
    for sx in (-1, 1):                                        # wrap-arounds back to the body
        p.tube(M["chrome"], [(sx * 0.96, s * 0.03, 0.08), (sx * 1.0, s * -0.02, 0.08), (sx * 1.0, s * -0.1, 0.08)], 0.06, 14)
        p.box(M["rubber"], (0.08, 0.05, 0.13), (sx * 0.44, s * 0.07, 0.08), bevel=0.02)          # overriders
        p.box(M["steel_dark"], (0.06, 0.12, 0.05), (sx * 0.6, s * -0.06, 0.08), bevel=0.008)     # irons
        pa.bolts(p, M["chrome"], [(sx * 0.6, s * 0.065, 0.08)], 0.012, 0.008, "Y")
    if not rear:
        p.box(M["black"], (0.42, 0.012, 0.06), (0, s * 0.055, 0.06), bevel=0.003)                  # plate mount
    else:
        p.box(M["cream"], (0.36, 0.012, 0.12), (0, s * 0.06, 0.1), bevel=0.004)                    # licence plate
        p.box(M["black"], (0.3, 0.014, 0.02), (0, s * 0.065, 0.1), bevel=0)
    return p.build()


def trabant(key, front):
    """Trabant bumper: slim chrome-capped steel bar with short returns at the ends."""
    p = Part(key)
    s = -1 if front else 1
    w = 1.36
    p.box(M["cream"], (w, 0.07, 0.12), (0, s * 0.0, 0.06), bevel=0.02)
    p.box(M["chrome"], (w * 0.98, 0.075, 0.03), (0, s * 0.002, 0.12), bevel=0.01)
    for sx in (-1, 1):
        p.box(M["cream"], (0.07, 0.14, 0.12), (sx * (w / 2 - 0.035), -s * 0.06, 0.06), bevel=0.02)
        p.box(M["rubber"], (0.06, 0.04, 0.08), (sx * 0.3, s * 0.045, 0.06), bevel=0.012)
    return p.build()


def plow():
    """bumper_plow: wedge cow-catcher, ribbed steel faces meeting at a central nose, three mounts."""
    p = Part("bumper_plow")
    W, H, D = 2.48, 1.08, 0.6
    rows = 8
    for side in (-1, 1):
        a = Vector((0, -D, 0))
        b = Vector((side * W / 2, -0.1, 0))
        n = (b - a).normalized()
        for k in range(rows):
            z0 = k * H / rows
            for ex in (0.0,):
                pass
        poly = [(0, -D), (side * W / 2, -0.12), (side * W / 2, 0.0), (0, -D + 0.08)]
        p.add(kit.bm_loft([[(x, y, z) for (x, y) in poly] for z in (0.0, H)]), M["ram_steel"])
        for k in range(1, rows):                               # horizontal bars across the face
            z = k * H / rows
            pa.rod(p, M["rust"], (0, -D - 0.02, z), (side * W / 2, -0.14, z), 0.016, 6)
        for t in (0.3, 0.65):                                  # vertical ribs
            q = a.lerp(b, t)
            p.box(M["bar_steel"], (0.04, 0.05, H), (q.x, q.y - 0.02, H / 2), (0, 0, side * math.atan2(D, W / 2)), bevel=0.006)
    p.box(M["bar_steel"], (0.08, 0.1, H + 0.02), (0, -D - 0.01, H / 2), bevel=0.012)        # nose post
    p.box(M["rust"], (W, 0.1, 0.06), (0, -0.05, H - 0.03), bevel=0.01)
    for x in (-0.72, 0.0, 0.72):
        p.box(M["steel_dark"], (0.1, 0.18, 0.12), (x, 0.06, 0.52), bevel=0.012)
        pa.bolts(p, M["chrome"], [(x - 0.03, 0.15, 0.52), (x + 0.03, 0.15, 0.52)], 0.01, 0.01, "Y")
    p.box(M["hazard_bar"], (W * 0.9, 0.02, 0.08), (0, -0.14, 0.06), bevel=0.003)
    return p.build()


def rear_spiked():
    ob = mp.spiked_bumper()
    ob.data.transform(Matrix.Rotation(math.pi, 4, "Z"))      # spikes point backwards
    ob.name = "rear_spiked"
    return ob


def spare_carrier():
    """rear_spare_carrier: rear bar with a swing-out post carrying the spare (tyre faces backward)."""
    p = Part("rear_spare_carrier")
    p.box(M["bar_steel"], (2.0, 0.12, 0.22), (0, 0.04, 0.12), bevel=0.02)
    for x in (-0.9, 0.9):
        p.box(M["black"], (0.08, 0.14, 0.08), (x, 0.06, -0.02), bevel=0.01)
    p.box(M["ram_steel"], (0.1, 0.08, 0.84), (0, 0.1, 0.62), bevel=0.012)                  # post
    p.cyl(M["steel"], 0.04, 0.24, (0, 0.1, 0.25), "Z", 14, bevel=0.006)                     # hinge
    p.box(M["bar_steel"], (0.5, 0.06, 0.06), (0, 0.16, 0.72), bevel=0.01)
    ob = p.build()
    wh = kit.car_wheel("spare", M, 0.44, 0.27, 0.24, "offroad", "steel", rim_mat=M["steel"], segs=36)
    wh.data.transform(Matrix.Translation((0, 0.3, 0.72)) @ Matrix.Rotation(-math.pi / 2, 4, "Z"))
    wh.parent = ob
    return ob


def smoke():
    """armor_smoke: side plate with three smoke-discharger tubes angled up and out, cable loom, warning stencil."""
    p = Part("armor_smoke")
    pa.plate(p, M["ram_steel"], (0.08, 1.36, 0.48), (0.04, 0, 0.12), rivets=4, rivet_mat=M["chrome"])
    for z in (-0.32, 0.0, 0.32):
        base = Vector((0.12, z, 0.16))
        tip = Vector((0.4, z, 0.44))
        p.box(M["steel_dark"], (0.12, 0.14, 0.1), (0.1, z, 0.12), bevel=0.012)
        kit.oriented(p, kit.bm_cyl(0.06, (tip - base).length, 18, 0.006), M["olive"], base, tip)
        d = (tip - base).normalized()
        kit.oriented(p, kit.bm_cyl(0.068, 0.04, 18, 0.006), M["steel_dark"], tip - d * 0.03, tip + d * 0.01)
        kit.oriented(p, kit.bm_cyl(0.052, 0.012, 18, 0.0), M["black"], tip + d * 0.008, tip + d * 0.012)
        p.tube(M["black"], [(0.09, z, 0.06), (0.09, z + 0.12, -0.02), (0.09, 0.5, -0.04)], 0.008, 6)
    p.box(M["hazard"], (0.01, 0.3, 0.06), (0.085, 0.4, 0.3), bevel=0.002)
    return p.build()


def sloped():
    """armor_sloped: thick steel plate leaning in toward the top, chamfered ends, weld seams and bolts."""
    p = Part("armor_sloped")
    L, H, t = 2.64, 0.96, 0.08
    lean = 0.28
    poly = [(-L / 2, 0.0), (L / 2, 0.0), (L / 2 - 0.16, H), (-L / 2 + 0.16, H)]
    sec_out = [(lean - 0.0 + 0.0, y, z) for (y, z) in poly]
    secs = []
    for off in (0.0, t):
        secs.append([(0.4 - z / H * lean - off, y, z) for (y, z) in poly])
    p.add(kit.bm_loft(secs), M["ram_steel"])
    for zf in (0.25, 0.7):                                      # weld seams
        z = zf * H
        x = 0.4 - zf * lean + 0.005
        pa.rod(p, M["rust"], (x, -L / 2 + 0.1, z), (x, L / 2 - 0.1, z), 0.008, 6)
    for k in range(7):
        y = -L / 2 + 0.25 + k * (L - 0.5) / 6
        for zf in (0.12, 0.88):
            z = zf * H
            p.cyl(M["chrome"], 0.016, 0.02, (0.4 - zf * lean + 0.008, y, z), "X", 6, bevel=0.003)
    for y in (-0.9, 0.0, 0.9):                                  # brackets back to the body
        p.box(M["steel_dark"], (0.36, 0.06, 0.08), (0.18, y, 0.2), bevel=0.01)
        p.box(M["steel_dark"], (0.16, 0.06, 0.08), (0.12, y, 0.72), bevel=0.01)
    p.box(M["black"], (0.02, 0.36, 0.05), (0.4 - 0.62 * lean + 0.01, -0.4, 0.62 * H), bevel=0.004)   # vision slot
    return p.build()


def skirt():
    """armor_skirt_spiked: riveted skirt along the sill with a serrated lower edge, spikes angled out and down."""
    p = Part("armor_skirt_spiked")
    L, H = 2.0, 0.34
    p.box(M["ram_steel"], (0.05, L, H), (0.06, 0, -0.08), bevel=0.008)
    for k in range(20):                                         # serrated lower edge
        y = -L / 2 + 0.05 + k * L / 20
        p.add(kit.bm_cyl(0.035, 0.08, 3, 0.0, 0.0), M["ram_steel"], (0.06, y, -0.29), (math.pi, 0, math.pi / 6))
    pa.plate(p, M["bar_steel"], (0.06, L, 0.06), (0.07, 0, 0.08), rivets=12, rivet_mat=M["chrome"])
    for k in range(8):
        y = -L / 2 + 0.14 + k * (L - 0.28) / 7
        base = Vector((0.09, y, -0.06))
        tip = base + Vector((0.38, 0, -0.16))
        kit.oriented(p, kit.bm_cyl(0.035, (tip - base).length, 6, 0.0, 0.002), M["chrome"], base, tip)
        p.cyl(M["steel_dark"], 0.045, 0.03, base + Vector((0.01, 0, 0)), "X", 6, bevel=0.004)
    for y in (-0.7, 0.7):
        p.box(M["steel_dark"], (0.08, 0.06, 0.2), (0.02, y, 0.1), bevel=0.01)
    return p.build()


def window_mesh():
    """armor_window_mesh: welded diamond mesh over the side windows on three clamp posts."""
    p = Part("armor_window_mesh")
    L, z0, z1 = 2.16, 0.32, 0.98
    frame = M["bar_steel"]
    x = 0.07
    for a, b in (((x, -L / 2, z0), (x, L / 2, z0)), ((x, -L / 2, z1), (x, L / 2, z1)), ((x, -L / 2, z0), (x, -L / 2, z1)), ((x, L / 2, z0), (x, L / 2, z1))):
        pa.rod(p, frame, a, b, 0.014, 8)
    step = 0.09
    h = z1 - z0
    n = int((L + h) / step)
    for k in range(n):                                          # two diagonal wire sets clipped to the frame
        for sgn in (1, -1):
            pts = []
            for t in (0.0, 1.0):
                y = -L / 2 + k * step - (h if sgn > 0 else 0) + t * h * (1 if sgn > 0 else 1)
                pts.append(Vector((x, y if sgn > 0 else -y, z0 + t * h)))
            a, b = pts
            # clip to |y| <= L/2
            def clip(a, b):
                lo, hi = -L / 2, L / 2
                ta, tb = 0.0, 1.0
                d = b - a
                for (bound, s) in ((lo, -1), (hi, 1)):
                    if abs(d.y) < 1e-9:
                        continue
                    t = (bound - a.y) / d.y
                    if s < 0:
                        if d.y > 0:
                            ta = max(ta, t)
                        else:
                            tb = min(tb, t)
                    else:
                        if d.y > 0:
                            tb = min(tb, t)
                        else:
                            ta = max(ta, t)
                return (a + d * ta, a + d * tb) if tb > ta + 0.02 else None
            c = clip(a, b)
            if c:
                pa.rod(p, M["steel"], c[0], c[1], 0.004, 4)
    for y in (-0.9, 0.0, 0.9):                                  # clamp posts
        p.box(M["steel_dark"], (0.1, 0.04, 0.04), (0.03, y, z0 + 0.06), bevel=0.006)
        p.box(M["steel_dark"], (0.1, 0.04, 0.04), (0.03, y, z1 - 0.06), bevel=0.006)
        p.box(M["black"], (0.02, 0.08, 0.1), (-0.01, y, z0 + 0.06), bevel=0.004)
    pa.rod(p, M["steel_dark"], (0.02, 0, 0.0), (0.02, 0, z0), 0.012, 8)
    return p.build()


SIDE = dict(azim=-45)
pa.add("bumper_bull_bar", mp.bull_bar, F, category="FrontBumper", socket="bumper_front", tile="bumpers_front")
pa.add("bumper_ram", mp.ram, F, category="FrontBumper", socket="bumper_front", tile="bumpers_front")
pa.add("bumper_spiked", mp.spiked_bumper, F, category="FrontBumper", socket="bumper_front", tile="bumpers_front")
pa.add("bumper_winch", mp.winch_bumper, F, category="FrontBumper", socket="bumper_front", tile="bumpers_front")
pa.add("bumper_plow", plow, F, category="FrontBumper", socket="bumper_front", tile="bumpers_front")
pa.add("bumper_chrome", lambda: chrome_bar("bumper_chrome", False), F, category="FrontBumper", socket="bumper_front", tile="bumpers_chrome")
pa.add("bumper_rear_chrome", lambda: chrome_bar("bumper_rear_chrome", True), F, category="RearBumper", socket="bumper_rear", tile="bumpers_chrome")
pa.add("bumper_trabant_front", lambda: trabant("bumper_trabant_front", True), F, category="FrontBumper", socket="bumper_front", tile="bumpers_chrome")
pa.add("bumper_trabant_rear", lambda: trabant("bumper_trabant_rear", False), F, category="RearBumper", socket="bumper_rear", tile="bumpers_chrome")
pa.add("rear_spiked", rear_spiked, F, category="RearBumper", socket="bumper_rear", tile="bumpers_rear")
pa.add("rear_spare_carrier", spare_carrier, F, category="RearBumper", socket="bumper_rear", tile="bumpers_rear")
pa.add("rear_dropper", mp.rear_dropper, F, category="RearBumper", socket="bumper_rear", tile="bumpers_rear")
pa.add("armor_plate", mp.side_plate, F, category="Armor", socket="armor (right; armor_L mirrors)", tile="armour_side", **SIDE)
pa.add("armor_spikes", mp.side_spikes, F, category="Armor", socket="armor (right; armor_L mirrors)", tile="armour_side", **SIDE)
pa.add("armor_smoke", smoke, F, category="Armor", socket="armor (right; armor_L mirrors)", tile="armour_side", **SIDE)
pa.add("armor_sloped", sloped, F, category="Armor", socket="armor (right; armor_L mirrors)", tile="armour_side2", **SIDE)
pa.add("armor_skirt_spiked", skirt, F, category="Armor", socket="armor (right; armor_L mirrors)", tile="armour_side2", **SIDE)
pa.add("armor_window_mesh", window_mesh, F, category="Armor", socket="armor (right; armor_L mirrors)", tile="armour_side2", **SIDE)

if __name__ == "__main__":
    pa.run(F)
