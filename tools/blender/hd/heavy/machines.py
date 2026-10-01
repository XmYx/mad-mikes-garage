"""HD construction machines: Excavator, Backhoe, Bulldozer, DumpTruck, Paver, Roller (+ Excavator posed tile).
Hinged tool parts keep the game's PartDesign.Segment names and pivots (boom/stick/bucket, arms/bucket,
swing/boom/stick/bucket, lift/blade); each segment is an object with its origin on its joint, parented like the game.
Run: blender -b -P machines.py -- [names]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hvlib as H  # noqa: E402
from hvlib import MB, add_wheels, arc_ring, empty, rrect, track_assembly  # noqa: E402
import common as C  # noqa: E402

Y = "paint_cat"


# ------------------------------------------------------------------------------------------------ helpers
def op_cab(b, gl, hx, z0, z1, floor, top, paint=Y, seats=True, beacon=True):
    """HD of MachineDesigns.Cab: ROPS posts, lower panels, glass all round above floor+5, roof with overhang,
    beacon, work lamps, wipers, operator controls."""
    xa, xb, za, zb = -hx - 0.5, hx + 0.5, z0 - 0.5, z1 + 0.5
    belt, head = floor + 5.5, top - 1.5
    b.box((xa, floor - 0.5, za), (xb, floor + 0.5, zb), "steel", 0.01)
    b.box((xa + 1, floor + 0.5, za + 1), (xb - 1, floor + 0.62, zb - 1), "tread", 0.0)
    for s in (-1, 1):
        b.wall("x", s * hx, za, zb, floor + 0.5, belt, [], 1.0, paint, 0.01)
    for z in (z0, z1):
        b.wall("z", z, xa + 1, xb - 1, floor + 0.5, belt, [], 1.0, paint, 0.01)
    for x in (xa, xb - 1.0):
        for z in (za, zb - 1.0):
            b.box((x, floor, z), (x + 1.0, top - 0.5, z + 1.0), paint, 0.012)
    b.box((xa, head, za), (xb, top - 0.5, zb), paint, 0.01)
    b.box((xa, belt - 0.3, za), (xb, belt + 0.3, zb), "black", 0.004)
    b.box((xa - 1, top - 0.5, za - 1), (xb + 1, top + 1.2, zb + 1), "steel", 0.03, 2)       # roof with overhang
    b.box((xa, top + 1.2, za), (xb, top + 1.5, zb), "steel_dark", 0.01)
    for s in (-1, 1):
        b.lamp((s * (hx - 0.5), top + 0.4, zb + 1.1), (0, -0.2, 1), 0.7, "lightw", "black", 0.6, square=True)
        b.lamp((s * (hx - 0.5), top + 0.4, za - 1.1), (0, -0.2, -1), 0.7, "lightw", "black", 0.6, square=True)
    if beacon:
        b.cyl((-hx + 2, top + 1.5, za + 2), (-hx + 2, top + 2.2, za + 2), 0.9, "black", 10)
        b.cyl((-hx + 2, top + 2.2, za + 2), (-hx + 2, top + 3.6, za + 2), 0.8, "amber", 12, r2=0.6)
    # glass: four sides between the posts, door on the left with handle and hinges
    gl.box((xa + 1, belt, zb - 0.6), (xb - 1, head, zb - 0.4), "glass", 0.0)
    gl.box((xa + 1, belt, za + 0.4), (xb - 1, head, za + 0.6), "glass", 0.0)
    for s in (-1, 1):
        gl.box((s * hx - 0.1, belt, za + 1), (s * hx + 0.1, head, zb - 1), "glass", 0.0)
    b.box((xa - 0.4, belt + 2, z0 + 3), (xa, belt + 2.6, z0 + 4.5), "chrome", 0.004)
    b.box((xa - 0.3, floor + 1, (z0 + z1) / 2 - 0.2), (xa, top - 2, (z0 + z1) / 2 + 0.2), "steel_dark", 0.0)   # door seam bar
    for s in (-1, 1):                                                            # grab handles
        b.cyl((s * (hx + 1), floor + 2, zb - 2), (s * (hx + 1), floor + 7, zb - 2), 0.25, "paint_crimson", 6)
    b.cyl((hx * 0.3, head, zb), (-hx * 0.2, belt + 2, zb + 0.15), 0.12, "black", 4)      # wiper
    if seats:
        mb_seat(b, floor, z0, z1, hx)


def mb_seat(b, floor, z0, z1, hx):
    """Single suspension seat + joystick consoles (machine operator)."""
    cz = z0 + 6
    b.box((1.5, floor + 0.5, cz - 4.5), (6.5, floor + 2.0, cz - 3), "steel_dark", 0.0)
    b.box((1.5, floor + 2.0, cz - 4.5), (6.5, floor + 4.5, cz + 0.5), "fabric", 0.06, 2)
    b.box((1.5, floor + 4.5, cz - 5.6), (6.5, floor + 12, cz - 4.4), "fabric", 0.06, 2)
    for x in (0.5, 7.5):
        b.box((x - 0.7, floor + 0.5, cz - 4), (x + 0.7, floor + 5.5, cz + 1), "black", 0.02)
        b.cyl((x, floor + 5.5, cz), (x, floor + 7.5, cz + 0.5), 0.2, "chrome", 6)
        b.ball((x, floor + 7.7, cz + 0.6), 0.45, "black", 8, 5)
    b.box((1, floor + 0.5, z1 - 3), (7, floor + 6, z1 - 1), "black", 0.02)                  # front console
    b.cyl((4, floor + 6, z1 - 2), (4, floor + 7.5, z1 - 2.5), 0.3, "steel_dark", 6)


def ram(b, a, c, r, body="paint_cat", rod="chrome", ext=0.55):
    """Hydraulic cylinder from a (barrel end) to c (rod end) with pin eyes."""
    a, c = H.Vector(a), H.Vector(c)
    m = a.lerp(c, ext)
    b.cyl(tuple(a), tuple(m), r, body, 12)
    b.cyl(tuple(m), tuple(m + (m - a).normalized() * 0.4), r * 1.08, "steel_dark", 12)
    b.cyl(tuple(m), tuple(c), r * 0.55, rod, 10)
    for p in (a, c):
        b.cyl((p.x - r * 0.9, p.y, p.z), (p.x + r * 0.9, p.y, p.z), r * 0.75, "steel_dark", 10)


def hd_bucket(b, c, w, d, dz=1, mat="steel", teeth=True):
    """HD of PartLibrary.Bucket(c, w, d): a curved back plate (quarter circle around c from the top (y+d) to the
    lip (z+dz*d)), side plates, wear strips and teeth along the lip."""
    cx, cy, cz = c
    if dz > 0:
        pts = arc_ring(cz, cy, d - 1.0, d + 0.1, 0, 90, 10)
    else:
        pts = arc_ring(cz, cy, d - 1.0, d + 0.1, 90, 180, 10)
    b.prism(pts, "x", cx - w - 0.5, cx + w + 0.5, mat, 0.01)
    side = [(cz, cy)] + [(cz + dz * math.cos(math.radians(a)) * (d + 0.1), cy + math.sin(math.radians(a)) * (d + 0.1)) for a in range(0, 91, 15)]
    for x in (cx - w - 0.5, cx + w + 0.5):
        b.prism(side, "x", x - 0.45, x + 0.45, mat, 0.008)
    b.box((cx - w - 0.5, cy - 0.3, cz + (dz * (d - 0.8) if dz > 0 else dz * (d + 0.4))),
          (cx + w + 0.5, cy + 0.5, cz + (dz * (d + 0.4) if dz > 0 else dz * (d - 0.8))), "steel_bare", 0.004)   # lip
    for a in (30, 60):                                                                                  # wear strips
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        b.box((cx - w - 0.2, cy + sa * (d + 0.1) - 0.3, cz + dz * ca * (d + 0.1) - 0.3),
              (cx + w + 0.2, cy + sa * (d + 0.1) + 0.3, cz + dz * ca * (d + 0.1) + 0.3), "steel_dark", 0.0)
    if teeth:
        n = max(3, int(w))
        for i in range(n):
            x = cx - w + 0.6 + i * (2 * w - 1.2) / (n - 1)
            b.prism([(cz + dz * (d - 0.2), cy - 0.2), (cz + dz * (d + 1.7), cy + 0.1), (cz + dz * (d - 0.2), cy + 0.8)],
                    "x", x - 0.45, x + 0.45, "steel_bare", 0.004)


def lamps(b, x, y, zf, zr):
    for s in (-1, 1):
        b.lamp((s * (x - 0.5), y + 0.5, zf + 0.4), (0, 0, 1), 0.8, "lightw", "black", 0.5, square=True)
        b.lamp((s * (x - 0.5), y, zr - 0.4), (0, 0, -1), 0.6, "tail", "black", 0.4, square=True)


def track_fenders(b, x0, w, z0, z1, h):
    """MachineDesigns.Track: inner frame rail with roller brackets + fender over the belt."""
    for s in (-1, 1):
        b.box((min(s * (x0 - 1.5), s * (x0 - 0.5)), 2.5, z0 + 1.5), (max(s * (x0 - 1.5), s * (x0 - 0.5)), 5.5, z1 - 1.5), "steel_dark", 0.01)
        b.box((min(s * x0 - .5 * s, s * (x0 + w + 1.5)), h + 0.5, z0 - 1.5), (max(s * x0 - .5 * s, s * (x0 + w + 1.5)), h + 1.5, z1 + 1.5), "hazard", 0.012)
        b.box((min(s * (x0 + w + 0.5), s * (x0 + w + 1.5)), h - 0.5, z0 - 1.5), (max(s * (x0 + w + 0.5), s * (x0 + w + 1.5)), h + 0.5, z1 + 1.5), "steel", 0.006)


def tool_root(root, name, socket, part, pos, props=None):
    p = {"socket": socket, "part": part, "category": "Tool"}
    p.update(props or {})
    return empty(name, root, pos, p)


def finish_tracks(root, x_in, width, zf, zr, y, R=4.3):
    track_assembly(root, 1, x_in, width, zf, zr, y, R, name="Track_R")
    track_assembly(root, -1, x_in, width, zf, zr, y, R, name="Track_L")
    for s, nm in ((1, "R"), (-1, "L")):
        for sock, z in (("wheel_front", zf), ("wheel_rear", zr)):
            empty(f"{sock.replace('wheel', 'Wheel')}_{nm}", root, (s * (x_in + 0.5), y, z),
                  {"socket": sock + ("" if s > 0 else "_L"), "part": "wheel_track", "category": "Wheel",
                   "note": "track socket (idler front / sprocket rear); visuals live in Track_R/L"})


# ================================================================================================ EXCAVATOR
def excavator_arm(root, pos):
    tool = tool_root(root, "Tool", "tool", "tool_excavator_arm", pos, {"segments": "boom>stick>bucket"})
    ox, oy, oz = pos

    def W(x, y, z):
        return (ox + x, oy + y, oz + z)
    # boom: banana box section from the foot pin to the knuckle
    bm = MB("boom")
    prof = [(-2.4, -1.8), (2.0, -2.2), (9, 5.5), (17, 12.6), (25.6, 14.6), (26, 17.6), (23, 18.4), (14, 15.2), (6, 9.2), (-2.4, 2.2)]
    bm.prism([(oz + z, oy + y) for z, y in prof], "x", ox - 1.7, ox + 1.7, Y, 0.03, 2)
    bm.cyl(W(-2.4, 0, 0), W(2.4, 0, 0), 2.2, "steel_dark", 16)
    bm.cyl(W(-2.9, 0, 0), W(2.9, 0, 0), 0.8, "steel_bare", 10)
    for x in (-1.75, 1.75):
        bm.box(W(x - 0.15, 4, 4), W(x + 0.15, 10, 13), "steel_dark", 0.0)
    ram(bm, W(0, -1.5, 3.5), W(0, 9.5, 16.5), 1.0)
    ram(bm, W(0, 15.8, 15), W(0, 19.5, 27.5), 0.75)                                     # stick ram (on the boom)
    bm.hose(W(1.9, 2, 1), W(1.9, 15.5, 24), 0.3, "hose", -1.0, 8)
    bm.hose(W(-1.9, 2, 1), W(-1.9, 15.5, 24), 0.3, "hose", -1.0, 8)
    bm.rivets([W(1.75, 8 + i * 1.6, 9 + i * 2.1) for i in range(5)], (1, 0, 0), 0.25)
    boom = bm.finish(parent=tool, origin=W(0, 0, 0), props={"segment": "boom", "pivot_vox": [0, 0, 0]})
    # stick
    st = MB("stick")
    sp = [(22.6, 19.2), (25.5, 18.4), (43.6, -5.4), (41.6, -7.4), (39.8, -5.6), (22.4, 15.8)]
    st.prism([(oz + z, oy + y) for z, y in sp], "x", ox - 1.4, ox + 1.4, Y, 0.025, 2)
    st.cyl(W(-2.0, 16, 24), W(2.0, 16, 24), 1.8, "steel_dark", 14)
    st.cyl(W(-2.4, 16, 24), W(2.4, 16, 24), 0.7, "steel_bare", 10)
    ram(st, W(0, 20.5, 27), W(0, 4.5, 39.2), 0.6)
    st.cyl(W(-1.6, 4, 40), W(1.6, 4, 40), 0.5, "steel_dark", 8)                         # linkage
    st.hose(W(1.6, 18, 25), W(1.6, 1, 41), 0.25, "hose", 0.6, 6)
    stick = st.finish(parent=boom, origin=W(0, 16, 24), props={"segment": "stick", "pivot_vox": [0, 16, 24]})
    # bucket
    bk = MB("bucket")
    hd_bucket(bk, W(0, -14, 38), 5, 7, 1, "steel")
    bk.box(W(-2.2, -7.5, 39.5), W(2.2, -5, 43.5), "steel_dark", 0.01)                 # ears
    bk.cyl(W(-2.6, -6, 42), W(2.6, -6, 42), 0.7, "steel_bare", 10)
    bk.finish(parent=stick, origin=W(0, -6, 42), props={"segment": "bucket", "pivot_vox": [0, -6, 42]})
    return tool


def excavator():
    H.new_scene()
    root = empty("Excavator", props={"design": "Excavator", "voxel": 0.08, "crawler": True})
    b, gl = MB("Body"), MB("Glass")
    track_fenders(b, 9, 5, -24, 24, 9)
    b.box((-8.5, 2.5, -18.5), (8.5, 7.5, 18.5), "steel_dark", 0.02)
    b.cyl((0, 7.5, 0), (0, 9.6, 0), 9.0, "steel", 32)
    b.cyl((0, 9.0, 0), (0, 9.4, 0), 9.4, "steel_dark", 32)
    # house: rounded counterweight at the back
    plan = [(-14.5, 4.5), (14.5, 4.5)] + [(14.5 - 6 + math.cos(math.radians(a)) * 6, -20.5 + math.sin(math.radians(a)) * 6) for a in range(0, -91, -15)] + \
           [(-14.5 + 6 + math.cos(math.radians(a)) * 6, -20.5 + math.sin(math.radians(a)) * 6) for a in range(-90, -181, -15)]
    b.prism(plan, "y", 9.5, 20.5, Y, 0.04, 2)
    b.prism([(-14.7, -17.5), (14.7, -17.5)] + [(14.7 - 6 + math.cos(math.radians(a)) * 6.2, -20.5 + math.sin(math.radians(a)) * 6.2) for a in range(0, -91, -15)] +
            [(-14.7 + 6 + math.cos(math.radians(a)) * 6.2, -20.5 + math.sin(math.radians(a)) * 6.2) for a in range(-90, -181, -15)],
            "y", 10, 17.5, "steel_dark", 0.03, 2)                                         # counterweight
    for s in (-1, 1):
        b.box((min(s * 14.5, s * 14.8), 14.6, -14.5), (max(s * 14.5, s * 14.8), 15.4, 4.5), "black", 0.0)
        for z in (-12, -2):
            b.seam((s * 14.55, 11, z), (s * 14.55, 19.5, z), (s, 0, 0))
        b.box((min(s * 14.5, s * 14.8), 16, -11), (max(s * 14.5, s * 14.8), 19, -3), "void", 0.0)   # side grille
        for z in range(-10, -3, 1):
            b.box((min(s * 14.5, s * 14.9), 16, z - 0.15), (max(s * 14.5, s * 14.9), 19, z + 0.15), "steel_dark", 0.0)
    for z in range(-16, -5, 2):
        b.box((-12, 20.5, z - 0.35), (12, 20.8, z + 0.35), "steel_dark", 0.0)
    for x in (-13.5, -4):                                                                 # handrails on the deck
        b.cyl((x, 20.5, -18), (x, 23, -18), 0.25, "paint_crimson", 6)
        b.cyl((x, 20.5, -2), (x, 23, -2), 0.25, "paint_crimson", 6)
        b.cyl((x, 23, -18), (x, 23, -2), 0.25, "paint_crimson", 6)
    b.cyl((-8, 20.5, -12), (-8, 26.5, -12), 1.0, "black", 10)
    b.cyl((-8, 26.5, -12), (-8, 27, -11.6), 1.05, "rust", 10)
    b.text("MM-320", (14.85, 17.8, 0.5), (1, 0, 0), 1.6, "black", 0.1)
    b.text("MM-320", (-14.85, 17.8, 0.5), (-1, 0, 0), 1.6, "black", 0.1)
    op_cab(b, gl, 9, 5, 22, 10, 27)
    for s in (-1, 1):                                                                    # Lamps(g, 8, 24, 23, -27): rear pair on the counterweight
        b.lamp((s * 7.5, 24.5, 23.4), (0, 0, 1), 0.8, "lightw", "black", 0.5, square=True)
        b.lamp((s * 7.5, 15.5, -27.1), (0, 0, -1), 0.6, "tail", "black", 0.4, square=True)
    b.box((6, 10, 9), (9.5, 20, 23), "steel_dark", 0.01)                                  # boom foot housing
    body = b.finish(parent=root, origin=(0, 0, 0), props={"part": "body"})
    gl.finish(parent=body, origin=(0, 0, 0))
    finish_tracks(root, 9.5, 6, 19, -19, 4)
    excavator_arm(root, (11, 17, 18))
    return root


def excavator_pose(root):
    """Extra tile: the arm segments posed (slew -45, boom raised 22, stick 30, bucket curled 75)."""
    tool = [o for o in root.children if o.name == "Tool"][0]
    boom = [c for c in tool.children if c.name == "boom"][0]
    stick = boom.children[0]
    bucket = stick.children[0]
    tool.rotation_euler.z = math.radians(15)
    boom.rotation_euler.x = math.radians(-22)
    stick.rotation_euler.x = math.radians(30)
    bucket.rotation_euler.x = math.radians(75)
    import bpy
    bpy.context.view_layer.update()
    import render_common as rc
    rc.render_tile("Excavator_posed", H.tree(root), H.OUT, azim=70)
    tool.rotation_euler.z = boom.rotation_euler.x = stick.rotation_euler.x = bucket.rotation_euler.x = 0


# ================================================================================================ BACKHOE LOADER
def backhoe():
    H.new_scene()
    root = empty("Backhoe", props={"design": "Backhoe", "voxel": 0.08})
    b, gl = MB("Body"), MB("Glass")
    b.prism([(-26.5, 6.5), (24.5, 6.5), (24.5, 15.5), (22, 17.5), (0.5, 17.5), (0.5, 12.5), (-26.5, 12.5)], "x", -9.5, 9.5, Y, 0.05, 2)
    b.grille(-8, 8, 8, 16, 24.6, "steel_dark", "steel", 9, "void", False)
    for s in (-1, 1):
        for z in range(4, 21, 2):
            b.box((min(s * 9.5, s * 9.8), 13, z - 0.35), (max(s * 9.5, s * 9.8), 16, z + 0.35), "void", 0.0)
        b.prism(arc_ring(-14, 8, 8.6, 9.4, 0, 180, 16), "x", min(s * 9.5, s * 17.5), max(s * 9.5, s * 17.5), "steel", 0.01)
        b.box((min(s * 9.5, s * 17.5), 17.5, -24.5), (max(s * 9.5, s * 17.5), 18.5, -3.5), "steel", 0.01)
        b.prism(arc_ring(16, 7, 7.3, 8.0, 10, 170, 12), "x", min(s * 9.5, s * 15.5), max(s * 9.5, s * 15.5), "black", 0.005)
        b.box((min(s * 9.5, s * 12), 4, -27.5), (max(s * 9.5, s * 12), 8, -24), "steel_dark", 0.01)    # stabiliser housing
        b.cyl((s * 12, 6, -26), (s * 16, 0.6, -27), 0.9, Y, 10)                                    # stabiliser legs
        b.box((s * 16 - 1.8, -0.5, -29), (s * 16 + 1.8, 0.7, -25), "steel_dark", 0.01)
    b.box((-9.5, 3.5, 22), (9.5, 7, 25.5), "steel_dark", 0.02)                                    # front counterweight
    op_cab(b, gl, 10, -20, -2, 12, 30)
    b.cyl((6, 17.5, 18), (6, 30, 18), 0.9, "black", 10)
    b.ball((6, 30, 18), 1.1, "black", 8, 5, 0.6)
    lamps(b, 7, 15, 25, -27)
    b.text("MM 4CX", (9.85, 9.5, -12), (1, 0, 0), 1.6, "black", 0.1)
    body = b.finish(parent=root, origin=(0, 0, 0), props={"part": "body"})
    gl.finish(parent=body, origin=(0, 0, 0))
    add_wheels(root, [("wheel_front", 10, 7, 16, True)], "wheel_offroad", 6.5, 5, 3.6, "offroad", "paint_cat")
    add_wheels(root, [("wheel_rear", 10, 8, -14, True)], "wheel_truck_cat", 6.7, 5, 4.0, "truck", "paint_cat")
    # front loader: arms > bucket
    pos = (0, 16, 18)
    tool = tool_root(root, "Tool", "tool", "tool_backhoe_loader", pos, {"segments": "arms>bucket"})
    ox, oy, oz = pos

    def W(x, y, z):
        return (ox + x, oy + y, oz + z)
    am = MB("arms")
    for x in (-11, 11):
        am.prism([(oz - 1.6, oy + 1.2), (oz + 1.2, oy + 1.6), (oz + 14.8, oy - 3.0), (oz + 13.2, oy - 5.0), (oz - 1.6, oy - 1.4)], "x", ox + x - 0.8, ox + x + 0.8, Y, 0.02)
        am.cyl(W(x - 1.2, 0, 0), W(x + 1.2, 0, 0), 1.2, "steel_dark", 12)
        ram(am, W(x * 0.85, -4, -6), W(x * 0.85, -1.2, 6), 0.6)
    am.cyl(W(-11, -2, 7), W(11, -2, 7), 0.8, Y, 12)
    arms = am.finish(parent=tool, origin=W(0, 0, 0), props={"segment": "arms", "pivot_vox": [0, 0, 0]})
    bk = MB("bucket")
    hd_bucket(bk, W(0, -10, 12), 12, 6, 1, Y)
    for x in (-11, 11):
        bk.box(W(x - 0.8, -5.5, 12.5), W(x + 0.8, -3, 15.5), "steel_dark", 0.01)
    bk.finish(parent=arms, origin=W(0, -4, 14), props={"segment": "bucket", "pivot_vox": [0, -4, 14]})
    # rear hoe: swing > boom > stick > bucket
    pos = (0, 13, -28)
    tool2 = tool_root(root, "Tool_rear", "tool_rear", "tool_hoe_arm", pos, {"segments": "swing>boom>stick>bucket"})
    ox, oy, oz = pos
    sw = MB("swing")
    sw.cyl(W(0, -3, 0), W(0, 2.5, 0), 2.6, "steel_dark", 16)
    sw.box(W(-2.2, -2, -3.5), W(2.2, 3, 0.5), Y, 0.02)
    swing = sw.finish(parent=tool2, origin=W(0, 0, 0), props={"segment": "swing", "pivot_vox": [0, 0, 0]})
    bo = MB("boom")
    bo.prism([(oz - 1.2, oy + 2.2), (oz - 2.8, oy - 0.2), (oz - 10, oy + 10), (oz - 18.6, oy + 12.2), (oz - 18.6, oy + 14.4), (oz - 9, oy + 13.4)], "x", ox - 1.3, ox + 1.3, Y, 0.025, 2)
    ram(bo, W(0, -1, -4), W(0, 9.5, -12.5), 0.55)
    bo.hose(W(1.4, 2, -3), W(1.4, 13, -17), 0.25, "hose", -0.6, 6)
    boom = bo.finish(parent=swing, origin=W(0, 1, -2), props={"segment": "boom", "pivot_vox": [0, 1, -2]})
    sk = MB("stick")
    sk.prism([(oz - 17.4, oy + 14.2), (oz - 19.2, oy + 12.4), (oz - 28.6, oy - 3.4), (oz - 27, oy - 4.0), (oz - 16.6, oy + 12.6)], "x", ox - 1.1, ox + 1.1, Y, 0.02)
    sk.cyl(W(-1.6, 13, -18), W(1.6, 13, -18), 1.2, "steel_dark", 12)
    ram(sk, W(0, 15, -18.5), W(0, 1, -26), 0.45)
    stick = sk.finish(parent=boom, origin=W(0, 13, -18), props={"segment": "stick", "pivot_vox": [0, 13, -18]})
    bk2 = MB("bucket")
    hd_bucket(bk2, W(0, -9, -25), 4, 5, -1, "steel")
    bk2.cyl(W(-1.8, -3, -28), W(1.8, -3, -28), 0.6, "steel_bare", 10)
    bk2.finish(parent=stick, origin=W(0, -3, -28), props={"segment": "bucket", "pivot_vox": [0, -3, -28]})
    return root


# ================================================================================================ BULLDOZER
def bulldozer():
    H.new_scene()
    root = empty("Bulldozer", props={"design": "Bulldozer", "voxel": 0.08, "crawler": True})
    b, gl = MB("Body"), MB("Glass")
    track_fenders(b, 9, 6, -24, 24, 10)
    b.prism([(-22.5, 3.5), (22.5, 3.5), (22.5, 15.5), (21, 16.5), (-3.5, 16.5), (-3.5, 11.5), (-22.5, 11.5)], "x", -9.5, 9.5, Y, 0.05, 2)
    b.grille(-8, 8, 5, 15, 22.6, "steel_dark", "steel", 9, "void", False)
    for s in (-1, 1):
        for z in range(0, 19, 2):
            b.box((min(s * 9.5, s * 9.8), 12, z - 0.35), (max(s * 9.5, s * 9.8), 15, z + 0.35), "void", 0.0)
        b.seam((s * 9.55, 5, -2), (s * 9.55, 16, -2), (s, 0, 0))
        b.seam((s * 9.55, 5, 10), (s * 9.55, 16, 10), (s, 0, 0))
    for z in (2, 8, 14):                                                                     # hood grab rails
        b.cyl((-7, 16.5, z), (-7, 17.5, z), 0.25, "paint_crimson", 6)
    b.cyl((-7, 17.5, 2), (-7, 17.5, 14), 0.25, "paint_crimson", 6)
    b.box((-6, 16.5, 16), (6, 17.4, 21), "steel_dark", 0.02)                                 # precleaner box
    b.cyl((4, 16.5, 10), (4, 24, 10), 1.0, "black", 10)
    b.cyl((4, 24, 10), (4.4, 24.5, 10.2), 1.05, "rust", 10)
    # rear ripper (decor, part of the body)
    b.box((-8, 4, -25.5), (8, 8, -22.5), "steel_dark", 0.02)
    for x in (-5, 0, 5):
        b.prism([(-25.5, 7), (-27.5, 7), (-28.5, -0.5), (-27, -1), (-25.5, 2)], "x", x - 0.6, x + 0.6, "steel_bare", 0.01)
    op_cab(b, gl, 9, -22, -4, 11, 29)
    lamps(b, 7, 14, 23, -23)
    b.text("MM D8", (9.85, 7.5, -12), (1, 0, 0), 1.8, "black", 0.1)
    body = b.finish(parent=root, origin=(0, 0, 0), props={"part": "body"})
    gl.finish(parent=body, origin=(0, 0, 0))
    finish_tracks(root, 9.5, 7, 19, -19, 4)
    # blade: lift > blade
    pos = (0, 0, 30)
    tool = tool_root(root, "Tool", "tool", "tool_dozer_blade", pos, {"segments": "lift>blade"})
    ox, oy, oz = pos

    def W(x, y, z):
        return (ox + x, oy + y, oz + z)
    lf = MB("lift")
    for x in (-12, 12):
        lf.box(W(x - 1.0, 3, -14.5), W(x + 1.0, 5.2, -0.5), Y, 0.02)
        lf.cyl(W(x - 1.4, 4, -14), W(x + 1.4, 4, -14), 1.2, "steel_dark", 12)
    for x in (-6, 6):
        ram(lf, W(x, 12, -12), W(x, 10, -2), 0.6)
    lift = lf.finish(parent=tool, origin=W(0, 4, -14), props={"segment": "lift", "pivot_vox": [0, 4, -14]})
    bl = MB("blade")
    curve = [(3 - math.sin(y / 12 * math.pi) * 2.5, y) for y in [i * 1.0 for i in range(13)]]
    front = [(oz + z + 0.5, oy + y) for z, y in curve]
    back = [(oz + z - 0.8, oy + y) for z, y in curve][::-1]
    bl.prism(front + back, "x", ox - 19.5, ox + 19.5, Y, 0.02)
    bl.box(W(-19.5, -0.5, 2.0), W(19.5, 0.8, 4.4), "steel_bare", 0.005)                       # cutting edge
    for x in (-19.5, 19.5):
        bl.prism([(oz + z - 1.0, oy + y) for z, y in curve] + [(oz - 1.5, oy + 12), (oz - 1.5, oy - 0.5)], "x", x - 0.5, x + 0.5, "steel_dark", 0.01)
    for x in range(-16, 17, 4):                                                                # back ribs
        bl.prism([(oz + 0.0, oy + 0.5), (oz - 2.5, oy + 2), (oz - 2.5, oy + 10), (oz + 0.0, oy + 11.5)], "x", ox + x - 0.4, ox + x + 0.4, "steel_dark", 0.004)
    bl.rivets([W(x, 11.2, 0.6) for x in range(-18, 19, 3)], (0, 0, 1), 0.25)
    bl.finish(parent=lift, origin=W(0, 4, 0), props={"segment": "blade", "pivot_vox": [0, 4, 0]})
    return root


# ================================================================================================ DUMP TRUCK
def dump_truck():
    H.new_scene()
    root = empty("DumpTruck", props={"design": "DumpTruck", "voxel": 0.08})
    b, gl = MB("Body"), MB("Glass")
    for s in (-1, 1):
        b.vbox(s * 6, 9, -48, s * 8, 12, 44, "steel_dark", 0.01)
    for z in range(-44, 41, 8):
        b.vbox(-6, 10, z, 6, 11, z, "steel_dark", 0.0)
    b.prism(rrect_top_safe(-13.5, 13.5, 9.5, 18.5, 1.5), "z", 21.5, 52.5, Y, 0.03, 2)
    b.grille(-10.5, 10.5, 10.5, 16.5, 52.6, "steel_dark", "steel", 12, "void")
    for s in (-1, 1):
        b.prism(arc_ring(40, 8, 7.4, 8.4, 0, 180, 16), "x", min(s * 11.5, s * 16.5), max(s * 11.5, s * 16.5), "black", 0.01)
        b.box((min(s * 11.5, s * 16.5), 16, -45.5), (max(s * 11.5, s * 16.5), 16.8, -11.5), "black", 0.01)
        b.box((min(s * 13.5, s * 15.5), 11, 21.5), (max(s * 13.5, s * 15.5), 12, 31), "tread", 0.005)      # steps
        b.box((min(s * 13.5, s * 15.5), 15, 21.5), (max(s * 13.5, s * 15.5), 16, 31), "tread", 0.005)
        b.lamp((s * 13.5, 19.5, 34), (s, 0, 0), 0.5, "amber", "black", 0.3, seg=8)
        C.fuel_tank(b, s * 11, 12, 4, 16, 2.4, "steel")
        C.mud_flap(b, min(s * 11.5, s * 16), max(s * 11.5, s * 16), 2.5, 12.5, -46.5)
        C.mirror_arm(b, (s * 13.6, 26, 51.6), s * 2.0, 2.0)
    b.box((-14.5, 7, 52.5), (14.5, 10.5, 55), "steel_dark", 0.02)                               # front bumper
    op_cab(b, gl, 12, 32, 51, 18, 34)
    lamps(b, 11, 14, 53, -49)
    b.box((-3, 13, -46), (3, 16, -40), "steel_dark", 0.01)                                      # tipping ram base
    b.box((-9, 12, 21.5), (9, 13, 31.5), "steel", 0.01)
    body = b.finish(parent=root, origin=(0, 0, 0), props={"part": "body"})
    gl.finish(parent=body, origin=(0, 0, 0))
    add_wheels(root, [("wheel_front", 12, 8, 40, True), ("wheel_mid", 12, 8, -20, True), ("wheel_rear", 12, 8, -36, True)],
               "wheel_truck_cat", 6.7, 5, 4.0, "truck", "paint_cat")
    # tipper bed: one rigid part hinged at its rear edge (the socket)
    pos = (0, 13, -48)
    tb = MB("Tool")
    ox, oy, oz = pos

    def W(x, y, z):
        return (ox + x, oy + y, oz + z)
    tb.box(W(-14.5, -0.5, -0.5), W(14.5, 1.5, 48.5), "steel", 0.02)
    for s in (-1, 1):
        tb.prism([(oz - 0.5, oy + 1.5), (oz + 48.5, oy + 1.5), (oz + 48.5, oy + 11.5), (oz + 2, oy + 11.5), (oz - 0.5, oy + 9.5)], "x",
                 min(s * 14, s * 15), max(s * 14, s * 15), Y, 0.02)
        for z in range(4, 45, 8):
            tb.box(W(min(s * 15, s * 15.6), 2, z - 0.4), W(max(s * 15, s * 15.6), 11.5, z + 0.4), "steel_dark", 0.004)
        tb.box(W(min(s * 15, s * 15.6), 10.8, -0.5), W(max(s * 15, s * 15.6), 11.8, 48.5), "steel_dark", 0.004)
    tb.box(W(-14.5, 1.5, 47.5), W(14.5, 14.5, 49.5), Y, 0.02)                                 # headboard
    tb.box(W(-14.5, 13.5, 49.5), W(14.5, 14.5, 54.5), "steel", 0.02)                           # cab guard
    for x in range(-12, 13, 4):
        tb.box(W(x - 0.4, 12, 49.5), W(x + 0.4, 13.5, 54.5), "steel_dark", 0.0)
    tb.box(W(-14, 1.5, -0.5), W(14, 9.5, 0.5), "rust", 0.01)                                   # tailgate
    for x in (-12, 12):
        tb.cyl(W(x, 10, -0.8), W(x, 10, 0.8), 0.6, "steel_dark", 8)
    tb.box(W(-13.5, 1.5, 2), W(13.5, 4, 40), "soil", 0.3, 3)                                  # dirt load
    tb.finish(parent=root, origin=pos, props={"socket": "tool", "part": "tool_dump_bed", "category": "Tool", "hinge": "rear edge"})
    return root


def rrect_top_safe(x0, x1, y0, y1, r):
    return H.rrect_top(x0, x1, y0, y1, r)


# ================================================================================================ PAVER
def paver():
    H.new_scene()
    root = empty("Paver", props={"design": "Paver", "voxel": 0.08, "crawler": True})
    b, gl = MB("Body"), MB("Glass")
    track_fenders(b, 8, 5, -18, 18, 9)
    b.prism(rrect_top_safe(-9.5, 9.5, 4.5, 14.5, 1.2), "z", -20.5, 18.5, Y, 0.03, 2)
    for s in (-1, 1):
        for z in range(-16, 15, 3):
            b.box((min(s * 9.5, s * 9.8), 8, z - 0.4), (max(s * 9.5, s * 9.8), 12.5, z + 0.4), "void", 0.0)
    b.box((-9.5, 14.5, -14.5), (9.5, 15.5, 6.5), "grating", 0.01)                              # platform
    for x in (-9, 9):
        for z in (-14, 6):
            b.box((x - 0.5, 15.5, z - 0.5), (x + 0.5, 30.5, z + 0.5), "steel", 0.006)
    b.box((-10.5, 30.5, -15.5), (10.5, 31.5, 7.5), "hazard", 0.02)
    b.box((-10.5, 31.5, -15.5), (10.5, 31.8, 7.5), "steel_dark", 0.0)
    for s in (-1, 1):                                                                            # railings
        b.cyl((s * 9.6, 21, -14), (s * 9.6, 21, 6), 0.3, "paint_crimson", 6)
    b.box((-3.5, 15.5, 2.5), (3.5, 20.5, 5.5), "black", 0.02)                                  # control console
    for i in range(4):
        b.cyl((-2 + i * 1.3, 20.5, 4), (-2 + i * 1.3, 21.2, 4), 0.3, ("amber", "lightw", "tail", "lightw")[i], 6)
    mb_seat(b, 15.5, -10, 6, 9)
    b.cyl((-6, 14.5, -16), (-6, 26, -16), 1.0, "black", 10)
    b.box((-9.5, 12, 18.5), (9.5, 14.5, 20.5), "steel_dark", 0.01)                             # push rollers
    for x in (-6, 6):
        b.cyl((x - 2, 8, 20.5), (x + 2, 8, 20.5), 1.6, "rubber", 14)
    lamps(b, 8, 12, 19, -21)
    body = b.finish(parent=root, origin=(0, 0, 0), props={"part": "body"})
    gl.finish(parent=body, origin=(0, 0, 0))
    finish_tracks(root, 8.5, 6, 13, -13, 4)
    pos = (0, 1, -21)
    t = MB("Tool")
    ox, oy, oz = pos

    def W(x, y, z):
        return (ox + x, oy + y, oz + z)
    t.box(W(-19.5, -0.5, -8.5), W(19.5, 3.5, 0.5), "steel", 0.02)
    t.box(W(-19.5, -0.5, -8.6), W(19.5, 0.3, -7.6), "steel_bare", 0.0)
    for x in (-19.5, 19.5):
        t.box(W(x - 0.5, -0.5, -8.5), W(x + 0.5, 5.5, 0.5), "hazard", 0.01)
        t.lamp(W(x, 5.6, -4), (0, 1, 0), 0.6, "amber", "black", 0.3, seg=8)
    t.prism([(oz - 6.5, oy + 3.5), (oz + 10.5, oy + 3.5), (oz + 10.5, oy + 12.5), (oz - 6.5, oy + 12.5)], "x", ox - 16.5, ox + 16.5, Y, 0.03, 2)
    t.box(W(-14.5, 11.5, -4.5), W(14.5, 12.8, 8.5), "hot_mix", 0.15, 3)
    t.cyl(W(-18, 2, -9.5), W(18, 2, -9.5), 1.0, "steel_dark", 12)
    t.text("HOT", W(16.85, 8, 2), (1, 0, 0), 2.0, "black", 0.1)
    t.finish(parent=root, origin=pos, props={"socket": "tool", "part": "tool_paver_screed", "category": "Tool"})
    return root


# ================================================================================================ ROLLER
def roller():
    H.new_scene()
    root = empty("Roller", props={"design": "Roller", "voxel": 0.08})
    b, gl = MB("Body"), MB("Glass")
    for zc in (20, -20):
        for s in (-1, 1):
            b.box((min(s * 15.5, s * 16.5), 5.5, zc - 5.5), (max(s * 15.5, s * 16.5), 17.5, zc + 5.5), Y, 0.02)
            b.cyl((s * 16.5, 8, zc), (s * 17.2, 8, zc), 2.0, "steel_dark", 14)
        b.box((-16.5, 16.5, zc - 5.5), (16.5, 17.5, zc + 5.5), Y, 0.02)
    b.prism(rrect_top_safe(-12.5, 12.5, 11.5, 18.5, 1.5), "z", -12.5, 12.5, Y, 0.03, 2)
    b.box((-16.5, 17.5, -18.5), (16.5, 18.5, 18.5), "grating", 0.01)
    for x in (-10, 10):
        for z in (-12, 4):
            b.box((x - 0.5, 18.5, z - 0.5), (x + 0.5, 33.5, z + 0.5), "steel", 0.006)
    b.box((-11.5, 33.5, -13.5), (11.5, 34.5, 5.5), "hazard", 0.02)
    b.box((-11.5, 34.5, -13.5), (11.5, 34.8, 5.5), "steel_dark", 0.0)
    b.cyl((-9, 34.8, -12), (-9, 35.5, -12), 0.8, "black", 10)
    b.cyl((-9, 35.5, -12), (-9, 36.8, -12), 0.7, "amber", 10, r2=0.5)
    for s in (-1, 1):
        for z in range(-9, 10, 3):
            b.box((min(s * 12.5, s * 12.8), 13, z - 0.4), (max(s * 12.5, s * 12.8), 17, z + 0.4), "void", 0.0)
        b.cyl((s * 15, 21.5, -17), (s * 15, 21.5, 17), 0.3, "paint_crimson", 6)
        for z in (-17, 0, 17):
            b.cyl((s * 15, 18.5, z), (s * 15, 21.5, z), 0.3, "paint_crimson", 6)
        b.cyl((s * 4, 24, 3), (s * 4, 18.5, 1), 0.2, "steel_dark", 6)
    b.box((-3, 18.5, 0), (3, 23, 3), "black", 0.02)
    b.lathe([(1.6, 0), (2.0, 0.5), (1.6, 1)], (0, 23.5, 0.8), (0, 24, 1.6), "black", seg=16)       # steering wheel
    mb_seat(b, 18.5, -12, 2, 9)
    b.cyl((8, 18.5, -10), (8, 27, -10), 0.9, "black", 10)
    b.box((-15, 8, 26.5), (15, 9, 27.5), "steel_dark", 0.0)                                    # scrapers
    b.box((-15, 8, -27.5), (15, 9, -26.5), "steel_dark", 0.0)
    lamps(b, 10, 15, 29, -29)
    b.text("MM CB-54", (12.85, 15, 0), (1, 0, 0), 1.4, "black", 0.1)
    # water tank + spray bars over the drums, articulation joint, hoses
    b.prism(rrect_top_safe(-9, 9, 18.5, 24, 2.0), "z", 6, 12, "paint_cream", 0.03, 2)
    b.cyl((0, 24, 9), (0, 24.6, 9), 1.2, "steel_dark", 12)
    for zc in (20, -20):
        sz = 1 if zc > 0 else -1
        b.cyl((-14, 15.5, zc + sz * 7.5), (14, 15.5, zc + sz * 7.5), 0.35, "steel_bare", 8)
        for x in range(-12, 13, 4):
            b.cyl((x, 15.5, zc + sz * 7.5), (x, 14.6, zc + sz * 7.8), 0.2, "black", 6)
        b.hose((6, 18, sz * 6), (10, 15.5, zc + sz * 7.5), 0.3, "hose", 1.0, 6)
    b.cyl((0, 10, -13), (0, 16, -13), 1.6, "steel_dark", 14)                                  # articulation joint
    for s in (-1, 1):
        ram(b, (s * 5, 13, -12.5), (s * 8, 13, -16.5), 0.6)
    b.text("MM CB-54", (-12.85, 15, 0), (-1, 0, 0), 1.4, "black", 0.1)
    body = b.finish(parent=root, origin=(0, 0, 0), props={"part": "body"})
    gl.finish(parent=body, origin=(0, 0, 0))
    for zc, nm in ((20, "Drum_front"), (-20, "Drum_rear")):
        d = MB(nm)
        d.lathe([(5.8, 0.0), (7.2, 0.01), (7.2, 0.99), (5.8, 1.0)], (-15.4, 8, zc), (15.4, 8, zc), "steel_bare", seg=64)
        d.lathe([(5.8, 0), (5.8, 1)], (-15.4, 8, zc), (15.4, 8, zc), "steel_dark", seg=48)
        for x in (-15.2, 15.2):
            d.cyl((x - 0.2, 8, zc), (x + 0.2, 8, zc), 5.9, Y, 32)
            for k in range(8):
                a = 2 * math.pi * k / 8
                d.cyl((x, 8 + math.sin(a) * 3.5, zc + math.cos(a) * 3.5), (x * 1.01, 8 + math.sin(a) * 3.5, zc + math.cos(a) * 3.5), 0.45, "steel_dark", 6)
        for x in range(-12, 13, 6):                                                          # weld seams around the shell
            d.lathe([(7.2, 0), (7.3, 0.5), (7.2, 1)], (x - 0.15, 8, zc), (x + 0.15, 8, zc), "steel", seg=64)
        d.finish(parent=root, origin=(0, 8, zc), props={"note": "steel drum (VoxelGrid body in the game); the wheel_truck sockets sit inside it",
                                                       "wheel_sockets": "wheel_front/wheel_rear at (±9, 8, ±20)"})
    return root


BUILDERS = {
    "Excavator": (excavator, "Excavator", "VehicleDesigns.Excavator + tool_excavator_arm (boom/stick/bucket)", True),
    "Backhoe": (backhoe, "Backhoe loader", "VehicleDesigns.Backhoe + tool_backhoe_loader (arms/bucket) + tool_hoe_arm (swing/boom/stick/bucket)", False),
    "Bulldozer": (bulldozer, "Bulldozer", "VehicleDesigns.Bulldozer + tool_dozer_blade (lift/blade)", False),
    "DumpTruck": (dump_truck, "Dump truck", "VehicleDesigns.DumpTruck + tool_dump_bed", False),
    "Paver": (paver, "Paver", "VehicleDesigns.Paver + tool_paver_screed", False),
    "Roller": (roller, "Roller", "VehicleDesigns.Roller (tandem drums)", False),
}


def run(names):
    for n in names:
        fn, label, replaces, posed = BUILDERS[n]
        root = fn()
        extra = [lambda r=root: excavator_pose(r)] if posed else []
        H.save_and_render(root, label, replaces, extra)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    run(argv or list(BUILDERS))
