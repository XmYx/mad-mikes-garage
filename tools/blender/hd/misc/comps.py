"""Shared HD components: engines (one per engine part key), propellers, lamps, small fittings.

All builders return one object named after the game part key, origin = the part's mount point (game convention),
front = Blender -Y, up = +Z, sized from the voxel original (PartLibrary*, 1 voxel = 0.08 m)."""
import math

from mathutils import Vector

import kit
from kit import Part, V, bm_box, bm_cyl, bm_tube, bm_lathe, finned_cylinder, oriented


def _bolts(p, M, pts, r=0.008, axis="Z"):
    for q in pts:
        p.cyl(M["chrome"], r, 0.01, q, axis, 6, bevel=0.0015)


def _pulley(p, M, loc, r, w=0.03, mat=None):
    p.lathe(mat or M["black"], [(r * 0.3, -w / 2), (r, -w / 2), (r * 0.92, -w * 0.15), (r * 0.92, w * 0.15), (r, w / 2), (r * 0.3, w / 2), (r * 0.3, -w / 2)], loc, "Y", 28)


def _air_cleaner(p, M, loc, r, h, mat=None, axis="Z"):
    p.cyl(mat or M["chrome"], r, h * 0.18, Vector(loc) + (Vector((0, 0, -h * 0.4)) if axis == "Z" else Vector((-h * 0.4, 0, 0))), axis, 32, bevel=0.004)
    p.cyl(M["black"], r * 0.92, h * 0.64, loc, axis, 32, bevel=0.0)
    p.cyl(mat or M["chrome"], r, h * 0.18, Vector(loc) + (Vector((0, 0, h * 0.4)) if axis == "Z" else Vector((h * 0.4, 0, 0))), axis, 32, bevel=0.004)
    off = Vector((0, 0, h * 0.52)) if axis == "Z" else Vector((h * 0.52, 0, 0))
    p.cyl(M["chrome"], r * 0.12, h * 0.1, Vector(loc) + off, axis, 6, bevel=0.002)


def inline_engine(key, M, w, h, l, cyls, block_mat, cover_mat, diesel=False, turbo=False, fan=False, filter_r=None):
    """Inline engine: block + head + ribbed valve cover, exhaust manifold on -X, intake on +X, pulleys/fan at the front."""
    p = Part(key)
    yb = -l / 2
    # oil pan, block (with ribs), head
    p.box(block_mat, (w * 0.7, l * 0.86, h * 0.16), (0, yb, h * 0.08), bevel=0.012)
    p.box(block_mat, (w * 0.86, l * 0.96, h * 0.42), (0, yb, h * 0.16 + h * 0.21), bevel=0.014)
    for i in range(cyls + 1):
        y = -l * 0.04 - i * l * 0.92 / cyls
        p.box(block_mat, (w * 0.9, 0.018, h * 0.36), (0, y, h * 0.36), bevel=0.004)
    p.box(block_mat, (w * 0.8, l * 0.94, h * 0.14), (0, yb, h * 0.64), bevel=0.01)
    p.box(M["black"], (w * 0.82, l * 0.95, 0.006), (0, yb, h * 0.715), bevel=0)  # head gasket line
    p.box(cover_mat, (w * 0.62, l * 0.86, h * 0.14), (0, yb, h * 0.79), bevel=0.03, segs=3)
    for i in range(6):
        p.box(cover_mat, (w * 0.5, 0.012, 0.012), (0, yb - l * 0.3 + i * l * 0.12, h * 0.865), bevel=0.003)
    _bolts(p, M, [(sx * w * 0.28, yb + sy * l * 0.38, h * 0.86) for sx in (-1, 1) for sy in (-1, 0, 1)], 0.007)
    p.cyl(M["black"], 0.018, 0.03, (w * 0.12, yb - l * 0.36, h * 0.88), "Z", 12)  # oil filler cap
    # exhaust manifold (-X)
    ports = [(-w * 0.42, -l * 0.08 - i * l * 0.84 / max(cyls - 1, 1), h * 0.56) for i in range(cyls)]
    col_y = -l * 0.5
    for q in ports:
        p.tube(M["rust"], [q, (q[0] - 0.05, q[1], q[2] - 0.02), (-w * 0.56, q[1] * 0.6 + col_y * 0.4, h * 0.4)], 0.022, 10)
    p.tube(M["rust"], [(-w * 0.56, -l * 0.06, h * 0.4), (-w * 0.56, -l * 0.94, h * 0.4), (-w * 0.56, -l * 1.0, h * 0.25), (-w * 0.5, -l * 1.0, h * 0.02)], 0.03, 12)
    # intake runners (+X) to a plenum
    for q in ports:
        p.tube(M["alu"], [(w * 0.42, q[1], q[2]), (w * 0.52, q[1], q[2] + 0.04), (w * 0.56, q[1], h * 0.7)], 0.018, 8)
    p.box(M["alu"], (0.08, l * 0.84, 0.08), (w * 0.58, yb, h * 0.72), bevel=0.02)
    if turbo:
        tc = Vector((-w * 0.62, -l * 0.86, h * 0.72))
        p.lathe(M["iron"], [(0.0, -0.05), (0.075, -0.05), (0.09, -0.02), (0.09, 0.02), (0.075, 0.05), (0.0, 0.05)], tc, "X", 24)
        p.lathe(M["alu"], [(0.0, 0.05), (0.07, 0.05), (0.085, 0.09), (0.07, 0.13), (0.0, 0.13)], tc, "X", 24)
        p.tube(M["alu"], [tc + Vector((0.1, 0, 0)), tc + Vector((0.16, 0.0, 0.12)), (w * 0.3, -l * 0.86, h * 0.95), (w * 0.58, -l * 0.8, h * 0.8)], 0.035, 12)
    else:
        fr = filter_r or w * 0.3
        _air_cleaner(p, M, (w * 0.2, -l * 0.3, h * 0.95), fr, 0.09, M["black"])
        p.tube(M["black"], [(w * 0.2, -l * 0.3, h * 0.9), (w * 0.45, -l * 0.35, h * 0.85), (w * 0.58, -l * 0.4, h * 0.76)], 0.02, 8)
    if diesel:
        for q in ports:
            p.tube(M["steel"], [(w * 0.2, q[1], h * 0.86), (w * 0.3, q[1], h * 0.9), (w * 0.38, -l * 0.95, h * 0.6)], 0.004, 5, caps=False)
        p.box(M["iron"], (0.1, 0.16, 0.12), (w * 0.4, -l * 0.95, h * 0.5), bevel=0.01)  # injection pump
    # front: crank pulley, water pump, alternator, belt
    fy = -l - 0.02
    _pulley(p, M, (0, fy, h * 0.22), h * 0.16)
    _pulley(p, M, (0, fy, h * 0.55), h * 0.12, mat=M["steel"])
    p.cyl(M["alu"], h * 0.12, 0.12, (w * 0.42, -l + 0.06, h * 0.58), "Y", 20, bevel=0.01)  # alternator
    for i in range(8):
        a = i / 8 * math.tau
        p.box(M["alu"], (0.006, 0.08, 0.02), (w * 0.42 + math.cos(a) * h * 0.12, -l + 0.06, h * 0.58 + math.sin(a) * h * 0.12), (0, a, 0), bevel=0)
    _pulley(p, M, (w * 0.42, fy, h * 0.58), h * 0.06, 0.025)
    p.tube(M["rubber"], [(0, fy, h * 0.06), (w * 0.44, fy, h * 0.52), (w * 0.4, fy, h * 0.66), (0, fy, h * 0.67), (-h * 0.13, fy, h * 0.5), (-h * 0.16, fy, h * 0.2), (0, fy, h * 0.06)], 0.008, 6, caps=False)
    if fan:
        hub = Vector((0, fy - 0.06, h * 0.5))
        p.cyl(M["steel"], 0.05, 0.05, hub, "Y", 16, bevel=0.005)
        for i in range(6):
            a = i / 6 * math.tau
            r = h * 0.36
            p.box(M["black"], (0.06, 0.008, r * 0.8), hub + Vector((math.cos(a) * r * 0.55, -0.01, math.sin(a) * r * 0.55)), (0.25, -a + math.pi / 2, 0), bevel=0.002)
    # oil filter, starter, dipstick, plug leads
    p.cyl(M["hazard"] if not diesel else M["steel"], 0.045, 0.11, (w * 0.47, -l * 0.6, h * 0.24), "X", 20, bevel=0.006)
    p.cyl(M["black"], 0.05, 0.2, (-w * 0.45, -l * 0.15, h * 0.2), "Y", 16, bevel=0.008)
    p.tube(M["hazard"], [(w * 0.36, -l * 0.2, h * 0.3), (w * 0.4, -l * 0.2, h * 0.9), (w * 0.43, -l * 0.2, h * 0.94)], 0.004, 5)
    if not diesel:
        for q in ports:
            p.tube(M["crimson_wire"] if "crimson_wire" in M else M["black"], [(-w * 0.2, q[1], h * 0.86), (-w * 0.3, q[1], h * 0.95), (-w * 0.1, -l * 0.95, h * 0.9)], 0.005, 5)
    return p.build()


def v8_blower(M):
    """engine_v8_blower: 0.88 w x 0.88 l block, blower case and scoop to 1.04 m; front = -Y."""
    p = Part("engine_v8_blower")
    l, w = 0.88, 0.88
    yb = -l / 2
    p.box(M["black"], (0.5, 0.78, 0.14), (0, yb, 0.07), bevel=0.02)
    p.box(M["iron"], (0.5, 0.86, 0.26), (0, yb, 0.27), bevel=0.02)
    for s in (-1, 1):  # banks
        p.box(M["iron"], (0.2, 0.84, 0.18), (s * 0.24, yb, 0.36), (0, s * -0.65, 0), bevel=0.015)
        p.box(M["chrome"], (0.17, 0.8, 0.07), (s * 0.31, yb, 0.45), (0, s * -0.65, 0), bevel=0.025, segs=3)
        for i in range(4):
            p.box(M["chrome"], (0.06, 0.01, 0.01), (s * 0.33, yb - 0.27 + i * 0.18, 0.49), (0, s * -0.65, 0), bevel=0.002)
        for i in range(4):  # headers
            y = -0.12 - i * 0.2
            p.tube(M["rust"], [(s * 0.34, y, 0.3), (s * 0.44, y, 0.27), (s * 0.5, y - 0.02, 0.16), (s * 0.5, -0.5, 0.06)], 0.022, 10)
        p.tube(M["rust"], [(s * 0.5, -0.05, 0.06), (s * 0.5, -0.8, 0.06)], 0.034, 12)
    # intake manifold + blower + belt drive + scoop with butterflies
    p.box(M["alu"], (0.3, 0.66, 0.1), (0, yb, 0.47), bevel=0.02)
    p.add(kit.superloft([(y, 0, 0.62, 0.17, 0.11) for y in (-0.1, -0.78)], 28, 3.2), M["chrome"])
    for i in range(9):
        p.box(M["chrome"], (0.36, 0.012, 0.014), (0, -0.14 - i * 0.075, 0.62), bevel=0.003)
    p.box(M["chrome"], (0.32, 0.03, 0.22), (0, -0.8, 0.62), bevel=0.01)
    _pulley(p, M, (0, -0.84, 0.66), 0.1, 0.05, M["chrome"])
    _pulley(p, M, (0, -0.92, 0.16), 0.11, 0.05)
    p.box(M["black"], (0.07, 0.008, 0.5), (0, -0.9, 0.41), bevel=0.002)  # drive belt (toothed)
    p.add(kit.superloft([(-0.3, 0, 0.82, 0.2, 0.09), (-0.5, 0, 0.84, 0.21, 0.1), (-0.6, 0, 0.84, 0.21, 0.1)], 24, 4.0), M["black"])
    p.add(kit.superloft([(-0.22, 0, 0.76, 0.17, 0.02), (-0.6, 0, 0.76, 0.2, 0.02)], 16, 4.0), M["black"])
    for x in (-0.09, 0.09):
        p.box(M["iron"], (0.12, 0.2, 0.02), (x, -0.45, 0.94), bevel=0.003)
        p.box(M["chrome"], (0.1, 0.006, 0.08), (x, -0.45, 0.94), (0.5, 0, 0), bevel=0)
    p.add(bm_box(0.42, 0.04, 0.12, bevel=0.01), M["black"], (0, -0.62, 0.86))  # scoop lip
    # distributor, plug leads, alternator, water pump, oil filter
    p.cyl(M["black"], 0.04, 0.1, (0, -0.06, 0.55), "Z", 16, bevel=0.006)
    p.cyl(M["crimson"], 0.05, 0.03, (0, -0.06, 0.62), "Z", 16, bevel=0.006)
    for s in (-1, 1):
        for i in range(4):
            p.tube(M["crimson"], [(s * 0.02, -0.06, 0.63), (s * 0.18, -0.12 - i * 0.18, 0.6), (s * 0.4, -0.12 - i * 0.2, 0.4)], 0.005, 5)
    p.cyl(M["alu"], 0.08, 0.1, (0.26, -0.82, 0.32), "Y", 20, bevel=0.01)
    p.cyl(M["iron"], 0.07, 0.08, (0, -0.86, 0.32), "Y", 18, bevel=0.01)
    p.cyl(M["hazard"], 0.045, 0.1, (0.2, -0.3, 0.12), "X", 18, bevel=0.006)
    return p.build()


def two_stroke(M, key="engine_2stroke", w=0.72, h=0.64, l=0.64):
    """engine_2stroke: crankcase, big finned barrel, pancake filter, expansion chamber on -X."""
    p = Part(key)
    yb = -l / 2
    p.add(kit.superloft([(0, 0, h * 0.3, w * 0.5, h * 0.3), (-l * 0.1, 0, h * 0.3, w * 0.52, h * 0.3), (-l * 0.9, 0, h * 0.3, w * 0.52, h * 0.3), (-l, 0, h * 0.3, w * 0.48, h * 0.28)], 28, 3.0), M["iron"])
    p.cyl(M["alu"], h * 0.22, 0.04, (w * 0.53, yb, h * 0.3), "X", 28, bevel=0.01)  # clutch cover
    _bolts(p, M, [(w * 0.56, yb + math.cos(a) * h * 0.18, h * 0.3 + math.sin(a) * h * 0.18) for a in [i / 6 * math.tau for i in range(6)]], 0.008, "X")
    finned_cylinder(p, M, (0, yb, h * 0.55), (0, yb + 0.03, h * 1.02), w * 0.2, 9, w * 0.38, M["alu"])
    p.box(M["alu"], (w * 0.52, w * 0.52, 0.04), (0, yb + 0.03, h * 1.03), bevel=0.012)
    p.cyl(M["black"], 0.012, 0.05, (0, yb + 0.03, h * 1.08), "Z", 8)
    _air_cleaner(p, M, (w * 0.3, yb - 0.12, h * 1.0), w * 0.2, 0.08, M["black"])
    p.tube(M["black"], [(w * 0.3, yb - 0.12, h * 0.94), (w * 0.22, yb - 0.08, h * 0.8), (w * 0.12, yb, h * 0.75)], 0.025, 10)
    pts = [(-w * 0.1, yb - 0.05, h * 0.72), (-w * 0.45, yb - 0.1, h * 0.7), (-w * 0.6, yb - 0.05, h * 0.45), (-w * 0.62, yb + 0.15, h * 0.25),
           (-w * 0.62, yb + 0.35, h * 0.2), (-w * 0.6, yb + 0.5, h * 0.22)]
    p.tube(M["rust"], pts, [0.025, 0.032, 0.05, 0.065, 0.045, 0.02], 14)
    p.cyl(M["rust"], 0.022, 0.06, (-w * 0.6, yb + 0.53, h * 0.22), "Y", 12)
    for s in (-1, 1):
        p.box(M["steel_dark"], (w * 0.2, 0.04, 0.03), (s * w * 0.35, yb, 0.015), bevel=0.006)
    return p.build()


def single_cyl(M):
    """engine_single (dirt bike thumper): 0.24 w crankcase, finned barrel to 0.64 m, carb + filter on +X, kick-start."""
    p = Part("engine_single")
    p.add(kit.superloft([(0.2, 0, 0.11, 0.1, 0.1), (0.1, 0, 0.12, 0.12, 0.12), (-0.1, 0, 0.12, 0.12, 0.13), (-0.2, 0, 0.1, 0.1, 0.1)], 24, 2.4), M["alu"])
    p.cyl(M["alu"], 0.1, 0.03, (0.13, 0.0, 0.12), "X", 24, bevel=0.008)
    p.cyl(M["black"], 0.07, 0.025, (-0.13, 0.05, 0.12), "X", 20, bevel=0.006)  # sprocket cover
    finned_cylinder(p, M, (0, -0.02, 0.22), (0, -0.06, 0.52), 0.055, 8, 0.1, M["alu"])
    p.box(M["alu"], (0.19, 0.19, 0.06), (0, -0.06, 0.56), (0.13, 0, 0), bevel=0.012)
    p.cyl(M["black"], 0.008, 0.04, (0, -0.1, 0.6), "Z", 8)
    p.tube(M["black"], [(0.06, 0.02, 0.4), (0.12, 0.06, 0.4), (0.18, 0.09, 0.42)], 0.02, 10)
    p.cyl(M["steel_dark"], 0.035, 0.07, (0.12, 0.06, 0.4), "Y", 16, bevel=0.006)  # carb
    p.lathe(M["black"], [(0.0, 0.0), (0.06, 0.0), (0.07, 0.04), (0.07, 0.1), (0.05, 0.12), (0, 0.12)], (0.18, 0.1, 0.42), "Y", 24)
    p.tube(M["chrome"], [(0.15, 0.2, 0.12), (0.17, 0.24, 0.13), (0.17, 0.32, 0.18)], 0.01, 8)  # kick-start
    p.box(M["black"], (0.03, 0.08, 0.02), (0.18, 0.34, 0.2), bevel=0.004)
    return p.build()


def vtwin(M):
    """engine_vtwin: 45 deg finned jugs, chrome rocker boxes, round chrome air cleaner on +X, primary cover on -X."""
    p = Part("engine_vtwin")
    p.add(kit.superloft([(0.22, 0, 0.12, 0.1, 0.11), (0.1, 0, 0.12, 0.12, 0.13), (-0.12, 0, 0.12, 0.12, 0.14), (-0.24, 0, 0.11, 0.1, 0.11)], 24, 2.3), M["chrome"])
    for s in (-1, 1):
        base = Vector((0, s * 0.07, 0.22))
        top = Vector((0, s * 0.27, 0.56))
        finned_cylinder(p, M, base, top, 0.06, 9, 0.11, M["black"])
        oriented(p, kit.superloft([(y, 0, 0, 0.09, 0.07) for y in (-0.08, 0.08)], 20, 3.0), M["chrome"], top - (top - base).normalized() * 0.0, top + (top - base).normalized() * 0.05)
        p.tube(M["chrome"], [(0.08, s * 0.05, 0.18), (0.08, s * 0.24, 0.5)], 0.012, 8)  # pushrod tubes
    p.lathe(M["chrome"], [(0, 0), (0.13, 0.0), (0.145, 0.02), (0.145, 0.05), (0.13, 0.07), (0, 0.07)], (0.08, 0.0, 0.4), "X", 32)
    p.cyl(M["chrome"], 0.02, 0.02, (0.16, 0.0, 0.4), "X", 6)
    p.add(kit.superloft([(0.1, -0.06, 0.12, 0.03, 0.1), (-0.2, -0.06, 0.1, 0.03, 0.08), (-0.35, -0.06, 0.1, 0.03, 0.09)], 20, 2.5), M["chrome"])  # primary
    return p.build()


def aero_2stroke(M):
    """engine_2stroke_aero: twin finned jugs, tuned pipe, prop flange at the back (+Y = rear), red air box on +X."""
    p = Part("engine_2stroke_aero")
    p.box(M["alu"], (0.4, 0.4, 0.24), (0, 0, 0.1), bevel=0.03, segs=3)
    for s in (-1, 1):
        finned_cylinder(p, M, (s * 0.12, 0, 0.22), (s * 0.17, 0, 0.44), 0.05, 8, 0.09, M["alu"])
        p.box(M["alu"], (0.13, 0.13, 0.04), (s * 0.175, 0, 0.46), (0, s * 0.2, 0), bevel=0.01)
    p.tube(M["chrome"], [(0, -0.18, 0.1), (0, -0.25, 0.0), (0.0, -0.05, -0.1), (0, 0.15, -0.1), (0.0, 0.26, -0.06)], [0.02, 0.04, 0.06, 0.04, 0.015], 14)
    p.lathe(M["black"], [(0.02, 0), (0.1, 0), (0.1, 0.03), (0.04, 0.05), (0.02, 0.05)], (0, 0.2, 0.08), "Y", 24)
    p.add(kit.superloft([(y, 0.32, 0.08, 0.06, 0.1) for y in (-0.08, 0.06)], 20, 3.0), M["crimson"])
    p.tube(M["black"], [(0.26, 0, 0.08), (0.2, 0, 0.1)], 0.03, 10)
    return p.build()


def outboard(M):
    """engine_outboard: cowling behind the transom clamp (origin), leg to -1.12 m, 3-blade prop, tiller forward."""
    p = Part("engine_outboard")
    p.add(kit.superloft([(0.08, 0, 0.36, 0.2, 0.26), (0.2, 0, 0.36, 0.25, 0.3), (0.36, 0, 0.38, 0.25, 0.32), (0.5, 0, 0.36, 0.2, 0.28)], 28, 2.6), M["ochre"])
    p.add(kit.superloft([(y, 0, 0.66, w, 0.05) for y, w in ((0.1, 0.17), (0.48, 0.17))], 24, 3.0), M["black"])
    for i in range(5):
        p.box(M["black"], (0.012, 0.012, 0.12), (0.24 - i * 0.03 + 0.06, 0.54, 0.34), bevel=0.003)
    p.box(M["steel_dark"], (0.18, 0.08, 0.2), (0, 0.04, 0.02), bevel=0.015)  # clamp bracket
    for s in (-1, 1):
        p.tube(M["chrome"], [(s * 0.06, -0.05, 0.08), (s * 0.06, -0.05, -0.02)], 0.01, 8)
    leg = kit.bm_loft([[(math.cos(a) * 0.045, 0.28 + math.sin(a) * 0.09 + (0.03 if math.sin(a) > 0 else 0), z) for a in [k / 16 * math.tau for k in range(16)]] for z in (0.08, -0.96)])
    p.add(leg, M["steel"])
    p.add(kit.superloft([(0.16, 0, -1.04, 0.06, 0.06), (0.24, 0, -1.04, 0.08, 0.08), (0.42, 0, -1.04, 0.07, 0.07), (0.5, 0, -1.04, 0.04, 0.04)], 20, 2.0), M["steel_dark"])
    p.box(M["steel"], (0.02, 0.2, 0.18), (0, 0.32, -1.16), bevel=0.006)  # skeg
    p.box(M["steel"], (0.24, 0.16, 0.012), (0, 0.24, -0.86), bevel=0.004)  # cavitation plate
    hub = Vector((0, 0.56, -1.04))
    p.cyl(M["chrome"], 0.035, 0.08, hub, "Y", 16, bevel=0.008)
    for i in range(3):
        a = i / 3 * math.tau
        p.add(bm_box(0.07, 0.012, 0.2, bevel=0.004), M["chrome"],
              hub + Vector((math.cos(a) * 0.1, 0, math.sin(a) * 0.1)), (0.45, -a + math.pi / 2, 0))
    p.tube(M["black"], [(0, 0.1, 0.36), (0, -0.1, 0.42), (0, -0.56, 0.48)], 0.018, 10)
    p.cyl(M["crimson"], 0.025, 0.08, (0, -0.58, 0.48), "Y", 12, bevel=0.006)
    return p.build()


def propeller(name, M, r, blades=2, hub_r=0.06, wood=True, pitch=0.35, axis="Y"):
    """Propeller / rotor about Blender Y (fore-aft) with tapered, twisted blades and amber tips."""
    p = Part(name)
    p.cyl(M["steel"], hub_r, hub_r * 1.2, (0, 0, 0), "Y", 20, bevel=0.01)
    p.lathe(M["steel"], [(hub_r * 0.9, 0), (hub_r * 0.6, -hub_r * 0.8), (0.0, -hub_r * 1.2)], (0, -hub_r * 0.6, 0), "Y", 20)
    for i in range(blades):
        a = i / blades * math.tau
        secs = []
        n = 8
        for k in range(n + 1):
            t = k / n
            rr = hub_r * 0.8 + t * (r - hub_r * 0.8)
            chord = (0.11 * (1 - t) + 0.05 + 0.04 * math.sin(t * math.pi)) * (r / 1.0) ** 0.3
            thick = 0.018 * (1 - t) + 0.005
            tw = pitch * (1 - t * 0.6)
            sec = []
            for (cx, cz) in ((-chord / 2, 0), (0, thick / 2), (chord / 2, 0), (0, -thick / 2)):
                x = cx * math.cos(tw) - cz * math.sin(tw)
                y = cx * math.sin(tw) + cz * math.cos(tw)
                sec.append((x, y, rr))
            secs.append(sec)
        bm = kit.bm_loft(secs)
        mat = M["wood_light"] if wood else M["steel"]
        p.add(bm, mat, (0, 0, 0), (0, 0, 0), matrix=kit.Matrix.Rotation(a, 4, "Y"))
        tip = [[(x, y, z) for (x, y, z) in s] for s in secs[-2:]]
        p.add(kit.bm_loft([[(x * 1.02, y * 1.2, z) for (x, y, z) in s] for s in tip]), M["hazard"], matrix=kit.Matrix.Rotation(a, 4, "Y"))
    ob = p.build()
    if axis == "Z":
        ob.rotation_euler = (math.radians(90), 0, 0)
    return ob


def lamp(p, M, loc, r, depth, axis="-Y", mat=None, lens=None):
    """Round lamp: shell + chrome bezel + emissive lens facing `axis` (Blender)."""
    d = Vector({"-Y": (0, -1, 0), "Y": (0, 1, 0), "X": (1, 0, 0), "-X": (-1, 0, 0), "Z": (0, 0, 1)}[axis])
    loc = Vector(loc)
    q = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
    T = kit.Matrix.Translation
    p.add(bm_lathe([(0, -depth), (r * 0.7, -depth), (r, -depth * 0.3), (r, 0)], 24), mat or M["black"], matrix=T(loc) @ q)
    p.add(bm_lathe([(r * 0.82, 0.0), (r * 1.06, 0.0), (r * 1.06, 0.012), (r * 0.82, 0.012)], 24), M["chrome"], matrix=T(loc) @ q)
    p.add(bm_lathe([(r * 0.86, 0.0), (r * 0.5, depth * 0.12), (0, depth * 0.16)], 24), lens or M["lamp"], matrix=T(loc) @ q)


def machine_gun(M, name="weapon_mg"):
    """weapon_mg: slew ring (base object), child 'Mount' (yaws, pivot 0.16 m up) with the gun shield, child 'Gun'
    (pitches, pivot 0.48 m up): receiver, perforated barrel jacket, ammo can, spade grips. Muzzle toward -Y."""
    base = Part(name)
    base.lathe(M["steel_dark"], [(0.2, 0), (0.29, 0), (0.29, 0.05), (0.26, 0.08), (0.2, 0.08)], (0, 0, 0), "Z", 40, close=True)
    for i in range(12):
        a = i / 12 * math.tau
        base.cyl(M["chrome"], 0.008, 0.012, (math.cos(a) * 0.255, math.sin(a) * 0.255, 0.085), "Z", 6, bevel=0.002)
    b = base.build()
    mnt = Part("Mount")
    mnt.cyl(M["steel"], 0.06, 0.26, (0, 0, 0.29), "Z", 16, bevel=0.008)
    mnt.box(M["steel"], (0.12, 0.12, 0.05), (0, 0, 0.45), bevel=0.01)
    shield = kit.bm_loft([[(x, -0.33 + abs(x) * 0.15, z) for (x, z) in ((-0.42, 0.24), (0.42, 0.24), (0.42, 0.74), (0.2, 0.8), (-0.2, 0.8), (-0.42, 0.74))],
                          [(x, -0.33 + abs(x) * 0.15 + 0.012, z) for (x, z) in ((-0.42, 0.24), (0.42, 0.24), (0.42, 0.74), (0.2, 0.8), (-0.2, 0.8), (-0.42, 0.74))]])
    mnt.add(shield, M["olive"])
    for x in (-0.3, 0.0, 0.3):
        mnt.cyl(M["chrome"], 0.01, 0.01, (x, -0.345 + abs(x) * 0.15, 0.7), "Y", 6, bevel=0)
    mnt.box(M["black"], (0.14, 0.03, 0.05), (0, -0.33, 0.56), bevel=0.004)
    for s in (-1, 1):
        mnt.tube(M["steel"], [(s * 0.05, 0, 0.45), (s * 0.3, -0.3, 0.3)], 0.012, 8)
    m = mnt.build(b)
    kit.recentre(m, (0, 0, 0.16))
    gun = Part("Gun")
    gun.box(M["black"], (0.12, 0.5, 0.14), (0, 0.08, 0.48), bevel=0.012)
    gun.box(M["black"], (0.1, 0.16, 0.05), (0, 0.0, 0.57), bevel=0.01)
    gun.cyl(M["steel_dark"], 0.045, 0.82, (0, -0.58, 0.48), "Y", 20, bevel=0.004)
    for i in range(14):
        for k in range(4):
            a = k / 4 * math.tau + 0.4
            gun.cyl(M["black"], 0.008, 0.01, (math.cos(a) * 0.046, -0.24 - i * 0.05, 0.48 + math.sin(a) * 0.046), "Y", 6, bevel=0, rot=None)
    gun.cyl(M["black"], 0.03, 0.12, (0, -1.04, 0.48), "Y", 12, bevel=0.004)
    gun.cyl(M["black"], 0.04, 0.04, (0, -1.1, 0.48), "Y", 6, bevel=0.003)
    gun.box(M["olive"], (0.18, 0.26, 0.2), (0.22, 0.04, 0.44), bevel=0.01)
    gun.box(M["brass"], (0.06, 0.08, 0.02), (0.11, 0.0, 0.52), bevel=0.003)
    for s in (-1, 1):
        gun.tube(M["chrome"], [(s * 0.04, 0.32, 0.48), (s * 0.08, 0.42, 0.44), (s * 0.08, 0.46, 0.4)], 0.014, 8)
    gun.box(M["steel"], (0.02, 0.04, 0.08), (0, -0.2, 0.6), bevel=0.003)
    kit.bpy.context.view_layer.update()
    g = gun.build()
    kit.recentre(g, (0, 0, 0.48))
    g.parent = m
    g.location = (0, 0, 0.32)
    return b
