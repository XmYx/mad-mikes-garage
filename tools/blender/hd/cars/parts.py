"""Mounted parts (PartLibrary keys), authored like the game: origin = socket / mount point, right-side parts face +X
(left sockets mirror them), voxel units. Each function returns (bmesh, [material names by tmp index])."""
import math

from mathutils import Vector

import hdlib as H
from hdlib import cyl, join, rbox, revolve, sphere, sweep, tube, xform


def _mi(bm, i):
    for f in bm.faces:
        f.material_index = i
    return bm


def bumper_chrome(hw, rear=False, depth=1.0, h=1.7, wrap=1.6):
    """Chrome blade bumper wrapping the corners, two overriders, brackets. mats [chrome, plastic, steel]."""
    s = -1 if rear else 1
    pts = []
    n = 14
    for i in range(n + 1):
        x = -hw + 0.3 + (2 * hw - 0.6) * i / n
        u = abs(x) / hw
        pts.append((x, 0, s * (-wrap * u ** 4)))
    bar = sweep(pts, H.rrect(depth, h, 0.45, 2), up=(0, 0, 1))
    parts = [_mi(bar, 0)]
    parts.append(_mi(sweep([(p[0], -0.15, p[2] + s * 0.52) for p in pts[2:-2]], H.rrect(0.12, 0.35, 0.05, 1), up=(0, 0, 1)), 1))
    for x in (-hw * 0.45, hw * 0.45):
        parts.append(_mi(rbox((x, 0.4, s * 0.45), (0.8, 2.8, 1.0), 0.35, 2), 0))
        parts.append(_mi(rbox((x, 0, -s * 1.2), (0.6, 0.6, 2.0), 0.1, 1), 2))
    return join(*parts), ["chrome", "plastic", "steel"]


def bumper_plastic(hw, rear=False, h=2.4, depth=1.4, wrap=2.4, mat="plastic"):
    """Moulded wrap-around bumper band (real cars: hc/hh from cars.py)."""
    s = -1 if rear else 1
    pts = []
    n = 16
    for i in range(n + 1):
        x = -hw + 0.2 + (2 * hw - 0.4) * i / n
        u = abs(x) / hw
        pts.append((x, 0, s * (-wrap * u ** 3)))
    bar = sweep(pts, H.rrect(depth, h, 0.5, 2), up=(0, 0, 1))
    rub = sweep([(p[0], 0.1, p[2] + s * depth * 0.5) for p in pts[1:-1]], H.rrect(0.16, 0.45, 0.06, 1), up=(0, 0, 1))
    return join(_mi(bar, 0), _mi(rub, 1)), [mat, "plastic_grey"]


def bull_bar(hw):
    """Steel tube bull bar: base bar, two uprights, top hoop, lamp tabs. mats [cage, steel]."""
    w = hw - 2.0
    r = 0.42
    parts = [tube([(-w, 0, 0.6), (-w * 0.6, 0, 1.4), (w * 0.6, 0, 1.4), (w, 0, 0.6)], r, 8)]
    hoop = [(-w * 0.55, 0, 1.4), (-w * 0.55, 6.5, 1.0), (-w * 0.35, 8.0, 0.6), (w * 0.35, 8.0, 0.6), (w * 0.55, 6.5, 1.0), (w * 0.55, 0, 1.4)]
    parts.append(tube(hoop, r, 8))
    for x in (-w * 0.2, w * 0.2):
        parts.append(tube([(x, 0, 1.5), (x, 7.8, 0.7)], r * 0.8, 8))
    parts.append(tube([(-w * 0.55, 4.2, 1.25), (w * 0.55, 4.2, 1.25)], r * 0.8, 8))
    for x in (-w * 0.8, w * 0.8):
        parts.append(_mi(rbox((x, 0, -0.6), (0.7, 1.4, 2.2), 0.1, 1), 1))
    return join(*[_mi(p, 0) for p in parts[:-2]] + parts[-2:]), ["cage_black", "steel"]


def bumper_winch(hw):
    """Heavy plate bumper with a winch drum, fairlead, hook, tow shackles. mats [steel, chrome, hazard, rubber]."""
    parts = [_mi(rbox((0, 0, 0.3), (2 * hw - 1.0, 3.0, 2.4), 0.35, 2), 0)]
    parts.append(_mi(rbox((0, 0.4, 1.6), (9.0, 2.6, 1.6), 0.25, 2), 0))
    parts.append(_mi(cyl((-3.2, 0.5, 1.7), (3.2, 0.5, 1.7), 1.0, 16), 3))          # drum with cable
    for x in (-3.6, 3.6):
        parts.append(_mi(rbox((x, 0.5, 1.7), (0.4, 2.8, 2.2), 0.15, 1), 0))
    parts.append(_mi(rbox((0, 0.0, 2.6), (3.0, 1.0, 0.5), 0.1, 1), 1))              # roller fairlead
    parts.append(_mi(tube([(0, 0.0, 2.8), (0, -0.6, 3.2), (0, -1.4, 3.1)], 0.22, 6), 1))
    parts.append(_mi(tube([(0, -1.4, 3.1), (0.5, -2.0, 3.0), (0.2, -2.5, 2.8), (-0.3, -2.1, 2.9)], 0.18, 6), 2))  # hook
    for x in (-hw + 2.2, hw - 2.2):
        parts.append(_mi(H.revolve([(-0.3, 0.6), (0.3, 0.6), (0.3, 0.3), (-0.3, 0.3), (-0.3, 0.6)], 12,
                                   origin=(x, -0.8, 1.5), axis=(1, 0, 0)), 2))
    for x in (-hw + 0.8, hw - 0.8):
        parts.append(_mi(rbox((x, 0.0, 1.3), (1.2, 2.6, 0.3), 0.08, 1), 2))       # hazard stripes on the ends
    return join(*parts), ["steel", "chrome", "hazard", "rubber"]


def spare_carrier(hw, wheel_key):
    """Rear step bumper + swing-out carrier with a spare tyre. mats from the wheel + [steel]."""
    R = H.WHEELS[wheel_key][0]
    parts = [_mi(rbox((0, 0, -0.4), (2 * hw - 1.4, 2.2, 1.8), 0.3, 2), 6)]
    px = hw - 2.0
    cx, cy = px - R - 0.6, R * 0.75 + 1.0
    parts.append(_mi(cyl((px, -0.8, -1.2), (px, cy + R * 0.6, -1.2), 0.45, 10), 6))                 # swing post
    parts.append(_mi(tube([(px, cy + 0.5, -1.2), (cx, cy + 0.5, -1.5)], 0.3, 8), 6))
    parts.append(_mi(tube([(px, cy - R * 0.4, -1.2), (cx, cy - 0.5, -1.5)], 0.25, 8), 6))
    w = H.wheel_bm(wheel_key, 64)
    xform(w, (cx, cy, -1.7 - H.WHEELS[wheel_key][1] / 2.0), (0, math.radians(90), 0))
    for x in (-hw + 1.0, hw - 1.0):
        parts.append(_mi(rbox((x, 0.6, -0.4), (0.8, 1.0, 2.2), 0.2, 1), 5))
    return join(w, *parts), H.wheel_mats(wheel_key) + ["steel"]


def spoiler(hw):
    """Rear wing on two struts with end plates. mats [paint, plastic]."""
    w = hw - 1.0
    foil = [(0, 0.0), (0.6, 0.35), (1.6, 0.45), (2.6, 0.3), (3.2, 0.0), (1.6, -0.15)]
    prof = [(y, -x) for x, y in foil]
    wing = sweep([(-w, 2.2, -0.3), (w, 2.2, -0.3)], prof, up=(0, 1, 0))
    parts = [_mi(wing, 0)]
    for x in (-w * 0.6, w * 0.6):
        parts.append(_mi(rbox((x, 1.0, -1.6), (0.35, 2.4, 1.4), 0.1, 1), 1))
    for x in (-w, w):
        parts.append(_mi(rbox((x, 2.4, -1.6), (0.25, 1.8, 3.8), 0.1, 1), 0))
    return join(*parts), ["paint", "plastic"]


def side_pipes():
    """Right-side exhaust: four headers out of the fender into a collector, twin pipes along the sill, chrome tips.
    Socket (13, 3, 9). mats [chrome, steel, rusty]."""
    parts = []
    for i in range(4):
        z = 1.6 - i * 1.1
        parts.append(_mi(tube([(-1.8, 2.2, z), (-0.6, 2.0, z), (0.3, 1.0, z * 0.6), (0.4, 0.2, -2.0)], 0.32, 8), 2))
    parts.append(_mi(H.revolve([(0, 0.0), (0, 0.65), (2.5, 0.75), (3.0, 0.45), (3.0, 0.0)], 14, origin=(0.4, 0.2, -5.0), axis=(0, 0, 1)), 1))
    for dy in (0.35, -0.35):
        parts.append(_mi(tube([(0.4, dy, -5.0), (0.4, dy, -16.0)], 0.38, 10, caps=False), 0))
        parts.append(_mi(H.revolve([(0, 0.38), (-1.2, 0.5), (-1.25, 0.42), (-0.6, 0.3)], 14, origin=(0.4, dy, -16.0), axis=(0, 0, 1)), 0))
    parts.append(_mi(rbox((-0.4, 0, -9.0), (1.2, 0.3, 0.5), 0.05, 1), 1))
    return join(*parts), ["chrome", "steel", "rusty"]


def exhaust_tip(length=3.0):
    p = join(_mi(tube([(0, 0, 0), (0, 0, -length)], 0.35, 10, caps=False), 0),
             _mi(H.revolve([(0, 0.35), (-0.8, 0.45), (-0.85, 0.38)], 12, origin=(0, 0, -length), axis=(0, 0, 1)), 0))
    return p, ["rusty"]


def engine(kind):
    """Engine block for the bay (mostly hidden; shows in the exploded view). Socket at the block base."""
    big = kind in ("engine_i6", "engine_diesel_i6", "engine_v8_blower")
    L = 7.5 if big else 5.5
    W = 5.0 if kind == "engine_v8_blower" else 3.4
    if kind == "engine_2stroke":
        L, W = 3.6, 3.0
    parts = [_mi(rbox((0, 2.2, 0), (W, 4.4, L), 0.4, 2), 0)]
    parts.append(_mi(rbox((0, 1.2, 0), (W * 0.9, 1.0, L * 0.95), 0.2, 1), 0))                       # sump
    if kind == "engine_v8_blower":
        for s in (-1, 1):
            parts.append(_mi(rbox((s * 1.6, 4.9, 0), (1.6, 1.2, L * 0.9), 0.3, 2), 1))                 # rocker covers
        parts.append(_mi(rbox((0, 6.6, 0.2), (3.0, 2.4, 5.6), 0.5, 2), 1))                              # blower case
        for i in range(7):
            parts.append(_mi(rbox((0, 7.85, -2.2 + i * 0.75), (3.1, 0.2, 0.22), 0.05, 1), 2))            # ribs
        for z in (-1.0, 1.3):
            parts.append(_mi(rbox((0, 8.6, z), (2.0, 1.6, 1.6), 0.3, 2), 2))                             # carbs
        parts.append(_mi(rbox((0, 10.0, 0.4), (3.4, 1.4, 4.2), 0.5, 2, rot=(math.radians(-6), 0, 0)), 3))   # scoop
        parts.append(_mi(rbox((0, 10.0, 2.55), (2.8, 1.0, 0.2), 0.05, 1), 4))                          # scoop mouth
        parts.append(_mi(H.revolve([(-0.4, 0), (-0.4, 1.3), (0.4, 1.3), (0.4, 0)], 20, origin=(0, 5.4, 3.8), axis=(0, 0, 1)), 2))  # pulley
    else:
        parts.append(_mi(rbox((0, 4.9, 0), (W * 0.85, 1.0, L * 0.92), 0.35, 2), 1))                    # valve cover
        parts.append(_mi(H.revolve([(0, 0), (0, 1.6), (0.9, 1.6), (0.9, 0)], 18, origin=(0, 5.4, 0.3), axis=(0, 1, 0)), 2))  # air cleaner
        parts.append(_mi(tube([(W / 2, 3.0, L * 0.35), (W / 2 + 0.6, 2.6, 0), (W / 2 + 0.6, 1.5, -L * 0.4)], 0.35, 8), 3))
    return join(*parts), ["engine_block", "engine_paint", "chrome", "paint", "void"]


def twin_tanks():
    """Cargo: two fuel tanks across the boot (Interceptor). Socket (0, 11, -24) on the deck. mats [steel, chrome, strap]."""
    parts = []
    for z in (-2.2, 2.2):
        parts.append(_mi(H.revolve([(-7.0, 0), (-7.0, 1.5), (-6.6, 1.9), (6.6, 1.9), (7.0, 1.5), (7.0, 0)], 20,
                                   origin=(0, 1.9, z), axis=(1, 0, 0)), 0))
        for x in (-4.5, 4.5):
            parts.append(_mi(H.revolve([(-0.25, 1.95), (0.25, 1.95), (0.25, 2.05), (-0.25, 2.05), (-0.25, 1.95)], 20,
                                       origin=(x, 1.9, z), axis=(1, 0, 0)), 2))
        parts.append(_mi(cyl((5.5, 3.7, z), (5.5, 4.3, z), 0.45, 10), 1))
    parts.append(_mi(tube([(5.5, 4.3, -2.2), (5.5, 4.8, 0), (5.5, 4.3, 2.2)], 0.2, 6), 1))
    for x in (-4.5, 4.5):
        parts.append(_mi(rbox((x, 0.3, 0), (0.6, 0.6, 8.6), 0.1, 1), 0))
    return join(*parts), ["steel", "chrome", "plastic"]


def jerry_rack(w=7.0, l=6.5, n=5, h=1.4):
    """Roof/bed rack: tube frame on four feet with jerry cans lying across. mats [cage, can colours..., steel]."""
    parts = []
    r = 0.3
    ring = [(-w, h, -l), (w, h, -l), (w, h, l), (-w, h, l), (-w, h, -l)]
    parts.append(_mi(tube(ring, r, 6), 0))
    for z in (-l * 0.33, l * 0.33):
        parts.append(_mi(tube([(-w, h, z), (w, h, z)], r * 0.8, 6), 0))
    for x in (-w, w):
        for z in (-l, l):
            parts.append(_mi(cyl((x, 0, z), (x, h, z), r, 6), 0))
        parts.append(_mi(tube([(x, h, -l), (x, h + 1.4, -l + 0.6), (x, h + 1.4, l - 0.6), (x, h, l)], r * 0.8, 6), 0))
    cols = [1, 2, 3, 1, 2, 3]
    for i in range(n):
        z = -l + 1.4 + (2 * l - 2.8) * i / max(1, n - 1)
        jc = H.jerry_can((0, 0, 0), (0, math.radians(90), math.radians(90)))
        xform(jc, (0, h + 1.35, z))
        for f in jc.faces:
            f.material_index = cols[i] if f.material_index == 0 else 4
        parts.append(jc)
    return join(*parts), ["cage_black", "jerry_olive", "jerry_red", "jerry_sand", "steel_dark"]


def armor_plate(L=18.0, Hh=5.0):
    """Right-side bolted plate (sits proud of the door). mats [steel, rivet]."""
    plate = rbox((0.25, 0, 0), (0.5, Hh, L), 0.15, 1)
    pts = []
    for z in [-L / 2 + 0.7 + i * (L - 1.4) / 9 for i in range(10)]:
        for y in (-Hh / 2 + 0.5, Hh / 2 - 0.5):
            pts.append((0.55, y, z))
    for y in (-0.8, 0.8):
        for z in (-L / 2 + 0.7, L / 2 - 0.7):
            pts.append((0.55, y, z))
    seam = rbox((0.55, 0, 2.0), (0.15, Hh * 0.95, 0.25), 0.05, 1)
    return join(_mi(plate, 0), _mi(H.rivets(pts, 0.2), 1), _mi(seam, 2)), ["rusty", "steel_dark", "steel"]


def turret_cannon():
    """Roof weapon: slewing ring, yoke, cannon with muzzle brake and recoil sleeve, ammo box, shield. Socket on the roof.
    mats [steel, dark, hazard, chrome]."""
    parts = [_mi(H.revolve([(0, 0), (0, 3.2), (0.6, 3.2), (0.8, 2.8), (0.8, 0)], 28, axis=(0, 1, 0)), 0)]
    parts.append(_mi(rbox((0, 1.6, 0), (4.2, 1.6, 4.0), 0.4, 2), 0))
    for s in (-1, 1):
        parts.append(_mi(rbox((s * 1.6, 3.0, 0.2), (0.5, 2.6, 2.4), 0.2, 1), 0))
    parts.append(_mi(H.revolve([(-2.6, 0), (-2.6, 1.05), (2.2, 1.05), (2.2, 0.85), (13.0, 0.55), (13.0, 0.75), (14.6, 0.75),
                                (14.6, 0.3), (14.6, 0.0)], 18, origin=(0, 3.4, 0), axis=(0, 0, 1)), 1))
    for z in (13.3, 13.9):
        parts.append(_mi(rbox((0, 3.4, z), (2.2, 0.35, 0.3), 0.05, 1), 1))
    parts.append(_mi(rbox((-2.6, 2.6, -0.8), (1.6, 1.6, 2.6), 0.15, 1), 2))
    parts.append(_mi(rbox((0, 4.0, 2.6), (5.0, 3.0, 0.3), 0.1, 1, rot=(math.radians(-12), 0, 0)), 0))
    parts.append(_mi(H.revolve([(0, 0), (0, 0.35), (0.8, 0.35), (0.8, 0)], 8, origin=(1.4, 4.8, -1.0), axis=(0, 1, 0)), 3))
    return join(*parts), ["steel", "steel_dark", "hazard", "chrome"]


def snorkel(height=12.0):
    """Right-side snorkel up the A-pillar with a forward ram head. Socket at the fender. mats [plastic, dark]."""
    pts = [(0, 0, 0), (0.2, 3.0, -0.3), (0.2, height - 1.5, -2.6), (0.2, height, -2.8)]
    p = tube(pts, 0.55, 10)
    head = rbox((0.2, height + 0.4, -2.0), (1.5, 1.4, 2.6), 0.5, 2)
    mouth = rbox((0.2, height + 0.4, -0.75), (1.2, 1.0, 0.15), 0.1, 1)
    clamps = [_mi(H.revolve([(-0.2, 0.62), (0.2, 0.62), (0.2, 0.7), (-0.2, 0.7), (-0.2, 0.62)], 10,
                            origin=(0.2, y, -0.3 - (y / height) * 2.3), axis=(0, 1, 0)), 1) for y in (3.0, 7.0)]
    return join(_mi(p, 0), _mi(head, 0), _mi(mouth, 1), *clamps), ["plastic", "void"]


def crane(boom_len=26.0):
    """Cargo crane: pedestal, slewing house, lattice-ish boom with a hydraulic ram, hook block on a cable.
    Socket at the deck. mats [hazard, steel, chrome, dark]."""
    parts = [_mi(H.revolve([(0, 0), (0, 2.6), (1.2, 2.6), (1.2, 1.8), (3.0, 1.8), (3.0, 0)], 20, axis=(0, 1, 0)), 1)]
    parts.append(_mi(rbox((0, 4.2, -0.5), (3.6, 2.8, 4.6), 0.4, 2), 0))
    a = math.radians(32)
    d = Vector((0, math.sin(a), math.cos(a)))
    base = Vector((0, 5.4, -1.2))
    tip = base + d * boom_len
    up = Vector((0, math.cos(a), -math.sin(a)))
    for s in (-1, 1):
        parts.append(_mi(tube([base + Vector((s * 0.9, 0, 0)), tip + Vector((s * 0.5, 0, 0))], 0.35, 6), 0))
        parts.append(_mi(tube([base + Vector((s * 0.9, 0, 0)) + up * 1.6, tip + Vector((s * 0.5, 0, 0)) + up * 0.6], 0.3, 6), 0))
    for i in range(1, 9):                       # lattice diagonals
        t0, t1 = (i - 1) / 8, i / 8
        p0 = base.lerp(tip, t0) + up * (1.6 * (1 - t0) + 0.6 * t0) * (i % 2)
        p1 = base.lerp(tip, t1) + up * (1.6 * (1 - t1) + 0.6 * t1) * ((i + 1) % 2)
        for s in (-1, 1):
            parts.append(_mi(cyl(p0 + Vector((s * 0.8, 0, 0)), p1 + Vector((s * 0.8, 0, 0)), 0.15, 5), 0))
    ram0 = Vector((0, 2.8, 1.6))
    ram1 = base.lerp(tip, 0.42) + up * -0.3
    parts.append(_mi(cyl(ram0, ram0.lerp(ram1, 0.55), 0.6, 10), 1))
    parts.append(_mi(cyl(ram0.lerp(ram1, 0.5), ram1, 0.35, 10), 2))
    parts.append(_mi(H.revolve([(-0.6, 0), (-0.6, 0.9), (0.6, 0.9), (0.6, 0)], 14, origin=tip, axis=(1, 0, 0)), 1))
    hook_y = max(4.0, tip.y - 9.0)
    parts.append(_mi(cyl(tip + Vector((0, -0.9, 0)), Vector((0, hook_y + 1.6, tip.z)), 0.1, 4), 3))
    parts.append(_mi(rbox((0, hook_y + 0.8, tip.z), (1.2, 1.8, 0.8), 0.25, 2), 0))
    parts.append(_mi(tube([(0, hook_y, tip.z), (0, hook_y - 1.0, tip.z), (0, hook_y - 1.4, tip.z + 0.6), (0, hook_y - 0.9, tip.z + 1.0)], 0.22, 6), 1))
    parts.append(_mi(rbox((-1.4, 3.3, 1.6), (0.6, 1.2, 1.0), 0.1, 1), 1))       # control levers box
    return join(*parts), ["hazard", "steel", "chrome", "void"]


def lights_bar(w=6.0):
    """Roof light bar: square tube with four round lamps facing forward, two brackets. mats [cage, chrome, lens]."""
    parts = [_mi(rbox((0, 1.4, 0), (2 * w + 1.0, 0.7, 0.7), 0.15, 1), 0)]
    for x in (-w * 0.95, w * 0.95):
        parts.append(_mi(rbox((x, 0.6, 0), (0.5, 1.4, 0.6), 0.1, 1), 0))
    for i in range(4):
        x = -w * 0.72 + i * w * 0.48
        lp = H.lamp_round((x, 2.3, 0.4), 0.85, (0, 0, 1), depth=0.9)
        parts.append(lp)
    bm = join(*parts)
    return bm, ["cage_black", "chrome", "lamp_yellow"]


def steps_side(L=8.0):
    """Right-side step board on two brackets. mats [steel, rubber]."""
    return join(_mi(rbox((1.2, 0, 0), (2.4, 0.4, L), 0.12, 1), 0), _mi(rbox((1.2, 0.25, 0), (2.0, 0.12, L - 0.6), 0.05, 1), 1),
                _mi(rbox((0.3, 0.6, -L * 0.35), (0.6, 1.4, 0.6), 0.08, 1), 0), _mi(rbox((0.3, 0.6, L * 0.35), (0.6, 1.4, 0.6), 0.08, 1), 0)), \
        ["steel", "rubber"]


def beacon():
    return join(_mi(rbox((0, 0.3, 0), (5.0, 0.6, 1.4), 0.2, 1), 0),
                _mi(H.revolve([(0, 1.0), (0.6, 0.95), (1.4, 0.7), (1.8, 0.0)], 16, origin=(0, 0.5, 0), axis=(0, 1, 0)), 1),
                _mi(H.revolve([(0, 0.6), (0.9, 0.5), (1.2, 0.0)], 12, origin=(-1.9, 0.5, 0), axis=(0, 1, 0)), 1),
                _mi(H.revolve([(0, 0.6), (0.9, 0.5), (1.2, 0.0)], 12, origin=(1.9, 0.5, 0), axis=(0, 1, 0)), 1)), ["plastic", "lamp_amber"]


def roof_rails(L, x):
    parts = []
    for s in (-1, 1):
        parts.append(_mi(tube([(s * x, 0, -L / 2), (s * x, 0.6, -L / 2 + 1.0), (s * x, 0.6, L / 2 - 1.0), (s * x, 0, L / 2)], 0.28, 6), 0))
    return join(*parts), ["chrome"]
