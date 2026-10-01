"""HD aircraft: Ultralight (flex-wing trike) and Gyrocopter (AircraftDesigns.cs; 1 voxel = 0.08 m).

blender -b -P aircraft.py -- <out_dir>
Root empty per aircraft with Body, Glass, Wing (trike), Wheel_Nose, Wheel_Main_L/R, engine part
(engine_2stroke_aero at its socket) and spinners Prop / Rotor (origin on the hub, spin axis = game Z / Y)."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit  # noqa: E402
import comps  # noqa: E402
from kit import V, Part, Vector  # noqa: E402

M = {}


def extra(M):
    M["pod_cream"] = kit.surface("PodCream", kit.P("cream", 3), chips=0.25, chip_col=kit.P("olive", 1), rust=0.0, dust=0.4, rough=0.35, var=0.08, clearcoat=0.4, streaks=0.15)
    M["pod_ochre"] = kit.surface("PodOchre", kit.P("ochre", 3), chips=0.4, rust=0.15, dust=0.45, rough=0.45, var=0.12, streaks=0.2)
    M["sail_red"] = kit.textured("SailRed", "canvas", kit.P("crimson", 3), kit.P("crimson", 2), dust=0.15, rough=0.85, bump=0.15)
    M["sail_cream"] = kit.textured("SailCream", "canvas", kit.P("cream", 2), kit.P("cream", 3), dust=0.15, rough=0.85, bump=0.15)
    M["tube"] = kit.surface("AlloyTube", kit.P("chrome", 1), dust=0.3, metal=0.9, rough=0.35, var=0.08)


def pod(p, stations, mat, n=28):
    p.add(kit.superloft(stations, n, 2.4), mat)


def aero_wheel(name, loc, root, side=1):
    return kit.car_wheel(name, M, 0.28, 0.14, 0.14, "aero", "steel", rim_mat=M["cream"], loc=loc, side=side, parent=root, segs=32)


def ultralight():
    root = kit.empty("Ultralight")
    b = Part("Body")
    gl = Part("Glass")
    # fibreglass pod, tandem cockpit: (game z, y-centre, half-width, half-height) in voxels
    st = [(13.6, 8.0, 0.4, 0.6), (13.0, 8.0, 2.4, 2.8), (11.5, 8.4, 3.6, 3.6), (9.5, 8.6, 4.2, 3.8), (6, 8.6, 4.4, 3.8), (3, 8.4, 4.3, 3.5), (1.6, 8.2, 3.8, 3.2)]
    pod(b, [(V(0, 0, z).y, 0, V(0, y, 0).z, w * 0.08, h * 0.08) for z, y, w, h in st], M["pod_cream"])
    for z0, z1 in ((10.4, 3.0),):
        b.add(kit.superloft([(V(0, 0, z).y, 0, V(0, 12.25, 0).z, 0.3, 0.03) for z in (z0, z1)], 20, 3.0), M["black"])  # cockpit opening
    for s in (-1, 1):
        b.add(kit.superloft([(V(0, 0, z).y, s * w * 0.08 * 1.002, V(0, 9.5, 0).z, 0.004, 0.06) for z, w in ((12, 3.9), (9.5, 4.25), (3, 4.35))], 8, 2.0), M["crimson"])
    gl.add(kit.superloft([(V(0, 0, 10.6).y + d * 0.06, 0, V(0, 13.0, 0).z + d * 0.07, 0.24 - d * 0.03, 0.12) for d in (0.0, 1.0)], 24, 2.0), M["glass"])
    for z, h in ((5.4, 7.2), (0.4, 8.2)):  # seats
        b.box(M["black"], (0.36, 0.34, 0.08), V(0, 6.6, z), bevel=0.03)
        b.box(M["black"], (0.34, 0.08, 0.3), V(0, h, z - 2.6), (-0.25, 0, 0), bevel=0.03)
    comps.lamp(b, M, V(-1.2, 11.6, 13.2), 0.035, 0.03, axis="-Y")
    comps.lamp(b, M, V(1.2, 11.6, 13.2), 0.035, 0.03, axis="-Y")
    t = M["tube"]
    b.tube(t, [V(0, 5, 13), V(0, 5.6, 0), V(0, 6.2, -9)], 0.028, 12)  # keel
    b.tube(t, [V(-9, 4.8, 0), V(9, 4.8, 0)], 0.022, 10)  # main gear axle
    for s in (-1, 1):
        b.tube(t, [V(s * 8.6, 4.8, 0), V(s * 1, 9, 2)], 0.02, 10)
        b.tube(t, [V(s * 8.6, 4.8, 0), V(s * 1, 6, -6)], 0.016, 10)
    b.tube(t, [V(0, 4.6, 14), V(0, 7, 13)], 0.022, 10)  # nose leg
    b.tube(t, [V(0, 7, -2), V(0, 28, 0)], 0.034, 14)  # mast
    b.tube(t, [V(0, 11.5, 8), V(0, 28, 1)], 0.022, 12)  # front strut
    for s in (-1, 1):
        b.tube(t, [V(0, 28, 1), V(s * 5, 17, 8)], 0.018, 10)  # control A-frame
    b.tube(M["black"], [V(-5, 17, 8), V(5, 17, 8)], 0.02, 10)
    b.box(M["steel_dark"], (0.18, 0.4, 0.26), V(0, 11.5, -7), bevel=0.02)  # engine mount
    b.cyl(M["olive"], 0.15, 0.5, V(0, 9.4, -3.5), "X", 20, bevel=0.02)  # fuel tank behind the rear seat
    b.cyl(M["black"], 0.03, 0.03, V(0, 11.4, -3.5), "Z", 12)
    body = b.build(root)
    gl.build(root)
    # the flex wing: swept delta sail in red/cream panels, leading-edge tubes, crossbar, king post, rigging
    w = Part("Wing")
    y29 = V(0, 29, 0).z
    xs = [i * 62 / 16 for i in range(-16, 17)]

    def chord(x):
        ax = abs(x)
        le = 11 - ax * 0.20
        te = -8 - ax * 0.02
        return V(0, 0, le).y, V(0, 0, te).y

    for i in range(len(xs) - 1):
        secs = []
        for x in (xs[i], xs[i + 1]):
            yl, yt = chord(x)
            bill = 0.06 * (1 - abs(x) / 62)
            sec = []
            for k in range(8):
                u = k / 7
                yy = yl + (yt - yl) * u
                zz = y29 + math.sin(u * math.pi) * (0.05 + bill) - u * 0.12 * (abs(x) / 62)
                sec.append((x * 0.08, yy, zz + 0.006))
            for k in range(7, -1, -1):
                u = k / 7
                yy = yl + (yt - yl) * u
                zz = y29 + math.sin(u * math.pi) * (0.05 + bill) - u * 0.12 * (abs(x) / 62)
                sec.append((x * 0.08, yy, zz - 0.006))
            secs.append(sec)
        w.add(kit.bm_loft(secs, True), M["sail_red"] if (i // 2) % 2 == 0 else M["sail_cream"])
    for s in (-1, 1):
        w.tube(t, [V(s * 62 * k / 6, 29.1, 11 - 62 * k / 6 * 0.2) for k in range(7)], 0.025, 10)  # leading edge
        w.tube(t, [V(0, 28.6, 2), V(s * 30, 28.8, 3)], 0.02, 10)  # crossbar
    w.tube(t, [V(0, 29, 11.2), V(0, 29.4, -8.6)], 0.02, 10)  # keel tube of the wing
    w.tube(t, [V(0, 29, 0), V(0, 34, 0)], 0.016, 8)  # king post
    for q in (V(-62, 29, -1.4), V(62, 29, -1.4), V(0, 29.4, 11), V(0, 29.4, -8.6), V(-30, 29, 5), V(30, 29, 5)):
        w.tube(M["steel"], [V(0, 34, 0), q], 0.003, 4, caps=False)
    for q in (V(-62, 29, -1.4), V(62, 29, -1.4), V(-30, 29, 5), V(30, 29, 5)):
        for base in (V(-5, 17, 8), V(5, 17, 8)):
            if (q.x < 0) == (base.x < 0):
                w.tube(M["steel"], [base, q], 0.003, 4, caps=False)
    w.build(root)
    aero_wheel("Wheel_Nose", V(0, 4, 14), root)
    aero_wheel("Wheel_Main_R", V(9.8, 4, 0), root)
    aero_wheel("Wheel_Main_L", V(-9.8, 4, 0), root, -1)
    eng = comps.aero_2stroke(M)
    eng.parent = root
    eng.location = V(0, 10, -7) + Vector((0, -0.12, 0.08))
    prop = comps.propeller("Prop", M, 0.8, 2, 0.06, wood=True)
    prop.parent = root
    prop.location = V(0, 12, -11)
    return root


def gyrocopter():
    root = kit.empty("Gyrocopter")
    b = Part("Body")
    gl = Part("Glass")
    st = [(12.6, 8.4, 0.4, 0.6), (12.0, 8.6, 2.6, 3.0), (10.5, 8.8, 3.8, 4.0), (8, 9.0, 4.3, 4.2), (4.5, 9.0, 4.4, 4.2), (2, 8.8, 4.2, 3.9), (0.8, 8.6, 3.6, 3.4)]
    pod(b, [(V(0, 0, z).y, 0, V(0, y, 0).z, w * 0.08, h * 0.08) for z, y, w, h in st], M["pod_ochre"])
    for s in (-1, 1):
        b.add(kit.superloft([(V(0, 0, z).y, s * w * 0.08 * 1.003, V(0, 8.2, 0).z, 0.004, 0.03) for z, w in ((11.5, 3.3), (8, 4.3), (1.2, 3.9))], 8, 2.0), M["black"])
    # bubble canopy (Glass) over the pilot, open at the back
    gl.add(kit.superloft([(V(0, 0, z).y, 0, V(0, 11.6, 0).z + hh * 0.5, w, hh) for z, w, hh in ((11.0, 0.05, 0.02), (10.0, 0.22, 0.14), (8.0, 0.32, 0.24), (5.5, 0.33, 0.26), (4.6, 0.3, 0.24))], 24, 2.0), M["glass"])
    b.tube(M["black"], [V(-4.0, 11.6, 4.6), V(0, 15.4, 4.6), V(4.0, 11.6, 4.6)], 0.014, 8)  # canopy hoop
    b.box(M["black"], (0.34, 0.32, 0.08), V(0, 6.6, 3), bevel=0.03)
    b.box(M["black"], (0.34, 0.08, 0.36), V(0, 9.4, 0.8), (-0.2, 0, 0), bevel=0.03)
    comps.lamp(b, M, V(0, 9.2, 12.5), 0.05, 0.04, axis="-Y")
    t = M["tube"]
    b.tube(t, [V(0, 5, 12), V(0, 5.6, 0), V(0, 6.2, -10)], 0.03, 12)
    b.tube(t, [V(0, 11, -8), V(0, 12, -27)], 0.04, 14)  # tail boom
    b.tube(t, [V(0, 6.2, -10), V(0, 11, -9.6)], 0.022, 10)
    b.tube(t, [V(-8, 4.8, 0), V(8, 4.8, 0)], 0.024, 10)
    for s in (-1, 1):
        b.tube(t, [V(s * 7.6, 4.8, 0), V(s * 1, 9, 2)], 0.02, 10)
        b.tube(t, [V(s * 7.6, 4.8, 0), V(s * 1, 6, -6)], 0.016, 10)
    b.tube(t, [V(0, 4.6, 13), V(0, 7, 12)], 0.024, 10)
    b.tube(t, [V(0, 8, -2), V(0, 33, 0)], 0.04, 14)  # mast
    for s in (-1, 1):
        b.tube(t, [V(s * 3, 8.6, 1), V(0, 26, -0.4)], 0.014, 8)
    b.box(M["chrome"], (0.16, 0.16, 0.16), V(0, 32.5, 0), bevel=0.03)  # rotor head
    b.cyl(M["chrome"], 0.05, 0.12, V(0, 31.6, 0.8), "Y", 12, bevel=0.01)  # pre-rotator drive
    b.tube(M["black"], [V(0, 31.6, 0.4), V(0.6, 20, 0.6), V(0.6, 11, -6)], 0.008, 6)  # pre-rotator shaft
    # tail: tailplane + twin fins with red tops and rudders
    tp = kit.bm_loft([[(x, V(0, 0, z).y, V(0, 12, 0).z + dz) for (z, dz) in ((-22.8, 0.0), (-24.5, 0.02), (-27, 0.0), (-24.5, -0.02))] for x in (-0.68, 0.68)])
    b.add(tp, M["pod_ochre"])
    for s in (-1, 1):
        fin = kit.bm_loft([[(V(s * 8, 0, 0).x + dx, V(0, 0, z).y, V(0, y, 0).z) for (z, y) in ((-23, 12), (-25.6, 18.4), (-27.4, 18.4), (-27.4, 12))] for dx in (-0.012, 0.012)])
        b.add(fin, M["pod_ochre"])
        tip = kit.bm_loft([[(V(s * 8, 0, 0).x + dx, V(0, 0, z).y, V(0, y, 0).z) for (z, y) in ((-25.0, 17.0), (-25.6, 18.5), (-27.5, 18.5), (-27.5, 17.0))] for dx in (-0.014, 0.014)])
        b.add(tip, M["crimson"])
    b.box(M["steel_dark"], (0.18, 0.4, 0.26), V(0, 9.5, -7), bevel=0.02)
    body = b.build(root)
    gl.build(root)
    aero_wheel("Wheel_Nose", V(0, 4, 13), root)
    aero_wheel("Wheel_Main_R", V(8.8, 4, 0), root)
    aero_wheel("Wheel_Main_L", V(-8.8, 4, 0), root, -1)
    eng = comps.aero_2stroke(M)
    eng.parent = root
    eng.location = V(0, 8, -6) + Vector((0, -0.12, 0.08))
    prop = comps.propeller("Prop", M, 0.72, 2, 0.06, wood=True)
    prop.parent = root
    prop.location = V(0, 10, -10.2)
    # rotor: 8 m two-blade, hub on the head, spins about +Z
    r = Part("Rotor")
    r.cyl(M["chrome"], 0.12, 0.08, (0, 0, 0), "Z", 20, bevel=0.01)
    r.box(M["chrome"], (0.5, 0.12, 0.05), (0, 0, 0.0), bevel=0.01)
    for s in (-1, 1):
        secs = []
        for k in range(11):
            x = s * (0.22 + k * (4.0 - 0.22) / 10)
            ch = 0.2
            th = 0.022
            secs.append([(x, -ch * 0.35, 0.0), (x, 0.0, th * 0.5), (x, ch * 0.65, 0.0), (x, 0.0, -th * 0.5)])
        r.add(kit.bm_loft(secs), M["tube"])
        tip = [[(s * xx, y * 1.02, z * 1.2) for (_, y, z) in secs[-1]] for xx in (3.62, 4.02)]
        r.add(kit.bm_loft(tip), M["crimson"])
    rot = r.build(root)
    rot.location = V(0, 34, 0)
    return root


if __name__ == "__main__":
    kit.run_group([(ultralight, "air_ultralight", "Ultralight trike (flex wing, pusher)", "Ultralight"),
                   (gyrocopter, "air_gyrocopter", "Gyrocopter (8 m rotor, twin fins)", "Gyrocopter")], M, extra)
