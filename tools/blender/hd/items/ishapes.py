"""Shape builders for HD inventory / world items (real-world size, metres, kit frame: Z up, front = -Y).
Every builder returns a built mesh object resting on z = 0, centred in x/y (the WorldItem convention: lowest point on
the origin). Colours are sRGB hex strings from the game data (FoodLibrary / ItemCatalog colours)."""
import math
import random

from mathutils import Matrix, Vector

import kit
import pa
from pa import M, Part

C = {}


def col(hexstr, rough=0.6, bump=0.3):
    """Organic / painted material for a game colour (cached per colour)."""
    key = (hexstr, rough)
    if key not in C:
        C[key] = kit.surface("Item_%s_%d" % (hexstr, int(rough * 10)), kit._hex(hexstr), dust=0.05, rough=rough, var=0.16, bump=bump)
    return C[key]


def shade(hexstr, k):
    r, g, b = (int(hexstr[i:i + 2], 16) for i in (0, 2, 4))
    return "%02x%02x%02x" % tuple(max(0, min(255, int(c * k))) for c in (r, g, b))


def rnd(seed):
    return random.Random(seed)


# ------------------------------------------------------------------------------------------------ containers
def can(name, label, r=0.038, h=0.11, top="tin"):
    p = Part(name)
    p.lathe(M["tin"], [(0.0, 0.0), (r - 0.003, 0.0), (r, 0.004), (r, h - 0.004), (r - 0.003, h), (0.0, h)], (0, 0, 0), "Z", 24)
    p.cyl(col(label, 0.7), r + 0.001, h * 0.7, (0, 0, h / 2), "Z", 24, bevel=0.0, caps=False)
    for z in (0.012, h - 0.012):
        pa.ring(p, M["tin"], (0, 0, z), r + 0.0015, 0.0018, "Z", 24, 4)
    p.box(M["label_cream"], (r * 0.9, 0.002, h * 0.3), (0, -r - 0.0015, h * 0.5), bevel=0.0)
    if top == "pull":
        p.add(kit.bm_torus(0.008, 0.0015, 10, 4), M["tin"], (0.01, 0, h + 0.002))
    return p.build()


def bottle(name, glass, label=None, liquid=None, h=0.24, r=0.035, cap="tin", neck=0.4):
    p = Part(name)
    prof = [(0.0, 0.0), (r * 0.92, 0.0), (r, 0.006), (r, h * (1 - neck)), (r * 0.45, h * (1 - neck * 0.4)), (r * 0.38, h - 0.006), (r * 0.42, h), (0.0, h)]
    p.lathe(glass, prof, (0, 0, 0), "Z", 20)
    if liquid:
        p.lathe(col(liquid, 0.2), [(0.0, 0.003), (r * 0.93, 0.003), (r * 0.93, h * 0.55), (0.0, h * 0.55)], (0, 0, 0), "Z", 20)
    if label:
        p.cyl(col(label, 0.7), r + 0.001, h * 0.28, (0, 0, h * 0.32), "Z", 20, bevel=0.0, caps=False)
    if cap:
        p.cyl(M[cap] if cap in M else col(cap), r * 0.45, 0.012, (0, 0, h + 0.004), "Z", 14, bevel=0.002)
    return p.build()


def jar(name, contents, lid="tin", h=0.12, r=0.045, cloth=None):
    p = Part(name)
    p.lathe(M["glass_jar"], [(0.0, 0.0), (r - 0.004, 0.0), (r, 0.006), (r, h * 0.85), (r * 0.85, h * 0.92), (r * 0.85, h), (0.0, h)], (0, 0, 0), "Z", 20)
    p.lathe(col(contents, 0.5, 0.5), [(0.0, 0.004), (r - 0.004, 0.004), (r - 0.004, h * 0.8), (0.0, h * 0.82)], (0, 0, 0), "Z", 20)
    if cloth:
        p.add(kit.bm_lathe([(0.0, h + 0.008), (r * 0.9, h + 0.006), (r * 1.15, h - 0.01), (r * 1.18, h - 0.03)], 16), col(cloth, 0.9))
        pa.ring(p, M["rope"], (0, 0, h - 0.012), r * 0.88, 0.003, "Z", 16, 4)
    else:
        p.cyl(M[lid] if lid in M else col(lid), r * 0.88, 0.014, (0, 0, h + 0.004), "Z", 20, bevel=0.003)
    p.box(M["paper_old"], (r * 0.8, 0.002, h * 0.3), (0, -r - 0.001, h * 0.45), bevel=0.0)
    return p.build()


def box(name, mat, size, label=None, z=0.0):
    p = Part(name)
    sx, sy, sz = size
    p.box(mat, size, (0, 0, z + sz / 2), bevel=min(size) * 0.12)
    if label:
        p.box(label, (sx * 0.7, 0.002, sz * 0.4), (0, -sy / 2 - 0.0012, z + sz * 0.5), bevel=0.0)
    return p.build()


def pouch(name, cloth, contents=None, w=0.1, h=0.12, tie=True, seed=1):
    """Drawstring pouch / small sack with its contents showing at the neck."""
    p = Part(name)
    prof = [(0.0, 0.0), (w * 0.45, 0.0), (w * 0.55, h * 0.12), (w * 0.58, h * 0.5), (w * 0.46, h * 0.82), (w * 0.22, h * 0.9), (w * 0.26, h)]
    bm = kit.bm_lathe(prof, 16)
    r = rnd(seed)
    for v in bm.verts:
        v.co.x *= 1 + r.uniform(-0.06, 0.06)
        v.co.y *= 1 + r.uniform(-0.06, 0.06)
    p.add(bm, cloth)
    if contents:
        p.add(kit.bm_rock(w * 0.22, seed, 0.5, 2), col(contents, 0.8, 0.8), (0, 0, h * 0.98))
    if tie:
        pa.ring(p, M["rope"], (0, 0, h * 0.86), w * 0.22, 0.004, "Z", 14, 4)
    return p.build()


def sack(name, cloth, contents=None, w=0.36, h=0.5, seed=2):
    return pouch(name, cloth, contents, w, h, True, seed)


def paper_bag(name, contents_hex=None, w=0.12, d=0.08, h=0.18, label=None):
    p = Part(name)
    p.box(M["cardboard"] if label is None else M["paper_old"], (w, d, h), (0, 0, h / 2), bevel=0.006)
    p.box(M["paper_old"], (w, d * 0.5, 0.04), (0, 0, h + 0.012), (0.3, 0, 0), bevel=0.003)
    if label:
        p.box(col(label, 0.7), (w * 0.7, 0.002, h * 0.35), (0, -d / 2 - 0.001, h * 0.5), bevel=0)
    return p.build()


# ------------------------------------------------------------------------------------------------ produce
def sphere_fruit(name, hexc, r=0.04, squash=0.9, stem=True, leaf=False, ribs=0, calyx=False, seed=3):
    p = Part(name)
    bm = kit.bm_sphere(r, 20, 12)
    for v in bm.verts:
        v.co.z *= squash
        if ribs:
            a = math.atan2(v.co.y, v.co.x)
            k = 1 + 0.06 * math.cos(a * ribs)
            v.co.x *= k
            v.co.y *= k
    p.add(bm, col(hexc, 0.45, 0.2), (0, 0, r * squash))
    if stem:
        pa.rod(p, M["wood_dark"], (0, 0, r * squash * 1.9), (0.004, 0.002, r * squash * 2 + 0.018), 0.003 + r * 0.04, 6)
    if leaf:
        p.add(kit.bm_box(0.03, 0.012, 0.002, bevel=0.001), M["leaf"], (0.016, 0, r * squash * 2 + 0.008), (0.2, -0.4, 0.4))
    if calyx:
        for k in range(5):
            a = k / 5 * math.tau
            p.add(kit.bm_box(r * 0.5, 0.008, 0.002, bevel=0), col("3a7024", 0.6), (math.cos(a) * r * 0.2, math.sin(a) * r * 0.2, r * squash * 2 - 0.002), (0, -0.3, a))
    return p.build()


def blob(name, hexc, r=0.05, squash=0.6, seed=4, rough=0.7):
    p = Part(name)
    p.add(kit.bm_rock(r, seed, squash, 2), col(hexc, rough, 0.6), (0, 0, r * squash * 0.9))
    return p.build()


def carrot(name, hexc="e07020", l=0.18, r=0.022):
    p = Part(name)
    p.add(kit.bm_cyl(r, l, 12, 0.0, 0.003), col(hexc, 0.6, 0.6), (0, 0, r), (0, math.pi / 2, 0))
    for k in range(5):
        pa.rod(p, M["leaf"], (-l / 2, 0, r), (-l / 2 - 0.08, (k - 2) * 0.012, r + 0.02 + k * 0.004), 0.004, 4)
    return p.build()


def corn(name, hexc="e0c040", husk="8a9a3a", roast=False):
    p = Part(name)
    l, r = 0.2, 0.026
    bm = kit.bm_cyl(r, l, 16, 0.0, r * 0.6)
    p.add(bm, col(hexc, 0.6, 1.0), (0, 0, r), (0, math.pi / 2, 0))
    if not roast:
        for k in range(3):
            a = k / 3 * math.tau
            p.add(kit.bm_box(l * 0.7, 0.03, 0.003, bevel=0.001), col(husk, 0.8), (-l * 0.25, math.cos(a) * r * 0.9, r + math.sin(a) * r * 0.9), (a, 0, 0))
    else:
        p.add(kit.bm_box(0.06, 0.03, 0.004), M["foam"], (0.03, 0, r * 2 - 0.002))      # butter
        pa.rod(p, M["wood_light"], (-l / 2 - 0.08, 0, r), (-l / 2 + 0.02, 0, r), 0.004, 6)
    return p.build()


def cabbage(name, hexc="78a040"):
    p = Part(name)
    for k in range(4):
        r = 0.07 - k * 0.012
        bm = kit.bm_sphere(r, 16, 10)
        for v in bm.verts:
            v.co.z *= 0.85
        p.add(bm, col(shade(hexc, 0.85 + k * 0.08), 0.6, 0.6), (0.004 * k, 0, 0.06 + k * 0.006))
    return p.build()


def pumpkin(name, hexc="e08020"):
    return sphere_fruit(name, hexc, 0.13, 0.75, True, True, ribs=10)


def beet(name, hexc="7a2440"):
    p = Part(name)
    p.add(kit.bm_lathe([(0.0, 0.0), (0.03, 0.02), (0.045, 0.05), (0.04, 0.08), (0.015, 0.095), (0.0, 0.1)], 16), col(hexc, 0.6), (0, 0, 0))
    for k in range(4):
        a = k / 4 * math.tau
        pa.rod(p, col("3a7a2a", 0.7), (0, 0, 0.09), (math.cos(a) * 0.04, math.sin(a) * 0.04, 0.18), 0.004, 4)
    return p.build()


def berries(name, hexc):
    p = Part(name)
    p.add(kit.bm_lathe([(0.0, 0.0), (0.05, 0.0), (0.07, 0.04), (0.068, 0.045), (0.048, 0.006), (0.0, 0.006)], 16), M["leaf"])
    r = rnd(5)
    for k in range(14):
        a = r.random() * math.tau
        d = r.random() * 0.045
        p.sphere(col(hexc, 0.3), 0.011, (math.cos(a) * d, math.sin(a) * d, 0.018 + r.random() * 0.02), 8, 6)
    return p.build()


def mushrooms(name, hexc, n=3, dried=False):
    p = Part(name)
    r = rnd(6)
    for k in range(n):
        x, y = (k - 1) * 0.03, r.uniform(-0.02, 0.02)
        h = 0.04 + r.random() * 0.02
        p.cyl(M["cream"], 0.008, h, (x, y, h / 2), "Z", 8)
        cap = kit.bm_sphere(0.026, 14, 8)
        for v in cap.verts:
            v.co.z = max(v.co.z, -0.002) * 0.6
        p.add(cap, col(hexc, 0.6), (x, y, h))
    if dried:
        pa.rod(p, M["rope"], (-0.06, 0, 0.06), (0.06, 0, 0.06), 0.002, 4)
    return p.build()


def bundle(name, hexc, n=9, l=0.18, tie="rope", seed=7):
    """Bunch of stalks / herbs tied in the middle, lying on its side."""
    p = Part(name)
    r = rnd(seed)
    for k in range(n):
        y = r.uniform(-0.02, 0.02)
        z = 0.012 + r.uniform(0, 0.02)
        pa.rod(p, col(hexc, 0.8), (-l / 2, y, z), (l / 2, y * 1.6, z + r.uniform(-0.005, 0.01)), 0.004, 4)
        p.sphere(col(shade(hexc, 1.2), 0.8), 0.012, (l / 2, y * 1.6, z + 0.004), 6, 4)
    pa.ring(p, M[tie], (0, 0, 0.022), 0.022, 0.004, "X", 12, 4)
    return p.build()


def egg(name, hexc="eeeadc"):
    p = Part(name)
    bm = kit.bm_sphere(0.022, 14, 10)
    for v in bm.verts:
        v.co.z *= 1.3 if v.co.z > 0 else 1.15
    p.add(bm, col(hexc, 0.5, 0.1), (0, 0, 0.026))
    return p.build()


def coconut(name, hexc="6a4a2a"):
    p = Part(name)
    bm = kit.bm_sphere(0.06, 16, 10)
    for v in bm.verts:
        v.co.z *= 1.1
    p.add(bm, M["fur"], (0, 0, 0.066))
    for k in range(3):
        a = k / 3 * math.tau
        p.cyl(M["black"], 0.006, 0.004, (math.cos(a) * 0.012, math.sin(a) * 0.012, 0.132), "Z", 8)
    return p.build()


# ------------------------------------------------------------------------------------------------ cooked
def plate(p, r=0.1, mat=None):
    p.lathe(mat or M["cream"], [(0.0, 0.0), (r * 0.6, 0.0), (r, 0.014), (r * 0.96, 0.016), (r * 0.6, 0.004), (0.0, 0.004)], (0, 0, 0), "Z", 24)


def bowl(name, contents, r=0.07, chunks=None, seed=8):
    p = Part(name)
    p.lathe(M["wood"], [(0.0, 0.0), (r * 0.5, 0.0), (r * 0.9, r * 0.45), (r, r * 0.7), (r * 0.94, r * 0.7), (r * 0.84, r * 0.4), (r * 0.45, 0.008), (0.0, 0.008)], (0, 0, 0), "Z", 24)
    p.cyl(col(contents, 0.35, 0.4), r * 0.88, 0.004, (0, 0, r * 0.58), "Z", 24, bevel=0.0)
    if chunks:
        rr = rnd(seed)
        for k in range(7):
            a = rr.random() * math.tau
            d = rr.random() * r * 0.6
            p.add(kit.bm_rock(0.01, k, 0.6, 1), col(rr.choice(chunks), 0.6), (math.cos(a) * d, math.sin(a) * d, r * 0.6))
    pa.rod(p, M["steel"], (r * 0.4, -r * 0.2, r * 0.62), (r * 1.3, -r * 0.6, r * 0.9), 0.003, 6)
    return p.build()


def loaf(name, hexc, l=0.22, w=0.1, h=0.08, slashes=3):
    p = Part(name)
    p.add(kit.superloft([(y, 0, h * 0.5, w / 2, h / 2) for y in (-l / 2, -l * 0.4, l * 0.4, l / 2)], 20, 2.4), col(hexc, 0.7, 0.4))
    for k in range(slashes):
        p.box(col(shade(hexc, 1.25), 0.8), (w * 0.6, 0.012, 0.004), (0, -l * 0.3 + k * l * 0.3, h * 0.98), (0, 0, 0.5), bevel=0.0)
    return p.build()


def disc(name, hexc, r=0.1, h=0.012, n=1, gap=0.002):
    p = Part(name)
    for k in range(n):
        p.cyl(col(hexc, 0.7, 0.4), r * (1 - k * 0.02), h, (0, 0, h / 2 + k * (h + gap)), "Z", 24, bevel=h * 0.4)
    return p.build()


def pie(name, crust, filling):
    p = Part(name)
    p.lathe(M["tin"], [(0.0, 0.0), (0.09, 0.0), (0.11, 0.035), (0.105, 0.035), (0.085, 0.004), (0.0, 0.004)], (0, 0, 0), "Z", 24)
    p.cyl(col(crust, 0.7, 0.5), 0.104, 0.03, (0, 0, 0.026), "Z", 24, bevel=0.01)
    for k in range(6):
        a = k / 6 * math.tau
        p.box(col(filling, 0.5), (0.05, 0.006, 0.004), (math.cos(a) * 0.05, math.sin(a) * 0.05, 0.042), (0, 0, a), bevel=0)
    return p.build()


def steak(name, hexc, fat="eeeadc", bone=False, l=0.16, w=0.1, h=0.03):
    p = Part(name)
    pts = [(math.cos(a) * l / 2 * (1 + 0.15 * math.sin(3 * a)), math.sin(a) * w / 2) for a in [k / 16 * math.tau for k in range(16)]]
    p.add(kit.bm_grid(pts, 0.0, h), col(hexc, 0.55, 0.6))
    p.add(kit.bm_grid([(x * 1.03, y * 1.03) for x, y in pts[2:7]] + [(x * 0.92, y * 0.92) for x, y in reversed(pts[2:7])], 0.002, h - 0.002), col(fat, 0.6))
    if bone:
        pa.rod(p, M["bone"], (-l * 0.1, 0, h / 2), (l * 0.6, 0.01, h / 2), 0.01, 8)
    return p.build()


def jerky(name, hexc, n=4):
    p = Part(name)
    r = rnd(9)
    for k in range(n):
        p.box(col(hexc, 0.7, 0.8), (0.14, 0.025, 0.006), (0, -0.04 + k * 0.026, 0.004 + k * 0.004), (0, r.uniform(-0.1, 0.1), r.uniform(-0.2, 0.2)), bevel=0.002)
    return p.build()


def sausages(name, hexc, n=3):
    p = Part(name)
    pts = []
    for k in range(n * 6 + 1):
        t = k / (n * 6)
        pts.append(Vector((math.sin(t * math.pi * 1.2) * 0.06, -0.12 + t * 0.24, 0.018)))
    rads = [0.012 + 0.006 * abs(math.sin(k / 6 * math.pi)) for k in range(len(pts))]
    p.add(kit.bm_tube(pts, rads, 10), col(hexc, 0.4, 0.4))
    return p.build()


def skewer(name, hexc, piece_hex=None):
    p = Part(name)
    pa.rod(p, M["wood_light"], (-0.14, 0, 0.02), (0.14, 0, 0.02), 0.003, 6)
    for k in range(5):
        p.add(kit.bm_rock(0.016, k, 0.8, 1), col(piece_hex or hexc, 0.6), (-0.08 + k * 0.04, 0, 0.02))
    return p.build()


def fish(name, hexc, belly=None, l=0.3, h=0.08, flat=False, glow=False, stick=False):
    """Fish lying on its side (+X head)."""
    p = Part(name)
    secs = []
    for k in range(9):
        t = k / 8
        x = -l / 2 + t * l * 0.85
        hh = h / 2 * math.sin(min(1, t * 1.15) * math.pi) ** 0.7 + 0.004
        hw = hh * (0.25 if flat else 0.45)
        secs.append([(x, hh * math.sin(a), hw * (1 + math.cos(a))) for a in [j / 12 * math.tau for j in range(12)]])
    p.add(kit.bm_loft(secs), M["lamp_green"] if glow else col(hexc, 0.35, 0.4))
    thick = h / 2 * (0.25 if flat else 0.45)
    tail = [(-l / 2, 0.004), (-l / 2 - 0.07, h * 0.45), (-l / 2 - 0.05, 0.0), (-l / 2 - 0.07, -h * 0.45)]
    p.add(kit.bm_grid([(x, y) for x, y in tail], thick - 0.003, thick + 0.003), col(shade(hexc, 0.7), 0.5))
    p.sphere(M["black"], 0.006, (l * 0.3, h * 0.1, thick * 1.8), 8, 6)
    if belly:
        p.add(kit.bm_box(l * 0.5, h * 0.12, 0.004, bevel=0.001), col(belly, 0.5), (0, -h * 0.28, thick * 1.7))
    if stick:
        pa.rod(p, M["wood_light"], (-l * 0.7, 0, thick), (l * 0.6, 0, thick), 0.004, 6)
    return p.build()


def cheese(name, hexc, wax=None):
    p = Part(name)
    p.cyl(col(hexc, 0.6, 0.5), 0.09, 0.07, (0, 0, 0.035), "Z", 28, bevel=0.012)
    if wax:
        p.cyl(col(wax, 0.4), 0.0905, 0.03, (0, 0, 0.035), "Z", 28, bevel=0.0, caps=False)
    p.box(col(shade(hexc, 1.1), 0.7), (0.06, 0.03, 0.06), (0.085, 0.0, 0.035), (0, 0, 0.3), bevel=0.004)
    return p.build()


def mug(name, liquid, mat=None):
    p = Part(name)
    p.lathe(mat or M["tin"], [(0.0, 0.0), (0.04, 0.0), (0.042, 0.004), (0.042, 0.09), (0.038, 0.09), (0.038, 0.006), (0.0, 0.006)], (0, 0, 0), "Z", 20)
    p.cyl(col(liquid, 0.15), 0.037, 0.003, (0, 0, 0.07), "Z", 20, bevel=0.0)
    p.add(kit.bm_torus(0.022, 0.005, 12, 5), mat or M["tin"], (0.048, 0, 0.05), (math.pi / 2, 0, 0))
    return p.build()


def jug(name, glaze="9c9888", h=0.22):
    p = Part(name)
    p.lathe(col(glaze, 0.4), [(0.0, 0.0), (0.06, 0.0), (0.08, 0.05), (0.08, 0.12), (0.05, 0.17), (0.022, 0.19), (0.024, h), (0.0, h)], (0, 0, 0), "Z", 20)
    p.add(kit.bm_torus(0.03, 0.007, 12, 5), col(glaze, 0.4), (0.06, 0, 0.16), (math.pi / 2, 0, 0))
    p.cyl(M["wood_light"], 0.02, 0.025, (0, 0, h + 0.006), "Z", 10, bevel=0.003)
    return p.build()


# ------------------------------------------------------------------------------------------------ paper, media, misc
def book(name, cover, pages="eeeadc", w=0.15, h=0.22, t=0.035, title=None):
    """Book lying flat."""
    p = Part(name)
    p.box(col(cover, 0.7), (w, h, 0.004), (0, 0, 0.002), bevel=0.001)
    p.box(col(cover, 0.7), (w, h, 0.004), (0, 0, t - 0.002), bevel=0.001)
    p.box(col(cover, 0.7), (0.006, h, t), (-w / 2, 0, t / 2), bevel=0.002)
    p.box(col(pages, 0.9), (w - 0.008, h - 0.008, t - 0.008), (0.002, 0, t / 2), bevel=0.0)
    if title:
        p.box(col(title, 0.5), (w * 0.6, h * 0.12, 0.001), (0.01, h * 0.22, t + 0.0005), bevel=0)
    return p.build()


def cassette(name, label):
    p = Part(name)
    p.box(M["plastic"], (0.188, 0.104, 0.025), (0, 0, 0.0125), bevel=0.002)
    p.box(col(label, 0.7), (0.15, 0.04, 0.001), (0, 0.02, 0.0255), bevel=0)
    for x in (-0.045, 0.045):
        p.cyl(M["plastic_white"], 0.014, 0.0012, (x, -0.012, 0.0255), "Z", 12)
    return p.build()


def document(name, mat=None, w=0.21, h=0.297, sheets=3, folded=False, seed=10):
    p = Part(name)
    r = rnd(seed)
    for k in range(sheets):
        p.box(mat or M["paper"], (w, h, 0.0012), (r.uniform(-0.006, 0.006), r.uniform(-0.006, 0.006), 0.0008 + k * 0.0014), (0, 0, r.uniform(-0.05, 0.05)), bevel=0)
    for k in range(6):                                            # ink lines
        p.box(M["black"], (w * r.uniform(0.4, 0.75), 0.003, 0.0003), (-w * 0.05, h * 0.3 - k * 0.03, sheets * 0.0014 + 0.0004), bevel=0)
    if folded:
        p.box(M["paper_old"], (w, 0.004, 0.0015), (0, 0, sheets * 0.0014 + 0.001), bevel=0)
    return p.build()


def ledger(name, cover="56361e"):
    return book(name, cover, "dcd8c8", 0.2, 0.3, 0.03, title="c47a40")


def medal(name, metal="gold", ribbon="a02a2c"):
    p = Part(name)
    p.cyl(M[metal], 0.022, 0.004, (0, -0.02, 0.002), "Z", 24, bevel=0.001)
    p.cyl(M[metal], 0.016, 0.0045, (0, -0.02, 0.0025), "Z", 6, bevel=0.0)
    p.box(col(ribbon, 0.8), (0.032, 0.05, 0.002), (0, 0.025, 0.001), bevel=0)
    p.box(M[metal], (0.034, 0.006, 0.004), (0, 0.0, 0.002), bevel=0.001)
    return p.build()


def crate_small(name, band="c48c2a", s=0.3, h=0.24):
    p = Part(name)
    p.box(M["wood"], (s, s * 0.8, h), (0, 0, h / 2), bevel=0.006)
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.box(M["wood_dark"], (0.024, 0.024, h + 0.004), (sx * (s / 2 - 0.01), sy * (s * 0.4 - 0.01), h / 2), bevel=0.003)
    p.box(col(band, 0.6), (s + 0.004, s * 0.8 + 0.004, 0.04), (0, 0, h * 0.55), bevel=0.0)
    return p.build()


def tin_box(name, hexc, w=0.12, d=0.08, h=0.05):
    p = Part(name)
    p.box(col(hexc, 0.4), (w, d, h), (0, 0, h / 2), bevel=0.006)
    p.box(M["tin"], (w + 0.003, d + 0.003, 0.012), (0, 0, h - 0.004), bevel=0.003)
    return p.build()
