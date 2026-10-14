"""Lived-in details for the HD world buildings / props (cozy pass): flower boxes, door mats, boots, coat hooks, laundry
lines, drainpipes, lanterns, string lights, soot rings, signs. Game space (metres, x right, y up, z front).

Animated / switched objects get their own Asset part (the game finds them by name):
Names follow Building/PieceMotion.Kind (a kind, or a kind + digit / '_'; pivot = hinge / base):
  Curtain_<n>  window curtains (pivot on the rod; warm emission lit through _LampOn)
  Lamp, Lamp2  lanterns, string lights (pivot at the hook; emissive)
  Laundry      clothes on the line (pivot on the line)
  Sign         signboards (pivot at the top edge)
  Chimney      chimney / stovepipe tops (pivot = where the smoke leaves)"""
import math

import hdkit as K
from hdkit import Matrix, Vector


def curtain_mat(kind="gingham"):
    """Curtain cloth that glows warm when its object is lit (lamp kind 'other')."""
    tex, tint = {"gingham": ("curtain", (1.0, 1.0, 1.0)), "rose": ("wallpaper_stripe", (1.05, 0.95, 0.95)),
                 "sack": ("burlap", (1.0, 0.95, 0.85)), "ticking": ("ticking", (1.0, 1.0, 1.0)),
                 "quilt": ("quilt", (1.0, 1.0, 1.0))}[kind]
    if not K.texmat.available(tex):
        return "cloth_cream"
    return K.texmat.image_material("CurtainLit_" + kind, tex, tint, 0.95, 0.45, True, "y", emit=((1.0, 0.62, 0.32), 1.6))


def flower_box(p, x0, x1, y, z, out=-1, seed=0, depth=0.18):
    """Window box under a sill on a wall facing `out` (-1 = -z, +1 = +z): planks, soil, flowers and trailing leaves."""
    r = K.rng(seed)
    za, zb = sorted((z, z + out * depth))
    p.box2(K.wood("", "x"), (x0, y - 0.16, za), (x1, y, zb), bevel=0.0)
    p.box2("soil", (x0 + 0.02, y - 0.03, za + 0.02), (x1 - 0.02, y - 0.01, zb - 0.02), bevel=0.0)
    n = max(3, int((x1 - x0) / 0.14))
    for i in range(n):
        x = x0 + 0.05 + (x1 - x0 - 0.1) * (i + r.uniform(-0.3, 0.3)) / max(1, n - 1)
        zc = (za + zb) / 2 + r.uniform(-0.04, 0.04)
        h = r.uniform(0.1, 0.22)
        p.rod("leaf", (x, y - 0.02, zc), (x + r.uniform(-0.03, 0.03), y + h, zc + r.uniform(-0.03, 0.03)), 0.005, 3, caps=False)
        p.sphere(r.choice(["flower_red", "flower_yellow", "flower_red", "cloth_cream"]), r.uniform(0.025, 0.04), (x, y + h, zc), (1, 0.7, 1), 6, 4)
        if i % 2 == 0:
            p.sphere("leaf", 0.045, (x + 0.03, y + 0.02, zc), (1.3, 0.6, 1), 6, 4)
    for i in range(3):                                   # trailing ivy over the front
        x = x0 + (x1 - x0) * (0.2 + 0.3 * i)
        zf = zb if out > 0 else za
        p.tube("leaf", [(x, y, zf), (x + 0.02, y - 0.12, zf + out * 0.015), (x - 0.01, y - 0.26, zf + out * 0.02)], 0.012, 3)


def doormat(p, cx, y, cz, w=0.7, d=0.42, yaw=0.0):
    m = "doormat" if "doormat" in K.M else "burlap"
    p.box(m, (w, 0.018, d), (cx, y + 0.009, cz), r=(0, yaw, 0), bevel=0.004)


def boots(p, x, y, z, yaw=0.0, m="leather"):
    """A pair of work boots standing side by side."""
    for k in (-1, 1):
        a = math.radians(yaw)
        ox, oz = math.cos(a) * 0.07 * k, -math.sin(a) * 0.07 * k
        p.box(m, (0.1, 0.06, 0.27), (x + ox, y + 0.03, z + oz), r=(0, yaw + 4 * k, 0), bevel=0.02, segs=1)
        p.add(K.kit.bm_cyl(0.045, 0.2, 8, 0.0), m, Matrix.Translation((x + ox, y + 0.15, z + oz - 0.06)) @ K._axis_mtx("y"))
        p.box("black", (0.11, 0.015, 0.28), (x + ox, y + 0.007, z + oz), r=(0, yaw + 4 * k, 0), bevel=0.0)


def coat_hooks(p, x0, x1, y, z, out=-1, coat="cloth_olive", hat=True):
    """A peg rail on a wall (facing `out` along z) with a coat and a hat hanging from it."""
    zf = z + out * 0.025
    p.box2(K.wood("dark", "x"), (x0, y - 0.05, min(z, zf)), (x1, y + 0.05, max(z, zf)), bevel=0.008)
    n = 3
    for i in range(n):
        x = x0 + (x1 - x0) * (i + 0.5) / n
        p.rod("brass", (x, y, zf), (x, y + 0.03, zf + out * 0.08), 0.008, 5)
    xc = x0 + (x1 - x0) * 0.5 / n                         # the coat: a soft hanging loft
    secs = []
    for k, (yy, wdt, dz) in enumerate(((y + 0.02, 0.07, 0.03), (y - 0.08, 0.2, 0.06), (y - 0.45, 0.22, 0.07), (y - 0.85, 0.24, 0.06))):
        zc = zf + out * (dz + 0.02)
        secs.append([(xc - wdt, yy, zc - 0.03), (xc + wdt, yy, zc - 0.03), (xc + wdt * 0.9, yy, zc + out * 0.05), (xc - wdt * 0.9, yy, zc + out * 0.05)])
    p.loft(coat, secs)
    p.tube(coat, [(xc + 0.2, y - 0.1, zf + out * 0.06), (xc + 0.24, y - 0.4, zf + out * 0.08), (xc + 0.22, y - 0.62, zf + out * 0.09)], 0.04, 6)   # sleeve
    if hat:
        xh = x0 + (x1 - x0) * 2.5 / n
        p.lathe("leather", [(0.0, 0.0), (0.16, 0.0), (0.16, 0.01), (0.09, 0.015), (0.085, 0.11), (0.0, 0.12)], (xh, y - 0.06, zf + out * 0.1), "z", 16,
                r=(90 if out < 0 else -90, 0, 0))


def laundry_line(a, x0, x1, y, z, seed=0, base_y=0.0):
    """Two posts and a line along x at height y, clothes pegged on it in object `Laundry` (pivot on the line)."""
    de = a.p("Detail")
    r = K.rng(seed)
    for x in (x0, x1):
        de.cyl(K.wood("dark", "y"), 0.035, y + 0.1 - base_y, (x, (y + 0.1 + base_y) / 2, z), "y", 8, 0.004)
        de.box(K.wood("dark", "x"), (0.36, 0.04, 0.04), (x, y + 0.05, z), bevel=0.004)
    sag = 0.08
    pts = [(x0 + (x1 - x0) * t / 8, y - sag * math.sin(math.pi * t / 8), z) for t in range(9)]
    de.tube("rope", pts, 0.005, 4, caps=False)
    la = a.p("Laundry", "Cloth", pivot=((x0 + x1) / 2, y, z), mover=True)
    x = x0 + 0.3
    items = ["ticking", "cloth_red", "quilt", "cloth_cream", "curtain", "cloth_blue", "sheet"]
    while x < x1 - 0.5:
        m = r.choice([i for i in items if i in K.M])
        wdt = r.uniform(0.32, 0.7)
        h = r.uniform(0.4, 0.8) if wdt > 0.5 else r.uniform(0.35, 0.55)
        t = (x + wdt / 2 - x0) / (x1 - x0)
        yl = y - sag * math.sin(math.pi * t)
        secs = []
        for k in range(4):                                  # a slightly billowing sheet
            yy = yl - h * k / 3
            bz = 0.03 * math.sin(k * 1.3 + x) * (k / 3)
            secs.append([(x, yy, z + bz - 0.004), (x + wdt / 2, yy, z + bz + 0.012 * (k > 0) - 0.004), (x + wdt, yy, z + bz - 0.004),
                         (x + wdt, yy, z + bz + 0.004), (x + wdt / 2, yy, z + bz + 0.012 * (k > 0) + 0.004), (x, yy, z + bz + 0.004)])
        la.loft(m, secs)
        for px in (x + 0.04, x + wdt - 0.04):               # pegs
            de.box(K.wood("light", "y"), (0.012, 0.06, 0.016), (px, yl - 0.01, z), bevel=0.002)
        x += wdt + r.uniform(0.08, 0.25)


def drainpipe(p, x, z, y_top, y_bot, m="steel_dark", r=0.04, kick=(0.0, 0.0)):
    """Downpipe from a gutter to the ground with a shoe kicking out by `kick` (dx, dz)."""
    p.tube(m, [(x, y_top, z), (x, y_bot + 0.2, z), (x + kick[0], y_bot + 0.06, z + kick[1])], r, 8, caps=False)
    for k in range(int((y_top - y_bot) / 0.9)):            # brackets
        y = y_top - 0.3 - k * 0.9
        p.cyl(m, r + 0.008, 0.02, (x, y, z), "y", 8, 0.0)


def gutter(p, x0, x1, y, z, m="steel_dark", r=0.05):
    p.add(K.kit.bm_lathe([(r, -0.5), (r, 0.5)], 12, "Z", False), m,
          Matrix.Translation(((x0 + x1) / 2, y, z)) @ K._axis_mtx("x") @ Matrix.Diagonal((1, 1, x1 - x0, 1)))


def lantern(a, at, name="Lamp", bracket_to=None):
    """Hurricane lantern; the glass + flame are their own object (lit at night)."""
    x, y, z = at
    de = a.p("Detail")
    de.cyl("steel_dark", 0.06, 0.02, (x, y, z), "y", 12, 0.003)
    de.cyl("steel_dark", 0.045, 0.03, (x, y + 0.2, z), "y", 12, 0.003)
    for k in range(4):
        an = k * math.pi / 2 + 0.78
        de.rod("steel_dark", (x + math.cos(an) * 0.05, y, z + math.sin(an) * 0.05), (x + math.cos(an) * 0.045, y + 0.2, z + math.sin(an) * 0.045), 0.005, 4)
    de.torus("steel_dark", 0.05, 0.005, (x, y + 0.28, z), "z", 10, 4)
    if bracket_to is not None:
        de.tube("black", [(x, y + 0.33, z), (x, y + 0.36, z), bracket_to], 0.008, 5)
    g = a.p(name, "Glass", pivot=(x, y + 0.36, z), mover=True)          # pivot at the hook (it swings)
    g.lathe("bulb_warm", [(0.0, 0.0), (0.035, 0.0), (0.045, 0.06), (0.04, 0.14), (0.02, 0.18), (0.0, 0.18)], (x, y + 0.01, z), "y", 12)


def string_lights(a, pts, n=12, name="Lamp2", sag=0.12):
    """Festoon of bulbs on a sagging wire through `pts` (each span sags); bulbs in their own object."""
    de = a.p("Detail")
    bl = a.p(name, "Glass", pivot=tuple((Vector(pts[0]) + Vector(pts[-1])) / 2), mover=True)
    for (p0, p1) in zip(pts, pts[1:]):
        p0, p1 = Vector(p0), Vector(p1)
        wire = []
        for t in range(9):
            q = p0.lerp(p1, t / 8)
            q.y -= sag * math.sin(math.pi * t / 8)
            wire.append(tuple(q))
        de.tube("black", wire, 0.004, 4, caps=False)
        for k in range(n):
            t = (k + 0.5) / n
            q = p0.lerp(p1, t)
            q.y -= sag * math.sin(math.pi * t) + 0.04
            de.cyl("black", 0.009, 0.02, (q.x, q.y + 0.025, q.z), "y", 5, 0.0)
            bl.sphere("bulb_warm", 0.022, tuple(q), (1, 1.25, 1), 6, 4)


def soot_ring(p, x0, x1, y0, y1, z0, z1, h=0.3):
    """Blackened band at the top of a chimney / stovepipe (box chimney)."""
    p.box2("soot", (x0 - 0.006, y1 - h, z0 - 0.006), (x1 + 0.006, y1 + 0.002, z1 + 0.006), bevel=0.004)


def stovepipe(p, x, y0, y1, z, r=0.07, cap=True, a=None):
    """Stovepipe with a soot band; with the asset `a` the cap is object `Chimney` (pivot at the outlet)."""
    p.cyl("steel_dark", r, y1 - y0, (x, (y0 + y1) / 2, z), "y", 12, 0.003)
    p.cyl("soot", r + 0.004, 0.18, (x, y1 - 0.09, z), "y", 12, 0.002)
    if cap:
        if a is not None:
            p = a.p("Chimney", "Scrap", pivot=(x, y1, z))
        p.lathe("rust", [(0.0, 0.0), (r * 2.0, 0.0), (0.0, r * 0.9)], (x, y1 + 0.08, z), "y", 12)
        for k in range(3):
            an = k * 2.09
            p.rod("steel_dark", (x + math.cos(an) * r, y1, z + math.sin(an) * r), (x + math.cos(an) * r * 1.5, y1 + 0.08, z + math.sin(an) * r * 1.5), 0.005, 4)


def sign_board(a, cx, cy, z, w, h, text_m="cloth_cream", board_m=None, out=-1, hang=True, letters=5, seed=0):
    """A painted signboard: object `Sign` (pivot at the top edge so it can swing when it hangs from chains)."""
    board_m = board_m or K.wood("", "x")
    sg = a.p("Sign", "Wood", pivot=(cx, cy + h / 2 + (0.12 if hang else 0), z))
    sg.box2(board_m, (cx - w / 2, cy - h / 2, z - 0.015), (cx + w / 2, cy + h / 2, z + 0.015), bevel=0.01)
    r = K.rng(seed)
    lw = (w - 0.12) / letters
    for i in range(letters):                                # painted block letters
        x = cx - w / 2 + 0.06 + i * lw
        hh = h * r.uniform(0.45, 0.6)
        sg.box2(text_m, (x + lw * 0.15, cy - hh / 2, z + out * 0.016), (x + lw * 0.85, cy + hh / 2, z + out * 0.019), bevel=0)
    if hang:
        for sx in (-1, 1):
            sg.rod("steel_dark", (cx + sx * w * 0.35, cy + h / 2, z), (cx + sx * w * 0.35, cy + h / 2 + 0.12, z), 0.006, 4)


def potted(p, at, m="terracotta", plant="leaf", r=0.09):
    x, y, z = at
    p.lathe(m, [(0.0, 0.0), (r * 0.75, 0.0), (r, r * 1.3), (r * 1.08, r * 1.35), (r * 1.08, r * 1.5), (0.0, r * 1.5)], (x, y, z), "y", 12)
    for k in range(5):
        an = k * 1.25
        p.sphere(plant, r * 0.6, (x + math.cos(an) * r * 0.5, y + r * 1.8 + 0.03 * (k % 2), z + math.sin(an) * r * 0.5), (1, 0.8, 1), 6, 4)
