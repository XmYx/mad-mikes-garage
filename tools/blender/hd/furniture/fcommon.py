"""Shared bits for the HD furniture scripts (0.08 m voxel grids: lo/hi = the faces of voxel index i)."""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "world"))
import hdkit as K  # noqa: E402,F401
from hdkit import Asset, Vector, Matrix  # noqa: E402,F401

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
