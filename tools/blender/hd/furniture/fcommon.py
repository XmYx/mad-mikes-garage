"""Shared bits for the HD furniture scripts (0.08 m voxel grids: lo/hi = the faces of voxel index i)."""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "world"))
import hdkit as K  # noqa: E402,F401
from hdkit import Asset, Vector, Matrix  # noqa: E402,F401
from texmat import image_material, decal_material  # noqa: E402,F401  (textures/texmat.py, on the path via hdkit)

VS = 0.08
HERE = os.path.dirname(os.path.abspath(__file__))


def lo(i, vs=VS):
    return (i - 0.5) * vs


def hi(i, vs=VS):
    return (i + 0.5) * vs


def c(i, vs=VS):
    return i * vs


def W(tone="", axis="x"):
    return K.wood(tone, axis)


def knob(p, m, at, r=0.018, axis="z", length=0.02):
    p.cyl(m, r, length, at, axis, 10, 0.004)


def screws(p, m, pts, r=0.007, axis="z"):
    for q in pts:
        p.cyl(m, r, 0.006, q, axis, 6, 0.0)


def leg_square(p, m, x, z, y0, y1, w=0.05, taper=0.0):
    if taper:
        p.add(K.kit.bm_cyl(w * 0.7071, y1 - y0, 4, 0.0, w * 0.7071 * (1 - taper)), m,
              Matrix.Translation((x, (y0 + y1) / 2, z)) @ Matrix.Rotation(math.radians(45), 4, "Y") @ K._axis_mtx("y"))
    else:
        p.box2(m, (x - w / 2, y0, z - w / 2), (x + w / 2, y1, z + w / 2), bevel=0.006)


def drawer_front(p, m, handle_m, x0, x1, y0, y1, z, t=0.02):
    p.box2(m, (x0 + 0.004, y0 + 0.004, z), (x1 - 0.004, y1 - 0.004, z + t), bevel=0.004)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    p.box2(handle_m, (cx - 0.05, cy - 0.008, z + t), (cx + 0.05, cy + 0.008, z + t + 0.014), bevel=0.003)


def bulb(p, m, at, r=0.03):
    p.sphere(m, r, at, (1, 1.2, 1), 12, 8)


def run(tiles, prefix):
    K.run_tiles(tiles, "furniture", HERE, prefix)


# ------------------------------------------------------------------------------------------ lived-in bits (cozy pass)
def drape(p, m, x0, x1, z0, z1, y, hang=0.0, t=0.012, seed=0, amp=0.012, nx=16, nz=12, sides=(True, True), foot=False,
          fold_z=None):
    """A soft cloth sheet (quilt, blanket, tablecloth) lying over the box top x0..x1 / z0..z1 at height y: rumpled by a
    few ripples, hanging `hang` metres down over the x sides (and the -z foot end when `foot`), `t` thick.
    fold_z: a turned-down band at that z (sheet folded back over itself)."""
    r = K.rng(seed)
    ph = [r.uniform(0, 6.28) for _ in range(4)]

    def hgt(u, w):
        return amp * (math.sin(u * 9.0 + ph[0]) * math.sin(w * 5.0 + ph[1]) + 0.6 * math.sin(u * 17 + w * 7 + ph[2]))

    xs = []
    if hang > 0 and sides[0]:
        xs += [(x0 - 0.012, y - hang), (x0 - 0.018, y - hang * 0.55), (x0 - 0.012, y - 0.02)]
    for i in range(nx + 1):
        xs.append((x0 + (x1 - x0) * i / nx, y))
    if hang > 0 and sides[1]:
        xs += [(x1 + 0.012, y - 0.02), (x1 + 0.018, y - hang * 0.55), (x1 + 0.012, y - hang)]
    secs = []
    zs = [z0 + (z1 - z0) * k / nz for k in range(nz + 1)]
    if foot and hang > 0:
        zs = [z0 - 0.012] + zs
    for zi, z in enumerate(zs):
        top, bot = [], []
        for (x, yy) in xs:
            u, w = (x - x0) / max(1e-3, x1 - x0), (z - z0) / max(1e-3, z1 - z0)
            dy = hgt(u, w)
            if yy < y - 0.01:
                dy *= 0.3
            yy2 = yy + dy
            zz = z
            if foot and zi == 0:
                yy2 = min(yy2, y - hang)
            top.append((x, yy2 + t / 2, zz))
            bot.append((x, yy2 - t / 2, zz))
        secs.append(top + list(reversed(bot)))
    p.loft(m, secs)
    if fold_z is not None:
        p.loft(m, [[(x, yy + t + 0.004 + (0.004 if yy >= y - 0.01 else 0), z) for (x, yy) in xs] +
                   [(x, yy + 0.004, z) for (x, yy) in reversed(xs)] for z in (fold_z - 0.09, fold_z + 0.09)])


def pillow(p, m, c_, w=0.5, d=0.34, h=0.13, yaw=0.0, tilt=0.0):
    """Plump pillow / cushion (superellipse puffed in the middle)."""
    x, y, z = c_
    p.add(K.kit.bm_sphere(1.0, 18, 10), m, Matrix.Translation((x, y, z)) @ K.rot(tilt, yaw, 0) @
          Matrix.Diagonal((w / 2, h / 2, d / 2, 1)))


def folded(p, m, lo_, hi_, layers=3, bevel=0.02):
    """A folded blanket / towel: stacked soft slabs."""
    ly = (hi_[1] - lo_[1]) / layers
    for k in range(layers):
        d = 0.006 * (k % 2)
        p.box2(m, (lo_[0] + d, lo_[1] + k * ly, lo_[2] + d), (hi_[0] - d, lo_[1] + (k + 1) * ly - 0.002, hi_[2] - d), bevel=min(bevel, ly * 0.45), segs=2)


def mug(p, m, at, r=0.04, h=0.09):
    x, y, z = at
    p.lathe(m, [(0.0, 0.0), (r, 0.0), (r * 1.02, h), (r * 0.9, h), (r * 0.88, 0.012), (0.0, 0.012)], (x, y, z), "y", 14)
    p.torus(m, h * 0.28, 0.008, (x + r + h * 0.18, y + h * 0.5, z), "z", 10, 4)


def kettle(p, m, at, r=0.09, handle_m=None):
    x, y, z = at
    p.lathe(m, [(0.0, 0.0), (r * 0.85, 0.0), (r, r * 0.35), (r * 0.95, r * 0.95), (r * 0.5, r * 1.3), (r * 0.2, r * 1.38), (0.0, r * 1.4)], (x, y, z), "y", 18)
    p.tube(m, [(x + r * 0.8, y + r * 0.4, z), (x + r * 1.3, y + r * 0.9, z), (x + r * 1.5, y + r * 1.15, z)], 0.012, 6)
    p.tube(handle_m or m, [(x - r * 0.5, y + r * 1.25, z), (x - r * 0.3, y + r * 1.9, z), (x + r * 0.3, y + r * 1.9, z), (x + r * 0.5, y + r * 1.25, z)], 0.008, 5, caps=False)


def pan(p, m, at, r=0.12, yaw=0.0):
    x, y, z = at
    p.lathe(m, [(0.0, 0.0), (r * 0.85, 0.0), (r, 0.05), (r * 0.94, 0.05), (r * 0.8, 0.008), (0.0, 0.008)], (x, y, z), "y", 20)
    a = math.radians(yaw)
    p.rod(m, (x + math.cos(a) * r, y + 0.04, z + math.sin(a) * r), (x + math.cos(a) * (r + 0.2), y + 0.06, z + math.sin(a) * (r + 0.2)), 0.012, 6)


def jar(p, glass_m, lid_m, at, r=0.045, h=0.13, fill_m=None, label_m=None):
    x, y, z = at
    p.lathe(glass_m, [(0.0, 0.0), (r, 0.0), (r * 1.02, h * 0.8), (r * 0.8, h * 0.9), (r * 0.8, h), (0.0, h)], (x, y, z), "y", 14)
    if fill_m:
        p.cyl(fill_m, r * 0.9, h * 0.6, (x, y + h * 0.32, z), "y", 12, 0.0)
    p.cyl(lid_m, r * 0.86, 0.02, (x, y + h + 0.008, z), "y", 12, 0.003)
    if label_m:
        p.cyl(label_m, r * 1.03, h * 0.35, (x, y + h * 0.42, z), "y", 14, 0.0, caps=False)


def candle(wax_p, flame_p, at, r=0.022, h=0.1, drips=3, seed=0, wax="wax", flame="candle_flame"):
    """Candle stub with wax drips; the flame goes to `flame_p` (its own object, so the game can flicker it)."""
    x, y, z = at
    rr = K.rng(seed)
    wax_p.lathe(wax, [(0.0, 0.0), (r * 1.15, 0.0), (r, 0.01), (r, h - 0.01), (r * 0.8, h), (0.0, h - 0.008)], (x, y, z), "y", 12)
    for k in range(drips):
        an = rr.uniform(0, math.tau)
        L = rr.uniform(0.02, h * 0.6)
        wax_p.rod(wax, (x + math.cos(an) * r, y + h - 0.004, z + math.sin(an) * r), (x + math.cos(an) * (r + 0.003), y + h - L, z + math.sin(an) * (r + 0.003)), 0.005, 5)
    wax_p.rod("black", (x, y + h - 0.01, z), (x, y + h + 0.012, z), 0.002, 4)
    flame_p.lathe(flame, [(0.0, 0.0), (0.008, 0.008), (0.006, 0.02), (0.0, 0.035)], (x, y + h + 0.008, z), "y", 8)


def log_pile(p, at, n=7, L=0.42, r=0.05, axis="x", seed=0):
    """Split firewood stacked in a small pile (logs along a game axis)."""
    x, y, z = at
    rr = K.rng(seed)
    row, k = 0, 0
    while k < n:
        per = max(1, 3 - row)
        for j in range(per):
            if k >= n:
                break
            off = (j - (per - 1) / 2) * r * 2.05
            c_ = (x, y + r + row * r * 1.75, z + off) if axis == "x" else (x + off, y + r + row * r * 1.75, z)
            p.cyl(K.wood(rr.choice(["", "dark", "light"]), axis), r * rr.uniform(0.85, 1.1), L * rr.uniform(0.9, 1.05), c_, axis, 7, 0.006)
            k += 1
        row += 1


def frame_picture(p, frame_m, pic_m, x0, x1, y0, y1, z, t=0.03, bar=0.03):
    """Wall picture in the game xy plane facing +z: a moulded frame and the picture (decal material) at z."""
    for (a0, b0, a1, b1) in ((x0, y0, x1, y0 + bar), (x0, y1 - bar, x1, y1), (x0, y0, x0 + bar, y1), (x1 - bar, y0, x1, y1)):
        p.box2(frame_m, (a0, b0, z), (a1, b1, z + t), bevel=0.006)
    p.box2(pic_m, (x0 + bar, y0 + bar, z + 0.004), (x1 - bar, y1 - bar, z + t * 0.6), bevel=0)
