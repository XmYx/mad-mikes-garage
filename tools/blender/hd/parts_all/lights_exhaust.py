"""HD lights (bar, LED bar, fog pods, emergency bar, searchlight + search pod with segment "lamp"), exhausts and
radiators. Origins = sockets; exhausts are authored for the right side (game +X out), outlets toward the rear.

blender -b -P tools/blender/hd/parts_all/lights_exhaust.py [-- ids]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pa  # noqa: E402
from pa import M, Part, Vector, Matrix, V, G, kit, bm_box  # noqa: E402
import comps  # noqa: E402
import parts as mp  # noqa: E402

mp.M = M
F = "lights_exhaust"


def led_bar():
    """lights_led_bar: slim black extrusion, four wide LED floods, heat-sink fins on the back, two feet."""
    p = Part("lights_led_bar")
    L = 1.96
    for x in (-0.8, 0.8):
        p.box(M["steel_dark"], (0.05, 0.08, 0.2), (x, 0.0, 0.1), bevel=0.008)
        pa.bolts(p, M["chrome"], [(x + 0.03, 0.0, 0.22)], 0.012, 0.03, "X")
    p.box(M["plastic"], (L, 0.1, 0.14), (0, 0.0, 0.3), bevel=0.02)
    for k in range(14):                                                 # fins on the top/back
        p.box(M["black"], (0.012, 0.09, 0.03), (-L / 2 + 0.07 + k * (L - 0.14) / 13, 0.01, 0.385), bevel=0.002)
    for i in range(4):
        x = -0.72 + i * 0.48
        p.box(M["chrome"], (0.42, 0.012, 0.11), (x, -0.052, 0.3), bevel=0.004)
        for k in range(5):
            p.cyl(M["lamp"], 0.022, 0.006, (x - 0.16 + k * 0.08, -0.06, 0.3), "Y", 12, bevel=0)
    for s in (-1, 1):
        p.box(M["plastic"], (0.03, 0.12, 0.16), (s * L / 2, 0, 0.3), bevel=0.008)
    return p.build()


def emergency():
    """lights_emergency: chrome base with a red and a blue dome, white take-down lamp in the middle."""
    p = Part("lights_emergency")
    for x in (-0.56, 0.56):
        p.box(M["steel_dark"], (0.05, 0.1, 0.14), (x, 0, 0.07), bevel=0.008)
    p.box(M["chrome"], (1.68, 0.24, 0.06), (0, 0, 0.17), bevel=0.02)
    for s, lens in ((-1, M["lamp_red"]), (1, M["lamp_blue"])):
        cx = s * 0.48
        p.add(kit.superloft([(y, cx, 0.24, 0.32, 0.06) for y in (-0.1, 0.1)], 20, 3.0), lens)
        p.add(kit.superloft([(y, cx, 0.24, 0.335, 0.07) for y in (-0.105, -0.095)], 20, 3.0), M["chrome"])
        p.add(kit.superloft([(y, cx, 0.24, 0.335, 0.07) for y in (0.095, 0.105)], 20, 3.0), M["chrome"])
        for k in range(3):
            p.cyl(M["chrome"], 0.03, 0.12, (cx - 0.15 + k * 0.15, 0, 0.24), "Y", 12, bevel=0.004)  # reflectors inside
    p.box(M["plastic"], (0.22, 0.2, 0.1), (0, 0, 0.24), bevel=0.015)
    p.box(M["lamp"], (0.14, 0.01, 0.05), (0, -0.101, 0.24), bevel=0.003)
    p.cyl(M["black"], 0.06, 0.06, (0, 0.06, 0.32), "Z", 12, bevel=0.01)                         # siren speaker
    return p.build()


def searchlight():
    """lights_search: turntable base; segment "lamp" (yoke + olive drum, big lens, handle) yaws at 0.16 m."""
    p = Part("lights_search")
    p.lathe(M["steel_dark"], [(0.0, 0.0), (0.18, 0.0), (0.18, 0.06), (0.14, 0.12), (0.0, 0.12)], (0, 0, 0), "Z", 32)
    pa.bolts(p, M["chrome"], [(math.cos(a) * 0.15, math.sin(a) * 0.15, 0.065) for a in [k / 6 * math.tau for k in range(6)]], 0.01, 0.012)
    base = p.build()
    l = Part("lamp")
    l.cyl(M["steel"], 0.05, 0.06, (0, 0, 0.19), "Z", 16, bevel=0.006)
    for s in (-1, 1):
        l.box(M["steel"], (0.025, 0.06, 0.26), (s * 0.23, 0, 0.32), bevel=0.006)
    l.box(M["steel"], (0.48, 0.06, 0.03), (0, 0, 0.2), bevel=0.006)
    c = Vector((0, 0.04, 0.4))
    l.lathe(M["olive"], [(0.0, -0.22), (0.12, -0.22), (0.18, -0.16), (0.21, -0.02), (0.21, 0.24), (0.0, 0.24)], c, "Y", 32)
    l.lathe(M["chrome"], [(0.17, 0.0), (0.225, 0.0), (0.225, 0.03), (0.17, 0.03)], c - Vector((0, 0.255, 0)), "Y", 32)
    pa.lens(l, M["lamp"], c - Vector((0, 0.24, 0)), 0.18, "-Y", 0.02, 32)
    for s in (-1, 1):
        l.cyl(M["chrome"], 0.03, 0.03, c + Vector((s * 0.22, 0, 0)), "X", 12, bevel=0.005)
    l.tube(M["black"], [c + Vector((-0.1, 0.0, 0.2)), c + Vector((-0.1, 0.04, 0.3)), c + Vector((0.1, 0.04, 0.3)), c + Vector((0.1, 0.0, 0.2))], 0.016, 8)
    l.tube(M["black"], [c + Vector((0, 0.24, 0)), c + Vector((0, 0.32, -0.1)), (0, 0.1, 0.16)], 0.01, 6)
    lo = l.build(base)
    pa.seg(lo, "lamp", G(0, 0.16, 0))
    return base


def search_pod():
    """lights_search_pod: turntable; segment "lamp": rectangular green pod, cooling fins, chrome bezel, carry handle."""
    p = Part("lights_search_pod")
    p.lathe(M["steel_dark"], [(0.0, 0.0), (0.19, 0.0), (0.19, 0.05), (0.16, 0.12), (0.0, 0.12)], (0, 0, 0), "Z", 32)
    base = p.build()
    l = Part("lamp")
    l.box(M["steel"], (0.16, 0.16, 0.06), (0, 0, 0.18), bevel=0.01)
    c = Vector((0, 0.08, 0.44))
    l.box(M["olive"], (0.52, 0.5, 0.46), c, bevel=0.04)
    for k in range(7):
        l.box(M["black"], (0.48, 0.02, 0.42), c + Vector((0, 0.26 + k * 0.0, 0)) + Vector((0, 0.0, 0)), bevel=0.003) if k == 0 else None
        l.box(M["black"], (0.02, 0.06, 0.42), c + Vector((-0.21 + k * 0.07, 0.27, 0)), bevel=0.003)
    l.box(M["chrome"], (0.54, 0.03, 0.48), c - Vector((0, 0.26, 0)), bevel=0.012)
    l.box(M["lamp"], (0.42, 0.012, 0.36), c - Vector((0, 0.28, 0)), bevel=0.01)
    for k in range(3):
        l.box(M["cream"], (0.42, 0.004, 0.006), c - Vector((0, 0.287, -0.1 + k * 0.1)), bevel=0)
    l.tube(M["black"], [c + Vector((-0.16, 0.0, 0.23)), c + Vector((-0.16, 0.0, 0.36)), c + Vector((0.16, 0.0, 0.36)), c + Vector((0.16, 0.0, 0.23))], 0.018, 8)
    lo = l.build(base)
    pa.seg(lo, "lamp", G(0, 0.16, 0))
    return base


def twin_chrome():
    """exhaust_twin_chrome: header flange, down pipe, black muffler can with chrome caps, twin tailpipes, rolled tips."""
    p = Part("exhaust_twin_chrome")
    p.box(M["rust"], (0.1, 0.1, 0.12), (-0.03, 0.0, 0.06), bevel=0.01)
    p.tube(M["steel"], [(0.0, 0.0, 0.08), (0.06, 0.06, 0.08), (0.1, 0.16, 0.08)], 0.04, 12)
    c = Vector((0.12, 0.44, 0.08))
    p.lathe(M["black"], [(0.0, -0.28), (0.15, -0.28), (0.16, -0.26), (0.16, 0.26), (0.15, 0.28), (0.0, 0.28)], c, "Y", 28)
    for s in (-1, 1):
        p.lathe(M["chrome"], [(0.0, 0.0), (0.162, 0.0), (0.162, 0.02), (0.0, 0.02)], c + Vector((0, s * 0.28 - (0.02 if s > 0 else 0), 0)), "Y", 28)
        pa.ring(p, M["steel"], c + Vector((0, s * 0.15, 0)), 0.163, 0.008, "Y", 28, 5)
    p.box(M["steel_dark"], (0.02, 0.06, 0.18), (0.02, 0.44, 0.24), bevel=0.004)          # hanger
    for x in (0.04, 0.24):
        p.tube(M["chrome"], [(x, 0.72, 0.08), (x, 1.24, 0.08)], 0.04, 16, caps=False)
        p.lathe(M["chrome"], [(0.04, 0.0), (0.06, 0.02), (0.065, 0.08), (0.052, 0.1), (0.045, 0.1)], (x, 1.24, 0.08), "Y", 20)
        p.cyl(M["black"], 0.04, 0.005, (x, 1.33, 0.08), "Y", 16, bevel=0)
    return p.build()


def flame_stack():
    """exhaust_flame_stack: flange, pipe out of the body and up, raked back, heat-blued, perforated shield, flared bell."""
    p = Part("exhaust_flame_stack")
    p.box(M["rust"], (0.08, 0.14, 0.12), (-0.02, 0, 0.06), bevel=0.01)
    pts = [(0.0, 0, 0.04), (0.1, 0, 0.1), (0.16, 0, 0.2), (0.16, 0.04, 0.5), (0.16, 0.12, 0.84)]
    p.tube(M["steel"], pts[:4], 0.07, 16)
    p.tube(M["blued"], pts[3:], 0.07, 16)
    shield = kit.bm_cyl(0.13, 0.3, 24, 0.0, caps=False)
    p.add(shield, M["chrome"], (0.16, 0.03, 0.36))
    for k in range(5):
        for j in range(12):
            a = j / 12 * math.tau + k * 0.26
            p.cyl(M["black"], 0.012, 0.01, (0.16 + math.cos(a) * 0.131, 0.03 + math.sin(a) * 0.131, 0.25 + k * 0.055), "X", 6, bevel=0, rot=(0, 0, a))
    p.lathe(M["blued"], [(0.07, 0.0), (0.1, 0.1), (0.15, 0.2), (0.155, 0.22), (0.12, 0.22)], (0.16, 0.12, 0.84), "Z", 28)
    pa.ring(p, M["black"], (0.16, 0.12, 1.06), 0.14, 0.012, "Z", 28, 6)
    p.box(M["steel_dark"], (0.1, 0.03, 0.03), (0.06, 0.0, 0.4), bevel=0.004)          # stay
    return p.build()


def oil_cooler():
    """radiator_oil_cooler: stock radiator core with header tank, oil cooler below on brackets, braided oil lines."""
    p = Part("radiator_oil_cooler")
    w, hh = 1.2, 0.48
    p.box(M["fins"], (w, 0.05, hh), (0, 0, hh / 2 + 0.0), bevel=0.004)
    p.box(M["chrome"], (w + 0.02, 0.08, 0.08), (0, 0, hh + 0.04), bevel=0.015)
    for s in (-1, 1):
        p.box(M["steel_dark"], (0.06, 0.08, hh + 0.12), (s * (w / 2 + 0.03), 0, hh / 2 + 0.02), bevel=0.01)
    p.cyl(M["chrome"], 0.03, 0.03, (-0.16, 0, hh + 0.1), "Z", 14, bevel=0.005)
    oc = Vector((0, -0.08, -0.18))
    p.box(M["black"], (0.84, 0.05, 0.16), oc, bevel=0.01)
    for k in range(20):
        p.box(M["chrome"], (0.008, 0.052, 0.13), oc + Vector((-0.4 + k * 0.042, 0, 0)), bevel=0)
    for s in (-1, 1):
        p.box(M["steel_dark"], (0.04, 0.1, 0.22), (s * 0.48, -0.04, -0.12), bevel=0.006)
        p.tube(M["steel"], [oc + Vector((s * 0.42, 0.03, 0.02)), oc + Vector((s * 0.58, 0.1, 0.1)), (s * 0.64, 0.3, 0.06)], 0.02, 8)
        for k in range(2):
            p.cyl(M["brass"], 0.026, 0.03, oc + Vector((s * 0.44, 0.03, 0.02)) if k == 0 else Vector((s * 0.64, 0.3, 0.06)), "Y", 10, bevel=0.003)
    p.tube(M["black"], [(-0.24, 0.03, hh - 0.04), (-0.24, 0.16, hh - 0.04), (-0.24, 0.28, hh - 0.1)], 0.028, 12)
    p.tube(M["black"], [(0.24, 0.03, 0.02), (0.24, 0.16, 0.02), (0.24, 0.28, 0.06)], 0.028, 12)
    return p.build()


S = dict(azim=-45)
pa.add("lights_bar", mp.light_bar, F, category="Lights", socket="lights", tile="lights_1")
pa.add("lights_led_bar", led_bar, F, category="Lights", socket="lights", tile="lights_1")
pa.add("lights_fog", mp.fog_pods, F, category="Lights", socket="lights", tile="lights_1")
pa.add("lights_emergency", emergency, F, category="Lights", socket="lights", tile="lights_1")
pa.add("lights_search", searchlight, F, category="Lights", socket="lights", tile="lights_2")
pa.add("lights_search_pod", search_pod, F, category="Lights", socket="lights", tile="lights_2")
pa.add("exhaust_stack", mp.exhaust_stack, F, category="Exhaust", socket="exhaust (right; exhaust_L mirrors)", tile="exhausts", **S)
pa.add("exhaust_side_pipes", mp.side_pipes, F, category="Exhaust", socket="exhaust (right; exhaust_L mirrors)", tile="exhausts", **S)
pa.add("exhaust_twin_chrome", twin_chrome, F, category="Exhaust", socket="exhaust (right; exhaust_L mirrors)", tile="exhausts", **S)
pa.add("exhaust_flame_stack", flame_stack, F, category="Exhaust", socket="exhaust (right; exhaust_L mirrors)", tile="exhausts", **S)
pa.add("radiator_car", lambda: mp.radiator("radiator_car", 7, 5), F, category="Radiator", socket="radiator", tile="radiators")
pa.add("radiator_truck", lambda: mp.radiator("radiator_truck", 8, 9), F, category="Radiator", socket="radiator", tile="radiators")
pa.add("radiator_bigcore", lambda: mp.radiator("radiator_bigcore", 8, 8, True), F, category="Radiator", socket="radiator", tile="radiators")
pa.add("radiator_oil_cooler", oil_cooler, F, category="Radiator", socket="radiator", tile="radiators")

if __name__ == "__main__":
    pa.run(F)
