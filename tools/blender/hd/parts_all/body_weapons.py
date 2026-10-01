"""HD body add-ons (snorkel, spoilers, side steps, roof ladder) and vehicle weapons with their game segments:
weapon_mg (mount -> gun), weapon_harpoon (mount -> launcher), weapon_flamer (mount -> nozzle), weapon_turret_cannon.
Side parts are authored for the right side (game +X out). Barrels point forward (game +Z = kit -Y).

blender -b -P tools/blender/hd/parts_all/body_weapons.py [-- ids]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pa  # noqa: E402
from pa import M, Part, Vector, Matrix, V, G, kit, bm_box  # noqa: E402
import comps  # noqa: E402
import parts as mp  # noqa: E402

mp.M = M
F = "body_weapons"


def ring_base(name, r=0.29):
    p = Part(name)
    p.lathe(M["steel_dark"], [(r * 0.7, 0), (r, 0), (r, 0.05), (r * 0.9, 0.08), (r * 0.7, 0.08)], (0, 0, 0), "Z", 40, close=True)
    for i in range(12):
        a = i / 12 * math.tau
        p.cyl(M["chrome"], 0.008, 0.012, (math.cos(a) * r * 0.88, math.sin(a) * r * 0.88, 0.085), "Z", 6, bevel=0.002)
    return p


def mg():
    b = comps.machine_gun(M)
    for c in b.children_recursive:
        nm = c.name.split(".")[0].lower()
        if nm in ("mount", "gun"):
            pa.seg(c, nm)
    return b


def harpoon():
    """weapon_harpoon: slew ring; "mount" = rusty cradle with trunnions; "launcher" = launch tube, barbed bolt head,
    cable drum behind, sight."""
    base = ring_base("weapon_harpoon").build()
    m = Part("mount")
    m.box(M["rust"], (0.5, 0.36, 0.2), (0, 0, 0.26), bevel=0.02)
    for s in (-1, 1):
        m.box(M["rust"], (0.05, 0.2, 0.26), (s * 0.22, 0, 0.42), bevel=0.01)
    m.cyl(M["steel"], 0.07, 0.08, (0, 0, 0.18), "Z", 16, bevel=0.008)
    mo = m.build(base)
    pa.seg(mo, "mount", G(0, 0.16, 0))
    l = Part("launcher")
    c = G(0, 0.48, 0)
    l.cyl(M["steel"], 0.1, 1.24, c + Vector((0, -0.2, 0)), "Y", 24, bevel=0.01)
    for k in range(5):
        pa.ring(l, M["steel_dark"], c + Vector((0, -0.7 + k * 0.25, 0)), 0.105, 0.012, "Y", 24, 5)
    l.cyl(M["steel_dark"], 0.12, 0.06, c + Vector((0, -0.8, 0)), "Y", 24, bevel=0.008)
    tip = c + Vector((0, -0.84, 0))
    l.cyl(M["steel"], 0.025, 0.12, tip + Vector((0, -0.06, 0)), "Y", 10)
    l.add(kit.bm_cyl(0.05, 0.16, 4, 0.0, 0.0), M["chrome"], tip + Vector((0, -0.2, 0)), (math.pi / 2, 0, 0))
    for s in (-1, 1):
        kit.oriented(l, kit.bm_cyl(0.012, 0.12, 6, 0.0, 0.002), M["chrome"], tip + Vector((0, -0.16, 0)), tip + Vector((s * 0.07, -0.08, 0)))
    for s in (-1, 1):
        l.cyl(M["steel"], 0.04, 0.06, c + Vector((s * 0.15, 0, 0)), "X", 14, bevel=0.006)          # trunnions
    d = c + Vector((0, 0.52, 0))
    l.cyl(M["black"], 0.15, 0.3, d, "X", 28, bevel=0.01)                                            # cable drum
    for s in (-1, 1):
        l.cyl(M["steel"], 0.17, 0.02, d + Vector((s * 0.16, 0, 0)), "X", 28, bevel=0.004)
    for k in range(9):
        pa.ring(l, M["steel"], d + Vector((-0.13 + k * 0.032, 0, 0)), 0.152, 0.008, "X", 20, 4)
    l.box(M["brass"], (0.3, 0.04, 0.02), d + Vector((0, -0.02, 0.16)), bevel=0.003)
    l.tube(M["steel"], [d + Vector((0, -0.1, 0.12)), c + Vector((0, 0.1, 0.1)), c + Vector((0, -0.6, 0.11))], 0.006, 4)
    l.box(M["black"], (0.02, 0.1, 0.06), c + Vector((0, -0.4, 0.13)), bevel=0.003)                   # sight
    lo = l.build()
    pa.seg(lo, "launcher", c, mo)
    return base


def flamer():
    """weapon_flamer: slew ring with two red fuel tanks behind; "mount" = short pintle; "nozzle" = pipe forward with a
    flared black nozzle, pilot light, hose back to the tanks."""
    p = ring_base("weapon_flamer")
    for x in (-0.24, 0.24):
        c = Vector((x, 0.24, 0.36))
        p.lathe(M["crimson"], [(0.0, -0.22), (0.08, -0.22), (0.11, -0.19), (0.11, 0.19), (0.08, 0.22), (0.0, 0.22)], c, "Z", 24)
        p.cyl(M["chrome"], 0.03, 0.04, c + Vector((0, 0, 0.24)), "Z", 12, bevel=0.004)
        for z in (-0.1, 0.12):
            pa.ring(p, M["black"], c + Vector((0, 0, z)), 0.112, 0.01, "Z", 24, 4)
    p.box(M["steel_dark"], (0.6, 0.06, 0.1), (0, 0.3, 0.12), bevel=0.01)
    base = p.build()
    m = Part("mount")
    m.cyl(M["steel"], 0.06, 0.22, (0, 0, 0.27), "Z", 16, bevel=0.006)
    m.box(M["steel"], (0.16, 0.1, 0.05), (0, 0, 0.37), bevel=0.008)
    mo = m.build(base)
    pa.seg(mo, "mount", G(0, 0.16, 0))
    n = Part("nozzle")
    c = G(0, 0.4, 0)
    n.cyl(M["steel"], 0.04, 1.08, c + Vector((0, -0.38, 0)), "Y", 16, bevel=0.004)
    for k in range(4):
        pa.ring(n, M["steel_dark"], c + Vector((0, -0.1 - k * 0.22, 0)), 0.045, 0.008, "Y", 16, 4)
    tip = c + Vector((0, -0.92, 0))
    n.lathe(M["black"], [(0.04, 0.0), (0.09, -0.04), (0.11, -0.16), (0.1, -0.18), (0.06, -0.18)], tip, "Y", 24)
    n.cyl(M["steel"], 0.012, 0.16, tip + Vector((0.0, 0.06, 0.1)), "Y", 8)
    n.sphere(M["flame"], 0.022, tip + Vector((0, -0.04, 0.1)))
    n.box(M["black"], (0.08, 0.14, 0.12), c + Vector((0, 0.14, -0.02)), bevel=0.01)                 # valve block / grip
    n.tube(M["rubber"], [c + Vector((0, 0.2, -0.04)), c + Vector((0.1, 0.3, -0.12)), (0.24, 0.24, 0.6)], 0.02, 8)
    no = n.build()
    pa.seg(no, "nozzle", c, mo)
    return base


def turret():
    """weapon_turret_cannon: ring, riveted olive housing with sloped front, hatch, mantlet, long barrel with a muzzle
    brake, ammo box on the right, whip antenna."""
    p = ring_base("weapon_turret_cannon", 0.36)
    hull = M["olive"]
    secs = []
    for (y, w, z0, z1) in ((-0.36, 0.6, 0.16, 0.46), (-0.2, 0.7, 0.16, 0.56), (0.24, 0.72, 0.16, 0.58), (0.42, 0.66, 0.16, 0.52)):
        secs.append([(-w / 2, y, z0), (w / 2, y, z0), (w / 2, y, z1), (-w / 2, y, z1)])
    p.add(kit.bm_loft(secs), hull)
    p.box(hull, (0.52, 0.52, 0.06), (0, 0.04, 0.6), bevel=0.015)
    p.cyl(M["steel"], 0.14, 0.04, (0, 0.1, 0.65), "Z", 24, bevel=0.008)                            # hatch
    p.box(M["steel_dark"], (0.06, 0.03, 0.02), (0, 0.1, 0.68), bevel=0.004)
    for k in range(4):
        for s in (-1, 1):
            p.cyl(M["chrome"], 0.012, 0.012, (s * 0.355, -0.2 + k * 0.18, 0.5), "X", 6, bevel=0.002)
    p.box(M["steel"], (0.36, 0.1, 0.22), (0, -0.4, 0.34), bevel=0.02)                              # mantlet
    p.cyl(M["steel"], 0.085, 1.3, (0, -1.08, 0.34), "Y", 20, bevel=0.006)
    p.cyl(M["steel_dark"], 0.1, 0.22, (0, -0.56, 0.34), "Y", 20, bevel=0.01)
    p.box(M["steel_dark"], (0.24, 0.2, 0.2), (0, -1.78, 0.34), bevel=0.02)                         # muzzle brake
    for s in (-1, 1):
        p.box(M["black"], (0.02, 0.12, 0.12), (s * 0.12, -1.78, 0.34), bevel=0.003)
    p.cyl(M["black"], 0.05, 0.01, (0, -1.885, 0.34), "Y", 16, bevel=0)
    p.box(M["rust"], (0.18, 0.36, 0.3), (0.44, 0.06, 0.3), bevel=0.02)                            # ammo box
    p.box(M["steel_dark"], (0.04, 0.1, 0.03), (0.54, 0.06, 0.42), bevel=0.004)
    pa.rod(p, M["steel_dark"], (-0.24, 0.4, 0.58), (-0.24, 0.48, 1.4), 0.006, 6)
    p.cyl(M["black"], 0.03, 0.06, (-0.24, 0.4, 0.6), "Z", 10, bevel=0.005)
    return p.build()


def ducktail():
    """spoiler_ducktail: low kicked-up lip on the boot lid with a chrome trim strip."""
    p = Part("spoiler_ducktail")
    W = 1.84
    prof = [(0.16, 0.0), (0.16, 0.03), (0.06, 0.05), (-0.06, 0.1), (-0.2, 0.2), (-0.24, 0.2), (-0.2, 0.14), (-0.06, 0.04), (0.06, 0.0)]
    p.add(kit.bm_loft([[(x, -y, z) for (y, z) in prof] for x in (-W / 2, W / 2)]), M["black"])
    p.box(M["chrome"], (W * 0.92, 0.02, 0.02), (0, -0.165, 0.015), bevel=0.004)
    return p.build()


def spoiler_rear():
    """spoiler_rear: wing on two swan-neck posts with end plates."""
    p = Part("spoiler_rear")
    W = 1.84
    for x in (-0.64, 0.64):
        p.box(M["black"], (0.05, 0.1, 0.18), (x, 0.0, 0.08), bevel=0.01)
        pa.bolts(p, M["chrome"], [(x, 0.0, 0.0)], 0.014, 0.01)
    foil = [(0.12, 0.0), (0.06, 0.03), (-0.04, 0.035), (-0.12, 0.012), (-0.12, 0.0), (0.0, -0.006)]
    p.add(kit.bm_loft([[(x, y, 0.18 + z) for (y, z) in foil] for x in (-W / 2, W / 2)]), M["black"])
    p.box(M["crimson"], (W, 0.012, 0.012), (0, 0.118, 0.19), bevel=0.003)                          # gurney flap
    for s in (-1, 1):
        p.box(M["black"], (0.012, 0.26, 0.12), (s * W / 2, 0.0, 0.22), bevel=0.004)
    return p.build()


def steps_side():
    """steps_side: diamond-plate running board on two brackets, black rubber edge."""
    p = Part("steps_side")
    L = 1.52
    p.box(M["diamond"], (0.24, L, 0.03), (0.12, 0, 0.0), bevel=0.006)
    p.box(M["rubber"], (0.03, L, 0.05), (0.255, 0, 0.0), bevel=0.01)
    for y in (-0.48, 0.48):
        p.box(M["steel_dark"], (0.06, 0.05, 0.18), (0.02, y, 0.08), bevel=0.008)
        p.box(M["steel_dark"], (0.2, 0.05, 0.04), (0.1, y, -0.03), bevel=0.006)
    for y in (-0.7, 0.7):
        p.box(M["chrome"], (0.24, 0.04, 0.035), (0.12, y, 0.0), bevel=0.006)
    return p.build()


def ladder():
    """steps_ladder: two rails up to the roof edge with hooked tops, chrome rungs, two stand-off brackets."""
    p = Part("steps_ladder")
    for y in (-0.24, 0.24):
        pa.rod(p, M["steel"], (0.08, y, 0.0), (0.08, y, 1.22), 0.018, 10)
        p.tube(M["steel"], [(0.08, y, 1.2), (0.06, y, 1.3), (0.0, y, 1.34), (0.0, y, 1.38)], 0.018, 10)
        p.box(M["rubber"], (0.05, 0.05, 0.03), (0.08, y, 0.0), bevel=0.008)
    for k in range(5):
        z = 0.08 + k * 0.24
        pa.rod(p, M["chrome"], (0.08, -0.24, z), (0.08, 0.24, z), 0.014, 8)
    for z in (0.16, 0.96):
        p.box(M["steel_dark"], (0.08, 0.5, 0.03), (0.04, 0, z), bevel=0.006)
    return p.build()


S = dict(azim=-45)
pa.add("snorkel", mp.snorkel, F, category="Snorkel", socket="snorkel (right side)", tile="body_addons", **S)
pa.add("spoiler_ducktail", ducktail, F, category="Spoiler", socket="spoiler", tile="body_addons", **S)
pa.add("spoiler_rear", spoiler_rear, F, category="Spoiler", socket="spoiler", tile="body_addons", **S)
pa.add("steps_side", steps_side, F, category="Steps", socket="steps (right; steps_L mirrors)", tile="body_addons", **S)
pa.add("steps_ladder", ladder, F, category="Steps", socket="steps (right; steps_L mirrors)", tile="body_addons", **S)
pa.add("weapon_mg", mg, F, category="Weapon", socket="roof", tile="weapons")
pa.add("weapon_harpoon", harpoon, F, category="Weapon", socket="roof", tile="weapons")
pa.add("weapon_flamer", flamer, F, category="Weapon", socket="roof", tile="weapons")
pa.add("weapon_turret_cannon", turret, F, category="Weapon", socket="roof", tile="weapons")

if __name__ == "__main__":
    pa.run(F)
