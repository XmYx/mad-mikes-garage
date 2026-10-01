"""HD animals for every species of AnimalLibrary.cs / AnimalLibrary.Wild.cs, rigged like Animal.cs poses them.

Hierarchy (names as in the game's Rig): <species id> (root empty) / Rig (armature) / bones + rigid parts parented to them:
  quadrupeds, birds, arthropods: Body, Head (pivot = AnimalMeshes.neck), Leg_<i> (pivot = legRoots[i]; quadrupeds
  FL FR BL BR = 0..3, birds L R, arthropods pairs front to back, left first), Tail (tailRoot), WingR / WingL (wingRoot,
  WingL mirrored), Saddle (horse, at (0, (leg + depth) * s, -0.05)), Fleece (sheep, at the torso centre);
  snakes: Seg_<i> (0, 0, -i * segLen), Head (origin), Rattle (child of the last segment).
Every part object carries custom props part (object name), rig_part (Leg / Seg / ...), index and pivot_game_m; the pivots come from the voxel dump
(ref_index.json "animal:<id>"), the proportions from the AnimalDef numbers. Coat / belly / accent colours are the
species' Pal ramps. Sprawled legs (lizards, arthropods) are modelled for the right side; left legs are separate
mirrored objects (the game mirrors the right leg with scale x = -1).

blender -b -P animals.py -- [out_dir] [-only tile,tile]"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "world"))
import bpy  # noqa: E402
import hdkit as K  # noqa: E402
from hdkit import Asset, Vector, Matrix  # noqa: E402


def D(sid):
    return K.REF["animal:" + sid]


def ramp(s):
    hexes, bias = s.split("/")
    hexes = hexes.split("-")
    return hexes, int(bias)


def species_mats(d):
    """Coat / belly / accent / dark materials from the species' ramps."""
    out = {}
    for key in ("coat", "belly", "accent"):
        hexes, bias = ramp(d[key])
        base = K.hexc(hexes[min(len(hexes) - 1, bias + (1 if key != "accent" else 0))])
        name = "%s_%s" % (d["id"], key)
        if name not in K.M:
            K.M[name] = K.kit.surface("Fur_" + name, base, dust=0.12, rough=0.92, var=0.2, bump=0.55, scale=3.0)
        out[key] = name
    K.M.setdefault("eye", K.kit.surface("Eye", K.hexc("0d0d10"), dust=0.0, rough=0.15, var=0.0, bump=0.0))
    K.M.setdefault("eye_glow", K.kit.emissive("EyeGlow", K.hexc("ffd15a"), 4.0))
    K.M.setdefault("nose", K.kit.surface("Nose", K.hexc("18171c"), dust=0.0, rough=0.4, var=0.1, bump=0.2))
    K.M.setdefault("pink", K.kit.surface("SkinPink", K.hexc("c07a70"), dust=0.05, rough=0.6, var=0.15, bump=0.2))
    K.M.setdefault("hoof", K.kit.surface("Hoof", K.hexc("24211f"), dust=0.2, rough=0.5, var=0.15, bump=0.3))
    K.M.setdefault("horn", K.kit.surface("Horn", K.hexc("6b625a"), dust=0.15, rough=0.5, var=0.2, bump=0.4))
    K.M.setdefault("horn_pale", K.kit.surface("HornPale", K.hexc("dcd8c8"), dust=0.15, rough=0.5, var=0.15, bump=0.4))
    K.M.setdefault("beak", K.kit.surface("Beak", K.hexc("c48c2a"), dust=0.1, rough=0.45, var=0.15, bump=0.2))
    K.M.setdefault("beak_dark", K.kit.surface("BeakDark", K.hexc("4f4842"), dust=0.1, rough=0.45, var=0.15, bump=0.2))
    K.M.setdefault("wool", K.kit.textured("Wool", "stone", K.hexc("dcd8c8"), K.hexc("eeeadc"), K.hexc("c4c0b0"), scale=6.0, dust=0.2, rough=0.98, bump=1.0))
    return out


# ------------------------------------------------------------------------------------------ rig
def make_rig(a, root, bones):
    """bones: [(name, head_game, tail_game, parent_name or None)]; parts are parented to the bone of the same name
    (part name minus a mirror suffix is matched by a.bone_of[part])."""
    arm = bpy.data.armatures.new(a.gid + "_Rig")
    ob = bpy.data.objects.new("Rig", arm)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = root
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    ebs = {}
    for (name, h, t, par) in bones:
        eb = arm.edit_bones.new(name)
        eb.head = K.g2b(h)
        eb.tail = K.g2b(t)
        if par:
            eb.parent = ebs[par]
        ebs[name] = eb
    bpy.ops.object.mode_set(mode="OBJECT")
    ob.select_set(False)
    bpy.context.view_layer.update()
    for part in list(root.children):
        if part == ob or part.type != "MESH":
            continue
        bone = a.bone_of.get(part.get("part", part.name))
        if not bone:
            continue
        mw = part.matrix_world.copy()
        part.parent = ob
        part.parent_type = "BONE"
        part.parent_bone = bone
        bpy.context.view_layer.update()
        part.matrix_world = mw
    ob["rig"] = "MadMax.Animals.Animal Rig"
    return ob


def finish(a, bones, parts_meta):
    """Attach the rig builder and the part metadata to an Asset."""
    a.bone_of = {}
    for (pname, bone, part, index) in parts_meta:
        a.bone_of[pname] = bone

    def post(asset, root):
        for ob in asset.objs:
            meta = [m for m in parts_meta if m[0] == ob["part"]]
            if meta:
                ob["rig_part"] = meta[0][2]          # Animal rig role: Body / Head / Leg / Tail / Wing / Seg / Rattle / Saddle / Fleece
                ob["index"] = meta[0][3]
                ob["pivot_game_m"] = list(asset.parts[ob["part"]].pivot)
        make_rig(asset, root, bones)
        d = K.REF.get("animal:" + asset.gid)
        if d:
            root["ref_height"] = d["height"]
            root["species_name"] = d["name"]
    a.post = post
    return a


# ------------------------------------------------------------------------------------------ shared shapes
def ellipsoid_loft(p, mat_fn, cx, cy, cz, rx, ry, rz, power=2.4, n=20, rings=14, along="z", shape=None):
    """Superellipsoid as a loft of rings along an axis; shape(t) -> (sx, sy, dy) per ring modulates the section."""
    secs = []
    for k in range(rings + 1):
        t = -1 + 2 * k / rings
        f = max(0.0, 1 - abs(t) ** power) ** (1 / power)
        sx, sy, dy = shape(t) if shape else (1, 1, 0)
        hw, hh = max(rx * f * sx, 1e-4), max(ry * f * sy, 1e-4)
        sec = []
        for j in range(n):
            an = j / n * math.tau
            co, si = math.cos(an), math.sin(an)
            u = hw * math.copysign(abs(co) ** (2 / power), co)
            w = hh * math.copysign(abs(si) ** (2 / power), si)
            if along == "z":
                sec.append((cx + u, cy + dy + w, cz + rz * t))
            else:
                sec.append((cx + u, cy + rz * t, cz + w))
        secs.append(sec)
    p.add_split(K.kit.bm_loft(secs, True), mat_fn)


def tube_part(p, m, pts, radii, segs=10):
    p.tube(m, [Vector(q) for q in pts], radii, segs)


def eye(p, at, r, glow=False):
    p.sphere("eye_glow" if glow else "eye", r, at, (1, 1, 0.8), 10, 6)


# ------------------------------------------------------------------------------------------ quadrupeds
def quadruped(sid):
    d = D(sid)
    s = 0.08 * d["scale"]
    Lv, Dv, Wv, Lg = d["len"], d["depth"], d["width"], d["leg"]
    feat = d["features"].split()
    has = lambda f: f in feat
    a = Asset(sid, "Animal", kind="animal", voxel=s)
    mats = species_mats(d)
    coat, belly, accent = mats["coat"], mats["belly"], mats["accent"]
    lizard = has("lizard") or d["sprawl"]
    paws = d["nature"] == "Predator" or sid in ("guarddog", "rat", "dog", "wolf", "coyote", "bear", "raccoon")
    cy = (Lg + Dv * 0.5) * s
    body = a.p("Body", "Hide")
    belly_y = cy - Dv * 0.2 * s

    def body_mat(c):
        if has("spots") and K.pal_hash(int(c.x / s / 2.5), int(c.y / s / 2.5), int(c.z / s / 2.5), 77) > 0.6:
            return accent
        if has("shell") and c.y > belly_y:
            return accent if int((c.z / s + 64) // 1.5) % 2 == 0 else coat
        if has("beads") and c.y > belly_y and K.pal_hash(int(c.x / s * 2), int(c.y / s * 2), int(c.z / s * 2), 5) > 0.6:
            return accent
        if "boar" in sid and abs(c.x) < 0.04 and c.y > cy + Dv * 0.3 * s:
            return accent
        return belly if c.y < belly_y else coat
    # torso: barrel, deeper chest at the front, rounder rump
    def shape(t):
        chest = max(0.0, 1 - abs(t - 0.55) / 0.5)
        rump = max(0.0, 1 - abs(t + 0.6) / 0.4)
        return (1 + 0.06 * chest + 0.05 * rump, 1 + 0.1 * chest, 0.3 * s * chest)
    flat = 0.75 if lizard else 1.0
    ellipsoid_loft(body, body_mat, 0, cy, 0, Wv * 0.52 * s, Dv * 0.5 * s * flat, Lv * 0.5 * s + 0.02, 2.4, 22, 16, shape=shape)
    if has("udder"):
        body.sphere("pink", 1.2 * s, (0, Lg * s + 0.02, (-Lv / 2 + 4) * s), (1.2, 0.8, 1.1), 10, 6)
        for k in range(4):
            body.cyl("pink", 0.2 * s, 0.8 * s, ((k % 2 - 0.5) * 1.0 * s, Lg * s - 0.3 * s, (-Lv / 2 + 3.5 + (k // 2)) * s), "y", 6, 0.0)
    if has("shell"):
        for k in range(int(Lv * 0.8)):
            z = (-Lv * 0.4 + k) * s
            body.torus(accent, Wv * 0.48 * s, 0.25 * s, (0, cy + 0.2 * s, z), "z", 18, 4)
    if has("fleece"):
        pass                                                   # the separate Fleece part covers the body
    parts = [("Body", "Body", "Body", 0)]

    # head + neck (pivot at AnimalMeshes.neck)
    nk = Vector(d["neck_pivot"])
    head = a.p("Head", "Hide", pivot=tuple(nk))
    hs = d["head"]
    ne = Vector((0, 0, 1.5)) if d["neck"] <= 1 else Vector((0, d["neck"] * 0.78, d["neck"] * 0.62))
    nr = max(1.0, min(Wv, Dv) * 0.34)
    hc = ne + Vector((0, hs * 0.15, hs * 0.35))
    P = lambda v: tuple(nk + v * s)
    tube_part(head, coat, [P(Vector((0, -0.6, -1.0))), P(ne * 0.5), P(ne)], [nr * 1.3 * s, nr * 1.05 * s, nr * 0.9 * s], 12)
    head.add_split(K.kit.bm_sphere(1.0, 16, 10), lambda c: coat, Matrix.Translation(P(hc)) @ Matrix.Diagonal((hs * 0.42 * s, hs * 0.48 * s * 0.9, hs * 0.55 * s, 1)) @ K._axis_mtx("y"))
    sz0, sz1 = hc.z + hs * 0.25, hc.z + hs * 0.35 + d["snout"]
    sy = hc.y - hs * 0.2
    sw = max(0.8, hs * 0.28)
    snout_m = "pink" if sid in ("pig",) else coat
    head.superloft(snout_m, [(sz0 * s + nk.z, nk.x, (sy + 0.2) * s + nk.y, sw * 1.15 * s, 1.35 * s), (((sz0 + sz1) / 2) * s + nk.z, nk.x, sy * s + nk.y, sw * s, 1.15 * s),
                             (sz1 * s + nk.z, nk.x, (sy - 0.15) * s + nk.y, sw * 0.85 * s, 0.95 * s), (sz1 * s + 0.15 * s + nk.z, nk.x, (sy - 0.2) * s + nk.y, sw * 0.5 * s, 0.6 * s)], 14, 2.2)
    nose_m = "pink" if ("boar" in sid or sid == "pig") else "nose"
    head.sphere(nose_m, sw * 0.75 * s, P(Vector((0, sy - 0.1, sz1 + 0.05))), (1.1, 0.75, 0.45), 10, 6)
    for sx in (-1, 1):
        eye(head, P(Vector((sx * hs * 0.4, hc.y + hs * 0.12, hc.z + hs * 0.18))), max(0.35, hs * 0.11) * s, has("glow"))
        top = hc.y + hs * 0.42
        ez0 = hc.z - hs * 0.2
        x = sx * max(1.0, hs * 0.3)
        if has("ears_tall"):
            head.superloft(coat, [(top * s + nk.y, x * s, ez0 * s + nk.z, 0.45 * s, 0.25 * s), ((top + 2.2) * s + nk.y, x * 1.15 * s, (ez0 - 0.4) * s + nk.z, 0.6 * s, 0.22 * s),
                                  ((top + 4.2) * s + nk.y, x * 1.25 * s, (ez0 - 0.8) * s + nk.z, 0.15 * s, 0.1 * s)], 10, 2.0, axis="y")
        elif has("ears_long"):
            head.add(K.kit.bm_sphere(1.0, 10, 6), coat, Matrix.Translation(P(Vector((x * 1.6, top - 1.2, ez0 + 0.4)))) @ K.rot(0, 0, sx * 60) @ Matrix.Diagonal((0.5 * s, 1.5 * s, 0.9 * s, 1)))
        elif has("ears_up"):
            head.add(K.kit.bm_cyl(0.6 * s, 1.8 * s, 6, 0.0, 0.05 * s), coat, Matrix.Translation(P(Vector((x, top + 0.8, ez0)))) @ K.rot(-10, 0, sx * 12) @ Matrix.Diagonal((1, 0.5, 1, 1)) @ K._axis_mtx("y"))
        if has("horns"):
            hm = "horn_pale" if sid == "cow" else "horn"
            if sid == "antelope":
                pts = [Vector((x, top, ez0 + 1)), Vector((x * 1.15, top + 3, ez0 - 1)), Vector((x * 1.3, top + 6, ez0 - 3))]
                head.tube(hm, [P(q) for q in pts], [0.55 * s, 0.4 * s, 0.1 * s], 8)
                for k in range(5):
                    q = pts[0] + (pts[1] - pts[0]) * (k / 4) * 1.5
                    head.torus(hm, 0.5 * s, 0.12 * s, P(q), "y", 8, 3)
            elif sid == "goat":
                head.tube(hm, [P(Vector((x, top, ez0 + 1))), P(Vector((x, top + 3, ez0 - 1))), P(Vector((x * 1.4, top + 1, ez0 - 3)))], [0.6 * s, 0.45 * s, 0.12 * s], 8)
            else:
                head.tube(hm, [P(Vector((x, top - 1, ez0 + 1))), P(Vector((x * 2.0, top - 0.2, ez0 + 1.2))), P(Vector((x * 2.4, top + 1, ez0 + 1.6)))], [0.6 * s, 0.45 * s, 0.1 * s], 8)
        if has("antlers"):
            b0, b1, b2 = Vector((x, top, ez0)), Vector((x * 2.2, top + 4, ez0 - 1)), Vector((x * 2.6, top + 7, ez0 - 3))
            am = K.wood("light", "y")
            head.tube(am, [P(b0), P(b1), P(b2)], [0.5 * s, 0.4 * s, 0.15 * s], 7)
            head.tube(am, [P(b1), P(b1 + Vector((0, 2, 2)))], [0.35 * s, 0.1 * s], 6)
            mid = b1.lerp(b2, 0.6)
            head.tube(am, [P(mid), P(mid + Vector((-x * 0.3, 2, 1.5)))], [0.3 * s, 0.08 * s], 6)
        if has("mask"):
            head.sphere(accent, hs * 0.16 * s, P(Vector((sx * hs * 0.38, hc.y + hs * 0.12, hc.z + hs * 0.14))), (1.4, 0.8, 0.6), 8, 5)
        if has("tusks"):
            tm = "glow_green" if has("glow") else "horn_pale"
            head.tube(tm, [P(Vector((sx * (sw + 0.3), sy - 0.8, sz1 - 0.8))), P(Vector((sx * (sw + 1.2), sy + 1.2, sz1 - 1.6))), P(Vector((sx * (sw + 1.0), sy + 2.2, sz1 - 2.2)))], [0.45 * s, 0.3 * s, 0.05 * s], 7)
    if has("mane"):
        for k in range(d["neck"] + 2):
            t = k / (d["neck"] + 1)
            q = Vector((0, nr + 0.4, -0.5)).lerp(ne + Vector((0, nr + 0.4, -0.5)), t)
            head.add(K.kit.bm_box(0.35 * s, 1.8 * s, 1.3 * s, bevel=0.1 * s), accent, Matrix.Translation(P(q)) @ K.rot(-35 + k * 3, 0, 0))
        head.add(K.kit.bm_box(0.4 * s, 1.0 * s, 1.6 * s, bevel=0.1 * s), accent, Matrix.Translation(P(Vector((0, hc.y + hs * 0.45, hc.z + 0.3)))) @ K.rot(30, 0, 0))
    if has("beard"):
        head.add(K.kit.bm_cyl(0.5 * s, 2.2 * s, 6, 0.0, 0.1 * s), accent, Matrix.Translation(P(Vector((0, sy - 2.2, sz0 + 0.5)))) @ K._axis_mtx("y"))
    parts.append(("Head", "Head", "Head", 0))

    # legs (pivots from legRoots): FL FR BL BR
    roots = [Vector(r) for r in d["legRoots"]]
    up = round(Dv * 0.35)
    th = 1 if Wv >= 6 else 0
    names = ["Leg_FL", "Leg_FR", "Leg_BL", "Leg_BR"]
    for i, (rt, nm) in enumerate(zip(roots, names)):
        lg = a.p(nm, "Hide", pivot=tuple(rt))
        front = i < 2
        Q = lambda v: tuple(rt + v * s)
        if lizard:
            right = rt.x > 0
            sx = 1 if right else -1
            reach = max(2.0, Wv * 0.45)
            fan = math.radians([30, 30, -25, -25][i])
            dvec = Vector((math.cos(fan) * sx, 0, math.sin(fan)))
            elbow = dvec * reach + Vector((0, 0.6, 0))
            foot = dvec * (reach + 0.8) + Vector((0, -Lg, 0.4))
            lr_ = max(0.75, Wv * 0.16)
            lg.tube(coat, [Q(Vector((0, 0, 0))), Q(elbow), Q(foot)], [lr_ * s, lr_ * 0.8 * s, lr_ * 0.55 * s], 12)
            lg.sphere(coat, lr_ * 0.85 * s, Q(elbow), (1, 1, 1), 10, 6)
            for k in range(4):                              # splayed toes
                an = fan + (k - 1.5) * 0.4
                toe = foot + Vector((math.cos(an) * sx * 1.4, 0.0, math.sin(an) * 1.4 + 0.6))
                lg.tube(accent, [Q(foot), Q(toe)], [max(0.2, Wv * 0.04) * s, 0.1 * s], 6)
        else:
            rad_u = (th + 0.9) * s * (1.25 if not paws else 1.1)
            rad_l = max(0.32, th * 0.6 + 0.35) * s
            knee = Vector((0, -Lg * 0.5, 0.35 if front else -0.9))
            ankle = Vector((0, -Lg + 1.2, 0.0 if front else 0.25))
            lg.tube(coat, [Q(Vector((0, up, 0))), Q(Vector((0, 0, 0))), Q(knee)], [rad_u * 1.2, rad_u, rad_u * 0.6], 10)
            lg.tube(coat if not paws else coat, [Q(knee), Q(ankle), Q(Vector((0, -Lg + 0.5, ankle.z)))], [rad_u * 0.55, rad_l, rad_l * 1.05], 8)
            lg.sphere(coat, rad_u * 0.6, Q(knee), (1, 1, 1), 8, 6)
            if paws:
                lg.sphere("hoof" if sid != "bear" else coat, rad_l * 1.6, Q(Vector((0, -Lg + 0.45, ankle.z + 0.5))), (1.1, 0.6, 1.4), 10, 6)
            else:
                lg.lathe("hoof", [(0.0, 0.0), (rad_l * 1.35, 0.0), (rad_l * 1.15, 0.9 * s), (0.0, 0.9 * s)], Q(Vector((0, -Lg - 0.5, ankle.z))), "y", 10)
        parts.append((nm, nm, "Leg", i))

    # tail
    tr = Vector(d["tailRoot"])
    tl = a.p("Tail", "Hide", pivot=tuple(tr))
    T = d["tail"]
    Qt = lambda v: tuple(tr + v * s)
    if lizard:
        tl.tube(coat, [Qt(Vector((0, 0, 0))), Qt(Vector((0, -0.4, -T * 0.5))), Qt(Vector((0, -0.8, -T)))], [max(0.7, Dv * 0.35) * s, 0.5 * s, 0.1 * s], 10)
        if has("beads"):
            for k in range(int(T)):
                tl.sphere(accent, 0.25 * s, Qt(Vector((0, 0.3 - k * 0.06, -k * 0.9))), (1, 0.6, 1), 6, 4)
    elif has("rings"):
        for k in range(T):
            tl.tube(accent if k % 2 == 0 else coat, [Qt(Vector((0, k * 0.25, -k))), Qt(Vector((0, (k + 1) * 0.25, -k - 1)))], [1.1 * s, 1.0 * s], 10)
        tl.sphere(accent, 0.9 * s, Qt(Vector((0, T * 0.25, -T))), (1, 1, 1), 8, 6)
    elif has("shell"):
        tl.tube(accent, [Qt(Vector((0, 0, 0))), Qt(Vector((0, -0.5, -T)))], [0.6 * s, 0.15 * s], 8)
    elif sid == "horse":
        for k in range(7):
            off = (k - 3) * 0.25
            tl.tube(accent, [Qt(Vector((off * 0.3, 0, 0))), Qt(Vector((off, -T * 0.4, -T * 0.3))), Qt(Vector((off * 1.4, -T * 0.85, -T * 0.4)))], [0.5 * s, 0.4 * s, 0.15 * s], 6)
    elif has("curl"):
        pts = [Vector((math.sin(k * 0.9) * 0.8, math.cos(k * 0.9) * 0.8 - 0.8, -0.3 * k)) for k in range(8)]
        tl.tube(coat, [Qt(q) for q in pts], [0.3 * s] * 7 + [0.15 * s], 6)
    elif sid == "rat":
        tl.tube("pink", [Qt(Vector((0, 0, 0))), Qt(Vector((0, -1, -T * 0.6))), Qt(Vector((0.4, -1.4, -T)))], [0.35 * s, 0.22 * s, 0.06 * s], 6)
    elif paws:
        tl.tube(coat, [Qt(Vector((0, 0, 0))), Qt(Vector((0, T * 0.15, -T * 0.45))), Qt(Vector((0, T * 0.3, -T * 0.9)))], [0.6 * s, 0.85 * s, 0.25 * s], 10)
    elif sid == "cow":
        tl.tube(coat, [Qt(Vector((0, 0, 0))), Qt(Vector((0, -T * 0.9, -1)))], [0.35 * s, 0.25 * s], 6)
        tl.sphere(accent, 0.6 * s, Qt(Vector((0, -T * 0.95, -1))), (0.8, 1.8, 0.8), 8, 6)
    else:
        tl.tube(coat, [Qt(Vector((0, 0, 0))), Qt(Vector((0, -T * 0.6, -T * 0.5)))], [0.55 * s, 0.2 * s], 8)
    parts.append(("Tail", "Tail", "Tail", 0))

    bones = [("Body", (0, cy, -Lv * 0.5 * s), (0, cy, Lv * 0.5 * s), None),
             ("Head", tuple(nk), tuple(nk + Vector((0, 0.1, hs * s))), "Body")]
    for i, (rt, nm) in enumerate(zip(roots, names)):
        bones.append((nm, tuple(rt), tuple(rt - Vector((0, Lg * s, 0))), "Body"))
    bones.append(("Tail", tuple(tr), tuple(tr + Vector((0, -0.2 * T * s, -T * s * 0.8))), "Body"))
    if sid == "horse":                                     # saddle (Animal.SetSaddled)
        sp = Vector((0, (Lg + Dv) * s, -0.05))
        sd = a.p("Saddle", "Leather", pivot=tuple(sp))
        Qs = lambda v: tuple(sp + v * s)
        sd.superloft("cloth_red", [(-4 * s + sp.z, 0, sp.y - 0.2 * s, (Wv / 2 + 1.2) * s, 0.4 * s), (3 * s + sp.z, 0, sp.y - 0.2 * s, (Wv / 2 + 1.2) * s, 0.4 * s)], 16, 3.0)
        sd.superloft("leather", [(-3 * s + sp.z, 0, sp.y + 0.6 * s, (Wv / 2) * s, 0.6 * s), (2 * s + sp.z, 0, sp.y + 0.5 * s, (Wv / 2 - 0.3) * s, 0.5 * s)], 16, 2.6)
        sd.tube("leather", [Qs(Vector((0, 1, -3))), Qs(Vector((0, 2.4, -3.3)))], 0.7 * s, 8)
        sd.tube("leather", [Qs(Vector((0, 1, 2))), Qs(Vector((0, 3, 2.3)))], 0.35 * s, 8)
        for sx in (-1, 1):
            sd.rod("leather", Qs(Vector((sx * (Wv / 2 + 1), 0, 0))), Qs(Vector((sx * (Wv / 2 + 1), -5, 0))), 0.18 * s, 5)
            sd.torus("steel", 0.6 * s, 0.12 * s, Qs(Vector((sx * (Wv / 2 + 1), -5.8, 0))), "x", 10, 4)
        parts.append(("Saddle", "Saddle", "Saddle", 0))
        bones.append(("Saddle", tuple(sp), tuple(sp + Vector((0, 0.2, 0))), "Body"))
    if has("fleece"):                                      # Animal.BuildFleece shell around the torso
        fp = Vector((0, cy, 0))
        fl = a.p("Fleece", "Wool", pivot=tuple(fp))
        r = K.rng(3150 + Lv)
        rx, ry, rz = (Wv * 0.5 + 1.3) * s, (Dv * 0.5 + 1.2) * s, (Lv * 0.5 + 0.7) * s
        for k in range(46):
            th_, ph = r.uniform(0, math.tau), math.acos(r.uniform(-0.4, 1))
            q = Vector((math.sin(ph) * math.cos(th_) * rx * 0.9, math.cos(ph) * ry * 0.9 + 0.4 * s, math.sin(ph) * math.sin(th_) * rz * 0.9 - 0.3 * s))
            fl.rock("wool", r.uniform(0.9, 1.4) * 2 * s, tuple(fp + q), seed=k, squash=0.85, detail=1)
        parts.append(("Fleece", "Fleece", "Fleece", 0))
        bones.append(("Fleece", tuple(fp), tuple(fp + Vector((0, 0.2, 0))), "Body"))
    return finish(a, bones, parts)


# ------------------------------------------------------------------------------------------ birds
def bird(sid):
    d = D(sid)
    s = 0.08 * d["scale"]
    Lv, Dv, Wv, Lg = d["len"], d["depth"], d["width"], d["leg"]
    feat = d["features"].split()
    has = lambda f: f in feat
    flies = d["flies"]
    a = Asset(sid, "Animal", kind="animal", voxel=s)
    mats = species_mats(d)
    coat, belly, accent = mats["coat"], mats["belly"], mats["accent"]
    cy = (Lg + Dv * 0.5) * s
    body = a.p("Body", "Hide")
    ellipsoid_loft(body, lambda c: belly if c.y < cy - s else coat, 0, cy, 0, Wv * 0.5 * s, Dv * 0.5 * s, Lv * 0.5 * s, 2.0, 28, 20,
                   shape=lambda t: (1.0, 1.0 + 0.1 * max(0.0, t), 0.0))
    if flies:                                              # flat tail wedge
        body.quad(coat, (-1.2 * s, cy, -Lv / 2 * s), (1.2 * s, cy, -Lv / 2 * s), (1.8 * s, cy - 0.3 * s, (-Lv / 2 - d["tail"]) * s), (-1.8 * s, cy - 0.3 * s, (-Lv / 2 - d["tail"]) * s), 0.3 * s)
    elif has("fan"):                                       # turkey: a spread fan of feathers
        for k in range(11):
            an = math.radians(-75 + k * 15)
            base = Vector((0, cy + 0.5 * s, -Lv / 2 * s + 0.3 * s))
            tip = base + Vector((math.sin(an) * d["tail"] * 1.6 * s, math.cos(an) * d["tail"] * 1.6 * s, -0.6 * s))
            body.tri_plate(coat if k % 2 else accent, base, tip + Vector((0.4 * s, 0, 0)), tip - Vector((0.4 * s, 0, 0)), 0.15 * s)
    else:                                                  # hen: raised tail tuft
        for k in range(5):
            base = Vector(((k - 2) * 0.35 * s, cy, -Lv / 2 * s + 0.4 * s))
            body.tube(coat, [base, base + Vector(((k - 2) * 0.2 * s, d["tail"] * s, -1.5 * s))], [0.6 * s, 0.15 * s], 6)
    nk = Vector(d["neck_pivot"])
    head = a.p("Head", "Hide", pivot=tuple(nk))
    P = lambda v: tuple(nk + v * s)
    hm = accent if has("bald") else coat
    if has("bald"):
        head.torus("cloth_cream", 1.0 * s, 0.6 * s, P(Vector((0, 0, 0))), "y", 12, 5)
    nt = d["neck"] * 0.7 + 0.4
    head.tube(hm, [P(Vector((0, -0.5, -0.3))), P(Vector((0, nt * 0.6, 0.3))), P(Vector((0, nt, 0.6)))], [0.9 * s, 0.7 * s, 0.6 * s], 12)
    hc = Vector((0, nt + 0.4, 1))
    hr = d["head"]
    head.sphere(hm, 1.0, P(hc), (hr * 0.6 * s, hr * 0.6 * s, hr * 0.72 * s), 22, 14)
    beak = "beak_dark" if flies else "beak"
    bz0, bz1 = hc.z + hr * 0.55, hc.z + hr + d["snout"]
    head.add(K.kit.bm_cyl(hr * 0.3 * s, (bz1 - bz0) * s, 8, 0.0, 0.02 * s), beak, Matrix.Translation(P(Vector((0, hc.y - 0.1, (bz0 + bz1) / 2)))) @ K._axis_mtx("z"))
    if flies:
        head.sphere(beak, 0.3 * s, P(Vector((0, hc.y - 0.4, bz1 - 0.1))), (0.8, 1.2, 0.8), 6, 4)
    if has("comb"):
        for k in range(4):
            head.sphere(accent, 0.45 * s, P(Vector((0, hc.y + hr * 0.6 + 0.3, hc.z - 0.6 + k * 0.5))), (0.5, 1.2, 0.8), 8, 5)
    if has("wattle"):
        head.sphere(accent, 0.5 * s, P(Vector((0, hc.y - 1.5, hc.z + 1.0))), (0.6, 1.4, 0.7), 8, 5)
    for sx in (-1, 1):
        eye(head, P(Vector((sx * hr * 0.5, hc.y + 0.4, hc.z + 0.3))), 0.3 * s)
    parts = [("Body", "Body", "Body", 0), ("Head", "Head", "Head", 0)]
    roots = [Vector(r) for r in d["legRoots"]]
    lm = "beak_dark" if flies else "beak"
    for i, rt in enumerate(roots):
        nm = "Leg_L" if i == 0 else "Leg_R"
        lg = a.p(nm, "Hide", pivot=tuple(rt))
        Q = lambda v: tuple(rt + v * s)
        lg.tube(coat, [Q(Vector((0, 1, 0))), Q(Vector((0, 0, -0.1))), Q(Vector((0, -Lg * 0.3, -0.2)))], [0.75 * s, 0.7 * s, 0.45 * s], 14)
        lg.tube(lm, [Q(Vector((0, -Lg * 0.3, -0.2))), Q(Vector((0, -Lg + 0.2, 0)))], [0.22 * s, 0.18 * s], 6)
        for an in (-35, 0, 35, 180):
            dv = Vector((math.sin(math.radians(an)), 0, math.cos(math.radians(an))))
            lg.tube(lm, [Q(Vector((0, -Lg + 0.2, 0))), Q(Vector((0, -Lg, 0)) + dv * (1.3 if an != 180 else 0.8))], [0.16 * s, 0.08 * s], 5)
        parts.append((nm, nm, "Leg", i))
    wr = Vector(d["wingRoot"])
    span = 12 if flies else 1
    chord = 5 if flies else Lv - 2
    for side, nm in ((1, "WingR"), (-1, "WingL")):
        root_p = Vector((wr.x * side, wr.y, wr.z))
        wg = a.p(nm, "Hide", pivot=tuple(root_p))
        Q = lambda v: tuple(root_p + Vector((v.x * side, v.y, v.z)) * s)
        if flies:                                          # broad wing, fingered primaries
            secs = []
            for k in range(7):
                x = span * k / 6
                c = chord - max(0, x - span + 3)
                secs.append([Q(Vector((x, 0.15, 1.2))), Q(Vector((x, 0.15, -c + 2))), Q(Vector((x, -0.15, -c + 2))), Q(Vector((x, -0.15, 1.2)))])
            wg.loft(coat, secs)
            for f in range(5):
                base = Vector((span - 2.5, 0, 1.0 - f * 1.1))
                wg.quad(accent if f % 2 else coat, Q(base), Q(base + Vector((3.5, 0, 0.4 - f * 0.25))), Q(base + Vector((3.5, 0, -0.3 - f * 0.25))), Q(base + Vector((0, 0, -0.7))), 0.12 * s)
        else:                                              # folded along the body
            wg.superloft(coat, [(0.5 * s + root_p.z, root_p.x + side * 0.2 * s, root_p.y - 0.8 * s, 0.35 * s, 1.4 * s), ((-chord + 3) * s + root_p.z, root_p.x + side * 0.2 * s, root_p.y - 0.8 * s, 0.4 * s, 1.6 * s),
                                (-chord * s + root_p.z, root_p.x, root_p.y - 0.4 * s, 0.15 * s, 0.6 * s)], 10, 2.2)
        parts.append((nm, nm, "Wing", 0 if side > 0 else 1))
    bones = [("Body", (0, cy, -Lv * 0.5 * s), (0, cy, Lv * 0.5 * s), None), ("Head", tuple(nk), tuple(nk + Vector((0, 0.05, hr * s))), "Body")]
    for i, rt in enumerate(roots):
        bones.append(("Leg_L" if i == 0 else "Leg_R", tuple(rt), tuple(rt - Vector((0, Lg * s, 0))), "Body"))
    bones.append(("WingR", (wr.x, wr.y, wr.z), (wr.x + span * s * 0.5 + 0.05, wr.y, wr.z), "Body"))
    bones.append(("WingL", (-wr.x, wr.y, wr.z), (-wr.x - span * s * 0.5 - 0.05, wr.y, wr.z), "Body"))
    return finish(a, bones, parts)


# ------------------------------------------------------------------------------------------ snakes
def snake(sid):
    d = D(sid)
    s = 0.08 * d["scale"]
    a = Asset(sid, "Animal", kind="animal", voxel=s)
    mats = species_mats(d)
    coat, belly, accent = mats["coat"], mats["belly"], mats["accent"]
    n, seg = d["segments"], d["segLen"]
    r0 = 1.05 * s
    parts, bones = [], [("Root", (0, 0, 0.05), (0, 0, -0.05), None)]
    for i in range(n):
        piv = Vector((0, 0, -i * seg))
        sg = a.p("Seg_%d" % i, "Hide", pivot=tuple(piv))
        taper = 1.0 - 0.45 * (i / max(1, n - 1)) ** 2
        rr = r0 * taper
        z0, z1 = piv.z + seg * 0.62, piv.z - seg * 0.62
        cy = rr
        ellipsoid_loft(sg, lambda c, cy=cy: belly if c.y < cy * 0.55 else coat, 0, cy, (z0 + z1) / 2, rr * 1.15, rr, seg * 0.62, 4.0, 16, 10)
        for k in range(2):                                 # dark diamonds down the back
            zc = piv.z + (0.25 - k * 0.5) * seg
            sg.quad(accent, (0, cy + rr * 0.98, zc + seg * 0.22), (rr * 0.6, cy + rr * 0.8, zc), (0, cy + rr * 0.98, zc - seg * 0.22), (-rr * 0.6, cy + rr * 0.8, zc), 0.08 * s)
        parts.append(("Seg_%d" % i, "Seg_%d" % i, "Seg", i))
        bones.append(("Seg_%d" % i, tuple(piv + Vector((0, 0, seg * 0.5))), tuple(piv - Vector((0, 0, seg * 0.5))), "Root"))
    hd = a.p("Head", "Hide")
    hd.superloft(coat, [(-0.4 * s, 0, 0.75 * s, 1.0 * s, 0.75 * s), (0.8 * s, 0, 0.85 * s, 1.5 * s, 0.85 * s), (2.4 * s, 0, 0.8 * s, 1.35 * s, 0.75 * s), (3.6 * s, 0, 0.65 * s, 0.7 * s, 0.5 * s),
                        (4.0 * s, 0, 0.6 * s, 0.2 * s, 0.2 * s)], 14, 2.4)
    for sx in (-1, 1):
        eye(hd, (sx * 1.15 * s, 1.35 * s, 2.3 * s), 0.3 * s)
    hd.tube("pink", [(0, 0.45 * s, 3.9 * s), (0, 0.4 * s, 4.8 * s), (0.25 * s, 0.4 * s, 5.4 * s)], [0.08 * s, 0.06 * s, 0.02 * s], 4)
    parts.append(("Head", "Head", "Head", 0))
    bones.append(("Head", (0, 0.5 * s, 0), (0, 0.5 * s, 3 * s), "Root"))
    last = Vector((0, 0, -(n - 1) * seg))
    rp = last + Vector((0, 0, -seg * 0.5))
    rt = a.p("Rattle", "Hide", pivot=tuple(rp))
    if "rattle" in d["features"].split():
        for k in range(4):
            rt.sphere("horn_pale" if k % 2 == 0 else "cloth_cream", 0.6 * s * (1 - k * 0.12), (0, 0.6 * s, rp.z - (k + 0.5) * 0.9 * s), (1.0, 0.8, 0.8), 10, 6)
    else:
        rt.add(K.kit.bm_cyl(0.55 * s * 0.55, 3 * s, 8, 0.0, 0.02 * s), coat, Matrix.Translation((0, 0.35 * s, rp.z - 1.5 * s)) @ K._axis_mtx("z"))
    parts.append(("Rattle", "Rattle", "Rattle", 0))
    bones.append(("Rattle", tuple(rp), tuple(rp - Vector((0, 0, 3 * s))), "Seg_%d" % (n - 1)))
    return finish(a, bones, parts)


# ------------------------------------------------------------------------------------------ arthropods
def arthropod(sid):
    d = D(sid)
    s = 0.08 * d["scale"]
    Lv, Dv, Wv = d["len"], d["depth"], d["width"]
    Lg = max(1, d["leg"])
    feat = d["features"].split()
    has = lambda f: f in feat
    spider = not has("legs6") and not has("pincers")
    a = Asset(sid, "Animal", kind="animal", voxel=s)
    mats = species_mats(d)
    coat, belly, accent = mats["coat"], mats["belly"], mats["accent"]
    cy = (Lg + Dv * 0.5) * s
    body = a.p("Body", "Hide")
    ellipsoid_loft(body, lambda c: belly if c.y < cy - Dv * 0.25 * s else coat, 0, cy, Lv * 0.2 * s, Wv * 0.36 * s, Dv * 0.45 * s, Lv * 0.22 * s, 2.2, 20, 12)
    if spider:
        ellipsoid_loft(body, lambda c: (accent if (has("hairy") and K.pal_hash(int(c.x * 200), int(c.y * 200), int(c.z * 200), 4) > 0.75) else (belly if c.y < cy - Dv * 0.1 * s else coat)),
                       0, cy + Dv * 0.2 * s, -Lv * 0.25 * s, Wv * 0.46 * s, Dv * 0.62 * s, Lv * 0.34 * s, 2.0, 16, 10)
        if has("hairy"):
            r = K.rng(len(sid))
            for k in range(40):
                th, ph = r.uniform(0, math.tau), r.uniform(0.2, 1.4)
                q = Vector((math.sin(ph) * math.cos(th) * Wv * 0.46 * s, cy + Dv * 0.2 * s + math.cos(ph) * Dv * 0.62 * s, -Lv * 0.25 * s + math.sin(ph) * math.sin(th) * Lv * 0.34 * s))
                body.rod(accent, q, q * 1.0 + (q - Vector((0, cy, -Lv * 0.25 * s))).normalized() * 0.4 * s, 0.06 * s, 3)
    elif has("shell"):
        ellipsoid_loft(body, lambda c: (accent if abs(c.x) < 0.12 * s and c.y > cy else (belly if c.y < cy - Dv * 0.2 * s else coat)), 0, cy + Dv * 0.15 * s, -Lv * 0.15 * s,
                       Wv * 0.5 * s, Dv * 0.6 * s, Lv * 0.4 * s, 2.0, 24, 16)
    else:                                                  # plated abdomen, a seam every other plate
        nz = max(3, int(Lv / 2 + 1))
        for k in range(nz):
            zt = -Lv / 2 * s + k * (Lv / 2 * s) / nz
            wv = Wv * 0.45 * (1 - abs(zt / s + Lv * 0.2) / (Lv * 0.45)) + 1
            body.superloft(accent if k % 2 == 0 else coat, [(zt, 0, cy - 0.05 * Dv * s, wv * s, Dv * 0.36 * s), (zt + (Lv / 2 * s) / nz * 0.92, 0, cy - 0.05 * Dv * s, wv * s * 1.02, Dv * 0.38 * s)], 12, 2.4)
    parts = [("Body", "Body", "Body", 0)]
    nk = Vector(d["neck_pivot"])
    hd = a.p("Head", "Hide", pivot=tuple(nk))
    P = lambda v: tuple(nk + v * s)
    hd.sphere(coat, 1.0, P(Vector((0, 0, 1))), ((Wv * 0.22 + 0.5) * s, (Dv * 0.3 + 0.5) * s, 1.2 * s), 12, 8)
    for sx in (-1, 1):
        eye(hd, P(Vector((sx * 0.9, 0.9, 2.0))), 0.28 * s, has("glow"))
    if spider:
        eye(hd, P(Vector((0, 1.05, 2.0))), 0.22 * s, has("glow"))
        for sx in (-1, 1):
            hd.tube(accent, [P(Vector((sx * 0.6, -0.6, 2.0))), P(Vector((sx * 0.5, -1.4, 2.6)))], [0.25 * s, 0.05 * s], 5)
    if has("pincers"):
        for sx in (-1, 1):
            a0, a1, a2 = Vector((sx * 1.0, 0, 1.0)), Vector((sx * (Wv * 0.55 + 1), 0, 2.5)), Vector((sx * (Wv * 0.45 + 1), 0, 3.5 + Lv * 0.2))
            hd.tube(coat, [P(a0), P(a1), P(a2)], [0.55 * s, 0.6 * s, 0.6 * s], 8)
            cc = a2 + Vector((0, 0, 1.2))
            hd.sphere(accent, 1.0, P(cc), (1.3 * s, 0.9 * s, 1.8 * s), 12, 8)
            hd.tube(accent, [P(cc + Vector((sx * 0.6, 0, 1.2))), P(cc + Vector((sx * 0.2, 0, 2.6)))], [0.4 * s, 0.08 * s], 6)
            hd.tube(accent, [P(cc + Vector((-sx * 0.6, 0, 1.0))), P(cc + Vector((-sx * 0.1, 0, 2.2)))], [0.35 * s, 0.06 * s], 6)
    if has("antennae"):
        for sx in (-1, 1):
            hd.tube(accent, [P(Vector((sx * 0.5, 0.5, 2.0))), P(Vector((sx * 1.6, 1.4, 2 + Lv * 0.4))), P(Vector((sx * 2.5, 2.0, 2 + Lv * 0.8)))], [0.18 * s, 0.1 * s, 0.04 * s], 4)
    parts.append(("Head", "Head", "Head", 0))
    roots = [Vector(r) for r in d["legRoots"]]
    fans = d["legFan"]
    reach = Wv * 0.55 + Lg * (1.1 if spider else 0.8)
    lr = max(0.45, Wv * 0.07)
    bones = [("Body", (0, cy, -Lv * 0.5 * s), (0, cy, Lv * 0.5 * s), None), ("Head", tuple(nk), tuple(nk + Vector((0, 0, 2 * s))), "Body")]
    for i, rt in enumerate(roots):
        left = rt.x < 0
        sx = -1 if left else 1
        nm = "Leg_%d" % i
        lg = a.p(nm, "Hide", pivot=tuple(rt))
        fan = math.radians(fans[i])
        dvec = Vector((math.cos(fan) * sx, 0, math.sin(fan)))
        knee = dvec * reach * 0.55 + Vector((0, (Lg * 0.9 + 1) if spider else (Lg * 0.4 + 0.5), 0))
        foot = dvec * reach + Vector((0, -Lg, 0))
        Q = lambda v: tuple(rt + v * s)
        lg.tube(coat, [Q(Vector((0, 0, 0))), Q(knee)], [lr * s, lr * 0.85 * s], 10)
        lg.tube(accent if has("hairy") else coat, [Q(knee), Q(foot.lerp(knee, 0.4)), Q(foot)], [lr * 0.85 * s, lr * 0.6 * s, lr * 0.3 * s], 10)
        lg.sphere(coat, lr * 0.95 * s, Q(knee), (1, 1, 1), 8, 5)
        parts.append((nm, nm, "Leg", i))
        bones.append((nm, tuple(rt), tuple(rt + Vector((sx * reach * s * 0.5, 0, 0))), "Body"))
    tr = Vector(d["tailRoot"])
    tl = a.p("Tail", "Hide", pivot=tuple(tr))
    Qt = lambda v: tuple(tr + v * s)
    if has("stinger"):
        T = max(4, d["tail"])
        pts = [Vector((0, 0, 0)), Vector((0, T * 0.25, -T * 0.35)), Vector((0, T * 0.7, -T * 0.3)), Vector((0, T * 0.95, T * 0.05)), Vector((0, T * 0.85, T * 0.3))]
        for k in range(4):
            seg_pts = [pts[k].lerp(pts[k + 1], u / 3) for u in range(4)]
            rad = (Wv * 0.18 + 0.6) * (1 - k * 0.15)
            tl.tube(coat if k % 2 == 0 else accent, [Qt(q) for q in seg_pts], [rad * s * 0.95, rad * s, rad * s, rad * s * 0.9], 8)
        tip = pts[-1]
        tl.sphere("glow_green" if has("glow") else accent, 1.0, Qt(tip), (0.9 * s, 1.1 * s, 1.1 * s), 10, 6)
        tl.tube("black", [Qt(tip), Qt(tip + Vector((0, -1.5, 1.2)))], [0.35 * s, 0.03 * s], 6)
    else:
        tl.sphere(belly, 0.5 * s, Qt(Vector((0, 0, -0.3))), (1, 0.8, 1.2), 8, 5)
        if has("antennae"):
            for sx in (-1, 1):
                tl.tube(accent, [Qt(Vector((sx * 0.3, 0, 0))), Qt(Vector((sx * 0.8, 0.3, -1.6)))], [0.12 * s, 0.04 * s], 4)
    parts.append(("Tail", "Tail", "Tail", 0))
    bones.append(("Tail", tuple(tr), tuple(tr + Vector((0, 0, -2 * s))), "Body"))
    return finish(a, bones, parts)


# ------------------------------------------------------------------------------------------ extras (not in AnimalLibrary)
def pack_mule():
    """Npc/PackAnimal.cs: trader's mule. Root mesh PackMule (body, saddlebags, pots, blanket roll, sack), four Leg children
    at (+-3, 11, +-7) voxels swinging about x, Head at (0, 15, 10) nodding."""
    s = 0.08
    a = Asset("PackMule", "Animal", kind="animal", voxel=s, note="Npc/PackAnimal (no AnimalDef); legs all named Leg in game, indexed FL FR BL BR")
    for key, hexes in (("mule_coat", "6b4a2e"), ("mule_dark", "3a2a1c")):
        K.M.setdefault(key, K.kit.surface("Fur_" + key, K.hexc(hexes), dust=0.15, rough=0.92, var=0.2, bump=0.55, scale=3.0))
    K.M.setdefault("eye", K.kit.surface("Eye", K.hexc("0d0d10"), dust=0.0, rough=0.15, var=0.0, bump=0.0))
    K.M.setdefault("hoof", K.kit.surface("Hoof", K.hexc("24211f"), dust=0.2, rough=0.5, var=0.15, bump=0.3))
    c = lambda v: v * s
    b = a.p("Body", "Hide")
    ellipsoid_loft(b, lambda q: "mule_coat", 0, c(14.2), 0, c(3.6), c(3.5), c(9.6), 2.6, 22, 16)
    b.tube("mule_dark", [(0, c(16), c(-9.5)), (0, c(13), c(-10.6)), (0, c(10.5), c(-10.8))], [c(0.6), c(0.5), c(0.25)], 8)
    for sx in (-1, 1):                                          # saddlebags with flaps and buckles, a hanging pot
        b.box2("canvas", (c(sx * 4 - 0.5) if sx > 0 else c(-6.5), c(11.5), c(-4.5)), (c(6.5) if sx > 0 else c(-3.5), c(17.4), c(3.5)), bevel=0.03)
        b.box2("olive", (c(sx * 6.4 - 0.15) if sx > 0 else c(-6.6), c(15.0), c(-4.2)), (c(6.6) if sx > 0 else c(-6.25), c(17.5), c(3.2)), bevel=0.01)
        for z in (-2.5, 1.5):
            b.box2("leather", (c(6.55) if sx > 0 else c(-6.75), c(13.0), c(z - 0.3)), (c(6.75) if sx > 0 else c(-6.55), c(16.5), c(z + 0.3)), bevel=0.003)
            b.box2("brass", (c(6.7) if sx > 0 else c(-6.82), c(14.4), c(z - 0.35)), (c(6.82) if sx > 0 else c(-6.7), c(14.9), c(z + 0.35)), bevel=0.002)
        b.lathe("steel_dark", [(0.0, 0.0), (c(1.1), 0.0), (c(1.25), c(1.6)), (c(1.15), c(1.7)), (0.0, c(1.7))], (c(sx * 6.5), c(14.8), c(5)), "y", 14)
        b.torus("steel", c(0.5), c(0.08), (c(sx * 6.5), c(16.8), c(5)), "x", 10, 4)
    b.superloft("cloth_red", [(c(-6.5), 0, c(19), c(3.4), c(1.4)), (c(5.5), 0, c(19), c(3.4), c(1.4))], 16, 2.0)
    for z in (-4, 0, 4):
        b.torus("leather", c(1.45), c(0.15), (0, c(19), c(z)), "z", 16, 4)
    b.rock("burlap", c(3.3), (0, c(21.4), c(-0.5)), seed=21, squash=0.45, detail=1, scale=(1.3, 1, 0.9))
    b.superloft("leather", [(c(-3), 0, c(17.4), c(3.9), c(0.35)), (c(3), 0, c(17.4), c(3.9), c(0.35))], 16, 3.0)
    for sx in (-1, 1):
        b.rod("leather", (c(sx * 3.7), c(17.2), 0), (c(sx * 3.4), c(11.4), c(0.3)), c(0.18), 6)
    parts = [("Body", "Body", "Body", 0)]
    bones = [("Body", (0, c(14), c(-9)), (0, c(14), c(9)), None)]
    names = ["Leg_FL", "Leg_FR", "Leg_BL", "Leg_BR"]
    for i, (x, z) in enumerate(((-3, 7), (3, 7), (-3, -7), (3, -7))):
        rt = Vector((c(x + 0.5 * (1 if x < 0 else -1) * 0), c(11), c(z)))
        lg = a.p(names[i], "Hide", pivot=tuple(rt))
        R = lambda v: tuple(rt + v * s)
        front = z > 0
        lg.tube("mule_coat", [R(Vector((-0.5, 2.5, -0.5))), R(Vector((-0.5, 0, -0.5))), R(Vector((-0.5, -5.5, 0.0 if front else -0.8))), R(Vector((-0.5, -10.4, 0)))],
                [c(1.5), c(1.25), c(0.75), c(0.6)], 12)
        lg.sphere("mule_coat", c(0.85), R(Vector((-0.5, -5.5, 0.0 if front else -0.8))), (1, 1, 1), 8, 6)
        lg.lathe("hoof", [(0.0, 0.0), (c(0.85), 0.0), (c(0.72), c(1.2)), (0.0, c(1.2))], R(Vector((-0.5, -12.5, 0))), "y", 12)
        parts.append((names[i], names[i], "Leg", i))
        bones.append((names[i], tuple(rt), tuple(rt - Vector((0, c(11), 0))), "Body"))
    hp = Vector((0, c(15), c(10)))
    h = a.p("Head", "Hide", pivot=tuple(hp))
    H = lambda v: tuple(hp + v * s)
    h.tube("mule_coat", [H(Vector((0, -1.0, -1.5))), H(Vector((0, 2.5, 0.8))), H(Vector((0, 5, 2.8)))], [c(1.9), c(1.4), c(1.3)], 12)
    h.superloft("mule_coat", [(c(1.6) + hp.z, 0, c(5.6) + hp.y, c(1.9), c(1.9)), (c(4.5) + hp.z, 0, c(5.3) + hp.y, c(1.7), c(1.6)), (c(7.4) + hp.z, 0, c(4.9) + hp.y, c(1.3), c(1.3)),
                                    (c(8.4) + hp.z, 0, c(4.8) + hp.y, c(1.0), c(1.0))], 16, 2.3)
    h.sphere("mule_dark", c(1.1), H(Vector((0, 4.6, 8.0))), (1.05, 0.9, 0.6), 10, 6)
    for sx in (-1, 1):
        eye(h, H(Vector((sx * 1.85, 6.2, 5.0))), c(0.35))
        h.add(K.kit.bm_sphere(1.0, 10, 6), "mule_coat", Matrix.Translation(H(Vector((sx * 1.6, 9.4, 2.5)))) @ K.rot(-10, 0, sx * 15) @ Matrix.Diagonal((c(0.55), c(2.2), c(0.8), 1)))
    h.add(K.kit.bm_box(c(0.45), c(1.0), c(4.5), bevel=0.01), "mule_dark", Matrix.Translation(H(Vector((0, 4.2, 0.6)))) @ K.rot(-50, 0, 0))
    h.torus("leather", c(1.75), c(0.14), H(Vector((0, 4.9, 6.5))), "z", 14, 4)
    parts.append(("Head", "Head", "Head", 0))
    bones.append(("Head", tuple(hp), tuple(hp + Vector((0, c(4), c(4)))), "Body"))
    a.ref_height = 22 * s
    return finish(a, bones, parts)


def sea_fish(kind):
    """World/SeaLife.cs: school fish SeaFish0..3 (0.05 m voxels; chrome, navy, banded olive, ochre)."""
    s = 0.05
    a = Asset("SeaFish%d" % kind, "Animal", kind="critter", voxel=s, note="World/SeaLife school fish, single mesh")
    pal = [("c8ccd0", "8a9098"), ("34508a", "18244a"), ("6b7a3a", "3a4420"), ("c48c2a", "7c5214")][kind]
    for key, hx in (("fish_top", pal[1]), ("fish_side", pal[0])):
        K.M.setdefault(key + str(kind), K.kit.surface("Fish%d_%s" % (kind, key), K.hexc(hx), dust=0.0, rough=0.25, var=0.15, bump=0.3, metal=0.4 if kind == 0 else 0.0, scale=6.0))
    K.M.setdefault("eye", K.kit.surface("Eye", K.hexc("0d0d10"), dust=0.0, rough=0.15, var=0.0, bump=0.0))
    K.M.setdefault("fish_band", K.kit.surface("FishBand", K.hexc("18171c"), dust=0.0, rough=0.3, var=0.1, bump=0.2))
    b = a.p("Body", "Hide")
    top, side = "fish_top%d" % kind, "fish_side%d" % kind

    def m(q):
        if kind == 2 and int((q.z / s + 10) // 1) % 2 == 0 and q.y > -0.01:
            return "fish_band"
        return top if q.y > s * 0.9 else side
    ellipsoid_loft(b, m, 0, s * 0.5, s * 0.4, s * 1.25, s * 1.3, s * 3.2, 2.2, 20, 16, shape=lambda t: (1 - 0.25 * max(0.0, -t), 1.0, 0.0))
    b.quad(side, (0, s * 0.6, -s * 2.6), (0, s * 2.6, -s * 4.6), (0, s * 0.5, -s * 3.7), (0, -s * 1.6, -s * 4.6), s * 0.15)
    b.tri_plate(top, (0, s * 1.6, s * 0.6), (0, s * 2.4, -s * 0.6), (0, s * 1.6, -s * 1.6), s * 0.12)
    for sx in (-1, 1):
        eye(b, (sx * s * 0.9, s * 1.0, s * 2.4), s * 0.28)
        b.tri_plate(side, (sx * s * 1.1, s * 0.2, s * 1.2), (sx * s * 2.0, -s * 0.3, s * 0.2), (sx * s * 1.1, s * 0.1, s * 0.3), s * 0.08)
    return finish(a, [("Body", (0, s * 0.5, -s * 3), (0, s * 0.5, s * 3), None)], [("Body", "Body", "Body", 0)])


def jellyfish():
    s = 0.05
    a = Asset("Jellyfish", "Animal", kind="critter", voxel=s, note="World/SeaLife jellyfish (stings), single mesh")
    K.M.setdefault("jelly", K.kit.glass("JellyBell", K.hexc("eab4a4")))
    K.M.setdefault("jelly_in", K.kit.emissive("JellyGlow", K.hexc("c07a70"), 1.2))
    K.M.setdefault("jelly_t", K.kit.surface("JellyTentacle", K.hexc("d8988a"), dust=0.0, rough=0.3, var=0.2, bump=0.1))
    b = a.p("Body", "Hide")
    prof = [(0.0, s * 3.4), (s * 1.6, s * 3.3), (s * 3.0, s * 2.6), (s * 4.0, s * 1.4), (s * 4.4, s * 0.2), (s * 4.1, -s * 0.3), (s * 3.6, s * 0.2), (s * 2.6, s * 1.4), (0.0, s * 1.9)]
    b.lathe("jelly", prof, (0, 0, 0), "y", 28)
    b.lathe("jelly_in", [(0.0, s * 1.9), (s * 1.8, s * 1.5), (s * 1.2, s * 0.6), (0.0, s * 0.4)], (0, 0, 0), "y", 16)
    for k in range(6):
        an = k * math.pi / 3
        r0 = s * 2.0
        pts = [(math.cos(an) * r0 + math.sin(t * 1.3 + k) * s * 0.4, -t * s * 1.2 + s * 0.3, math.sin(an) * r0 + math.cos(t * 1.1 + k) * s * 0.4) for t in range(6)]
        b.tube("jelly_t", pts, [s * 0.28, s * 0.24, s * 0.2, s * 0.16, s * 0.12, s * 0.05], 6)
    for k in range(4):
        an = k * math.pi / 2 + 0.4
        b.tube("jelly_in", [(math.cos(an) * s * 0.4, s * 0.4, math.sin(an) * s * 0.4), (math.cos(an) * s * 0.9, -s * 2.5, math.sin(an) * s * 0.9), (math.cos(an) * s * 0.6, -s * 4.5, math.sin(an) * s * 0.6)],
               [s * 0.45, s * 0.35, s * 0.1], 8)
    return finish(a, [("Body", (0, -s * 6, 0), (0, s * 3, 0), None)], [("Body", "Body", "Body", 0)])


def bat():
    """World/BatColony.cs: cave bats (0.05 m voxels), wings spread."""
    s = 0.05
    a = Asset("Bat", "Animal", kind="critter", voxel=s, note="World/BatColony, single mesh")
    K.M.setdefault("bat_fur", K.kit.surface("BatFur", K.hexc("232127"), dust=0.0, rough=0.95, var=0.2, bump=0.5, scale=6))
    K.M.setdefault("bat_wing", K.kit.surface("BatWing", K.hexc("302d33"), dust=0.0, rough=0.6, var=0.15, bump=0.2))
    K.M.setdefault("eye", K.kit.surface("Eye", K.hexc("0d0d10"), dust=0.0, rough=0.15, var=0.0, bump=0.0))
    b = a.p("Body", "Hide")
    b.sphere("bat_fur", 1.0, (0, s * 0.6, 0), (s * 0.55, s * 0.9, s * 0.5), 12, 8)
    b.sphere("bat_fur", 1.0, (0, s * 1.55, s * 0.1), (s * 0.45, s * 0.4, s * 0.45), 12, 8)
    for sx in (-1, 1):
        b.add(K.kit.bm_cyl(s * 0.2, s * 0.6, 6, 0.0), "bat_fur", Matrix.Translation((sx * s * 0.25, s * 2.05, s * 0.05)) @ K.rot(0, 0, sx * -15) @ K._axis_mtx("y"))
        eye(b, (sx * s * 0.18, s * 1.6, s * 0.48), s * 0.07)
        sh, el, tip = Vector((sx * s * 0.4, s * 1.1, 0)), Vector((sx * s * 1.6, s * 1.7, 0)), Vector((sx * s * 3.3, s * 2.2, 0))
        b.tube("bat_fur", [sh, el, tip], [s * 0.1, s * 0.08, s * 0.04], 5)
        for (f, lowr) in ((tip, Vector((sx * s * 3.0, s * 0.9, 0))), (el.lerp(tip, 0.5), Vector((sx * s * 2.2, s * 0.6, 0))), (el, Vector((sx * s * 1.2, s * 0.5, 0)))):
            b.tri_plate("bat_wing", el if f is not el else sh, f, lowr, s * 0.04)
        b.tri_plate("bat_wing", sh, el, Vector((sx * s * 0.5, s * 0.3, 0)), s * 0.04)
    return finish(a, [("Body", (0, 0, 0), (0, s * 2, 0), None)], [("Body", "Body", "Body", 0)])


def build(sid):
    d = D(sid)
    plan = d["plan"]
    if plan == "Bird":
        return bird(sid)
    if plan == "Snake":
        return snake(sid)
    if plan == "Arthropod":
        return arthropod(sid)
    return quadruped(sid)


GROUPS = [("animals_hoofed", "Horse (saddled), antelope, deer, cow, goat, sheep (fleece)", ["horse", "antelope", "deer", "cow", "goat", "sheep"]),
          ("animals_pigs", "Boar, rad-boar, pig, bear", ["boar", "radboar", "pig", "bear"]),
          ("animals_dogs", "Dog, guard dog, wolf, coyote, jackrabbit, rat", ["dog", "guarddog", "wolf", "coyote", "jackrabbit", "rat"]),
          ("animals_small", "Armadillo, raccoon, lizard, gila monster, rad-lizard", ["armadillo", "raccoon", "lizard", "gila", "radlizard"]),
          ("animals_birds", "Vulture, chicken, crow, turkey", ["vulture", "chicken", "crow", "turkey"]),
          ("animals_snakes", "Rattlesnake, cottonmouth, python", ["snake", "cottonmouth", "python"]),
          ("animals_bugs", "Scorpion, rad-scorpion, tarantula, cave spider, rad-roach, crab, beetle", ["scorpion", "radscorpion", "tarantula", "cavespider", "radroach", "crab", "beetle"])]

TILES = [(g, lbl, [lambda ids=ids: [build(i) for i in ids]], min(4, len(ids))) for (g, lbl, ids) in GROUPS]
TILES.append(("animals_extra", "Pack mule (trader), cave bat, sea fish x4, jellyfish",
              [pack_mule, bat, lambda: [sea_fish(k) for k in range(4)], jellyfish], 4))

if __name__ == "__main__":
    K.run_tiles(TILES, "animals", HERE, "")
