"""HD engines: every engine part. Origin = the engine socket (bottom of the bay); car engines grow forward (game +Z),
boat/aero/bike engines as in their voxel designs (outboard leg down, aero prop flange to the rear).

blender -b -P tools/blender/hd/parts_all/engines.py [-- ids]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pa  # noqa: E402
from pa import M, Part, Vector, Matrix, kit, bm_box  # noqa: E402
import comps  # noqa: E402

F = "engines"


def vee(key, l, banks_n, cover, polish=False, blowers=0, stacks=False, red_covers=False):
    """V engine (V8 / V12): block, angled banks with cam covers, headers both sides, optional blowers or stacks.
    Front = kit -Y (game +Z), from y = 0 to -l."""
    p = Part(key)
    yb = -l / 2
    blk = M["steel"] if polish else M["iron"]
    p.box(M["black"], (0.48, l * 0.88, 0.12), (0, yb, 0.06), bevel=0.02)                 # oil pan
    p.box(blk, (0.5, l * 0.98, 0.26), (0, yb, 0.25), bevel=0.02)
    for i in range(banks_n + 1):
        p.box(blk, (0.54, 0.014, 0.2), (0, -0.03 - i * (l - 0.06) / banks_n, 0.26), bevel=0.003)
    cov = cover
    for s in (-1, 1):
        p.box(blk, (0.2, l * 0.95, 0.18), (s * 0.24, yb, 0.36), (0, s * -0.65, 0), bevel=0.015)
        p.box(cov, (0.17, l * 0.9, 0.07), (s * 0.31, yb, 0.45), (0, s * -0.65, 0), bevel=0.025, segs=3)
        for i in range(banks_n):
            p.box(cov, (0.06, 0.012, 0.012), (s * 0.335, yb - l * 0.36 + i * l * 0.72 / max(banks_n - 1, 1), 0.49), (0, s * -0.65, 0), bevel=0.002)
        pa.bolts(p, M["chrome"], [(s * 0.3, yb + k * l * 0.4, 0.49) for k in (-1, 0, 1)], 0.008)
        for i in range(banks_n):                                                          # tubular headers
            y = -0.08 - i * (l - 0.16) / max(banks_n - 1, 1)
            p.tube(M["blued"] if polish else M["rust"], [(s * 0.34, y, 0.3), (s * 0.44, y, 0.27), (s * 0.5, y - 0.02, 0.16), (s * 0.5, -l * 0.55, 0.06)], 0.022, 10)
        p.tube(M["blued"] if polish else M["rust"], [(s * 0.5, -0.05, 0.06), (s * 0.5, -l * 0.95, 0.06)], 0.034, 12)
    p.box(M["alu"], (0.3, l * 0.76, 0.1), (0, yb, 0.47), bevel=0.02)                     # intake valley
    if blowers:
        bl = (l * 0.86) / blowers
        for k in range(blowers):
            y0 = -0.06 - k * bl
            p.add(kit.superloft([(y, 0, 0.62, 0.16, 0.1) for y in (y0, y0 - bl * 0.9)], 28, 3.2), M["chrome"])
            for i in range(int(bl / 0.07)):
                p.box(M["chrome"], (0.34, 0.012, 0.014), (0, y0 - 0.04 - i * 0.07, 0.62), bevel=0.003)
            ys = y0 - bl * 0.45
            p.add(kit.superloft([(ys + 0.12, 0, 0.8, 0.14, 0.07), (ys, 0, 0.84, 0.15, 0.08), (ys - 0.1, 0, 0.84, 0.15, 0.08)], 24, 4.0), M["black"])  # scoop
            for x in (-0.06, 0.06):
                p.box(M["chrome"], (0.08, 0.006, 0.06), (x, ys - 0.1, 0.84), (0.5, 0, 0), bevel=0)
        comps._pulley(p, M, (0, -l - 0.02, 0.66), 0.09, 0.05, M["chrome"])
        p.box(M["black"], (0.06, 0.008, 0.48), (0, -l + 0.0, 0.42), bevel=0.002)
    if stacks:
        for s in (-1, 1):
            for i in range(4):
                y = -0.12 - i * (l - 0.24) / 3
                p.lathe(M["chrome"], [(0.034, 0), (0.034, 0.1), (0.05, 0.14), (0.054, 0.16), (0.03, 0.16)], (s * 0.08, y, 0.52), "Z", 20)
        p.box(M["steel"], (0.3, l * 0.8, 0.04), (0, yb, 0.53), bevel=0.01)
    comps._pulley(p, M, (0, -l - 0.02, 0.16), 0.11, 0.05)
    p.cyl(M["alu"], 0.08, 0.1, (0.26, -l + 0.06, 0.32), "Y", 20, bevel=0.01)
    p.cyl(M["iron"], 0.07, 0.08, (0, -l + 0.04, 0.32), "Y", 18, bevel=0.01)
    p.cyl(M["hazard"], 0.045, 0.1, (0.2, yb * 0.6, 0.12), "X", 18, bevel=0.006)
    p.cyl(M["black"], 0.04, 0.1, (0, -0.06, 0.55), "Z", 16, bevel=0.006)                  # distributor
    p.cyl(M["crimson"], 0.05, 0.03, (0, -0.06, 0.62), "Z", 16, bevel=0.006)
    for s in (-1, 1):
        for i in range(banks_n):
            p.tube(M["crimson"], [(s * 0.02, -0.06, 0.63), (s * 0.18, -0.12 - i * l * 0.2, 0.6), (s * 0.4, -0.12 - i * l * 0.22, 0.4)], 0.005, 5)
    return p.build()


def marine_diesel():
    """engine_marine_diesel: big green inline six on its bed, crimson rocker covers, flywheel housing aft, rusty
    manifold on the left, tall lagged exhaust stack, raw-water pump."""
    l = 1.36
    ob = comps.inline_engine("engine_marine_diesel", M, 0.88, 0.86, l, 6, M["olive"], M["crimson"], diesel=True, filter_r=0.16)
    ob.data.transform(Matrix.Translation((0, l / 2, 0)))                       # centre fore-aft on the socket
    p = Part("extras")
    p.box(M["steel_dark"], (0.84, 0.14, 0.62), (0, l / 2 + 0.06, 0.38), bevel=0.02)       # flywheel housing (aft = +Y)
    p.cyl(M["steel"], 0.2, 0.06, (0, l / 2 + 0.15, 0.32), "Y", 28, bevel=0.01)
    base = Vector((-0.48, 0.48, 0.66))
    p.tube(M["rust"], [(-0.48, 0.2, 0.42), (-0.5, 0.42, 0.5), base], 0.06, 14)
    p.cyl(M["black"], 0.09, 1.3, base + Vector((0, 0, 0.68)), "Z", 20, bevel=0.004)          # lagged stack
    for k in range(6):
        pa.ring(p, M["steel"], base + Vector((0, 0, 0.12 + k * 0.22)), 0.092, 0.008)
    p.lathe(M["rust"], [(0.09, 0), (0.1, 0.05), (0.13, 0.08), (0.0, 0.08)], base + Vector((0, 0, 1.32)), "Z", 20)
    p.box(M["steel"], (0.1, 0.2, 0.36), (0.48, -0.42, 0.36), bevel=0.02)                   # raw-water pump
    p.cyl(M["steel"], 0.07, 0.1, (0.5, -0.42, 0.56), "X", 16, bevel=0.01)
    for s in (-1, 1):                                                                        # engine bed rails
        p.box(M["steel_dark"], (0.1, l * 1.05, 0.08), (s * 0.4, 0, 0.0), bevel=0.01)
    e = p.build(ob)
    return ob


def pedals():
    """engine_pedals: chainring, cranks and pedals on a bottom-bracket shell, chain running back to the hub."""
    p = Part("engine_pedals")
    p.lathe(M["steel"], [(0.02, -0.004), (0.17, -0.004), (0.17, 0.004), (0.02, 0.004)], (0.06, 0, 0), "X", 48)
    for i in range(44):
        a = i / 44 * math.tau
        p.box(M["steel"], (0.006, 0.012, 0.012), (0.06, math.sin(a) * 0.176, math.cos(a) * 0.176), (-a, 0, 0), bevel=0.001)
    for i in range(5):
        a = i / 5 * math.tau
        p.box(M["chrome"], (0.01, 0.025, 0.12), (0.066, math.sin(a) * 0.08, math.cos(a) * 0.08), (-a, 0, 0), bevel=0.003)
    p.cyl(M["steel_dark"], 0.03, 0.14, (0, 0, 0), "X", 16, bevel=0.004)                   # bottom bracket
    for s, ang in ((1, 0.35), (-1, 0.35 + math.pi)):
        tip = Vector((s * 0.1, -math.sin(ang) * 0.17, -math.cos(ang) * 0.17))
        pa.rod(p, M["chrome"], (s * 0.075, 0, 0), (s * 0.085, tip.y, tip.z), 0.012, 8)
        p.box(M["black"], (0.1, 0.06, 0.02), (s * 0.15, tip.y, tip.z), bevel=0.004)          # pedal
        p.box(M["steel"], (0.11, 0.065, 0.006), (s * 0.15, tip.y, tip.z + 0.011), bevel=0.001)
    p.tube(M["black"], [(0.06, -0.02, 0.17), (0.06, 0.56, 0.03)], 0.004, 5, caps=False)   # chain runs to the rear hub
    p.tube(M["black"], [(0.06, -0.02, -0.17), (0.06, 0.56, -0.02)], 0.004, 5, caps=False)
    return p.build()


def last_engine():
    return vee("engine_v12_last", 1.04, 6, M["crimson"], blowers=2)


def forged():
    return vee("engine_v8_forged", 0.84, 4, M["chrome"], polish=True, stacks=True)


E = "Engine"
pa.add("engine_v8_blower", lambda: comps.v8_blower(M), F, category=E, socket="engine", tile="engines_v")
pa.add("engine_v12_last", last_engine, F, category=E, socket="engine", label="engine_v12_last (Last Engine, twin blowers)", tile="engines_v")
pa.add("engine_v8_forged", forged, F, category=E, socket="engine", tile="engines_v")
pa.add("engine_i4", lambda: comps.inline_engine("engine_i4", M, 0.56, 0.5, 0.64, 4, M["steel"], M["crimson"], filter_r=0.13), F, category=E, socket="engine", tile="engines_inline")
pa.add("engine_i6", lambda: comps.inline_engine("engine_i6", M, 0.56, 0.6, 0.88, 6, M["iron"], M["chrome"], filter_r=0.15), F, category=E, socket="engine", tile="engines_inline")
pa.add("engine_diesel_i6", lambda: comps.inline_engine("engine_diesel_i6", M, 0.72, 0.72, 0.96, 6, M["olive"], M["steel"], diesel=True, filter_r=0.18), F, category=E, socket="engine", tile="engines_inline")
pa.add("engine_truck_diesel", lambda: comps.inline_engine("engine_truck_diesel", M, 1.2, 0.9, 1.36, 6, M["olive"], M["steel"], diesel=True, turbo=True, fan=True), F, category=E, socket="engine", tile="engines_inline")
pa.add("engine_marine_diesel", marine_diesel, F, category=E, socket="engine (boat)", tile="engines_marine")
pa.add("engine_outboard", lambda: comps.outboard(M), F, category=E, socket="engine (transom)", tile="engines_marine")
pa.add("engine_2stroke", lambda: comps.two_stroke(M), F, category=E, socket="engine", tile="engines_small")
pa.add("engine_single", lambda: comps.single_cyl(M), F, category=E, socket="engine (bike)", tile="engines_small")
pa.add("engine_vtwin", lambda: comps.vtwin(M), F, category=E, socket="engine (bike)", tile="engines_small")
pa.add("engine_2stroke_aero", lambda: comps.aero_2stroke(M), F, category=E, socket="engine (pusher)", tile="engines_small")
pa.add("engine_pedals", pedals, F, category=E, socket="engine (bicycle)", tile="engines_marine")

if __name__ == "__main__":
    pa.run(F)
