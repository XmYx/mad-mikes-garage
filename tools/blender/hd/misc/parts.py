"""HD swappable vehicle parts (PartLibrary*.cs; 1 voxel = 0.08 m). One object per part, named by its part key,
origin = mount point (socket), +X = outward for side parts (right side; mirrored with scale.x = -1 on the left),
front = Blender -Y. Hinged parts keep their segments as child objects pivoting at the joint (cargo_crane: turret
-> boom; weapon_mg: Mount -> Gun).

blender -b -P parts.py -- <out_dir>"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit  # noqa: E402
import comps  # noqa: E402
from kit import V, Part, Vector, Matrix  # noqa: E402

M = {}


def extra(M):
    M["bar_steel"] = kit.surface("BarSteel", kit.P("metal", 2), chips=0.0, rust=0.35, dust=0.35, metal=0.75, rough=0.5, var=0.15, streaks=0.4)
    M["ram_steel"] = kit.surface("RamSteel", kit.P("metal", 1), rust=0.5, dust=0.4, metal=0.6, rough=0.65, var=0.2, streaks=0.5, bump=0.6)
    M["hazard_bar"] = kit.textured("HazardStripes", "planks", kit._hex("d4b020"), kit.P("black", 1), scale=1.0, dust=0.3, rough=0.6, bump=0.1, rot="X")
    M["fins"] = kit.textured("RadiatorFins", "corrugated", kit.P("metal", 1), kit.P("metal", 3), kit.P("rust", 2), scale=2.0, dust=0.2, rough=0.5, bump=0.8, rot="X")
    M["fins_cu"] = kit.textured("RadiatorCore", "corrugated", kit.P("bronze", 1), kit.P("bronze", 3), kit.P("rust", 1), scale=2.0, dust=0.2, rough=0.5, bump=0.8, rot="X")
    M["crane"] = kit.surface("CraneYellow", kit._hex("c89a22"), chips=0.45, rust=0.3, dust=0.4, rough=0.5, streaks=0.4)
    M["jerry"] = kit.surface("JerryOlive", kit.P("olive", 2), chips=0.5, chip_col=kit.P("metal", 2), rust=0.3, dust=0.4, rough=0.55)


def at_inner_face(ob, w):
    """Wheel parts are authored from x = 0 (inner face, at the socket) outward."""
    ob.data.transform(Matrix.Translation((w / 2, 0, 0)))
    return ob


# ------------------------------------------------------------------------------------------- tiles
def engines_car():
    v8 = comps.v8_blower(M)
    d6 = comps.inline_engine("engine_diesel_i6", M, 0.72, 0.72, 0.96, 6, M["olive"], M["steel"], diesel=True, filter_r=0.18)
    td = comps.inline_engine("engine_truck_diesel", M, 1.2, 0.9, 1.36, 6, M["olive"], M["steel"], diesel=True, turbo=True, fan=True)
    i4 = comps.inline_engine("engine_i4", M, 0.56, 0.5, 0.64, 4, M["steel"], M["crimson"], filter_r=0.13)
    t2 = comps.two_stroke(M)
    objs = [v8, td, d6, i4, t2]
    kit.grid(objs, 3, 0.3)
    return objs


def engines_small():
    objs = [comps.single_cyl(M), comps.vtwin(M), comps.aero_2stroke(M), comps.outboard(M)]
    kit.grid(objs, 2, 0.3)
    return objs


def wheels_road():
    st = at_inner_face(kit.car_wheel("wheel_street", M, 0.432, 0.27, 0.32, "street", "steel", rim_mat=M["brass"], loc=(0, 0, 0.432)), 0.32)
    sp = at_inner_face(kit.car_wheel("wheel_sport", M, 0.448, 0.34, 0.36, "sport", "5spoke", rim_mat=M["chrome"], loc=(0, 0, 0.448)), 0.36)
    rn = at_inner_face(kit.car_wheel("wheel_rain", M, 0.432, 0.27, 0.3, "rain", "mag", rim_mat=M["chrome"], loc=(0, 0, 0.432)), 0.3)
    sm = at_inner_face(kit.car_wheel("wheel_small", M, 0.344, 0.24, 0.24, "small", "steel", rim_mat=M["cream"], loc=(0, 0, 0.344)), 0.24)
    bk = kit.moto_wheel("wheel_bike", M, 0.37, 0.12, (0, 0, 0.37), knobby=True)
    objs = [st, sp, rn, sm, bk]
    for o in objs:
        o.rotation_euler = (0, 0, math.radians(-30))
    kit.grid(objs, 3, 0.25)
    return objs


def track_roller():
    """wheel_track: crawler road wheel (hidden inside the tracks at runtime) shown with a run of grouser shoes."""
    p = Part("wheel_track")
    R, w = 0.344, 0.48
    p.lathe(M["iron"], [(0.06, -w / 2), (R * 0.82, -w / 2), (R, -w * 0.42), (R, -w * 0.12), (R * 0.85, -w * 0.08), (R * 0.85, w * 0.08), (R, w * 0.12), (R, w * 0.42),
                        (R * 0.82, w / 2), (0.06, w / 2)], (0, 0, 0), "X", 36)
    p.cyl(M["steel"], 0.07, w + 0.06, (0, 0, 0), "X", 16, bevel=0.01)
    for i in range(8):
        a = i / 8 * math.tau
        p.cyl(M["chrome"], 0.012, 0.02, (w / 2 + 0.01, math.cos(a) * 0.12, math.sin(a) * 0.12), "X", 6)
    # track shoes wrapping the lower half
    for i in range(13):
        a = math.radians(-180 + i * 15)
        r = R + 0.04
        c = Vector((0, math.cos(a) * r, math.sin(a) * r))
        p.add(kit.bm_box(w + 0.1, 0.13, 0.03, bevel=0.006), M["steel_dark"], c, (a + math.pi / 2, 0, 0))
        p.add(kit.bm_box(w + 0.1, 0.02, 0.04, bevel=0.004), M["steel_dark"], c + c.normalized() * 0.03, (a + math.pi / 2, 0, 0))
    ob = p.build()
    ob.location = (0, 0, R + 0.07)
    return ob


def wheels_offroad():
    of = at_inner_face(kit.car_wheel("wheel_offroad", M, 0.52, 0.29, 0.4, "offroad", "beadlock", rim_mat=M["black"], loc=(0, 0, 0.52)), 0.4)
    md = at_inner_face(kit.car_wheel("wheel_mud", M, 0.544, 0.296, 0.48, "mud", "steel", rim_mat=M["olive"], loc=(0, 0, 0.544)), 0.48)
    tr = at_inner_face(kit.car_wheel("wheel_truck", M, 0.536, 0.32, 0.4, "truck", "truck", rim_mat=M["crimson"], loc=(0, 0, 0.536)), 0.4)
    tk = track_roller()
    objs = [of, md, tr, tk]
    for o in objs:
        o.rotation_euler = (0, 0, math.radians(-30))
    kit.grid(objs, 2, 0.3)
    return objs


def bull_bar():
    p = Part("bumper_bull_bar")
    s = M["bar_steel"]
    p.box(s, (2.0, 0.14, 0.2), (0, -0.06, 0.1), bevel=0.02)
    p.tube(s, [V(-11, 2, 2), V(11, 2, 2)], 0.04, 12)
    p.tube(s, [V(-10, 2, 1), V(-10, 9, 2.4), V(-7, 10, 2.6), V(7, 10, 2.6), V(10, 9, 2.4), V(10, 2, 1)], 0.04, 12)
    for x in (-5, 5):
        p.tube(s, [V(x, 2, 2), V(x, 10, 2.6)], 0.035, 12)
    for x in (-10, 10):
        p.tube(s, [V(x, 9, 2.4), V(x, 9.5, -0.5)], 0.03, 10)
    p.cyl(M["black"], 0.08, 0.38, V(0, 2.3, 1.6), "X", 20, bevel=0.01)  # winch
    p.cyl(M["steel"], 0.085, 0.03, V(-2.6, 2.3, 1.6), "X", 20, bevel=0.005)
    p.cyl(M["steel"], 0.085, 0.03, V(2.6, 2.3, 1.6), "X", 20, bevel=0.005)
    p.add(kit.bm_torus(0.04, 0.012, 14, 6), M["rust"], V(0, 2.2, 3.2), (math.pi / 2, 0, 0))
    comps.lamp(p, M, V(-7.5, 9.0, 3.1), 0.07, 0.07)
    comps.lamp(p, M, V(7.5, 9.0, 3.1), 0.07, 0.07)
    for x in (-12, 12):
        p.box(M["black"], (0.06, 0.12, 0.06), V(x, 2, -0.4), bevel=0.01)
    return p.build()


def ram():
    p = Part("bumper_ram")
    poly = [(-1.08, -0.02), (1.08, -0.02), (1.08, 0.08), (0.0, 0.42), (-1.08, 0.08)]
    p.add(kit.bm_loft([[(x, -y, z) for (x, y) in poly] for z in (0.0, 0.76)]), M["ram_steel"])
    for i in range(6):  # welded ribs
        x = -0.9 + i * 0.36
        y = 0.08 + (1.08 - abs(x)) / 1.08 * 0.34
        p.box(M["ram_steel"], (0.04, 0.06, 0.8), (x, -y - 0.02, 0.38), (0, 0, math.atan2(0.34, 1.08) * (1 if x < 0 else -1)), bevel=0.008)
    p.box(M["rust"], (2.16, 0.32, 0.06), (0, -0.14, 0.78), bevel=0.01)
    for x in (-0.8, 0.0, 0.8):
        yy = 0.08 + (1.08 - abs(x)) / 1.08 * 0.34
        p.add(kit.bm_cyl(0.07, 0.24, 4, 0.0, 0.0), M["chrome"], (x, -yy - 0.1, 0.35), (math.pi / 2, 0, math.pi / 4))
    for i in range(10):
        x = -1.0 + i * 0.22
        p.cyl(M["chrome"], 0.012, 0.01, (x, -0.06 - (1.08 - abs(x)) / 1.08 * 0.34 - 0.04, 0.68), "Y", 6, bevel=0)
    return p.build()


def spiked_bumper():
    p = Part("bumper_spiked")
    p.box(M["rust"], (2.0, 0.12, 0.3), (0, -0.04, 0.14), bevel=0.02)
    p.box(M["bar_steel"], (2.04, 0.08, 0.06), (0, -0.1, 0.26), bevel=0.01)
    for row, (y, step, off) in enumerate(((2, 4, -11), (0, 6, -9))):
        for x in range(off, -off + 1, step):
            base = V(x, y + 0.5, 1)
            p.add(kit.bm_cyl(0.045, 0.42, 6, 0.0, 0.002), M["chrome"], base + Vector((0, -0.21, 0)), (math.pi / 2, 0, 0))
            p.add(kit.bm_cyl(0.06, 0.04, 6, 0.004), M["steel_dark"], base, (math.pi / 2, 0, 0))
    return p.build()


def side_spikes():
    p = Part("armor_spikes")
    p.box(M["bar_steel"], (0.12, 2.32, 0.3), (0.06, 0, 0.14), bevel=0.015)
    for z in range(-12, 13, 4):
        c = V(1, 2, z)
        p.add(kit.bm_cyl(0.05, 0.42, 6, 0.0, 0.002), M["chrome"], c + Vector((0.21, 0, 0)), (0, math.pi / 2, 0))
        p.add(kit.bm_cyl(0.07, 0.04, 6, 0.004), M["steel_dark"], c, (0, math.pi / 2, 0))
    return p.build()


def side_plate():
    p = Part("armor_plate")
    p.box(M["ram_steel"], (0.08, 2.64, 0.72), (0.06, 0, 0.18), bevel=0.012)
    p.box(M["rust"], (0.1, 2.68, 0.06), (0.07, 0, -0.16), bevel=0.01)
    for z in (-0.66, 0.0, 0.66):
        p.box(M["bar_steel"], (0.1, 0.05, 0.74), (0.08, z, 0.18), bevel=0.006)  # welded straps
    for z in range(-14, 15, 7):
        for y in (4, -1):
            p.cyl(M["chrome"], 0.02, 0.02, V(1.5, y, z), "X", 8, bevel=0.004)
    p.box(M["black"], (0.1, 0.3, 0.06), (0.12, 0.4, 0.42), bevel=0.01)  # vision slit cover
    return p.build()


def winch_bumper():
    p = Part("bumper_winch")
    p.box(M["bar_steel"], (2.0, 0.14, 0.32), (0, -0.06, 0.15), bevel=0.02)
    p.box(M["steel_dark"], (0.84, 0.24, 0.12), V(0, 0.5, 3), bevel=0.015)
    p.cyl(M["black"], 0.13, 0.62, V(0, 3, 2), "X", 24, bevel=0.01)
    p.cyl(M["steel"], 0.16, 0.04, V(-4, 3, 2), "X", 24, bevel=0.006)
    p.cyl(M["steel"], 0.16, 0.04, V(4, 3, 2), "X", 24, bevel=0.006)
    for k in range(10):
        p.add(kit.bm_torus(0.14, 0.012, 20, 5), M["steel"], V(-3.4 + k * 0.75, 3, 2), (0, math.pi / 2, 0))
    p.box(M["black"], (0.18, 0.14, 0.22), V(4.9, 3, 2), bevel=0.02)  # motor
    p.box(M["hazard"], (0.2, 0.06, 0.1), V(0, 2, 5.4), bevel=0.01)  # fairlead
    p.tube(M["steel"], [V(0, 3, 3.6), V(0, 2, 6.5), V(0, 1.4, 7.4)], 0.008, 6)
    p.add(kit.bm_torus(0.04, 0.012, 14, 6), M["rust"], V(0, 1.2, 7.8), (0, math.pi / 2, 0))
    for x in (-10, 10):
        p.box(M["rust"], (0.08, 0.14, 0.2), V(x, 5, 0.6), bevel=0.01)
        p.add(kit.bm_torus(0.045, 0.014, 14, 6), M["hazard"], V(x, 6.4, 1.2), (0, math.pi / 2, 0))
    return p.build()


def front_armour():
    objs = [bull_bar(), ram(), spiked_bumper(), winch_bumper(), side_spikes(), side_plate()]
    kit.grid(objs, 2, 0.35)
    return objs


def roof_rack():
    p = Part("cargo_roof_rack")
    r = M["bar_steel"]
    for x in (-10, 10):
        for z in (-7, 7):
            p.box(M["black"], (0.06, 0.1, 0.06), V(x, 0.3, z), bevel=0.01)
            p.tube(r, [V(x, 0.5, z), V(x, 4, z)], 0.016, 8)
    for x in (-10, 10):
        p.tube(r, [V(x, 1, -7.6), V(x, 1, 7.6)], 0.016, 8)
        p.tube(r, [V(x, 4, -7), V(x, 4, 7)], 0.018, 8)
    for z in (-7, 0, 7):
        p.tube(r, [V(-10, 1, z), V(10, 1, z)], 0.016, 8)
    p.tube(r, [V(-10, 4, 7), V(10, 4, 7)], 0.018, 8)
    p.add(kit.superloft([(V(0, 0, z).y, V(-3, 0, 0).x, V(0, 3.6, 0).z, 0.42, 0.13 - abs(z) * 0.002) for z in (-5, -3, 0, 3, 5)], 20, 2.6), M["tarp"])
    for z in (-3, 2):
        p.add(kit.superloft([(V(0, 0, z).y + d, V(-3, 0, 0).x, V(0, 3.6, 0).z, 0.43, 0.14) for d in (-0.012, 0.012)], 20, 2.6), M["rope"])
    ob = p.build()
    sp = kit.car_wheel("SpareWheel", M, 0.27, 0.17, 0.2, "offroad", "steel", loc=(0, 0, 0))
    sp.data.transform(Matrix.Translation(V(6, 2.8, 0)) @ Matrix.Rotation(math.pi / 2, 4, "Y"))
    sp.parent = ob
    jc = jerry_can()
    jc.parent = ob
    jc.location = V(-6.5, 1.4, -4.5) + Vector((0, 0, 0.08))
    jc.rotation_euler = (0, 0, math.pi / 2)
    return ob


def light_bar():
    p = Part("lights_bar")
    for x in (-8, 8):
        p.box(M["steel_dark"], (0.05, 0.06, 0.22), V(x, 1.2, 0), bevel=0.008)
    p.box(M["black"], (1.84, 0.12, 0.14), V(0, 3.6, -0.3), bevel=0.02)
    for i in range(6):
        x = -9 + i * 3.6
        comps.lamp(p, M, V(x, 3.6, 0.6), 0.05, 0.05, mat=M["black"])
    return p.build()


def fog_pods():
    p = Part("lights_fog")
    p.tube(M["bar_steel"], [V(-8, 2, 0), V(8, 2, 0)], 0.018, 8)
    for x in (-5, 5):
        p.box(M["black"], (0.04, 0.04, 0.14), V(x, 1, 0), bevel=0.006)
    for x in (-6, 6):
        p.tube(M["steel"], [V(x, 2, 0), V(x, 3.4, 0)], 0.012, 8)
        comps.lamp(p, M, V(x, 5, 1.2), 0.17, 0.18, mat=M["black"], lens=M["lamp_amber"])
        p.tube(M["black"], [V(x, 3, 3.3), V(x, 7, 3.3)], 0.008, 6)
        p.tube(M["black"], [V(x - 2, 5, 3.3), V(x + 2, 5, 3.3)], 0.008, 6)
    return p.build()


def snorkel():
    p = Part("snorkel")
    p.box(M["steel_dark"], (0.1, 0.36, 0.1), V(0.5, 0.5, 0), bevel=0.01)
    p.tube(M["black"], [V(1.5, 1, 0.5), V(1.5, 14.5, 0.5), V(1.6, 16.4, 0.8), V(1.6, 17, 1.6)], 0.075, 16)
    p.add(kit.superloft([(V(0, 0, z).y, V(1.6, 0, 0).x, V(0, 17.2, 0).z, w, h) for z, w, h in ((0.2, 0.1, 0.1), (1.6, 0.13, 0.12), (3.8, 0.13, 0.12), (4.4, 0.12, 0.11))], 24, 3.0), M["black"])
    p.box(M["steel_dark"], (0.2, 0.02, 0.18), V(1.6, 17.2, 4.5), bevel=0.004)
    for y in (5, 11):
        p.add(kit.bm_torus(0.08, 0.012, 16, 6), M["chrome"], V(1.5, y, 0.5))
        p.box(M["chrome"], (0.12, 0.04, 0.03), V(0.6, y, 0.5), bevel=0.004)
    return p.build()


def rear_dropper():
    p = Part("rear_dropper")
    p.box(M["hazard_bar"], (2.0, 0.14, 0.3), (0, 0.04, 0.13), bevel=0.02)
    p.add(kit.bm_loft([[(x, y, z) for (x, y) in ((-0.44, -0.08), (0.44, -0.08), (0.44, 0.26), (-0.44, 0.26))] for z in (0.78, 0.36)] +
                      [[(x * 0.5, y * 0.6 + 0.07, 0.3) for (x, y) in ((-0.44, -0.08), (0.44, -0.08), (0.44, 0.26), (-0.44, 0.26))]]), M["steel"])
    p.box(M["steel_dark"], (0.92, 0.38, 0.03), (0, 0.09, 0.8), bevel=0.008)
    p.box(M["steel_dark"], (0.5, 0.12, 0.16), (0, 0.24, 0.18), bevel=0.01)
    p.box(M["black"], (0.34, 0.02, 0.06), (0, 0.31, 0.1), bevel=0.003)
    p.cyl(M["black"], 0.05, 0.12, (0.5, 0.1, 0.6), "X", 16, bevel=0.008)  # actuator
    p.add(kit.bm_box(0.9, 0.02, 0.06, bevel=0.003), M["hazard"], (0, -0.09, 0.7))
    for i in range(5):  # loose caltrops on the lid
        q = Vector((-0.2 + i * 0.1, 0.05 + (i % 2) * 0.06, 0.84))
        for d in ((1, 0, 0.5), (-0.5, 0.86, 0.5), (-0.5, -0.86, 0.5), (0, 0, -1)):
            p.tube(M["steel"], [q, q + Vector(d) * 0.035], 0.004, 4)
    return p.build()


def roof_kit():
    objs = [roof_rack(), comps.machine_gun(M), light_bar(), fog_pods(), snorkel(), rear_dropper()]
    kit.grid(objs, 3, 0.35)
    return objs


def exhaust_stack():
    p = Part("exhaust_stack")
    pipe = M["chrome"]
    p.cyl(pipe, 0.095, 2.4, (0, 0, 1.2), "Z", 20, bevel=0.0)
    p.tube(pipe, [(0, 0, 2.38), (0, 0, 2.52), (0, 0.06, 2.62), (0, 0.18, 2.66)], 0.095, 20)
    hs = kit.bm_cyl(0.13, 0.96, 24, 0.004)
    p.add(hs, M["steel_dark"], (0, 0, 1.12))
    for i in range(6):
        for k in range(12):
            a = k / 12 * math.tau + i * 0.26
            p.cyl(M["black"], 0.012, 0.01, (math.cos(a) * 0.131, math.sin(a) * 0.131, 0.75 + i * 0.15), "X", 6, bevel=0, rot=(0, 0, a))
    for z in (0.7, 1.55):
        p.add(kit.bm_torus(0.135, 0.01, 24, 6), M["steel"], (0, 0, z))
    p.box(M["steel_dark"], (0.16, 0.12, 0.12), (-0.12, 0, 0.06), bevel=0.01)
    p.cyl(M["black"], 0.09, 0.01, (0, 0.2, 2.66), "Y", 16, bevel=0)
    return p.build()


def side_pipes():
    p = Part("exhaust_side_pipes")
    for i, (dx, dz) in enumerate(((0.0, 0.0), (0.08, 0.0), (0.0, 0.08), (0.08, 0.08))):
        p.tube(M["chrome"], [(dx - 0.08, -0.04, dz + 0.02), (dx + 0.02, 0.06, dz + 0.02), (dx + 0.02, 1.15, dz + 0.02)], 0.035, 14, caps=False)
        p.cyl(M["black"], 0.03, 0.01, (dx + 0.02, 1.15, dz + 0.02), "Y", 12, bevel=0)
    p.box(M["steel_dark"], (0.04, 0.06, 0.18), (0.06, 0.7, -0.06), bevel=0.006)
    p.box(M["steel"], (0.24, 0.5, 0.01), (0.06, 0.8, 0.14), bevel=0.003)  # heat shield
    for k in range(4):
        p.cyl(M["black"], 0.01, 0.012, (0.06 - 0.06 + k * 0.04, 0.65, 0.146), "Z", 6, bevel=0)
    return p.build()


def radiator(key, hx, h, bigcore=False):
    p = Part(key)
    w = (hx * 2 + 1) * 0.08
    hh = (h + 1) * 0.08
    core = M["fins_cu"] if bigcore else M["fins"]
    p.box(core, (w, 0.05 if not bigcore else 0.1, hh), (0, 0, hh / 2), bevel=0.004)
    p.box(M["chrome"] if not bigcore else M["steel"], (w + 0.02, 0.1, 0.1), (0, -0.01, hh + 0.05), bevel=0.015)
    p.box(M["steel_dark"], (w + 0.02, 0.1, 0.06), (0, -0.01, -0.03), bevel=0.01)
    for s in (-1, 1):
        p.box(M["steel_dark"], (0.06, 0.1, hh + 0.16), (s * (w / 2 + 0.03), -0.01, hh / 2 + 0.02), bevel=0.01)
    p.cyl(M["chrome"], 0.035, 0.03, (-0.2, -0.01, hh + 0.12), "Z", 16, bevel=0.006)
    p.tube(M["black"], [(-0.24, 0.05, hh - 0.02), (-0.24, 0.24, hh - 0.04), (-0.2, 0.34, hh - 0.1)], 0.028, 12)
    p.tube(M["black"], [(0.24, 0.05, 0.05), (0.24, 0.24, 0.06), (0.2, 0.34, 0.12)], 0.028, 12)
    if bigcore:
        for x in (-0.32, 0.32):
            c = Vector((x, 0.12, 0.36))
            p.add(kit.bm_lathe([(0.27, -0.05), (0.27, 0.05), (0.24, 0.07), (0.24, -0.05)], 32, "Y"), M["black"], c)
            p.cyl(M["steel"], 0.04, 0.08, c, "Y", 16, bevel=0.006)
            for k in range(7):
                a = k / 7 * math.tau
                p.add(kit.bm_box(0.06, 0.01, 0.2, bevel=0.003), M["black"], c + Vector((math.cos(a) * 0.13, 0.0, math.sin(a) * 0.13)), (0.3, -a + math.pi / 2, 0))
            for k in range(4):
                p.box(M["steel_dark"], (0.54, 0.012, 0.012), c + Vector((0, 0.08, -0.2 + k * 0.13)), bevel=0)
    return p.build()


def cargo_crane():
    """cargo_crane: slew ring (root), 'turret' segment (yaw, pivot at the mount), 'boom' segment (pitch, pivot 0.72 m
    up). Boom tip at (0, 2.4, -3.2) m in game terms (0, 30, -40 voxels)."""
    base = Part("cargo_crane")
    base.lathe(M["steel_dark"], [(0.2, 0), (0.34, 0), (0.34, 0.08), (0.3, 0.1), (0.2, 0.1)], (0, 0, 0), "Z", 40, close=True)
    for i in range(16):
        a = i / 16 * math.tau
        base.cyl(M["chrome"], 0.01, 0.014, (math.cos(a) * 0.31, math.sin(a) * 0.31, 0.105), "Z", 6, bevel=0)
    b = base.build()
    t = Part("turret")
    t.box(M["crane"], (0.56, 0.6, 0.5), (0, 0, 0.38), bevel=0.04)
    t.box(M["crane"], (0.4, 0.2, 0.3), (0, 0.32, 0.56), bevel=0.03)
    t.box(M["glass"], (0.3, 0.02, 0.14), (0, -0.31, 0.5), bevel=0.01)
    t.cyl(M["lamp_amber"], 0.05, 0.08, (0.18, -0.2, 0.7), "Z", 12, bevel=0.01)
    for s in (-1, 1):
        t.box(M["steel"], (0.06, 0.24, 0.3), (s * 0.18, 0, 0.76), bevel=0.01)
    t.cyl(M["chrome"], 0.04, 0.24, (0, 0.16, 0.56), "X", 12, bevel=0.006)
    tob = t.build()
    tob.parent = b
    bm = Part("boom")
    p0, p1 = V(0, 9, 0), V(0, 30, -38)
    d = (p1 - p0)
    L = d.length
    q = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
    bm.add(kit.superloft([(z, 0, 0, 0.1 - z * 0.008, 0.13 - z * 0.01) for z in (0.0, L)], 16, 3.5), M["crane"], matrix=Matrix.Translation(p0) @ q @ Matrix.Rotation(math.pi / 2, 4, "X"))
    bm.tube(M["chrome"], [V(0, 10, -3), V(0, 20, -20)], 0.035, 12)
    bm.tube(M["black"], [V(0, 10, -3), V(0, 15, -11)], 0.05, 12)
    bm.box(M["steel"], (0.24, 0.3, 0.3), V(0, 29.5, -39), bevel=0.02)
    bm.cyl(M["black"], 0.12, 0.08, V(0, 30, -40), "X", 20, bevel=0.01)
    bm.tube(M["steel"], [V(0, 29, -40.6), V(0, 14, -40.6)], 0.006, 4, caps=False)
    bm.box(M["hazard"], (0.14, 0.1, 0.16), V(0, 13, -40.6), bevel=0.015)
    bm.tube(M["steel"], [V(0, 12, -40.6), V(0, 11, -40.6), V(0, 10.4, -41.6), V(0, 11, -42.4)], 0.014, 8)
    for i in range(4):
        bm.box(M["black"], (0.21, 0.02, 0.05), p0 + d * (0.3 + i * 0.15) + Vector((0, 0, 0.12)), bevel=0)
    bob = bm.build()
    kit.recentre(bob, p0)
    bob.parent = tob
    bob.location = p0
    return b


def jerry_can(name="item_jerrycan"):
    """Jerry can (item / cargo prop): 0.47 x 0.345 x 0.165 m, pressed X ribs, three handles, spout."""
    p = Part(name)
    w, h, d = 0.165, 0.47, 0.345
    p.box(M["jerry"], (w, d, h), (0, 0, h / 2), bevel=0.02, segs=2)
    for s in (-1, 1):
        for a in (0.62, -0.62):
            p.add(kit.bm_box(0.01, 0.36, 0.035, bevel=0.004, segs=1), M["jerry"], (s * w / 2, 0, h * 0.45), (a, 0, 0))
        p.add(kit.bm_box(0.008, d * 0.86, h * 0.84, bevel=0.003, segs=1), M["jerry"], (s * (w / 2 - 0.002), 0, h * 0.47))
    for k in range(3):
        y = -0.1 + k * 0.1
        p.add(kit.bm_box(0.03, 0.022, 0.07, bevel=0.006), M["jerry"], (0, y, h + 0.03))
    p.box(M["jerry"], (0.03, 0.26, 0.02), (0, 0.0, h + 0.07), bevel=0.006)
    p.cyl(M["steel"], 0.026, 0.04, (0, -0.15, h + 0.02), "Z", 14, bevel=0.004)
    p.box(M["steel"], (0.06, 0.04, 0.02), (0, -0.17, h + 0.05), bevel=0.004)
    return p.build()


def utility():
    objs = [exhaust_stack(), side_pipes(), radiator("radiator_car", 7, 5), radiator("radiator_bigcore", 8, 8, True), cargo_crane(), jerry_can()]
    kit.grid(objs, 3, 0.35)
    return objs


if __name__ == "__main__":
    kit.run_group([(engines_car, "parts_engines_car", "Engines: V8 blower, truck diesel, diesel I6, I4, two-stroke", "engine_v8_blower, engine_truck_diesel, engine_diesel_i6, engine_i4, engine_2stroke"),
                   (engines_small, "parts_engines_small", "Engines: thumper, V-twin, aero two-stroke, outboard", "engine_single, engine_vtwin, engine_2stroke_aero, engine_outboard"),
                   (wheels_road, "parts_wheels_road", "Wheels: street, sport, rain, small, bike", "wheel_street, wheel_sport, wheel_rain, wheel_small, wheel_bike"),
                   (wheels_offroad, "parts_wheels_offroad", "Wheels: all-terrain, mud, truck, track roller", "wheel_offroad, wheel_mud, wheel_truck, wheel_track"),
                   (front_armour, "parts_armour", "Bull bar, ram, spiked bumper, winch, side spikes, plate", "bumper_bull_bar, bumper_ram, bumper_spiked, bumper_winch, armor_spikes, armor_plate"),
                   (roof_kit, "parts_roof", "Roof rack, roof MG, light bar, fog pods, snorkel, rear dropper", "cargo_roof_rack, weapon_mg, lights_bar, lights_fog, snorkel, rear_dropper"),
                   (utility, "parts_utility", "Exhausts, radiators, cargo crane, jerry can", "exhaust_stack, exhaust_side_pipes, radiator_car, radiator_bigcore, cargo_crane, jerry can")], M, extra)
