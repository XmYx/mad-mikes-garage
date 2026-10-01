"""HD two-wheelers: DirtBike, Chopper, Bicycle, SidecarOutfit (BikeDesigns.cs; 1 voxel = 0.08 m).

blender -b -P bikes.py -- <out_dir>
Each vehicle = root empty (name = design name) with children Body, Wheel_Front, Wheel_Rear (Wheel_Side), the engine
part (named by part key, at its socket) and weapon parts. Centre line x = 0 (the voxel bikes sit 4 cm left of it)."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit  # noqa: E402
import comps  # noqa: E402
from kit import V, Part, bm_box  # noqa: E402

M = {}


def extra(M):
    M["ochre_plastic"] = kit.surface("OchrePlastic", kit.P("ochre", 3), chips=0.0, dust=0.45, rough=0.38, var=0.08, bump=0.1, clearcoat=0.2)
    M["navy_frame"] = kit.surface("NavyFrame", kit.P("navy", 3), chips=0.45, chip_col=kit.P("chrome", 1), rust=0.25, dust=0.3, rough=0.4, streaks=0.2)
    M["drab"] = kit.surface("DrabGreen", kit.P("riggreen", 3), chips=0.4, rust=0.3, dust=0.5, rough=0.7, var=0.18)
    M["crimson_candy"] = kit.surface("CandyCrimson", kit.P("crimson", 3), chips=0.15, dust=0.2, rough=0.25, var=0.1, clearcoat=0.8)
    M["gold"] = kit.surface("GoldAnodised", kit.P("ochre", 4), dust=0.2, metal=1.0, rough=0.3)
    M["seat"] = kit.surface("SeatVinyl", kit.P("black", 2), dust=0.3, rough=0.6, var=0.2, bump=0.4)


def fork(p, top_c, axle, legs_dx, upper_mat, lower_mat, r=0.022, split=0.55):
    for s in (-1, 1):
        a = axle + kit.Vector((s * legs_dx, 0, 0))
        t = top_c + kit.Vector((s * legs_dx, 0, 0))
        m = a.lerp(t, split)
        p.tube(lower_mat, [a, m], r, 14)
        p.tube(upper_mat, [m, t], r * 1.25, 14)
        p.box(M["alu"], (0.035, 0.05, 0.06), a + kit.Vector((0, 0, 0)), bevel=0.006)


def dirt_bike():
    root = kit.empty("DirtBike")
    b = Part("Body")
    fa, ra = V(0, 5, 9), V(0, 5, -9)
    top = V(0, 15.5, 5.6)
    fork(b, top, fa, 0.075, M["gold"], M["chrome"], 0.02, 0.45)
    for z in (15.0, 16.2):
        b.box(M["alu"], (0.2, 0.06, 0.03), V(0, z, 5.4 + (z - 15) * -0.3), (0.35, 0, 0), bevel=0.008)
    b.tube(M["black"], [V(-5.5, 17.2, 4.6), V(-2, 17, 5.0), V(2, 17, 5.0), V(5.5, 17.2, 4.6)], 0.011, 10)
    b.box(M["alu"], (0.04, 0.04, 0.06), V(0, 16.6, 5.2), bevel=0.006)
    for s in (-1, 1):
        b.tube(M["crimson"], [V(s * 5.2, 17.2, 4.6), V(s * 6.4, 17.3, 4.4)], 0.016, 10)
        b.tube(M["steel"], [V(s * 4.0, 17.4, 4.9), V(s * 5.2, 17.0, 5.9)], 0.005, 6)  # levers
    # number plate + headlight
    b.add(kit.superloft([(V(0, 0, 8.2).y, 0, V(0, 14.2, 0).z, 0.13, 0.13), (V(0, 0, 7.4).y, 0, V(0, 14.2, 0).z, 0.12, 0.12)], 24, 3.0), M["ochre_plastic"])
    comps.lamp(b, M, V(0, 14.0, 8.4), 0.045, 0.05)
    # high front fender
    kit.strip(b, M["ochre_plastic"], [V(0, 11.8, 5.2), V(0, 12.4, 7.0), V(0, 12.5, 9.0), V(0, 12.2, 11.4), V(0, 11.5, 13.2)], 0.13, 0.008)
    # frame: twin spars
    for s in (-1, 1):
        x = s * 0.4
        b.tube(M["steel"], [V(x * 0.2, 15, 5.4), V(x, 13.5, 2), V(x, 12, -4), V(x, 12, -8)], 0.016, 10)
        b.tube(M["steel"], [V(x * 0.2, 14, 5), V(x, 7, 0), V(x, 5.6, -1.5), V(x, 7, -3), V(x, 12, -4)], 0.015, 10)
        b.tube(M["steel"], [V(x, 12, -8), V(x, 13.2, -13)], 0.01, 8)
        b.tube(M["alu"], [V(s * 1.5, 6.8, -2), V(s * 1.6, 5.5, -6), V(s * 1.5, 5, -9.2)], 0.022, 12)  # swingarm
    b.box(M["alu"], (0.18, 0.06, 0.05), V(0, 6.8, -2.2), bevel=0.01)
    kit.coil(b, M["crimson"], V(0.6, 7, -3), V(0.6, 12, -5.5), 0.032, 8, 0.006)
    b.tube(M["gold"], [V(0.6, 7, -3), V(0.6, 12, -5.5)], 0.016, 10)
    # tank + shrouds, side panels, seat, tail
    b.add(kit.superloft([(V(0, 0, z).y, 0, V(0, h, 0).z, w, d) for z, h, w, d in ((4.6, 13.0, 0.08, 0.07), (3.6, 13.2, 0.14, 0.1), (1.5, 13.0, 0.15, 0.1), (-0.4, 12.6, 0.12, 0.08))], 24, 2.6), M["ochre_plastic"])
    for s in (-1, 1):
        sh = kit.bm_loft([[V(s * 1.9, y, z) + kit.Vector((s * t, 0, 0)) for (y, z) in ((10.5, 5.5), (14.2, 5.0), (13.5, 1.0), (10.0, 1.6))] for t in (0, 0.006)])
        b.add(sh, M["ochre_plastic"])
        sp = kit.bm_loft([[V(s * 1.7, y, z) + kit.Vector((s * t, 0, 0)) for (y, z) in ((10.6, -2.5), (13.4, -2.0), (13.6, -7.5), (11.6, -7.0))] for t in (0, 0.006)])
        b.add(sp, M["ochre_plastic"])
        b.box(M["black"], (0.08, 0.05, 0.03), V(s * 3.4, 7, -0.5), bevel=0.006)  # pegs
    b.add(kit.superloft([(V(0, 0, z).y, 0, V(0, 14.6, 0).z, w, 0.04) for z, w in ((1.2, 0.07), (0.0, 0.11), (-4, 0.12), (-8.5, 0.1), (-9.5, 0.07))], 20, 3.2), M["seat"])
    b.add(kit.superloft([(V(0, 0, z).y, 0, V(0, h, 0).z, w, 0.03) for z, h, w in ((-8, 13.6, 0.1), (-11, 13.5, 0.08), (-13.6, 13.2, 0.05))], 16, 3.0), M["ochre_plastic"])
    b.box(M["lamp_red"], (0.06, 0.012, 0.025), V(0, 13.2, -13.7), bevel=0.004)
    # exhaust: header loops round the right, silencer high
    b.tube(M["rust"], [V(0.4, 9.5, 1.6), V(1.6, 8.5, 2.8), V(2.4, 7.2, 2.2), V(2.4, 8, -1), V(2.4, 11.2, -4.5), V(2.5, 12, -5.4)], 0.016, 12)
    b.add(kit.superloft([(V(0, 0, z).y, V(2.5, 0, 0).x, V(0, 12, 0).z, r, r) for z, r in ((-5.0, 0.03), (-5.6, 0.048), (-10.4, 0.05), (-11.2, 0.03))], 20, 2.0), M["alu"])
    b.cyl(M["black"], 0.015, 0.03, V(2.5, 12, -11.5), "Y", 10)
    body = b.build(root)
    wf = kit.moto_wheel("Wheel_Front", M, 0.37, 0.09, fa, knobby=True, parent=root)
    wr = kit.moto_wheel("Wheel_Rear", M, 0.37, 0.12, ra, knobby=True, parent=root)
    eng = comps.single_cyl(M)
    eng.parent = root
    eng.location = V(0, 5, 1)
    # rear sprocket + chain (left side)
    ch = Part("Chain")
    ch.cyl(M["steel"], 0.09, 0.006, ra + kit.Vector((-0.09, 0, 0)), "X", 30, bevel=0)
    kit.chain_loop(ch, M["steel_dark"], ra + kit.Vector((-0.09, 0, 0)), 0.09, V(0, 6.2, 1.6) + kit.Vector((-0.09, 0, 0)), 0.035)
    ch.build(root)
    return root


def chopper():
    root = kit.empty("Chopper")
    b = Part("Body")
    fa, ra = V(0, 4, 14), V(0, 4, -9)
    # springer front end: long raked chrome legs + rockers
    for s in (-1, 1):
        b.tube(M["chrome"], [fa + kit.Vector((s * 0.09, 0, 0)), V(s * 1.1, 15.5, 7.3)], 0.016, 12)
        b.tube(M["chrome"], [V(s * 1.1, 4.8, 13.2), V(s * 1.1, 15.3, 6.7)], 0.01, 8)
        b.box(M["chrome"], (0.03, 0.12, 0.03), fa + kit.Vector((s * 0.09, 0.03, 0.02)), (0.5, 0, 0), bevel=0.006)
    for z in (15.0, 16.3):
        b.box(M["chrome"], (0.22, 0.07, 0.035), V(0, z, 7.0), (0.6, 0, 0), bevel=0.01)
    for s in (-1, 1):
        b.tube(M["chrome"], [V(s * 0.8, 16.4, 7), V(s * 1.6, 19, 6.8), V(s * 4.4, 21.2, 6.2), V(s * 5.2, 21.2, 5.8)], 0.012, 10)
        b.tube(M["black"], [V(s * 5.0, 21.2, 5.9), V(s * 6.3, 21.2, 5.4)], 0.016, 10)
        b.tube(M["chrome"], [V(s * 1.2, 6, 3), V(s * 3.2, 5.2, 6)], 0.01, 8)  # forward controls
        b.box(M["black"], (0.04, 0.12, 0.025), V(s * 3.9, 5.2, 6.2), bevel=0.006)
    comps.lamp(b, M, V(0, 13, 9.4), 0.11, 0.1, mat=M["chrome"])
    # frame
    for s in (-1, 1):
        x = s * 0.3
        b.tube(M["black"], [V(x * 0.3, 15.4, 7), V(x, 13.6, 2), V(x, 11.2, -7.6), V(x * 2, 10.6, -8.2)], 0.02, 12)
        b.tube(M["black"], [V(x * 0.3, 15, 7), V(x * 1.2, 5.2, 3.6), V(x * 1.6, 4.4, 1), V(x * 2, 4.4, -6), V(s * 1.2, 4.2, -9)], 0.018, 12)
        b.tube(M["black"], [V(x * 2, 10.6, -8.2), V(s * 1.2, 4.3, -9)], 0.016, 10)
    b.box(M["black"], (0.1, 0.08, 0.1), V(0, 15.6, 7.2), (0.6, 0, 0), bevel=0.01)
    # teardrop tank with pinstripe, saddle, bobbed fender, sissy bar
    tank = [(6.4, 13.4, 0.04, 0.04), (5.6, 13.9, 0.1, 0.09), (3.5, 14.0, 0.13, 0.11), (1.6, 13.4, 0.11, 0.09), (0.6, 12.8, 0.05, 0.05)]
    b.add(kit.superloft([(V(0, 0, z).y, 0, V(0, h, 0).z, w, d) for z, h, w, d in tank], 28, 2.2), M["crimson_candy"])
    b.add(kit.superloft([(V(0, 0, z).y, 0, V(0, h, 0).z, w * 1.01, d * 0.2) for z, h, w, d in tank[1:4]], 28, 2.2), M["ochre"])
    b.cyl(M["chrome"], 0.025, 0.02, V(0, 14.9, 4), "Z", 16, bevel=0.004)
    b.add(kit.superloft([(V(0, 0, z).y, 0, V(0, h, 0).z, w, d) for z, h, w, d in ((-0.6, 11.4, 0.05, 0.02), (-1.6, 10.8, 0.14, 0.035), (-4, 10.9, 0.15, 0.04), (-5.4, 11.8, 0.12, 0.04))], 20, 2.6), M["leather"])
    fend = [kit.Vector((0, ra.y + math.sin(a) * 0.4, ra.z + math.cos(a) * 0.4)) for a in [math.radians(t) for t in range(-25, 115, 10)]]
    kit.strip(b, M["crimson_candy"], fend, 0.2, 0.008)
    b.tube(M["chrome"], [V(0, 9.2, -10.6), V(0, 17.2, -12.4), V(0, 18.4, -12.8)], 0.012, 10)
    for s in (-1, 1):
        b.tube(M["chrome"], [V(s * 1.6, 4.4, -9), V(s * 1.6, 9.2, -10.5), V(s * 1.2, 15, -12), V(0, 17.6, -12.6)], 0.01, 8)
    b.add(kit.superloft([(V(0, 0, z).y, 0, V(0, 18.8, 0).z, 0.1, 0.07) for z in (-12.4, -13.2)], 16, 3.0), M["leather"])
    b.box(M["lamp_red"], (0.05, 0.03, 0.03), (0, ra.y + 0.41, ra.z + 0.07), (1.4, 0, 0), bevel=0.006)
    for y in (5, 7):  # twin straight pipes with fishtails
        b.tube(M["chrome"], [V(1.6, y + 3, 2.6), V(2.8, y + 1.5, 1.4), V(3.0, y, -1), V(3.2, y - 0.4, -13)], 0.024, 14)
        b.add(kit.superloft([(V(0, 0, z).y, V(3.2, 0, 0).x, V(0, y - 0.4, 0).z, w, h) for z, w, h in ((-13, 0.024, 0.024), (-15, 0.03, 0.05))], 16, 2.2, close=False), M["chrome"])
    body = b.build(root)
    kit.moto_wheel("Wheel_Front", M, 0.34, 0.09, fa, chrome=True, whitewall=True, parent=root, spokes=40)
    kit.moto_wheel("Wheel_Rear", M, 0.34, 0.17, ra, chrome=True, whitewall=True, parent=root, spokes=40)
    eng = comps.vtwin(M)
    eng.parent = root
    eng.location = V(0, 4, 0)
    ch = Part("Chain")
    ch.cyl(M["chrome"], 0.11, 0.006, ra + kit.Vector((-0.12, 0, 0)), "X", 32, bevel=0)
    kit.chain_loop(ch, M["steel_dark"], ra + kit.Vector((-0.12, 0, 0)), 0.11, V(0, 5, -2.6) + kit.Vector((-0.12, 0, 0)), 0.04, 0.007)
    ch.build(root)
    return root


def bicycle():
    root = kit.empty("Bicycle")
    b = Part("Body")
    fa, ra = V(0, 4, 7), V(0, 4, -7)
    fm = M["navy_frame"]
    bb = V(0, 5, 0)
    head_lo, head_hi = V(0, 9.4, 6), V(0, 12.2, 5)
    seat_top = V(0, 12.6, -2.8)
    b.tube(fm, [head_lo, head_hi], 0.02, 14)
    b.tube(fm, [head_hi + kit.Vector((0, 0.01, -0.02)), seat_top + kit.Vector((0, 0, -0.03))], 0.014, 12)
    b.tube(fm, [head_lo + kit.Vector((0, 0, 0.02)), bb], 0.016, 12)
    b.tube(fm, [bb, seat_top + kit.Vector((0, 0, 0.02))], 0.015, 12)
    for s in (-1, 1):
        b.tube(fm, [bb + kit.Vector((s * 0.02, 0, 0)), ra + kit.Vector((s * 0.06, 0, 0))], 0.008, 8)
        b.tube(fm, [seat_top + kit.Vector((s * 0.015, 0, -0.03)), ra + kit.Vector((s * 0.06, 0, 0))], 0.007, 8)
        b.tube(M["chrome"], [fa + kit.Vector((s * 0.05, 0, 0)), V(s * 0.6, 8.2, 6.6), head_lo + kit.Vector((s * 0.025, 0, -0.01))], 0.009, 8)
    b.box(M["chrome"], (0.06, 0.03, 0.03), head_lo + kit.Vector((0, 0, -0.02)), bevel=0.006)
    b.tube(M["chrome"], [head_hi, V(0, 13.6, 4.8), V(0, 13.8, 3.8)], 0.012, 10)
    b.tube(M["chrome"], [V(-5.2, 14, 3.4), V(-3, 13.9, 3.8), V(3, 13.9, 3.8), V(5.2, 14, 3.4)], 0.01, 10)
    for s in (-1, 1):
        b.tube(M["black"], [V(s * 4.6, 14, 3.5), V(s * 5.6, 14.05, 3.2)], 0.014, 10)
        b.cyl(M["chrome"], 0.02, 0.02, V(s * 2.5, 14.2, 3.9), "Z", 12)  # bell / lever stub
    b.tube(M["chrome"], [seat_top, V(0, 13.2, -3.1)], 0.011, 8)
    b.add(kit.superloft([(V(0, 0, z).y, 0, V(0, 13.5, 0).z, w, 0.028) for z, w in ((-1.6, 0.025), (-2.4, 0.05), (-3.8, 0.1), (-4.2, 0.08))], 18, 2.4), M["leather"])
    for s in (-1, 1):
        b.tube(M["steel"], [V(s * 1.1, 13.2, -4.2), V(s * 1.2, 13.0, -4.6)], 0.006, 6)
    # rear rack + crate
    for s in (-1, 1):
        b.tube(M["steel"], [ra + kit.Vector((s * 0.06, 0, 0)), V(s * 1.6, 9, -7.4)], 0.007, 6)
        b.tube(M["steel"], [V(s * 1.6, 9, -4.6), V(s * 1.6, 9, -10.2)], 0.007, 6)
    cr = V(0, 10.6, -7.5)
    b.box(M["wood"], (0.4, 0.46, 0.035), cr + kit.Vector((0, 0, -0.08)), bevel=0.004)
    for s in (-1, 1):
        for k in range(3):
            b.box(M["wood"], (0.018, 0.46, 0.035), cr + kit.Vector((s * 0.19, 0, -0.04 + k * 0.05)), bevel=0.003)
            b.box(M["wood"], (0.4, 0.018, 0.035), cr + kit.Vector((0, s * 0.22, -0.04 + k * 0.05)), bevel=0.003)
        b.box(M["wood_dark"], (0.03, 0.03, 0.17), cr + kit.Vector((s * 0.19, 0.22, 0.0)), bevel=0.003)
        b.box(M["wood_dark"], (0.03, 0.03, 0.17), cr + kit.Vector((s * 0.19, -0.22, 0.0)), bevel=0.003)
    b.box(M["canvas"], (0.3, 0.3, 0.08), cr + kit.Vector((0, 0, 0.06)), bevel=0.03)
    comps.lamp(b, M, V(0, 10, 7.1), 0.035, 0.05, mat=M["chrome"])
    # mudguards
    kit.strip(b, M["chrome"], [kit.Vector((0, fa.y + math.sin(a) * 0.39, fa.z + math.cos(a) * 0.39)) for a in [math.radians(t) for t in range(-80, 60, 10)]], 0.05, 0.004)
    kit.strip(b, M["chrome"], [kit.Vector((0, ra.y + math.sin(a) * 0.39, ra.z + math.cos(a) * 0.39)) for a in [math.radians(t) for t in range(-40, 110, 10)]], 0.05, 0.004)
    b.build(root)
    kit.moto_wheel("Wheel_Front", M, 0.35, 0.035, fa, disc=False, parent=root, spokes=36)
    kit.moto_wheel("Wheel_Rear", M, 0.35, 0.035, ra, disc=False, parent=root, spokes=36)
    # engine_pedals: chainring, cranks, pedals; chain to the rear sprocket
    pe = Part("engine_pedals")
    pe.cyl(M["steel"], 0.1, 0.006, (0.04, 0, 0), "X", 36, bevel=0)
    for i in range(36):
        a = i / 36 * math.tau
        pe.box(M["steel"], (0.006, 0.012, 0.012), (0.04, math.cos(a) * 0.105, math.sin(a) * 0.105), (a, 0, 0), bevel=0)
    pe.cyl(M["steel"], 0.02, 0.12, (0, 0, 0), "X", 12)
    for s in (-1, 1):
        pe.box(M["chrome"], (0.018, 0.03, 0.17), (s * 0.06, 0, -s * 0.075), bevel=0.006)
        pe.box(M["black"], (0.09, 0.05, 0.02), (s * 0.1, 0, -s * 0.16), bevel=0.005)
    kit.chain_loop(pe, M["steel_dark"], (0.04, 0, 0), 0.1, (0.04, ra.y - bb.y, ra.z - bb.z), 0.04, 0.004)
    eng = pe.build(root, loc=bb)
    return root


def sidecar():
    root = kit.empty("SidecarOutfit")
    b = Part("Body")
    fa, ra, sa = V(0, 4, 11), V(0, 4, -8), V(17, 4, -1)
    dr = M["drab"]
    for s in (-1, 1):  # girder fork
        b.tube(M["chrome"], [fa + kit.Vector((s * 0.09, 0, 0)), V(s * 1.1, 15.4, 7.2)], 0.018, 12)
    b.box(M["steel_dark"], (0.24, 0.08, 0.05), V(0, 15.4, 7.0), (0.4, 0, 0), bevel=0.01)
    b.tube(M["steel_dark"], [V(-5.8, 17, 6), V(-3, 17.2, 6.4), V(3, 17.2, 6.4), V(5.8, 17, 6)], 0.012, 10)
    for s in (-1, 1):
        b.tube(M["black"], [V(s * 5.5, 17, 6), V(s * 6.6, 17, 5.8)], 0.016, 10)
    kit.strip(b, dr, [kit.Vector((0, fa.y + math.sin(a) * 0.4, fa.z + math.cos(a) * 0.4)) for a in [math.radians(t) for t in range(-50, 90, 10)]], 0.16, 0.008)
    comps.lamp(b, M, V(0, 13, 8.6), 0.1, 0.1, mat=M["drab"])
    for s in (-1, 1):
        x = s * 0.3
        b.tube(M["black"], [V(x * 0.3, 15.4, 7), V(x, 13.6, 2), V(x, 12, -7.6)], 0.02, 12)
        b.tube(M["black"], [V(x * 0.3, 15, 7), V(x * 1.4, 5.2, 2), V(x * 1.6, 4.6, -6), V(s * 1.2, 4.2, -8)], 0.018, 12)
        b.tube(M["black"], [V(x, 12, -7.6), V(s * 1.2, 4.3, -8)], 0.016, 10)
    b.add(kit.superloft([(V(0, 0, z).y, 0, V(0, h, 0).z, w, d) for z, h, w, d in ((5.6, 13.0, 0.08, 0.08), (4.6, 13.4, 0.17, 0.13), (1.0, 13.4, 0.17, 0.13), (0.0, 13.0, 0.1, 0.09))], 24, 3.0), dr)
    b.cyl(M["chrome"], 0.03, 0.02, V(0, 14.9, 3), "Z", 16, bevel=0.004)
    b.add(kit.superloft([(V(0, 0, z).y, 0, V(0, 13.6, 0).z, w, 0.04) for z, w in ((-0.6, 0.06), (-1.4, 0.15), (-5, 0.16), (-6.2, 0.14))], 20, 2.6), M["leather"])
    for s in (-1, 1):
        kit.coil(b, M["chrome"], V(s * 1.2, 12, -2.8), V(s * 1.2, 13.2, -2.8), 0.02, 4, 0.004)
    kit.strip(b, dr, [kit.Vector((0, ra.y + math.sin(a) * 0.4, ra.z + math.cos(a) * 0.4)) for a in [math.radians(t) for t in range(-10, 120, 10)]], 0.18, 0.008)
    b.box(M["lamp_red"], (0.05, 0.03, 0.03), (0, 1.0, 0.57), (0.5, 0, 0), bevel=0.006)
    b.tube(M["rust"], [V(-1.2, 8, 2), V(-2.6, 6.8, 1), V(-3, 6.8, -6), V(-3.2, 7.2, -13)], 0.022, 12)
    b.cyl(M["rust"], 0.04, 0.4, V(-3.2, 7.2, -11), "Y", 16, bevel=0.006)
    # struts to the chair
    b.tube(M["black"], [V(1, 6, 4), V(8.4, 7, 4)], 0.016, 10)
    b.tube(M["black"], [V(1, 6, -6), V(8.4, 7, -6)], 0.016, 10)
    b.tube(M["black"], [V(1, 13, -2), V(9, 11, -2)], 0.013, 10)
    b.tube(M["black"], [V(16, 7, -4), V(17, 4, -1), V(16, 7, 2)], 0.016, 10)
    # the chair: boat-shaped tub with an open cockpit, coaming, seat, spare on the back
    st = []
    for z in (8.4, 7, 5, 3, 0, -4, -7, -8.8, -9.6):
        taper = (z - 3) * 0.8 if z > 3 else ((-7 - z) * 1.2 if z < -7 else 0)
        hw = max(0.4, 4.2 - taper * 0.6) * 0.08
        top = 11 - ((z - 3) / 2 if z > 3 else 0)
        bot = 5 + (max(0, z - 5) * 0.6)
        st.append((V(0, 0, z).y, V(12, 0, 0).x, V(0, (top + bot) / 2, 0).z, hw, (top - bot) / 2 * 0.08))
    b.add(kit.superloft(st, 28, 2.8), dr)
    cx, cy, cz = V(12, 0, 0).x, V(0, 0, -2.3).y, V(0, 11.0, 0).z
    ring = [(cx + math.cos(a) * 0.2, cy + math.sin(a) * 0.34, cz + 0.012) for a in [k / 24 * math.tau for k in range(25)]]
    b.add(kit.superloft([(cy + dy, cx, cz + 0.004, 0.19 * math.sqrt(max(0.0, 1 - (dy / 0.33) ** 2)) + 0.002, 0.004) for dy in (-0.33, -0.2, 0, 0.2, 0.33)], 24, 2.0), M["black"])
    b.tube(M["leather"], ring, 0.018, 8, caps=False)
    b.box(M["leather"], (0.3, 0.06, 0.26), (cx, cy + 0.26, cz + 0.12), (-0.25, 0, 0), bevel=0.03)
    b.box(M["tarp"], (0.36, 0.3, 0.12), V(12, 7.5, -4), bevel=0.04)
    b.box(M["tarp"], (0.34, 0.08, 0.3), V(12, 9.5, -6.8), (0.2, 0, 0), bevel=0.03)
    for s in (-1, 1):
        for z in (-5, 0, 5):
            b.cyl(M["chrome"], 0.007, 0.008, V(12 + s * 4.15, 9, z), "X", 6, bevel=0)
    sp = kit.car_wheel("spare", M, 0.2, 0.12, 0.09, "street", "steel")
    sp.data.transform(kit.Matrix.Translation(V(12.5, 8, -10.4)))
    b.build(root)
    sp.parent = root
    sp.name = "SpareWheel"
    kit.moto_wheel("Wheel_Front", M, 0.34, 0.12, fa, parent=root, spokes=36)
    kit.moto_wheel("Wheel_Rear", M, 0.34, 0.14, ra, parent=root, spokes=36)
    kit.moto_wheel("Wheel_Side", M, 0.34, 0.12, sa, parent=root, spokes=36, disc=False)
    eng = comps.vtwin(M)
    eng.parent = root
    eng.location = V(0, 4, 0)
    gun = comps.machine_gun(M)
    gun.parent = root
    gun.location = V(12, 11.4, 5)
    gun.scale = (0.8, 0.8, 0.8)
    return root


if __name__ == "__main__":
    kit.run_group([(dirt_bike, "bike_dirtbike", "DirtBike (thumper, knobbies)", "DirtBike"),
                   (chopper, "bike_chopper", "Chopper (V-twin, springer fork)", "Chopper"),
                   (bicycle, "bike_bicycle", "Bicycle (crate rack, pedals part)", "Bicycle"),
                   (sidecar, "bike_sidecar", "Sidecar outfit (MG on the chair)", "SidecarOutfit")], M, extra)
