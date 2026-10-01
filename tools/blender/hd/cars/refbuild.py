"""Build every road car from its real-world reference (refdata.CARS): body sections -> lofted surfaces (body.py),
real lamps/grilles/bumpers, real wheelbase/track/tyres, plus the game's mounted parts for the fictional designs.
The car is aligned with its voxel design on the wheelbase midpoint and the ground, so socket deltas are measurable."""
import math
import os

from mathutils import Vector

import body as B
import dress as D
import hdlib as H
import parts as P
from body import G, Spec
from hdlib import cyl, join, rbox, tube, xform
from refdata import CARS, GAME

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "../../../.."))
S = 0.08

# paint (Pal ramp, rust) per car; real cars from ModelCars.cs, game cars from their designs
PAINT = {
    "Fiat126p": ("crimson", 0.3), "Renault5": ("ochre", 0.3), "CitroenBX": ("cream", 0.25), "CitroenXM": ("navy", 0.2),
    "CitroenXantia": ("riggreen", 0.2), "LanciaYpsilon": ("bronze", 0.15), "Fiat500": ("paleblue", 0.3), "Peugeot205": ("crimson", 0.2),
    "Peugeot206": ("chrome", 0.15), "Peugeot207CC": ("black", 0.1), "Peugeot405": ("sand", 0.25), "Peugeot406Break": ("moss", 0.2),
    "FiatMultipla": ("olive", 0.2), "Interceptor": ("black", 0.25), "Scavenger": ("olive", 0.45), "Trabant": ("paleblue", 0.15),
    "Pickup": ("riggreen", 0.35), "Coupe": ("bronze", 0.25), "Sedan": ("paleblue", 0.3), "Wagon": ("cream", 0.3),
    "TowTruck": ("sand", 0.35), "Wrecker": ("rust", 0.4), "DuneBuggy": ("ochre", 0.2), "MonsterTruck": ("crimson", 0.1),
}
REAL_FILE = {"Fiat126p": "fiat126p", "Renault5": "renault5", "CitroenBX": "citroen_bx", "CitroenXM": "citroen_xm",
             "CitroenXantia": "citroen_xantia", "LanciaYpsilon": "lancia_ypsilon", "Fiat500": "fiat500", "Peugeot205": "peugeot205",
             "Peugeot206": "peugeot206", "Peugeot207CC": "peugeot207cc", "Peugeot405": "peugeot405", "Peugeot406Break": "peugeot406break",
             "FiatMultipla": "fiat_multipla"}


def lerp(pts, s):
    if s <= pts[0][0]:
        return pts[0][1]
    for (s0, h0), (s1, h1) in zip(pts, pts[1:]):
        if s0 <= s <= s1:
            return h0 if s1 == s0 else h0 + (h1 - h0) * (s - s0) / (s1 - s0)
    return pts[-1][1]


def header(fid):
    h = {}
    for line in open(os.path.join(REPO, "Assets/MadMax/Models/Cars", fid + ".txt")):
        if not line.startswith("#"):
            break
        k, _, v = line[2:].strip().partition(" ")
        h[k] = v
    return h


def game_layout(name):
    """(front z, rear z, wheel y, part key, socket x) of the voxel design."""
    if name in GAME:
        return GAME[name]
    hd = header(REAL_FILE[name])
    return int(hd["wheelZF"]), int(hd["wheelZR"]), 5, "wheel_compact", int(hd["wheelX"])


class Frame:
    """Metres (s from the front bumper, h above ground, x right) -> game voxels aligned on the voxel design."""

    def __init__(self, c, lay):
        zf, zr, wy, key, _ = lay
        self.zm = (zf + zr) / 2
        self.smid = c["fo"] + c["wb"] / 2
        self.gy = wy - H.WHEELS[key][0]
        self.lift = c.get("lift", 0.0)

    def z(self, s):
        return self.zm + (self.smid - s) / S

    def y(self, h, body=True):
        return self.gy + (h + (self.lift if body else 0.0)) / S

    def p(self, x, h, s, body=True):
        return Vector((x / S, self.y(h, body), self.z(s)))


def wheel_key(name, tyre, rear=False):
    R, w, rim = tyre
    key = "%s_%s" % (name, "R" if rear else "F")
    n = max(2, int(round(w / S)) - 1)
    rr = min(rim * 0.0127 / S, R / S - 0.9)
    tread = {"Pickup": "blocks", "TowTruck": "truck", "Wrecker": "truck", "Scavenger": "blocks", "MonsterTruck": "monster",
             "DuneBuggy": "mud" if rear else "blocks", "Wagon": "blocks"}.get(name, "street")
    rim_s = {"Interceptor": "bronze", "Coupe": "alloy", "Pickup": "steel", "TowTruck": "truck", "Wrecker": "truck",
             "Scavenger": "steel", "MonsterTruck": "beadlock", "DuneBuggy": "steel", "Wagon": "steel", "Sedan": "alloy",
             "Peugeot206": "alloy", "Peugeot207CC": "alloy", "CitroenXM": "alloy", "LanciaYpsilon": "alloy"}.get(name, "hubcap")
    H.WHEELS[key] = (R / S, n, rr, tread, rim_s)
    return key


def build(name, outline=False):
    c = CARS[name]
    lay = game_layout(name)
    F = Frame(c, lay)
    L, W = c["L"], c["W"]
    LB = c.get("length_body", L)          # lofted part (the Wrecker's cab + hood; its bed is separate)
    z, y = F.z, F.y
    ramp, rust = PAINT[name]
    pm = H.defmat(name + "_paint", "paint", ramp=ramp, idx=(1, 2, 3), rust=0.6 + rust * 2.5, seed=hash(name) % 50, rough=0.42) or name + "_paint"
    pm = name + "_paint"
    H.defmat(name + "_seam", "paint", ramp=ramp, idx=(0, 0, 1), rust=0.0, chips=None, seed=1, rough=0.6)
    if name == "MonsterTruck":
        H.defmat(pm, "flames", ground=F.gy)
    top = lambda s: lerp(c["top"], s)
    beltf = lambda s: min(top(s), lerp(c["belt"], s))
    ss = sorted(set([round(i * 0.03, 3) for i in range(int(L / 0.03) + 1)] + [L] + [p[0] for p in c["top"]] + [p[0] for p in c["belt"]]))
    ss = [s for s in ss if 0 <= s <= LB]
    planf = B.poly([(z(s), lerp(c["plan"], s) / S) for s in ss])
    hw = W / 2 / S
    style = c["style"]
    gl = c.get("glass")
    rear_engine = style == "rear"

    # ---- greenhouse
    g = None
    if gl:
        ws0, ws1, rw1, rw0 = gl
        gs = [s for s in ss if top(s) > beltf(s) + 0.03]
        sa, sb = min(gs), max(gs)
        roof_m, roof_zone = pm, (z(rw1), z(ws1))
        if c.get("roofPanel"):
            roof_m, roof_zone = "canvas", (z(c["roofPanel"][1]), z(c["roofPanel"][0]))
        if c.get("twotone"):
            H.defmat(name + "_roof", "paint", ramp="cream", idx=(2, 3, 4), rust=0.5, seed=7)
            roof_m = name + "_roof"
        rear = (z(rw0), z(rw1)) if rw0 - rw1 > 0.08 else None
        roofw = lerp(c["plan"], (ws1 + rw1) / 2) / S * c["roof"]
        g = G(z(sb), z(sa), [(z(s), y(top(s))) for s in ss if sa <= s <= sb], [(z(s), y(beltf(s))) for s in ss if sa <= s <= sb],
              base_ratio=c["shoulder"], roof_ratio=c["roof"], er=max(0.3, c["ger"] / S), crown=0.3, sill=0.45, frame=0.15,
              windshield=(z(ws1), z(ws0)), rear=rear, roof=roof_zone, windows=[(z(b), z(a)) for a, b in c["windows"]],
              open_windows=[(z(b), z(a)) for a, b in c.get("open_windows", [])], xp=roofw - 1.3, hfull=(c["H"] - beltf(ws1)) / S)
    # ---- bands: low = sill colour, one higher band = crease material
    low = [b for b in c["sides"] if b[1] < 0.35]
    mid = [b for b in c["sides"] if b[1] >= 0.35]
    bot_mid = lerp(c["bottom"], L / 2)
    sill = (y(low[0][1] + low[0][2] / 2) - y(bot_mid)) if low else 1.3
    crease, cmat = None, pm
    if mid:
        k, h, hh = mid[0]
        hh = max(hh, 0.04)
        crease = (y(h - hh / 2), y(h + hh / 2), z(L - 0.12), z(0.12))
        cmat = "chrome" if k == "C" else "plastic"
    # ---- tubs, doors, lids
    floor_h = bot_mid + 0.08
    f = int(round(y(floor_h)))
    tubs = []
    if gl:
        rear_end = gl[3] - 0.05 if style in ("hatch", "wagon", "rear") else gl[3] - 0.10
        if style in ("pickup", "truck"):
            rear_end = gl[2] - 0.04
        tubs.append((z(rear_end), z(gl[0] + 0.15), y(floor_h), lerp(c["plan"], gl[0] + 0.5) / S * c["shoulder"] - 1.3))
    if c.get("bed"):
        b0, b1 = c["bed"]
        tubs.append((z(b1), z(b0), y(beltf(b0) - 0.45), hw - 0.5))
    if c.get("tub"):
        t0, t1 = c["tub"]
        tubs.append((z(t1), z(t0), y(bot_mid + 0.06), lerp(c["plan"], (t0 + t1) / 2) / S - 0.6))
        e0, e1 = c["engine_bay"]
        tubs.append((z(e1 - 0.05), z(e0 + 0.02), y(bot_mid + 0.12), lerp(c["plan"], (e0 + e1) / 2) / S - 0.6))
    wi = tubs[0][3] if tubs else hw - 1
    doors = []
    for i, (a, b) in enumerate(c["doors"]):
        nm = "Door" if i == 0 else "DoorRear"
        doors.append((nm, z(b), z(a), (hw * 0.95, f, z(a))))
    hood = trunk = tail = None
    if gl and style not in ("truck",):
        hz1 = 0.10 if style not in ("pickup",) else 0.20
        hood = (z(gl[0] - 0.02), z(hz1), lerp(c["plan"], gl[0] * 0.6) / S - 1.0, (0, y(beltf(gl[0])) - 1, z(gl[0]) - 1))
    if c.get("tub"):
        hood = (z(0.95), z(0.08), lerp(c["plan"], 0.6) / S - 0.8, (0, y(top(0.9)) - 1, z(0.95)))
    if gl and (style == "sedan" or rear_engine):
        trunk = (z(L - 0.10), z(gl[3] + 0.08), lerp(c["plan"], L - 0.6) / S - 1.2, "Trunk")
    if style == "hatch":
        tail = ("green", z(gl[2]) + 0.4)
    elif style == "wagon":
        tail = ("green", z(gl[2] - 0.02))
    elif style == "pickup":
        tail = ("cap", z(L - 0.05))
    wR, wFkey = c["tyre"][0], wheel_key(name, c["tyre"])
    wRkey = wheel_key(name, c.get("tyre_rear", c["tyre"]), True)
    tr = c["track"]
    sxF = tr / 2 / S - (H.WHEELS[wFkey][1]) / 2.0
    sxR = tr / 2 / S - (H.WHEELS[wRkey][1]) / 2.0
    zF, zRw = z(c["fo"]), z(c["fo"] + c["wb"])
    wyF = F.gy + c["tyre"][0] / S
    wyR = F.gy + c.get("tyre_rear", c["tyre"])[0] / S
    wheels = [(zF, wyF, c["tyre"][0] / S, sxF), (zRw, wyR, c.get("tyre_rear", c["tyre"])[0] / S, sxR)]
    if c.get("spat"):
        wheels[1] = wheels[1] + (y(c["spat"]),)
    mats = dict(paint=pm, roof=roof_m if gl else pm, gpaint=(name + "_roof") if c.get("twotone") else pm, sill="plastic" if low else pm,
                crease=cmat, cap=pm, seam=name + "_seam")
    accent = None
    if c.get("woody"):
        mats["accent"] = "wood"
        accent = (y(c["woody"][0]), y(c["woody"][1]), z(L - 0.25), z(0.35))
    lift = c.get("lift", 0.0)
    sp = Spec(z0=z(LB), z1=z(0), hw=hw, planf=planf, pmin=0.5,
              bottom=[(z(s), y(h)) for s, h in c["bottom"]], top=[(z(s), y(beltf(s))) for s in ss],
              er=max(0.25, c["er"] / S), crown=max(0.1, c["crown"] / S), sill=max(1.0, sill), crease=crease, accent=accent, tubs=tubs, g=g,
              doors=doors, hood=hood, trunk=trunk, tailgate=tail, wheels=wheels, arch=0.9 if lift == 0 else 0.9,
              wells=lift == 0, mats=mats, door_xmin=wi - 0.2, chamfer=0.5, ds=0.35, dent=0.12)
    # wheel arches: the body's lower edge follows each arch (radius + clearance), so the openings are part of the
    # lofted surface (no boolean); a spat (BX) stops the arch at its skirt line
    if lift == 0 and style != "buggy":
        base_bottom = sp.bottom
        arches = []
        for w in wheels:
            zc, yc, R = w[0], w[1], w[2] + 0.9
            arches.append((zc, yc + 0.5, R, w[4] if len(w) > 4 else None))

        def bottom_arch(zz, base=base_bottom, arches=arches):
            b = base(zz)
            for zc, yc, R, spat in arches:
                dz = zz - zc
                if abs(dz) < R:
                    a = yc + math.sqrt(R * R - dz * dz)
                    if spat is not None:
                        a = min(a, spat)
                    b = max(b, a)
            return b
        sp.bottom = bottom_arch
        sp.wells = False
        sp.keys = [zc + d * R * k for zc, yc, R, sp_ in arches for d in (-1, 1) for k in (0.999, 0.97, 0.9, 0.75, 0.5, 0.25, 0.0)]
    v = H.Veh(name, F.gy)
    bvh = B.build(v, sp, hash(name) % 97)
    E = dict(F=F, c=c, sp=sp, bvh=bvh, pm=pm, hw=hw, f=f, name=name, v=v)

    # ---- lamps, grille, plates, bumpers from the reference lists
    def feats(lst, back):
        out = []
        for kind, xc, hc, w, hh in lst:
            X, Y, Wd, Hh = xc / S, y(hc), w / S, hh / S
            if kind == "H":
                out.append(("rect", X, Y, Wd, Hh, "lamp_head"))
            elif kind == "O":
                out.append(("round", X, Y, Wd, Wd, "lamp_head"))
            elif kind == "A":
                out.append(("rect", X, Y, Wd, Hh, "lamp_amber"))
            elif kind in ("T", "W"):
                out.append(("rect", X, Y, Wd, Hh, "lamp_tail"))
                if kind == "W":
                    for sg in (1, -1):
                        p, n = D.side_point(bvh, Y, z(L - 0.10), sg)
                        if p is not None:
                            D.put(v, "Lights", H.lamp_rect(p + Vector((sg * 0.05, 0, 0)), 1.6, Hh, (sg, 0, 0), depth=0.4, bevel=0.2),
                                  ["plastic", "lamp_tail"])
            elif kind == "K":                 # swept cat-eye: tilted lamp + a tail running back along the wing
                for sg in (1, -1):
                    p, n = D.front_point(bvh, sg * X, Y, sp.z1)
                    n = (n + Vector((0, 0, 1.5))).normalized()
                    lm = H.lamp_rect((0, 0, 0), Wd, Hh, (0, 0, 1), depth=0.6, bevel=Hh * 0.35)
                    xform(lm, (0, 0, 0), (0, 0, sg * math.radians(14)))
                    rot = Vector((0, 0, 1)).rotation_difference(n).to_euler()
                    xform(lm, p, tuple(rot))
                    D.put(v, "Lights", lm, ["chrome", "lamp_head"])
                    q, m = D.side_point(bvh, Y + Hh * 0.35, z(0.42), sg)
                    if q is not None:
                        D.put(v, "Lights", H.lamp_rect(q + Vector((sg * 0.03, 0, 0)), 3.2, Hh * 0.55, (sg, 0, 0), depth=0.4, bevel=0.2),
                              ["chrome", "lamp_head"])
            elif kind == "R" and not back:
                style_g = "vertical" if name in ("LanciaYpsilon", "Wagon", "Scavenger") else ("mesh" if Hh > 2.5 else "bars")
                D.grille(v, bvh, sp, Wd / 2, Y - Hh / 2, Y + Hh / 2, style_g,
                         mat="chrome" if (c.get("grilleChrome") or name in ("Pickup", "TowTruck", "Wagon", "Sedan", "Wrecker", "MonsterTruck")) else "plastic_grey",
                         frame="chrome" if style in ("pickup", "truck", "wagon") or name in ("Sedan", "Coupe", "Trabant", "LanciaYpsilon") else "plastic")
            elif kind == "M" and not back:
                p, n = D.front_point(bvh, 0, Y, sp.z1)
                D.put(v, "Body", cyl((-Wd / 2, Y, p.z + 0.15), (Wd / 2, Y, p.z + 0.15), max(0.25, Hh / 2), 8), ["chrome"])
            elif kind == "G":
                p, n = D.front_point(bvh, 0, Y, sp.z1, True)
                k = max(3, int(Hh / 0.35))
                D.put(v, "Body", join(*[rbox((0, Y - Hh / 2 + Hh * (i + 0.5) / k, p.z - 0.05), (Wd, 0.22, 0.3), 0.05, 1) for i in range(k)]), ["void"])
            elif kind == "L":
                part = "Body"
                if back and tail:
                    part = "Tailgate" if tail[0] == "green" and Y > y(beltf(L - 0.3)) - 1 else "Body"
                if back and trunk:
                    part = "Body"
                D.plate_at(v, bvh, sp, Y, back, part, w=min(Wd, 6.0), h=Hh)
            elif kind in ("B", "C") and xc == 0:
                if kind == "C" and Hh < 1.3:
                    bm, m = P.bumper_chrome(hw + 0.1, rear=back, depth=0.7, h=max(Hh, 0.7), wrap=1.4)
                else:
                    bm, m = P.bumper_plastic(hw + 0.15, rear=back, h=Hh, depth=1.3, wrap=2.6 if not back else 2.2,
                                             mat="chrome" if kind == "C" else "plastic")
                p, n = D.front_point(bvh, 0, Y, sp.z1, back)
                pos = (0, Y, p.z + (-0.45 if back else 0.45))
                D.mount(v, "Bumper_R" if back else "Bumper_F", "bumper_rear" if back else "bumper_front", "bumper_" + name.lower(), pos, bm, m)
        return out
    D.lamps(v, bvh, sp, front=feats(c["front"], False), rear=feats(c["rear"], True))

    # ---- body dressing
    if gl:
        belt_y = y(beltf((c["doors"][0][0] + c["doors"][0][1]) / 2)) if c["doors"] else y(beltf(gl[0]))
        D.door_dressing(v, bvh, sp, belt_y, mirror_z=z(gl[0]) - 0.9 if c["doors"] else False,
                        mirror_part="Body" if style == "truck" else None)
        D.wipers(v, sp, y(beltf(gl[0])) + 0.8, z(gl[0]) - 0.6, int(lerp(c["plan"], gl[0]) / S * c["shoulder"]) - 1)
        if c.get("roofRails"):
            bm, m = P.roof_rails(z(gl[1]) - z(gl[2]) - 1.0, lerp(c["plan"], 2.5) / S * c["roof"] - 1.0)
            xform(bm, (0, y(c["H"]) - 0.2, (z(gl[1]) + z(gl[2])) / 2))
            D.put(v, "Body", bm, m)
        if style in ("pickup", "wagon", "truck") and gl[3] - gl[2] <= 0.08:     # flat rear glass on the end wall
            gw = lerp(c["plan"], gl[2]) / S * c["roof"] - 2.0
            part = "Tailgate" if style == "wagon" else "Glass"
            D.tail_glass(v, gw, y(beltf(gl[2])) + 1.5, y(c["H"]) - 1.4, z(gl[2] + 0.05), part=part)
        dx = -min(5, int(hw * 0.42))
        if c.get("rhd"):
            dx = -dx
        dash_s = gl[0] + 0.30 if style not in ("truck",) else gl[0] + 0.2
        seat_room = (top(dash_s + 0.9) - 0.07 - floor_h) / S - 5.8
        D.interior(v, f, int(round(z(dash_s))), int(round(z(dash_s + 0.62))), dx, int(wi),
                   rear_bench=len(c["doors"]) > 1 or style in ("hatch", "wagon"), dash_w=min(8, int(wi) - 1),
                   back_h=max(4.0, min(7.6, seat_room)))
    if rear_engine:
        es = L - 0.45
        D.engine_at(v, "engine_2stroke", (0, min(f + 1, y(beltf(es)) - 6.8), z(es)))
    elif gl and style not in ("buggy", "pickup", "truck") and name not in ("Interceptor", "Scavenger", "Trabant", "Wagon"):
        es = max(0.4, gl[0] - 0.6)
        D.engine_at(v, "engine_i4", (0, min(f + 1, y(beltf(es)) - 6.8), z(es)))
    if c.get("deck_louvres"):
        a, b = c["deck_louvres"]
        k = 6
        bits = []
        for i in range(k):
            s = a + (b - a) * (i + 0.5) / k
            hit, n = H.surface_hit(bvh, (0, 400, z(s)), (0, -1, 0))
            if hit is not None:
                bits.append(rbox(hit + Vector((0, 0.06, 0)), (lerp(c["plan"], s) / S * 0.9, 0.12, 0.28), 0.04, 1))
        if bits:
            D.put(v, "Trunk" if trunk else "Body", join(*bits), ["void"])

    # ---- the car's own extras (archetype details, the game's default parts)
    EXTRAS.get(name, lambda e: None)(E)

    H.add_wheels(v, [("F", sxF, wyF, zF, wFkey), ("R", sxR, wyR, zRw, wRkey)])
    # report
    zf0, zr0, wy0, k0, sx0 = lay
    v.info.update(length_m=L, width_m=W, height_m=c["H"] + lift, wheelbase_m=c["wb"], track_m=tr, tyre_radius_m=c["tyre"][0],
                  archetype=c.get("archetype", name), socket_front=[round(sxF, 2), round(wyF, 2), round(zF, 2)],
                  socket_rear=[round(sxR, 2), round(wyR, 2), round(zRw, 2)], voxel_sockets=[[sx0, wy0, zf0], [sx0, wy0, zr0]],
                  voxel_wheel_radius_m=H.WHEELS[k0][0] * S)
    if outline:
        ref_outline(v, c, F)
    return v


def ref_outline(v, c, F):
    """Red reference outline: real length x height box, wheel circles at the real wheelbase, tyre size."""
    L, Hh = c["L"], c["H"] + c.get("lift", 0)
    pts = [F.p(c["W"] / 2 + 0.05, 0, 0, False), F.p(c["W"] / 2 + 0.05, Hh, 0, False), F.p(c["W"] / 2 + 0.05, Hh, L, False),
           F.p(c["W"] / 2 + 0.05, 0, L, False)]
    pts = [p + Vector((0, 0, 0)) for p in pts]
    parts = [cyl(a, b, 0.12, 4) for a, b in zip(pts, pts[1:] + pts[:1])]
    for s, tyre in ((c["fo"], c["tyre"]), (c["fo"] + c["wb"], c.get("tyre_rear", c["tyre"]))):
        cc = F.p(c["W"] / 2 + 0.05, tyre[0], s, False)
        r = tyre[0] / S
        ring = [cc + Vector((0, math.sin(a) * r, math.cos(a) * r)) for a in [i * math.pi / 18 for i in range(37)]]
        parts.append(H.tube(ring, 0.1, 4))
        parts.append(cyl(cc + Vector((0, -r - 1.5, 0)), cc + Vector((0, r + 1.5, 0)), 0.08, 4))
    H.defmat("ref_line", "flat", col="ff2a1a", rough=1.0, emit="ff2a1a", strength=2.0)
    v.part("RefOutline").put(join(*parts), "ref_line")


# ============================================================================================ per-car extras
def _mi(bm, i):
    for fc in bm.faces:
        fc.material_index = i
    return bm


def x_interceptor(e):
    F, c, v, pm, hw, f = e["F"], e["c"], e["v"], e["pm"], e["hw"], e["f"]
    for s in (c["fo"], c["fo"] + c["wb"]):
        D.flare(v, F.z(s), F.y(c["tyre"][0], False), c["tyre"][0] / S + 1.1, c["tyre"][0] / S + 2.3, hw - 0.4, ymax=F.y(0.86))
    # Pursuit Special roof spoiler and chin spoiler
    D.put(v, "Body", rbox(F.p(0, c["H"] + 0.02, 2.98), (2 * hw * 0.72, 0.5, 3.2), 0.2, 2, rot=(math.radians(8), 0, 0)), [pm])
    D.put(v, "Body", rbox(F.p(0, 0.22, 0.12), (2 * hw - 2, 0.6, 2.0), 0.2, 1), ["plastic"])
    bm, m = P.engine("engine_v8_blower")
    D.mount(v, "Engine", "engine", "engine_v8_blower", F.p(0, 0.26, 1.15), bm, m, paint=pm)
    bm, m = P.side_pipes()
    D.mount(v, "Exhaust", "exhaust", "exhaust_side_pipes", F.p(c["W"] / 2 + 0.02, 0.22, 1.25), bm, m, mirrored=True)
    bm, m = P.bumper_chrome(hw, depth=0.9, h=1.4)
    D.mount(v, "Bumper_R", "bumper_rear", "bumper_rear_chrome", F.p(0, 0.40, c["L"] + 0.04), *_rear(bm, m))
    bm, m = P.spoiler(hw)
    D.mount(v, "Spoiler", "spoiler", "spoiler_rear", F.p(0, 0.95, c["L"] - 0.10), bm, m, paint=pm)
    bm, m = P.twin_tanks()
    D.mount(v, "Cargo", "cargo", "cargo_twin_fuel_tanks", F.p(0, 0.96, c["L"] - 0.48), bm, m)


def _rear(bm, m):
    return bm, m


def x_scavenger(e):
    F, c, v, pm, hw, f, bvh, sp = e["F"], e["c"], e["v"], e["pm"], e["hw"], e["f"], e["bvh"], e["sp"]
    # armour: welded plates over the rear side windows with gun slits, mesh over the rear glass
    pl = []
    for sg in (1, -1):
        x = sg * (hw * c["roof"] - 0.1)
        pl.append(_mi(rbox((x, F.y(1.50), F.z(3.95)), (0.5, F.y(1.74) - F.y(1.26), (3.0 - 0.1) / S * 0.5 + 6), 0.15, 1), 0))
        for s in (3.4, 3.7, 4.0, 4.3):
            pl.append(_mi(rbox((x + sg * 0.3, F.y(1.52), F.z(s)), (0.3, 1.4, 0.9), 0.05, 1), 1))
    D.put(v, "Body", join(*pl), ["rusty", "void"])
    nrm = Vector((0, 0.5, 0.86)) * 0.4
    a0, a1 = F.p(-0.70, 1.25, 1.50), F.p(0.70, 1.76, 1.86)
    b0, b1 = F.p(0.70, 1.25, 1.50), F.p(-0.70, 1.76, 1.86)
    D.put(v, "Body", join(cyl(a0 + nrm, a1 + nrm, 0.3, 6), cyl(b0 + nrm, b1 + nrm, 0.3, 6)), ["steel"])
    for s in (c["fo"], c["fo"] + c["wb"]):
        D.flare(v, F.z(s), F.y(c["tyre"][0], False), c["tyre"][0] / S + 1.0, c["tyre"][0] / S + 2.4, hw - 0.3, "rusty", ymax=F.y(1.12))
    riv = [Vector((sg * (hw + 0.05), F.y(0.95), F.z(s))) for sg in (1, -1) for s in [0.4 + i * 0.25 for i in range(17)]]
    D.put(v, "Body", H.rivets(riv, 0.22), ["steel"])
    bm, m = P.bull_bar(hw)
    D.mount(v, "Bumper_F", "bumper_front", "bumper_bull_bar", F.p(0, 0.62, -0.08), bm, m)
    bm, m = P.spare_carrier(hw, "Scavenger_R")
    D.mount(v, "Bumper_R", "bumper_rear", "rear_spare_carrier", F.p(0, 0.62, c["L"] + 0.08), bm, m)
    bm, m = P.jerry_rack(hw * c["roof"] - 2.5, 9.0, 5, 0.6)
    D.mount(v, "Cargo", "cargo", "cargo_jerry_rack", F.p(0, c["H"], 3.7), bm, m)
    bm, m = P.armor_plate(10.0, 4.6)
    D.mount(v, "Armor", "armor", "armor_plate", F.p(c["W"] / 2 + 0.01, 0.82, 2.0), bm, m, mirrored=True)
    bm, m = P.turret_cannon()
    D.mount(v, "Weapon", "roof", "weapon_turret_cannon", F.p(0, c["H"] - 0.02, 2.35), bm, m)
    bm, m = P.snorkel(11.5)
    D.mount(v, "Snorkel", "snorkel", "snorkel", F.p(c["W"] / 2 + 0.02, 0.95, 1.25), bm, m)
    D.engine_at(v, "engine_diesel_i6", F.p(0, 0.55, 0.85))


def x_trabant(e):
    F, c, v, hw = e["F"], e["c"], e["v"], e["hw"]
    bm, m = P.bumper_chrome(hw, depth=0.7, h=0.9, wrap=1.2)
    D.mount(v, "Bumper_F", "bumper_front", "bumper_trabant_front", F.p(0, 0.32, -0.04), bm, m)
    bm, m = P.bumper_chrome(hw, rear=True, depth=0.7, h=0.9, wrap=1.2)
    D.mount(v, "Bumper_R", "bumper_rear", "bumper_trabant_rear", F.p(0, 0.32, c["L"] + 0.04), bm, m)
    D.engine_at(v, "engine_2stroke", F.p(0, 0.30, 0.55))


def _pickup_common(e, cargo, rear_key, front_key="bumper_chrome", engine=True, rear="spare"):
    F, c, v, hw, bvh = e["F"], e["c"], e["v"], e["hw"], e["bvh"]
    b0, b1 = c["bed"]
    floor = F.y(c["belt"][-1][1] - 0.44)
    D.put(v, "Body", join(*[rbox((x, floor + 0.12, F.z((b0 + b1) / 2)), (1.2, 0.2, (b1 - b0) / S - 1.0), 0.05, 1)
                            for x in range(-int(hw) + 2, int(hw) - 1, 2)]), ["wood"])
    # wheel-arch tubs inside the bed and stake pockets on the rails
    for sg in (1, -1):
        D.put(v, "Body", join(*[rbox((sg * (hw - 0.4), F.y(1.21), F.z(s)), (0.5, 0.15, 0.9), 0.05, 1) for s in (b0 + 0.3, (b0 + b1) / 2, b1 - 0.3)]),
              ["void"])
    if front_key == "bumper_chrome":
        bm, m = P.bumper_chrome(hw, depth=1.1, h=2.0, wrap=1.0)
    else:
        bm, m = P.bumper_winch(hw)
    D.mount(v, "Bumper_F", "bumper_front", front_key, F.p(0, 0.58, -0.06), bm, m)
    D.plate_at(v, bvh, e["sp"], F.y(0.58), False, "Bumper_F", z=F.z(-0.12))
    if rear == "spare":
        bm, m = P.spare_carrier(hw, rear_key)
        D.mount(v, "Bumper_R", "bumper_rear", "rear_spare_carrier", F.p(0, 0.58, c["L"] + 0.08), bm, m)
    else:
        bm, m = P.bumper_chrome(hw, rear=True, depth=1.2, h=1.8, wrap=1.0)
        D.mount(v, "Bumper_R", "bumper_rear", "bumper_rear_chrome", F.p(0, 0.58, c["L"] + 0.06), bm, m)
    D.plate_at(v, bvh, e["sp"], F.y(0.70), True)
    if engine:
        D.engine_at(v, "engine_i6", F.p(0, 0.62, 0.9))
    if cargo == "jerry":
        bm, m = P.jerry_rack(hw - 2.5, 4.5, 4, 0.6)
        D.mount(v, "Cargo", "cargo", "cargo_jerry_rack", (0, floor + 0.2, F.z(b1 - 0.7)), bm, m)
    else:
        bm, m = P.crane(30)
        D.mount(v, "Cargo", "cargo", "cargo_crane", (0, floor + 0.2, F.z((b0 + b1) / 2 + 0.2)), bm, m)


def x_pickup(e):
    _pickup_common(e, "jerry", "Pickup_R")


def x_towtruck(e):
    _pickup_common(e, "crane", "TowTruck_R", "bumper_winch")
    F, c = e["F"], e["c"]
    bm, m = P.beacon()
    D.put(e["v"], "Lights", xform(bm, F.p(0, c["H"] + 0.02, 2.2)), m)


def x_monster(e):
    F, c, v, hw, pm = e["F"], e["c"], e["v"], e["hw"], e["pm"]
    _pickup_common(e, "jerry", "MonsterTruck_R", "bumper_chrome", engine=False, rear="chrome")
    lift = c["lift"]
    R = c["tyre"][0]
    ch = [rbox((sg * 0.5 / S, F.y(R + 0.15, False), F.z(c["L"] / 2)), (2.0, 3.6, c["L"] / S - 6), 0.3, 1) for sg in (1, -1)]
    ch += [rbox((0, F.y(R + 0.15, False), F.z(s)), (12.0, 2.0, 1.2), 0.2, 1) for s in (0.6, 1.4, 2.4, 3.4, 4.2)]
    D.put(v, "Body", join(*ch), ["steel_dark"])
    sus = []
    for s in (c["fo"], c["fo"] + c["wb"]):
        zc = F.z(s)
        yc = F.y(R, False)
        for sg in (1, -1):
            sus.append(_mi(cyl((sg * 6, F.y(R + 0.65, False), zc + 5), (sg * 11, yc + 1.5, zc), 0.9, 10), 0))
            sus.append(_mi(cyl((sg * 6, F.y(R + 0.65, False), zc + 5), (sg * 8.5, yc + 4.5, zc + 2.5), 1.2, 12), 1))
            sus.append(_mi(cyl((sg * 6, F.y(R + 0.10, False), zc - 7), (sg * 10, yc, zc - 1), 0.5, 8), 2))
        sus.append(_mi(cyl((-12, yc, zc), (12, yc, zc), 1.0, 10), 2))
        sus.append(_mi(H.revolve([(-2.0, 0), (-2.0, 2.4), (2.0, 2.4), (2.0, 0)], 16, origin=(0, yc, zc), axis=(1, 0, 0)), 2))
    D.put(v, "Body", join(*sus), ["chrome", "hazard", "steel_dark"])
    for sg in (1, -1):
        D.put(v, "Body", tube([F.p(sg * 0.85, 1.25, 2.70), F.p(sg * 0.85, 1.95, 2.75)], 0.6, 10), ["chrome"])
    D.put(v, "Body", tube([F.p(-0.85, 1.95, 2.75), F.p(0.85, 1.95, 2.75)], 0.6, 10), ["chrome"])
    bm, m = P.engine("engine_v8_blower")
    D.mount(v, "Engine", "engine", "engine_v8_blower", F.p(0, 0.75, 0.75), bm, m, paint="engine_paint")
    bm, m = P.side_pipes()
    D.mount(v, "Exhaust", "exhaust", "exhaust_side_pipes", F.p(c["W"] / 2 + 0.02, 0.62, 1.40), bm, m, mirrored=True)
    bm, m = P.lights_bar(8.0)
    D.mount(v, "LightsBar", "lights", "lights_bar", F.p(0, c["H"], 2.2), bm, m)
    bm, m = P.steps_side(8.0)
    D.mount(v, "Steps", "steps", "steps_side", F.p(c["W"] / 2, 0.35, 2.05), bm, m, mirrored=True)


def x_wrecker(e):
    F, c, v, hw, pm = e["F"], e["c"], e["v"], e["hw"], e["pm"]
    L = c["L"]
    R = c["tyre"][0]
    st = [rbox((sg * 0.48 / S, F.y(0.80), F.z(3.6)), (2.6, 3.4, (L - 0.4) / S), 0.3, 1) for sg in (1, -1)]
    D.put(v, "Body", join(*st), ["steel_dark"])
    # front fenders (flared, separate from the narrow hood) and a step
    for sg in (1, -1):
        pts = []
        zc, yc = F.z(c["fo"]), F.y(R, False)
        for i in range(0, 181, 9):
            a = math.radians(i)
            pts.append((sg * (c["track"] / 2 / S), yc + math.sin(a) * (R / S + 1.2), zc + math.cos(a) * (R / S + 1.2) * 1.05))
        D.put(v, "Body", H.sweep(pts, H.rrect(4.2, 0.4, 0.15, 2), up=(1, 0, 0)), [pm])
        D.put(v, "Body", rbox((sg * (hw - 0.6), F.y(0.75), F.z(2.4)), (2.0, 0.3, 4.0), 0.1, 1), ["steel"])
    bed = [rbox((0, F.y(1.10), F.z((3.1 + L) / 2)), ((c["W"]) / S, 2.0, (L - 3.1) / S), 0.15, 1)]
    for s in [3.2 + i * 0.25 for i in range(int((L - 3.2) / 0.25))]:
        bed.append(rbox((0, F.y(1.10) + 1.05, F.z(s)), (c["W"] / S - 0.5, 0.12, 0.3), 0.0))
    D.put(v, "Body", join(*bed), ["steel"])
    rails = []
    for sg in (1, -1):
        rails.append(rbox((sg * (c["W"] / 2 / S - 0.3), F.y(1.30), F.z((3.1 + L) / 2)), (0.5, 2.4, (L - 3.1) / S), 0.1, 1))
        zc, yc = F.z(c["fo"] + c["wb"]), F.y(R, False)
        pts = [(sg * (c["track"] / 2 / S), yc + math.sin(math.radians(a)) * (R / S + 1.3), zc + math.cos(math.radians(a)) * (R / S + 1.3))
               for a in range(0, 181, 10)]
        rails.append(H.sweep(pts, H.rrect(4.0, 0.3, 0.1, 1), up=(1, 0, 0)))
    D.put(v, "Body", join(*rails), ["rusty"])
    D.put(v, "Body", rbox(F.p(0, 1.95, 3.08), (c["W"] / S - 1, 5.5, 0.6), 0.15, 1), ["steel_dark"])          # headboard
    D.put(v, "Body", H.revolve([(-4, 0), (-4, 1.9), (4, 1.9), (4, 0)], 20, origin=F.p(-0.95, 0.85, 2.5), axis=(0, 0, 1)), ["chrome"])
    D.put(v, "Body", join(cyl(F.p(0.95, 1.2, 2.98), F.p(0.95, 2.95, 2.98), 0.45, 10)), ["chrome"])          # exhaust stack
    for sg in (1, -1):
        D.put(v, "Lights", H.lamp_rect(F.p(sg * 0.95, 1.05, L + 0.02), 2.0, 1.0, (0, 0, -1)), ["plastic", "lamp_tail"])
        D.put(v, "Body", join(cyl(F.p(sg * 1.02, 2.1, 2.20), F.p(sg * 1.25, 2.1, 2.25), 0.2, 5),
                              rbox(F.p(sg * 1.28, 2.15, 2.25), (0.6, 4.6, 2.2), 0.2, 1)), ["chrome"])
    bm, m = P.beacon()
    D.put(v, "Lights", xform(bm, F.p(0, c["H"] + 0.02, 2.6)), m)
    D.put(v, "Body", rbox(F.p(0, c["H"] - 0.02, 2.58), (c["W"] / S - 3, 0.6, 9.0), 0.2, 1), ["steel_dark"])    # roof visor
    D.plate_at(v, e["bvh"], e["sp"], F.y(0.9), True, z=F.z(L + 0.03))
    D.engine_at(v, "engine_diesel_i6", F.p(0, 0.95, 1.0))
    D.interior(v, int(F.y(1.10)), int(F.z(2.15)), int(F.z(2.78)), 4, 10)
    bm, m = P.bumper_winch(hw)
    D.mount(v, "Bumper_F", "bumper_front", "bumper_winch", F.p(0, 0.72, -0.10), bm, m)
    bm, m = P.crane(36)
    D.mount(v, "Cargo", "cargo", "cargo_crane", F.p(0, 1.24, L - 1.6), bm, m)


def x_buggy(e):
    F, c, v, hw, pm = e["F"], e["c"], e["v"], e["hw"], e["pm"]
    t = []
    r = 0.55
    for sg in (1, -1):
        x = sg * 0.56 / S
        t += [tube([F.p(sg * 0.55, 0.62, 1.10), F.p(sg * 0.50, 1.05, 1.18)], r, 8),               # windscreen frame
              tube([F.p(sg * 0.60, 0.62, 2.35), F.p(sg * 0.55, 1.32, 2.35)], r, 8),               # roll hoop
              tube([F.p(sg * 0.55, 1.32, 2.35), F.p(sg * 0.40, 0.66, 3.20)], r, 8)]               # rear stays
    t += [tube([F.p(-0.55, 1.32, 2.35), F.p(0.55, 1.32, 2.35)], r, 8), tube([F.p(-0.50, 1.05, 1.18), F.p(0.50, 1.05, 1.18)], 0.4, 8),
          tube([F.p(-0.40, 0.66, 3.20), F.p(0.40, 0.66, 3.20)], r, 8)]
    D.put(v, "Body", join(*t), ["cage_black"])
    D.put(v, "Glass", join(rbox(F.p(0, 0.85, 1.14), (2 * 0.50 / S, 0.40 / S, 0.15), 0.3, 2, rot=(math.radians(-12), 0, 0))), ["glass"])
    # Manx fenders: flared fibreglass arches over the exposed wheels, running into the nose / rear deck
    for s, tyre, a0, a1 in ((c["fo"], c["tyre"], 0, 200), (c["fo"] + c["wb"], c["tyre_rear"], -10, 180)):
        R = tyre[0] / S + 0.9
        zc, yc = F.z(s), F.y(tyre[0], False)
        for sg in (1, -1):
            pts = []
            for i in range(a0, a1 + 1, 8):
                a = math.radians(i)
                pts.append((sg * (c["track"] / 2 / S - 0.3), yc + math.sin(a) * R, zc + math.cos(a) * R * 1.08))
            D.put(v, "Body", H.sweep(pts, H.rrect(tyre[1] / S + 1.4, 0.45, 0.2, 2), up=(1, 0, 0)), [pm])
    D.put(v, "Hood", rbox(F.p(0, 0.69, 0.55), (6.0, 0.6, 4.4), 0.3, 2), ["void"])
    D.interior(v, int(F.y(0.40)), int(F.z(1.30)), int(F.z(1.95)), -4, 7, bucket=True, dash_w=5)
    bm, m = P.engine("engine_i4")
    D.mount(v, "Engine", "engine", "engine_i4", F.p(0, 0.38, 3.02), bm, m, paint="engine_paint")
    D.put(v, "Engine", join(tube([F.p(0.12, 0.65, 3.0), F.p(0.30, 0.70, 3.3), F.p(0.35, 0.68, 3.55)], 0.35, 8)), ["rusty"])
    bm, m = P.bull_bar(8.0)
    D.mount(v, "Bumper_F", "bumper_front", "bumper_bull_bar", F.p(0, 0.38, -0.08), bm, m)
    bm, m = P.lights_bar(6.0)
    D.mount(v, "LightsBar", "lights", "lights_bar", F.p(0, 1.32, 2.35), bm, m)


def x_coupe(e):
    F, c, v, pm, bvh = e["F"], e["c"], e["v"], e["pm"], e["bvh"]
    hit, n = H.surface_hit(bvh, (0, 400, F.z(1.0)), (0, -1, 0))
    if hit is not None:
        D.put(v, "Hood", rbox(hit + Vector((0, 0.2, 0)), (6.0, 0.5, 9.0), 0.4, 3), [pm])                 # power bulge
    for sg in (1, -1):
        p, n2 = D.side_point(bvh, F.y(0.68), F.z(3.35), sg)
        if p is not None:
            D.put(v, "Body", join(*[rbox(p + Vector((sg * 0.05, -0.4 * i, 0.3 * i)), (0.25, 0.25, 1.6), 0.05, 1) for i in range(3)]), ["void"])
    bm, m = P.bumper_chrome(e["hw"], depth=0.7, h=0.9, wrap=1.6)
    D.mount(v, "Bumper_F", "bumper_front", "bumper_chrome", F.p(0, 0.42, -0.04), bm, m)
    bm, m = P.bumper_chrome(e["hw"], rear=True, depth=0.7, h=0.9, wrap=1.6)
    D.mount(v, "Bumper_R", "bumper_rear", "bumper_rear_chrome", F.p(0, 0.45, c["L"] + 0.04), bm, m)


def x_sedan(e):
    F, c, v, hw = e["F"], e["c"], e["v"], e["hw"]
    bm, m = P.bumper_plastic(hw + 0.1, h=1.8, depth=1.6, wrap=2.0, mat="plastic")
    D.mount(v, "Bumper_F", "bumper_front", "bumper_chrome", F.p(0, 0.48, -0.10), bm, m)
    bm, m = P.bumper_plastic(hw + 0.1, rear=True, h=1.8, depth=1.6, wrap=2.0, mat="plastic")
    D.mount(v, "Bumper_R", "bumper_rear", "bumper_rear_chrome", F.p(0, 0.50, c["L"] + 0.10), bm, m)
    D.plate_at(v, e["bvh"], e["sp"], F.y(0.48), False, "Bumper_F", z=F.z(-0.24))


def x_wagon(e):
    F, c, v, hw = e["F"], e["c"], e["v"], e["hw"]
    bm, m = P.bumper_chrome(hw, depth=1.0, h=1.8, wrap=1.0)
    D.mount(v, "Bumper_F", "bumper_front", "bumper_chrome", F.p(0, 0.62, -0.06), bm, m)
    bm, m = P.bumper_chrome(hw, rear=True, depth=1.0, h=1.8, wrap=1.0)
    D.mount(v, "Bumper_R", "bumper_rear", "bumper_rear_chrome", F.p(0, 0.62, c["L"] + 0.06), bm, m)
    D.plate_at(v, e["bvh"], e["sp"], F.y(0.80), True, "Tailgate")
    # woody frame strips along the panel edges
    lo, hi = c["woody"]
    strips = []
    for sg in (1, -1):
        for h in (lo, hi):
            pts = []
            for s in [0.4 + i * 0.1 for i in range(int((c["L"] - 0.7) / 0.1))]:
                p, n = D.side_point(e["bvh"], F.y(h), F.z(s), sg)
                if p is not None:
                    pts.append(p + Vector((sg * 0.1, 0, 0)))
            if len(pts) > 3:
                strips.append(H.sweep(pts, H.rrect(0.3, 0.9, 0.1, 1), up=(1, 0, 0)))
    if strips:
        D.put(v, "Body", join(*strips), ["wood"])
    bm, m = P.jerry_rack(hw * c["roof"] - 2.5, 10.0, 5, 1.0)
    D.mount(v, "Cargo", "cargo", "cargo_jerry_rack", F.p(0, c["H"], 3.2), bm, m)
    D.engine_at(v, "engine_i6", F.p(0, 0.60, 0.80))


def x_multipla(e):
    pass


EXTRAS = {"Interceptor": x_interceptor, "Scavenger": x_scavenger, "Trabant": x_trabant, "Pickup": x_pickup, "TowTruck": x_towtruck,
          "MonsterTruck": x_monster, "Wrecker": x_wrecker, "DuneBuggy": x_buggy, "Coupe": x_coupe, "Sedan": x_sedan, "Wagon": x_wagon}


def x_406(e):
    F, c, v = e["F"], e["c"], e["v"]
    bm, m = P.jerry_rack(e["hw"] * c["roof"] - 2.5, 11.0, 5, 1.2)
    D.mount(v, "Cargo", "cargo", "cargo_jerry_rack", F.p(0, c["H"], 3.0), bm, m)


EXTRAS["Peugeot406Break"] = x_406

ROSTER = {n: (lambda n=n: build(n)) for n in CARS}
