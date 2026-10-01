"""HD trailers: Tanker, TankerSmall, BoxTrailer, CargoTrailer, CarTrailer, CarTrailerDouble.
Run: blender -b -P trailers.py -- [names]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hvlib as H  # noqa: E402
from hvlib import MB, add_wheels, arc_ring, empty  # noqa: E402
import common as C  # noqa: E402


def tank_shell(b, cy, r, z0, z1, mat, band_every=8, band_mat="rust", dish=2.5, seg=40):
    """Cylindrical tank along z with dished end caps and raised rolling hoops."""
    L = z1 - z0
    prof = [(0.0, 0.0), (r * 0.55, dish * 0.35 / L), (r * 0.85, dish * 0.75 / L), (r, dish / L),
            (r, 1 - dish / L), (r * 0.85, 1 - dish * 0.75 / L), (r * 0.55, 1 - dish * 0.35 / L), (0.0, 1.0)]
    b.lathe(prof, (0, cy, z0), (0, cy, z1), mat, seg=seg)
    z = z0 + dish + band_every / 2
    while z < z1 - dish:
        b.lathe([(r, 0), (r + 0.3, 0.2), (r + 0.3, 0.8), (r, 1)], (0, cy, z - 0.45), (0, cy, z + 0.45), band_mat, seg=seg)
        z += band_every


def placard(b, x, y, z, nx, size=1.6, text="3"):
    """Hazard diamond on a side (normal along x)."""
    s = 1 if nx > 0 else -1
    pts = [(z, y - size), (z + size, y), (z, y + size), (z - size, y)]
    b.prism(pts, "x", min(x, x + s * 0.15), max(x, x + s * 0.15), "red_cross", 0.0)
    b.text(text, (x + s * 0.15, y - 0.2, z), (s, 0, 0), size * 0.9, "white_enamel", 0.05)


def legs_pair(parent, xs, z0, z1, y_top, foot=1.5, name="Legs", brace=True):
    """Semi-trailer landing legs (Body/Legs): square outer tubes, inner legs, sand shoes, crank box."""
    mb = MB(name)
    for x in xs:
        mb.box((x - 1, 6, z0), (x + 1, y_top, z1), "steel_dark", 0.01)
        mb.box((x - 0.7, 0.8, z0 + 0.3), (x + 0.7, 6, z1 - 0.3), "steel_bare", 0.005)
        mb.box((x - foot, -0.5, z0 - foot + 1), (x + foot, 0.8, z1 + foot - 1), "steel_dark", 0.01)
    if brace:
        mb.cyl((xs[0], 9, (z0 + z1) / 2), (xs[-1], 9, (z0 + z1) / 2), 0.45, "steel", 8)
        mb.cyl((xs[0], 14, (z0 + z1) / 2), (xs[-1], 9, (z0 + z1) / 2), 0.35, "steel", 6)
    x = xs[-1]
    mb.box((x + 1, y_top - 5, z0), (x + 2.6, y_top - 2, z1), "steel", 0.01)              # crank box
    mb.cyl((x + 2.6, y_top - 3.5, (z0 + z1) / 2), (x + 3.6, y_top - 3.5, (z0 + z1) / 2), 0.25, "steel_bare", 6)
    mb.cyl((x + 3.6, y_top - 3.5, (z0 + z1) / 2), (x + 3.6, y_top - 7.5, (z0 + z1) / 2), 0.25, "steel_bare", 6)
    return mb.finish(parent=parent, origin=(0, 0, 0), props={"label": "legs", "note": "Body/Legs: wound up by TowCoupling"})


def jockey_leg(parent, z, top, name="Legs"):
    mb = MB(name)
    mb.cyl((0, 2, z), (0, top, z), 0.9, "steel_dark", 12)
    mb.cyl((0, 0.8, z), (0, 2, z), 0.6, "steel_bare", 10)
    mb.lathe([(0.6, 0), (2.2, 0.0), (2.2, 1.0), (0.6, 1.0)], (-1.0, 0.0, z), (1.0, 0.0, z), "rubber", seg=16)   # jockey wheel
    mb.cyl((0, top, z), (0, top + 1.5, z), 0.4, "steel_bare", 6)
    mb.cyl((0, top + 1.5, z), (2, top + 1.5, z), 0.3, "steel_bare", 6)
    return mb.finish(parent=parent, origin=(0, 0, 0), props={"label": "legs", "note": "Body/Legs: wound up by TowCoupling"})


def drawbar(b, y, z0, z1, half=6, r=0.9):
    """A-frame drawbar ending in a towing eye at z1 (the coupler)."""
    for s in (-1, 1):
        b.cyl((s * half, y, z0), (0, y, z1 - 2), r, "steel_dark", 10)
    b.cyl((0, y, z1 - 2.5), (0, y, z1 - 0.8), r * 1.2, "steel_dark", 10)
    b.lathe([(0.5, 0), (1.3, 0), (1.3, 1), (0.5, 1)], (-0.5, y, z1), (0.5, y, z1), "steel_bare", seg=16, cap0=False)
    b.cyl((0, y, z1 - 1.3), (0, y, z1 - 0.6), 0.5, "steel_bare", 8)
    b.hose((1.5, y + 0.5, z0 + 1), (1, y + 0.6, z1 - 1.5), 0.3, "hose", 1.2)


# ================================================================================================ TANKER
def tanker():
    H.new_scene()
    root = empty("Tanker", props={"design": "Tanker", "voxel": 0.08, "coupler": [0, 11, 34]})
    b = MB("Body")
    for s in (-1, 1):
        b.vbox(s * 6, 9, -54, s * 8, 12, 22, "steel_dark", 0.01)
    for z in range(-50, 20, 8):
        b.vbox(-6, 10, z, 6, 11, z, "steel_dark", 0.0)
    tank_shell(b, 25, 12, -50.5, 18.5, "paint_black", 8, "rust")
    for z in (-44, -24, -4, 14):                                            # saddles
        b.prism(arc_ring(0, 25, 11.8, 12.4, 200, 340, 10), "z", z - 0.6, z + 0.6, "steel_dark", 0.0)
        b.box((-8, 12.5, z - 0.6), (8, 14.5, z + 0.6), "steel_dark", 0.0)
    # catwalk with railings, hatches, vents
    b.box((-3.5, 36.6, -40.5), (3.5, 37.2, 8.5), "grating", 0.005)
    for s in (-1, 1):
        for z in range(-40, 9, 8):
            b.cyl((s * 3.3, 37.2, z), (s * 3.3, 42, z), 0.25, "steel", 6)
        b.cyl((s * 3.3, 42, -40), (s * 3.3, 42, 8), 0.3, "steel", 6)
        b.cyl((s * 3.3, 39.6, -40), (s * 3.3, 39.6, 8), 0.2, "steel", 6)
    for z in (-16, 4):
        b.cyl((0, 36.5, z), (0, 38.2, z), 2.2, "steel", 20)
        b.cyl((0, 38.2, z), (0, 38.7, z), 2.4, "steel_bare", 20)
        for k in range(6):
            a = 2 * math.pi * k / 6
            b.cyl((math.cos(a) * 2.1, 38.6, z + math.sin(a) * 2.1), (math.cos(a) * 2.1, 39.2, z + math.sin(a) * 2.1), 0.25, "steel_dark", 6)
    b.cyl((4.5, 36, -30), (4.5, 38.5, -30), 0.6, "steel_bare", 8)
    # ladder at the rear (left) + rear manifold: valves, hose tube, pump
    b.ladder(-13, -11, -51.6, 13, 37, 3, "steel", normal_z=1)
    b.box((-4, 11.5, -53), (4, 13.5, -50.5), "steel_dark", 0.01)
    for x in (-2.5, 0, 2.5):
        b.cyl((x, 12.5, -53), (x, 12.5, -54.3), 0.5, "steel_bare", 8)
        b.cyl((x, 13.3, -53.6), (x, 14.2, -53.6), 0.2, "paint_crimson", 6)
        b.cyl((x - 0.7, 14.2, -53.6), (x + 0.7, 14.2, -53.6), 0.2, "paint_crimson", 6)
    for s in (-1, 1):
        b.lathe([(0.9, 0), (0.9, 1)], (s * 9.5, 12, -48), (s * 9.5, 12, -20), "steel", seg=12, cap0="steel", cap1="steel")  # hose tubes
        b.box((min(s * 9, s * 15.5), 14.5, -50.5), (max(s * 9, s * 15.5), 15.2, -24), "black", 0.01)                     # mud guards
        b.prism(arc_ring(-30, 7, 7.6, 8.2, 60, 120, 4), "x", min(s * 11, s * 15.5), max(s * 11, s * 15.5), "black", 0.0)
        C.mud_flap(b, min(s * 11, s * 15.5), max(s * 11, s * 15.5), 3, 12.5, -51.5)
        placard(b, s * 12.3, 25, 10, s, 2.0, "3")
        placard(b, s * 12.3, 25, -42, s, 2.0, "3")
        b.text("FUEL", (s * 12.25, 27, -16), (s, 0, 0), 3.0, "white_enamel", 0.12)
        b.lamp((s * 13, 12.5, -52.2), (0, 0, -1), 0.8, "tail", "black", 0.3, square=True)
        b.lamp((s * 12.2, 15, 16), (s, 0, 0), 0.5, "amber", "black", 0.3, seg=8)
    b.box((-14.5, 11.5, -52.5), (14.5, 12.0, -51.5), "steel_dark", 0.0)
    drawbar(b, 11, 20, 34, 6, 0.9)
    body = b.finish(parent=root, origin=(0, 0, 0), props={"part": "body"})
    legs_pair(body, (-10.5, 10.5), 9.5, 11.5, 12.5)
    add_wheels(root, [("wheel_mid", 11, 7, -30, True), ("wheel_rear", 11, 7, -44, True)])
    return root


# ================================================================================================ TANKER SMALL (bowser)
def tanker_small():
    H.new_scene()
    root = empty("TankerSmall", props={"design": "TankerSmall", "voxel": 0.08, "coupler": [0, 7, 22]})
    b = MB("Body")
    for s in (-1, 1):
        b.vbox(s * 4, 5, -22, s * 5, 6, 6, "steel_dark", 0.01)
    for z in (-20, -9, 2):
        b.vbox(-4, 5, z, 4, 6, z, "steel_dark", 0.0)
    tank_shell(b, 13, 7, -21.5, 5.5, "paint_olive", 6, "rust", 1.8, 32)
    for z in (-17, 1):
        b.prism(arc_ring(0, 13, 6.8, 7.3, 205, 335, 8), "z", z - 0.5, z + 0.5, "steel_dark", 0.0)
    b.cyl((0, 19.5, -8), (0, 20.8, -8), 1.6, "steel", 16)
    b.cyl((0, 20.8, -8), (0, 21.2, -8), 1.8, "steel_bare", 16)
    b.cyl((1.5, 20.8, -8), (2.6, 21.6, -8), 0.25, "steel_bare", 6)
    b.box((-1.6, 5.5, -24.6), (1.6, 9.8, -21.5), "black", 0.02)                   # pump box
    b.cyl((0, 8, -24.6), (0, 8, -25.6), 0.5, "chrome", 8)
    b.lathe([(0.3, 0), (2.0, 0), (2.0, 1), (0.3, 1)], (-2.6, 9, -23), (-1.6, 9, -23), "paint_crimson", seg=16)     # hose reel
    b.hose((-2.6, 9, -23), (-1.0, 7.4, -25.4), 0.25, "hose", 0.8, 4)
    for s in (-1, 1):
        b.prism(arc_ring(-9, 4, 4.8, 5.3, 0, 180, 12), "x", min(s * 7.5, s * 10.8), max(s * 7.5, s * 10.8), "black", 0.005)
        b.lamp((s * 4, 7.5, -23.2), (0, 0, -1), 0.7, "tail", "black", 0.3, square=True)
        placard(b, s * 7.2, 13, -14, s, 1.4, "3")
        b.text("DIESEL" if s < 0 else "DIESEL", (s * 7.15, 13, -1), (s, 0, 0), 1.6, "white_enamel", 0.08)
    drawbar(b, 6, 5, 22, 3.5, 0.6)
    b.cyl((0, 6.5, 19), (1.5, 6.5, 19), 0.6, "steel_dark", 8)
    body = b.finish(parent=root, origin=(0, 0, 0), props={"part": "body"})
    jockey_leg(body, 16, 6)
    add_wheels(root, [("wheel", 7, 4, -9, True)], "wheel_small", 4.3, 3, 3.1, "small", "paint_cream")
    return root


# ================================================================================================ CARGO TRAILER
def cargo_trailer():
    H.new_scene()
    root = empty("CargoTrailer", props={"design": "CargoTrailer", "voxel": 0.08, "coupler": [0, 6, 20]})
    b = MB("Body")
    P_ = "paint_olive"
    b.box((-10.5, 4.5, -16.5), (10.5, 6.5, 6.5), "steel", 0.01)
    b.box((-9.5, 6.5, -15.5), (9.5, 6.65, 5.5), "wood", 0.0)
    for s in (-1, 1):
        b.wall("x", s * 10, -16.5, 6.5, 6.5, 12.5, [], 1.0, P_, 0.01)
        for z in (-12, -5, 2):
            b.box((min(s * 10.5, s * 10.9), 6.5, z - 0.35), (max(s * 10.5, s * 10.9), 12.5, z + 0.35), P_, 0.004)
        b.prism(arc_ring(-5, 4, 4.8, 5.4, 0, 180, 12), "x", min(s * 10.5, s * 14), max(s * 10.5, s * 14), "black", 0.005)
        b.lamp((s * 8, 7.5, -16.8), (0, 0, -1), 0.8, "tail", "black", 0.3, square=True)
        b.cyl((s * 10.6, 12.5, -15), (s * 10.6, 12.5, 5), 0.3, "steel", 6)                         # rope rail
        for z in (-14, -5, 4):
            b.box((min(s * 10.5, s * 11), 11, z - 0.4), (max(s * 10.5, s * 11), 12.5, z + 0.4), "steel", 0.0)
    b.wall("z", -16, -9.5, 9.5, 6.5, 12.5, [], 1.0, P_, 0.01)
    b.wall("z", 6, -9.5, 9.5, 6.5, 12.5, [], 1.0, P_, 0.01)
    for z in (-16, 6):
        b.box((-10.6, 12.5, z - 0.6), (10.6, 13.5, z + 0.6), "steel", 0.008)
    b.box((-10.6, 12.5, -16.6), (-9.4, 13.5, 6.6), "steel", 0.008)
    b.box((9.4, 12.5, -16.6), (10.6, 13.5, 6.6), "steel", 0.008)
    # load: tarp-covered heap, a crate, a wheel
    b.box((-8, 6.6, -14), (2, 12, -4), "canvas", 0.2, 3)
    b.box((3, 6.6, -14), (9, 11, -8), "wood", 0.03)
    b.lathe([(2.5, 0), (4, 0.1), (4, 0.9), (2.5, 1)], (5.5, 6.7, -1), (5.5, 9.5, -1), "tyre", seg=18)
    b.text("SCRAP", (0, 9.5, -16.6), (0, 0, -1), 2.0, "white_enamel", 0.1)
    drawbar(b, 6, 6, 20, 4, 0.6)
    body = b.finish(parent=root, origin=(0, 0, 0), props={"part": "body"})
    jockey_leg(body, 14, 5)
    add_wheels(root, [("wheel", 11, 4, -5, True)], "wheel_small", 4.3, 3, 3.1, "small", "paint_cream")
    return root


# ================================================================================================ BOX SEMI-TRAILER
def box_trailer():
    H.new_scene()
    root = empty("BoxTrailer", props={"design": "BoxTrailer", "voxel": 0.08, "coupler": [0, 17, 54]})
    b = MB("Body")
    skin = "paint_cream"
    for s in (-1, 1):
        b.vbox(s * 6, 13, -82, s * 8, 17, 60, "steel_dark", 0.01)
    for z in range(-80, 60, 6):
        b.vbox(-15, 16, z, 15, 17, z, "steel_dark", 0.0)
    b.box((-15.5, 17.5, -82.5), (15.5, 19.5, 60.5), "steel", 0.01)
    for s in (-1, 1):
        holes = [(-4.5, 10.5, 20.5, 46.5)] if s > 0 else []
        b.wall("x", s * 15, -82.5, 60.5, 19.5, 50.5, [], 1.0, skin, 0.01)
        xo = s * 15.5
        b.box((min(xo, xo + s * 0.2), 41.5, -82.5), (max(xo, xo + s * 0.2), 44.5, 60.5), "paint_ochre", 0.0)
        for z in range(-80, 59, 8):
            b.box((min(xo, xo + s * 0.45), 19.5, z - 0.4), (max(xo, xo + s * 0.45), 50.5, z + 0.4), skin, 0.004)
        b.box((min(xo, xo + s * 0.8), 18.5, -82.5), (max(xo, xo + s * 0.8), 20, 60.5), "steel", 0.006)
        b.box((min(xo, xo + s * 0.8), 50.2, -82.5), (max(xo, xo + s * 0.8), 51.5, 60.5), "steel", 0.006)
        b.box((min(s * 9, s * 15.5), 15.5, -78.5), (max(s * 9, s * 15.5), 17.2, -49.5), "black", 0.02)
        C.mud_flap(b, min(s * 11, s * 15.5), max(s * 11, s * 15.5), 2.5, 12.5, -80)
        b.lamp((s * 13, 21, -83.2), (0, 0, -1), 1.0, "tail", "black", 0.3, square=True)
        b.lamp((s * 15.2, 52, -82), (0, 1, 0), 0.4, "amber", "black", 0.3, seg=8)
        b.lamp((s * 15.2, 52, 60), (0, 1, 0), 0.4, "amber", "black", 0.3, seg=8)
        for z in (-60, -20, 20):
            b.lamp((s * 15.6, 20.5, z), (s, 0, 0), 0.4, "amber", "black", 0.2, seg=8)
        C.plate_patch(b, "x", xo + s * 0.6, -70, -62, 22, 30, "rust", s)
    b.text("MAD MIKE'S", (15.65, 35.5, -10), (1, 0, 0), 5.5, "paint_crimson", 0.15)
    b.text("FREIGHT & SALVAGE", (15.65, 27.5, -10), (1, 0, 0), 3.2, "black", 0.12)
    b.text("MAD MIKE'S", (-15.65, 35.5, -10), (-1, 0, 0), 5.5, "paint_crimson", 0.15)
    b.text("FREIGHT & SALVAGE", (-15.65, 27.5, -10), (-1, 0, 0), 3.2, "black", 0.12)
    b.wall("z", 60, -15.5, 15.5, 19.5, 50.5, [], 1.0, skin, 0.01)
    b.box((-15.5, 50.5, -82.5), (15.5, 52.5, 60.5), "steel", 0.01)
    for z in range(-80, 60, 4):
        b.box((-15.2, 52.5, z - 0.2), (15.2, 52.7, z + 0.2), "steel", 0.0)
    # side door to the hold (right), frame + hinges + lock bar
    b.box((15.5, 20.5, -4.5), (16.1, 46.5, -3.5), "steel", 0.004)
    b.box((15.5, 20.5, 9.5), (16.1, 46.5, 10.5), "steel", 0.004)
    b.box((15.5, 45.5, -4.5), (16.1, 46.5, 10.5), "steel", 0.004)
    b.box((16.0, 30, 6.5), (16.5, 32, 8.5), "chrome", 0.004)
    for z in (-40, -38, -36, -32, -30, -26, -24):
        b.box((15.5, 35.5, z - 0.4), (15.8, 39.5, z + 0.4), "black", 0.0)
    # rear doors: two leaves with lock bars and hinges
    b.wall("z", -82, -15.5, 15.5, 19.5, 50.5, [], 1.0, skin, 0.01)
    b.box((-0.2, 19.5, -83), (0.2, 50.5, -82.5), "steel_dark", 0.0)
    for x in (-10, -4, 4, 10):
        b.cyl((x, 20, -83.1), (x, 50, -83.1), 0.3, "chrome", 8)
        b.box((x - 0.6, 30, -83.5), (x + 0.6, 31, -82.6), "steel_bare", 0.004)
    for s in (-1, 1):
        for y in (22, 30, 38, 46):
            b.box((s * 15.1 - 0.4, y - 0.8, -83.2), (s * 15.1 + 0.4, y + 0.8, -82.5), "steel_bare", 0.004)
    b.text("NO RIDERS", (-7, 45.5, -83.1), (0, 0, -1), 1.3, "black", 0.08)
    # underride bar, kingpin plate
    b.box((-13.5, 7.5, -84.5), (13.5, 10.5, -83.5), "hazard_red", 0.01)
    for x in (-10, 10):
        b.box((x - 0.5, 10.5, -84), (x + 0.5, 13.5, -83), "steel_dark", 0.0)
    b.box((-7.5, 16.5, 49.5), (7.5, 17.5, 58.5), "steel_bare", 0.01)
    b.cyl((0, 16.5, 54), (0, 14.6, 54), 0.8, "steel_bare", 12)
    b.cyl((0, 14.8, 54), (0, 14.4, 54), 1.1, "steel_bare", 12)
    for i, mat in enumerate(("tail", "paint_navy")):
        b.box((-4 + i * 6, 34, 60.5), (-2 + i * 6, 36, 61.2), "steel_dark", 0.004)      # glad hands
        b.cyl((-3 + i * 6, 35, 61.2), (-3 + i * 6, 35, 61.8), 0.5, mat, 8)
    body = b.finish(parent=root, origin=(0, 0, 0), props={"part": "body", "cargoKg": 3000})
    legs_pair(body, (-10.5, 10.5), 45.5, 47.5, 16.5, 2.0)
    add_wheels(root, [("wheel_front", 11, 7, -58, True), ("wheel_rear", 11, 7, -70, True)])
    return root


# ================================================================================================ CAR TRAILERS
def trailer_frame(b, half_len, deck_y):
    for s in (-1, 1):
        b.box((min(s * 11.5, s * 14.5), deck_y - 3.5, -half_len - 0.5), (max(s * 11.5, s * 14.5), deck_y - 0.5, half_len + 0.5), "steel_dark", 0.012)
        b.box((min(s * 11.5, s * 14.5), deck_y - 3.0, -half_len + 0.5), (max(s * 11.5, s * 14.5), deck_y - 1.0, half_len - 0.5), "void", 0.0)
        b.lamp((s * 12, deck_y - 1.5, -half_len - 0.8), (0, 0, -1), 0.8, "tail", "black", 0.3, square=True)
    for z in range(-half_len, half_len + 1, 8):
        b.box((-11.5, deck_y - 2.5, z - 0.5), (11.5, deck_y - 0.5, z + 0.5), "steel_dark", 0.0)
    # A-frame drawbar to the coupler eye
    drawbar(b, deck_y - 2, half_len, half_len + 15, 10, 0.8)
    b.box((-1.5, deck_y - 3.5, half_len + 14), (1.5, deck_y - 0.5, half_len + 15.5), "steel_bare", 0.01)


def deck(name, parent, pivot, half_w, length):
    """Movable deck (game TransformDesigns.Deck): local grid from z = -length..0, y 0..1, rust side rails."""
    mb = MB(name)
    mb.box((-half_w - 0.5, -0.5, -length - 0.5), (half_w + 0.5, 1.5, 0.5), "tread", 0.01)
    for z in range(-length, 1, 3):
        mb.box((-half_w + 0.5, 1.5, z - 0.15), (half_w - 0.5, 1.7, z + 0.15), "steel_bare", 0.0)
    for x in (-half_w, half_w):
        mb.box((x - 0.5, 1.5, -length - 0.5), (x + 0.5, 3.5, 0.5), "rust", 0.006)
        for z in range(-length + 4, 0, 8):
            mb.cyl((x, 3.5, z), (x, 4.6, z), 0.3, "steel_bare", 6)                      # strap posts
    for x in (-9, 9):                                                                    # wheel chocks
        mb.prism([(-12, 1.5), (-9, 1.5), (-10, 3)], "x", x - 2, x + 2, "hazard", 0.005)
    return mb.finish(parent=parent, origin=pivot, local=True, props={"movable": name})


def ramp(name, parent, pivot, half_w, length, tilt=-22.0):
    mb = MB(name)
    mb.box((-half_w - 0.5, -0.5, -length - 0.5), (half_w + 0.5, 1.5, 0.5), "tread", 0.01)
    for z in range(-length + 1, 0, 2):
        mb.box((-half_w, 1.5, z - 0.2), (half_w, 1.9, z + 0.2), "steel_bare", 0.0)
    for x in (-half_w, half_w):
        mb.box((x - 0.4, 1.5, -length - 0.5), (x + 0.4, 2.4, 0.5), "steel_dark", 0.0)
    ob = mb.finish(parent=parent, origin=pivot, local=True, props={"movable": name, "rest_euler_x_unity": tilt})
    ob.rotation_euler.x = math.radians(tilt)      # far end down to the ground (Unity Euler x = -22)
    return ob


def car_trailer(double=False):
    H.new_scene()
    nm = "CarTrailerDouble" if double else "CarTrailer"
    root = empty(nm, props={"design": nm, "voxel": 0.08, "coupler": [0, 7, 55]})
    b = MB("Body")
    trailer_frame(b, 40, 9)
    for s in (-1, 1):
        b.prism(arc_ring(-1, 4, 5.0, 5.6, 0, 180, 12) + [], "x", min(s * 14.5, s * 17.5), max(s * 14.5, s * 17.5), "black", 0.005)
        b.box((min(s * 14.5, s * 17.5), 4, -8), (max(s * 14.5, s * 17.5), 5, -7), "steel_dark", 0.0)
        b.lamp((s * 14.6, 7.6, 30), (s, 0, 0), 0.4, "amber", "black", 0.2, seg=8)
        if double:
            for zz, z0, z1 in ((39, 37.5, 40.5), (-39, -40.5, -37.5)):
                b.box((min(s * 13.5, s * 14.5), 8.5, z0), (max(s * 13.5, s * 14.5), 37.5, z1), "paint_ochre", 0.01)
                for y in range(12, 36, 4):
                    b.box((min(s * 14.5, s * 14.8), y, z0 + 0.3), (max(s * 14.5, s * 14.8), y + 0.5, z1 - 0.3), "black", 0.0)
            b.box((min(s * 13.5, s * 14.5), 35.5, -40.5), (max(s * 13.5, s * 14.5), 37.5, 40.5), "paint_ochre", 0.01)
            for z in (-20, 0, 20):                                                  # diagonal braces
                b.cyl((s * 14, 10, z - 9), (s * 14, 35, z + 9), 0.45, "steel_dark", 8)
            b.cyl((s * 14, 36.5, 30), (s * 14, 20, 30), 0.9, "steel_bare", 10)          # tilt ram
            b.cyl((s * 14, 36.5, -30), (s * 14, 24, -30), 1.1, "paint_ochre", 10)
    if double:
        b.box((-14.5, 35.5, 37.5), (14.5, 37.5, 40.5), "paint_ochre", 0.01)
        b.box((-14.5, 8.5, 37.5), (14.5, 10.5, 38.5), "steel_dark", 0.0)
    body = b.finish(parent=root, origin=(0, 0, 0), props={"part": "body"})
    jockey_leg(body, 50, 7)
    deck("Deck", root, (0, 9, 40), 13 if double else 14, 80)
    if double:
        deck("UpperDeck", root, (0, 33, 37), 12, 78)
    ramp("RampL", root, (-8, 9, -41), 4, 22)
    ramp("RampR", root, (8, 9, -41), 4, 22)
    add_wheels(root, [("wheel_front", 12, 4, 4, True), ("wheel_rear", 12, 4, -6, True)], "wheel_small", 4.3, 3, 3.1, "small", "steel")
    return root


BUILDERS = {
    "Tanker": (tanker, "Tanker", "VehicleDesigns.Tanker (2000 L fuel trailer)"),
    "TankerSmall": (tanker_small, "Tanker (small)", "VehicleDesigns.TankerSmall (450 L bowser)"),
    "CargoTrailer": (cargo_trailer, "Cargo trailer", "VehicleDesigns.CargoTrailer"),
    "BoxTrailer": (box_trailer, "Box semi-trailer", "VehicleDesigns.BoxTrailer"),
    "CarTrailer": (lambda: car_trailer(False), "Car trailer", "VehicleDesigns.CarTrailer (single deck + ramps)"),
    "CarTrailerDouble": (lambda: car_trailer(True), "Car trailer (double)", "VehicleDesigns.CarTrailerDouble (tilting upper deck)"),
}


def run(names):
    for n in names:
        fn, label, replaces = BUILDERS[n]
        root = fn()
        H.save_and_render(root, label, replaces)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    run(argv or list(BUILDERS))
