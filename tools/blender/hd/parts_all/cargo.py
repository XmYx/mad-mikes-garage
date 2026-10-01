"""HD cargo-socket parts: roof rack, cargo box, water tank, onboard generator, small wind turbine (segment "rotor"),
jerry rack, twin fuel tanks, guild crate, knuckle crane (segments "turret" -> "boom").

blender -b -P tools/blender/hd/parts_all/cargo.py [-- ids]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pa  # noqa: E402
from pa import M, Part, Vector, Matrix, V, G, kit, bm_box  # noqa: E402
import comps  # noqa: E402
import parts as mp  # noqa: E402

mp.M = M
F = "cargo"


def crane():
    b = mp.cargo_crane()
    for c in b.children_recursive:
        nm = c.name.split(".")[0]
        if nm in ("turret", "boom"):
            pa.seg(c, nm)
    return b


def cargo_box():
    """cargo_box: ribbed steel locker with a lid, hasp + padlock, stencil, side handles."""
    p = Part("cargo_box")
    W, D, H = 1.68, 1.04, 0.64
    p.box(M["steel"], (W, D, H), (0, 0, H / 2), bevel=0.015)
    for k in range(-5, 6):
        x = k * 0.16
        p.box(M["steel_dark"], (0.03, D + 0.02, H - 0.06), (x, 0, H / 2), bevel=0.006)
    p.box(M["steel"], (W + 0.03, D + 0.03, 0.06), (0, 0, H + 0.03), bevel=0.012)                # lid
    p.box(M["steel_dark"], (W + 0.02, 0.03, 0.04), (0, D / 2 + 0.01, H + 0.0), bevel=0.004)    # hinge line (rear)
    p.box(M["chrome"], (0.12, 0.02, 0.18), (0, -D / 2 - 0.01, H - 0.06), bevel=0.004)          # hasp
    p.add(kit.bm_torus(0.03, 0.007, 14, 5), M["chrome"], (0, -D / 2 - 0.04, H - 0.17), (math.pi / 2, 0, 0))
    p.box(M["brass"], (0.06, 0.025, 0.07), (0, -D / 2 - 0.04, H - 0.22), bevel=0.008)         # padlock
    p.box(M["ochre"], (0.4, 0.01, 0.12), (-0.45, -D / 2 - 0.006, H * 0.4), bevel=0.002)        # stencil
    for s in (-1, 1):
        p.tube(M["black"], [(s * (W / 2 + 0.0), -0.16, H * 0.45), (s * (W / 2 + 0.05), -0.12, H * 0.45), (s * (W / 2 + 0.05), 0.12, H * 0.45), (s * W / 2, 0.16, H * 0.45)], 0.012, 6)
        for y in (-D / 2 + 0.05, D / 2 - 0.05):
            p.box(M["black"], (0.08, 0.08, 0.04), (s * (W / 2 - 0.06), y, 0.0), bevel=0.01)
    return p.build()


def water_tank():
    """cargo_water_tank: moulded poly tank (horizontal cylinder, ribs) in a steel cradle, filler, tap, level window."""
    p = Part("cargo_water_tank")
    L, R = 1.64, 0.38
    c = Vector((0, 0, 0.42))
    p.lathe(M["poly"], [(0.0, -L / 2), (R * 0.9, -L / 2), (R, -L / 2 + 0.05), (R, L / 2 - 0.05), (R * 0.9, L / 2), (0.0, L / 2)], c, "X", 36)
    for k in range(-3, 4):
        pa.ring(p, M["poly"], c + Vector((k * 0.2, 0, 0)), R + 0.004, 0.012, "X", 36, 5)
    p.cyl(M["plastic_blue"], 0.1, 0.06, c + Vector((0.0, 0, R + 0.02)), "Z", 20, bevel=0.01)       # filler cap
    p.box(M["plastic_blue"], (0.4, 0.01, 0.1), c + Vector((0, -R - 0.002, 0.04)), bevel=0.002)     # level window
    p.tube(M["chrome"], [c + Vector((-L / 2 + 0.02, -0.25, -0.22)), c + Vector((-L / 2 - 0.06, -0.25, -0.22)), c + Vector((-L / 2 - 0.06, -0.25, -0.3))], 0.015, 8)
    p.box(M["black"], (0.05, 0.02, 0.03), c + Vector((-L / 2 - 0.06, -0.25, -0.2)), bevel=0.004)
    for x in (-0.72, 0.72):                                       # cradle
        p.box(M["steel_dark"], (0.06, 0.84, 0.06), (x, 0, 0.03), bevel=0.01)
        for y in (-0.4, 0.4):
            p.box(M["steel_dark"], (0.06, 0.06, 0.28), (x, y, 0.16), bevel=0.008)
        p.tube(M["webbing"], [(x, -0.4, 0.3), (x, -0.3, 0.68), (x, 0.0, 0.81), (x, 0.3, 0.68), (x, 0.4, 0.3)], 0.012, 6)
    p.box(M["steel_dark"], (1.5, 0.06, 0.06), (0, -0.4, 0.06), bevel=0.01)
    p.box(M["steel_dark"], (1.5, 0.06, 0.06), (0, 0.4, 0.06), bevel=0.01)
    return p.build()


def generator():
    """cargo_generator: open tube frame, small single-cylinder engine, alternator, red fuel tank, panel, exhaust."""
    p = Part("cargo_generator")
    W, D, H = 1.36, 0.88, 0.76
    for x in (-W / 2, W / 2):
        for y in (-D / 2, D / 2):
            pa.rod(p, M["steel_dark"], (x, y, 0), (x, y, H), 0.022, 10)
        pa.rod(p, M["steel_dark"], (x, -D / 2, H), (x, D / 2, H), 0.022, 10)
        pa.rod(p, M["steel_dark"], (x, -D / 2, 0.02), (x, D / 2, 0.02), 0.022, 10)
    for y in (-D / 2, D / 2):
        pa.rod(p, M["steel_dark"], (-W / 2, y, H), (W / 2, y, H), 0.022, 10)
        pa.rod(p, M["steel_dark"], (-W / 2, y, 0.02), (W / 2, y, 0.02), 0.022, 10)
    p.box(M["cat"], (0.56, 0.6, 0.44), (-0.2, 0, 0.3), bevel=0.03)                              # engine
    comps.finned_cylinder(p, M, (-0.32, 0, 0.5), (-0.36, 0, 0.66), 0.07, 6, 0.12, M["alu"])
    p.cyl(M["steel"], 0.22, 0.34, (0.3, 0, 0.3), "X", 28, bevel=0.02)                            # alternator
    for k in range(8):
        a = k / 8 * math.tau
        p.box(M["steel_dark"], (0.3, 0.02, 0.02), (0.3, math.cos(a) * 0.222, 0.3 + math.sin(a) * 0.222), (a, 0, 0), bevel=0.002)
    p.add(kit.superloft([(y, -0.08, 0.66, 0.34, 0.07) for y in (-0.24, 0.24)], 20, 3.0), M["crimson"])   # fuel tank
    p.cyl(M["black"], 0.04, 0.03, (-0.2, 0.0, 0.74), "Z", 14, bevel=0.005)
    p.box(M["plastic"], (0.16, 0.04, 0.24), (0.55, -0.42, 0.42), bevel=0.01)                   # panel
    p.cyl(M["lamp_amber"], 0.018, 0.012, (0.55, -0.445, 0.48), "Y", 10, bevel=0)
    for k in range(2):
        p.cyl(M["black"], 0.018, 0.02, (0.52 + k * 0.06, -0.45, 0.38), "Y", 10, bevel=0)
    p.tube(M["rust"], [(-0.48, -0.2, 0.36), (-0.56, -0.24, 0.4), (-0.56, -0.24, 0.9)], 0.03, 10)
    p.cyl(M["black"], 0.04, 0.08, (-0.56, -0.24, 0.86), "Z", 12, bevel=0.005)
    for x in (-0.4, 0.3):
        p.box(M["rubber"], (0.08, 0.6, 0.04), (x, 0, 0.05), bevel=0.01)
    return p.build()


def wind_turbine():
    """cargo_wind_turbine: mast on a foot plate, nacelle with tail vane, three-blade rotor segment (pivot at the hub)."""
    p = Part("cargo_wind_turbine")
    p.box(M["steel"], (0.52, 0.52, 0.1), (0, 0, 0.05), bevel=0.01)
    pa.bolts(p, M["chrome"], [(sx * 0.2, sy * 0.2, 0.105) for sx in (-1, 1) for sy in (-1, 1)], 0.014, 0.012)
    p.cyl(M["steel"], 0.06, 1.95, (0, 0, 1.05), "Z", 16, bevel=0.005)
    for z in (0.68, 1.32):
        p.cyl(M["rust"], 0.085, 0.06, (0, 0, z), "Z", 16, bevel=0.01)
    hub = G(0, 2.24, 0.4)
    p.add(kit.superloft([(y, 0, 2.24, w, h) for y, w, h in ((-0.42, 0.1, 0.1), (-0.3, 0.17, 0.15), (0.2, 0.16, 0.14), (0.42, 0.1, 0.1))], 20, 2.4), M["cream"])
    p.box(M["rust"], (0.02, 0.4, 0.42), (0, 0.66, 2.32), bevel=0.006)                           # tail vane
    pa.rod(p, M["steel"], (0, 0.38, 2.26), (0, 0.6, 2.28), 0.016, 8)
    p.cyl(M["lamp_amber"], 0.025, 0.04, (0, 0.3, 2.42), "Z", 10)
    body = p.build()
    r = Part("rotor")
    r.lathe(M["steel"], [(0.0, -0.06), (0.1, -0.06), (0.11, 0.0), (0.06, 0.08), (0.0, 0.1)], (0, 0, 0), "Y", 24)
    for b in range(3):
        a = b * math.tau / 3 + 0.4
        secs = []
        for k in range(7):
            t = k / 6
            rr = 0.1 + t * 0.86
            ch = 0.12 * (1 - t) + 0.04
            tw = 0.5 * (1 - t) + 0.1
            sec = []
            for (cx, cz) in ((-ch / 2, 0), (0, 0.012), (ch / 2, 0), (0, -0.01)):
                sec.append((cx * math.cos(tw), cx * math.sin(tw) + cz, rr))
            secs.append(sec)
        r.add(kit.bm_loft(secs), M["cream"], matrix=Matrix.Rotation(a, 4, "Y"))
    rot = r.build(body)
    rot.location = hub
    pa.seg(rot, "rotor")
    return body


def jerry_rack():
    """cargo_jerry_rack: tube rack with three jerry cans, a tarp roll and a whip antenna with a red tip."""
    p = Part("cargo_jerry_rack")
    r = M["bar_steel"]
    for x in (-0.88, 0.88):
        pa.rod(p, r, (x, -0.48, 0.08), (x, 0.48, 0.08), 0.018, 8)
        for y in (-0.48, 0.48):
            pa.rod(p, r, (x, y, 0), (x, y, 0.28), 0.018, 8)
            p.box(M["black"], (0.06, 0.06, 0.03), (x, y, 0.0), bevel=0.008)
    for y in (-0.48, 0.0, 0.48):
        pa.rod(p, r, (-0.88, y, 0.08), (0.88, y, 0.08), 0.018, 8)
    ob = p.build()
    for i, x in enumerate((-0.44, 0.0, 0.44)):
        jc = mp.jerry_can("jc%d" % i)
        jc.data.transform(Matrix.Translation((x, -0.18, 0.1)) @ Matrix.Rotation(math.pi / 2, 4, "Z"))
        jc.parent = ob
    q = Part("roll")
    q.add(kit.bm_cyl(0.11, 1.36, 24, 0.02), M["tarp"], (0, 0.24, 0.22), (0, math.pi / 2, 0))
    for x in (-0.4, 0.4):
        pa.ring(q, M["rope"], (x, 0.24, 0.22), 0.115, 0.012, "X", 24, 6)
    pa.rod(q, M["steel_dark"], (0.72, 0.4, 0.1), (0.72, 0.4, 1.66), 0.006, 6)                      # whip antenna
    q.sphere(M["lamp_red"], 0.03, (0.72, 0.4, 1.68))
    q.cyl(M["black"], 0.03, 0.08, (0.72, 0.4, 0.12), "Z", 12, bevel=0.006)
    q.build(ob)
    return ob


def twin_tanks():
    """cargo_twin_fuel_tanks: two horizontal steel tanks on straps, filler caps, a crossover pipe."""
    p = Part("cargo_twin_fuel_tanks")
    for y in (-0.12, 0.12):
        p.lathe(M["steel"], [(0.0, -0.76), (0.1, -0.76), (0.115, -0.73), (0.115, 0.73), (0.1, 0.76), (0.0, 0.76)], (0, y, 0.12), "X", 28)
        for x in (-0.4, 0.4):
            pa.ring(p, M["black"], (x, y, 0.12), 0.12, 0.012, "X", 28, 5)
    p.cyl(M["chrome"], 0.03, 0.04, (0.56, -0.12, 0.25), "Z", 14, bevel=0.005)
    p.cyl(M["chrome"], 0.03, 0.04, (-0.56, 0.12, 0.25), "Z", 14, bevel=0.005)
    p.tube(M["rubber"], [(-0.76, -0.12, 0.06), (-0.8, -0.06, 0.04), (-0.8, 0.06, 0.04), (-0.76, 0.12, 0.06)], 0.014, 8)
    for x in (-0.4, 0.4):
        p.box(M["steel_dark"], (0.06, 0.48, 0.03), (x, 0, 0.0), bevel=0.006)
    return p.build()


def crate():
    """cargo_crate: Fuel Guild haulage crate: nailed plywood with corner battens, ochre band and stencil."""
    p = Part("cargo_crate")
    S, H = 0.72, 0.64
    p.box(M["wood"], (S, S, H), (0, 0, H / 2), bevel=0.01)
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.box(M["wood_dark"], (0.06, 0.06, H + 0.01), (sx * (S / 2 - 0.02), sy * (S / 2 - 0.02), H / 2), bevel=0.006)
    for z in (0.03, H - 0.03):
        for ax in (0, 1):
            for s in (-1, 1):
                size = (S + 0.01, 0.05, 0.06) if ax == 0 else (0.05, S + 0.01, 0.06)
                loc = (0, s * (S / 2 - 0.015), z) if ax == 0 else (s * (S / 2 - 0.015), 0, z)
                p.box(M["wood_dark"], size, loc, bevel=0.005)
    for ax in (0, 1):
        for s in (-1, 1):
            size = (S + 0.012, 0.012, 0.14) if ax == 0 else (0.012, S + 0.012, 0.14)
            loc = (0, s * S / 2, 0.3) if ax == 0 else (s * S / 2, 0, 0.3)
            p.box(M["ochre"], size, loc, bevel=0.002)
    for y in (-1, 1):                                                    # stencil chevron
        for k, (dx, dz) in enumerate(((-0.06, 0.44), (0.0, 0.5), (0.06, 0.44))):
            p.box(M["black"], (0.05, 0.006, 0.05), (dx, y * (S / 2 + 0.004), dz), bevel=0.0)
    p.box(M["wood_light"], (S - 0.08, S - 0.08, 0.02), (0, 0, H + 0.005), bevel=0.003)
    pa.bolts(p, M["steel"], [(sx * 0.3, sy * 0.3, H + 0.016) for sx in (-1, 1) for sy in (-1, 1)], 0.006, 0.004)
    return p.build()


C = "Cargo"
pa.add("cargo_roof_rack", mp.roof_rack, F, category=C, socket="cargo", tile="cargo_1")
pa.add("cargo_box", cargo_box, F, category=C, socket="cargo", tile="cargo_1")
pa.add("cargo_water_tank", water_tank, F, category=C, socket="cargo", tile="cargo_1")
pa.add("cargo_generator", generator, F, category=C, socket="cargo", tile="cargo_1")
pa.add("cargo_jerry_rack", jerry_rack, F, category=C, socket="cargo", tile="cargo_2")
pa.add("cargo_twin_fuel_tanks", twin_tanks, F, category=C, socket="cargo", tile="cargo_2")
pa.add("cargo_crate", crate, F, category=C, socket="cargo (or loose)", tile="cargo_2")
pa.add("cargo_wind_turbine", wind_turbine, F, category=C, socket="cargo", tile="cargo_crane")
pa.add("cargo_crane", crane, F, category=C, socket="cargo", tile="cargo_crane")

if __name__ == "__main__":
    pa.run(F)
