"""HD machine tools not covered by the heavy group (tools/blender/hd/heavy/: excavator arm, loader, hoe arm, dozer
blade, dump bed, paver screed, plough, seeder, sprayer, harvester):
  tool_excavator_drill  segments boom -> stick -> bucket (drive head) -> bit (auger), pivots as in the game prefab
  tool_baler            round baler trailed from the hitch (origin) backwards

blender -b -P tools/blender/hd/parts_all/machine_tools.py [-- ids]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pa  # noqa: E402
from pa import M, Part, Vector, Matrix, V, G, kit, bm_box  # noqa: E402

F = "machine_tools"


def ram(p, a, b, r, ext=0.55):
    """Hydraulic ram: painted barrel from a, chrome rod to b."""
    a, b = Vector(a), Vector(b)
    m = a.lerp(b, ext)
    kit.oriented(p, kit.bm_cyl(r, (m - a).length, 16, 0.004), M["cat"], a, m)
    kit.oriented(p, kit.bm_cyl(r * 0.55, (b - m).length + 0.04, 12, 0.002), M["chrome"], m - (b - a).normalized() * 0.04, b)
    for q in (a, b):
        p.cyl(M["steel"], r * 0.8, r * 2.4, q, "X", 12, bevel=0.004)


def beam(p, a, b, w0, h0, w1, h1, mat):
    """Box-section arm tapering from (w0, h0) at a to (w1, h1) at b, cross-section in the plane normal to a->b."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    side = Vector((1, 0, 0))
    up = d.cross(side).normalized()
    secs = []
    for c, w, h in ((a, w0, h0), (b, w1, h1)):
        secs.append([c + side * sx * w / 2 + up * sz * h / 2 for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
    p.add(kit.bm_loft(secs), mat)


def drill():
    P0, P1, P2, P3 = V(0, 0, 0), V(0, 16, 24), V(0, -6, 42), V(0, -11, 42)
    root = Part("tool_excavator_drill")
    root.cyl(M["steel_dark"], 0.06, 0.04, (0, 0, 0), "Z", 8)          # tiny mount marker (the arm hangs off the machine)
    top = root.build()
    b = Part("boom")
    b.cyl(M["steel_dark"], 0.18, 0.34, P0, "X", 24, bevel=0.01)
    beam(b, P0, P1, 0.26, 0.3, 0.22, 0.24, M["cat"])
    mid = P0.lerp(P1, 0.5) + Vector((0, 0, 0.1))
    ram(b, P0 + Vector((0, -0.2, -0.12)), P0.lerp(P1, 0.6) + Vector((0, 0, -0.12)), 0.06)
    ram(b, P0.lerp(P1, 0.3) + Vector((0, 0, 0.16)), P1 + Vector((0, 0.1, 0.18)), 0.05)
    b.tube(M["black"], [P0 + Vector((0.14, 0.05, 0.1)), mid + Vector((0.14, 0, 0.1)), P1 + Vector((0.14, 0.1, 0.05))], 0.018, 8)
    b.tube(M["black"], [P0 + Vector((-0.14, 0.05, 0.1)), mid + Vector((-0.14, 0, 0.1)), P1 + Vector((-0.14, 0.1, 0.05))], 0.018, 8)
    bo = b.build()
    pa.seg(bo, "boom", P0, top)
    s = Part("stick")
    s.cyl(M["steel_dark"], 0.15, 0.32, P1, "X", 20, bevel=0.01)
    beam(s, P1, P2, 0.2, 0.22, 0.16, 0.16, M["cat"])
    ram(s, P1 + Vector((0, 0.1, 0.16)), P1.lerp(P2, 0.7) + Vector((0, -0.1, 0.06)), 0.045)
    s.tube(M["black"], [P1 + Vector((0.12, 0, 0.0)), P2 + Vector((0.12, 0, 0.12))], 0.016, 8)
    s.tube(M["black"], [P1 + Vector((-0.12, 0, 0.0)), P2 + Vector((-0.12, 0, 0.12))], 0.016, 8)
    so = s.build()
    pa.seg(so, "stick", P1, bo)
    k = Part("bucket")
    k.cyl(M["steel_dark"], 0.12, 0.3, P2, "X", 20, bevel=0.01)
    k.box(M["steel_dark"], (0.34, 0.34, 0.36), P2 + Vector((0, 0, -0.2)), bevel=0.03)        # hydraulic drive head
    k.cyl(M["steel"], 0.15, 0.08, P2 + Vector((0, 0, -0.4)), "Z", 24, bevel=0.01)
    for sx in (-1, 1):
        k.tube(M["black"], [P2 + Vector((sx * 0.18, 0.06, -0.1)), P2 + Vector((sx * 0.24, 0.06, 0.06)), P2 + Vector((sx * 0.14, 0.1, 0.16))], 0.022, 8)
    k.box(M["hazard"], (0.35, 0.02, 0.06), P2 + Vector((0, -0.172, -0.1)), bevel=0.003)
    ko = k.build()
    pa.seg(ko, "bucket", P2, so)
    t = Part("bit")
    top_b, tip = P3 + Vector((0, 0, -0.02)), V(0, -26, 42)
    t.cyl(M["steel"], 0.05, (top_b - tip).length, (top_b + tip) / 2, "Z", 16, bevel=0.004)
    pts = []
    n = 70
    for i in range(n + 1):
        u = i / n
        z = top_b.z - 0.08 - u * (top_b.z - tip.z - 0.16)
        a = u * 5.5 * math.tau
        pts.append(Vector((math.cos(a) * 0.12, P3.y + math.sin(a) * 0.12, z)))
    t.tube(M["chrome"], pts, 0.022, 6)
    t.lathe(M["chrome"], [(0.05, 0.0), (0.06, -0.04), (0.0, -0.12)], tip + Vector((0, 0, 0.1)), "Z", 16)
    to = t.build()
    pa.seg(to, "bit", P3, ko)
    return top


def baler():
    """tool_baler: drawbar, tined pick-up, green bale chamber with side plates and hub, hood, two wheels, tailgate with
    a finished round bale of hay."""
    p = Part("tool_baler")
    paint = M["olive"]
    p.box(M["steel_dark"], (0.2, 0.76, 0.2), V(0, 6, -4.5), bevel=0.02)                        # drawbar
    p.box(M["steel"], (0.24, 0.2, 0.06), V(0, 6, 0.4), bevel=0.01)
    p.box(M["steel_dark"], (2.3, 0.3, 0.2), V(0, 2, -10.5), bevel=0.02)                         # pick-up
    for k in range(-13, 14, 2):
        p.tube(M["chrome"], [V(k, 1.6, -9.2), V(k, 0.4, -8.8), V(k, -0.2, -8.2)], 0.008, 4)
    c = V(0, 10, -19)
    p.cyl(paint, 0.66, 1.68, c, "X", 40, bevel=0.03)
    for s in (-1, 1):
        p.cyl(M["steel"], 0.7, 0.04, c + Vector((s * 0.88, 0, 0)), "X", 40, bevel=0.008)
        p.cyl(M["chrome"], 0.16, 0.12, c + Vector((s * 0.94, 0, 0)), "X", 20, bevel=0.01)
        for k in range(10):
            a = k / 10 * math.tau
            p.cyl(M["chrome"], 0.014, 0.02, c + Vector((s * 0.91, math.cos(a) * 0.6, math.sin(a) * 0.6)), "X", 6)
    for k in range(6):                                                                           # chamber ribs
        a = math.radians(40 + k * 40)
        p.box(M["steel_dark"], (1.68, 0.03, 0.03), c + Vector((0, math.cos(a) * 0.665, math.sin(a) * 0.665)), (a, 0, 0), bevel=0.004)
    p.box(M["steel"], (1.62, 0.6, 0.1), V(0, 17.5, -15.5), (math.radians(-12), 0, 0), bevel=0.02)   # hood over the pick-up
    wheels = []
    for x in (-13.5, 13.5):
        w = kit.car_wheel("w", M, 0.28, 0.17, 0.16, "sport", "steel", rim_mat=M["olive"], segs=28)
        w.rotation_euler = (0, 0, 0 if x > 0 else math.pi)
        w.location = V(x, 3.4, -19)
        wheels.append(w)
    p.box(M["steel_dark"], (1.12, 0.5, 0.06), V(0, 0.8, -29), bevel=0.01)                      # tailgate ramp
    for s in (-1, 1):
        p.box(M["steel_dark"], (0.05, 0.5, 0.12), V(s * 7, 1.5, -29), bevel=0.006)
    bale = V(0, 5, -30)
    p.cyl(M["hay"], 0.37, 0.96, bale, "X", 32, bevel=0.06)
    for x in (-0.3, 0.0, 0.3):
        pa.ring(p, M["rope"], bale + Vector((x, 0, 0)), 0.372, 0.008, "X", 32, 4)
    ob = p.build()
    for w in wheels:
        w.parent = ob
    return ob


pa.add("tool_excavator_drill", drill, F, category="Tool", socket="tool (excavator)", tile="machine_drill", label="tool_excavator_drill (boom > stick > bucket > bit)")
pa.add("tool_baler", baler, F, category="Tool", socket="hitch (tractor)", tile="machine_baler")

if __name__ == "__main__":
    pa.run(F)
