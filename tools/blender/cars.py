"""Real-car bodies for Mad Mike's Garage.

Each car is lofted in Blender from its real dimensions (side profile, beltline, plan-view rounding, tumblehome),
voxelized at 0.08 m by ray casting the mesh (BVH), and every surface voxel is given a material code (paint, glass,
lamps, bumpers, grille, trim) from the car's features. Output per car:
    <out>/<id>.txt          header (# key value) + "x y z code" lines in game voxel coords (+X right, +Y up, +Z front)
    <preview>/<id>_*.png    side + three-quarter preview renders (Workbench) for checking proportions

Run (from the repo root):  blender -b -P tools/blender/cars.py -- Assets/MadMax/Models/Cars [preview dir|-] [car ids...]
The C# side (Designs/ModelCars.cs) turns the voxels into a vehicle: shell, interior, doors, hood, sockets, arches.
"""
import math
import os
import sys

import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

S = 0.08          # voxel size (m)
TYRE_R = 0.31     # wheel_compact radius (m): wheel centre sits at socket y 5
Y_GROUND = 5 - TYRE_R / S
LIFT = 0.07       # extra ground clearance for the wasteland (m)

# ---------------------------------------------------------------- car specs (metres; s = distance from the front bumper)
# top / bottom / belt: (s, h) polylines. glass: windshield base s, windshield top s, roof end s, rear window base s.
# windows: side window openings (s0, s1). doors: (s0, s1). front / rear: (kind, x centre off the axis, h centre, width, height)
# kinds: H headlamp, O round headlamp, A indicator, T tail, R grille, B black band, C chrome band, L plate
CARS = {
    "fiat126p": dict(
        name="Fiat 126p", L=3.05, W=1.38, wb=1.84, fo=0.53, nf=3.0, nr=3.2, roof=0.82, shoulder=0.97,
        top=[(0, 0.44), (0.04, 0.58), (0.14, 0.70), (0.40, 0.77), (0.84, 0.82), (1.22, 1.30), (1.34, 1.33), (2.12, 1.31),
             (2.46, 0.95), (2.86, 0.88), (3.00, 0.78), (3.05, 0.50)],
        bottom=[(0, 0.30), (0.25, 0.20), (2.80, 0.20), (3.05, 0.30)],
        belt=[(0.84, 0.86), (2.40, 0.88)],
        glass=(0.86, 1.24, 2.10, 2.44), windows=[(0.98, 1.56), (1.64, 2.12)], doors=[(0.86, 1.60)],
        front=[("H", 0.44, 0.62, 0.20, 0.12), ("A", 0.44, 0.52, 0.12, 0.05), ("B", 0, 0.34, 1.40, 0.10)],
        rear=[("T", 0.50, 0.70, 0.12, 0.18), ("R", 0, 0.80, 0.50, 0.08), ("B", 0, 0.34, 1.40, 0.10), ("L", 0, 0.52, 0.30, 0.10)],
        sides=[], engine="rear"),
    "renault5": dict(
        name="Renault 5", L=3.51, W=1.52, wb=2.43, fo=0.55, nf=4.5, nr=5.0, roof=0.86, shoulder=0.97,
        top=[(0, 0.55), (0.06, 0.66), (0.25, 0.76), (0.94, 0.88), (1.36, 1.37), (1.46, 1.40), (2.95, 1.39),
             (3.28, 1.02), (3.46, 0.94), (3.51, 0.56)],
        bottom=[(0, 0.24), (0.40, 0.19), (3.20, 0.21), (3.51, 0.26)],
        belt=[(0.94, 0.90), (3.00, 0.95)],
        glass=(0.96, 1.36, 2.93, 3.26), windows=[(1.06, 2.06), (2.14, 2.60)], doors=[(0.94, 2.08)],
        front=[("H", 0.48, 0.68, 0.24, 0.14), ("A", 0.62, 0.60, 0.08, 0.06), ("R", 0, 0.66, 0.46, 0.10), ("B", 0, 0.38, 1.60, 0.26)],
        rear=[("T", 0.64, 0.86, 0.08, 0.28), ("B", 0, 0.38, 1.60, 0.26), ("L", 0, 0.62, 0.30, 0.10)],
        sides=[("B", 0.38, 0.10)], engine="front"),
    "citroen_bx": dict(
        name="Citroen BX", L=4.23, W=1.66, wb=2.655, fo=0.80, nf=5.0, nr=6.0, roof=0.84, shoulder=0.97,
        top=[(0, 0.50), (0.06, 0.64), (0.40, 0.74), (1.26, 0.86), (1.96, 1.33), (2.10, 1.36), (2.62, 1.36),
             (3.88, 1.00), (4.16, 0.98), (4.23, 0.58)],
        bottom=[(0, 0.26), (0.50, 0.18), (3.80, 0.20), (4.23, 0.30)],
        belt=[(1.26, 0.86), (3.90, 0.97)],
        glass=(1.28, 1.94, 2.60, 3.86), windows=[(1.42, 2.36), (2.46, 3.28), (3.36, 3.80)], doors=[(1.28, 2.40), (2.40, 3.30)],
        front=[("H", 0.52, 0.62, 0.32, 0.09), ("A", 0.73, 0.62, 0.06, 0.08), ("R", 0, 0.58, 0.60, 0.04), ("B", 0, 0.36, 1.70, 0.20)],
        rear=[("T", 0.55, 0.80, 0.32, 0.12), ("B", 0, 0.36, 1.70, 0.20), ("L", 0, 0.62, 0.34, 0.10)],
        sides=[("B", 0.34, 0.08)], engine="front"),
    "citroen_xm": dict(
        name="Citroen XM", L=4.71, W=1.79, wb=2.85, fo=0.90, nf=5.0, nr=5.5, roof=0.84, shoulder=0.97,
        top=[(0, 0.50), (0.06, 0.62), (0.45, 0.72), (1.34, 0.86), (2.04, 1.36), (2.16, 1.39), (2.94, 1.39),
             (4.04, 1.04), (4.44, 0.99), (4.70, 0.94), (4.71, 0.58)],
        bottom=[(0, 0.25), (0.60, 0.17), (4.30, 0.20), (4.71, 0.30)],
        belt=[(1.34, 0.84), (3.40, 0.93), (4.04, 0.98)],
        glass=(1.36, 2.02, 2.92, 4.02), windows=[(1.50, 2.46), (2.56, 3.40), (3.48, 3.98)], doors=[(1.36, 2.50), (2.50, 3.44)],
        front=[("H", 0.58, 0.64, 0.38, 0.08), ("A", 0.82, 0.64, 0.05, 0.08), ("R", 0, 0.60, 0.36, 0.04), ("B", 0, 0.36, 1.80, 0.18)],
        rear=[("T", 0, 0.84, 1.50, 0.10), ("B", 0, 0.38, 1.80, 0.18), ("L", 0, 0.64, 0.36, 0.10)],
        sides=[("B", 0.36, 0.06)], engine="front"),
    "citroen_xantia": dict(
        name="Citroen Xantia", L=4.44, W=1.76, wb=2.74, fo=0.85, nf=4.2, nr=4.8, roof=0.84, shoulder=0.97,
        top=[(0, 0.52), (0.08, 0.66), (0.45, 0.76), (1.34, 0.90), (2.04, 1.35), (2.16, 1.38), (2.90, 1.38),
             (3.84, 1.03), (4.26, 0.99), (4.43, 0.92), (4.44, 0.56)],
        bottom=[(0, 0.25), (0.55, 0.18), (4.00, 0.20), (4.44, 0.30)],
        belt=[(1.34, 0.90), (3.84, 0.98)],
        glass=(1.36, 2.02, 2.88, 3.82), windows=[(1.50, 2.44), (2.54, 3.34), (3.40, 3.78)], doors=[(1.36, 2.50), (2.50, 3.40)],
        front=[("H", 0.54, 0.66, 0.30, 0.12), ("A", 0.76, 0.64, 0.05, 0.10), ("R", 0, 0.66, 0.44, 0.08), ("B", 0, 0.38, 1.78, 0.20)],
        rear=[("T", 0.58, 0.80, 0.30, 0.14), ("B", 0, 0.38, 1.78, 0.20), ("L", 0, 0.62, 0.34, 0.10)],
        sides=[("B", 0.36, 0.06)], engine="front"),
    "lancia_ypsilon": dict(
        name="Lancia Ypsilon", L=3.78, W=1.70, wb=2.39, fo=0.72, nf=3.2, nr=3.6, roof=0.84, shoulder=0.96,
        top=[(0, 0.56), (0.10, 0.72), (0.40, 0.86), (0.94, 0.98), (1.70, 1.49), (1.86, 1.53), (2.96, 1.53),
             (3.52, 1.40), (3.70, 1.12), (3.78, 0.62)],
        bottom=[(0, 0.28), (0.40, 0.20), (3.50, 0.22), (3.78, 0.32)],
        belt=[(0.94, 0.98), (3.40, 1.08)],
        glass=(0.96, 1.68, 2.94, 3.50), windows=[(1.10, 2.10), (2.20, 3.18)], doors=[(0.96, 2.14)],
        front=[("H", 0.55, 0.78, 0.28, 0.14), ("R", 0, 0.62, 0.16, 0.24), ("B", 0, 0.40, 1.72, 0.18)],
        rear=[("T", 0.70, 1.14, 0.08, 0.36), ("B", 0, 0.42, 1.72, 0.18), ("L", 0, 0.66, 0.34, 0.10)],
        sides=[("B", 0.36, 0.06)], engine="front", grilleChrome=True),
    "fiat500": dict(
        name="Fiat 500", L=2.97, W=1.32, wb=1.84, fo=0.48, nf=2.6, nr=2.8, roof=0.80, shoulder=0.96,
        top=[(0, 0.42), (0.04, 0.56), (0.12, 0.66), (0.42, 0.74), (0.80, 0.80), (1.12, 1.26), (1.26, 1.32), (1.96, 1.31),
             (2.30, 1.06), (2.74, 0.82), (2.92, 0.70), (2.97, 0.48)],
        bottom=[(0, 0.30), (0.35, 0.22), (2.70, 0.22), (2.97, 0.32)],
        belt=[(0.80, 0.84), (2.26, 0.86)],
        glass=(0.82, 1.12, 1.96, 2.28), windows=[(0.92, 1.58), (1.66, 2.06)], doors=[(0.82, 1.62)],
        front=[("O", 0.48, 0.64, 0.16, 0.16), ("A", 0.40, 0.50, 0.06, 0.05), ("C", 0, 0.60, 0.40, 0.04), ("C", 0, 0.34, 1.34, 0.05)],
        rear=[("T", 0.52, 0.66, 0.08, 0.14), ("R", 0, 0.72, 0.44, 0.10), ("C", 0, 0.34, 1.34, 0.05), ("L", 0, 0.50, 0.28, 0.10)],
        sides=[("C", 0.46, 0.03)], engine="rear", roofPanel=(1.24, 1.92)),
    "peugeot205": dict(
        name="Peugeot 205", L=3.70, W=1.57, wb=2.42, fo=0.66, nf=5.5, nr=6.5, roof=0.85, shoulder=0.97,
        top=[(0, 0.56), (0.06, 0.70), (0.30, 0.78), (0.94, 0.86), (1.54, 1.34), (1.66, 1.37), (2.86, 1.37),
             (3.44, 1.04), (3.62, 0.96), (3.70, 0.58)],
        bottom=[(0, 0.25), (0.45, 0.19), (3.30, 0.21), (3.70, 0.28)],
        belt=[(0.94, 0.86), (3.40, 0.92)],
        glass=(0.96, 1.52, 2.84, 3.42), windows=[(1.06, 1.90), (2.00, 2.74), (2.80, 3.20)], doors=[(0.96, 1.95), (1.95, 2.80)],
        front=[("H", 0.50, 0.70, 0.28, 0.12), ("A", 0.70, 0.68, 0.06, 0.10), ("R", 0, 0.69, 0.46, 0.10), ("B", 0, 0.38, 1.60, 0.24)],
        rear=[("T", 0.56, 0.78, 0.22, 0.14), ("B", 0, 0.38, 1.60, 0.24), ("L", 0, 0.62, 0.32, 0.10)],
        sides=[("B", 0.54, 0.05), ("B", 0.30, 0.10)], engine="front"),
    "peugeot206": dict(
        name="Peugeot 206", L=3.83, W=1.65, wb=2.44, fo=0.75, nf=3.4, nr=4.0, roof=0.82, shoulder=0.96,
        top=[(0, 0.50), (0.08, 0.66), (0.40, 0.80), (1.00, 0.93), (1.64, 1.39), (1.80, 1.43), (2.86, 1.43),
             (3.52, 1.16), (3.76, 0.96), (3.83, 0.60)],
        bottom=[(0, 0.25), (0.50, 0.19), (3.40, 0.21), (3.83, 0.30)],
        belt=[(1.00, 0.92), (3.50, 1.02)],
        glass=(1.02, 1.62, 2.84, 3.50), windows=[(1.12, 1.95), (2.04, 2.84), (2.90, 3.30)], doors=[(1.02, 2.00), (2.00, 2.86)],
        front=[("H", 0.52, 0.76, 0.34, 0.12), ("R", 0, 0.46, 0.80, 0.18), ("B", 0, 0.30, 1.66, 0.10)],
        rear=[("T", 0.62, 0.92, 0.14, 0.22), ("B", 0, 0.38, 1.66, 0.20), ("L", 0, 0.66, 0.34, 0.10)],
        sides=[("B", 0.40, 0.05)], engine="front"),
    "peugeot207cc": dict(
        name="Peugeot 207 CC", L=4.04, W=1.75, wb=2.54, fo=0.82, nf=3.2, nr=3.8, roof=0.80, shoulder=0.96,
        top=[(0, 0.48), (0.10, 0.66), (0.45, 0.82), (1.14, 0.95), (1.84, 1.37), (2.00, 1.40), (2.50, 1.40),
             (3.30, 1.03), (3.88, 0.99), (4.04, 0.60)],
        bottom=[(0, 0.24), (0.55, 0.18), (3.60, 0.20), (4.04, 0.28)],
        belt=[(1.14, 0.95), (3.30, 1.01)],
        glass=(1.16, 1.82, 2.48, 3.26), windows=[(1.26, 2.34), (2.40, 2.92)], doors=[(1.16, 2.40)],
        front=[("H", 0.55, 0.78, 0.36, 0.16), ("R", 0, 0.42, 0.90, 0.22), ("B", 0, 0.28, 1.70, 0.08)],
        rear=[("T", 0.60, 0.86, 0.26, 0.12), ("B", 0, 0.36, 1.74, 0.18), ("L", 0, 0.62, 0.34, 0.10)],
        sides=[], engine="front"),
    "peugeot405": dict(
        name="Peugeot 405", L=4.41, W=1.69, wb=2.67, fo=0.84, nf=5.5, nr=6.0, roof=0.84, shoulder=0.97,
        top=[(0, 0.56), (0.06, 0.70), (0.35, 0.78), (1.14, 0.88), (1.80, 1.38), (1.92, 1.41), (3.00, 1.41),
             (3.52, 1.04), (4.30, 1.00), (4.40, 0.94), (4.41, 0.58)],
        bottom=[(0, 0.25), (0.55, 0.18), (4.00, 0.20), (4.41, 0.30)],
        belt=[(1.14, 0.88), (3.50, 0.96)],
        glass=(1.16, 1.78, 2.98, 3.50), windows=[(1.26, 2.20), (2.30, 3.04), (3.10, 3.40)], doors=[(1.16, 2.25), (2.25, 3.10)],
        front=[("H", 0.52, 0.70, 0.34, 0.12), ("A", 0.74, 0.70, 0.06, 0.10), ("R", 0, 0.70, 0.40, 0.08), ("B", 0, 0.38, 1.70, 0.22)],
        rear=[("T", 0.55, 0.80, 0.34, 0.14), ("B", 0, 0.38, 1.70, 0.22), ("L", 0, 0.62, 0.34, 0.10)],
        sides=[("B", 0.52, 0.05)], engine="front"),
    "peugeot406break": dict(
        name="Peugeot 406 Break", L=4.74, W=1.76, wb=2.70, fo=0.88, nf=4.5, nr=7.0, roof=0.85, shoulder=0.97,
        top=[(0, 0.54), (0.08, 0.70), (0.40, 0.80), (1.24, 0.92), (1.94, 1.43), (2.08, 1.48), (4.30, 1.51),
             (4.60, 1.34), (4.72, 1.02), (4.74, 0.60)],
        bottom=[(0, 0.25), (0.60, 0.18), (4.30, 0.20), (4.74, 0.30)],
        belt=[(1.24, 0.92), (4.40, 0.99)],
        glass=(1.26, 1.92, 4.28, 4.64), windows=[(1.36, 2.36), (2.46, 3.24), (3.34, 4.34)], doors=[(1.26, 2.40), (2.40, 3.30)],
        front=[("H", 0.55, 0.72, 0.34, 0.12), ("R", 0, 0.66, 0.46, 0.08), ("B", 0, 0.40, 1.76, 0.22)],
        rear=[("T", 0.72, 0.98, 0.10, 0.26), ("B", 0, 0.40, 1.76, 0.20), ("L", 0, 0.66, 0.34, 0.10)],
        sides=[("B", 0.38, 0.06)], engine="front", roofRails=True),
    "fiat_multipla": dict(
        name="Fiat Multipla", L=3.99, W=1.87, wb=2.67, fo=0.64, nf=4.0, nr=6.0, roof=0.90, shoulder=0.97,
        top=[(0, 0.52), (0.08, 0.66), (0.30, 0.76), (0.66, 0.80), (0.72, 0.98), (0.98, 1.02), (1.56, 1.62), (1.70, 1.67),
             (3.66, 1.67), (3.86, 1.55), (3.96, 1.10), (3.99, 0.60)],
        bottom=[(0, 0.26), (0.50, 0.20), (3.60, 0.22), (3.99, 0.30)],
        belt=[(0.98, 0.98), (3.80, 1.00)],
        glass=(1.00, 1.54, 3.64, 3.92), windows=[(1.10, 2.00), (2.10, 2.94), (3.02, 3.74)], doors=[(1.00, 2.05), (2.05, 3.00)],
        front=[("H", 0.62, 0.68, 0.22, 0.12), ("H", 0.50, 0.94, 0.22, 0.06), ("R", 0, 0.54, 0.70, 0.14), ("B", 0, 0.34, 1.86, 0.14)],
        rear=[("T", 0.80, 1.14, 0.08, 0.44), ("B", 0, 0.40, 1.86, 0.20), ("L", 0, 0.70, 0.34, 0.10)],
        sides=[("B", 0.40, 0.12)], engine="front"),
}


def lerp_poly(pts, s):
    if s <= pts[0][0]:
        return pts[0][1]
    for (s0, h0), (s1, h1) in zip(pts, pts[1:]):
        if s0 <= s <= s1:
            return h0 if s1 == s0 else h0 + (h1 - h0) * (s - s0) / (s1 - s0)
    return pts[-1][1]


class Car:
    def __init__(self, cid, spec):
        self.id, self.c = cid, spec
        self.L, self.W = spec["L"], spec["W"]

    def top(self, s):
        return lerp_poly(self.c["top"], s)

    def bottom(self, s):
        return lerp_poly(self.c["bottom"], s)

    def belt(self, s):
        return min(self.top(s), lerp_poly(self.c["belt"], s))

    def plan(self, s):
        """Half width at station s from a two-sided superellipse (front / rear rounding)."""
        u = (s - self.L / 2) / (self.L / 2)
        n = self.c["nf"] if u < 0 else self.c["nr"]
        a = min(abs(u), 0.992)
        return self.W / 2 * max(0.5, (1 - a ** n) ** (1 / n))

    def half_width(self, s, h):
        top, bot, belt = self.top(s), self.bottom(s), self.belt(s)
        w = self.plan(s)
        tuck = max(0.0, (bot + 0.10 - h) / 0.10)                     # sill tucks under
        w *= 1 - 0.10 * tuck * tuck
        if h > belt and top > belt + 0.02:                            # greenhouse leans in (tumblehome)
            t = (h - belt) / (top - belt)
            w *= self.c["shoulder"] + (self.c["roof"] - self.c["shoulder"]) * t
        else:
            w *= 1 - (1 - self.c["shoulder"]) * max(0.0, min(1.0, (h - (belt - 0.06)) / 0.06))
        r = 0.10                                                      # rounded top edge
        if h > top - r:
            k = min(1.0, (h - (top - r)) / r)
            w *= math.sqrt(max(0.0, 1 - k * k)) * 0.35 + 0.65
        return w


def build_mesh(car):
    """Loft closed cross-sections along the length into a watertight mesh."""
    bm = bmesh.new()
    rings = []
    ns = int(round(car.L / 0.02))
    for i in range(ns + 1):
        s = car.L * i / ns
        top, bot = car.top(s), car.bottom(s)
        hs = [bot + (top - bot) * j / 23 for j in range(24)]
        ws = [max(0.02, car.half_width(s, h)) for h in hs]
        X = car.L / 2 - s
        ring = [bm.verts.new((X, -w, h + LIFT)) for h, w in zip(hs, ws)]
        ring += [bm.verts.new((X, w, h + LIFT)) for h, w in zip(reversed(hs), reversed(ws))]
        rings.append(ring)
    n = len(rings[0])
    for a, b in zip(rings, rings[1:]):
        for j in range(n):
            bm.faces.new((a[j], a[(j + 1) % n], b[(j + 1) % n], b[j]))
    bm.faces.new(rings[0])
    bm.faces.new(list(reversed(rings[-1])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(car.id)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(car.id, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def voxelize(car, ob):
    """Fill voxel centres between the first hit from above and the first from below in every (x, z) column."""
    tree = BVHTree.FromObject(ob, bpy.context.evaluated_depsgraph_get())
    hx, hz = int(math.ceil(car.W / 2 / S)) + 1, int(math.ceil(car.L / 2 / S)) + 1
    vox = set()
    for gz in range(-hz, hz + 1):
        for gx in range(-hx, hx + 1):
            X, Y = gz * S, -gx * S
            down = tree.ray_cast(Vector((X, Y, 4.0)), Vector((0, 0, -1)))
            up = tree.ray_cast(Vector((X, Y, -1.0)), Vector((0, 0, 1)))
            if down[0] is None or up[0] is None:
                continue
            y0 = int(math.ceil(up[0].z / S + Y_GROUND))
            y1 = int(math.floor(down[0].z / S + Y_GROUND))
            for gy in range(y0, y1 + 1):
                vox.add((gx, gy, gz))
    return vox


def classify(car, vox):
    c = car.c
    ws0, ws1, rw1, rw0 = c["glass"]
    out, xmax = {}, {}
    for (x, y, z) in vox:
        xmax[(y, z)] = max(xmax.get((y, z), 0), abs(x))

    def sof(z): return car.L / 2 - z * S
    def hof(y): return (y - Y_GROUND) * S - LIFT
    def empty(x, y, z): return (x, y, z) not in vox

    for (x, y, z) in vox:
        s, h = sof(z), hof(y)
        code = "P"
        surface = any(empty(x + dx, y + dy, z + dz) for dx, dy, dz in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)))
        if surface:
            belt, top = car.belt(s), car.top(s)
            side = abs(x) >= xmax[(y, z)]
            if h > belt + 0.02 and ws0 <= s <= rw0:
                if ws1 <= s <= rw1 and empty(x, y + 1, z):
                    if c.get("roofPanel") and c["roofPanel"][0] <= s <= c["roofPanel"][1] and abs(x) < xmax[(y, z)] - 1:
                        code = "K"                                          # rolled-back canvas roof
                elif s < ws1 or s > rw1:
                    code = "P" if side else "G"                             # windshield / rear window, pillars at the edge
                elif side:
                    ok = any(a <= s <= b for a, b in c["windows"]) and h < top - 0.10
                    code = "G" if ok else "P"
            if c.get("roofRails") and ws1 <= s <= rw1 and empty(x, y + 1, z) and abs(x) >= xmax[(y, z)] - 1 and h > top - 0.05:
                code = "C"
            faces = c["front"] if empty(x, y, z + 1) and s < 0.45 else c["rear"] if empty(x, y, z - 1) and s > car.L - 0.45 else []
            for kind, xc, hc, w, hh in faces:
                xm = abs(x) * S
                if kind == "O":
                    inside = (xm - xc) ** 2 + (h - hc) ** 2 <= (w / 2 + 0.01) ** 2
                else:
                    inside = abs(h - hc) <= hh / 2 + 0.001 and (abs(xm - xc) <= w / 2 + 0.001 if xc > 0 else xm <= w / 2 + 0.001)
                if inside:
                    code = "H" if kind == "O" else ("C" if kind == "R" and c.get("grilleChrome") else kind)
            ends = c["front"] if s < 0.3 else c["rear"] if s > car.L - 0.3 else []
            for kind, xc, hc, w, hh in ends:                              # bumper bands wrap around the corners
                if kind in ("B", "C") and xc == 0 and abs(h - hc) <= hh / 2 and code == "P":
                    code = kind
            if side and code == "P":
                for kind, hc, hh in c["sides"]:
                    if abs(h - hc) <= hh / 2:
                        code = kind
        out[(x, y, z)] = code
    return out


def header(car, vox):
    c = car.c
    def gz(s): return int(round((car.L / 2 - s) / S))
    def gy(h): return int(round((h + LIFT) / S + Y_GROUND))
    half = max(abs(x) for x, _, _ in vox)
    ws0, ws1, rw1, rw0 = c["glass"]
    seat_s = (c["doors"][0][0] + c["doors"][0][1]) / 2 + 0.25
    floor = min(y for x, y, z in vox if x == 0 and z == gz(seat_s))
    lines = {
        "name": c["name"], "halfW": half, "floor": floor, "belt": gy(car.belt(seat_s)), "roof": max(y for _, y, _ in vox),
        "zFront": max(z for _, _, z in vox), "zRear": min(z for _, _, z in vox),
        "wheelZF": gz(c["fo"]), "wheelZR": gz(c["fo"] + c["wb"]), "wheelX": half - 3, "wheelY": 5,
        "windZ": gz(ws0), "roofFZ": gz(ws1), "roofRZ": gz(rw1), "seatZ": gz(seat_s), "engine": c["engine"],
        "doors": " ".join(f"{gz(a)},{gz(b)}" for a, b in c["doors"]),
    }
    return "".join(f"# {k} {v}\n" for k, v in lines.items())


def preview(car, path):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.render.resolution_x, scene.render.resolution_y = 900, 420
    for w in (car.c["fo"], car.c["fo"] + car.c["wb"]):
        for side in (-1, 1):
            bpy.ops.mesh.primitive_cylinder_add(radius=TYRE_R, depth=0.19, location=(car.L / 2 - w, side * (car.W / 2 - 0.12), TYRE_R), rotation=(math.pi / 2, 0, 0))
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = car.L * 1.15
    for tag, loc, rot in (("side", (0, -8, 0.75), (math.pi / 2, 0, 0)), ("34", (6.5, -6.5, 3.6), (math.radians(66), 0, math.radians(45)))):
        cam.location, cam.rotation_euler = loc, rot
        scene.render.filepath = f"{path}_{tag}.png"
        bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = argv[0] if argv else "."
    prev = argv[1] if len(argv) > 1 and argv[1] != "-" else None
    ids = argv[2:] or list(CARS)
    os.makedirs(out, exist_ok=True)
    for cid in ids:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        car = Car(cid, CARS[cid])
        ob = build_mesh(car)
        vox = voxelize(car, ob)
        codes = classify(car, vox)
        with open(os.path.join(out, cid + ".txt"), "w") as fh:
            fh.write(header(car, vox))
            for (x, y, z), code in sorted(codes.items()):
                fh.write(f"{x} {y} {z} {code}\n")
        print(f"[cars] {cid}: {len(vox)} voxels", flush=True)
        if prev:
            os.makedirs(prev, exist_ok=True)
            preview(car, os.path.join(prev, cid))


main()
