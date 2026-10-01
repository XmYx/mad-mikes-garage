"""Author the HD character's animation clips in Blender and export them for the game.

Run:  blender -b <hd_characters.blend> -P tools/blender/hd/character/anims.py -- <out_dir> [--rig outfit_starter_Rig]
          [--clips walk,run] [--fbx] [--preview <png_dir>] [--also <dir> ...]

* Keys every clip of `clips.CLIPS` on the HD armature as an Action (quaternion keys from game-convention Euler poses,
  Bezier interpolation, Cycles modifiers on loops so the seam is smooth), samples the evaluated pose at 30 fps and
  converts it back to HumanRig local rotations (identity rest, Unity axes): `<out_dir>/<clip>.json` (the format
  `MadMax.Game.HumanClips` reads) + `<out_dir>/clips_index.json`.
* `--fbx` also writes `<out_dir>/hd_character_anims.fbx` (the armature with one NLA strip per clip) for reference.
* `--preview` renders a filmstrip per clip (side view, Workbench) for checking the motion by eye.
* `--also` copies the JSON clips to more folders (the game reads them from Assets/MadMax/Resources/CharacterAnims;
  the HD pack keeps a copy in Assets/MadMax/Models/HD/character/anims).
The blend is never saved."""
import json
import math
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Matrix, Quaternion  # noqa: E402

import clips as C  # noqa: E402

GB = np.array([[-1, 0, 0], [0, 0, -1], [0, 1, 0]], dtype=np.float64)
FPS = 30

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv and not argv[0].startswith("--") else os.path.join(HERE, "anims_out")


def opt(name, default=None, many=False):
    vals = []
    for i, a in enumerate(argv):
        if a == name:
            if i + 1 < len(argv) and not argv[i + 1].startswith("--"):
                vals.append(argv[i + 1])
            else:
                vals.append(True)
    if many:
        return vals
    return vals[0] if vals else default


RIG = opt("--rig", "outfit_starter_Rig")
ONLY = opt("--clips")
ONLY = set(ONLY.split(",")) if isinstance(ONLY, str) else None
PREVIEW = opt("--preview")
FBX = opt("--fbx", False)
ALSO = opt("--also", many=True)


def log(*a):
    print("[anims]", *a, flush=True)


arm = bpy.data.objects.get(RIG)
if arm is None:
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
log("armature", arm.name)
scene = bpy.context.scene
scene.render.fps = FPS
REST = {p: np.array(arm.data.bones[p].matrix_local.to_3x3()) for p in C.PARTS}


def game_to_basis(part, euler):
    Rg = C.unity_euler(*euler)
    Rb = GB @ Rg @ GB.T
    B = REST[part]
    return Matrix((B.T @ Rb @ B).tolist()).to_quaternion()


def basis_to_game(part, q):
    B = REST[part]
    Rb = B @ np.array(q.normalized().to_matrix()) @ B.T
    return GB.T @ Rb @ GB


def pelvis_to_basis(off):
    return (REST["Pelvis"].T @ (GB @ np.asarray(off, float))).tolist()


def pelvis_to_game(loc):
    return GB.T @ (REST["Pelvis"] @ np.asarray(loc, float))


def key_clip(name, c):
    """Builds the Action for one clip; returns (action, frame count)."""
    act = bpy.data.actions.new("clip_" + name)
    arm.animation_data_create()
    arm.animation_data.action = act
    n = max(2, int(round(c["length"] * FPS)))
    keys = list(c["keys"])
    if c["loop"] and keys[0][0] == 0.0:
        keys = keys + [(1.0, keys[0][1], keys[0][2])]
    prev = {}
    for t, pose, opts in keys:
        full, pel = C.resolve_key(pose, opts)
        f = 1 + t * n
        for p in C.PARTS:
            pb = arm.pose.bones[p]
            pb.rotation_mode = "QUATERNION"
            q = game_to_basis(p, full[p])
            if p in prev and prev[p].dot(q) < 0:       # shortest path between keys
                q.negate()
            prev[p] = q
            pb.rotation_quaternion = q
            pb.keyframe_insert("rotation_quaternion", frame=f)
        pb = arm.pose.bones["Pelvis"]
        pb.location = pelvis_to_basis(pel)
        pb.keyframe_insert("location", frame=f)
    for fc in _fcurves(act):
        for kp in fc.keyframe_points:
            kp.interpolation = "BEZIER"
            kp.handle_left_type = kp.handle_right_type = "AUTO_CLAMPED"
        if c["loop"]:
            fc.modifiers.new("CYCLES")
        fc.update()
    return act, n


def _fcurves(act):
    if hasattr(act, "fcurves") and len(getattr(act, "fcurves", [])) > 0:
        return list(act.fcurves)
    out = []                                     # Blender 4.4+/5: layered actions
    for layer in getattr(act, "layers", []):
        for strip in layer.strips:
            for bag in getattr(strip, "channelbags", []):
                out.extend(bag.fcurves)
    return out


def sample(name, c, n):
    rot, pel = [], []
    for i in range(n if c["loop"] else n + 1):
        scene.frame_set(1 + i)
        for p in C.PARTS:
            Rg = basis_to_game(p, arm.pose.bones[p].rotation_quaternion)
            q = Matrix(Rg.tolist()).to_quaternion()
            rot += [round(q.x, 5), round(q.y, 5), round(q.z, 5), round(q.w, 5)]
        g = pelvis_to_game(arm.pose.bones["Pelvis"].location)
        pel += [round(float(v), 5) for v in g]
    frames = len(pel) // 3
    # continuity: no quaternion sign flips between frames (the game lerps neighbours)
    R = np.array(rot).reshape(frames, len(C.PARTS), 4)
    for f in range(1, frames):
        flip = (R[f] * R[f - 1]).sum(1) < 0
        R[f][flip] *= -1
    out = {"name": name, "fps": FPS, "frames": frames, "length": c["length"], "loop": bool(c["loop"]), "mask": c["mask"],
           "hit": float(c.get("hit", -1.0)), "speed": float(c.get("speed", 0.0)), "gait": bool(c.get("gait", False)),
           "bones": C.PARTS, "rot": [round(float(v), 5) for v in R.ravel()], "pelvis": pel}
    return out


def check(name, c, data):
    """Round trip: the sampled pose at every key equals the authored one."""
    worst = 0.0
    n = data["frames"] if c["loop"] else data["frames"] - 1
    R = np.array(data["rot"]).reshape(data["frames"], len(C.PARTS), 4)
    for t, pose, opts in c["keys"]:
        f = t * n
        if abs(f - round(f)) > 1e-6 or round(f) >= data["frames"]:
            continue                             # keys between frames are interpolated, not hit exactly
        f = int(round(f))
        full, _ = C.resolve_key(pose, opts)
        for i, p in enumerate(C.PARTS):
            want = Matrix(C.unity_euler(*full[p]).tolist()).to_quaternion()
            x, y, z, w = R[f, i]
            got = Quaternion((w, x, y, z))
            ang = math.degrees(want.rotation_difference(got).angle)
            worst = max(worst, min(ang, 360 - ang))
    return worst


def preview(name, c, n, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    cam = bpy.data.objects.get("AnimCam")
    if cam is None:
        cd = bpy.data.cameras.new("AnimCam"); cd.type = "ORTHO"; cd.ortho_scale = 2.3
        cam = bpy.data.objects.new("AnimCam", cd); scene.collection.objects.link(cam)
        cam.location = (4.0, 0.0, 0.95); cam.rotation_euler = (math.radians(90), 0, math.radians(90))
    scene.camera = cam
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.render.resolution_x = scene.render.resolution_y = 200
    scene.render.film_transparent = False
    # only this character
    keep = set([arm.name] + [o.name for o in arm.children])
    for o in bpy.data.objects:
        if o.type in ("MESH", "CURVE"):
            o.hide_render = o.name not in keep or "__L" in o.name
    paths = []
    count = 8
    for k in range(count):
        f = 1 + int(round(k * (n - (1 if c["loop"] else 0)) / max(1, count - (1 if c["loop"] else 1))))
        scene.frame_set(f)
        p = os.path.join(out_dir, f"_{name}_{k}.png")
        scene.render.filepath = p
        bpy.ops.render.render(write_still=True)
        paths.append(p)
    return paths


os.makedirs(OUT, exist_ok=True)
index = []
strips = []
for name, c in C.CLIPS.items():
    if ONLY and name not in ONLY:
        continue
    act, n = key_clip(name, c)
    data = sample(name, c, n)
    err = check(name, c, data)
    with open(os.path.join(OUT, name + ".json"), "w") as fh:
        json.dump(data, fh, separators=(",", ":"))
    index.append({"name": name, "frames": data["frames"], "length": c["length"], "loop": c["loop"], "mask": c["mask"],
                  "hit": data["hit"], "speed": data["speed"], "keyError": round(err, 3)})
    log(f"{name}: {data['frames']} frames, key round-trip error {err:.3f} deg")
    if PREVIEW:
        preview(name, c, n, PREVIEW)
    strips.append((act, n))

with open(os.path.join(OUT, "clips_index.json"), "w") as fh:
    json.dump(index, fh, indent=1)
for d in ALSO:
    os.makedirs(d, exist_ok=True)
    for it in index:
        shutil.copy(os.path.join(OUT, it["name"] + ".json"), os.path.join(d, it["name"] + ".json"))
    shutil.copy(os.path.join(OUT, "clips_index.json"), os.path.join(d, "clips_index.json"))
    log("copied to", d)

if FBX and strips:
    ad = arm.animation_data
    ad.action = None
    start = 1
    for act, n in strips:
        tr = ad.nla_tracks.new(); tr.name = act.name
        st = tr.strips.new(act.name, start, act)
        start += n + 10
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    for o in arm.children:
        o.select_set(False)
    bpy.context.view_layer.objects.active = arm
    path = os.path.join(OUT, "hd_character_anims.fbx")
    from bpy_extras.io_utils import axis_conversion
    from io_scene_fbx import export_fbx_bin          # the operator's properties do not register in some Blender 5 builds

    class _Rep:
        def report(self, kind, msg):
            log("fbx:", kind, msg)
    export_fbx_bin.save(_Rep(), bpy.context, filepath=path, use_selection=True,
                        global_matrix=axis_conversion(to_forward="-Z", to_up="Y").to_4x4(), object_types={"ARMATURE"},
                        apply_unit_scale=True, global_scale=1.0, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z",
                        axis_up="Y", bake_space_transform=False, add_leaf_bones=False, primary_bone_axis="Y",
                        secondary_bone_axis="X", armature_nodetype="NULL", bake_anim=True, bake_anim_use_all_bones=True,
                        bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
                        bake_anim_step=1.0, bake_anim_simplify_factor=0.0, path_mode="STRIP", use_custom_props=False)
    log("fbx", path)
log("done", len(index), "clips ->", OUT)
