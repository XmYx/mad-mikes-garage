"""HD heavy lifters (LifterDesigns.cs + PartLibrary.Lifters.cs, 2026-10-13): the MagnetCrane (8x8 cab-over, tread-plate
bed, timber magnet mats, outriggers fore and aft, the lattice-boom magnet crane part) and the CarCrusher (6x6 cab-over,
hydraulic power pack, the crusher bay with its side-hinged press lid, the knuckle-boom grapple part).
Crane parts sit on the "cargo" socket as an empty (part magnet_crane / grapple_crane) whose children are the game's
hinged segments with their origins on the joints (turret > boom; turret > boom > stick > claw > jaw_a / jaw_b), in the
transport pose the voxel parts are authored in. Movables (Outrigger_*, OutriggerLeg_*, CrusherLid) carry their design
pivots; the lid is authored closed (the builder applies the design's rest angle).
Run: blender -b -P lifters.py -- [MagnetCrane CarCrusher]   (HD_PREVIEW=<dir> overrides the tile folder)"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hvlib as H  # noqa: E402
from hvlib import MB, add_wheels, arc_ring, empty, rrect_top  # noqa: E402
import common as C  # noqa: E402

if os.environ.get("HD_PREVIEW"):
    H.OUT = os.environ["HD_PREVIEW"]

Y = "paint_cat"
CAB_Z0, CAB_Z1, CAB_FLOOR, CAB_TOP = 42, 64, 22, 46


# ------------------------------------------------------------------------------------------------ shared truck
def heavy_cab(b, gl, paint):
    """VehicleDesigns.HeavyCab: cab-over glasshouse with split windscreen, door + quarter windows, coachline, grille,
    lamps, striped push bumper, mirrors, beacon bar, air horns, steps, exhaust stack and air intake."""
    xa, xb, za, zb = -15.5, 15.5, CAB_Z0 - 0.5, CAB_Z1 + 0.5
    for s in (-1, 1):                                                       # sides: skirts to y 12, door + quarter windows
        b.wall("x", s * 15, za, zb, 11.5, CAB_TOP - 0.5, [(49.5, 55.5, 30.5, 42.5), (56.5, 62.5, 30.5, 42.5)], 1.0, paint, 0.012)
        gl.box((s * 15 - 0.12, 30.5, 49.5), (s * 15 + 0.12, 42.5, 55.5), "glass", 0.0)
        gl.box((s * 15 - 0.12, 30.5, 56.5), (s * 15 + 0.12, 42.5, 62.5), "glass", 0.0)
        b.box((min(s * 15, s * 15.6), 30.3, 55.6), (max(s * 15, s * 15.6), 42.7, 56.4), "black", 0.004)   # window pillar seal
        for z in (48, 63):                                                  # door seams
            b.seam((s * 15.5, 15, z), (s * 15.5, 45, z), (s, 0, 0))
        b.seam((s * 15.5, 15, 48), (s * 15.5, 15, 63), (s, 0, 0))
        b.box((min(s * 15.5, s * 16.3), 30.6, 57.5), (max(s * 15.5, s * 16.3), 31.4, 60.5), "chrome", 0.004)   # door handle
        b.box((min(s * 15.5, s * 15.7), 28.5, za), (max(s * 15.5, s * 15.7), 29.5, zb), "paint_cream", 0.0)  # coachline
        C.mirror_arm(b, (s * 15.5, 33.5, 63.5), s * 4.0, 3.0)
        for y, z0, z1 in ((16, 49.5, 58.5), (11, 50.5, 57.5)):           # steps
            b.box((min(s * 14.5, s * 17.5), y - 0.5, z0), (max(s * 14.5, s * 17.5), y + 0.5, z1), "tread", 0.006)
        b.box((min(s * 15.5, s * 17.5), 10.5, 49.5), (max(s * 15.5, s * 17.5), 16.5, 50.3), "steel_dark", 0.004)
        b.lamp((s * 13, 22, 65.2), (0, 0, 1), 1.4, "lightw", "chrome", 0.6, square=True)     # headlamps
        b.lamp((s * 14, 24.6, 65.2), (0, 0, 1), 0.55, "amber", "black", 0.3, square=True)
        b.lamp((s * 9, 15, 68.3), (0, 0, 1), 0.7, "lighty", "black", 0.5, seg=10)           # fog lamps
        b.lamp((s * 15, 46.6, 64), (0, 1, 0.3), 0.45, "amber", "black", 0.3, seg=8)         # roof markers
        b.cyl((s * 3, 48, 52), (s * 3, 48, 58), 0.55, "chrome", 10, r2=0.9)                # air horns
        b.cyl((s * 3, 46.5, 54), (s * 3, 48, 54), 0.25, "steel_dark", 6)
        b.box((min(s * 6, s * 7), 12.5, 68.5), (max(s * 6, s * 7), 13.5, 69.6), "steel_bare", 0.004)   # tow eyes
    b.wall("z", CAB_Z1, xa + 1, xb - 1, 11.5, CAB_TOP - 0.5, [(-13.5, -0.5, 30.5, 43.5), (0.5, 13.5, 30.5, 43.5)], 1.0, paint, 0.012)
    for (x0, x1) in ((-13.5, -0.5), (0.5, 13.5)):
        gl.box((x0, 30.5, CAB_Z1 - 0.1), (x1, 43.5, CAB_Z1 + 0.15), "glass", 0.0)
    b.box((-0.5, 30.5, CAB_Z1 + 0.2), (0.5, 43.5, CAB_Z1 + 0.5), "black", 0.004)            # windscreen pillar
    b.box((xa + 1, 28.5, CAB_Z1 + 0.5), (xb - 1, 29.5, CAB_Z1 + 0.7), "paint_cream", 0.0)
    b.wall("z", CAB_Z0, xa + 1, xb - 1, CAB_FLOOR - 0.5, CAB_TOP - 0.5, [(-5.5, 5.5, 33.5, 40.5)], 1.0, paint, 0.012)
    gl.box((-5.5, 33.5, CAB_Z0 - 0.1), (5.5, 40.5, CAB_Z0 + 0.1), "glass", 0.0)
    b.prism(rrect_top(xa, xb, CAB_TOP - 1.5, CAB_TOP + 0.6, 1.4), "z", za, zb, paint, 0.03, 2)   # roof
    b.box((xa + 1, CAB_FLOOR - 0.5, za + 1), (xb - 1, CAB_FLOOR + 0.5, zb - 1), "steel", 0.0)
    b.box((xa + 1.5, CAB_FLOOR + 0.5, za + 1.5), (xb - 1.5, CAB_FLOOR + 0.62, zb - 1.5), "tread", 0.0)
    b.box((xa, 43.5, CAB_Z1 + 0.5), (xb, 45.5, CAB_Z1 + 2.5), paint, 0.02)                 # sun visor
    b.grille(-10.5, 10.5, 14.5, 27.5, CAB_Z1 + 1.4, "chrome", "steel_dark", 14)
    b.box((-11.5, 27.6, CAB_Z1 + 0.5), (11.5, 28.5, CAB_Z1 + 1.6), "chrome", 0.01)
    b.box((-3.5, 16.5, CAB_Z1 + 1.5), (3.5, 18.5, CAB_Z1 + 1.8), "paint_cream", 0.004)       # plate
    b.text("MM", (0, 17.5, CAB_Z1 + 1.8), (0, 0, 1), 1.4, "black", 0.08)
    b.box((-16.5, 11.5, CAB_Z1 + 1.5), (16.5, 15.5, CAB_Z1 + 4.5), "hazard", 0.03, 2)         # push bumper
    b.box((-7.5, 46.5, 57.5), (7.5, 47.5, 61.5), "steel", 0.01)                                # beacon bar
    for x in range(-6, 7, 3):
        b.cyl((x, 47.5, 59.5), (x, 49.2, 59.5), 0.55, "amber", 10, r2=0.4)
    b.cyl((13, 20, 41), (13, 50, 41), 1.1, "chrome", 14)                                        # exhaust stack
    b.cyl((13, 50, 41), (13, 51.5, 40.6), 1.25, "rust", 14)
    b.cyl((-13, 22, 41), (-13, 44, 41), 1.4, "black", 14)                                       # air intake + cap
    b.box((-14.7, 44.5, 39.5), (-11.3, 46.5, 42.5), "steel_dark", 0.02)
    for y in (26, 34):
        for x in (13, -13):
            b.cyl((x, y, 41), (x, y, 42.4), 0.3, "steel_dark", 6)
    C.cab_interior(b, CAB_FLOOR, 58, 52, 4)
    b.box((-1.5, CAB_FLOOR + 0.5, 43.5), (1.5, CAB_FLOOR + 3.5, 56.5), "black", 0.02)         # console over the engine


def heavy_chassis(b, z_rear, bed_front, paint, bed):
    """VehicleDesigns.HeavyChassis: rails, cross members, bed + skirts, fuel tanks, toolboxes, tail lamps, mud flaps,
    hazard under-run bar."""
    for s in (-1, 1):
        b.vbox(s * 6, 8, z_rear, s * 8, 12, 64, "steel_dark", 0.01)
    for z in range(z_rear + 4, 61, 10):
        b.vbox(-6, 9, z, 6, 11, z, "steel_dark", 0.0)
    if bed:
        b.box((-15.5, 14.5, z_rear - 0.5), (15.5, 16.5, bed_front + 0.5), "tread", 0.012)
        for s in (-1, 1):
            b.box((min(s * 14.5, s * 15.6), 12.5, z_rear - 0.5), (max(s * 14.5, s * 15.6), 16.5, bed_front + 0.5), paint, 0.012)
            for z in range(z_rear + 3, bed_front + 1, 8):
                b.lathe([(0.45, 0), (0.45, 1)], (s * 15.6, 15, z - 0.6), (s * 16.4, 15, z - 0.6), "steel_bare", seg=8, cap1=True)
                b.cyl((s * 16.4, 14.4, z - 0.6), (s * 16.4, 15.6, z - 0.6), 0.3, "steel_bare", 6)    # rope hooks
    for s in (-1, 1):
        C.fuel_tank(b, s * 12, 11, 3.5, 22.5, 3.1, "chrome")
        b.box((min(s * 10.5, s * 15.5), 7.5, -22.5), (max(s * 10.5, s * 15.5), 13.5, -11.5), "black", 0.02)   # toolboxes
        b.box((min(s * 15.5, s * 15.9), 10.8, -20.5), (max(s * 15.5, s * 15.9), 11.4, -13.5), "chrome", 0.004)
        b.lamp((s * 13, 14, z_rear - 0.9), (0, 0, -1), 1.2, "tail", "black", 0.4, square=True)
        b.lamp((s * 10.7, 14, z_rear - 0.9), (0, 0, -1), 0.5, "amber", "black", 0.3, square=True)
        C.mud_flap(b, min(s * 10.5, s * 16.5), max(s * 10.5, s * 16.5), 2.5, 12.5, z_rear + 7)
        b.vbox(s * 10, 7, z_rear - 1, s * 10, 12, z_rear, "steel_dark", 0.004)
    b.box((-13.5, 4.5, z_rear - 1.5), (13.5, 7.5, z_rear - 0.5), "hazard", 0.02)                # under-run bar


def outrigger(root, b, z, tag, paint):
    """Outrigger housing across the frame + its two beams and legs as movable objects at the design pivots."""
    b.vbox(-14, 9, z - 2, 14, 13, z + 2, "steel_dark", 0.012)
    for s in (-1, 1):
        b.box((min(s * 14.5, s * 13.5), 8.5, z - 2.5), (max(s * 14.5, s * 13.5), 13.5, z + 2.5), "hazard", 0.012)
        side = tag + ("R" if s > 0 else "L")
        bm = MB("Outrigger_" + side)
        px, py, pz = s * 15, 9, z
        bm.box((min(px - s * 12.5, px + s * 0.5), py - 0.5, pz - 1.5), (max(px - s * 12.5, px + s * 0.5), py + 3.5, pz + 1.5), paint, 0.02, 2)
        bm.box((min(px, px + s * 0.6), py - 0.5, pz - 1.5), (max(px, px + s * 0.6), py + 3.5, pz + 1.5), "hazard", 0.01)
        bm.rivets([(px - s * k, py + 3.5, pz) for k in range(2, 12, 3)], (0, 1, 0), 0.22)
        bm.finish(parent=root, origin=(px, py, pz), props={"movable": "outrigger beam", "rest_euler_x_unity": 0.0})
        lg = MB("OutriggerLeg_" + side)
        px, py = s * 15, 10
        x0, x1 = sorted((px - s * 1.5, px + s * 2.5))
        lg.box((x0, py - 1.5, pz - 2.5), (x1, py + 7.5, pz + 2.5), paint, 0.025, 2)            # sleeve
        lg.box((x0, py + 6.5, pz - 2.5), (x1, py + 7.6, pz + 2.5), "hazard", 0.01)
        cx = px + s * 0.5
        lg.cyl((cx, py - 1.5, pz), (cx, py - 6.5, pz), 0.95, "chrome", 12)                    # ram rod
        lg.cyl((cx, py - 6.5, pz), (cx, py - 7.0, pz), 1.4, "steel_dark", 12)
        p0, p1 = sorted((px - s * 2.5, px + s * 3.5))
        lg.box((p0, py - 8.5, pz - 3.5), (p1, py - 6.8, pz + 3.5), "black", 0.03, 2)              # pad
        lg.finish(parent=root, origin=(px, py, pz), props={"movable": "outrigger leg", "rest_euler_x_unity": 0.0})


def cargo_root(root, part):
    return empty("Crane", root, (0, 17, 26), {"socket": "cargo", "part": part, "category": "Cargo"})


def rivet_line(mb, a, b, step, normal, r=0.22):
    a, b = H.Vector(a), H.Vector(b)
    n = max(abs(c) for c in (b - a))
    pts = [tuple(a.lerp(b, i / n if n else 0)) for i in range(0, int(n) + 1, step)]
    mb.rivets(pts, normal, r)


# ------------------------------------------------------------------------------------------------ magnet crane part
def magnet_crane(root):
    tool = cargo_root(root, "magnet_crane")

    def W(x, y, z):
        return (x, 17 + y, 26 + z)
    t = MB("turret")
    t.lathe([(7.8, 0), (8.9, 0.1), (8.9, 0.9), (7.8, 1.0)], W(0, -0.5, 0), W(0, 1.5, 0), "steel_dark", seg=40, cap0=True, cap1=True)   # slew bearing
    for i in range(0, 40, 2):
        an = i * math.pi / 20
        t.box(W(math.cos(an) * 9 - 0.4, 0.6, math.sin(an) * 9 - 0.4), W(math.cos(an) * 9 + 0.4, 1.4, math.sin(an) * 9 + 0.4), "steel", 0.0)
    t.box(W(-10.5, 1.5, -13.5), W(10.5, 3.5, 13.5), "steel", 0.02)                             # turntable deck
    for s in (-1, 1):
        rivet_line(t, W(s * 10.5, 3.5, -13), W(s * 10.5, 3.5, 13), 3, (0, 1, 0))
    t.box(W(-9.5, 3.5, -7.5), W(9.5, 13.5, 6.5), Y, 0.04, 2)                                    # machinery house
    for s in (-1, 1):
        for y in range(6, 12, 2):
            t.box(W(min(s * 9.5, s * 9.8), y - 0.4, -5.5), W(max(s * 9.5, s * 9.8), y + 0.4, -0.5), "black", 0.004)   # louvres
        t.seam(W(s * 9.5, 5, 1), W(s * 9.5, 12.5, 1), (s, 0, 0))
        t.seam(W(s * 9.5, 12.5, 1), W(s * 9.5, 12.5, 5.5), (s, 0, 0))
        t.box(W(min(s * 9.5, s * 10.3), 7.6, 3.6), W(max(s * 9.5, s * 10.3), 8.4, 4.4), "chrome", 0.004)
        rivet_line(t, W(s * 9.5, 13.5, -7), W(s * 9.5, 13.5, 6), 2, (0, 1, 0))
    t.box(W(-7.5, 4.5, -8.6), W(7.5, 11.5, -7.5), "void", 0.0)
    for y in range(5, 12):                                                                       # radiator grille
        t.box(W(-7.5, y - 0.2, -8.7), W(7.5, y + 0.2, -8.2), "steel_bare", 0.0)
    t.box(W(-10.5, 3.5, 6.5), W(10.5, 12.5, 13.5), "steel", 0.03, 2)                              # cast counterweight
    for y in (6, 9, 12):
        t.seam(W(-10.5, y, 13.5), W(10.5, y, 13.5), (0, 0, 1))
    t.box(W(-10.6, 3.5, 13.4), W(10.6, 5.5, 13.7), "hazard", 0.0)
    for s in (-1, 1):
        t.box(W(min(s * 10.5, s * 10.7), 3.5, 6.5), W(max(s * 10.5, s * 10.7), 5.5, 13.5), "hazard", 0.0)
        t.lathe([(0.6, 0), (0.9, 0.5), (0.6, 1)], W(s * 6, 13, 9.4), W(s * 6, 13, 10.6), "steel_bare", seg=10)   # lifting eye
        t.cyl(W(s * 6, 12.5, 10), W(s * 6, 13.5, 10), 0.4, "steel_bare", 6)
    t.cyl(W(-6, 16, -1), W(6, 16, -1), 3.3, "black", 24)                                          # hoist drum
    for x in range(-5, 6, 2):
        t.cyl(W(x - 0.45, 16, -1), W(x + 0.45, 16, -1), 3.55, "steel_bare", 24)                  # wire wraps
    for s in (-1, 1):
        t.cyl(W(s * 6.6, 16, -1), W(s * 7.4, 16, -1), 4.2, "paint_cat", 24)                      # cheek plates
        t.box(W(min(s * 6.6, s * 7.4), 13.5, -3), W(max(s * 6.6, s * 7.4), 16, 1), "paint_cat", 0.01)
    t.cyl(W(-8.5, 16, -1), W(8.5, 16, -1), 0.9, "chrome", 10)
    for s in (-1, 1):                                                                            # A-frame gantry
        t.cyl(W(s * 8, 14, -6), W(s * 2, 27, 1), 0.8, Y, 10)
        t.cyl(W(s * 8, 14, 7), W(s * 2, 27, 1), 0.8, Y, 10)
    t.box(W(-2.5, 26.5, -0.5), W(2.5, 28.5, 2.5), "paint_cat", 0.02)
    t.cyl(W(-1.5, 28, 1), W(1.5, 28, 1), 1.6, "chrome", 16)
    t.cyl(W(6, 13.5, 4), W(6, 21.5, 4), 0.8, "black", 10)                                         # exhaust
    t.cyl(W(6, 21.5, 4), W(6, 22.6, 3.6), 0.95, "rust", 10)
    t.cyl(W(-7, 13.5, 5), W(-7, 14.5, 5), 0.6, "steel", 8)                                        # beacon
    t.cyl(W(-7, 14.5, 5), W(-7, 16.3, 5), 0.7, "amber", 10, r2=0.45)
    for s in (-1, 1):                                                                            # hand rails
        for z in (-6, 0, 6):
            t.cyl(W(s * 9, 13.5, z), W(s * 9, 15.2, z), 0.22, "hazard", 6)
        t.cyl(W(s * 9, 15.2, -6), W(s * 9, 15.2, 6), 0.3, "hazard", 6)
    for z in (3, 5):
        t.cyl(W(-11, 4, z), W(-11, 14.5, z), 0.3, "steel", 6)
    for y in range(4, 14, 2):
        t.cyl(W(-11, y, 3), W(-11, y, 5), 0.24, "steel", 6)
    t.box(W(5.5, 3.5, -12.5), W(10.5, 9.5, -8.5), "black", 0.03)                                  # rectifier cabinet
    t.lamp(W(8, 8, -12.7), (0, 0, -1), 0.4, "lighty", "black", 0.2, seg=8)
    t.lamp(W(9.2, 8, -12.7), (0, 0, -1), 0.3, "tail", "black", 0.2, seg=8)
    t.hose(W(8, 9.5, -10), W(2, 15, -2), 0.5, "hose", 1.0, 6)                                     # power cable to the drum
    for s in (-1, 1):                                                                            # boom foot brackets
        t.box(W(min(s * 5.5, s * 7.5), 3.5, -13.5), W(max(s * 5.5, s * 7.5), 12.5, -6.5), "paint_cat", 0.02)
        t.box(W(min(s * 2.5, s * 4.5), 2.5, -13.5), W(max(s * 2.5, s * 4.5), 5.5, -10.5), "paint_cat", 0.01)
    t.cyl(W(-8.3, 10, -10), W(8.3, 10, -10), 1.3, "chrome", 12)
    turret = t.finish(parent=tool, origin=W(0, 0, 0), props={"segment": "turret", "pivot_vox": [0, 0, 0]})

    bm = MB("boom")
    foot, head = -17, -86

    def chord(sx, sy, z):
        f = (z - foot) / (head - foot)
        return W(sx * (5 + (3 - 5) * f), 10 + sy * (4 + (3 - 4) * f), z)
    bm.box(W(-5.5, 5.5, foot - 0.5), W(5.5, 14.5, -7.5), Y, 0.04, 2)                               # box-section foot
    bm.cyl(W(-5.5, 10, -10), W(5.5, 10, -10), 2.4, "paint_cat", 18)
    for s in (-1, 1):
        bm.box(W(min(s * 5.5, s * 5.7), 8.5, -15.5), W(max(s * 5.5, s * 5.7), 11.5, -10.5), "paint_cream", 0.004)   # load chart
        rivet_line(bm, W(s * 5, 14.5, foot), W(s * 5, 14.5, -8), 2, (0, 1, 0))
    for sx in (-1, 1):
        for sy in (-1, 1):
            bm.cyl(chord(sx, sy, foot), chord(sx, sy, head), 0.9, Y, 10)                             # four chords
    flip = False
    z = foot
    while z > head:
        z2 = max(head, z - 6)
        for s in (-1, 1):
            bm.cyl(chord(s, 1 if flip else -1, z), chord(s, -1 if flip else 1, z2), 0.45, "paint_cat", 6)   # lacing
            bm.cyl(chord(1 if flip else -1, s, z), chord(-1 if flip else 1, s, z2), 0.45, "paint_cat", 6)
            bm.cyl(chord(s, -1, z), chord(s, 1, z), 0.45, "paint_cat", 6)                           # battens
            bm.cyl(chord(-1, s, z), chord(1, s, z), 0.45, "paint_cat", 6)
        z -= 6
        flip = not flip
    for s in (-1, 1):
        bm.box(W(min(s * 2.5, s * 3.5), 1.5, -41.5), W(max(s * 2.5, s * 3.5), 5.5, -38.5), "paint_cat", 0.01)   # ram lugs
    bm.box(W(-3.5, 6.5, -94.5), W(3.5, 13.5, head + 0.5), Y, 0.03, 2)                               # head
    for s in (-1, 1):
        bm.box(W(min(s * 3.5, s * 3.8), 6.5, -94.5), W(max(s * 3.5, s * 3.8), 13.5, -87.5), "hazard", 0.006)
    bm.cyl(W(-2, 8, -92), W(2, 8, -92), 3.4, "black", 24)                                           # twin sheaves
    bm.cyl(W(-2.1, 8, -92), W(2.1, 8, -92), 2.2, "steel_bare", 18)
    bm.cyl(W(-4.2, 8, -92), W(4.2, 8, -92), 0.8, "chrome", 10)
    bm.box(W(-0.5, 13.5, -88.5), W(0.5, 15.5, -85.5), "steel_bare", 0.01)                           # pendant lug
    for x in (-1, 1):
        bm.lamp(W(x, 13, -95.2), (0, 0, -1), 0.45, "lightw", "black", 0.3, seg=8)
    bm.cyl(W(0, 13.5, -94), W(0, 14.8, -94), 0.45, "tail", 8)                                         # warning lamp
    bm.finish(parent=turret, origin=W(0, 10, -10), props={"segment": "boom", "pivot_vox": [0, 10, -10]})
    return tool


# ------------------------------------------------------------------------------------------------ grapple part
def grapple_crane(root):
    tool = cargo_root(root, "grapple_crane")

    def W(x, y, z):
        return (x, 17 + y, 26 + z)
    t = MB("turret")
    t.lathe([(6.0, 0), (7.1, 0.1), (7.1, 0.9), (6.0, 1.0)], W(0, -0.5, 0), W(0, 1.5, 0), "steel_dark", seg=36, cap0=True, cap1=True)
    for i in range(0, 32, 2):
        an = i * math.pi / 16
        t.box(W(math.cos(an) * 7 - 0.4, 0.6, math.sin(an) * 7 - 0.4), W(math.cos(an) * 7 + 0.4, 1.4, math.sin(an) * 7 + 0.4), "steel", 0.0)
    t.cyl(W(0, 1.5, 0), W(0, 14.5, 0), 4.9, Y, 24)                                                  # pedestal column
    for y in (3, 13):
        t.cyl(W(0, y - 0.5, 0), W(0, y + 0.5, 0), 5.4, "steel", 24)
    for i in range(12):
        an = i * math.pi / 6
        t.cyl(W(math.cos(an) * 5, 3.5, math.sin(an) * 5), W(math.cos(an) * 5, 4.0, math.sin(an) * 5), 0.3, "chrome", 6)
    t.box(W(-6.5, 14.5, -6.5), W(6.5, 22.5, 8.5), Y, 0.04, 2)                                       # upper house
    for y in range(16, 21, 2):
        t.box(W(-6.8, y - 0.4, -0.5), W(-6.5, y + 0.4, 6.5), "black", 0.004)
    t.box(W(-6.5, 13.5, 8.5), W(6.5, 22.5, 13.5), "steel", 0.03, 2)                                 # counterweight
    t.box(W(-6.6, 13.5, 13.4), W(6.6, 15.5, 13.7), "hazard", 0.0)
    for y in (17, 21):
        t.seam(W(-6.5, y, 13.5), W(6.5, y, 13.5), (0, 0, 1))
    rivet_line(t, W(-6.5, 22.5, -6), W(-6.5, 22.5, 13), 2, (0, 1, 0))
    # elevated operator's cab on the right: posts, glass on three sides, roof with visor
    xa, xb, za, zb = 6.5, 12.5, -3.5, 7.5
    t.box((xa, 17 + 14.5, 26 + za), (xb, 17 + 18.5, 26 + zb), Y, 0.02)                              # lower panels
    t.box((xa, 17 + 25.5, 26 + za), (xb, 17 + 26.5, 26 + zb), "steel", 0.01)
    for x in (xa, xb - 1):
        for z in (za, zb - 1):
            t.box((x, 17 + 18.5, 26 + z), (x + 1, 17 + 25.5, 26 + z + 1), Y, 0.01)
    t.box((xa, 17 + 18.5, 26 + za + 1), (xa + 1, 17 + 25.5, 26 + zb - 1), Y, 0.01)                   # inner wall (house side)
    gt = MB("grapple_glass")
    gt.box((xb - 0.6, 17 + 18.5, 26 + za + 1), (xb - 0.4, 17 + 25.5, 26 + zb - 1), "glass", 0.0)
    gt.box((xa + 1, 17 + 18.5, 26 + za + 0.4), (xb - 1, 17 + 25.5, 26 + za + 0.6), "glass", 0.0)
    gt.box((xa + 1, 17 + 18.5, 26 + zb - 0.6), (xb - 1, 17 + 25.5, 26 + zb - 0.4), "glass", 0.0)
    t.merge(gt)
    t.box(W(5.5, 26.5, -4.5), W(13.5, 27.5, 8.5), "steel", 0.02)                                     # roof with visor
    t.box(W(7.5, 24.5, -5.5), W(11.5, 25.5, -3.5), "steel_dark", 0.01)                               # wiper housing
    t.box(W(8.5, 15.5, 2.5), W(11.5, 18.5, 5.5), "fabric", 0.04, 2)                                  # seat
    t.box(W(8.5, 18.5, 4.5), W(11.5, 22.5, 5.5), "fabric", 0.04, 2)
    for x in (8, 11):
        t.cyl(W(x, 17.5, 0), W(x, 20.5, 0), 0.3, "black", 6)
        t.ball(W(x, 21, 0), 0.5, "tail", 8, 5)
    t.lamp(W(12, 26, -4.2), (0, 0, -1), 0.5, "lightw", "black", 0.3, square=True)
    t.cyl(W(10, 27.5, 6), W(10, 29.2, 6), 0.6, "amber", 10, r2=0.4)
    for z in (6, 8):
        t.cyl(W(7, 1.5, z), W(7, 15.5, z), 0.3, "steel", 6)
    for y in range(2, 15, 2):
        t.cyl(W(7, y, 6), W(7, y, 8), 0.24, "steel", 6)
    t.box(W(-11.5, 14.5, -2.5), W(-6.5, 21.5, 6.5), "steel", 0.03, 2)                                # oil tank
    t.cyl(W(-9, 21.5, 2), W(-9, 22.5, 2), 0.6, "chrome", 8)
    t.box(W(-11.7, 16.5, -2.5), W(-11.5, 20.5, -1.5), "lens", 0.0)                                   # sight glass
    t.hose(W(-7, 20, -2), W(-2, 25, -7), 0.5, "hose", 0.6, 6)
    t.hose(W(-7, 18, -1), W(2, 24, -7), 0.5, "hose", 0.6, 6)
    t.cyl(W(-4, 22.5, 11), W(-4, 29.5, 11), 0.7, "black", 10)                                        # exhaust
    t.cyl(W(-4, 29.5, 11), W(-4, 30.5, 10.6), 0.85, "rust", 10)
    for s in (-1, 1):                                                                              # boom brackets, ram lugs
        t.box(W(min(s * 2.5, s * 4.5), 21.5, -8.5), W(max(s * 2.5, s * 4.5), 29.5, -0.5), "paint_cat", 0.02)
        t.box(W(min(s * 1.5, s * 2.5), 15.5, -8.5), W(max(s * 1.5, s * 2.5), 18.5, -5.5), "paint_cat", 0.01)
    t.cyl(W(-5.5, 26, -4), W(5.5, 26, -4), 1.2, "chrome", 10)
    turret = t.finish(parent=tool, origin=W(0, 0, 0), props={"segment": "turret", "pivot_vox": [0, 0, 0]})

    bo = MB("boom")
    prof = []
    for z in (-2.5, -52.5):
        f = (z + 2) / (-50)
        h = 3.4 + (2.0 - 3.4) * max(0.0, min(1.0, f))
        prof.append((z, h))
    bo.prism([(26 + prof[0][0], 17 + 26 - prof[0][1] - 0.5), (26 + prof[1][0], 17 + 26 - prof[1][1] - 0.5),
              (26 + prof[1][0], 17 + 26 + prof[1][1] + 0.5), (26 + prof[0][0], 17 + 26 + prof[0][1] + 0.5)], "x", -2.5, 2.5, Y, 0.04, 2)
    bo.cyl(W(-2.6, 26, -4), W(2.6, 26, -4), 2.8, "paint_cat", 18)
    for zc in (-16, -28, -40):                                                                     # lightening holes
        for s in (-1, 1):
            bo.cyl(W(s * 2.45, 26, zc), W(s * 2.65, 26, zc), 1.6, "void", 14)
    for s in (-1, 1):
        rivet_line(bo, W(s * 2.5, 29, -4), W(s * 2.5, 28, -50), 3, (s, 0, 0))
        bo.hose(W(s, 30, -6), W(s, 29.5, -50), 0.45, "hose", -0.6, 8)
        bo.box(W(min(s * 2.5, s * 2.8), 20.5, -25.5), W(max(s * 2.5, s * 2.8), 23.5, -22.5), "paint_cat", 0.01)
    bo.box(W(-3.5, 22.5, -57.5), W(3.5, 29.5, -50.5), "paint_cat", 0.03)                             # tip clevis
    for s in (-1, 1):
        bo.box(W(min(s * 3.5, s * 3.8), 22.5, -57.5), W(max(s * 3.5, s * 3.8), 29.5, -51.5), "hazard", 0.004)
    bo.cyl(W(-4.3, 26, -54), W(4.3, 26, -54), 1.2, "chrome", 10)
    bo.lamp(W(0, 23, -58.2), (0, 0, -1), 0.45, "lightw", "black", 0.3, seg=8)
    boom = bo.finish(parent=turret, origin=W(0, 26, -4), props={"segment": "boom", "pivot_vox": [0, 26, -4]})

    st = MB("stick")
    st.box(W(-2.5, 1.5, -56.5), W(2.5, 25.5, -51.5), Y, 0.03, 2)
    st.cyl(W(-2.6, 26, -54), W(2.6, 26, -54), 2.2, "paint_cat", 16)
    st.box(W(-2.7, 4.5, -56.7), W(2.7, 7.5, -51.3), "hazard", 0.004)
    for s in (-1, 1):
        rivet_line(st, W(s * 2.5, 24, -56), W(s * 2.5, 3, -56), 3, (s, 0, 0))
        st.cyl(W(s, 24, -51.2), W(s, 3, -51.2), 0.45, "hose", 8)
    st.box(W(-1.5, -0.5, -55.5), W(1.5, 1.5, -52.5), "steel", 0.01)
    stick = st.finish(parent=boom, origin=W(0, 26, -54), props={"segment": "stick", "pivot_vox": [0, 26, -54]})

    cl = MB("claw")
    cl.box(W(-1.5, -3.5, -55.5), W(1.5, -0.5, -52.5), "black", 0.01)                                 # rotator link
    cl.cyl(W(0, -5.5, -54), W(0, -3.5, -54), 2.4, "steel", 18)                                      # rotator motor
    cl.cyl(W(0, -7.5, -54), W(0, -5.5, -54), 4.6, Y, 24)                                             # head
    cl.lathe([(4.65, 0), (4.65, 1)], W(0, -6.4, -54), W(0, -5.8, -54), "hazard", seg=24)
    for s in (-1, 1):
        cl.box(W(min(s * 1.5, s * 3.5), -5.5, -55.5), W(max(s * 1.5, s * 3.5), -2.5, -52.5), "black", 0.01)   # jaw rams
        cl.cyl(W(s * 2, -2.5, -54), W(s * 2, -1.5, -54), 0.4, "chrome", 6)
        cl.cyl(W(s * 3.5, -7, -57.5), W(s * 3.5, -7, -50.5), 1.2, "chrome", 10)                         # hinge pins
    claw = cl.finish(parent=stick, origin=W(0, 0, -54), props={"segment": "claw", "pivot_vox": [0, 0, -54]})

    for s, name in ((-1, "jaw_a"), (1, "jaw_b")):
        jw = MB(name)
        for z in (-57, -51):
            jw.cyl(W(s * 3, -7, z), W(s * 7, -11, z), 0.85, "steel", 10)
            jw.ball(W(s * 7, -11, z), 0.86, "steel", 10, 6)
            jw.cyl(W(s * 7, -11, z), W(s * 6, -15, z), 0.8, "steel", 10)
            jw.ball(W(s * 6, -15, z), 0.81, "steel", 10, 6)
            jw.cyl(W(s * 6, -15, z), W(s * 2, -17.3, z), 0.75, "steel", 10, r2=0.45)
            jw.box(W(min(s * 1, s * 2.5), -18.5, z - 0.6), W(max(s * 1, s * 2.5), -17, z + 0.6), "steel_bare", 0.01)   # hardened tips
        jw.cyl(W(s * 7, -11, -57), W(s * 7, -11, -51), 0.7, "steel_dark", 10)
        jw.cyl(W(s * 5, -9, -56), W(s * 5, -9, -52), 0.5, "steel_dark", 8)
        jw.finish(parent=claw, origin=W(s * 3, -7, -54), props={"segment": name, "pivot_vox": [s * 3, -7, -54]})
    return tool


# ================================================================================================ MAGNET CRANE TRUCK
def magnet_crane_truck():
    H.new_scene()
    root = empty("MagnetCrane", props={"design": "MagnetCrane", "voxel": 0.08})
    b, gl = MB("Body"), MB("Glass")
    paint = "paint_navy"
    heavy_chassis(b, -70, 41, paint, True)
    heavy_cab(b, gl, paint)
    for z in range(-68, -55, 3):                                                                    # timber magnet mats
        b.box((-9.5, 16.5, z - 0.5), (9.5, 17.5, z + 1.5), "wood", 0.02)
        for x in (-8, 8):
            b.cyl((x, 17.5, z + 0.5), (x, 17.7, z + 0.5), 0.25, "steel_dark", 6)
    b.lathe([(3.2, 0), (4.6, 0.05), (5.4, 0.5), (4.6, 0.95), (3.2, 1)], (0, 17.5, -62), (0, 19.5, -62), "tyre", seg=28)   # scrap tyre buffer
    b.box((8.5, 16.5, 7.5), (14.5, 20.5, 16.5), "steel_dark", 0.02)                                 # chain basket
    for i in range(6):
        b.lathe([(0.35, 0), (0.55, 0.5), (0.35, 1)], (9.5 + i * 0.8, 20.6, 9 + (i % 3)), (9.5 + i * 0.8 + 0.5, 21.2, 9.4 + (i % 3)), "steel_bare", seg=8)
    b.box((-14.5, 16.5, 9.5), (-8.5, 22.5, 16.5), "paint_olive", 0.03)                              # site hut box
    b.cyl((-11, 22.5, 13), (-11, 23.6, 13), 0.5, "amber", 8)
    for s in (-1, 1):                                                                              # warning plates
        b.box((min(s * 15.6, s * 16.2), 12.5, -40.5), (max(s * 15.6, s * 16.2), 15.5, -29.5), "hazard_red", 0.004)
    outrigger(root, b, 27, "F", "paint_cat")
    outrigger(root, b, -66, "B", "paint_cat")
    body = b.finish(parent=root, origin=(0, 0, 0), props={"part": "body"})
    gl.finish(parent=body, origin=(0, 0, 0))
    add_wheels(root, [("wheel_front", 11, 7, 54, True), ("wheel_front2", 11, 7, 38, True), ("wheel_mid", 11, 7, -42, True), ("wheel_rear", 11, 7, -56, True)])
    magnet_crane(root)
    return root


# ================================================================================================ CAR CRUSHER
def car_crusher():
    H.new_scene()
    root = empty("CarCrusher", props={"design": "CarCrusher", "voxel": 0.08})
    b, gl = MB("Body"), MB("Glass")
    paint = "paint_riggreen"
    heavy_chassis(b, -78, 41, paint, False)
    heavy_cab(b, gl, paint)
    b.box((-15.5, 14.5, -14.5), (15.5, 16.5, 41.5), "tread", 0.012)                                  # front bed under the turret
    for s in (-1, 1):
        b.box((min(s * 14.5, s * 15.6), 12.5, -14.5), (max(s * 14.5, s * 15.6), 16.5, 41.5), paint, 0.012)
    # hydraulic power pack beside the turret: vents, gauges, filter, tank; battery box opposite
    b.box((-15.5, 16.5, -10.5), (-8.5, 25.5, 8.5), paint, 0.03, 2)
    for z in range(-6, 5, 2):
        b.box((-15.8, 21.5, z - 0.4), (-15.5, 24.5, z + 0.4), "black", 0.0)
    for z in (-2, 1):
        b.cyl((-15.5, 21, z), (-16.1, 21, z), 0.7, "lens", 12)
        b.cyl((-15.5, 21, z), (-15.9, 21, z), 0.85, "chrome", 12)
    b.cyl((-12, 25.5, 6), (-12, 28.5, 6), 1.2, "chrome", 12)
    b.box((9.5, 16.5, -10.5), (15.5, 21.5, 4.5), "black", 0.03)
    b.hose((-9, 22, -8), (-1, 17.5, -2), 0.4, "hose", 0.8, 6)
    b.hose((-9, 20, -9), (1, 17.5, -3), 0.4, "hose", 0.8, 6)
    # ---- crusher bay over the rear tandem
    b.box((-16.5, 12.5, -77.5), (16.5, 16.5, -14.5), "steel_dark", 0.02)                            # floor + bearers
    b.box((-14.5, 16.5, -76.5), (14.5, 16.7, -18.5), "rust", 0.0)                                   # scarred floor plate
    for z in range(-74, -19, 4):
        b.box((-14.5, 16.6, z - 0.3), (14.5, 16.8, z + 0.3), "steel_dark", 0.0)
    for s in (-1, 1):
        x0, x1 = sorted((s * 14.5, s * 16.5))
        b.box((x0, 16.5, -77.5), (x1, 30.5, -14.5), "steel", 0.02, 2)                               # side walls
        for z in range(-74, -17, 7):
            b.box((min(s * 16.5, s * 17.5), 16.5, z - 0.5), (max(s * 16.5, s * 17.5), 30.5, z + 0.5), "steel_dark", 0.008)   # outside ribs
        b.box((min(s * 16.5, s * 17.5), 29.5, -77.5), (max(s * 16.5, s * 17.5), 30.5, -14.5), "steel_dark", 0.008)
        b.box((x0 - 0.1, 30.5, -77.5), (x1 + 0.1, 31.5, -14.5), "hazard", 0.006)                      # hazard capping
        for zz in range(-70, -20, 9):                                                                # dents and scrapes inside
            b.box((x0 + (0.0 if s > 0 else 1.9), 18 + (zz % 5), zz), (x1 - (1.9 if s > 0 else 0.0), 18.6 + (zz % 5), zz + 3), "steel_bare", 0.0)
    b.box((-16.5, 16.5, -18.5), (16.5, 35.5, -14.5), "steel", 0.03, 2)                               # bulkhead
    b.box((-16.6, 32.5, -18.7), (16.6, 35.5, -18.4), "hazard", 0.0)
    b.box((-16.5, 16.5, -77.5), (16.5, 29.5, -75.5), "steel", 0.02)                                  # tail gate
    for x in range(-12, 13, 6):
        b.box((x - 0.5, 16.5, -78.5), (x + 0.5, 29.5, -77.5), "steel_dark", 0.006)
    b.box((-12.5, 24.5, -78.5), (12.5, 25.5, -77.5), "steel_dark", 0.006)
    b.box((-17.5, 29.5, -76.5), (-15.5, 31.5, -17.5), "steel_bare", 0.01)                             # hinge rail
    for z in range(-72, -21, 10):
        b.cyl((-16, 31, z - 0.5), (-16, 31, z + 2.5), 1.3, "chrome", 12)
    for z0 in (-74, -24):                                                                            # lid rests
        b.box((13.5, 30.5, z0 - 0.5), (16.5, 32.5, z0 + 4.5), "steel_dark", 0.01)
    b.cyl((-10, 15, -77), (-10, 15, -81), 0.7, "chrome", 8)                                            # drain spout + stain
    b.box((-11.5, 13.6, -82.5), (-8.5, 13.8, -80.5), "oil", 0.0)
    for s in (-1, 1):                                                                                # DANGER plates
        b.box((min(s * 17.5, s * 18.6), 21.5, -50.5), (max(s * 17.5, s * 18.6), 27.5, -41.5), "paint_crimson", 0.01)
        b.text("DANGER", (s * 18.65, 24.5, -46), (s, 0, 0), 1.4, "paint_cream", 0.08)
    b.box((15.5, 17.5, -80.5), (18.5, 26.5, -78.5), "black", 0.02)                                    # rear control station
    b.box((16.5, 23.5, -81.5), (17.5, 26.5, -80.5), "steel_bare", 0.004)
    b.cyl((16, 24.5, -80.6), (16, 24.5, -81.2), 0.45, "tail", 8)
    b.cyl((18, 26.5, -80), (18, 27.8, -80), 0.5, "amber", 8)
    outrigger(root, b, 31, "F", "paint_cat")
    outrigger(root, b, -75, "B", "paint_cat")
    body = b.finish(parent=root, origin=(0, 0, 0), props={"part": "body"})
    gl.finish(parent=body, origin=(0, 0, 0))
    # press lid (authored closed, hinged on the left wall top at (-16, 31, -47); the design swings it open)
    lid = MB("CrusherLid")
    hx, hy, hz = -16, 31, -47

    def Lp(x, y, z):
        return (hx + x, hy + y, hz + z)
    lid.box(Lp(-0.5, -0.5, -29.5), Lp(32.5, 2.5, 29.5), "steel", 0.03, 2)
    for x in range(3, 31, 6):                                                                        # stiffeners
        lid.box(Lp(x - 0.5, 2.5, -28.5), Lp(x + 0.5, 4.5, 28.5), "steel_dark", 0.01)
    for z in range(-24, 25, 12):
        lid.box(Lp(0.5, 2.5, z - 0.5), Lp(31.5, 3.5, z + 0.5), "steel_dark", 0.008)
    lid.box(Lp(29.5, -0.6, -29.6), Lp(32.6, 3.5, 29.6), "hazard", 0.01)                               # free edge
    lid.box(Lp(-3.5, 0.5, -2.5), Lp(-0.5, 4.5, 2.5), "steel_bare", 0.01)                              # ram lever
    lid.cyl(Lp(0, 1, -29.5), Lp(0, 1, 29.5), 1.4, "chrome", 14)                                       # hinge tube
    for z in range(-26, 27, 4):
        lid.box(Lp(30.5, -1.0, z - 0.5), Lp(31.5, -0.4, z + 0.5), "steel_bare", 0.004)                  # wear teeth
    lid.rivets([Lp(x, 2.6, z) for x in (1.5, 16, 30) for z in (-27, 0, 27)], (0, 1, 0), 0.25)
    lid.finish(parent=root, origin=(hx, hy, hz), props={"movable": "press lid", "rest_euler_x_unity": 0.0, "hinge": "left wall top (local +Z)"})
    add_wheels(root, [("wheel_front", 11, 7, 52, True), ("wheel_mid", 11, 7, -44, True), ("wheel_rear", 11, 7, -60, True)])
    grapple_crane(root)
    return root


BUILDERS = {
    "MagnetCrane": (magnet_crane_truck, "MagnetCrane", "LifterDesigns.MagnetCrane (8x8 lattice-boom magnet crane) + part magnet_crane"),
    "CarCrusher": (car_crusher, "CarCrusher", "LifterDesigns.CarCrusher (6x6 grapple + crusher bay, press lid) + part grapple_crane"),
}


def run(names):
    for n in names:
        fn, label, replaces = BUILDERS[n]
        root = fn()
        H.save_and_render(root, label, replaces)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    run(argv or list(BUILDERS))
