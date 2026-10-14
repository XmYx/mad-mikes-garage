"""HD running gear of the moving bases (PartLibrary.Rig.cs, 2026-10-13): the earthmover tyre (wheel_heavy, R 1.1 m) and
the mining haul tyre (wheel_giant, R 1.6 m) with chevron bars and painted multi-piece rims, the crawler sprockets
(wheel_track_wide R 0.45 m, wheel_track_heavy R 0.9 m: toothed drive ring between guide flanges, spoked web) and the
city tread drum (wheel_track_giant, R 2.0 m: box spokes, cast shoe pads, chain cog on the inboard face). Origin = the
inner face (x = 0) on the axle, the part extends to game +X like every wheel.

blender -b -P tools/blender/hd/parts_all/rig_wheels.py [-- ids]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pa  # noqa: E402
from pa import M, Part, kit  # noqa: E402

F = "rig_wheels"
VS = 0.08


def _paint(key, hexes):
    if key not in M:
        M[key] = kit.surface(key.title().replace("_", ""), kit._hex(hexes), chips=0.45, chip_col=kit.P("metal", 2), rust=0.35, dust=0.45, rough=0.5, streaks=0.4)
    return M[key]


def haul_tyre(key, R_vox, rim_vox, wx, bars, paint, mass, size):
    """PartLibrary.HaulTyre: chevron bars alternating across the tread, a sidewall ring and lettering dashes, a painted
    multi-piece rim with a chrome lock ring, deep dish, hub cap, bolt circle and valve guard."""
    def fn():
        R, rim, w = R_vox * VS, rim_vox * VS, (wx + 1) * VS
        lug = max(1.6, R_vox * 0.11) * VS
        core = R - lug
        p = Part(key)
        pm = _paint(paint[0], paint[1])
        bul = (core - rim) * 0.1
        prof = [(rim - 0.01, 0.02), (rim + 0.02, 0.0), ((rim + core) * 0.5, -bul * 0.3), (core - 0.06, 0.03), (core, 0.1),
                (core, w - 0.1), (core - 0.06, w - 0.03), ((rim + core) * 0.5, w + bul * 0.3), (rim + 0.02, w), (rim - 0.01, w - 0.02)]
        p.lathe(M["rubber"], prof, (0, 0, 0), "X", 56)
        for s, x in ((0, 0.0), (1, w)):                                   # sidewall ring + lettering dashes
            sx = -0.004 if s == 0 else 0.004
            ring_r = rim + (core - rim) * 0.42
            p.add(kit.bm_torus(ring_r, 0.012, 56, 4), M["tread"], (x + sx * 2, 0, 0), (0, math.pi / 2, 0))
            for i in range(48):
                if i % 2 or (i // 8) % 2:
                    continue
                an = i / 48 * math.tau
                rr = rim + (core - rim) * 0.68
                p.box(M["tread"], (0.01, 0.05, 0.022), (x + sx, math.sin(an) * rr, math.cos(an) * rr), (-an, 0, 0), bevel=0.003)
        hw = w / 2
        for i in range(bars):                                             # chevron bars: each half slants the other way
            a0 = i / bars * math.tau
            for side in (-1, 1):
                a = a0 + (0.5 / bars * math.tau if side > 0 else 0)
                cx = hw + side * hw * 0.5
                mt = kit.Matrix.Translation((cx, math.sin(a) * core, math.cos(a) * core)) @ kit.Matrix.Rotation(-a, 4, "X") @ kit.Matrix.Rotation(side * 0.45, 4, "Z")
                p.add(kit.bm_box(hw * 1.02, 0.42 / bars * math.tau * R, lug * 2.0, bevel=0.015, segs=1), M["tread"], matrix=mt)
        # rim: inner flange, barrel, outer lock ring (chrome), deep dish with the hub
        p.lathe(pm, [(rim - 0.005, 0.03), (rim + 0.03, 0.035), (rim - 0.03, 0.08), (rim - 0.04, w - 0.12), (rim - 0.005, w - 0.06)], (0, 0, 0), "X", 48)
        p.lathe(M["chrome"], [(rim - 0.02, w - 0.08), (rim + 0.035, w - 0.06), (rim + 0.03, w - 0.01), (rim - 0.03, w - 0.02)], (0, 0, 0), "X", 48)
        dish = w - 2.5 * VS
        p.lathe(pm, [(rim - 0.04, w - 0.12), (rim * 0.8, dish), (rim * 0.4, dish - 0.02), (rim * 0.38, dish), (0.0, dish)], (0, 0, 0), "X", 40)
        p.lathe(M["steel_dark"], [(rim * 0.38, dish), (rim * 0.36, dish + 0.06), (rim * 0.2, dish + 0.06), (0.0, dish + 0.07)], (0, 0, 0), "X", 32)
        p.lathe(M["chrome"], [(rim * 0.19, dish + 0.06), (rim * 0.17, dish + 0.14), (rim * 0.1, dish + 0.17), (0.0, dish + 0.18)], (0, 0, 0), "X", 24)   # hub cap
        bolts = 18 if R_vox > 15 else 12
        for i in range(bolts):
            an = i / bolts * math.tau
            p.cyl(M["chrome"], 0.022, 0.05, (dish + 0.02, math.sin(an) * rim * 0.5, math.cos(an) * rim * 0.5), "X", 6, bevel=0.004)
        for i in range(8):                                                # dish ventilation holes
            an = (i + 0.5) / 8 * math.tau
            p.cyl(M["black"], rim * 0.1, 0.012, (dish - 0.004, math.sin(an) * rim * 0.65, math.cos(an) * rim * 0.65), "X", 12, bevel=0.0)
        p.box(M["chrome"], (0.07, 0.07, 0.14), (w - 0.06, rim - 0.12, 0.04), bevel=0.01)   # valve guard
        p.cyl(M["steel"], 0.12, 0.1, (0.05, 0, 0), "X", 20, bevel=0.01)       # inner hub stub
        return p.build()
    pa.add(key, fn, F, fit_axes=(1, 2), category="Wheel", radius=round(R_vox * VS, 3), width=round((wx + 1) * VS, 3), tread="haul",
           socket="wheel_* (rig axle mounts)", mass=mass, size_class=size, tile="wheels_rig")


def sprocket(key, R_vox, wx, mass, size):
    """PartLibrary.TrackSprocket: a toothed drive ring between two guide flanges, a six-spoke web, hub and bolt circle."""
    def fn():
        R, w = R_vox * VS, (wx + 1) * VS
        p = Part(key)
        pm = _paint("rig_ochre", "c8961e")
        fl = 2 * VS
        for (x0, x1) in ((0.0, fl), (w - fl, w)):                          # guide flanges
            p.lathe(M["steel"], [(R * 0.4, x0), (R - 0.03, x0), (R, x0 + 0.02), (R, x1 - 0.02), (R - 0.03, x1), (R * 0.4, x1)], (0, 0, 0), "X", 48)
        teeth = max(10, round(R_vox * 2.2))
        root = R - 1.3 * VS
        p.lathe(M["steel_dark"], [(root - 1.0 * VS, fl), (root, fl), (root, w - fl), (root - 1.0 * VS, w - fl)], (0, 0, 0), "X", 48)
        for i in range(teeth):                                            # teeth: worn bright tips
            an = i / teeth * math.tau
            p.add(kit.bm_box(w - 2 * fl, 0.5 / teeth * math.tau * R * 0.9, 1.4 * VS, bevel=0.012, segs=1), M["chrome"],
                  (w / 2, math.sin(an) * (root + 0.6 * VS), math.cos(an) * (root + 0.6 * VS)), (-an, 0, 0))
        for i in range(6):                                                # web spokes
            an = i / 6 * math.tau
            mid = (R * 0.42 + root - 1.6 * VS) / 2
            p.add(kit.bm_box(w - 2 * fl - 0.02, 0.45 * R * 0.5, root - 1.6 * VS - R * 0.42 + 0.06, bevel=0.012, segs=1), pm,
                  (w / 2, math.sin(an) * mid, math.cos(an) * mid), (-an, 0, 0))
        p.lathe(pm, [(R * 0.22, fl), (R * 0.44, fl), (R * 0.44, w - fl), (R * 0.22, w - fl)], (0, 0, 0), "X", 32)   # hub ring
        p.lathe(M["chrome"], [(0.0, 0.0), (R * 0.22, 0.0), (R * 0.22, w), (R * 0.12, w + 0.03), (0.0, w + 0.035)], (0, 0, 0), "X", 24)
        bc = max(6, round(R_vox))
        for i in range(bc):
            an = i / bc * math.tau
            p.cyl(M["chrome"], 0.018, 0.04, (w, math.sin(an) * R * 0.3, math.cos(an) * R * 0.3), "X", 6, bevel=0.003)
        for i in range(10):                                               # rust and grease on the flange faces
            an = i / 10 * math.tau
            p.cyl(M["rust"], 0.03, 0.006, (w + 0.002, math.sin(an) * R * 0.7, math.cos(an) * R * 0.7), "X", 8, bevel=0.0)
        return p.build()
    pa.add(key, fn, F, fit_axes=(1, 2), category="Wheel", radius=round(R_vox * VS, 3), width=round((wx + 1) * VS, 3), tread="track",
           socket="wheel_* (rig track mounts)", mass=mass, size_class=size, tile="wheels_rig")


def giant_drum():
    """PartLibrary.GiantTrackDrum: the rolling city's crawler drum at base scale: riveted rust-red wheel, eight box spokes
    on both faces, a rim band of cast shoe pads with grouser bars, slewing hub, the chain cog on the inboard face."""
    def fn():
        key = "wheel_track_giant"
        R, wx = 25 * VS, 27
        w = (wx + 1) * VS
        p = Part(key)
        rd = M["rust"]
        band = 3.2 * VS
        p.lathe(M["steel_dark"], [(R - band, 3 * VS), (R - 1.1 * VS, 3 * VS), (R - 1.1 * VS, w - 3 * VS), (R - band, w - 3 * VS)], (0, 0, 0), "X", 64)
        for i in range(40):                                               # cast shoe pads (grousers) round the rim
            an = i / 40 * math.tau
            p.add(kit.bm_box(w - 6 * VS, 0.78 / 40 * math.tau * R, 1.1 * VS, bevel=0.015, segs=1), M["steel"],
                  (w / 2, math.sin(an) * (R - 0.55 * VS), math.cos(an) * (R - 0.55 * VS)), (-an, 0, 0))
            p.add(kit.bm_box(w - 6 * VS, 0.05, 0.6 * VS, bevel=0.006, segs=1), M["iron"],
                  (w / 2, math.sin(an + 0.07) * (R + 0.1 * VS), math.cos(an + 0.07) * (R + 0.1 * VS)), (-an, 0, 0))
        for (x0, x1) in ((0.0, 3 * VS), (w - 3 * VS, w)):                 # flanges: outer ring + spokes + hub on each face
            p.lathe(rd, [(R - 6 * VS, x0), (R, x0), (R, x1), (R - 6 * VS, x1)], (0, 0, 0), "X", 64)
            for i in range(8):
                an = (i + 0.5) / 8 * math.tau
                mid = (R * 0.26 + R - 6 * VS) / 2
                p.add(kit.bm_box(x1 - x0, 0.16 / 8 * math.tau * mid * 1.6, R - 6 * VS - R * 0.26 + 0.1, bevel=0.02, segs=1), rd,
                      ((x0 + x1) / 2, math.sin(an) * mid, math.cos(an) * mid), (-an, 0, 0))
            for i in range(48):                                           # rivet rows
                an = i / 48 * math.tau
                xr = x0 - 0.004 if x0 == 0.0 else x1 + 0.004
                p.cyl(M["chrome"], 0.016, 0.012, (xr, math.sin(an) * (R - 4.5 * VS), math.cos(an) * (R - 4.5 * VS)), "X", 6, bevel=0.0)
        p.lathe(rd, [(0.0, 0.0), (R * 0.26, 0.0), (R * 0.26, w), (0.0, w)], (0, 0, 0), "X", 40)            # hub barrel
        p.lathe(M["steel"], [(2.5 * VS, w), (4.5 * VS, w), (4.5 * VS, w + 0.04), (2.5 * VS, w + 0.04)], (0, 0, 0), "X", 32)
        p.lathe(M["chrome"], [(0.0, w), (2.5 * VS, w), (2.2 * VS, w + 0.12), (0.0, w + 0.14)], (0, 0, 0), "X", 24)
        p.lathe(M["steel_dark"], [(6.5 * VS, -1.5 * VS), (8.2 * VS + 0.04, -1.5 * VS), (8.2 * VS + 0.04, -0.5 * VS), (6.5 * VS, -0.5 * VS)], (0, 0, 0), "X", 40)   # chain cog
        for i in range(0, 20, 2):
            an = i / 20 * math.tau
            p.add(kit.bm_box(2 * VS, 0.1, 0.16, bevel=0.01, segs=1), M["steel"], (-1.0 * VS, math.sin(an) * 9 * VS, math.cos(an) * 9 * VS), (-an, 0, 0))
        return p.build()
    pa.add("wheel_track_giant", fn, F, fit_axes=(1, 2), category="Wheel", radius=2.0, width=2.24, tread="track",
           socket="wheel_* (rig heavy track mounts)", mass=6000.0, size_class=5, tile="wheels_rig")


haul_tyre("wheel_heavy", 13.75, 8.0, 8, 22, ("rig_ochre", "c8961e"), 420.0, 4)
haul_tyre("wheel_giant", 20.0, 11.5, 12, 30, ("haul_crimson", "8a2018"), 1400.0, 5)
sprocket("wheel_track_wide", 5.6, 10, 420.0, 3)
sprocket("wheel_track_heavy", 11.25, 17, 1600.0, 4)
giant_drum()

if __name__ == "__main__":
    out = os.environ.get("HD_PREVIEW")
    if out:
        pa.PREVIEW = out
    pa.run(F, per_tile=5)
