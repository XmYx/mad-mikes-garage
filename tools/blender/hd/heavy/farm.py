"""HD farm tractor (FarmDesigns.Tractor) with the three-point-hitch implements (PartLibrary.Farm: tool_plough,
tool_seeder, tool_sprayer, tool_harvester). Implements are authored from the hitch (origin) backwards (-z), resting
on the ground at y 0, exactly like the game. Run: blender -b -P farm.py -- [Tractor Implements]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hvlib as H  # noqa: E402
from hvlib import MB, add_wheels, arc_ring, empty, rrect_top  # noqa: E402
import render_common as rc  # noqa: E402
from machines import op_cab, ram  # noqa: E402

R_ = "paint_tractor"


def tractor_body(root):
    b, gl = MB("Body"), MB("Glass")
    b.box((-4.5, 5.5, -26.5), (4.5, 9.5, 30.5), "steel_dark", 0.02)                       # chassis / engine block / gearbox
    for z in (-20, -8, 4, 16):
        b.box((-4.8, 6, z - 2), (4.8, 9, z + 2), "steel_dark", 0.01)
        b.cyl((-5, 7.5, z), (5, 7.5, z), 0.6, "steel", 8)
    # bonnet with rounded top, louvres, grille and lamps
    b.prism(rrect_top(-5.5, 5.5, 9.5, 17.5, 1.8, 5), "z", -4.5, 31.5, R_, 0.03, 2)
    for s in (-1, 1):
        for z in range(6, 26, 3):
            b.box((min(s * 5.5, s * 5.8), 11.5, z - 0.5), (max(s * 5.5, s * 5.8), 15.5, z + 0.5), "void", 0.0)
            b.box((min(s * 5.5, s * 6.0), 15.5, z - 0.6), (max(s * 5.5, s * 6.0), 15.9, z + 0.6), R_, 0.0)
        b.seam((s * 5.55, 10, 2), (s * 5.55, 16, 2), (s, 0, 0))
        b.lamp((s * 3.5, 17.6, 30.5), (0, 0.2, 1), 0.7, "lightw", "chrome", 0.5, seg=12)
    b.grille(-4.5, 4.5, 9.5, 16.5, 31.6, "chrome", "steel_dark", 8, "void", False)
    b.box((-5.5, 16.5, 31.0), (5.5, 17.6, 32.0), R_, 0.02)
    b.text("MM 1066", (5.85, 13.5, 16), (1, 0, 0), 1.3, "white_enamel", 0.08)
    b.text("MM 1066", (-5.85, 13.5, 16), (-1, 0, 0), 1.3, "white_enamel", 0.08)
    # front weights
    b.box((-5.5, 5.5, 31.5), (5.5, 9.5, 33), "steel_dark", 0.01)
    for x in range(-5, 6, 2):
        b.box((x - 0.9, 5.7, 33), (x + 0.9, 9.3, 37.5), "steel", 0.02)
    b.cyl((-5.5, 7.5, 35.5), (5.5, 7.5, 35.5), 0.4, "steel_bare", 8)
    # exhaust stack + air intake
    b.cyl((4, 17.5, 6), (4, 33, 6), 0.9, "black", 10)
    b.cyl((4, 33, 6), (4.1, 34.5, 6.6), 0.95, "rust", 10)
    b.box((3, 27, 5), (5, 27.4, 7), "steel_dark", 0.0)
    b.cyl((-3.5, 17.5, 8), (-3.5, 24, 8), 1.1, R_, 10)
    b.ball((-3.5, 24.3, 8), 1.3, "black", 10, 6, 0.6)
    # cab over the rear axle
    op_cab(b, gl, 8, -24, -5, 14, 34, R_)
    b.box((-8.5, 13.5, -24.5), (8.5, 14.5, -4.5), "steel", 0.01)
    # rear fenders over the big wheels, step and tool boxes
    for s in (-1, 1):
        x0, x1 = (8.5, 15.5) if s > 0 else (-15.5, -8.5)
        pts = []
        for z in range(-30, -2):
            dz = (z + 16) / 14.0
            pts.append((z, 21 + 4 * (1 - dz * dz)))
        top = [(z, y + 0.5) for z, y in pts]
        bot = [(z, y - 0.4) for z, y in pts][::-1]
        b.prism(top + bot, "x", x0, x1, R_, 0.02)
        b.prism([(z, y + 0.5) for z, y in pts] + [(z, y - 2.5) for z, y in pts][::-1], "x",
                min(s * 15.5, s * 16.2), max(s * 15.5, s * 16.2), R_, 0.01)               # fender lip
        b.box((min(s * 8.5, s * 10.5), 11.5, -6.5), (max(s * 8.5, s * 10.5), 13.5, -1.5), "tread", 0.01)
        b.box((min(s * 8.5, s * 14.5), 15.5, -30.5), (max(s * 8.5, s * 14.5), 20.5, -26.5), "steel_dark", 0.02)
        b.lamp((s * 12.5, 21.2, -30.5), (0, 0, -1), 0.5, "tail", "black", 0.3, seg=8)
        b.lamp((s * 12.5, 21.4, -27), (0, 0.6, -1), 0.6, "lightw", "black", 0.5, square=True)
    # three-point hitch: lower links, top link, lift arms, PTO
    for s in (-1, 1):
        b.cyl((s * 4, 6.5, -25), (s * 5, 3.5, -30.5), 0.6, "steel_dark", 10)
        b.cyl((s * 4, 11, -26), (s * 4.5, 9, -30), 0.5, "steel", 8)
        ram(b, (s * 6, 9, -24), (s * 5, 5, -28), 0.5, R_)
    b.cyl((0, 13, -26.5), (0, 9.5, -31), 0.5, "steel", 8)
    b.box((-1.5, 9.5, -30.5), (1.5, 12.5, -26.5), "steel_dark", 0.01)
    b.cyl((0, 9, -30.5), (0, 9, -32), 0.45, "steel_bare", 10)
    body = b.finish(parent=root, origin=(0, 0, 0), props={"part": "body"})
    gl.finish(parent=body, origin=(0, 0, 0))
    return body


def tractor_wheels(root):
    add_wheels(root, [("wheel_front", 6, 6, 22, True)], "wheel_tractor_front", 5.4, 4, 3.2, "tractor_front", R_)
    add_wheels(root, [("wheel_rear", 9, 10, -16, True)], "wheel_tractor", 9.4, 6, 5.6, "tractor", R_)


# ------------------------------------------------------------------------------------------------ implements (part-local)
def _part(name, parent, pos, key, mass):
    mb = MB(name)
    mb.frame(pos)
    return mb


def _done(mb, parent, pos, key):
    mb.reset()
    return mb.finish(parent=parent, origin=pos, props={"socket": "tool", "part": key, "category": "Tool"})


def headstock(mb, half=6):
    mb.box((-half - 0.5, 5.5, -2.5), (half + 0.5, 8.5, 0.5), R_, 0.02)
    for x in (-half, half):
        mb.cyl((x, 4, -0.5), (x, 4, 0.8), 0.7, "steel_bare", 8)       # lower link pins
    mb.prism([(-1.5, 8.5), (0.5, 8.5), (0.5, 14), (-0.5, 14)], "x", -1, 1, R_, 0.01)   # top link tower
    mb.cyl((-1.3, 13, 0), (1.3, 13, 0), 0.5, "steel_bare", 8)


def plough(parent, pos):
    mb = _part("Tool", parent, pos, "tool_plough", 420)
    headstock(mb)
    a, b_ = (0, 7.5, -2), (13, 7.5, -30)
    d = (H.Vector(b_) - H.Vector(a))
    mb.cyl(a, b_, 1.1, R_, 12)
    mb.box((-1.2, 6.4, -3), (1.2, 8.6, -1), R_, 0.01)
    for i in range(4):
        z, x = -6 - i * 7, -16 + i * 9
        t = (z + 2) / -28.0
        bx = a[0] + d.x * t
        mb.box((min(x, bx), 6.6, z - 0.6), (max(x, bx) + 1, 8.4, z + 0.6), R_, 0.01)        # arm to the beam
        mb.box((x - 0.2, 1, z - 0.6), (x + 1.2, 7, z + 0.6), R_, 0.01)                       # leg
        # curved mouldboard: twisted ruled surface
        top = [(x + 1 + k * 0.6, 3.8 + k * 0.45, z - k * 1.0) for k in range(7)]
        bot = [(x + 0.6 + k * 0.3, 0.0 + k * 0.15, z + 0.5 - k * 1.0) for k in range(7)]
        mb.quad_strip(top, bot, "steel_bare")
        mb.quad_strip([(p[0] - 0.25, p[1], p[2]) for p in bot], [(p[0] - 0.25, p[1], p[2]) for p in top], "steel")
        mb.prism([(z + 2.5, -0.5), (z - 1.0, -0.3), (z - 1.0, 0.6), (z + 1.5, 0.4)], "x", x - 1.5, x + 2.5, "chrome", 0.004)   # share
        mb.prism([(z - 1, -0.2), (z - 6, 0.0), (z - 1, 0.5)], "x", x - 0.6, x - 0.3, "steel_dark", 0.0)                          # landside
    mb.lathe([(1.0, 0), (2.4, 0.05), (2.4, 0.95), (1.0, 1)], (14.5, 2.4, -32), (16.5, 2.4, -32), "tyre", seg=20, cap0="steel", cap1="steel")
    mb.cyl((13, 7.5, -30), (15.5, 2.4, -32), 0.5, "steel_dark", 8)
    return _done(mb, parent, pos, "tool_plough")


def seeder(parent, pos):
    mb = _part("Tool", parent, pos, "tool_seeder", 380)
    headstock(mb, 8)
    mb.box((-14.5, 5.5, -4.5), (14.5, 7.5, -1.5), "steel", 0.01)
    mb.prism([(-10.5, 7.5), (-2.5, 7.5), (-2.0, 16.5), (-11.0, 16.5)], "x", -13.5, 13.5, R_, 0.03, 2)
    mb.box((-12.5, 16.5, -9.5), (12.5, 17.5, -3.5), "steel", 0.02)
    mb.box((-12.5, 17.5, -9.5), (12.5, 17.7, -3.5), "steel_dark", 0.0)
    for x in range(-12, 13, 2):
        mb.cyl((x, 7.5, -8), (x, 3.2, -8), 0.25, "black", 6)                            # seed tubes
        mb.box((x - 0.3, 1.2, -8.6), (x + 0.3, 7.2, -7.4), "steel_dark", 0.0)
        mb.cyl((x - 0.25, 1.6, -8), (x + 0.25, 1.6, -8), 1.6, "chrome", 14)              # disc coulters
    mb.lathe([(1.4, 0), (2.2, 0.02), (2.2, 0.98), (1.4, 1)], (-14.5, 2.2, -14), (14.5, 2.2, -14), "steel", seg=20, cap0="steel_dark", cap1="steel_dark")
    for x in (-14.5, 14.5):
        mb.box((x - 0.5, 2, -15), (x + 0.5, 6.5, -2), "steel", 0.01)
    mb.box((-14.5, 3.5, -14.5), (14.5, 4.5, -11.5), "steel", 0.01)
    return _done(mb, parent, pos, "tool_seeder")


def harvester(parent, pos):
    mb = _part("Tool", parent, pos, "tool_harvester", 900)
    mb.box((-1.5, 4.5, -8.5), (1.5, 7.5, 0.5), "steel", 0.01)
    mb.cyl((0, 6, 0.5), (0, 6, 1.5), 0.6, "steel_bare", 8)
    mb.prism([(-14.5, 0), (-7.5, 0), (-7.5, 1), (-13, 3.5), (-14.5, 3.5)], "x", -15.5, 15.5, "steel", 0.02)   # header tray
    for x in (-15.5, 15.5):
        mb.prism([(-14.5, 0), (-7.5, 0), (-7.5, 4), (-14.5, 9)], "x", x - 0.5, x + 0.5, R_, 0.01)
    for x in range(-15, 16, 1):                                                                      # cutter bar guards
        mb.prism([(-7.5, 0.6), (-6.2, 0.9), (-7.5, 1.4)], "x", x - 0.2, x + 0.2, "chrome", 0.0)
    mb.cyl((-15.5, 7, -11), (15.5, 7, -11), 0.4, "steel_bare", 8)                                     # reel shaft
    for k in range(6):
        a = k / 6.0 * math.pi * 2
        y, z = 7 + math.sin(a) * 3, -11 + math.cos(a) * 3
        mb.cyl((-14, y, z), (14, y, z), 0.3, "paint_ochre", 6)
        for x in (-14, 0, 14):
            mb.cyl((x, 7, -11), (x, y, z), 0.15, "steel", 4)
    mb.cyl((-14, 2.5, -12), (14, 2.5, -12), 1.2, "steel_dark", 12)                                   # auger
    mb.box((-10.5, 3.5, -24.5), (10.5, 14.5, -14.5), R_, 0.03, 2)                                     # grain tank
    mb.box((-9.5, 14.5, -23.5), (9.5, 15.2, -15.5), "steel", 0.01)
    mb.box((-8.5, 15.2, -22.5), (8.5, 15.5, -16.5), "grain", 0.0)
    mb.cyl((10.5, 12.5, -19.5), (16.5, 13.5, -19.5), 0.8, "steel", 10)                                # spout
    mb.cyl((16.5, 13.5, -19.5), (17, 11.5, -19.5), 0.8, "steel", 10)
    for x in (-11.5, 11.5):
        mb.lathe([(1.4, 0), (3.2, 0.05), (3.2, 0.95), (1.4, 1)], (x - 1, 3.2, -20), (x + 1, 3.2, -20), "tyre", seg=20, cap0="steel", cap1="steel")
    return _done(mb, parent, pos, "tool_harvester")


def sprayer(parent, pos):
    mb = _part("Tool", parent, pos, "tool_sprayer", 350)
    headstock(mb, 6)
    mb.box((-6.5, 5.5, -16.5), (6.5, 7.5, -1.5), "steel", 0.01)
    mb.lathe([(0, 0), (4.5, 0.03), (5.2, 0.1), (5.2, 0.9), (4.5, 0.97), (0, 1)], (0, 12, -16.5), (0, 12, -3.5), "white_enamel", seg=28)
    mb.cyl((0, 17, -10), (0, 18.4, -10), 1.0, "steel", 12)
    for s in (-1, 1):
        mb.cyl((s * 2, 7, -17), (s * 26, 6, -17), 0.35, "steel", 8)                       # boom
        mb.cyl((s * 2, 9, -17), (s * 18, 6.2, -17), 0.25, "steel", 6)                     # brace
        mb.box((s * 1 - 1, 5, -17.6), (s * 1 + 1, 10, -16.4), "steel_dark", 0.0)
    for x in range(-24, 25, 4):
        mb.cyl((x, 5.8, -17), (x, 3.5, -17), 0.2, "black", 6)
        mb.cyl((x, 3.5, -17), (x, 3.0, -17), 0.35, "paint_crimson", 6, r2=0.15)
    mb.hose((0, 8, -16.5), (-24, 6.3, -17.3), 0.15, "hose", 0.6, 10)
    for x in (-5, 5):
        mb.lathe([(1.0, 0), (2.6, 0.05), (2.6, 0.95), (1.0, 1)], (x - 0.8, 2.6, -12), (x + 0.8, 2.6, -12), "tyre", seg=18, cap0="steel", cap1="steel")
    return _done(mb, parent, pos, "tool_sprayer")


def tractor():
    H.new_scene()
    root = empty("Tractor", props={"design": "Tractor", "voxel": 0.08, "machine": "Tractor"})
    tractor_body(root)
    tractor_wheels(root)
    plough(root, (0, 1, -31))
    return root


def implements_tile():
    """Extra tile: seeder, sprayer and harvester as they hang on the hitch (each a separate part)."""
    H.new_scene()
    root = empty("Tractor_implements", props={"note": "PartLibrary.Farm implements, part-local origin = hitch"})
    for name, fn, pos in (("Seeder", seeder, (-30, 1, 26)), ("Sprayer", sprayer, (30, 1, 30)), ("Harvester", harvester, (-30, 1, -14)), ("Plough", plough, (24, 1, -10))):
        holder = empty(name, root, pos)
        ob = fn(holder, pos)
        ob.name = "Tool_" + name.lower()
    return root


BUILDERS = {
    "Tractor": (tractor, "Tractor + plough", "FarmDesigns.Tractor + tool_plough (default)"),
    "Tractor_implements": (implements_tile, "Tractor implements", "PartLibrary.Farm: tool_seeder, tool_sprayer, tool_harvester, tool_plough"),
}


def run(names):
    for n in names:
        fn, label, replaces = BUILDERS[n]
        root = fn()
        H.save_and_render(root, label, replaces)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    run(argv or list(BUILDERS))
