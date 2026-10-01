"""HD watercraft: Raft, Skiff, Trawler, Houseboat, IronEel (BoatDesigns.cs; 1 voxel = 0.08 m).

blender -b -P boats.py -- <out_dir>
Root empty per boat: Hull, Body (deck gear / superstructure), Glass, Prop (spinner, origin on the shaft), engine part
at its socket (engine_outboard / engine_marine_diesel). Keel at z = 0 as in the voxel design (waterline = boatDraft)."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit  # noqa: E402
import comps  # noqa: E402
from kit import V, Part, Vector  # noqa: E402

M = {}


def extra(M):
    M["antifoul"] = kit.surface("Antifouling", kit.P("crimson", 2), chips=0.2, rust=0.45, dust=0.0, rough=0.75, var=0.2, streaks=0.4)
    M["topsides"] = kit.surface("NavyTopsides", kit.P("navy", 2), chips=0.45, chip_col=kit.P("rust", 2), rust=0.4, dust=0.2, rough=0.6, var=0.18, streaks=0.6)
    M["band"] = kit.surface("CreamBand", kit.P("cream", 2), chips=0.3, rust=0.3, dust=0.2, rough=0.55, streaks=0.5)
    M["house"] = kit.surface("WheelhouseCream", kit.P("cream", 2), chips=0.4, chip_col=kit.P("rust", 2), rust=0.3, dust=0.3, rough=0.55, streaks=0.5)
    M["deck_steel"] = kit.surface("DeckSteel", kit.P("metal", 2), rust=0.35, dust=0.3, metal=0.6, rough=0.7, bump=0.8)
    M["hull_alu"] = kit.surface("HullAlu", kit._hex("8a8d90"), dust=0.3, metal=0.85, rough=0.45, var=0.12, streaks=0.2)
    M["sub_iron"] = kit.surface("SubIron", kit.P("metal", 2), rust=0.45, dust=0.0, metal=0.7, rough=0.6, var=0.2, streaks=0.5, bump=0.6)
    M["sub_olive"] = kit.surface("SubOlive", kit.P("riggreen", 3), chips=0.4, rust=0.4, dust=0.0, rough=0.65, var=0.2, streaks=0.5)
    M["drum_olive"] = kit.surface("DrumOlive", kit.P("olive", 2), chips=0.4, rust=0.5, dust=0.3, rough=0.6, streaks=0.4)
    M["drum_navy"] = kit.surface("DrumNavy", kit.P("navy", 2), chips=0.4, rust=0.5, dust=0.3, rough=0.6, streaks=0.4)
    M["planks"] = kit.textured("DeckPlanks", "planks", kit.P("wood", 2), kit.P("wood", 3), scale=1.0, dust=0.25, rough=0.85, bump=0.5, rot="X")
    M["siding"] = kit.textured("ShackSiding", "planks", kit.P("wood", 1), kit.P("wood", 2), scale=1.0, dust=0.3, rough=0.85, bump=0.6, rot="Z")
    M["tin"] = kit.textured("TinRoof", "corrugated", kit.P("metal", 2), kit.P("metal", 3), kit.P("rust", 2), scale=1.0, dust=0.1, rough=0.6, bump=0.6, rot="X")
    M["net"] = kit.textured("NetHeap", "canvas", kit.P("olive", 1), kit.P("olive", 3), dust=0.1, rough=0.95, bump=1.0)
    M["sail_patch"] = kit.textured("TarpSail", "canvas", kit.P("cream", 2), kit.P("cream", 1), dust=0.2, rough=0.9, bump=0.3)


def hull(p, zs, wfn, deckfn, keelfn, bands, sharp=0.38, n_side=7):
    """Lofted hull: stations at game-z voxels `zs`; half-width w(z), deck y(z), keel y(z) in voxels. Side curve
    x = w * t^sharp (t = height fraction). Bands = [(y0, y1, material)] in voxels, each a closed solid slice."""
    for (y0, y1, mat) in bands:
        secs = []
        for z in zs:
            w, dk, kl = wfn(z), deckfn(z), keelfn(z)
            a, b = max(y0, kl), min(y1, dk)
            if b <= a:
                a, b = min(max(y0, kl), dk), min(max(y0, kl), dk) + 0.01
            side = []
            for k in range(n_side + 1):
                y = a + (b - a) * k / n_side
                t = max(0.0, (y - kl) / max(dk - kl, 1e-3))
                side.append((max(w * t ** sharp, 0.05), y))
            loop = [V(x, y, z) for x, y in side] + [V(-x, y, z) for x, y in reversed(side)]
            secs.append(loop)
        p.add(kit.bm_loft(secs), mat)


def hull_shell(p, zs, wfn, deckfn, keelfn, floor, thick, mat, sharp=0.3, n_side=7, stripes=()):
    """Open hull (skiffs): outer skin, gunwale, inner skin down to a floor at `floor` voxels; `thick` in voxels.
    stripes = [(y0, y1, material)] painted bands just proud of the outer skin."""
    secs = []
    for z in zs:
        w, dk, kl = wfn(z), deckfn(z), keelfn(z)
        outer = []
        for k in range(n_side + 1):
            y = kl + (dk - kl) * k / n_side
            t = (y - kl) / max(dk - kl, 1e-3)
            outer.append((max(w * t ** sharp, 0.05), y))
        fl = max(floor, kl + thick)
        inner = []
        for k in range(n_side + 1):
            y = dk - (dk - fl) * k / n_side
            t = (y - kl) / max(dk - kl, 1e-3)
            inner.append((max(w * t ** sharp - thick, 0.02), y))
        loop = [V(x, y, z) for x, y in outer] + [V(x, y, z) for x, y in inner] + [V(-x, y, z) for x, y in reversed(inner)] + [V(-x, y, z) for x, y in reversed(outer)]
        secs.append(loop)
    p.add(kit.bm_loft(secs), mat)
    for (y0, y1, smat) in stripes:
        for s in (-1, 1):
            st = []
            for z in zs[1:-1]:
                w, dk, kl = wfn(z), deckfn(z), keelfn(z)
                if y0 <= kl:
                    continue
                xs = [w * ((y - kl) / max(dk - kl, 1e-3)) ** sharp for y in (y0, y1)]
                st.append([V(s * (xs[0] + 0.06), y0, z), V(s * (xs[1] + 0.06), y1, z), V(s * (xs[1] - 0.05), y1, z), V(s * (xs[0] - 0.05), y0, z)])
            if len(st) > 1:
                p.add(kit.bm_loft(st), smat)


def tyre_fender(p, loc, r=0.2, w=0.12):
    p.add(kit.bm_torus(r * 0.62, r * 0.38, 20, 8), M["rubber"], loc, (0, math.pi / 2, 0))


def raft():
    root = kit.empty("Raft")
    h = Part("Hull")
    for x in (-8, 8):
        for z in (-14, 0, 14):
            mat = M["drum_olive"] if ((x + z) & 8) == 0 else M["drum_navy"]
            c = V(x, 4, z)
            h.cyl(mat, 0.31, 1.0, c, "Y", 28, bevel=0.02)
            for dz in (-0.33, 0.0, 0.33):
                h.add(kit.bm_torus(0.312, 0.012, 28, 6), M["rust"], c + Vector((0, dz, 0)), (math.pi / 2, 0, 0))
            h.cyl(M["steel"], 0.03, 0.02, c + Vector((0.12, 0.5, 0.12)), "Y", 8)
    # timber frame + deck planks + lashings
    for z in (-18, -6, 6, 18):
        h.box(M["wood_dark"], (2.16, 0.12, 0.1), V(0, 8.4, z), bevel=0.01)
    for i in range(14):
        x = -13 + i * 2
        h.box(M["wood"] if i % 3 else M["wood_light"], (0.15, 3.44 - (i % 2) * 0.1, 0.05), V(x, 9.2, 0) + Vector((0, 0, 0.0)), (0, 0, (i % 3 - 1) * 0.01), bevel=0.008)
    for z in (-18, -6, 6, 18):
        h.box(M["rope"], (2.2, 0.035, 0.035), V(0, 9.8, z), bevel=0.012)
        for x in (-11, -3, 5, 12):
            h.add(kit.bm_torus(0.03, 0.012, 10, 6), M["rope"], V(x, 9.6, z), (math.pi / 2, 0, 0))
    h.build(root)
    b = Part("Body")
    b.tube(M["wood_dark"], [V(0, 10, 14), V(0, 44, 14)], 0.05, 12)  # mast
    b.tube(M["wood_dark"], [V(0, 16, 14), V(0, 16, 1)], 0.03, 10)  # boom
    sail = kit.bm_loft([[V(x, y, z) for (y, z) in ((16.5, 1.5), (40, 13.5), (16.5, 13.5))] for x in (0.9, 1.1)])
    b.add(sail, M["sail_patch"])
    for (y, z) in ((24, 9), (31, 11.5), (20, 5)):
        b.box(M["crimson"], (0.02, 0.35, 0.28), V(1.15, y, z), bevel=0.003)  # patches
    for q in (V(-13, 9.6, 21), V(13, 9.6, 21), V(0, 9.6, -21)):
        b.tube(M["rope"], [V(0, 43, 14), q], 0.006, 5, caps=False)
    b.box(M["wood"], (0.5, 0.42, 0.38), V(0, 12, -13.5), bevel=0.015)  # crate seat
    b.box(M["wood_dark"], (0.52, 0.03, 0.06), V(0, 13.5, -11.4), bevel=0.004)
    b.box(M["canvas"], (0.42, 0.36, 0.06), V(0, 14.4, -13.5), bevel=0.02)
    b.tube(M["wood_light"], [V(-10, 10.2, -8), V(-6, 10.2, 12)], 0.022, 8)  # paddle
    b.box(M["wood_light"], (0.2, 0.32, 0.02), V(-5.6, 10.2, 13.5), (0, 0, 0.2), bevel=0.008)
    b.cyl(M["olive"], 0.15, 0.36, V(9, 13, -14), "Z", 20, bevel=0.02)  # jerry / water keg
    b.cyl(M["black"], 0.04, 0.03, V(9, 15.4, -14), "Z", 10)
    b.build(root)
    return root


def skiff():
    root = kit.empty("Skiff")
    h = Part("Hull")

    def w(z):
        bow = min(1, max(0, (z - 17) / 10))
        return 10 - bow * 2.2 - (max(0, -24 - z)) * 0.3

    def keel(z):
        bow = min(1, max(0, (z - 17) / 10))
        return bow * bow * 7

    zs = [-27, -26, -20, -10, 0, 10, 15, 18, 20, 22, 24, 26, 27]
    hull_shell(h, zs, w, lambda z: 10, keel, 1.6, 0.35, M["hull_alu"], sharp=0.18, stripes=[(6.6, 7.6, M["olive"])])
    h.box(M["hull_alu"], (1.6, 0.05, 0.75), V(0, 5.3, -26.6), bevel=0.01)  # transom plate
    # inner floor + rib frames, gunwale rails, rivet lines
    h.box(M["hull_alu"], (1.5, 3.6, 0.03), V(0, 1.2, -6), bevel=0.0)
    for z in range(-24, 17, 6):
        h.box(M["steel"], (1.58, 0.04, 0.06), V(0, 2.0, z), bevel=0.006)
    for s in (-1, 1):
        h.tube(M["chrome"], [V(s * w(z) * 1.0, 10.15, z) for z in range(-27, 28, 3)], 0.022, 8)
        for z in range(-26, 24, 2):
            h.cyl(M["chrome"], 0.008, 0.01, V(s * (w(z) + 0.05), 3, z), "X", 6, bevel=0)
    h.build(root)
    b = Part("Body")
    for z in (-19, -2, 14):
        b.box(M["wood"], (1.56, 0.28, 0.04), V(0, 6.2, z + 1.5), bevel=0.008)
        for s in (-1, 1):
            b.box(M["wood_dark"], (0.06, 0.24, 0.3), V(s * 7.4, 4.6, z + 1.5), bevel=0.006)
    b.box(M["crimson"], (0.32, 0.46, 0.3), V(-5, 2.4, 5.5), bevel=0.03)  # cooler
    b.box(M["cream"], (0.34, 0.48, 0.05), V(-5, 4.4, 5.5), bevel=0.02)
    for s in (-1, 1):
        b.tube(M["black"], [V(s * 9, 10, -8), V(s * 12, 30, -16)], 0.01, 6)
        b.cyl(M["steel"], 0.035, 0.04, V(s * 9.3, 11.5, -8.4), "X", 12, bevel=0.005)
        b.tube(M["steel"], [V(s * 12, 30, -16), V(s * 13, 18, -24)], 0.0015, 3, caps=False)
    comps.lamp(b, M, V(0, 11, 26.6), 0.04, 0.04, mat=M["chrome"])
    b.box(M["tarp"], (0.5, 0.6, 0.18), V(4, 2.4, -14), bevel=0.06)
    b.build(root)
    eng = comps.outboard(M)
    eng.parent = root
    eng.location = V(0, 9, -28)
    return root


def trawler():
    root = kit.empty("Trawler")
    DK = 20

    def w(z):
        bt = min(1, max(0, (z - 20) / 40))
        return max(0.6, 20 * math.sqrt(max(0, 1 - bt * bt)) - max(0, -56 - z) * 0.5)

    def deck(z):
        return DK + max(0, z - 30) * 0.15

    def keel(z):
        return max(0, z - 30) * 0.35

    zs = [-60, -58, -50, -35, -15, 0, 15, 25, 32, 40, 47, 53, 57, 59.5, 60.5]
    h = Part("Hull")
    hull(h, zs, w, deck, keel, [(-1, 8.6, M["antifoul"]), (8.6, 10.4, M["band"]), (10.4, 40, M["topsides"])], sharp=0.33, n_side=8)
    # bulwarks with freeing ports + rubbing strakes + tyre fenders
    for s in (-1, 1):
        pts_lo = [(s * w(z), deck(z), z) for z in zs[1:-1]]
        secs = []
        for (x, y, z) in pts_lo:
            secs.append([V(x - s * 0.15, y - 0.2, z), V(x + s * 0.02, y - 0.2, z), V(x + s * 0.02, y + 4.2, z), V(x - s * 0.15, y + 4.2, z)])
        h.add(kit.bm_loft(secs), M["topsides"])
        h.add(kit.bm_loft([[V(x + s * 0.02, y + 4.2, z), V(x - s * 0.3, y + 4.2, z), V(x - s * 0.3, y + 4.7, z), V(x + s * 0.02, y + 4.7, z)] for (x, y, z) in pts_lo]), M["wood_dark"])
        h.tube(M["rust"], [V(s * (w(z) + 0.2), 14, z) for z in zs[1:-2]], 0.04, 8)
        for z in (-30, -5, 20):
            tyre_fender(h, V(s * (w(z) + 0.6), DK - 4, z), 0.22)
    h.box(M["deck_steel"], (2.9, 7.4, 0.02), V(0, DK + 0.1, -15), bevel=0)
    h.build(root)
    b = Part("Body")
    gl = Part("Glass")
    f = DK + 1
    # wheelhouse: walls, windows, door, roof with rail, mast, radar, lights, searchlight
    hc = V(0, f + 10.5, 20)
    b.box(M["house"], (1.92, 1.92, 1.72), hc + Vector((0, 0, -0.04)), bevel=0.03)
    b.box(M["band"], (1.94, 1.94, 0.06), V(0, f + 10, 20), bevel=0.01)
    for i in range(4):  # front windows
        x = -7.5 + i * 5
        gl.box(M["glass"], (0.34, 0.03, 0.42), V(x, f + 15, 32.2), bevel=0.01)
        b.box(M["black"], (0.38, 0.02, 0.46), V(x, f + 15, 32.05), bevel=0.005)
    for s in (-1, 1):
        for z in (21, 26, 30.5):
            gl.box(M["glass"], (0.03, 0.3, 0.42), V(s * 12.2, f + 15, z), bevel=0.01)
            b.box(M["black"], (0.02, 0.34, 0.46), V(s * 12.05, f + 15, z), bevel=0.005)
    b.box(M["black"], (0.5, 0.04, 1.14), V(0, f + 7, 7.8), bevel=0.01)  # door
    b.box(M["wood"], (0.44, 0.03, 1.08), V(0, f + 7, 7.7), bevel=0.01)
    b.box(M["deck_steel"], (2.12, 2.12, 0.14), V(0, f + 22.5, 20), bevel=0.02)
    for s in (-1, 1):
        b.tube(M["steel"], [V(s * 12.8, f + 23.5, 8), V(s * 12.8, f + 26, 8), V(s * 12.8, f + 26, 32), V(s * 12.8, f + 23.5, 32)], 0.015, 6)
    b.tube(M["steel"], [V(0, f + 23, 20), V(0, f + 47, 20)], 0.05, 12)
    b.tube(M["steel"], [V(-6, f + 40, 20), V(6, f + 40, 20)], 0.025, 8)
    b.sphere(M["lamp_red"], 0.04, V(-6, f + 41, 20))
    b.sphere(M["lamp_green"], 0.04, V(6, f + 41, 20))
    b.sphere(M["lamp"], 0.05, V(0, f + 47.6, 20))
    b.cyl(M["black"], 0.04, 0.1, V(0, f + 24.5, 25), "Z", 10)
    b.box(M["black"], (0.7, 0.12, 0.06), V(0, f + 25.6, 25), (0, 0, 0.5), bevel=0.02)  # radar bar
    b.add(kit.bm_lathe([(0, 0), (0.12, 0), (0.14, 0.1), (0.12, 0.2), (0, 0.2)], 20, "Y"), M["chrome"], V(0, f + 25, 30.5))
    b.add(kit.bm_lathe([(0.0, 0.0), (0.1, 0.0)], 20, "Y", close=True), M["lamp"], V(0, f + 25, 30.5) + Vector((0, -0.2, 0)))
    b.cyl(M["black"], 0.07, 0.8, V(-9, f + 26, 12), "Z", 16, bevel=0.01)  # exhaust stack
    b.cyl(M["rust"], 0.08, 0.06, V(-9, f + 31, 12), "Z", 16, bevel=0.01)
    # stern gantry, net drum, net heap with floats, hold hatch, winch, life ring
    for s in (-1, 1):
        b.tube(M["ochre"], [V(s * 17.5, f, -54), V(s * 14, f + 34, -58)], 0.07, 12)
        b.box(M["ochre"], (0.25, 0.25, 0.08), V(s * 17.5, f, -54), bevel=0.02)
    b.tube(M["ochre"], [V(-14, f + 34, -58), V(14, f + 34, -58)], 0.07, 12)
    for x in (-6, 6):
        b.add(kit.bm_lathe([(0, 0), (0.1, 0), (0.1, 0.04), (0.04, 0.06), (0, 0.06)], 16, "X"), M["steel"], V(x, f + 32, -58))
        b.tube(M["steel"], [V(x, f + 32, -58), V(x * 0.6, f + 10, -56)], 0.006, 4, caps=False)
    dc = V(0, f + 5, -40)
    b.cyl(M["steel_dark"], 0.38, 1.7, dc, "X", 28, bevel=0.01)
    b.cyl(M["net"], 0.36, 1.5, dc, "X", 28, bevel=0.0)
    for s in (-1, 1):
        b.cyl(M["ochre"], 0.48, 0.05, dc + Vector((s * 0.85, 0, 0)), "X", 28, bevel=0.01)
        b.box(M["ochre"], (0.08, 0.3, 0.42), dc + Vector((s * 0.92, 0, -0.2)), bevel=0.01)
    import mathutils.noise as mn
    heap = kit.bm_rock(0.75, 3, 0.4, 3)
    b.add(heap, M["net"], V(0, f, -52), scale=(1.0, 0.9, 1.0))
    for i in range(9):
        a = i * 2.39
        b.sphere(M["crimson"] if i % 2 else M["hazard"], 0.06, V(0, f + 2.4, -52) + Vector((math.cos(a) * 0.5, math.sin(a) * 0.45, 0.05 * (i % 3))), 10, 6)
    b.box(M["deck_steel"], (1.04, 1.04, 0.1), V(0, DK + 0.6, -16), bevel=0.02)
    b.box(M["steel_dark"], (0.9, 0.9, 0.04), V(0, DK + 1.3, -16), bevel=0.01)
    b.add(kit.bm_torus(0.22, 0.05, 20, 8), M["crimson"], V(12.4, f + 12, 8) + Vector((0, 0.05, 0)), (math.pi / 2, 0, 0))
    b.box(M["olive"], (0.6, 0.5, 0.5), V(-8, f + 3, 0), bevel=0.04)  # fish boxes
    b.box(M["navy"], (0.6, 0.5, 0.5), V(-8, f + 9, 0), bevel=0.04)
    b.box(M["olive"], (0.6, 0.5, 0.4), V(8, f + 2.5, -4), (0, 0, 0.2), bevel=0.04)
    b.build(root)
    gl.build(root)
    prop = comps.propeller("Prop", M, 0.48, 3, 0.08, wood=False, pitch=0.6)
    prop.parent = root
    prop.location = V(0, 5, -62)
    h2 = Part("Rudder")
    h2.box(M["antifoul"], (0.05, 0.5, 0.7), V(0, 5, -65.5), bevel=0.015)
    h2.tube(M["steel"], [V(0, 0, -64), V(0, 10, -64)], 0.02, 8)
    h2.build(root)
    return root


def houseboat():
    root = kit.empty("Houseboat")
    F = 14
    h = Part("Hull")
    for x in (-16, 16):  # pontoon tanks with conical noses
        c = V(x, 6, 0)
        h.add(kit.bm_lathe([(0.0, -4.86), (0.2, -4.8), (0.44, -4.5), (0.48, -4.2), (0.48, 4.2), (0.44, 4.5), (0.2, 4.8), (0.0, 4.86)], 28, "Y"), M["rust"], c)
        for k in range(-3, 4):
            h.add(kit.bm_torus(0.482, 0.015, 28, 6), M["rust"], c + Vector((0, k * 1.2, 0)), (math.pi / 2, 0, 0))
    for z in range(-56, 57, 14):
        h.box(M["steel_dark"], (3.6, 0.12, 0.16), V(0, 11.5, z), bevel=0.01)
    h.box(M["planks"], (3.92, 9.7, 0.12), V(0, 13.5, 0), bevel=0.01)
    h.build(root)
    b = Part("Body")
    gl = Part("Glass")
    # shack: plank walls with window/door openings (built as wall panels around the holes)
    y0, y1 = (F + 0.5) * 0.08, (F + 30.5) * 0.08

    def wall(axis_x, fixed, a0, a1, holes, mat):
        cuts = sorted({a0, a1} | {u for hh in holes for u in (hh[0], hh[1])})
        for ua, ub in zip(cuts, cuts[1:]):
            vs = [y0, y1]
            hs = [hh for hh in holes if hh[0] <= ua and hh[1] >= ub]
            segs = []
            lo = y0
            for hh in sorted(hs, key=lambda q: q[2]):
                segs.append((lo, hh[2]))
                lo = hh[3]
            segs.append((lo, y1))
            for (va, vb) in segs:
                if vb - va < 1e-3:
                    continue
                cu, cv = (ua + ub) / 2, (va + vb) / 2
                if axis_x:
                    b.box(mat, (ub - ua, 0.08, vb - va), (cu, fixed, cv), bevel=0.0)
                else:
                    b.box(mat, (0.08, ub - ua, vb - va), (fixed, cu, cv), bevel=0.0)
        for hh in holes:
            if hh[2] > y0 + 0.01:
                cu = (hh[0] + hh[1]) / 2
                if axis_x:
                    gl.box(M["glass"], (hh[1] - hh[0], 0.02, hh[3] - hh[2]), (cu, fixed, (hh[2] + hh[3]) / 2), bevel=0)
                    b.box(M["wood_light"], (hh[1] - hh[0] + 0.1, 0.12, 0.06), (cu, fixed, hh[2] - 0.02), bevel=0.01)
                else:
                    gl.box(M["glass"], (0.02, hh[1] - hh[0], hh[3] - hh[2]), (fixed, cu, (hh[2] + hh[3]) / 2), bevel=0)
                    b.box(M["wood_light"], (0.12, hh[1] - hh[0] + 0.1, 0.06), (fixed, cu, hh[2] - 0.02), bevel=0.01)

    wy = lambda vz: -vz * 0.08
    win = ((F + 14) * 0.08, (F + 22) * 0.08)
    wall(True, wy(20), -1.44, 1.44, [(-0.5, 0.5, y0, (F + 27) * 0.08), (-1.2, -0.8, *win), (0.8, 1.2, *win)], M["siding"])
    wall(True, wy(-30), -1.44, 1.44, [(-0.32, 0.32, *win)], M["siding"])
    for s in (-1, 1):
        wall(False, s * 1.44, wy(20), wy(-30), [(wy(-14), wy(-22), *win), (wy(6), wy(-2), *win)], M["siding"])
    b.box(M["wood_dark"], (0.06, 0.04, (F + 27) * 0.08 - y0), (-0.53, wy(20) - 0.04, (y0 + (F + 27) * 0.08) / 2), bevel=0)
    b.box(M["wood_dark"], (0.06, 0.04, (F + 27) * 0.08 - y0), (0.53, wy(20) - 0.04, (y0 + (F + 27) * 0.08) / 2), bevel=0)
    b.box(M["wood"], (0.96, 0.05, 2.1), (0.0, wy(20) - 0.6, (y0 + (F + 27) * 0.08) / 2 - 0.02), (0, 0, 1.0), bevel=0.01)  # door ajar
    for s in (-1, 1):
        for z in (20, -30):
            b.box(M["wood_dark"], (0.12, 0.12, y1 - y0), (s * 1.44, wy(z), (y0 + y1) / 2), bevel=0.01)
    # tin roof (two shallow pitches) with eaves
    ry = (F + 31) * 0.08
    for s in (-1, 1):
        b.add(kit.bm_box(1.66, 4.5, 0.03, bevel=0), M["tin"], (s * 0.8, wy(-5), ry + 0.12), (0, -s * 0.12, 0))
    b.box(M["wood_dark"], (0.1, 4.5, 0.1), (0, wy(-5), ry + 0.22), bevel=0.01)
    b.tube(M["black"], [V(10, F + 33, -20), V(10, F + 44, -20)], 0.07, 12)
    b.cyl(M["rust"], 0.12, 0.04, V(10, F + 44.5, -20), "Z", 14, bevel=0.01, r2=0.02)
    # porch railing + bench + paint-can plants; stern washing line
    for x in range(-22, 23, 4):
        b.box(M["wood"], (0.06, 0.06, 0.8), V(x, F + 5.5, 58), bevel=0.008)
    b.box(M["wood_light"], (3.6, 0.08, 0.06), V(0, F + 10, 58), bevel=0.01)
    for s in (-1, 1):
        for z in range(22, 59, 4):
            b.box(M["wood"], (0.06, 0.06, 0.8), V(s * 24, F + 5.5, z), bevel=0.008)
        b.box(M["wood_light"], (0.08, 2.96, 0.06), V(s * 24, F + 10, 40), bevel=0.01)
    b.box(M["wood"], (0.8, 0.36, 0.06), V(17, F + 4.4, 42), bevel=0.01)
    for x in (13, 21):
        b.box(M["wood_dark"], (0.06, 0.3, 0.32), V(x, F + 2.4, 42), bevel=0.006)
    for x in (-19, -13):
        b.cyl(M["crimson"], 0.11, 0.24, V(x, F + 2.6, 53), "Z", 16, bevel=0.01)
        b.add(kit.bm_rock(0.16, x, 1.1, 2), M["leaf"], V(x, F + 5.6, 53))
    for x in (-20, 20):
        b.tube(M["wood_dark"], [V(x, F + 1, -50), V(x, F + 20, -50)], 0.035, 8)
    b.tube(M["rope"], [V(-20, F + 20, -50), V(0, F + 19, -50), V(20, F + 20, -50)], 0.005, 4, caps=False)
    for x, mat in ((-12, M["navy"]), (-2, M["crimson"]), (10, M["cream"])):
        b.box(mat, (0.36, 0.015, 0.5), V(x, F + 16.4, -50), (0, 0, 0.05), bevel=0.003)
    b.cyl(M["olive"], 0.25, 0.8, V(-18, F + 5.5, -40), "Z", 20, bevel=0.02)  # water barrel
    b.box(M["wood_dark"], (0.6, 0.4, 0.3), V(-6, F + 1.9, 6), bevel=0.02)  # table inside (seen through the door)
    b.build(root)
    gl.build(root)
    eng = comps.outboard(M)
    eng.parent = root
    eng.location = V(0, F, -61)
    return root


def iron_eel():
    root = kit.empty("IronEel")
    C, R = 20, 17
    h = Part("Hull")

    def rad(z):
        if z > 45:
            return R * math.sqrt(max(0.0, 1 - ((z - 45) / 30) ** 2))
        if z < -45:
            return R * (1 - (-45 - z) / 30 * 0.8)
        return R

    prof = [(rad(z) * 0.08, -z * 0.08) for z in [75, 74.5, 74, 72, 69, 65, 60, 55, 50, 45, 20, 0, -20, -45, -55, -65, -72, -75]]
    prof = [(max(r, 0.0), y) for r, y in prof]
    prof[0] = (0.0, prof[0][1])
    hb = kit.bm_lathe(prof, 40, "Y", close=True)
    h.add(hb, M["sub_iron"], V(0, C, 0))
    # olive casing deck along the top + weld seams + rivet rows + portholes
    h.add(kit.superloft([(V(0, 0, z).y, 0, V(0, C + R - 1.2, 0).z, w, 0.18) for z, w in ((58, 0.05), (52, 0.42), (-40, 0.45), (-50, 0.25), (-56, 0.04))], 20, 4.0), M["sub_olive"])
    for z in range(-63, 70, 9):
        r = rad(z) * 0.08
        h.add(kit.bm_torus(r + 0.004, 0.012, 40, 4), M["sub_iron"], V(0, C, z), (math.pi / 2, 0, 0))
        for k in range(24):
            a = k / 24 * math.tau
            if math.sin(a) < -0.2:
                continue
            h.cyl(M["chrome"], 0.012, 0.014, V(0, C, z + 1.2) + Vector((math.cos(a) * (r + 0.006), 0, math.sin(a) * (r + 0.006))), "X", 6, bevel=0,
                  rot=(0, -a + math.pi / 2, 0))
    gl = Part("Glass")
    for z in range(-30, 50, 15):
        for s in (-1, 1):
            c = V(s * (R - 0.6), C + 2, z)
            h.add(kit.bm_torus(0.11, 0.025, 20, 6), M["brass"], c, (0, math.pi / 2, 0))
            gl.cyl(M["glass"], 0.1, 0.03, c, "X", 20, bevel=0)
    h.build(root)
    b = Part("Body")
    # the sail: rounded conning tower with the hatch, rails, periscope, sail planes
    sail = kit.superloft([(V(0, 0, z).y, 0, V(0, C + R + 8.5, 0).z, w, 0.88) for z, w in ((30.4, 0.08), (29, 0.3), (24, 0.42), (12, 0.42), (6, 0.3), (4.6, 0.1))], 28, 3.0)
    b.add(sail, M["sub_olive"])
    b.box(M["sub_iron"], (0.9, 2.0, 0.04), V(0, C + R + 18.2, 17.5), bevel=0.01)
    b.add(kit.bm_lathe([(0, 0), (0.14, 0), (0.15, 0.04), (0.12, 0.08), (0, 0.08)], 20), M["sub_iron"], V(0, C + R + 18.5, 20))
    b.add(kit.bm_torus(0.1, 0.012, 16, 6), M["chrome"], V(0, C + R + 19.6, 20))
    b.tube(M["steel"], [V(-2, C + R + 18, 14), V(-2, C + R + 27, 14)], 0.04, 12)
    b.box(M["steel_dark"], (0.1, 0.16, 0.1), V(-2, C + R + 27.5, 14.4), bevel=0.02)
    b.box(M["glass"], (0.06, 0.02, 0.05), V(-2, C + R + 27.5, 15.4), bevel=0.005)
    b.tube(M["steel"], [V(2, C + R + 18, 22), V(2, C + R + 24, 22)], 0.02, 8)  # snorkel mast
    for s in (-1, 1):
        b.tube(M["steel"], [V(s * 4.6, C + R + 18.5, 26), V(s * 4.6, C + R + 21.5, 26), V(s * 4.6, C + R + 21.5, 9), V(s * 4.6, C + R + 18.5, 9)], 0.012, 6)
        pl = kit.bm_loft([[(V(s * x, 0, 0).x, V(0, 0, z).y, V(0, C + R + 10, 0).z + dz) for (z, dz) in ((24, 0.0), (21, 0.035), (18, 0.0), (21, -0.035))] for x in (5, 12)])
        b.add(pl, M["sub_iron"])
        sp = kit.bm_loft([[(V(s * x, 0, 0).x, V(0, 0, z).y, V(0, C, 0).z + dz) for (z, dz) in ((-58, 0.0), (-61, 0.04), (-64, 0.0), (-61, -0.04))] for x in (rad(-60) - 1, 24)])
        b.add(sp, M["sub_iron"])
    b.add(kit.bm_loft([[(dx, V(0, 0, z).y, V(0, y, 0).z) for (z, y) in ((-69, C - 12), (-69, C + 12), (-74, C + 13), (-74, C - 13))] for dx in (-0.03, 0.03)]), M["sub_iron"])
    comps.lamp(b, M, V(0, C + 2.5, 74.2), 0.08, 0.06, axis="-Y", mat=M["sub_iron"])
    b.tube(M["steel"], [V(0, C + R - 0.4, 60), V(0, C + R + 6, 30.4)], 0.008, 4, caps=False)  # jumping wire
    b.tube(M["steel"], [V(0, C + R + 6, 4.6), V(0, C + R - 0.4, -50)], 0.008, 4, caps=False)
    b.build(root)
    gl.build(root)
    prop = comps.propeller("Prop", M, 0.56, 4, 0.1, wood=False, pitch=0.6)
    prop.parent = root
    prop.location = V(0, C, -77)
    return root


if __name__ == "__main__":
    kit.run_group([(raft, "boat_raft", "Raft (oil drums, tarp sail)", "Raft"),
                   (skiff, "boat_skiff", "Skiff (riveted alu, outboard)", "Skiff"),
                   (trawler, "boat_trawler", "Trawler (wheelhouse, gantry, net)", "Trawler"),
                   (houseboat, "boat_houseboat", "Houseboat (pontoons, shack)", "Houseboat"),
                   (iron_eel, "boat_ironeel", "IRON EEL submarine", "IronEel")], M, extra)
