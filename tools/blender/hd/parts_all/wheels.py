"""HD wheels: every wheel part (PartLibrary wheels, bikes, farm, aircraft). Origin = inner face (x = 0) at the axle
centre, tyre extends toward game +X (outward on a right socket; left sockets mirror).

blender -b -P tools/blender/hd/parts_all/wheels.py [-- ids]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pa  # noqa: E402
from pa import M, Part, Vector, kit  # noqa: E402

F = "wheels"


def inner(ob, w):
    """car_wheel / moto_wheel are centred on x = 0: shift so the inner face sits on the origin."""
    ob.data.transform(kit.Matrix.Translation((w / 2, 0, 0)))
    return ob


def game_width(ob, key):
    """Scale across the axle (about the inner face) so the overall width equals the game wheel's."""
    g = pa.GAME[key]["bounds"]
    want = g[1][0] - g[0][0]
    xs = [v.co.x for v in ob.data.vertices]
    lo, hi = min(xs), max(xs)
    ob.data.transform(kit.Matrix.Translation((-lo, 0, 0)))
    ob.data.transform(kit.Matrix.Diagonal((want / (hi - lo), 1, 1, 1)))
    return ob


def car(key, R, rim_r, w, tread, rim, rim_mat, **kw):
    def fn():
        return game_width(inner(kit.car_wheel(key, M, R, rim_r, w, tread, rim, rim_mat=M[rim_mat] if isinstance(rim_mat, str) else rim_mat, segs=kw.get("segs", 40)), w), key)
    pa.add(key, fn, F, fit_axes=(1, 2), category="Wheel", radius=R, width=w, tread=tread, socket="wheel_*", tile=kw.get("tile"))


def moto(key, R, w, knobby, chrome, whitewall=False, disc=True, spokes=32, tile=None):
    def fn():
        return game_width(inner(kit.moto_wheel(key, M, R, w, (0, 0, 0), knobby=knobby, chrome=chrome, whitewall=whitewall, disc=disc, spokes=spokes), w), key)
    pa.add(key, fn, F, fit_axes=(1, 2), category="Wheel", radius=R, width=w, socket="wheel_* (centre line)", tile=tile)


def compact():
    """wheel_compact: everyday 0.31 m hatchback wheel, steel rim with a plastic trim and four studs."""
    R, w, rim_r = 0.28, 0.2, 0.19
    ob = kit.car_wheel("wheel_compact", M, R, rim_r, w, "small", "steel", rim_mat=M["steel"], segs=36)
    p = Part("trim")
    sw = w / 2
    p.lathe(M["plastic"], [(rim_r - 0.005, sw * 0.6), (rim_r * 0.8, sw * 0.86), (rim_r * 0.35, sw * 0.95), (0.0, sw * 0.97)], (0, 0, 0), "X", 36)
    for i in range(8):
        a = i / 8 * math.tau
        p.add(kit.bm_box(0.012, rim_r * 0.18, rim_r * 0.42, bevel=0.003), M["black"], (sw * 0.93, math.sin(a) * rim_r * 0.58, math.cos(a) * rim_r * 0.58), (-a, 0, 0))
    for i in range(4):
        a = i / 4 * math.tau + 0.4
        p.cyl(M["chrome"], 0.01, 0.012, (sw * 0.98, math.sin(a) * 0.05, math.cos(a) * 0.05), "X", 6, bevel=0.002)
    t = p.build(ob)
    inner(t, w)
    inner(ob, w)
    pa._join(ob, [t])
    return game_width(ob, "wheel_compact")


def track_roller():
    """wheel_track: crawler road wheel with a run of grouser shoes (hidden in the tracks at runtime)."""
    R, w = 0.34, 0.46
    p = Part("wheel_track")
    p.lathe(M["iron"], [(0.06, -w / 2), (R * 0.82, -w / 2), (R, -w * 0.42), (R, -w * 0.12), (R * 0.85, -w * 0.08), (R * 0.85, w * 0.08), (R, w * 0.12), (R, w * 0.42),
                        (R * 0.82, w / 2), (0.06, w / 2)], (0, 0, 0), "X", 36)
    p.cyl(M["steel"], 0.07, w + 0.04, (0, 0, 0), "X", 16, bevel=0.01)
    for s in (-1, 1):
        for i in range(8):
            a = i / 8 * math.tau
            p.cyl(M["chrome"], 0.012, 0.02, (s * (w / 2 + 0.006), math.cos(a) * 0.12, math.sin(a) * 0.12), "X", 6)
        for i in range(6):
            a = i / 6 * math.tau + 0.3
            p.cyl(M["steel_dark"], 0.04, 0.02, (s * (w / 2 - 0.004), math.cos(a) * 0.22, math.sin(a) * 0.22), "X", 12, bevel=0.003)
    return game_width(inner(p.build(), w), "wheel_track")


def tractor(key, R, rim_r, w, front):
    """Lugged tractor tyre: rear with chevron bars, front with ribs; ochre rim with a dished centre."""
    def fn():
        p = Part(key)
        sw = w / 2
        core = R - (0.05 if not front else 0.025)
        bul = (core - rim_r) * 0.12
        prof = [(rim_r - 0.005, -sw * 0.86), (rim_r + 0.01, -sw * 0.98), ((rim_r + core) * 0.5, -sw - bul), (core - 0.03, -sw * 0.92), (core, -sw * 0.7),
                (core, sw * 0.7), (core - 0.03, sw * 0.92), ((rim_r + core) * 0.5, sw + bul), (rim_r + 0.01, sw * 0.98), (rim_r - 0.005, sw * 0.86)]
        p.lathe(M["rubber"], prof, (0, 0, 0), "X", 44)
        lug_h = R - core
        if not front:
            n = 22
            for i in range(n):
                a = i / n * math.tau
                for s in (-1, 1):
                    aa = a + (0.5 / n * math.tau if s > 0 else 0)
                    # bar runs from the centre line out to the shoulder at an angle (chevron)
                    p.add(kit.bm_box(sw * 1.05, 0.07, lug_h * 2, bevel=0.012, segs=1), M["tread"],
                          (s * sw * 0.5, math.sin(aa) * core, math.cos(aa) * core), (-aa, 0, s * 0.55))
        else:
            for x in (-0.6, -0.2, 0.2, 0.6):
                p.lathe(M["tread"], [(core, x * sw - 0.025), (core + lug_h, x * sw - 0.018), (core + lug_h, x * sw + 0.018), (core, x * sw + 0.025)], (0, 0, 0), "X", 44)
        rim = M["ochre"]
        p.lathe(rim, [(rim_r - 0.004, -sw * 0.86), (rim_r + 0.012, -sw * 0.8), (rim_r - 0.015, -sw * 0.7), (rim_r - 0.015, sw * 0.6), (rim_r + 0.012, sw * 0.8),
                      (rim_r - 0.004, sw * 0.86), (rim_r - 0.02, sw * 0.82)], (0, 0, 0), "X", 44)
        hub = rim_r * 0.35
        p.lathe(rim, [(rim_r - 0.02, sw * 0.7), (rim_r * 0.75, sw * 0.1), (hub, 0.0), (hub * 0.8, sw * 0.2), (0, sw * 0.2)], (0, 0, 0), "X", 36)
        for i in range(8):
            a = i / 8 * math.tau
            p.cyl(M["black"], rim_r * 0.08, 0.03, (sw * 0.3, math.sin(a) * rim_r * 0.55, math.cos(a) * rim_r * 0.55), "X", 12)
        for i in range(8 if not front else 6):
            a = i / (8 if not front else 6) * math.tau
            p.cyl(M["chrome"], 0.013, 0.03, (sw * 0.22, math.sin(a) * hub * 0.75, math.cos(a) * hub * 0.75), "X", 6, bevel=0.002)
        p.lathe(M["steel"], [(hub * 0.6, sw * 0.2), (hub * 0.55, sw * 0.42), (0, sw * 0.44)], (0, 0, 0), "X", 20)
        return game_width(inner(p.build(), w), key)
    pa.add(key, fn, F, fit_axes=(1, 2), category="Wheel", radius=R, width=w, tread="tractor_" + ("front" if front else "rear"), socket="wheel_*", tile="wheels_farm")


def monster():
    R, w, rim_r = 0.84, 0.72, 0.42
    ob = kit.car_wheel("wheel_monster", M, R, rim_r, w, "mud", "beadlock", rim_mat=M["chrome"], segs=48)
    return game_width(inner(ob, w), "wheel_monster")


def aero():
    """wheel_aero: light aircraft main wheel (smooth, low profile) on a split hub."""
    R, w, rim_r = 0.28, 0.14, 0.1
    p = Part("wheel_aero")
    sw = w / 2
    prof = [(rim_r, -sw * 0.8), ((rim_r + R) * 0.5, -sw * 1.08), (R - 0.03, -sw * 0.92), (R, -sw * 0.4), (R, sw * 0.4), (R - 0.03, sw * 0.92), ((rim_r + R) * 0.5, sw * 1.08), (rim_r, sw * 0.8)]
    p.lathe(M["rubber"], prof, (0, 0, 0), "X", 40)
    for x in (-0.15, 0.15):
        p.lathe(M["tread"], [(R, x * w - 0.008), (R + 0.003, x * w - 0.004), (R + 0.003, x * w + 0.004), (R, x * w + 0.008)], (0, 0, 0), "X", 40)
    p.lathe(M["alu"], [(rim_r, -sw * 0.8), (rim_r * 0.9, -sw * 0.5), (0.03, -sw * 0.45), (0.02, -sw * 0.6), (0, -sw * 0.6)], (0, 0, 0), "X", 28)
    p.lathe(M["crimson"], [(rim_r, sw * 0.8), (rim_r * 0.9, sw * 0.5), (0.03, sw * 0.45), (0.02, sw * 0.65), (0, sw * 0.65)], (0, 0, 0), "X", 28)
    for i in range(6):
        a = i / 6 * math.tau
        p.cyl(M["chrome"], 0.007, 0.012, (sw * 0.55, math.sin(a) * rim_r * 0.6, math.cos(a) * rim_r * 0.6), "X", 6)
    p.cyl(M["steel"], 0.022, w + 0.02, (0, 0, 0), "X", 12, bevel=0.003)
    return game_width(inner(p.build(), w), "wheel_aero")


car("wheel_street", 0.432, 0.27, 0.32, "street", "steel", "brass", tile="wheels_road")
car("wheel_sport", 0.44, 0.34, 0.4, "sport", "5spoke", "chrome", tile="wheels_road")
car("wheel_rain", 0.432, 0.27, 0.32, "rain", "mag", "chrome", tile="wheels_road")
car("wheel_small", 0.36, 0.2, 0.24, "small", "steel", "cream", tile="wheels_road")
pa.add("wheel_compact", compact, F, fit_axes=(1, 2), category="Wheel", radius=0.28, width=0.24, tread="street", socket="wheel_*", tile="wheels_road")
car("wheel_offroad", 0.52, 0.29, 0.4, "offroad", "beadlock", "black", tile="wheels_offroad")
car("wheel_mud", 0.52, 0.296, 0.48, "mud", "steel", "olive", tile="wheels_offroad")
car("wheel_truck", 0.52, 0.3, 0.4, "truck", "truck", "steel", tile="wheels_offroad")
pa.add("wheel_track", track_roller, F, fit_axes=(1, 2), category="Wheel", radius=0.344, width=0.48, tread="track", socket="wheel_* (crawler)", tile="wheels_offroad")
pa.add("wheel_monster", monster, F, fit_axes=(1, 2), category="Wheel", radius=0.84, width=0.72, tread="mud", socket="wheel_*", tile="wheels_farm")
tractor("wheel_tractor", 0.76, 0.42, 0.48, False)
tractor("wheel_tractor_front", 0.44, 0.24, 0.32, True)
moto("wheel_bike", 0.368, 0.12, True, False, tile="wheels_bike")
moto("wheel_bike_street", 0.336, 0.2, False, True, whitewall=True, tile="wheels_bike")
moto("wheel_bicycle", 0.352, 0.04, False, False, disc=False, spokes=36, tile="wheels_bike")
pa.add("wheel_aero", aero, F, fit_axes=(1, 2), category="Wheel", radius=0.28, width=0.16, tread="aero", socket="wheel_*", tile="wheels_bike")

if __name__ == "__main__":
    pa.run(F, per_tile=6)
