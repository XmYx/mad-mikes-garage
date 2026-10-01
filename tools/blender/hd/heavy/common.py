"""Shared heavy-vehicle details: cab interiors, default socket parts (exhaust stack, plough bumper, light bar,
turret, jerry rack, spikes, chrome bumpers), fuel tanks, mirrors, mud flaps. Game-voxel coordinates (see hvlib)."""
import math

from hvlib import MB, P, empty, instance  # noqa: F401


def cab_interior(mb, f, dashZ, seatZ, dx, half=8, seat_mat="leather"):
    """HD version of VehicleDesigns.Interior: two bucket seats, dashboard with gauges, steering wheel, levers.
    f = floor voxel layer, dashboard at dashZ..dashZ+3, seats end at seatZ, driver at x = dx."""
    mb.box((-half - .5, f + 4.5, dashZ - .5), (half + .5, f + 8.5, dashZ + 3.5), "black", 0.03, 2)       # dash
    mb.box((-half - .5, f + 8.3, dashZ - 0.2), (half + .5, f + 8.8, dashZ + 3.5), "fabric", 0.02)         # dash top
    for gx in (dx - 1.2, dx + 1.2):
        mb.cyl((gx, f + 7.2, dashZ - .6), (gx, f + 7.2, dashZ - .45), 0.9, "lens", 12)
    mb.box((1, f + 6, dashZ - .7), (5, f + 7.5, dashZ - .45), "steel_dark", 0.0)                          # radio
    for sx in (-1, 1):
        x0, x1 = (1.5, 6.5) if sx > 0 else (-6.5, -1.5)
        mb.box((x0, f + 0.5, seatZ - 4.5), (x1, f + 3.5, seatZ + 0.5), seat_mat, 0.06, 2)               # cushion
        mb.box((x0, f + 3.5, seatZ - 5.6), (x1, f + 11.5, seatZ - 4.4), seat_mat, 0.06, 2)              # backrest
        mb.box((x0 + 1, f + 11.5, seatZ - 5.4), (x1 - 1, f + 13, seatZ - 4.6), seat_mat, 0.05, 2)       # headrest
        mb.box((x0 + .5, f - 0.5, seatZ - 4), (x1 - .5, f + 0.5, seatZ), "steel_dark", 0.0)              # seat base
    # steering wheel (tilted ring) + column
    c = (dx, f + 8.5, dashZ - 1.3)
    mb.frame(c, 0, -25)
    mb.lathe([(2.3, 0), (2.6, 0.5), (2.3, 1.0), (2.0, 0.5)], (0, 0, -0.25), (0, 0, 0.25), "black", seg=20)
    mb.cyl((0, 0, 0), (0, 0, 1.5), 0.3, "steel_dark", 8)
    mb.cyl((-2.2, 0, 0), (2.2, 0, 0), 0.22, "black", 6)
    mb.reset()
    mb.cyl((dx, f + 7.5, dashZ - 0.3), (dx, f + 8.4, dashZ - 1.2), 0.4, "steel_dark", 8)
    mb.cyl((0, f + 0.5, seatZ + 2), (0.6, f + 6, seatZ + 1), 0.25, "chrome", 6)                            # gear lever
    mb.ball((0.6, f + 6.2, seatZ + 1), 0.6, "black", 8, 5)


def exhaust_stack(root, socket_pos, mirror_pair=True, top=58, name="Exhaust"):
    """exhaust_stack part: vertical chrome pipe with a perforated heat shield, elbow from below and a rain cap.
    Authored for the right side (+X outward), origin = socket."""
    mb = MB(name + "_R")
    mb.cyl((0.8, -4, 0), (0.8, 0, 0), 1.0, "rust", 10)
    mb.cyl((0.8, 0, 0), (1.6, top - socket_pos[1] - 4, 0), 1.2, "chrome", 14)
    mb.cyl((1.6, top - socket_pos[1] - 4, 0), (1.6, top - socket_pos[1], -0.3), 1.2, "chrome", 14, r2=1.25)
    mb.box((1.6 - 1.4, top - socket_pos[1] - 0.2, -1.6), (1.6 + 1.4, top - socket_pos[1] + 0.2, 1.2), "chrome_dull", 0.01)   # flap
    mb.lathe([(1.55, 0), (1.55, 1)], (1.6, 6, 0), (1.6, 18, 0), "steel_bare", seg=14)                           # heat shield
    for y in (7, 12, 17):
        mb.cyl((0.6, y, 0), (-0.4, y, 0), 0.25, "steel_dark", 6)                                                # brackets
    for i in range(10):
        y = 7.5 + i
        mb.cyl((3.1, y, -0.6), (3.2, y, -0.6), 0.25, "void", 6)
    ob = mb.finish(parent=root, origin=socket_pos, props={"socket": "exhaust", "part": "exhaust_stack", "category": "Exhaust"}, local=True)
    if mirror_pair:
        instance(ob, name + "_L", root, (-socket_pos[0], socket_pos[1], socket_pos[2]), True,
                 {"socket": "exhaust_L", "part": "exhaust_stack", "category": "Exhaust"})
    return ob


def bumper_plow(root, pos, width=16, name="Bumper_front"):
    """bumper_plow: wedge plough blade with ribs, bolted to a push frame (origin = socket)."""
    mb = MB(name)
    mb.prism([(0, 0), (4, 0), (6.5, 9), (2, 10.5), (0, 6)], "x", -width, width, "hazard", 0.02)
    for x in range(-width + 2, width, 4):
        mb.prism([(-3, 3), (0, 2), (0, 8), (-3, 8)], "x", x - 0.4, x + 0.4, "steel_dark", 0.005)
    mb.box((-width, 0, 4.5), (width, 0.8, 7.2), "steel_bare", 0.01)                                    # cutting edge
    for s in (-1, 1):
        mb.box((s * 7 - 1, 4, -6), (s * 7 + 1, 6.5, 0), "steel_dark", 0.01)
        mb.rivets([(s * 7 + dx, 6.6, -2 - dz) for dx in (-0.5, 0.5) for dz in (0, 2)], (0, 1, 0), 0.3)
    return mb.finish(parent=root, origin=pos, props={"socket": "bumper_front", "part": "bumper_plow", "category": "FrontBumper"}, local=True)


def bumper_chrome(root, pos, half=14, name="Bumper_front", rear=False, sock="bumper_front", part="bumper_chrome"):
    mb = MB(name)
    d = -1 if rear else 1
    mb.prism([(0, 0), (2.4 * d, 0.4), (2.6 * d, 2.0), (2.4 * d, 3.6), (0, 4)], "x", -half, half, "chrome", 0.01)
    for s in (-1, 1):
        mb.box((s * 7 - 1, 0.8, -3 * d), (s * 7 + 1, 3.2, 0), "steel_dark", 0.0)
        if not rear:
            mb.prism([(2.3, 0.5), (4.5, 0.8), (4.5, 3.2), (2.3, 3.5)], "x", s * 6 - 0.8, s * 6 + 0.8, "chrome", 0.01)   # overriders
    mb.box((-3, 1, 2.5 * d), (3, 3, 2.75 * d), "paint_cream", 0.0)                                   # plate
    return mb.finish(parent=root, origin=pos, props={"socket": sock, "part": part, "category": "RearBumper" if rear else "FrontBumper"}, local=True)


def lights_bar(root, pos, half=12, name="Lights", n=6):
    mb = MB(name)
    for s in (-1, 1):
        mb.cyl((s * half, 0, 0), (s * half, 3, 0), 0.4, "steel_dark", 8)
    mb.cyl((-half - 1, 3, 0), (half + 1, 3, 0), 0.55, "black", 10)
    for i in range(n):
        x = -half + 2 + i * (2 * half - 4) / (n - 1)
        mb.box((x - 1.3, 3.3, -1), (x + 1.3, 5.6, 0.6), "black", 0.01)
        mb.lamp((x, 4.45, 0.6), (0, 0, 1), 1.0, "lightw", "black", 0.3, square=True)
    return mb.finish(parent=root, origin=pos, props={"socket": "lights", "part": "lights_bar", "category": "Lights"}, local=True)


def emergency_lights(root, pos, half=10, name="Lights"):
    mb = MB(name)
    mb.box((-half, 0, -2), (half, 1, 2), "black", 0.02)
    for i, mat in enumerate(("tail", "lightw", "amber", "lightw", "tail")):
        x = -half + 2 + i * (2 * half - 4) / 4
        mb.box((x - 1.6, 1, -1.5), (x + 1.6, 3, 1.5), mat, 0.03, 2)
    return mb.finish(parent=root, origin=pos, props={"socket": "lights", "part": "lights_emergency", "category": "Lights"}, local=True)


def searchlight(root, pos, name="Lights"):
    base = MB(name)
    base.cyl((0, 0, 0), (0, 2, 0), 1.2, "steel_dark", 12)
    ob = base.finish(parent=root, origin=pos, props={"socket": "lights", "part": "lights_search", "category": "Lights"}, local=True)
    lamp = MB("lamp")
    lamp.box((-1.6, 0, -0.3), (1.6, 0.6, 0.3), "steel_dark", 0.0)
    for s in (-1, 1):
        lamp.box((s * 1.5 - 0.2, 0, -0.3), (s * 1.5 + 0.2, 3, 0.3), "steel_dark", 0.0)
    lamp.cyl((0, 3, -2), (0, 3, 1.5), 1.4, "black", 14)
    lamp.cyl((0, 3, 1.5), (0, 3, 1.8), 1.2, "lightw", 14)
    lamp.finish(parent=ob, origin=(pos[0], pos[1] + 2, pos[2]), props={"segment": "lamp"}, local=True)
    return ob


def jerry_rack(root, pos, name="Cargo", n=4, half=10):
    mb = MB(name)
    mb.box((-half, 0, -5), (half, 0.6, 5), "grating", 0.0)
    for s in (-1, 1):
        mb.cyl((s * half, 0.6, -5), (s * half, 0.6, 5), 0.35, "steel", 6)
        for z in (-5, 5):
            mb.cyl((s * half, 0, z), (s * half, 4, z), 0.35, "steel", 6)
        mb.cyl((s * half, 4, -5), (s * half, 4, 5), 0.35, "steel", 6)
    for i in range(n):
        x = -half + 2.5 + i * (2 * half - 5) / max(1, n - 1)
        mat = ("paint_olive", "paint_crimson", "paint_olive", "paint_navy")[i % 4]
        mb.box((x - 1.6, 0.6, -4), (x + 1.6, 9, 4), mat, 0.05, 2)
        mb.seam((x - 1.6, 4.8, -4), (x - 1.6, 4.8, 4), (-1, 0, 0))
        mb.cyl((x, 9, 2.5), (x, 10.2, 2.5), 0.6, "steel_dark", 8)
        for zz in (-2.2, -0.5):
            mb.box((x - 0.4, 9, zz - 0.3), (x + 0.4, 10.2, zz + 0.3), "steel_dark", 0.0)
    return mb.finish(parent=root, origin=pos, props={"socket": "cargo", "part": "cargo_jerry_rack", "category": "Cargo"}, local=True)


def roof_rack(root, pos, half=12, length=16, name="Cargo"):
    mb = MB(name)
    for s in (-1, 1):
        mb.cyl((s * half, 1.5, -length), (s * half, 1.5, length), 0.5, "steel", 8)
        for z in (-length + 1, 0, length - 1):
            mb.cyl((s * half, 0, z), (s * half, 1.5, z), 0.45, "steel", 6)
    for z in range(-length + 2, length - 1, 3):
        mb.cyl((-half, 1.5, z), (half, 1.5, z), 0.35, "steel", 6)
    mb.box((-8, 2, -10), (0, 6, -2), "canvas", 0.12, 2)
    mb.box((2, 2, 1), (9, 5, 9), "wood", 0.03)
    mb.lathe([(3.6, 0), (3.6, 1)], (-6, 2, 6), (-6, 5, 6), "tyre", seg=18, cap1="tyre")
    return mb.finish(parent=root, origin=pos, props={"socket": "cargo", "part": "cargo_roof_rack", "category": "Cargo"}, local=True)


def turret_cannon(root, pos, name="Weapon"):
    """weapon_turret_cannon: ring base ("mount" segment yaw) and the cannon ("gun" pitch)."""
    base = MB("Weapon")
    base.lathe([(6.5, 0), (6.5, 0.6), (5.8, 1.0)], (0, 0, 0), (0, 1.6, 0), "steel_dark", seg=28, cap1="steel_dark")
    ob = base.finish(parent=root, origin=pos, props={"socket": "roof", "part": "weapon_turret_cannon", "category": "Weapon"}, local=True)
    m = MB("mount")
    m.prism([(-5, 0), (5, 0), (4, 6), (-3, 6)], "x", -5, 5, "camo", 0.03)
    m.rivets([(5.4, 1.5 + i * 1.5, z) for i in range(3) for z in (-3, 0, 3)], (1, 0, 0), 0.3)
    m.box((-1, 6, -2), (1, 8, 1), "steel_dark", 0.02)
    mo = m.finish(parent=ob, origin=pos, props={"segment": "mount"}, local=True)
    gun = MB("gun")
    gun.cyl((0, 0, 0), (0, 0, 16), 0.9, "steel_dark", 12)
    gun.cyl((0, 0, 13), (0, 0, 17), 1.3, "steel_dark", 12)
    for i in range(5):
        gun.cyl((0, 0, 13.3 + i * 0.8), (0, 0, 13.6 + i * 0.8), 1.45, "void", 12)
    gun.box((-2.2, -2, -4), (2.2, 2, 2), "camo", 0.04)
    gun.finish(parent=mo, origin=(pos[0], pos[1] + 4, pos[2] + 2), props={"segment": "gun"}, local=True)
    return ob


def machine_gun(root, pos, name="Weapon"):
    base = MB("Weapon")
    base.cyl((0, 0, 0), (0, 1.2, 0), 4.8, "steel_dark", 24)
    ob = base.finish(parent=root, origin=pos, props={"socket": "roof", "part": "weapon_mg", "category": "Weapon"}, local=True)
    m = MB("mount")
    m.cyl((0, 0, 0), (0, 4, 0), 0.7, "steel", 10)
    m.prism([(-3, 2), (3, 2), (3, 9), (-1, 9)], "x", -4.6, -4.2, "camo", 0.01)
    m.prism([(-3, 2), (3, 2), (3, 9), (-1, 9)], "x", 4.2, 4.6, "camo", 0.01)
    m.prism([(2.6, 2), (3.0, 2), (3.0, 9), (2.6, 9)], "x", -4.6, 4.6, "camo", 0.01)
    mo = m.finish(parent=ob, origin=(pos[0], pos[1] + 2, pos[2]), props={"segment": "mount"}, local=True)
    g = MB("gun")
    g.box((-0.9, -0.9, -4), (0.9, 1.1, 2), "steel_dark", 0.01)
    g.cyl((0, 0, 2), (0, 0, 9), 0.4, "steel_dark", 8)
    g.cyl((0, 0, 6.5), (0, 0, 9.3), 0.6, "black", 8)
    g.box((-2.6, -1.8, -1), (-0.9, 0.6, 1.5), "paint_olive", 0.01)
    g.cyl((0, 0, -4), (0, -1.5, -5.5), 0.3, "steel_dark", 6)
    g.finish(parent=mo, origin=(pos[0], pos[1] + 6, pos[2]), props={"segment": "gun"}, local=True)
    return ob


def side_spikes(root, pos, length=20, name="Armor"):
    mb = MB(name + "_R")
    mb.box((0, -1.5, -length / 2), (0.8, 1.5, length / 2), "steel_dark", 0.01)
    for i in range(5):
        z = -length / 2 + 2 + i * (length - 4) / 4
        mb.cyl((0.8, 0, z), (6, 0, z + 1.2), 1.0, "steel_bare", 8, r2=0.05)
        mb.rivets([(0.8, 1.0, z - 1), (0.8, -1.0, z - 1)], (1, 0, 0), 0.25)
    ob = mb.finish(parent=root, origin=pos, props={"socket": "armor", "part": "armor_spikes", "category": "Armor"}, local=True)
    instance(ob, name + "_L", root, (-pos[0], pos[1], pos[2]), True, {"socket": "armor_L", "part": "armor_spikes", "category": "Armor"})
    return ob


def smoke_dischargers(root, pos, name="Armor"):
    mb = MB(name + "_R")
    mb.box((0, -1, -3), (1, 2, 3), "camo", 0.01)
    for i in range(3):
        mb.cyl((1, 0.5, -2 + i * 2), (3.5, 2.5, -2 + i * 2), 0.8, "steel_dark", 10)
    ob = mb.finish(parent=root, origin=pos, props={"socket": "armor", "part": "armor_smoke", "category": "Armor"}, local=True)
    instance(ob, name + "_L", root, (-pos[0], pos[1], pos[2]), True, {"socket": "armor_L", "part": "armor_smoke", "category": "Armor"})


def snorkel(root, pos, top=44, name="Snorkel"):
    mb = MB(name)
    mb.cyl((0, 0, 0), (1.5, 0, 0), 1.0, "black", 10)
    mb.cyl((1.5, 0, 0), (1.5, top - pos[1], 0), 1.0, "black", 10)
    mb.box((0.3, top - pos[1], -1.2), (2.7, top - pos[1] + 2.5, 2.2), "black", 0.03, 2)
    for y in (4, 10):
        mb.cyl((1.5, y, 0), (0, y, 0), 0.3, "steel", 6)
    return mb.finish(parent=root, origin=pos, props={"socket": "snorkel", "part": "snorkel", "category": "Snorkel"}, local=True)


def side_steps(root, pos, length=8, name="Steps"):
    mb = MB(name + "_R")
    mb.box((0, -0.4, -length / 2), (3, 0.4, length / 2), "tread", 0.01)
    for z in (-length / 2 + 1, length / 2 - 1):
        mb.box((0, 0.4, z - 0.4), (0.6, 3, z + 0.4), "steel_dark", 0.0)
    ob = mb.finish(parent=root, origin=pos, props={"socket": "steps", "part": "steps_side", "category": "Steps"}, local=True)
    instance(ob, name + "_L", root, (-pos[0], pos[1], pos[2]), True, {"socket": "steps_L", "part": "steps_side", "category": "Steps"})


def fuel_tank(mb, x, y, z0, z1, r, mat="chrome"):
    mb.lathe([(r * 0.9, 0), (r, 0.03), (r, 0.97), (r * 0.9, 1)], (x, y, z0), (x, y, z1), mat, seg=20, cap0=mat, cap1=mat)
    for z in (z0 + 1.5, z1 - 1.5):
        mb.lathe([(r + 0.15, 0), (r + 0.15, 1)], (x, y, z - 0.35), (x, y, z + 0.35), "steel_dark", seg=20)
    mb.cyl((x, y + r, (z0 + z1) / 2), (x, y + r + 0.6, (z0 + z1) / 2), 0.8, "black", 10)


def mirror_arm(mb, base, out, half_h=2.5, mat="black"):
    """West-coast mirror: arm from the cab to a tall mirror head."""
    bx, by, bz = base
    s = 1 if out > 0 else -1
    mb.cyl((bx, by, bz), (bx + out, by, bz), 0.25, "chrome", 6)
    mb.cyl((bx, by + half_h * 2, bz), (bx + out, by + half_h * 2, bz), 0.25, "chrome", 6)
    mb.box((bx + out - 0.3 * s - 0.5, by - 0.5, bz - 1.4), (bx + out + 0.3 * s + 0.5, by + half_h * 2 + 0.5, bz + 0.2), mat, 0.02)
    mb.box((bx + out - 0.6, by, bz - 1.45), (bx + out + 0.6, by + half_h * 2, bz - 1.38), "chrome", 0.0)


def mud_flap(mb, x0, x1, y0, y1, z, text=False):
    mb.box((x0, y0, z - 0.3), (x1, y1, z + 0.3), "rubber", 0.01)
    mb.box((x0, y1, z - 0.4), (x1, y1 + 0.6, z + 0.4), "chrome_dull", 0.005)


def window_bars(mb, axis, at, u0, u1, v0, v1, n=4, mat="steel_dark", out=0.6):
    """Welded anti-raider bars across a window opening (vertical bars)."""
    for i in range(n):
        u = u0 + (u1 - u0) * (i + 0.5) / n
        if axis == "x":
            mb.cyl((at + out, v0, u), (at + out, v1, u), 0.3, mat, 6)
        else:
            mb.cyl((u, v0, at + out), (u, v1, at + out), 0.3, mat, 6)


def plate_patch(mb, axis, at, u0, u1, v0, v1, mat="rust", out=1, rivet=True):
    """Welded-on repair / armour plate with rivets on a flat face."""
    t = 0.35
    if axis == "x":
        a0, a1 = (at, at + t * out) if out > 0 else (at + t * out, at)
        mb.box((a0, v0, u0), (a1, v1, u1), mat, 0.01)
        if rivet:
            pts = [(a1 if out > 0 else a0, v, u) for v in (v0 + 0.6, v1 - 0.6) for u in (u0 + 0.6, (u0 + u1) / 2, u1 - 0.6)]
            mb.rivets(pts, (out, 0, 0), 0.22)
    elif axis == "z":
        a0, a1 = (at, at + t * out) if out > 0 else (at + t * out, at)
        mb.box((u0, v0, a0), (u1, v1, a1), mat, 0.01)
        if rivet:
            pts = [(u, v, a1 if out > 0 else a0) for v in (v0 + 0.6, v1 - 0.6) for u in (u0 + 0.6, (u0 + u1) / 2, u1 - 0.6)]
            mb.rivets(pts, (0, 0, out), 0.22)
