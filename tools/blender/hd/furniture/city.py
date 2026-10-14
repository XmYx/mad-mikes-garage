"""HD city build pieces (FurnitureLibrary.City.cs, 2026-10-13): diamond-plate decks S/M/L, vehicle ramp, foot ramp,
steel stairs, railings, corrugated wall, deck ladder, gangway, shanty cabin, living pod, roof greenhouse, flower box,
the city lights (deck lamp post, searchlight, hull flood, bulkhead, string lights, lantern, barrel fire), tank on legs,
neon sign, gang banner, chimney stack and yard crane. Ported from the same CityKit builders the game meshes them with
(generator axes: x along = front, y across = -game x, z up; see citykit.py), so the HD model sits on the voxel collider.
Lamp glass is a "Bulb" object (PoweredLight swaps it to its glow); other emissive bits split into Lamp_* children.

blender -b -P city.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fcommon import K, Asset, Vector, run  # noqa: E402
import citykit as CK_  # noqa: E402
from citykit import CK  # noqa: E402


def new(gid, cat, kind="furniture", yaw=0.0):
    CK_.mats()
    return Asset(gid, cat, kind=kind, yaw=yaw)


def hazard_edge(k, x0, x1, y0, y1, z0, z1, along_y=True, step=0.3):
    """Alternating yellow / black blocks (CityKit hazard edges)."""
    if along_y:
        y, i = y0, 0
        while y < y1 - 0.01:
            k.span("deck_paint" if i % 2 == 0 else "black", x0, x1, y, min(y1, y + step), z0, z1, bevel=0.002)
            y += step
            i += 1
    else:
        x, i = x0, 0
        while x < x1 - 0.01:
            k.span("deck_paint" if i % 2 == 0 else "black", x, min(x1, x + step), y0, y1, z0, z1, bevel=0.002)
            x += step
            i += 1


# ------------------------------------------------------------------------------------------ decks, ramps, stairs
def deck_plate(gid, L, Wd):
    a = new(gid, "Structure")
    k = CK(a.p("Iron"))
    hx, hy = L / 2, Wd / 2
    k.span("deck_plate", -hx, hx, -hy, hy, 0.06, 0.16, bevel=0.012)
    for s in (-1, 1):                                                            # girders (I-beams)
        y = s * (hy - 0.15)
        k.span("steel_dark", -hx, hx, y - 0.06, y + 0.06, 0.0, 0.012, bevel=0.002)
        k.span("steel_dark", -hx, hx, y - 0.008, y + 0.008, 0.012, 0.048, bevel=0.0)
        k.span("steel_dark", -hx, hx, y - 0.06, y + 0.06, 0.048, 0.06, bevel=0.002)
    x = -hx + 0.1
    while x <= hx - 0.05:                                                        # cross members
        k.span("rust_dark", x - 0.05, x + 0.05, -hy + 0.1, hy - 0.1, 0.0, 0.06, bevel=0.004)
        x += 1.0
    for s in (-1, 1):                                                            # hazard edges front and back
        hazard_edge(k, s * hx - 0.1 if s > 0 else -hx, hx if s > 0 else -hx + 0.1, -hy, hy, 0.16, 0.172)
    # plate joints (welded seams every 1 m across) and rivet rows along the sides
    x = -hx + 1.0
    while x < hx - 0.2:
        k.span("steel_dark", x - 0.008, x + 0.008, -hy + 0.02, hy - 0.02, 0.16, 0.166, bevel=0.0)
        x += 1.0
    x = -hx + 0.15
    while x < hx:
        for s in (-1, 1):
            k.tube("chrome", (x, s * (hy - 0.05), 0.16), (x, s * (hy - 0.05), 0.172), 0.016, 6)
        x += 0.3
    for sx in (-1, 1):                                                           # lifting eyes at the corners
        for sy in (-1, 1):
            k.ring("steel", (sx * (hx - 0.18), sy * (hy - 0.18), 0.175), (1, 0, 0), 0.03, 0.009, 10, 4)
    return a


def vehicle_ramp():
    a = new("city_ramp", "Structure")
    k = CK(a.p("Iron"))
    L, Wd, H = 5.0, 3.0, 1.2
    ang = math.atan2(H, L)
    ln = math.hypot(L, H)

    def Z(x):
        return (x + L / 2) / L * H
    k.box("deck_plate", (ln, Wd, 0.12), (0, 0, H / 2), (0, -ang, 0), bevel=0.01)
    x = -L / 2 + 0.3
    while x < L / 2 - 0.1:                                                       # cleats
        k.beam("steel", (x, -Wd / 2 + 0.15, Z(x) + 0.07), (x, Wd / 2 - 0.15, Z(x) + 0.07), 0.04)
        x += 0.45
    for s in (-1, 1):
        k.beam("deck_paint", (-L / 2, s * Wd / 2, 0.12), (L / 2, s * Wd / 2, H + 0.12), 0.12)   # kerbs
        for x in (0.0, L / 2 - 0.15):
            yy = s * (Wd / 2 - 0.25)
            k.span("steel_dark", x - 0.07, x + 0.07, yy - 0.07, yy + 0.07, 0.0, Z(x))
            k.span("steel_dark", x - 0.12, x + 0.12, yy - 0.12, yy + 0.12, 0.0, 0.012, bevel=0.002)   # foot plates
            k.beam("rust", (x - 1.2, yy, Z(x - 1.2) * 0.5), (x, yy, 0.1), 0.05)
    k.span("steel_dark", L / 2 - 0.15, L / 2, -Wd / 2, Wd / 2, 0.0, H)
    hazard_edge(k, L / 2 - 0.16, L / 2 + 0.01, -Wd / 2, Wd / 2, H - 0.18, H)
    k.box("steel", (0.25, Wd - 0.1, 0.02), (L / 2 + 0.1, 0, H - 0.01), (0, 0.15, 0))   # landing lip
    return a


def foot_ramp():
    a = new("city_ramp_foot", "Structure")
    k = CK(a.p("Iron"))
    L, Wd, H = 3.0, 1.2, 0.8
    ang = math.atan2(H, L)
    ln = math.hypot(L, H)

    def Z(x):
        return (x + L / 2) / L * H
    k.box("deck_plate", (ln, Wd, 0.08), (0, 0, H / 2), (0, -ang, 0), bevel=0.008)
    x = -L / 2 + 0.2
    while x < L / 2:
        k.beam("steel", (x, -Wd / 2 + 0.05, Z(x) + 0.05), (x, Wd / 2 - 0.05, Z(x) + 0.05), 0.03)
        x += 0.3
    x = -L / 2 + 0.1
    while x <= L / 2:
        k.tube("steel", (x, Wd / 2 - 0.05, Z(x)), (x, Wd / 2 - 0.05, Z(x) + 1.0), 0.028, 10)
        x += 1.45
    k.tube("steel", (-L / 2 + 0.1, Wd / 2 - 0.05, Z(-L / 2 + 0.1) + 1.0), (L / 2, Wd / 2 - 0.05, H + 1.0), 0.026, 10)
    k.span("steel_dark", L / 2 - 0.1, L / 2, -Wd / 2, Wd / 2, 0.0, H)
    for s in (-1, 1):
        k.beam("deck_paint", (-L / 2, s * (Wd / 2 - 0.01), 0.05), (L / 2, s * (Wd / 2 - 0.01), H + 0.05), 0.02, 0.06)
    return a


def stairs():
    a = new("city_stairs", "Structure")
    k = CK(a.p("Iron"))
    n, rise, run_, hw = 12, 0.225, 0.3, 0.5
    x0 = -n * run_ / 2
    for i in range(n):
        x, z = x0 + i * run_, (i + 1) * rise
        k.span("deck_plate", x, x + run_ + 0.02, -hw, hw, z - 0.05, z, bevel=0.006)
        k.span("deck_paint", x + run_ - 0.04, x + run_ + 0.02, -hw, hw, z - 0.002, z + 0.004, bevel=0.002)   # nosing
        for s in (-1, 1):
            k.span("steel_dark", x + 0.02, x + run_, s * hw - 0.01, s * hw + 0.01, z - 0.12, z - 0.05, bevel=0.0)   # tread brackets
    for s in (-1, 1):
        k.beam("steel", (x0, s * (hw + 0.04), 0.02), (-x0, s * (hw + 0.04), n * rise), 0.06, 0.24)   # stringers
        for i in range(0, n + 1, 4):
            x, z = x0 + i * run_ + 0.15, i * rise
            k.tube("steel", (x, s * (hw + 0.06), z), (x, s * (hw + 0.06), z + 1.0), 0.025, 8)
        k.tube("steel", (x0 + 0.15, s * (hw + 0.06), 1.0), (-x0 + 0.1, s * (hw + 0.06), n * rise + 1.0), 0.025, 8)
        k.tube("steel", (x0 + 0.15, s * (hw + 0.06), 0.5), (-x0 + 0.1, s * (hw + 0.06), n * rise + 0.5), 0.016, 6)
        k.span("steel_dark", x0 - 0.08, x0 + 0.12, s * (hw + 0.04) - 0.06, s * (hw + 0.04) + 0.06, 0.0, 0.012, bevel=0.002)
    return a


def railing(gid, half):
    a = new(gid, "Structure")
    k = CK(a.p("Iron"))
    k.railing([(0.0, -half), (0.0, half)], 0.0)
    return a


def corrugated_wall():
    a = new("city_wall", "Structure")
    k = CK(a.p("Scrap"))
    hy, H = 1.0, 2.7
    # one corrugated sheet in the frame (game x across, y up): build it in game space directly (ribs vertical)
    sheet = a.p("Scrap")
    K.corrugated(sheet, "tin_rust_v", -hy, hy, 0.05, H - 0.05, 0.03, 0.16, 0.004,
                 Matrix_from_game(rot_x=-90), samples_per_period=6)
    for z in (0.0, H - 0.08, H * 0.5):                                           # rails
        k.span("steel_dark", -0.07, 0.07, -hy - 0.06, hy + 0.06, z, z + 0.08)
    for s in (-1, 1):                                                            # posts
        k.span("steel_dark", -0.07, 0.07, s * hy - 0.06, s * hy + 0.06, 0.0, H)
    y = -hy + 0.2
    while y < hy:                                                                # bolts
        for z in (0.04, H * 0.5 + 0.04, H - 0.04):
            k.tube("chrome", (0.07, y, z), (0.095, y, z), 0.014, 6)
        y += 0.4
    k.span("rust", -0.072, 0.072, -hy + 0.3, -hy + 0.9, 0.9, 1.4, bevel=0.004)   # a patched plate
    k.rivets("chrome", [(0.075, -hy + 0.32 + i * 0.18, zz) for i in range(4) for zz in (0.92, 1.38)], (1, 0, 0), 0.01, 0.006)
    return a


def Matrix_from_game(rot_x=0.0):
    """Corrugated sheet frame: kit.corrugated builds (u across ribs, h normal, v along ribs); rot_x turns it upright."""
    from mathutils import Matrix
    return Matrix.Rotation(math.radians(rot_x), 4, "X")


def deck_ladder():
    a = new("city_ladder", "Structure")
    k = CK(a.p("Iron"))
    H = 3.2
    for s in (-0.25, 0.25):
        k.beam("steel", (0, s, 0), (0, s, H), 0.06, bevel=0.006)
        for z in (0.3, H - 0.3):
            k.beam("steel_dark", (0, s, z), (-0.2, s, z), 0.04)
            k.span("steel_dark", -0.22, -0.2, s - 0.05, s + 0.05, z - 0.05, z + 0.05, bevel=0.002)   # wall plates
    z = 0.3
    while z < H:
        k.tube("steel", (0, -0.25, z), (0, 0.25, z), 0.018, 8)
        k.span("deck_paint", -0.02, 0.02, -0.12, 0.12, z + 0.014, z + 0.02, bevel=0.0)                # grip tape
        z += 0.3
    k.tube("steel", (0, -0.25, H), (0.18, -0.25, H + 0.15), 0.025, 8)                                 # grab hoops
    k.tube("steel", (0, 0.25, H), (0.18, 0.25, H + 0.15), 0.025, 8)
    return a


def gangway():
    a = new("city_gangway", "Structure")
    k = CK(a.p("Iron"))
    hx, hy = 3.0, 0.7
    k.span("deck_plate", -hx, hx, -hy, hy, 0.02, 0.14, bevel=0.01)
    x = -hx + 0.2
    while x < hx:
        k.beam("steel", (x, -hy + 0.05, 0.15), (x, hy - 0.05, 0.15), 0.025)
        x += 0.35
    for s in (-1, 1):
        k.span("steel_dark", -hx, hx, s * hy - 0.03, s * hy + 0.03, -0.04, 0.14, bevel=0.003)          # side channels
        k.railing([(-hx + 0.2, s * hy), (hx - 0.2, s * hy)], 0.14, h=1.0, spacing=1.5, toe=False)
    k.tube("steel_dark", (-hx, -hy - 0.05, 0.08), (-hx, hy + 0.05, 0.08), 0.1, 16)                   # hinge drum
    k.tube("chrome", (-hx, -hy - 0.1, 0.08), (-hx, hy + 0.1, 0.08), 0.03, 8)
    k.box("deck_paint", (0.3, 2 * hy, 0.03), (hx + 0.12, 0, 0.05), (0, 0.25, 0))                    # landing lip
    return a


# ------------------------------------------------------------------------------------------ cabin, pod, greenhouse
def shanty_cabin():
    a = new("city_cabin", "Structure", kind="building")
    wall, det, gl = a.p("Scrap"), a.p("Wood"), a.p("Glass")
    k = CK(wall)
    w, d, h = 3.2, 2.8, 2.6
    face = 1
    fy, by = face * (d / 2 + 0.03), -face * d / 2
    wm = "tin_rust_v"
    k.span(wm, -w / 2, w / 2, by - 0.1, by, 0, h)                               # back
    k.span(wm, -w / 2, -w / 2 + 0.1, -d / 2, d / 2, 0, h)                       # far end
    k.span(wm, w / 2 - 0.1, w / 2, -d / 2, -d / 2 + 0.6, 0, h)                  # door end (door opening between)
    k.span(wm, w / 2 - 0.1, w / 2, d / 2 - 0.6, d / 2, 0, h)
    k.span(wm, w / 2 - 0.1, w / 2, -d / 2, d / 2, 2.25, h)
    k.span(wm, -w / 2, w / 2, fy - 0.1, fy, 0, 1.0)                             # front with the window
    k.span(wm, -w / 2, w / 2, fy - 0.1, fy, 2.0, h)
    k.span(wm, -w / 2, -1.1, fy - 0.1, fy, 1.0, 2.0)
    k.span(wm, 0.1, w / 2, fy - 0.1, fy, 1.0, 2.0)
    for (x0, x1) in ((-w / 2, -w / 2 + 0.05), (w / 2 - 0.05, w / 2)):            # corner angles
        for y in (-d / 2 - 0.08, d / 2 + 0.04):
            k.span("steel_dark", x0 - 0.02, x1 + 0.02, y, y + 0.06, 0, h)
    k.use(gl).box("glass_clear", (1.2, 0.04, 0.9), (-0.5, fy - 0.05, 1.5))
    k.use(det)
    for z in (1.03, 1.97):
        k.box(K.wood("dark", "x"), (1.36, 0.06, 0.08), (-0.5, fy, z))
    for x in (-1.12, 0.12):
        k.box(K.wood("dark", "x"), (0.08, 0.06, 1.0), (x, fy, 1.5))
    k.box(K.wood("", "x"), (0.06, 0.04, 0.9), (-0.5, fy - 0.03, 1.5))           # mullion
    k.use(wall)
    k.box("awning", (w + 0.4, 1.0, 0.05), (0, fy + 0.45, h + 0.1), (0.3, 0, 0))
    for s in (-1, 1):                                                           # awning struts
        k.beam("steel_dark", (s * (w / 2 + 0.1), fy, h - 0.3), (s * (w / 2 + 0.1), fy + 0.85, h - 0.05), 0.03)
    k.box("white", (0.75, 0.3, 0.5), (0.9, fy + 0.18, 1.2), bevel=0.02)          # dish / AC box
    k.tube("steel_dark", (-w / 2 + 0.4, 0, h), (-w / 2 + 0.4, 0, h + 1.2), 0.1, 10)   # stovepipe
    k.tube("rust", (-w / 2 + 0.4, 0, h + 1.2), (-w / 2 + 0.4, 0, h + 1.28), 0.16, 10)
    K.corrugated(wall, "tin_rust", -(w / 2 + 0.15), w / 2 + 0.15, -(d / 2 + 0.15), d / 2 + 0.15, 0.012, 0.076, 0.004,
                 _corr_roof(h + 0.04), samples_per_period=4)
    k.use(det)
    k.span(K.wood("", "z"), -1.2, 0.0, by + 0.1, by + 0.7, 0.0, 0.42, bevel=0.01)     # cot
    k.span("blanket", -1.18, -0.02, by + 0.12, by + 0.68, 0.42, 0.5, bevel=0.02)
    k.box("cloth_cream", (0.3, 0.45, 0.1), (-1.0, by + 0.4, 0.55), bevel=0.03)
    for i, (x, y) in enumerate(((1.1, by + 0.3), (1.1, by + 0.65))):              # crates inside
        k.span(K.wood("light", "x"), x - 0.22, x + 0.22, y - 0.16, y + 0.16, 0.0, 0.34 + 0.06 * i, bevel=0.012)
    # door hanging open on the +x end
    k.use(wall).span("rust", w / 2 + 0.05, w / 2 + 0.1, -d / 2 + 0.6, -d / 2 + 0.66, 0.02, 2.2)
    k.box("rust", (0.05, 0.85, 2.18), (w / 2 + 0.36, -d / 2 + 0.95, 1.11), (0, 0, 0.9))
    return a


def _corr_roof(y):
    """Corrugated roof sheet frame in game space: u along game x, ribs along game z at height y."""
    from mathutils import Matrix
    return Matrix.Translation((0, y, 0)) @ Matrix(((0, 0, 1, 0), (0, 1, 0, 0), (-1, 0, 0, 0), (0, 0, 0, 1)))


def living_pod():
    a = new("city_pod", "Structure", kind="building")
    sh, fe, gl = a.p("Scrap"), a.p("Iron"), a.p("Glass")
    k = CK(fe)
    for x in (-1.5, 1.5):
        for y in (-1.0, 1.0):
            k.span("steel_dark", x - 0.08, x + 0.08, y - 0.08, y + 0.08, 0.0, 1.25)
            k.span("steel_dark", x - 0.16, x + 0.16, y - 0.16, y + 0.16, 0.0, 0.015, bevel=0.002)
        k.beam("rust", (x, -1.0, 0.2), (x, 1.0, 1.0), 0.05)                    # leg bracing
    k.span("rust_dark", -1.8, 1.8, -1.2, 1.2, 1.1, 1.25)
    # the pod: a rounded box (corner tubes) w 4 (x), d 3 (y), h 3, porthole + window on +y
    k.use(sh)
    x, y, z, w, h, d, r = 0.0, 0.0, 1.25, 4.0, 3.0, 3.0, 0.5
    k.span("rust", x - w / 2 + r, x + w / 2 - r, y - d / 2, y + d / 2, z, z + h, bevel=0.01)
    k.span("rust", x - w / 2, x + w / 2, y - d / 2, y + d / 2, z + r, z + h - r, bevel=0.01)
    for sx in (-1, 1):
        for sz in (0, 1):
            zz = z + r + sz * (h - 2 * r)
            k.tube("rust", (x + sx * (w / 2 - r), y - d / 2, zz), (x + sx * (w / 2 - r), y + d / 2, zz), r, 20)
    for xx in (-1.0, 0.0, 1.0):                                                 # weld seams / ribs round the hull
        k.span("rust_dark", xx - 0.03, xx + 0.03, y - d / 2 - 0.02, y + d / 2 + 0.02, z - 0.02, z + h + 0.02, bevel=0.004)
    fy = y + d / 2 + 0.02
    k.use(gl).box("glass_clear", (2.2, 0.04, 1.5), (x - 0.4, fy, z + 1.5))
    k.use(sh)
    for zz in (z + 0.72, z + 2.28):
        k.box("steel_dark", (2.4, 0.08, 0.12), (x - 0.4, fy, zz))
    for xx in (x - 1.6, x + 0.8):
        k.box("steel_dark", (0.1, 0.08, 1.68), (xx, fy, z + 1.5))
    k.tube("steel_dark", (x + 1.4, fy - 0.05, z + 1.6), (x + 1.4, fy + 0.08, z + 1.6), 0.42, 24)    # porthole
    k.use(gl).tube("glass_clear", (x + 1.4, fy, z + 1.6), (x + 1.4, fy + 0.1, z + 1.6), 0.32, 24)
    k.use(sh)
    for i in range(10):
        an = i / 10 * math.tau
        k.tube("chrome", (x + 1.4 + math.cos(an) * 0.37, fy + 0.08, z + 1.6 + math.sin(an) * 0.37),
               (x + 1.4 + math.cos(an) * 0.37, fy + 0.1, z + 1.6 + math.sin(an) * 0.37), 0.015, 6)
    for kk in range(6):
        k.box("steel", (0.08, 0.06, 0.08), (x - w / 2 + 0.4 + kk * 0.65, fy, z + h - 0.25))
    for sx in (-1, 1):                                                          # brackets to the wall behind (-y)
        k.beam("rust_dark", (x + sx * 1.2, y - d / 2, z), (x + sx * 1.2, y - (d / 2 + 2.0), z + 2.2), 0.3)
        k.span("steel_dark", x + sx * 1.2 - 0.25, x + sx * 1.2 + 0.25, y - (d / 2 + 2.05), y - (d / 2 + 1.95), z + 1.95, z + 2.45)
    for kk in range(5):
        k.cable((x - 1.5 + kk * 0.6, y - d / 2, z + 0.05), (x - 1.2 + kk * 0.5, y - (d / 2 + 1.8), z + 0.3), 0.8 + 0.25 * kk, 8, 0.05, "black")
    k.span("rust_dark", x - 0.9, x + 0.9, y + d / 2 - 0.4, y + d / 2 + 0.02, z + h - 0.02, z + h + 0.25, bevel=0.01)   # roof hatch
    k.tube("steel", (x - 0.3, y, z + h), (x - 0.3, y, z + h + 0.9), 0.03, 6)    # aerial
    return a


def greenhouse():
    a = new("city_greenhouse", "Garden", kind="building")
    cc, w_, gl, pl = a.p("Concrete"), a.p("Wood"), a.p("Glass"), a.p("Plants", "Wood")
    k = CK(cc)
    w, d, h = 5.0, 3.6, 2.2
    k.span("conc_dark", -w / 2, w / 2, -d / 2, d / 2, 0.0, 0.5, bevel=0.02)
    k.span("soil", -w / 2 + 0.1, w / 2 - 0.1, -d / 2 + 0.1, d / 2 - 0.1, 0.5, 0.56, bevel=0.0)
    k.use(gl)
    for s in (-1, 1):
        k.span("glass_clear", -w / 2 + 0.05, w / 2 - 0.05, s * d / 2 - 0.02, s * d / 2 + 0.02, 0.5, h, bevel=0.0)
        k.span("glass_clear", s * w / 2 - 0.02, s * w / 2 + 0.02, -d / 2 + 0.05, d / 2 - 0.05, 0.5, h, bevel=0.0)
        k.box("glass_clear", (w, d / 2 * 1.08, 0.02), (0, s * d / 4, h + 0.5), (-s * math.atan2(1.0, d / 2), 0, 0), bevel=0.0)
    # gable ends (triangles)
    for s in (-1, 1):
        gl.tri_plate("glass_clear", CK_.g2g((s * w / 2, -d / 2, h)), CK_.g2g((s * w / 2, d / 2, h)), CK_.g2g((s * w / 2, 0, h + 1.0)), 0.02)
    k.use(w_)
    n = int(w / 0.6)
    for i in range(n + 1):
        xx = -w / 2 + i * w / n
        for s in (-1, 1):
            k.beam(K.wood("", "y"), (xx, s * d / 2, 0.5), (xx, s * d / 2, h), 0.06)
            k.beam(K.wood("", "y"), (xx, s * d / 2, h), (xx, 0, h + 1.0), 0.06)
    for s in (-1, 1):
        k.beam(K.wood("", "x"), (-w / 2, s * d / 2, h), (w / 2, s * d / 2, h), 0.07)
        k.beam(K.wood("", "x"), (-w / 2, s * d / 2, 0.52), (w / 2, s * d / 2, 0.52), 0.06)
        k.beam(K.wood("", "y"), (s * w / 2, -d / 2, 1.35), (s * w / 2, d / 2, 1.35), 0.05)
    k.beam(K.wood("", "x"), (-w / 2, 0, h + 1.0), (w / 2, 0, h + 1.0), 0.08)
    k.use(pl)
    for i in range(4):                                                         # plants in rows
        cx = -w / 2 + 0.8 + i * 1.1
        pl.rock("leaf", 0.3, CK_.g2g((cx, -d / 4, 0.85)), seed=i * 7 + 1, squash=0.8, detail=2, scale=(1.3, 1.0, 1.0))
        pl.rock("leaf", 0.22, CK_.g2g((cx + 0.3, d / 4, 0.75)), seed=i * 7 + 3, squash=0.9, detail=2)
        for t in range(3):
            k.tube("wood_light_y", (cx - 0.2 + t * 0.2, -d / 4 + 0.25, 0.56), (cx - 0.2 + t * 0.2, -d / 4 + 0.25, 1.3), 0.008, 4)   # canes
    for i in range(3):
        k.ball("flower_red" if i % 2 else "flower_yellow", (-1.2 + i * 1.2, -d / 4 - 0.1, 1.12), 0.05)
    k.use(w_).span("steel_dark", -w / 2 - 0.02, -w / 2 + 0.02, -0.45, 0.45, 0.5, 2.1, bevel=0.002)   # door frame on the end
    return a


def flower_box():
    a = new("city_flower_box", "Garden")
    cl, pl = a.p("Clay"), a.p("Plants", "Wood")
    k = CK(cl)
    k.span("terracotta", -0.22, 0.22, -0.6, 0.6, 0.0, 0.32, bevel=0.025)
    k.span("terracotta", -0.24, 0.24, -0.62, 0.62, 0.28, 0.33, bevel=0.012)    # lip
    k.span("soil", -0.18, 0.18, -0.56, 0.56, 0.32, 0.345, bevel=0.0)
    r = K.rng(31)
    for i in range(7):
        y = -0.48 + i * 0.16
        hgt = r.uniform(0.15, 0.35)
        for t in range(5):                                                      # leafy tufts: blades
            an = r.uniform(0, math.tau)
            k.box("leaf", (0.012, 0.05, hgt), (r.uniform(-0.08, 0.08) + math.cos(an) * 0.03, y + math.sin(an) * 0.03, 0.34 + hgt / 2),
                  (math.cos(an) * 0.25, math.sin(an) * 0.25, an), bevel=0.0)
        if r.random() < 0.7:
            k.ball(r.choice(["flower_red", "flower_yellow", "stack_white"]), (r.uniform(-0.08, 0.08), y, 0.36 + hgt), 0.035, (1, 1, 0.6))
    return a


# ------------------------------------------------------------------------------------------ lights
def lamp_post():
    a = new("city_lamp_post", "Furniture")
    fe, b = a.p("Iron"), a.p("Bulb", "Glass")
    k = CK(fe)
    k.tube("steel_dark", (0, 0, 0), (0, 0, 0.6), 0.2, 16, bevel=0.01)          # base sleeve
    k.lathe("steel_dark", (0, 0, 0), (0, 0, 1), [(0.0, 0.0), (0.3, 0.0), (0.3, 0.03), (0.22, 0.06), (0.0, 0.06)], 16)
    for i in range(4):
        an = i / 4 * math.tau + 0.4
        k.tube("chrome", (math.cos(an) * 0.26, math.sin(an) * 0.26, 0.06), (math.cos(an) * 0.26, math.sin(an) * 0.26, 0.09), 0.02, 6)
    k.tube("steel", (0, 0, 0.6), (0, 0, 6.2), 0.08, 14, r2=0.06)
    top = Vector((0, 0, 6.2))
    tip = top + Vector((1.6, 0, 0.25))
    k.tube("steel", top - Vector((0, 0, 0.05)), tip, 0.045, 10)
    k.tube("steel", top - Vector((0, 0, 0.6)), top + Vector((0.6, 0, 0.09)), 0.02, 6)            # brace
    k.lathe("steel_dark", tip + Vector((0.3, 0, -0.02)), (0, 0, 1), [(0.0, 0.0), (0.3, 0.0), (0.38, 0.08), (0.3, 0.16), (0.0, 0.18)], 16)   # head
    k.box("steel_dark", (0.75, 0.32, 0.12), tip + Vector((0.3, 0, 0.06)), bevel=0.04)
    k.use(b).box("glow_sodium", (0.55, 0.24, 0.03), tip + Vector((0.3, 0, -0.04)), bevel=0.01)
    k.use(fe).box("deck_paint", (0.04, 0.17, 0.6), (0.09, 0, 2.2), bevel=0.0)  # a hazard band on the pole
    return a


def searchlight():
    a = new("city_searchlight", "Furniture")
    fe, b = a.p("Iron"), a.p("Bulb", "Glass")
    k = CK(fe)
    p = Vector((0, 0, 1.6))
    d = (Vector((10, 0, 5)) - p).normalized()
    r = 0.9
    k.tube("steel_dark", p - Vector((0, 0, 1.6)), p - Vector((0, 0, 0.6)), 0.3, 16, bevel=0.01)       # pedestal
    k.lathe("steel_dark", (0, 0, 0), (0, 0, 1), [(0.0, 0.0), (0.42, 0.0), (0.42, 0.05), (0.3, 0.1), (0.0, 0.1)], 16)
    k.box("steel_dark", (0.3, 2 * r + 0.5, 0.3), p - Vector((0, 0, 0.6)), bevel=0.02)                 # yoke
    for s in (-1, 1):
        k.box("steel", (0.15, 0.15, 0.6), p + Vector((0, s * (r + 0.15), -0.3)), bevel=0.01)
        k.tube("chrome", p + Vector((0, s * (r + 0.05), 0)), p + Vector((0, s * (r + 0.25), 0)), 0.09, 12)   # trunnion
    k.tube("steel", p - d * 0.9, p + d * 0.6, r, 28, bevel=0.02)                                     # drum
    k.tube("steel_dark", p - d * 0.95, p - d * 0.85, r * 0.6, 20)                                    # back cap
    for t in (-0.6, -0.2, 0.2):                                                                       # cooling ribs
        k.ring("steel_dark", p + d * t, d, r + 0.01, 0.02, 28, 4)
    k.tube("steel_dark", p + d * 0.6, p + d * 0.75, r + 0.1, 28, bevel=0.01)                         # bezel
    k.use(b).tube("glow_cyan", p + d * 0.75, p + d * 0.8, r - 0.075, 28)
    k.use(fe)
    for i in range(5):                                                                                 # lens grille bars
        yy = -r + 0.3 + i * (2 * r - 0.6) / 4
        k.box("steel_dark", (0.02, 0.02, 2 * r - 0.3), p + d * 0.82 + Vector((0, yy, 0)), (0, -math.atan2(d.z, d.x), 0), bevel=0.0)
    k.tube("steel", p + d * 0.2 + Vector((0, 0, r)), p + d * 0.2 + Vector((0, 0, r + 0.12)), 0.03, 6)
    k.tube("black", p + d * 0.2 + Vector((0, 0, r + 0.12)), p - d * 0.4 + Vector((0, 0, r + 0.14)), 0.03, 6)   # handle
    return a


def floodlight():
    a = new("city_floodlight", "Furniture")
    fe, b = a.p("Iron"), a.p("Bulb", "Glass")
    k = CK(fe)
    p = Vector((0, 0, 0.6))
    d = (Vector((4, 0, 0.2)) - p).normalized()
    k.tube("steel", p - Vector((0, 0, 0.6)), p - Vector((0, 0, 0.2)), 0.06, 10)
    k.box("steel_dark", (0.3, 0.3, 0.02), (0, 0, 0.01), bevel=0.004)
    k.box("steel", (0.08, 0.62, 0.06), p - Vector((0, 0, 0.2)), bevel=0.01)
    for s in (-1, 1):
        k.box("steel", (0.06, 0.04, 0.3), p + Vector((0, s * 0.31, -0.08)), bevel=0.006)
    k.beam("steel_dark", p - d * 0.25, p + d * 0.15, 0.7, 0.55, bevel=0.03)
    for i in range(5):                                                                                # fins on the back
        k.beam("steel_dark", p - d * 0.25 + Vector((0, -0.25 + i * 0.125, 0)), p - d * 0.33 + Vector((0, -0.25 + i * 0.125, 0)), 0.5, 0.02, 0.0)
    k.use(b).beam("lamp", p + d * 0.15, p + d * 0.19, 0.6, 0.45, 0.01)
    k.use(fe).beam("steel_dark", p + d * 0.17, p + d * 0.21, 0.72, 0.04, 0.0)                          # bezel bars
    k.beam("steel_dark", p + d * 0.17 + Vector((0, 0, 0)), p + d * 0.21, 0.04, 0.57, 0.0)
    return a


def bulkhead():
    a = new("city_bulkhead", "Furniture")
    fe, b = a.p("Scrap"), a.p("Bulb", "Glass")
    k = CK(fe)
    k.box("steel_dark", (0.3, 0.48, 0.05), (0, 0, 0.025), bevel=0.01)
    k.box("steel_dark", (0.24, 0.4, 0.16), (0, 0, 0.13), bevel=0.03)
    k.use(b).box("lamp", (0.18, 0.32, 0.03), (0, 0, 0.215), bevel=0.012)
    k.use(fe)
    for x in (-0.07, 0.0, 0.07):
        k.box("steel", (0.022, 0.42, 0.022), (x, 0, 0.25), bevel=0.004)
    for y in (-0.18, 0.18):
        k.box("steel", (0.2, 0.022, 0.06), (0, y, 0.235), bevel=0.004)
    for (x, y) in ((-0.12, -0.21), (0.12, -0.21), (-0.12, 0.21), (0.12, 0.21)):
        k.tube("chrome", (x, y, 0.05), (x, y, 0.06), 0.012, 6)
    k.tube("black", (0, -0.24, 0.025), (0, -0.34, 0.025), 0.018, 8)          # cable gland
    return a


def string_lights():
    a = new("city_string_lights", "Furniture")
    fe, b, cc = a.p("Iron"), a.p("Bulb", "Glass"), a.p("Concrete")
    k = CK(fe)
    hy, H = 2.0, 2.6
    for s in (-1, 1):
        k.beam("steel_dark", (0, s * hy, 0), (0, s * hy, H), 0.08, bevel=0.008)
        k.use(cc).span("conc_dark", -0.15, 0.15, s * hy - 0.15, s * hy + 0.15, 0.0, 0.12, bevel=0.015)
        k.use(fe).tube("steel", (0, s * hy, H - 0.1), (0, s * (hy - 0.08), H - 0.1), 0.02, 6)
        k.ring("steel", (0, s * (hy - 0.08), H - 0.1), (0, 1, 0), 0.025, 0.006, 8, 4)
    p, q = Vector((0, -hy + 0.08, H - 0.1)), Vector((0, hy - 0.08, H - 0.1))
    sag = 0.45
    k.cable(p, q, sag, 16, 0.02, "black")
    cols = ["lamp", "lamp_amber", "lamp_red", "glow_cyan", "lamp"]
    for i in range(1, 12):
        f = i / 12
        c = p.lerp(q, f) - Vector((0, 0, sag * 4 * f * (1 - f)))
        k.use(fe).tube("black", c, c - Vector((0, 0, 0.04)), 0.016, 6)
        m = cols[i % len(cols)]
        k.use(b if m == "lamp" else fe).ball(m, c - Vector((0, 0, 0.09)), 0.035, (1, 1, 1.35), 10, 6)
    return a


def lantern():
    a = new("city_lantern", "Furniture")
    fe, b, gl = a.p("Scrap"), a.p("Bulb", "Glass"), a.p("Glass")
    k = CK(fe)
    k.box("steel_dark", (0.22, 0.22, 0.03), (0, 0, 0.015), bevel=0.008)
    for x in (-0.09, 0.09):
        for y in (-0.09, 0.09):
            k.tube("steel", (x, y, 0.03), (x, y, 0.3), 0.01, 6)
    k.use(b).ball("lamp_amber", (0, 0, 0.13), 0.035, (1, 1, 1.5), 10, 8)
    k.use(fe).tube("brass", (0, 0, 0.03), (0, 0, 0.08), 0.05, 12)                # fuel well
    k.use(gl).tube("glass_clear", (0, 0, 0.08), (0, 0, 0.28), 0.075, 16, caps=False)
    k.use(fe)
    k.box("steel_dark", (0.24, 0.24, 0.03), (0, 0, 0.315), bevel=0.008)
    k.lathe("steel_dark", (0, 0, 0.33), (0, 0, 1), [(0.0, 0.0), (0.09, 0.0), (0.05, 0.05), (0.0, 0.06)], 12)
    k.poly("steel", [(0, -0.07, 0.37), (0, -0.05, 0.43), (0, 0.0, 0.46), (0, 0.05, 0.43), (0, 0.07, 0.37)], 0.007, 6)
    return a


def barrel_fire():
    a = new("city_barrel_fire", "Furniture")
    fe, b, w_ = a.p("Scrap"), a.p("Bulb", "Glass"), a.p("Wood")
    k = CK(fe)
    k.lathe("rust", (0, 0, 0), (0, 0, 1), [(0.0, 0.01), (0.29, 0.0), (0.3, 0.02), (0.3, 0.84), (0.32, 0.86), (0.32, 0.9), (0.29, 0.9), (0.29, 0.1), (0.0, 0.1)], 24)
    for z in (0.28, 0.58):
        k.ring("rust_dark", (0, 0, z), (0, 0, 1), 0.305, 0.018, 24, 4)
    for i in range(10):                                                          # punched holes, glowing
        an = i * math.pi * 0.2 + (i & 1) * 0.3
        z = 0.18 + (i % 3) * 0.17
        k.use(fe).box("ember", (0.06, 0.06, 0.05), (math.cos(an) * 0.296, math.sin(an) * 0.296, z), (0, 0, an), bevel=0.01)
    k.use(fe).lathe("ash", (0, 0, 0.78), (0, 0, 1), [(0.0, 0.0), (0.28, 0.0), (0.24, 0.05), (0.0, 0.07)], 20)
    r = K.rng(5)
    k.use(b)
    for i in range(6):
        k.box("flame", (0.05, 0.05, r.uniform(0.12, 0.3)), (r.uniform(-0.15, 0.15), r.uniform(-0.15, 0.15), 0.95), (r.uniform(-0.3, 0.3), r.uniform(-0.3, 0.3), 0), bevel=0.015)
    k.use(w_)
    for i in range(3):
        k.beam(K.wood("dark", "x"), (r.uniform(-0.2, 0.2), -0.25, 0.88), (r.uniform(-0.2, 0.2), 0.25, 0.9), 0.05, bevel=0.01)
    k.use(fe)
    for i in range(5):
        an = r.uniform(0, math.tau)
        k.ball("ember", (math.cos(an) * 0.18, math.sin(an) * 0.18, 0.84), 0.03, (1, 1, 0.6), 6, 4)
    return a


# ------------------------------------------------------------------------------------------ utility, decor
def water_tank_legs():
    a = new("city_water_tank", "Utility", kind="building")
    fe, ts = a.p("Iron"), a.p("Scrap")
    k = CK(fe)
    for i in range(4):
        x, y = -1 + 2 * (i % 2), -1 + 2 * (i // 2)
        k.beam("steel_dark", (x, y, 0), (x, y, 2.2), 0.12, bevel=0.01)
        k.span("steel_dark", x - 0.15, x + 0.15, y - 0.15, y + 0.15, 0.0, 0.015, bevel=0.003)
    corners = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    for z in (0.8, 1.6):
        for i in range(4):
            a_, b_ = corners[i], corners[(i + 1) % 4]
            k.beam("steel", (a_[0], a_[1], z), (b_[0], b_[1], z), 0.05)
    for i in range(4):                                                           # X bracing in the lower bay
        a_, b_ = corners[i], corners[(i + 1) % 4]
        k.tube("rust", (a_[0], a_[1], 0.1), (b_[0], b_[1], 0.8), 0.015, 6)
        k.tube("rust", (b_[0], b_[1], 0.1), (a_[0], a_[1], 0.8), 0.015, 6)
    k.use(ts)
    k.tube("rust", (0, 0, 2.2), (0, 0, 4.4), 1.3, 32, bevel=0.02)
    k.lathe("rust_dark", (0, 0, 4.4), (0, 0, 1), [(0.0, 0.0), (1.35, 0.0), (1.35, 0.15), (0.3, 0.45), (0.0, 0.47)], 32)   # conical lid
    k.tube("steel_dark", (0, 0, 4.86), (0, 0, 4.95), 0.18, 12)
    for z in (2.9, 3.7):
        k.ring("steel_dark", (0, 0, z), (0, 0, 1), 1.31, 0.03, 32, 4)
    for i in range(16):                                                          # vertical seam rivets
        an = i / 16 * math.tau
        k.tube("rust_dark", (math.cos(an) * 1.29, math.sin(an) * 1.29, 2.25), (math.cos(an) * 1.31, math.sin(an) * 1.31, 4.38), 0.012, 4)
    k.use(fe).ladder((1.4, 0, 0), (1.4, 0, 4.5), 0.4)
    k.poly("steel", [(1.4, -0.2, 4.5), (1.25, -0.2, 4.7), (1.0, -0.2, 4.72)], 0.025)
    k.tube("pipe", (0, 0, 2.2), (0, 0, 0.4), 0.08, 10)
    k.tube("brass", (0, 0, 0.4), (0.25, 0, 0.4), 0.05, 8)                       # tap
    k.tube("pipe", (0.25, 0, 0.4), (0.25, 0, 0.3), 0.03, 6)
    k.tube("crimson", (0, 0, 0.6), (0, 0, 0.66), 0.09, 10)                      # valve wheel
    return a


def neon_sign():
    a = new("city_neon", "Decor")
    fe = a.p("Scrap")
    k = CK(fe)
    glyphs = ["111.111.111.101", "101.101.100.111", "101.111.110.111", "101.100.100.111", "111.100.111.101"]
    px = 0.06
    cols, rows = len(glyphs[0]), len(glyphs)
    hw, hh = cols * px / 2 + 0.12, rows * px / 2 + 0.12
    k.box("black", (2 * hh, 2 * hw, 0.06), (0, 0, 0.03), bevel=0.012)
    for s in (-1, 1):
        k.box("glow_cyan", (0.025, 2 * hw - 0.06, 0.025), (s * (hh - 0.04), 0, 0.075), bevel=0.008)
        k.box("glow_cyan", (2 * hh - 0.06, 0.025, 0.025), (0, s * (hw - 0.04), 0.075), bevel=0.008)
    for r_ in range(rows):
        for c in range(cols):
            if glyphs[r_][c] == "1":
                m = "lamp_amber" if c < 7 else "lamp_red"
                k.use(fe).box(m, (px * 0.62, px * 0.62, 0.02), ((rows / 2 - r_ - 0.5) * px, (cols / 2 - c - 0.5) * px, 0.075), bevel=0.004, segs=1)
    for i in range(2):
        k.use(fe).tube("chrome", ((-1 if i == 0 else 1) * (hh - 0.06), hw - 0.06, 0.06), ((-1 if i == 0 else 1) * (hh - 0.06), hw - 0.06, 0.1), 0.02, 6)
    k.box("steel_dark", (0.12, 0.06, 0.03), (-hh + 0.08, -hw + 0.1, 0.075), bevel=0.006)               # transformer
    k.poly("black", [(-hh + 0.02, -hw + 0.1, 0.06), (-hh - 0.06, -hw + 0.1, 0.03), (-hh - 0.16, -hw + 0.05, 0.02)], 0.008, 6)
    return a


def banner():
    a = new("city_banner", "Decor")
    fe, cl, cc = a.p("Iron"), a.p("Cloth"), a.p("Concrete")
    k = CK(fe)
    k.tube("steel_dark", (0, 0, 0), (0, 0, 3.3), 0.035, 10)
    k.tube("steel", (0, 0, 3.3), (0, 0, 3.4), 0.05, 10)
    k.ball("steel", (0, 0, 3.42), 0.05)
    k.use(cc).span("conc_dark", -0.18, 0.18, -0.18, 0.18, 0.0, 0.14, bevel=0.02)
    k.use(fe).tube("steel", (0, 0, 3.1), (0, -1.0, 3.1), 0.02, 8)
    for y in (-0.15, -0.55, -0.92):
        k.ring("steel", (0, y, 3.06), (0, 1, 0), 0.03, 0.006, 8, 4)
    # the cloth: a slightly waving sheet hanging from the bar, tattered bottom
    r = K.rng(3)
    secs = []
    nz = 10
    for zi in range(nz + 1):
        z = 3.06 - zi * 1.7 / nz
        row = []
        for yi in range(7):
            y = -0.07 - yi * 0.9 / 6
            row.append((0.03 * math.sin(yi * 1.3 + zi * 0.6), y, z))
        secs.append(row)
    for zi in range(nz):
        for yi in range(6):
            a0, b0, c0, d0 = secs[zi][yi], secs[zi][yi + 1], secs[zi + 1][yi + 1], secs[zi + 1][yi]
            if zi == nz - 1 and r.random() < 0.35:
                continue                                                         # torn out
            m = "stack_red" if (zi == 0 or zi == 6) else "canvas"
            if 2 <= zi <= 4 and 2 <= yi <= 3:
                m = "stack_white"                                                # the skull
            cl.quad(m, CK_.g2g(a0), CK_.g2g(b0), CK_.g2g(c0), CK_.g2g(d0), 0.012)
    for y in (-0.42, -0.62):
        k.use(cl).ball("black", (0.04, y, 2.65), 0.05, (0.4, 1, 1.2), 8, 6)      # eye holes
    for i in range(4):
        k.use(cl).box("black", (0.02, 0.02, 0.06), (0.045, -0.4 - i * 0.07, 2.45), bevel=0.0)   # teeth gaps
    return a


def chimney():
    a = new("city_chimney", "Decor", kind="building")
    fe, b = a.p("Iron"), a.p("Bulb", "Glass")
    k = CK(fe)
    k.lathe("rust_dark", (0, 0, 0), (0, 0, 1), [(0.0, 0.0), (0.95, 0.0), (0.95, 0.9), (0.7, 1.0), (0.0, 1.0)], 32)
    for i in range(8):                                                           # base bolts
        an = i / 8 * math.tau
        k.tube("chrome", (math.cos(an) * 0.85, math.sin(an) * 0.85, 0.9), (math.cos(an) * 0.8, math.sin(an) * 0.8, 0.96), 0.03, 6)
    for i in range(5):
        r = 0.62 - i * 0.02
        m = ("stack_white" if i % 2 == 1 else "stack_red") if i >= 3 else ("rust" if i % 2 == 1 else "pipe")
        k.tube(m, (0, 0, 1 + i * 1.1), (0, 0, 1 + (i + 1) * 1.1), r, 28, caps=False)
        k.ring("steel_dark", (0, 0, 1 + i * 1.1), (0, 0, 1), r + 0.02, 0.035, 28, 4)
    k.lathe("steel_dark", (0, 0, 6.45), (0, 0, 1), [(0.5, 0.0), (0.68, 0.0), (0.68, 0.15), (0.5, 0.15)], 28, close=False)
    k.lathe("soot", (0, 0, 6.3), (0, 0, 1), [(0.0, 0.0), (0.5, 0.0), (0.5, 0.25)], 24, close=False)
    k.ladder((0.68, 0, 1), (0.68, 0, 6.4), 0.4)
    for z in (2.5, 4.0, 5.5):                                                     # safety cage hoops
        k.ring("steel", (0.9, 0, z), (0, 0, 1), 0.3, 0.012, 16, 4)
    k.tube("steel", (0.45, 0, 6.6), (0.45, 0, 6.66), 0.06, 8)
    k.use(b).box("lamp_red", (0.18, 0.18, 0.18), (0.45, 0, 6.75), bevel=0.05)
    k.use(fe).ring("steel_dark", (0.45, 0, 6.75), (0, 0, 1), 0.1, 0.01, 10, 3)
    return a


def yard_crane():
    a = new("city_crane", "Decor", kind="building")
    fe, cc, gl, b = a.p("Iron"), a.p("Concrete"), a.p("Glass"), a.p("Bulb", "Glass")
    k = CK(cc)
    H = 6.0
    k.span("conc_dark", -0.9, 0.9, -0.9, 0.9, 0.0, 0.4, bevel=0.03)
    k.use(fe)
    for x in (-0.35, 0.35):
        for y in (-0.35, 0.35):
            k.beam("rust", (x, y, 0.4), (x, y, H), 0.08, bevel=0.006)
            k.span("steel_dark", x - 0.1, x + 0.1, y - 0.1, y + 0.1, 0.4, 0.43, bevel=0.003)
    z = 0.4
    while z < H - 0.5:                                                           # lattice lacing
        k.tube("rust_dark", (-0.35, -0.35, z), (0.35, -0.35, z + 0.8), 0.02, 6)
        k.tube("rust_dark", (0.35, 0.35, z), (-0.35, 0.35, z + 0.8), 0.02, 6)
        k.tube("rust_dark", (-0.35, 0.35, z), (-0.35, -0.35, z + 0.8), 0.02, 6)
        k.tube("rust_dark", (0.35, -0.35, z), (0.35, 0.35, z + 0.8), 0.02, 6)
        for (p, q) in (((-0.35, -0.35), (0.35, -0.35)), ((0.35, -0.35), (0.35, 0.35)), ((0.35, 0.35), (-0.35, 0.35)), ((-0.35, 0.35), (-0.35, -0.35))):
            k.tube("rust_dark", (p[0], p[1], z), (q[0], q[1], z), 0.018, 6)
        z += 0.8
    k.span("rust_dark", -0.6, 0.6, -0.6, 0.6, H, H + 0.3)
    k.tube("steel_dark", (0, 0, H + 0.3), (0, 0, H + 0.38), 0.45, 20)            # slew ring
    k.span("deck_paint", -0.2, 0.9, 0.45, 1.25, H - 1.0, H, bevel=0.02)          # cab
    k.span("steel_dark", -0.25, 0.95, 0.4, 1.3, H - 0.02, H + 0.05, bevel=0.01)
    k.use(gl).box("glass_clear", (0.04, 0.7, 0.5), (0.91, 0.85, H - 0.45))
    k.box("glass_clear", (1.0, 0.04, 0.45), (0.35, 1.26, H - 0.45))
    k.use(fe)
    root, tip = Vector((0, 0, H + 0.3)), Vector((7, 0, H + 0.6))
    for y in (-0.3, 0.3):
        k.beam("rust", root + Vector((0, y, 0)), tip + Vector((0, y, 0)), 0.08)
    k.beam("rust", root + Vector((0, 0, 0.7)), tip + Vector((0, 0, 0.2)), 0.08)
    for i in range(9):
        a_ = root.lerp(tip, i / 9)
        b_ = root.lerp(tip, (i + 1) / 9)
        top_b = b_ + Vector((0, 0, 0.7 - 0.5 * (i + 1) / 9))
        for y in (-0.3, 0.3):
            k.tube("rust", a_ + Vector((0, y, 0)), top_b, 0.018, 6)
        k.tube("rust", a_ + Vector((0, -0.3, 0)), a_ + Vector((0, 0.3, 0)), 0.018, 6)
    k.beam("rust", root, Vector((-2.4, 0, H + 0.35)), 0.12)                       # counter jib
    k.use(cc).span("conc_dark", -2.6, -1.8, -0.45, 0.45, H - 0.4, H + 0.3, bevel=0.03)
    k.use(fe)
    k.tube("steel", root + Vector((0, 0, 0.7)), root + Vector((0, 0, 1.6)), 0.05, 8)   # A-frame top + pendants
    k.tube("steel_dark", root + Vector((0, 0, 1.6)), tip + Vector((0, 0, 0.2)), 0.012, 4)
    k.tube("steel_dark", root + Vector((0, 0, 1.6)), Vector((-2.2, 0, H + 0.35)), 0.012, 4)
    hook = Vector((5.5, 0, H + 0.45))
    k.box("steel_dark", (0.4, 0.3, 0.2), hook + Vector((0, 0, 0.1)), bevel=0.02)  # trolley
    k.tube("steel_dark", hook, hook - Vector((0, 0, 4.0)), 0.012, 4)
    k.box("stack_red", (0.3, 0.3, 0.4), hook - Vector((0, 0, 4.2)), bevel=0.04)    # hook block
    k.poly("steel", [hook - Vector((0, 0, 4.4)), hook - Vector((0, 0, 4.6)), hook - Vector((-0.1, 0, 4.72)), hook - Vector((-0.16, 0, 4.6))], 0.02, 6)
    k.use(b).box("lamp_red", (0.14, 0.14, 0.14), tip + Vector((0, 0, 0.35)), bevel=0.04)
    return a


TILES = [("furn_city_decks", "City deck plates S / M / L, vehicle ramp, foot ramp",
          [lambda: [deck_plate("city_deck_s", 2.0, 2.0), deck_plate("city_deck_m", 4.0, 4.0), deck_plate("city_deck_l", 8.0, 4.0), vehicle_ramp(), foot_ramp()]], 3),
         ("furn_city_access", "City stairs, railings 1.5 / 3 m, corrugated wall, deck ladder, gangway",
          [lambda: [stairs(), railing("city_railing", 0.75), railing("city_railing_long", 1.5), corrugated_wall(), deck_ladder(), gangway()]], 3),
         ("furn_city_cabins", "Shanty cabin, living pod, roof greenhouse, flower box",
          [lambda: [shanty_cabin(), living_pod(), greenhouse(), flower_box()]], 2),
         ("furn_city_lights", "Deck lamp post, searchlight, hull flood, bulkhead, string lights, lantern, barrel fire",
          [lambda: [lamp_post(), searchlight(), floodlight(), bulkhead(), string_lights(), lantern(), barrel_fire()]], 4),
         ("furn_city_decor", "Tank on legs, neon sign, gang banner, chimney stack, yard crane",
          [lambda: [water_tank_legs(), neon_sign(), banner(), chimney(), yard_crane()]], 3)]

if __name__ == "__main__":
    run(TILES, "city_")
