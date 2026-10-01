"""HD character preview: base bodies, heads/hair, 8 outfits, 3 poses, clothing flat-lays.

Run:  blender -b -P tools/blender/hd/character/build_characters.py -- <out_dir> [tile-filter]
Writes <out_dir>/<tile>.png + <tile>_px.png (render_common.render_tile), <out_dir>/tiles.json, and saves
hd_characters.blend next to this script."""
import json
import math
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))

import bmesh  # noqa: E402
import bpy  # noqa: E402
import numpy as np  # noqa: E402

import render_common as RC  # noqa: E402
import hdlib as H  # noqa: E402
import body as B  # noqa: E402
import build_lib as BL  # noqa: E402
import garments as G  # noqa: E402
import materials as MAT  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else os.path.join(HERE, "out")
ONLY = argv[1].split(",") if len(argv) > 1 else None
T0 = time.time()

RC.reset_scene()
RC.setup_stage()

# ---------------------------------------------------------------- poses (HumanAnimator conventions: local Unity Euler, identity rest)
POSES = {
    "apose": {"UpperArmL": (0, 0, -32), "UpperArmR": (0, 0, 32), "ForearmL": (-8, 0, 0), "ForearmR": (-8, 0, 0),
              "ThighL": (0, 0, -4), "ThighR": (0, 0, 4), "FootL": (0, 0, 4), "FootR": (0, 0, -4)},
    "idle": {"Pelvis": (0, 4, 2), "Chest": (2, -4, -1.5), "Head": (-2, 6, 1), "ThighL": (-3, 0, -3), "ThighR": (5, 0, 2),
             "ShinL": (5, 0, 0), "ShinR": (3, 0, 0), "FootL": (-2, -8, 3), "FootR": (-8, 6, -2),
             "UpperArmL": (-4, 0, -8), "UpperArmR": (4, 0, 10), "ForearmL": (-14, 0, 0), "ForearmR": (-22, 0, 0),
             "HandL": (0, 0, 4), "HandR": (0, 0, -6)},
    "walk": {"Pelvis": (0, 6, 2), "Chest": (4, -8, 0), "Head": (-3, 4, 0), "ThighL": (-27, 0, -2), "ShinL": (10, 0, 0), "FootL": (8, 0, 0),
             "ThighR": (20, 0, 2), "ShinR": (34, 0, 0), "FootR": (-24, 0, 0), "UpperArmL": (24, 0, -6), "UpperArmR": (-26, 0, 6),
             "ForearmL": (-14, 0, 0), "ForearmR": (-34, 0, 0)},
    "drive": {"ThighL": (-84, 0, -4), "ThighR": (-84, 0, 4), "ShinL": (78, 0, 0), "ShinR": (72, 0, 0), "FootL": (4, 0, 0), "FootR": (10, 0, 0),
              "Chest": (-6, 0, 0), "Head": (6, 0, 0), "UpperArmL": (-72, 0, -6), "UpperArmR": (-72, 0, 6), "ForearmL": (-24, 0, 0),
              "ForearmR": (-24, 0, 0)},
}
WALK_DROP = (0, -0.035, 0)

# ---------------------------------------------------------------- bodies
HUMANS = {}


def human(key):
    if key not in HUMANS:
        spec = {"M": ("m", "rugged", 1.0, 1.0), "M2": ("m", "lean", 1.02, 0.95), "F": ("f", "soft", 0.96, 0.9),
                "F2": ("f", "sharp", 0.95, 0.88)}[key]
        h = BL.Human(key, *spec)
        h.build_body()
        HUMANS[key] = h
    return HUMANS[key]


def mat_for(key):
    hexc, kind, opt = G.MATS[key]
    return MAT.make("m_" + key, hexc, kind, **opt)


def garment_pieces(hum, gid):
    out = []
    for spec in G.GARMENTS[gid][0](hum):
        lo, hi = hum.bounds(spec["parts"], 0.17, spec.get("extra"))
        kw = {k: spec[k] for k in ("res", "tris", "allowed", "smooth", "sharp", "sym", "hides", "inner") if k in spec}
        pc = hum.piece(spec["name"], spec["field"], lo, hi, **kw)
        if pc:
            out.append((pc, spec["mat"]))
    return out


# ---------------------------------------------------------------- characters
CHAR_STATS = {}


def make_character(name, key, look, outfit, pose="idle"):
    hum = human(key)
    coll = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(coll)
    arm = H.make_armature(name + "_Rig", hum.skel, coll)
    objs = [arm]
    tris = {}
    # headwear cuts the hair
    cut = None
    for gid in outfit:
        if gid in G.HEADWEAR and G.HEADWEAR[gid] > 0:
            cut = G.HEADWEAR[gid]
    pieces = []
    for gid in outfit:
        pieces += garment_pieces(hum, gid)
    # body: hide skin under garments
    body = hum.body
    ob = BL.make_object(name + "_Body", body["mesh"], arm, MAT.make(f"skin{look['skin']}", B.SKIN_TONES[look["skin"]][0], "skin"),
                        coll, body["weights"], copy_mesh=True)
    col = hum.body_paint(look.get("beard", 0))
    a = ob.data.color_attributes.new("Col", "FLOAT_COLOR", "POINT")
    a.data.foreach_set("color", col.astype(np.float32).ravel())
    hidden = np.zeros(len(hum.body_verts), bool)
    for pc, _ in pieces:
        if pc["covers"] is not None:
            hidden |= pc["covers"]
    if hidden.any():
        vg = ob.vertex_groups.new(name="visible")
        vg.add(np.nonzero(~hidden)[0].tolist(), 1.0, "REPLACE")
        mk = ob.modifiers.new("HideCovered", "MASK"); mk.vertex_group = "visible"
    objs.append(ob)
    tris["body"] = body["tris"] - int(body["tris"] * hidden.mean())
    # head
    hc = MAT.make(f"hair{look['hair_color']}", B.HAIR_COLORS[look["hair_color"]], "hair", grime=0.1)
    for pc in (BL.build_brows(hum), BL.build_hair(hum, look["hair"], cut), BL.build_beard(hum, look.get("beard", 0))):
        if pc:
            objs.append(BL.make_object(f"{name}_{pc['name']}", pc["mesh"], arm, hc, coll, pc["weights"]))
            tris[pc["name"]] = pc["tris"]
    em = MAT.make(f"eye_{look.get('eye', '4a3420')}", "e8e0d4", "eye", hex2=look.get("eye", "4a3420"))
    for i, (me, loc) in enumerate(BL.build_eyes(hum)):
        eo = BL.rigid_object(f"{name}_Eye{i}", me, arm, "Head", em, coll)
        eo.location = loc
        objs.append(eo)
    tris["eyes"] = 2 * 16 * 12 * 2
    for pc, mk_ in pieces:
        objs.append(BL.make_object(f"{name}_{pc['name']}", pc["mesh"], arm, mat_for(mk_), coll, pc["weights"]))
        tris[pc["name"]] = pc["tris"]
    H.pose_armature(arm, POSES[pose], WALK_DROP if pose == "walk" else (0, 0, 0))
    CHAR_STATS[name] = tris
    return objs, arm


def set_pose(arm, pose):
    H.pose_armature(arm, POSES[pose], WALK_DROP if pose == "walk" else (0, 0, 0))


# ---------------------------------------------------------------- static display copies (flat-lays, busts)
def static_copy(pc, mat, offset, name, cut_below=None, coll=None, rest=False):
    me = pc["mesh"].copy()
    me.name = name
    v = (H.mesh_arrays(pc["mesh"]) if rest else np.asarray(pc["build"], np.float64)) + np.asarray(offset)
    me.vertices.foreach_set("co", v.astype(np.float32).ravel())
    me.update()
    if cut_below is not None:
        bm = bmesh.new(); bm.from_mesh(me)
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, cut_below + offset[2]), plane_no=(0, 0, 1),
                               clear_inner=True)
        cx, cy = offset[0], offset[1]
        far = [vv for vv in bm.verts if math.hypot(vv.co.x - cx, vv.co.y - cy) > 0.2]
        bmesh.ops.delete(bm, geom=far, context="VERTS")
        bm.to_mesh(me); bm.free()
    if mat is not None:
        me.materials.clear(); me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    H.link(ob, coll)
    ob.add_rest_position_attribute = True
    return ob


def bust(key, look, x, coll, extras=(), cut_z=None, grey=False):
    hum = human(key)
    hy = hum.Wb["Head"][1, 3]
    cz = cut_z if cut_z is not None else hy + 0.005
    off = (x * 0.7071, x * 0.7071, -cz)
    skin = MAT.make("bust_grey", "9c9888", "plastic", grime=0) if grey else MAT.make(f"skin{look['skin']}", B.SKIN_TONES[look["skin"]][0], "skin")
    ob = static_copy(hum.body, skin, off, f"Bust_{key}_{x:.2f}", cz, coll)
    if not grey:
        # paint after the cut: re-evaluate the face colours on the remaining vertices (head space = build space)
        vb = np.array([v.co[:] for v in ob.data.vertices]) - np.asarray(off)
        save = hum.body_verts
        hum.body_verts = H.b2g(vb)
        col = hum.body_paint(look.get("beard", 0))
        hum.body_verts = save
        a = ob.data.color_attributes.new("Col", "FLOAT_COLOR", "POINT")
        a.data.foreach_set("color", col.astype(np.float32).ravel())
    objs = [ob]
    if not grey:
        hc = MAT.make(f"hair{look['hair_color']}", B.HAIR_COLORS[look["hair_color"]], "hair", grime=0.1)
        cut = None
        for gid in extras:
            if gid in G.HEADWEAR and G.HEADWEAR[gid] > 0:
                cut = G.HEADWEAR[gid]
        for pc in (BL.build_brows(hum), BL.build_hair(hum, look["hair"], cut), BL.build_beard(hum, look.get("beard", 0))):
            if pc:
                objs.append(static_copy(pc, hc, off, f"Bust_{pc['name']}_{x:.2f}", None, coll))
    em = MAT.make(f"eye_{look.get('eye', '4a3420')}", "e8e0d4", "eye", hex2=look.get("eye", "4a3420"))
    for i, (me, loc) in enumerate(BL.build_eyes(hum)):
        eo = bpy.data.objects.new(f"BustEye_{x:.2f}_{i}", me)
        eo.location = tuple(np.asarray(loc) + np.asarray(off))
        me.materials.append(em)
        H.link(eo, coll)
        objs.append(eo)
    for gid in extras:
        for pc, mk_ in garment_pieces(hum, gid):
            objs.append(static_copy(pc, mat_for(mk_), off, f"Bust_{pc['name']}_{x:.2f}", cz, coll))
    return objs


# ---------------------------------------------------------------- tiles
TILES = []


def want(name):
    return ONLY is None or any(name.startswith(o) for o in ONLY)


def tile(name, label, replaces, objs, **kw):
    if not want(name):
        return
    t = time.time()
    RC.render_tile(name, objs, OUT, **kw)
    TILES.append({"name": name, "label": label, "replaces": replaces})
    BL.log(f"tile {name} ({time.time() - t:.1f}s)")


LOOKS = {
    "m_base": dict(skin=1, hair="Short", hair_color=1, beard=1),
    "f_base": dict(skin=2, hair="Ponytail", hair_color=0, beard=0, eye="2a3c24"),
}

OUTFITS = [
    # tile, label, body, look, outfit ids (ClothingLibrary), replaces
    ("outfit_starter", "Starting outfit (ClothingLibrary.Starter)", "M", dict(skin=1, hair="Short", hair_color=1, beard=1),
     ["tshirt_worn", "jacket", "pants", "boots", "shoulder"]),
    ("outfit_wastelander", "Wastelander (duster, shemagh, goggles, pack)", "F", dict(skin=1, hair="Long", hair_color=2, beard=0, eye="2a3c24"),
     ["tshirt", "duster", "pants", "boots", "shemagh", "goggles", "fingerless", "backpack"]),
    ("outfit_mechanic", "Mechanic (overalls, tool belt)", "M", dict(skin=2, hair="Buzz", hair_color=0, beard=2),
     ["tshirt", "overalls", "boots", "gloves", "toolbelt", "beanie", "goggles_up"]),
    ("outfit_trader", "Trader / packer (sun hat, scav vest)", "F2", dict(skin=0, hair="Long", hair_color=3, beard=0, eye="3a5a7a"),
     ["sweater", "vest", "jeans", "boots", "sunhat", "scarf", "backpack"]),
    ("outfit_raider_boss", "Raider boss (scrap armour, skull mask)", "M2", dict(skin=0, hair="Mohawk", hair_color=5, beard=1),
     ["jacket", "vest_scrap", "jeans", "combat_boots", "gloves", "shoulder", "arm_guards", "shin_guards", "skull_mask"]),
    ("outfit_doctor", "Doctor (long coat, gas mask)", "F", dict(skin=3, hair="Ponytail", hair_color=0, beard=0),
     ["sweater", "labcoat", "jeans", "boots", "gloves", "gasmask"]),
    ("outfit_farmer", "Farmer (cowboy hat, overalls)", "M2", dict(skin=1, hair="Short", hair_color=4, beard=2),
     ["sweater", "overalls", "boots", "cowboy", "bandana"]),
    ("outfit_soldier", "Soldier (kevlar, bomber, military pack)", "F2", dict(skin=2, hair="Buzz", hair_color=1, beard=0),
     ["bomber", "vest_kevlar", "pants", "combat_boots", "fingerless", "helmet", "milpack"]),
]

FLATLAYS = [
    ("cloth_tops", "Tops: t-shirt, tank, hoodie, wool sweater", ["tshirt", "tank", "hoodie", "sweater"]),
    ("cloth_outer", "Outerwear: leather jacket, bomber, duster, scav vest", ["jacket", "bomber", "duster", "vest"]),
    ("cloth_legs", "Legwear: cargo pants, jeans, shorts, work overalls", ["pants", "jeans", "shorts", "overalls"]),
    ("cloth_feet_hands", "Boots, combat boots, gloves, fingerless gloves", ["boots", "combat_boots", "gloves", "fingerless"]),
    ("cloth_armour", "Armour: scrap plate vest, kevlar vest, shoulder, arm and shin guards",
     ["vest_scrap", "vest_kevlar", "shoulder", "arm_guards", "shin_guards"]),
    ("cloth_bags", "Bags: backpack, military pack, tool belt", ["backpack", "milpack", "toolbelt"]),
]

# ---- base bodies (A-pose)
if want("base_"):
    for key, lk, tname, lab in (("M", "m_base", "base_male", "Base body, male (1.78 m, rugged face)"),
                                ("F", "f_base", "base_female", "Base body, female (h 0.96, build 0.9, soft face)")):
        objs, arm = make_character(tname, key, LOOKS[lk], ["underwear"], "apose")
        tile(tname, lab, "HumanRig / HumanDesign body (voxel 4 cm)", objs)

# ---- heads: 4 faces with hair / beard
if want("heads") or want("hair") or (ONLY and "dbg_head" in ONLY):
    coll = bpy.data.collections.new("Busts"); bpy.context.scene.collection.children.link(coll)
if ONLY and "dbg_head" in ONLY:
    tile("dbg_head", "debug", "-", bust("M", dict(skin=1, hair="Short", hair_color=1, beard=1), 0, coll))
    for o in bpy.data.collections["Busts"].objects:
        o.hide_render = True
if want("heads"):
    objs = []
    faces = [("M", dict(skin=1, hair="Short", hair_color=1, beard=1)), ("M2", dict(skin=0, hair="Buzz", hair_color=0, beard=2)),
             ("F", dict(skin=2, hair="Ponytail", hair_color=0, eye="2a3c24")), ("F2", dict(skin=0, hair="Long", hair_color=3, eye="3a5a7a"))]
    for i, (k, lk) in enumerate(faces):
        objs += bust(k, lk, i * 0.32, coll)
    tile("heads", "Heads: rugged, lean (M) / soft, sharp (F) faces, stubble + full beard", "HumanDesign head SDF + HairCap", objs)
if want("hair"):
    objs = []
    styles = [("M", "Bald", 0, 2, 1), ("M2", "Buzz", 1, 1, 0), ("M", "Short", 2, 2, 1), ("M2", "Mohawk", 5, 1, 0),
              ("F", "Ponytail", 3, 0, 2), ("F2", "Long", 1, 0, 3)]
    for i, (k, st, hc, bd, sk) in enumerate(styles):
        objs += bust(k, dict(skin=sk, hair=st, hair_color=hc, beard=bd), i * 0.32, coll)
    tile("hair", "Hair styles Bald / Buzz / Short / Mohawk / Ponytail / Long in HairColors", "HairStyle + HairStrands caps", objs)

# ---- outfits
CHARS = {}
for tname, lab, key, look, outfit in OUTFITS:
    if not want(tname) and not (tname == "outfit_starter" and want("pose")) and not want("lineup"):
        continue
    objs, arm = make_character(tname, key, look, outfit, "idle")
    CHARS[tname] = (objs, arm)
    tile(tname, lab, "NPC / player outfit: " + ", ".join(outfit), objs)

if want("lineup") and len(CHARS) == len(OUTFITS):
    allobjs = []
    for i, (tname, *_rest) in enumerate(OUTFITS):
        objs, arm = CHARS[tname]
        arm.location = ((i - 3.5) * 0.62 * 0.7071, (i - 3.5) * 0.62 * 0.7071, 0)
        allobjs += objs
    bpy.context.view_layer.update()
    tile("lineup", "All eight outfits", "NpcProfile outfits", allobjs, size=1024)
    for tname, *_rest in OUTFITS:
        CHARS[tname][1].location = (0, 0, 0)

# ---- poses
if want("pose") and "outfit_starter" in CHARS:
    objs, arm = CHARS["outfit_starter"]
    set_pose(arm, "idle")
    tile("pose_idle", "Pose: standing idle (breathing stance)", "HumanAnimator idle", objs)
    set_pose(arm, "walk")
    tile("pose_walk", "Pose: walking stride", "HumanAnimator gait", objs)
    set_pose(arm, "drive")
    # cockpit props from the posed skeleton
    hum = human("M")
    W = H.fk(hum.skel, POSES["drive"])
    hl, hr = W["HandL"][:3, 3], W["HandR"][:3, 3]
    props = []
    seat_m = MAT.make("m_seat", "302d33", "leather")
    wheel_m = MAT.make("m_wheel", "18171c", "rubber")
    floor_m = MAT.make("m_floor", "4f4842", "metal", hex2="80401d")
    def box(name, c, size, mat, rot=(0, 0, 0)):
        bpy.ops.mesh.primitive_cube_add(size=1, location=tuple(H.g2b(c)))
        o = bpy.context.active_object; o.name = name
        o.scale = (size[0], size[2], size[1]); o.rotation_euler = rot
        o.data.materials.append(mat)
        bpy.ops.object.shade_auto_smooth() if hasattr(bpy.ops.object, "shade_auto_smooth") else None
        bev = o.modifiers.new("bev", "BEVEL"); bev.width = 0.02; bev.segments = 3
        props.append(o)
    pel = W["Pelvis"][:3, 3]
    box("Seat", (0, pel[1] - 0.16, pel[2] - 0.02), (0.5, 0.08, 0.5), seat_m)
    box("SeatBack", (0, pel[1] + 0.25, pel[2] - 0.22), (0.48, 0.62, 0.08), seat_m, (math.radians(-8), 0, 0))
    fl, fr = W["FootL"][:3, 3], W["FootR"][:3, 3]
    fy = min(fl[1], fr[1]) - 0.07
    box("Floor", (0, fy - 0.02, (fl[2] + pel[2]) / 2 + 0.1), (0.7, 0.04, 1.3), floor_m)
    c = (hl + hr) / 2 + np.array([0, 0.0, 0.03])
    r = np.linalg.norm(hl - hr) / 2
    bpy.ops.mesh.primitive_torus_add(major_radius=r, minor_radius=0.014, major_segments=40, minor_segments=10, location=tuple(H.g2b(c)),
                                     rotation=(math.radians(62), 0, 0))
    wo = bpy.context.active_object; wo.name = "Wheel"; wo.data.materials.append(wheel_m); props.append(wo)
    bpy.ops.object.shade_smooth()
    col_ = (c + np.array([0, -0.18, 0.12]))
    bpy.ops.mesh.primitive_cylinder_add(radius=0.022, depth=0.4, location=tuple(H.g2b(c + np.array([0, -0.09, 0.15]))),
                                        rotation=(math.radians(62), 0, 0))
    co = bpy.context.active_object; co.data.materials.append(wheel_m); props.append(co)
    tile("pose_drive", "Pose: seated driving, hands on the wheel", "HumanAnimator.Sit (driving)", objs + props)
    for o in props:
        o.hide_render = True
    set_pose(arm, "idle")

# ---- clothing flat-lays (garments in the A build pose, side by side)
if any(want(n) for n, *_ in FLATLAYS) or want("cloth_head") or want("cloth_face"):
    coll = bpy.data.collections.new("FlatLays"); bpy.context.scene.collection.children.link(coll)
    hum = human("M")
    for tname, lab, ids in FLATLAYS:
        if not want(tname):
            continue
        objs = []
        x = 0.0
        for gid in ids:
            pcs = garment_pieces(hum, gid)
            if not pcs:
                continue
            allv = np.concatenate([H.mesh_arrays(pc["mesh"]) for pc, _ in pcs])
            w0, w1 = allv[:, 0].min(), allv[:, 0].max()
            off = ((x - w0) * 0.7071, (x - w0) * 0.7071, -allv[:, 2].min() + 0.002)   # rests on the ground
            for pc, mk_ in pcs:
                objs.append(static_copy(pc, mat_for(mk_), off, f"FL_{pc['name']}", None, coll, rest=True))
            x += (w1 - w0) + 0.1
        tile(tname, lab, "ClothingLibrary garments (inflated voxel shells)", objs, size=512)
    for tname, lab, ids in (("cloth_head", "Headwear: cowboy hat, sun hat, beanie, scrap helmet (on mannequin heads)",
                             ["cowboy", "sunhat", "beanie", "helmet"]),
                            ("cloth_face", "Face: goggles, bandana, shemagh, scarf, gas mask, skull mask",
                             ["goggles", "bandana", "shemagh", "scarf", "gasmask", "skull_mask"])):
        if not want(tname):
            continue
        objs = []
        for i, gid in enumerate(ids):
            objs += bust("M", LOOKS["m_base"], i * 0.42, coll, [gid], grey=True)
        tile(tname, lab, "ClothingLibrary Head / Face slot items (props)", objs)

# ---------------------------------------------------------------- outputs
os.makedirs(OUT, exist_ok=True)
tj = os.path.join(OUT, "tiles.json")
old = []
if ONLY and os.path.exists(tj):
    old = [t for t in json.load(open(tj)) if t["name"] not in {x["name"] for x in TILES}]
order = [t["name"] for t in old + TILES]
json.dump(old + TILES, open(tj, "w"), indent=1)

stats = {"pieces": {k: {n: p["tris"] for n, p in h.pieces.items() if p} for k, h in HUMANS.items()}, "characters": CHAR_STATS}
json.dump(stats, open(os.path.join(HERE, "polycounts.json"), "w"), indent=1)
for k, v in CHAR_STATS.items():
    BL.log(f"{k}: {sum(v.values())} tris")
def unwrap():
    """Smart-UV every skinned mesh (for baking the procedural materials to textures on import)."""
    seen, obs = set(), []
    for o in bpy.data.objects:
        if o.type == "MESH" and o.parent and o.parent.type == "ARMATURE" and o.data.name not in seen and not o.data.uv_layers:
            seen.add(o.data.name); obs.append(o)
    if not obs:
        return
    try:
        bpy.ops.object.select_all(action="DESELECT")
        for o in obs:
            o.hide_set(False); o.select_set(True)
        bpy.context.view_layer.objects.active = obs[0]
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.01)
        bpy.ops.object.mode_set(mode="OBJECT")
        BL.log(f"unwrapped {len(obs)} meshes")
    except Exception as e:   # noqa: BLE001
        BL.log("unwrap skipped:", e)


if ONLY is None or "save" in (ONLY or []):
    unwrap()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "hd_characters.blend"), compress=True)
BL.log(f"done in {time.time() - T0:.0f}s")
