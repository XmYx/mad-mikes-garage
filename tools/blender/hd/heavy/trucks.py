"""HD heavy road vehicles: Hauler, Semi, Bus, Ambulance, APC.
Run: blender -b -P trucks.py -- [Hauler Semi Bus Ambulance APC]"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hvlib as H  # noqa: E402
from hvlib import MB, add_wheels, arc_ring, empty, furniture, instance, rrect_top  # noqa: E402
import common as C  # noqa: E402


def interior_furniture(parent, items):
    root = H.empty("Interior", parent)
    counts = {}
    for kind, pos, yaw in items:
        counts[kind] = counts.get(kind, 0) + 1
        mb = MB(f"Furn_{kind}_{counts[kind]}")
        furniture(mb, kind, (0, 0, 0), yaw)
        ob = mb.finish(parent=root, origin=None, props={"piece": kind, "note": "default furniture (FurnitureLibrary piece)"})
        ob.location = H.P(*pos)          # geometry authored at the piece origin
    return root


def rear_lamp(mb, x, y, z, nz=-1):
    mb.lamp((x, y, z), (0, 0, nz), 0.9, "tail", "black", 0.4, square=True)


# ================================================================================================ HAULER
def hauler():
    H.new_scene()
    root = empty("Hauler", props={"design": "Hauler", "voxel": 0.08})
    b = MB("Body")
    paint, dark = "paint_riggreen", "steel_dark"
    # chassis
    for s in (-1, 1):
        b.vbox(s * 6, 8, -60, s * 8, 11, 54, dark, 0.01)
    for z in range(-56, 51, 8):
        b.vbox(-6, 9, z, 6, 10, z, dark, 0.0)
    # deck shared by cab and module
    b.box((-15.5, 14.5, -58.5), (15.5, 16.5, 25.5), "steel", 0.015)
    b.box((-14.5, 16.5, -57.5), (14.5, 16.65, 24.5), "tread", 0.0)
    # side walls with door + module window openings
    holes = [(7.5, 20.5, 16.5, 42.5), (-50.5, -41.5, 31.5, 36.5), (-22.5, -13.5, 31.5, 36.5)]
    for s in (-1, 1):
        b.wall("x", s * 15, -58.5, 25.5, 16.5, 47.5, holes, 1.0, paint, 0.01)
        xo = s * 15.5
        for z in range(-50, 25, 10):                                        # vertical stiffener ribs
            if 7 <= z <= 21:
                continue
            b.box((min(xo, xo + s * 0.35), 16.5, z - 0.35), (max(xo, xo + s * 0.35), 47.5, z + 0.35), paint, 0.004)
        b.box((min(xo, xo + s * 0.5), 23.5, -58.5), (max(xo, xo + s * 0.5), 24.5, 7.5), dark, 0.006)   # rub rail
        b.box((min(xo, xo + s * 0.5), 46.6, -58.5), (max(xo, xo + s * 0.5), 47.5, 25.5), dark, 0.006)  # drip rail
        for (za, zb) in ((-50.5, -41.5), (-22.5, -13.5)):                   # window seals + raider bars
            b.box((min(xo, xo + s * 0.3), 30.8, za - 0.6), (max(xo, xo + s * 0.3), 37.2, zb + 0.6), "rubber", 0.004)
            C.window_bars(b, "x", s * 15.0, za, zb, 31.5, 36.5, 5, out=s * 0.8)
        C.plate_patch(b, "x", xo, -36, -27, 18, 26, "rust", s)
        C.plate_patch(b, "x", xo, -8, 2, 37, 44, "steel", s)
        b.seam((xo, 30, -58), (xo, 30, 7), (s, 0, 0))
    # cab/module frame
    for s in (-1, 1):
        b.vbox(s * 14, 17, 3, s * 14, 47, 3, "steel", 0.006)
    b.vbox(-14, 44, 3, 14, 47, 3, "steel", 0.006)
    # rear wall with the door opening, front wall with the split windscreen
    b.wall("z", -58, -15.5, 15.5, 16.5, 47.5, [(-4.5, 4.5, 16.5, 42.5)], 1.0, paint, 0.01)
    b.wall("z", 25, -15.5, 15.5, 16.5, 47.5, [(-13.5, -0.6, 23.5, 44.5), (0.6, 13.5, 23.5, 44.5)], 1.0, paint, 0.01)
    b.box((-14, 23, 25.5), (14, 23.5, 26.2), "rubber", 0.0)
    b.prism([(25.5, 47.5), (29.5, 47.6), (29.5, 46.9), (25.5, 45.4)], "x", -15.5, 15.5, paint, 0.008)  # sun visor
    for x in range(-12, 13, 4):
        b.lamp((x, 47.2, 29.4), (0, -0.3, 1), 0.55, "amber", "black", 0.3, seg=8)
    # ladder on the rear wall (right)
    b.ladder(11, 13, -59.4, 17, 49, 3, "steel")
    # rear beam, hitch, tail lamps
    b.box((-15.5, 9.5, -60.5), (15.5, 12.5, -58.5), dark, 0.015)
    b.box((-1.5, 9.5, -62.5), (1.5, 12.5, -60.5), "steel_bare", 0.01)
    b.cyl((0, 11, -62.5), (0, 11, -63.3), 0.8, "steel_bare", 10)
    for s in (-1, 1):
        rear_lamp(b, s * 13, 17.8, -58.7)
        b.lamp((s * 13, 20, -58.6), (0, 0, -1), 0.6, "amber", "black", 0.3, seg=8)
        C.mud_flap(b, s * 11 - .5 if s > 0 else s * 15 - .5, s * 15 + .5 if s > 0 else s * 11 + .5, 2.5, 12.5, -49)
    # hood shell (top panel is the Hood part), grille, lamps, fenders
    b.prism(rrect_top(-11.5, 11.5, 12.5, 23.6, 1.6), "z", 25.5, 52.5, paint, 0.012)
    for s in (-1, 1):
        for z in range(30, 49, 3):
            b.box((s * 11.5 - 0.15, 18, z - 0.4), (s * 11.5 + 0.15, 22, z + 0.4), "void", 0.0)    # louvres
        C.plate_patch(b, "x", s * 11.5, 31, 45, 13, 17, "steel", s)
    b.grille(-9.5, 9.5, 12.5, 23.5, 52.6, "chrome", "chrome_dull", 12)
    b.box((-10.5, 23.6, 52.3), (10.5, 24.6, 53.4), "chrome", 0.01)
    for s in (-1, 1):
        b.lamp((s * 10.5, 20, 53.3), (0, 0, 1), 1.6, "lightw", "chrome", 0.8, seg=16)
        b.lamp((s * 10.5, 16.5, 53.2), (0, 0, 1), 0.7, "amber", "chrome", 0.4, seg=10)
        x0, x1 = (10.5, 15.5) if s > 0 else (-15.5, -10.5)
        b.prism(arc_ring(42, 7, 7.6, 8.9, 0, 180, 16), "x", x0, x1, "black", 0.01)
        b.prism(arc_ring(-33, 7, 7.6, 8.4, 20, 160, 14), "x", x0, x1, "black", 0.01)          # tandem guards
        # running board, step, fuel tank
        b.box((min(s * 15, s * 16.6), 11.5, 3.5), (max(s * 15, s * 16.6), 13.5, 24.5), "tread", 0.01)
        b.box((min(s * 15.5, s * 16.6), 13.8, 9.5), (max(s * 15.5, s * 16.6), 14.4, 14.5), "tread", 0.005)
        C.fuel_tank(b, s * 12, 11, -16, 2, 2.4)
    C.cab_interior(b, 16, 21, 14, -4)
    body = b.finish(parent=root, origin=(0, 0, 0), props={"part": "body"})

    gl = MB("Glass")
    for (x0, x1) in ((-13.5, -0.6), (0.6, 13.5)):
        gl.box((x0, 23.5, 24.9), (x1, 44.5, 25.1), "glass", 0.0)
    for s in (-1, 1):
        for (za, zb) in ((-50.5, -41.5), (-22.5, -13.5)):
            gl.box((s * 15 - 0.1, 31.5, za), (s * 15 + 0.1, 36.5, zb), "glass", 0.0)
    gl.finish(parent=body, origin=(0, 0, 0))

    rf = MB("Roof")
    rf.box((-15.5, 47.5, -58.5), (15.5, 49.5, 25.5), paint, 0.02)
    rf.box((-5, 49.5, -24), (5, 50.3, -14), "steel", 0.01)                     # hatch
    rf.box((-8, 49.5, 6), (8, 52, 14), "steel_dark", 0.02)                     # cab vent / AC box
    for z in (7, 9, 11, 13):
        rf.box((-7, 52, z - 0.3), (7, 52.2, z + 0.3), "void", 0.0)
    for s in (-1, 1):
        rf.cyl((s * 14, 50.5, -57), (s * 14, 50.5, 24), 0.4, "steel", 8)
        for z in range(-56, 25, 10):
            rf.cyl((s * 14, 49.5, z), (s * 14, 50.5, z), 0.3, "steel", 6)
    rf.box((-6, 49.5, -54), (6, 51, -46), "canvas", 0.1, 2)                     # tarp bundle
    rf.finish(parent=body, origin=(0, 0, 0))

    hd = MB("Hood")
    hd.prism(rrect_top(-11, 11, 23.6, 24.8, 0.6), "z", 26, 52.6, paint, 0.01)
    for z in range(30, 48, 4):
        hd.box((-6, 24.8, z), (6, 25.1, z + 1.2), "steel_dark", 0.004)
    hd.cyl((0, 24.8, 50), (0, 26, 51.5), 0.5, "chrome", 8)
    hd.finish(parent=root, origin=(0, 24, 26), props={"socket": "hood", "part": "hauler_hood", "category": "Hood"})

    dr = MB("Door_R")
    dr.wall("x", 15, 7.5, 20.5, 16.5, 42.5, [(9.5, 18.5, 29.5, 40.5)], 1.0, paint, 0.01)
    dr.box((14.9, 29.5, 9.5), (15.1, 40.5, 18.5), "glass", 0.0)
    dr.box((15.5, 27, 9), (16.0, 27.6, 11.5), "chrome", 0.005)
    C.plate_patch(dr, "x", 15.5, 9, 19, 18, 26, "steel", 1)
    for y in (20, 38):
        dr.cyl((15.6, y - 1, 20.3), (15.6, y + 1, 20.3), 0.35, "steel_dark", 8)
    C.window_bars(dr, "x", 15, 9.5, 18.5, 29.5, 40.5, 3, out=0.8)
    door = dr.finish(parent=root, origin=(15, 17, 20), props={"socket": "door", "part": "hauler_door", "category": "Door"})
    instance(door, "Door_L", root, (-15, 17, 20), True, {"socket": "door_L", "part": "hauler_door", "category": "Door"})

    rd = MB("Door_Rear")
    rd.box((-4.5, 16.5, -58.5), (4.5, 42.5, -57.5), paint, 0.01)
    rd.cyl((0, 36, -58.6), (0, 36, -58.2), 2.0, "rubber", 16)
    rd.cyl((0, 36, -58.7), (0, 36, -58.5), 1.6, "glass", 16)
    rd.box((2.5, 26, -59.1), (3.5, 29, -58.5), "chrome", 0.005)
    rd.rivets([(x, y, -58.5) for x in (-4, 4) for y in range(18, 42, 4)], (0, 0, -1), 0.22)
    rd.finish(parent=root, origin=(0, 17, -58), props={"socket": "door_rear", "part": "hauler_rear_door", "category": "Door"})

    add_wheels(root, [("wheel_front", 11, 7, 42, True), ("wheel_mid", 11, 7, -26, True), ("wheel_rear", 11, 7, -40, True)])
    C.exhaust_stack(root, (16, 20, 22))
    C.side_spikes(root, (16, 10, 2))
    C.bumper_plow(root, (0, 3, 54))
    C.turret_cannon(root, (0, 50, 0))
    C.jerry_rack(root, (0, 50, -40))
    C.lights_bar(root, (0, 50, 22))
    interior_furniture(body, [("bed", (-9, 16.5, -46), 0), ("fridge", (11, 16.5, -30), -90), ("workbench", (10, 16.5, -46), -90),
                              ("locker", (-12, 16.5, -28), 90), ("stove", (11, 16.5, -16), -90), ("crate", (-10, 16.5, -14), 0),
                              ("lamp", (0, 47.5, -30), 0), ("lamp", (0, 47.5, -8), 0), ("shelf", (-14.5, 36, -46), 90)])
    return root


def side_rail(b, s, xo, z0, z1, y0, y1, mat="black", out=0.45):
    b.box((min(xo, xo + s * out), y0, z0), (max(xo, xo + s * out), y1, z1), mat, 0.006)


# ================================================================================================ BUS
def bus():
    H.new_scene()
    root = empty("Bus", props={"design": "Bus", "voxel": 0.08})
    b = MB("Body")
    Y, blk = "paint_ochre", "black"
    for s in (-1, 1):
        b.vbox(s * 6, 8, -78, s * 8, 11, 44, "steel_dark", 0.01)
    for z in range(-74, 41, 8):
        b.vbox(-6, 9, z, 6, 10, z, "steel_dark", 0.0)
    b.box((-15.5, 14.5, -77.5), (15.5, 16.5, 22.5), "steel", 0.015)
    b.box((-14.5, 16.5, -76.5), (14.5, 16.65, 21.5), "wood", 0.0)                         # plank floor of the home
    wins = [(-70 + 12 * k - 0.5, -70 + 12 * k + 8.5) for k in range(7)]
    for s in (-1, 1):
        holes = [(za, zb, 28.5, 37.5) for za, zb in wins]
        if s > 0:
            holes.append((7.5, 20.5, 16.5, 43.5))
        b.wall("x", s * 15, -77.5, 22.5, 16.5, 44.5, holes, 1.0, Y, 0.01)
        b.wall("x", s * 15, -69.5, 20.5 if s < 0 else 7.5, 10.5, 16.5, [(-61.5, -42.5, 10.5, 15.0)], 1.0, Y, 0.01)   # skirt
        xo = s * 15.5
        for y in (14, 22, 40):
            side_rail(b, s, xo, -77.5, 22.5 if (s < 0 or y < 17) else 7.5, y - 0.5, y + 0.5, blk)
        b.prism(arc_ring(-52, 7, 7.6, 8.6, 15, 165, 14), "x", min(s * 15.5, s * 16.2), max(s * 15.5, s * 16.2), blk, 0.008)
        for za, zb in wins:                                                       # window frames + pillars
            side_rail(b, s, xo, za - 0.3, zb + 0.3, 37.5, 38.2, "chrome_dull", 0.2)
            side_rail(b, s, xo, za - 0.3, zb + 0.3, 27.8, 28.5, "chrome_dull", 0.2)
        b.rivets([(xo, y, z) for z in range(-76, 22, 4) for y in (17.2, 26.8)], (s, 0, 0), 0.22)
        # boarded windows on the living side (post-apocalyptic)
        for k in (1, 4):
            za, zb = wins[k]
            for i in range(3):
                yy = 29.2 + i * 2.8
                b.box((min(xo + s * 0.3, xo + s * 0.7), yy, za - 0.6), (max(xo + s * 0.3, xo + s * 0.7), yy + 2.2, zb + 0.6), "wood", 0.01,
                      rot=None)
        C.plate_patch(b, "x", xo, -30, -22, 17.5, 21, "rust", s)
    b.text("DUST COUNTY SCHOOLS", (15.5, 24.9, -35), (1, 0, 0), 2.6, blk, 0.2)
    b.text("DUST COUNTY SCHOOLS", (-15.5, 24.9, -35), (-1, 0, 0), 2.6, blk, 0.2)
    # rear wall: emergency door (part), windows, lamps, lettering
    b.wall("z", -77, -15.5, 15.5, 16.5, 44.5, [(-12.5, -6.5, 29.5, 38.5), (6.5, 12.5, 29.5, 38.5), (-3.5, 3.5, 16.5, 42.5)], 1.0, Y, 0.01)
    for y in (22, 40):
        b.box((-15.5, y - 0.5, -78), (-3.5, y + 0.5, -77.5), blk, 0.005)
        b.box((3.5, y - 0.5, -78), (15.5, y + 0.5, -77.5), blk, 0.005)
    for s in (-1, 1):
        rear_lamp(b, s * 12.5, 19, -77.7)
        b.lamp((s * 12.5, 21.5, -77.6), (0, 0, -1), 0.6, "amber", "black", 0.3, seg=8)
        b.lamp((s * 13, 42.5, -77.6), (0, 0, -1), 0.9, "amber", "black", 0.4, seg=10)
        b.lamp((s * 13, 42.5, 22.6), (0, 0, 1), 0.9, "amber", "black", 0.4, seg=10)
    b.text("SCHOOL BUS", (0, 42.6, -77.6), (0, 0, -1), 2.2, blk, 0.2)
    b.box((-9, 41, -77.6), (9, 44, -77.5), Y, 0.0)
    # front wall: split windscreen + destination sign
    b.wall("z", 22, -15.5, 15.5, 16.5, 44.5, [(-13.5, -0.5, 27.5, 41.5), (0.5, 13.5, 27.5, 41.5), (-8.5, 8.5, 41.6, 44.4)], 1.0, Y, 0.01)
    b.box((-8.5, 41.6, 21.8), (8.5, 44.4, 22.2), "sign", 0.0)
    b.box((-15.5, 21.5, 22.5), (15.5, 22.5, 23.0), blk, 0.004)
    # dog nose: sloped hood shell, fenders, grille, lamps
    b.prism([(22.5, 11.5), (45.5, 11.5), (45.5, 23.5), (44.5, 24.2), (22.5, 26.6)], "x", -11.5, 11.5, Y, 0.06, 3)
    b.grille(-9.5, 9.5, 12.5, 24.5, 45.6, "chrome", "chrome_dull", 14)
    b.box((-10.5, 24.6, 45.2), (10.5, 25.5, 46.2), "chrome", 0.01)
    for s in (-1, 1):
        b.lamp((s * 10.5, 22, 46.2), (0, 0, 1), 1.5, "lightw", "chrome", 0.6, seg=16)
        b.lamp((s * 11, 19, 46.0), (0, 0, 1), 0.6, "amber", "chrome", 0.4, seg=10)
        x0, x1 = (10.5, 14.8) if s > 0 else (-14.8, -10.5)
        b.prism(arc_ring(34, 7, 7.6, 9.0, 0, 180, 18), "x", x0, x1, blk, 0.02)
        b.box((min(s * 10.5, s * 14.8), 7.5, 24.5), (max(s * 10.5, s * 14.8), 16.5, 25.5), blk, 0.01)
        C.mirror_arm(b, (s * 15.6, 30, 22.4), s * 1.6, 2.4)
    # stop arm (left side)
    b.box((-16.2, 31.5, 11.5), (-15.6, 33, 12.5), blk, 0.0)
    b.cyl((-17.2, 32, 12), (-17.0, 32, 12), 2.2, "red_cross", 8)
    b.text("STOP", (-17.25, 32, 12), (-1, 0, 0), 1.1, "white_enamel", 0.1)
    # exhaust tail pipe on the right
    b.cyl((15.5, 12, -68), (16.5, 11.5, -74), 0.7, "rust", 10)
    C.cab_interior(b, 16, 19, 13, -4)
    body = b.finish(parent=root, origin=(0, 0, 0), props={"part": "body"})

    gl = MB("Glass")
    for (x0, x1) in ((-13.5, -0.5), (0.5, 13.5)):
        gl.box((x0, 27.5, 21.9), (x1, 41.5, 22.1), "glass", 0.0)
    for s in (-1, 1):
        for k, (za, zb) in enumerate(wins):
            if k in (1, 4):
                continue
            gl.box((s * 15 - 0.1, 28.5, za), (s * 15 + 0.1, 37.5, zb), "glass", 0.0)
    for (x0, x1) in ((-12.5, -6.5), (6.5, 12.5)):
        gl.box((x0, 29.5, -77.1), (x1, 38.5, -76.9), "glass", 0.0)
    gl.finish(parent=body, origin=(0, 0, 0))

    rf = MB("Roof")
    rf.prism(rrect_top(-15.6, 15.6, 44.5, 47.2, 2.6, 5), "z", -77.6, 22.6, "paint_cream", 0.01)
    rf.box((-4.5, 47.0, -30.5), (4.5, 47.8, -21.5), "steel", 0.01)
    rf.box((-3.8, 47.8, -29.8), (3.8, 48.0, -22.2), "glass", 0.0)
    for s in (-1, 1):
        rf.lamp((s * 12, 47.1, 21.5), (0, 1, 0), 0.6, "amber", "black", 0.3, seg=8)
        rf.lamp((s * 12, 47.1, -76.5), (0, 1, 0), 0.6, "tail", "black", 0.3, seg=8)
    for z in range(-74, 20, 6):
        rf.box((-15.6, 46.6, z - 0.15), (15.6, 47.25, z + 0.15), "paint_cream", 0.004)   # roof bows
    rf.box((6, 47.2, -12), (14, 47.6, 6), "steel_bare", 0.01)                     # solar panel (scavenged)
    rf.box((6.3, 47.6, -11.7), (13.7, 47.7, 5.7), "glass", 0.0)
    rf.finish(parent=body, origin=(0, 0, 0))

    hd = MB("Hood")
    hd.prism([(23, 24.6), (44.6, 23.7), (44.6, 24.4), (23, 27.2)], "x", -11.2, 11.2, Y, 0.05, 2)
    for s in (-1, 1):
        hd.box((s * 11.25 - 0.25, 23.9, 24), (s * 11.25 + 0.25, 24.3, 44.4), "chrome", 0.004)   # hood edge trim
    hd.finish(parent=root, origin=(0, 24, 23), props={"socket": "hood", "part": "bus_hood", "category": "Hood"})

    dr = MB("Door_R")                                                          # two-leaf folding glass door
    for (za, zb) in ((7.5, 13.9), (14.1, 20.5)):
        dr.wall("x", 15, za, zb, 16.5, 43.5, [(za + 0.8, zb - 0.8, 24, 41.5), (za + 0.8, zb - 0.8, 17.5, 23)], 0.6, blk, 0.005)
        dr.box((14.95, 24, za + 0.8), (15.05, 41.5, zb - 0.8), "glass", 0.0)
        dr.box((14.95, 17.5, za + 0.8), (15.05, 23, zb - 0.8), "glass", 0.0)
    dr.cyl((15.4, 17, 14), (15.4, 43, 14), 0.3, "chrome", 8)
    dr.finish(parent=root, origin=(15, 17, 20), props={"socket": "door", "part": "bus_door", "category": "Door"})

    rd = MB("Door_Rear")
    rd.wall("z", -77, -3.5, 3.5, 16.5, 42.5, [(-2.5, 2.5, 29.5, 38.5)], 1.0, Y, 0.01)
    rd.box((-2.5, 29.5, -77.1), (2.5, 38.5, -76.9), "glass", 0.0)
    rd.box((-3, 25.5, -77.9), (3, 26.5, -77.4), "chrome", 0.005)
    rd.text("EMERGENCY DOOR", (0, 40.2, -77.55), (0, 0, -1), 0.7, "red_cross", 0.08)
    rd.finish(parent=root, origin=(0, 17, -77), props={"socket": "door_rear", "part": "bus_rear_door", "category": "Door"})

    add_wheels(root, [("wheel_front", 12, 7, 34, True), ("wheel_rear", 12, 7, -52, True)])
    C.bumper_chrome(root, (0, 4, 46), 15)
    C.bumper_chrome(root, (0, 6, -79), 15, "Bumper_rear", True, "bumper_rear", "bumper_rear_chrome")
    C.lights_bar(root, (0, 47, 16), 12)
    C.roof_rack(root, (0, 47, -52), 12, 16)
    interior_furniture(body, [("bed", (-8, 16.5, -65), 0), ("sofa", (11, 16.5, -44), -90), ("table", (8, 16.5, -21), 90),
                              ("fridge", (-12, 16.5, -18), 90), ("stove", (-12, 16.5, -28), 90), ("sink", (-12, 16.5, -38), 90),
                              ("locker", (12, 16.5, -64), -90), ("crate", (-11, 16.5, -49), 0),
                              ("lamp", (0, 44.5, -60), 0), ("lamp", (0, 44.5, -36), 0), ("lamp", (0, 44.5, -12), 0)])
    return root


# ================================================================================================ SEMI TRACTOR
def semi():
    H.new_scene()
    root = empty("Semi", props={"design": "Semi", "voxel": 0.08})
    b = MB("Body")
    R_ = "paint_crimson"
    for s in (-1, 1):
        b.vbox(s * 6, 8, -52, s * 8, 11, 44, "steel_dark", 0.01)
    for z in range(-48, 41, 8):
        b.vbox(-6, 9, z, 6, 10, z, "steel_dark", 0.0)
    b.box((-14.5, 13.5, -16.5), (14.5, 15.5, 20.5), "steel", 0.015)
    for s in (-1, 1):
        b.wall("x", s * 14, 1.5, 20.5, 15.5, 42.5, [(3.5, 18.5, 15.5, 41.5)], 1.0, R_, 0.01)        # cab side (door hole)
        b.wall("x", s * 14, -16.5, 1.5, 15.5, 46.5, [(-12.5, -3.5, 32.5, 36.5)], 1.0, R_, 0.01)      # sleeper side
        xo = s * 14.5
        side_rail(b, s, xo, -16.5, 3.5, 23.5, 24.5, "chrome", 0.3)
        b.box((min(xo, xo + s * 0.3), 32, -13), (max(xo, xo + s * 0.3), 37, -3), "rubber", 0.004)
        b.rivets([(xo, y, z) for z in (-16, 1) for y in range(17, 46, 3)], (s, 0, 0), 0.2)
        # fenders with lamps, tanks, steps, tandem guards, flaps, mirrors
        x0, x1 = (10.5, 15.5) if s > 0 else (-15.5, -10.5)
        b.prism(arc_ring(34, 7, 7.6, 9.4, 0, 180, 18), "x", x0, x1, R_, 0.03, 2)
        b.lamp((s * 13.5, 17.5, 44.2), (0, 0, 1), 1.0, "lightw", "chrome", 0.6, seg=12)
        b.lamp((s * 15, 16, 43.2), (0.5 * s, 0, 1), 0.5, "amber", "chrome", 0.3, seg=8)
        C.fuel_tank(b, s * 12, 11, -12, 1, 2.6)
        for y in (10, 13):
            b.box((min(s * 15, s * 16.6), y - 0.5, 5.5), (max(s * 15, s * 16.6), y + 0.4, 16.5), "tread", 0.005)
        b.box((min(s * 9, s * 15.5), 15.5, -52.5), (max(s * 9, s * 15.5), 16.8, -21.5), "black", 0.02)
        b.prism(arc_ring(-37, 7, 7.6, 8.4, 30, 150, 10), "x", x0, x1, "black", 0.01)
        C.mud_flap(b, min(s * 11, s * 15.5), max(s * 11, s * 15.5), 2.5, 12.5, -53)
        rear_lamp(b, s * 11, 11.6, -53.4)
        C.mirror_arm(b, (s * 14.6, 30, 20.4), s * 1.8, 3.0)
        b.lamp((s * 12, 47.6, 1.0), (0, 0, 1), 0.5, "amber", "black", 0.3, seg=8)
    b.wall("z", 20, -14.5, 14.5, 15.5, 42.5, [(-13, -0.5, 28.5, 40.5), (0.5, 13, 28.5, 40.5)], 1.0, R_, 0.01)
    b.box((-14.5, 23.5, 20.5), (14.5, 24.5, 21.0), "chrome", 0.004)
    b.wall("z", -16, -14.5, 14.5, 15.5, 46.5, [], 1.0, R_, 0.01)
    b.prism(rrect_top(-14.5, 14.5, 42.5, 44.5, 1.2), "z", 1.5, 20.5, R_, 0.02)            # cab roof
    b.prism([(1.5, 42.5), (7.5, 42.5), (7.5, 43.5), (1.5, 48.5)], "x", -14.5, 14.5, R_, 0.02)   # fairing step
    b.prism(rrect_top(-14.5, 14.5, 46.5, 48.6, 1.4), "z", -16.5, 1.5, R_, 0.02)          # sleeper roof
    for x in (-10, -6, 6, 10):
        b.lamp((x, 44.6, 19.5), (0, 0.4, 1), 0.5, "amber", "black", 0.3, seg=8)
    # long nose
    b.prism([(20.5, 11.5), (44.5, 11.5), (44.5, 25.2), (20.5, 28.4)], "x", -11.5, 11.5, R_, 0.08, 3)
    b.grille(-9.5, 9.5, 12.5, 26.5, 45.4, "chrome", "chrome_dull", 16)
    b.box((-10.5, 26.6, 44.8), (10.5, 27.6, 45.9), "chrome", 0.01)
    b.text("WAR RIG", (0, 19.5, 45.95), (0, 0, 1), 1.4, "chrome", 0.15)
    # fifth wheel
    b.box((-7.5, 13.5, -42.5), (7.5, 15.5, -27.5), "steel_bare", 0.02)
    b.box((-1.5, 15.0, -42.6), (1.5, 15.6, -35.5), "void", 0.0)
    b.prism([(-42.5, 15.5), (-36, 15.5), (-42.5, 14.5)], "x", -7.5, 7.5, "steel_bare", 0.0)
    b.box((-7.5, 15.5, -30.5), (7.5, 16.5, -27.5), "steel", 0.01)
    b.box((-15.5, 9.5, -53.5), (15.5, 11.5, -52), "steel_dark", 0.01)
    # air lines coiled behind the cab
    for i, mat in enumerate(("tail", "paint_navy")):
        b.lathe([(0.25, 0), (0.25, 1)], (-2 + i * 4, 24, -17.5), (-2 + i * 4, 32, -17.5), mat, seg=8)
        for k in range(5):
            b.lathe([(1.6, 0), (1.8, 0.5), (1.6, 1)], (-2 + i * 4, 25 + k * 1.4, -18.6), (-2 + i * 4, 25.5 + k * 1.4, -18.6), mat, seg=12)
    C.cab_interior(b, 15, 16, 10, -4)
    body = b.finish(parent=root, origin=(0, 0, 0), props={"part": "body"})

    gl = MB("Glass")
    for (x0, x1) in ((-13, -0.5), (0.5, 13)):
        gl.box((x0, 28.5, 19.9), (x1, 40.5, 20.1), "glass", 0.0)
    for s in (-1, 1):
        gl.box((s * 14 - 0.1, 32.5, -12.5), (s * 14 + 0.1, 36.5, -3.5), "glass", 0.0)
    gl.finish(parent=body, origin=(0, 0, 0))

    hd = MB("Hood")
    hd.prism([(21, 27.6), (44.6, 24.6), (44.6, 25.6), (21, 28.9)], "x", -11.3, 11.3, R_, 0.05, 2)
    hd.cyl((0, 25.6, 43.6), (0, 28.6, 44.2), 0.35, "chrome", 8)
    hd.ball((0, 28.8, 44.3), 0.8, "chrome", 10, 6, 0.6)
    hd.finish(parent=root, origin=(0, 25, 21), props={"socket": "hood", "part": "semi_hood", "category": "Hood"})

    dr = MB("Door_R")
    dr.wall("x", 14, 3.5, 18.5, 15.5, 41.5, [(5.5, 16.5, 28.5, 38.5)], 1.0, R_, 0.01)
    dr.box((13.9, 28.5, 5.5), (14.1, 38.5, 16.5), "glass", 0.0)
    dr.box((14.5, 23.5, 3.5), (14.8, 24.5, 18.5), "chrome", 0.004)
    dr.box((14.5, 26, 5), (15, 26.6, 7.5), "chrome", 0.004)
    dr.text("MM", (14.55, 19.5, 11), (1, 0, 0), 2.5, "chrome", 0.1)
    door = dr.finish(parent=root, origin=(14, 15, 18), props={"socket": "door", "part": "semi_door", "category": "Door"})
    instance(door, "Door_L", root, (-14, 15, 18), True, {"socket": "door_L", "part": "semi_door", "category": "Door"})

    add_wheels(root, [("wheel_front", 11, 7, 34, True), ("wheel_mid", 11, 7, -30, True), ("wheel_rear", 11, 7, -44, True)])
    C.exhaust_stack(root, (15, 16, -1), top=56)
    C.bumper_chrome(root, (0, 5, 46), 15)
    C.lights_bar(root, (0, 49, -4), 12)
    return root


# ================================================================================================ AMBULANCE
def ambulance():
    H.new_scene()
    root = empty("Ambulance", props={"design": "Ambulance", "voxel": 0.08})
    b = MB("Body")
    W, Rd = "paint_cream", "red_cross"
    for s in (-1, 1):
        b.vbox(s * 6, 6, -41, s * 8, 8, 32, "steel_dark", 0.01)
    b.box((-13.5, 9.5, -40.5), (13.5, 11.5, 20.5), "steel", 0.015)
    b.box((-12.5, 11.5, -39.5), (12.5, 11.65, 19.5), "tread", 0.0)
    for s in (-1, 1):
        b.wall("x", s * 13, -40.5, 20.5, 11.5, 33.5, [(7.5, 19.5, 11.5, 33.5)], 1.0, W, 0.01)
        b.wall("x", s * 13, -40.5, 7.5, 6.5, 11.5, [(-35.5, -16.5, 6.5, 11.5)], 1.0, W, 0.01)
        xo = s * 13.5
        side_rail(b, s, xo, -40.5, 7.5, 15.5, 18.5, Rd, 0.15)
        # red cross on the box
        b.box((min(xo, xo + s * .2), 24.5, -17.5), (max(xo, xo + s * .2), 27.5, -14.5), Rd, 0.0)
        b.box((min(xo, xo + s * .2), 20.5, -17.5), (max(xo, xo + s * .2), 31.5, -14.5), Rd, 0.0)
        b.box((min(xo, xo + s * .2), 24.5, -21.5), (max(xo, xo + s * .2), 27.5, -10.5), Rd, 0.0)
        b.box((min(s * 13, s * 13.6), 31.5, -40.5), (max(s * 13, s * 13.6), 33.5, 20.5), "white_enamel", 0.01)
        x0, x1 = (10.5, 13.6) if s > 0 else (-13.6, -10.5)
        b.prism(arc_ring(27, 6, 7.3, 8.5, 0, 180, 16), "x", x0, x1, "black", 0.01)
        b.prism(arc_ring(-26, 6, 7.3, 8.3, 0, 180, 16), "x", min(s * 13, s * 14), max(s * 13, s * 14), "black", 0.01)
        C.mirror_arm(b, (s * 13.6, 26, 19.4), s * 1.6, 1.8)
        b.lamp((s * 11.5, 30.5, -40.7), (0, 0, -1), 1.0, "tail", "black", 0.4, square=True)
        rear_lamp(b, s * 11.5, 13, -40.7)
        b.lamp((s * 12, 15.3, -40.6), (0, 0, -1), 0.5, "amber", "black", 0.3, seg=8)
        b.lamp((s * 13.6, 28, 18), (s, 0, 0), 0.8, "tail", "black", 0.3, seg=10)
        b.lamp((s * 13.6, 28, -38), (s, 0, 0), 0.8, "tail", "black", 0.3, seg=10)
    b.text("AMBULANCE", (13.55, 13.2, -16), (1, 0, 0), 2.0, Rd, 0.1)
    b.text("AMBULANCE", (-13.55, 13.2, -16), (-1, 0, 0), 2.0, Rd, 0.1)
    b.wall("z", 20, -13.5, 13.5, 11.5, 33.5, [(-11.5, 11.5, 20.5, 32.5)], 1.0, W, 0.01)
    b.box((-13.5, 33.5, -40.5), (13.5, 35.5, 20.5), "white_enamel", 0.03, 2)              # high roof (Roof object below)
    # short nose
    b.prism([(20.5, 10.5), (32.5, 10.5), (32.5, 18.0), (20.5, 20.6)], "x", -11.5, 11.5, W, 0.06, 3)
    b.box((-11.6, 15.5, 20.5), (11.6, 17.5, 32.6), Rd, 0.0)
    b.grille(-8.5, 8.5, 11.5, 15.5, 33.2, "chrome", "chrome_dull", 10)
    for s in (-1, 1):
        b.lamp((s * 9.5, 15.6, 33.3), (0, 0, 1), 1.1, "lightw", "chrome", 0.5, seg=14)
        b.lamp((s * 10, 12.8, 33.2), (0, 0, 1), 0.5, "amber", "chrome", 0.3, seg=8)
    C.cab_interior(b, 11, 16, 10, -4)
    body = b.finish(parent=root, origin=(0, 0, 0), props={"part": "body"})

    gl = MB("Glass")
    gl.box((-11.5, 20.5, 19.9), (11.5, 32.5, 20.1), "glass", 0.0)
    gl.finish(parent=body, origin=(0, 0, 0))

    rf = MB("Roof")
    rf.box((-13.6, 35.5, -40.5), (13.6, 35.8, 20.5), "white_enamel", 0.0)
    for s in (-1, 1):
        rf.cyl((s * 12.5, 36.6, -39), (s * 12.5, 36.6, 19), 0.35, "steel", 8)
        for z in (-38, -10, 18):
            rf.cyl((s * 12.5, 35.8, z), (s * 12.5, 36.6, z), 0.3, "steel", 6)
    rf.box((-4, 35.8, -32), (4, 37.5, -24), "steel_bare", 0.03)                    # roof vent
    rf.finish(parent=body, origin=(0, 0, 0))

    hd = MB("Hood")
    hd.prism([(21, 19.6), (32.6, 17.3), (32.6, 18.2), (21, 20.8)], "x", -11.3, 11.3, W, 0.04, 2)
    hd.finish(parent=root, origin=(0, 17, 21), props={"socket": "hood", "part": "amb_hood", "category": "Hood"})

    dr = MB("Door_R")
    dr.wall("x", 13, 7.5, 19.5, 11.5, 33.5, [(8.5, 18.5, 20.5, 30.5)], 1.0, W, 0.01)
    dr.box((12.9, 20.5, 8.5), (13.1, 30.5, 18.5), "glass", 0.0)
    dr.box((13.5, 15.5, 7.5), (13.65, 18.5, 19.5), Rd, 0.0)
    dr.box((13.5, 19, 9), (14, 19.6, 11.5), "chrome", 0.004)
    door = dr.finish(parent=root, origin=(13, 11, 19), props={"socket": "door", "part": "amb_door", "category": "Door"})
    instance(door, "Door_L", root, (-13, 11, 19), True, {"socket": "door_L", "part": "amb_door", "category": "Door"})

    rd = MB("Door_Rear")
    for s in (-1, 1):
        x0, x1 = (0.1, 12.5) if s > 0 else (-12.5, -0.1)
        rd.wall("z", -40, x0, x1, 8.5, 33.5, [(min(s * 2, s * 8), max(s * 2, s * 8), 26.5, 31.5)], 1.0, W, 0.01)
        rd.box((min(s * 2, s * 8), 26.5, -40.1), (max(s * 2, s * 8), 31.5, -39.9), "glass", 0.0)
        rd.box((x0, 15.5, -40.7), (x1, 18.5, -40.5), Rd, 0.0)
        rd.box((s * 1.2 - .3, 20, -41.1), (s * 1.2 + .3, 24, -40.5), "chrome", 0.004)
        for y in (12, 29):
            rd.cyl((s * 12.6, y - 1, -40.6), (s * 12.6, y + 1, -40.6), 0.3, "steel_dark", 6)
    rd.box((-3.5, 21, -40.75), (3.5, 23, -40.55), Rd, 0.0)
    rd.box((-1, 18.5, -40.75), (1, 25.5, -40.55), Rd, 0.0)
    rd.finish(parent=root, origin=(0, 9, -40), props={"socket": "door_rear", "part": "amb_rear_door", "category": "Door"})

    add_wheels(root, [("wheel_front", 10, 6, 27, True), ("wheel_rear", 10, 6, -26, True)], "wheel_offroad", 6.5, 5, 3.6, "offroad")
    C.bumper_chrome(root, (0, 5, 34), 13)
    C.bumper_chrome(root, (0, 6, -42), 13, "Bumper_rear", True, "bumper_rear", "bumper_rear_chrome")
    C.emergency_lights(root, (0, 36, 16), 10)
    interior_furniture(body, [("bed", (6, 11.5, -22), 0), ("locker", (-11, 11.5, -28), 90), ("sink", (-11, 11.5, -14), 90),
                              ("shelf", (-12.5, 25, -20), 90), ("lamp", (0, 33.5, -26), 0), ("lamp", (0, 33.5, -8), 0)])
    return root


# ================================================================================================ APC 6x6
def apc():
    H.new_scene()
    root = empty("APC", props={"design": "APC", "voxel": 0.08})
    hull = MB("hull")
    sect = [(-10, 7.5), (10, 7.5), (16.5, 16), (16.5, 26), (12.5, 32.5), (-12.5, 32.5), (-16.5, 26), (-16.5, 16)]
    hull.prism(sect, "z", -46.5, 48.5, "camo", 0.0)
    hull.cut_plane((0, 32.5, 36), (0, 1, 5 / 3), "camo")          # glacis
    hull.cut_plane((0, 7.5, 43.5), (0, -1, 1), "camo")             # nose underside
    hull.cut_plane((0, 32.5, -44), (0, 1, -1.2), "camo")           # rear deck chamfer
    b = MB("Body")
    b.merge(hull)
    # driver's compartment & interior floor
    b.box((-14, 9.5, 14), (14, 10.5, 34), "tread", 0.0)
    # vision blocks band along the glacis
    import math
    gl = MB("Glass")
    a = math.atan2(5, 3)                                            # glacis slope in the (z, y) plane
    for (x0, x1) in ((-11.5, -0.6), (0.6, 11.5)):
        za, zb = 36 + (31 - 23) * 3 / 5 - 0.3, 36 + (31 - 19) * 3 / 5 - 0.3
        gl.quad_strip([(x0, 23, za + 0.15), (x1, 23, za + 0.15)], [(x0, 19, zb + 0.15), (x1, 19, zb + 0.15)], "glass")
        b.quad_strip([(x0 - 0.4, 23.5, za - 0.1), (x1 + 0.4, 23.5, za - 0.1)], [(x0 - 0.4, 23.1, za + 0.4), (x1 + 0.4, 23.1, za + 0.4)], "steel_dark")
    # rivet rows, side skirts, louvres, hatches, lamps
    for s in (-1, 1):
        b.rivets([(s * 16.5, 26, z) for z in range(-44, 35, 3)], (s, 0, 0), 0.3)
        b.rivets([(s * 16.5, 16.5, z) for z in range(-44, 35, 3)], (s, 0, 0), 0.3)
        for z in range(-44, -31, 2):
            b.box((s * 16.5 - 0.3, 22.5, z - 0.35), (s * 16.5 + 0.3, 25.5, z + 0.35), "void", 0.0)
        C.plate_patch(b, "x", s * 16.5, -20, -8, 18, 24, "camo", s)
        b.lamp((s * 12.5, 13.5, 47.6), (0, 0, 1), 1.0, "lightw", "black", 0.6, seg=12)
        for k in range(4):                                         # lamp cage
            b.cyl((s * 12.5 - 1.4 + k * 0.93, 12, 48.6), (s * 12.5 - 1.4 + k * 0.93, 15, 48.6), 0.15, "steel_dark", 4)
        b.box((s * 8.5 - 0.8, 10.5, 47), (s * 8.5 + 0.8, 12.5, 49.6), "steel_bare", 0.01)       # tow eyes
        b.lamp((s * 13.5, 20.5, -46.6), (0, 0, -1), 0.8, "tail", "black", 0.3, square=True)
        b.prism([(-46, 15.5), (-30, 15.5), (-30, 16.5), (-46, 16.5)], "x", min(s * 16.5, s * 17.5), max(s * 16.5, s * 17.5), "steel_dark", 0.01)
    # commander hatch, turret ring (deck), engine deck frame
    b.cyl((-5, 32.4, 24), (-5, 33.4, 24), 3.4, "camo", 20, bev=0.01)
    b.cyl((-5, 33.4, 24), (-5, 33.8, 24), 2.6, "steel_dark", 16)
    b.lathe([(4.5, 0), (6.2, 0), (6.2, 1), (4.5, 1)], (0, 32.4, -4), (0, 33.4, -4), "steel", seg=28)
    b.box((-3.5, 32.4, -30.5), (3.5, 33, -25.5), "steel", 0.01)
    # wheel arches
    for s in (-1, 1):
        for z in (30, 6, -22):
            b.prism(arc_ring(z, 7, 7.6, 8.5, 25, 155, 12), "x", min(s * 12, s * 17.2), max(s * 12, s * 17.2), "steel_dark", 0.01)
    C.cab_interior(b, 10, 30, 24, -4, half=9, seat_mat="canvas")
    body = b.finish(parent=root, origin=(0, 0, 0), props={"part": "body"})
    gl.finish(parent=body, origin=(0, 0, 0))

    hd = MB("Hood")                                                # engine deck
    hd.box((-10.5, 32.4, -45.5), (10.5, 33.2, -29.5), "camo", 0.02)
    for z in range(-44, -31, 2):
        hd.box((-8, 33.2, z - 0.3), (8, 33.5, z + 0.3), "steel_dark", 0.0)
    hd.finish(parent=root, origin=(0, 31, -30), props={"socket": "hood", "part": "apc_deck", "category": "Hood"})

    dr = MB("Door_R")
    dr.box((16.3, 15.5, -6.5), (17.2, 25.5, 6.5), "camo", 0.02)
    dr.cyl((17.2, 21, 0), (17.6, 21, 0), 1.2, "glass", 12)
    dr.cyl((17.2, 21, 0), (17.5, 21, 0), 1.6, "steel_dark", 12)
    dr.box((17.2, 17.5, -4), (17.7, 18.2, -1), "steel_bare", 0.004)
    for y in (17, 24):
        dr.cyl((17.3, y - 1, 6.4), (17.3, y + 1, 6.4), 0.4, "steel_dark", 8)
    dr.rivets([(17.2, y, z) for y in (16.2, 24.8) for z in (-5.5, -2, 2, 5.5)], (1, 0, 0), 0.25)
    door = dr.finish(parent=root, origin=(16, 16, 6), props={"socket": "door", "part": "apc_door", "category": "Door"})
    instance(door, "Door_L", root, (-16, 16, 6), True, {"socket": "door_L", "part": "apc_door", "category": "Door"})

    add_wheels(root, [("wheel_front", 12, 7, 30, True), ("wheel_mid", 12, 7, 6, True), ("wheel_rear", 12, 7, -22, True)])
    C.exhaust_stack(root, (16, 22, -40), top=40)
    C.bumper_plow(root, (0, 6, 49), 15)
    C.smoke_dischargers(root, (17, 17, 0))
    C.machine_gun(root, (0, 34, -4))
    C.searchlight(root, (0, 33, 30))
    C.jerry_rack(root, (0, 33, -18), n=4, half=9)
    C.snorkel(root, (16, 24, 28), top=40)
    C.side_steps(root, (16, 12, -30))
    return root


BUILDERS = {
    "Hauler": (hauler, "Hauler", "VehicleDesigns.Hauler (6-wheel war-rig, walk-in module)", 30),
    "Bus": (bus, "Bus", "VehicleDesigns.Bus (school bus mobile home)", 30),
    "Semi": (semi, "Semi", "VehicleDesigns.SemiTractor (long-nose sleeper, fifth wheel)", 0),
    "Ambulance": (ambulance, "Ambulance", "VehicleDesigns.Ambulance (high-roof van, treatment bay)", 0),
    "APC": (apc, "APC", "VehicleDesigns.Apc (6x6 armoured carrier)", 0),
}


def run(names):
    for n in names:
        fn, label, replaces, cut = BUILDERS[n]
        root = fn()
        extra = []
        if cut:
            extra.append(lambda r=root, c=cut, n=n: H.cutaway_tile(r, n + "_cutaway", c, n + " cutaway"))
        H.save_and_render(root, label, replaces, extra)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    run(argv or list(BUILDERS))
