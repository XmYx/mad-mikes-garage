"""Bake + export one HD asset for Unity (Blender 5, headless).

    blender -b <asset.blend> -P tools/blender/hd/export/export_hd.py -- --group cars --out <dir> [options]

Options
    --group G        cars | heavy | misc | character (axis convention, atlas grouping, defaults; see GROUPS)
    --out DIR        output root; the asset goes to DIR/<Asset>/ (Assets/MadMax/Models/HD/<group> in the game)
    --root NAME      export the hierarchy under this top-level object (default: every top-level non-stage object,
                     each as its own asset)
    --collection C   character: export the objects of this collection (rig + skinned meshes)
    --name N         asset name (default: root / collection name)
    --size N         atlas size (default by group; character sizes per mesh, see atlas_size)
    --ao-samples N   AO bake samples (default 48)
    --kind K         vehicle | part | prop | character (sidecar hint, readable flag; default by group)
    --no-lod         skip LOD1 / LOD2
    --cpu            bake on the CPU

Writes DIR/<Asset>/<Asset>.fbx, <Atlas>_Base.png, <Atlas>_Mask.png, [<Atlas>_Normal.png], [<Atlas>_Emission.png],
<Asset>.hd.json (sidecar). Conventions: tools/blender/hd/PIPELINE.md.
"""
import json
import math
import os
import re
import sys
import time

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

STAGE = {"Ground", "Sun", "Fill", "IsoCam", "Floor", "Cylinder"}

# axis conventions of the source scripts: cars/character map game (x, y, z) -> Blender (-x, -z, y) (a proper rotation the
# FBX/Unity chain undoes); heavy/misc used (x, -z, y) (a mirror image) and are mirrored back on export.
GROUPS = {
    "cars": dict(mirror=False, atlas="asset", size=2048, ao=0.5, kind="vehicle"),
    "heavy": dict(mirror=True, atlas="asset", size=2048, ao=0.8, kind="vehicle"),
    "misc": dict(mirror=True, atlas="asset", size=1024, ao=0.6, kind="prop"),
    "character": dict(mirror=False, atlas="object", size=1024, ao=0.06, kind="character"),
    # wave 6 content (parts_all/, items/, world/, furniture/, animals/): all use the cars mapping, no mirror
    "parts": dict(mirror=False, atlas="asset", size=512, ao=0.25, kind="part"),
    "items": dict(mirror=False, atlas="asset", size=256, ao=0.08, kind="item"),
    # hdkit (world / furniture / animals) smart-projects every mesh on its own: always one shared re-unwrap
    "world": dict(mirror=False, atlas="asset", size=1024, ao=0.8, kind="prop", unwrap=True),
    "furniture": dict(mirror=False, atlas="asset", size=512, ao=0.4, kind="prop", unwrap=True),
    "animals": dict(mirror=False, atlas="asset", size=1024, ao=0.15, kind="animal", unwrap=True),
}
SWAY_TIP = 0.12                 # HDLit _SwayTip for meshes with a Sway attribute (Col.a = 1 - Sway)

LOD_RATIOS = (0.5, 0.2)
LOD_MIN_TRIS = 800              # smaller meshes keep one level
SUFFIX_LOD = "__L"              # Body__L1, Body__L2  (never "_LOD": Unity would build its own LOD groups)
SUFFIX_SPLIT = "__"             # Door_R__Glass, Lights__Lamp_Head
AO_IN_BASE = 0.35               # share of the baked AO multiplied into the base colour (crevices the sun's shadow map misses)

# Unity local = C @ Blender local @ C^-1 (FBX "apply transform" -Z forward, Y up, then Unity's X flip)
C3 = Matrix(((-1, 0, 0), (0, 0, 1), (0, -1, 0)))
C4 = C3.to_4x4()


def log(*a):
    print("[hd-export]", *a, flush=True)


def args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    o = {"group": "cars", "out": None, "root": None, "collection": None, "name": None, "size": None, "ao_samples": 48,
         "kind": None, "lod": True, "cpu": False, "max_size": None, "skip_fresh": False}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--no-lod":
            o["lod"] = False
        elif a == "--cpu":
            o["cpu"] = True
        elif a == "--skip-fresh":
            o["skip_fresh"] = True
        elif a.startswith("--"):
            o[a[2:].replace("-", "_")] = argv[i + 1]
            i += 1
        i += 1
    for k in ("size", "ao_samples", "max_size"):
        if o[k] is not None:
            o[k] = int(o[k])
    return o


# ================================================================================================= scene preparation
def tree(root):
    return [root] + list(root.children_recursive)


def pick_assets(o):
    if o["collection"]:
        col = bpy.data.collections[o["collection"]]
        objs = list(col.all_objects)
        rigs = [x for x in objs if x.type == "ARMATURE"]
        root = rigs[0] if rigs else None
        return [(o["name"] or col.name, root, objs)]
    if o["root"]:
        r = bpy.data.objects[o["root"]]
        return [(o["name"] or asset_name(r), r, tree(r))]
    out = []
    for r in bpy.context.scene.objects:
        if r.parent is None and r.name not in STAGE and r.type in {"EMPTY", "MESH", "ARMATURE"} and not r.name.startswith(("RefOutline", "_")):
            out.append((asset_name(r), r, tree(r)))
    return out


def asset_name(r):
    """The game id when the source names it (world / furniture / animals / parts / items roots), else the root name."""
    gid = r.get("game_id") if hasattr(r, "get") else None
    return re.sub(r"[^A-Za-z0-9_.-]", "_", str(gid) if gid else r.name)


def isolate(objs):
    keep = set(objs)
    for ob in list(bpy.data.objects):
        if ob not in keep:
            bpy.data.objects.remove(ob, do_unlink=True)
    # multi-asset files number duplicate names (Body.003): give the asset its clean names back
    for ob in objs:
        base = re.sub(r"\.\d{3}$", "", ob.name)
        if base != ob.name and base not in bpy.data.objects:
            ob.name = base
    for ob in objs:
        ob.hide_render = False
        ob.hide_viewport = False
        if ob.name not in bpy.context.view_layer.objects:
            bpy.context.scene.collection.objects.link(ob)
        ob.hide_set(False)
    bpy.context.view_layer.update()


def select(objs, active=None):
    bpy.ops.object.select_all(action="DESELECT")
    for ob in objs:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = active or (objs[0] if objs else None)


def apply_modifiers(meshes, drop=()):
    """Bake every modifier except the armature into the mesh (bevels, geometry nodes); modifier types in `drop` are
    removed instead (character: the MASK hiding skin under the outfit - the game layers garments over a full body)."""
    for ob in meshes:
        for m in [m for m in ob.modifiers if m.type in drop]:
            ob.modifiers.remove(m)
        mods = [m for m in ob.modifiers if m.type != "ARMATURE"]
        if not mods:
            continue
        if ob.data.users > 1:
            ob.data = ob.data.copy()
        select([ob], ob)
        for m in mods:
            try:
                bpy.ops.object.modifier_apply(modifier=m.name)
            except RuntimeError as e:
                log("modifier", ob.name, m.name, "not applied:", e)
                ob.modifiers.remove(m)


def uv_overlap(objs, res=128):
    """Share of the covered atlas cells used by more than one mesh (each mesh unwrapped on its own 0..1 square)."""
    if len(objs) < 2:
        return 0.0
    count = np.zeros((res, res), np.int16)
    seen = set()
    for ob in objs:
        me = ob.data
        if me.name in seen or not me.uv_layers:
            continue
        seen.add(me.name)
        uv = np.empty(len(me.loops) * 2, np.float32)
        me.uv_layers[0].data.foreach_get("uv", uv)
        uv = uv.reshape(-1, 2)
        me.calc_loop_triangles()
        tri = np.empty(len(me.loop_triangles) * 3, np.int32)
        me.loop_triangles.foreach_get("loops", tri)
        t = uv[tri.reshape(-1, 3)]
        w = np.array([[1, 0, 0], [0, 1, 0], [0, 0, 1], [1 / 3, 1 / 3, 1 / 3], [.5, .5, 0], [0, .5, .5], [.5, 0, .5]], np.float32)
        pts = np.einsum("kj,tjc->tkc", w, t).reshape(-1, 2)
        cells = np.unique((np.clip(pts, 0, 0.9999) * res).astype(np.int32), axis=0)
        count[cells[:, 1], cells[:, 0]] += 1
    used = (count > 0).sum()
    return float((count > 1).sum()) / max(1, used)


def ensure_uvs(groups, force=False):
    """Atlas groups without UVs get one smart-projected layout shared by all their meshes. `force` re-unwraps (the
    character file packs every mesh of the file into one shared layout, useless for per-mesh atlases); so do groups
    whose meshes were unwrapped one by one (their islands would overlap in the shared atlas)."""
    for name, objs, size in groups:
        if not force and all(ob.data.uv_layers for ob in objs) and uv_overlap(objs) > 0.15:
            log("uv islands of", name, "overlap: re-unwrapping as one atlas")
            force_this = True
        else:
            force_this = force
        if not force_this and all(ob.data.uv_layers for ob in objs):
            for ob in objs:
                ob.data.uv_layers.active_index = 0
            continue
        log("unwrap", name, len(objs), "meshes")
        for ob in objs:
            while ob.data.uv_layers:
                ob.data.uv_layers.remove(ob.data.uv_layers[0])
            ob.data.uv_layers.new(name="UVMap")
        select(objs, objs[0])
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=2.5 / size, area_weight=0.6, scale_to_bounds=False)
        bpy.ops.object.mode_set(mode="OBJECT")


# ================================================================================================= materials
def principled(mat):
    if not mat or not mat.use_nodes:
        return None, None
    nt = mat.node_tree
    outs = [n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"]
    out = next((n for n in outs if n.is_active_output), outs[0] if outs else None)
    if not out or not out.inputs["Surface"].links:
        return out, None
    b = out.inputs["Surface"].links[0].from_node
    return out, (b if b.type == "BSDF_PRINCIPLED" else None)


def classify(mat):
    """glass | lamp_head | lamp_tail | lamp_amber | lamp_other | paint | opaque (paint resolved later per asset)."""
    n = mat.name.lower()
    _, b = principled(mat)
    if "glass" in n or "lens" in n or getattr(mat, "surface_render_method", "") == "BLENDED":
        return "glass"
    if b is not None:
        es = b.inputs["Emission Strength"]
        ec = b.inputs["Emission Color"]
        lit = es.links or es.default_value > 0.0
        col = ec.links or max(ec.default_value[:3]) > 0.001
        if lit and col:
            if any(k in n for k in ("tail", "red", "brake", "stop")):
                return "lamp_tail"
            if any(k in n for k in ("amber", "indicator", "orange", "blink")):
                return "lamp_amber"
            if any(k in n for k in ("head", "white", "lightw", "yellow", "lamp", "light", "beam", "fog")):
                return "lamp_head"
            return "lamp_other"
    return "opaque"


def feed(nt, inp, target):
    """Route whatever drives `inp` (link or default value) into `target`."""
    if inp.links:
        nt.links.new(inp.links[0].from_socket, target)
        return
    v = inp.default_value
    try:
        n = len(v)
    except TypeError:
        n = 0
    if target.type == "RGBA":
        target.default_value = tuple(v) if n == 4 else ((*v, 1.0) if n == 3 else (v, v, v, 1.0))
    elif target.type == "VECTOR":
        target.default_value = tuple(v[:3]) if n >= 3 else (v, v, v)
    else:
        target.default_value = v if n == 0 else v[0]


class Rewire:
    """Temporarily replace a material's surface with an emission of one channel (EMIT bakes)."""

    def __init__(self, mat, kind, paint=0.0):
        self.mat = mat
        nt = mat.node_tree
        self.out, b = principled(mat)
        self.orig = self.out.inputs["Surface"].links[0].from_socket if self.out.inputs["Surface"].links else None
        self.new = []
        em = self.node("ShaderNodeEmission")
        em.inputs["Strength"].default_value = 1.0
        col = em.inputs["Color"]
        if b is None:
            col.default_value = (0.5, 0.5, 0.5, 1) if kind == "base" else (0, 0.8, paint, 1) if kind == "props" else (1, 1, 1, 1) if kind == "alpha" else (0, 0, 0, 1)
        elif kind == "base":
            feed(nt, b.inputs["Base Color"], col)
        elif kind == "alpha":
            feed(nt, b.inputs["Alpha"], col)
        elif kind == "props":
            cc = self.node("ShaderNodeCombineColor")
            feed(nt, b.inputs["Metallic"], cc.inputs[0])
            feed(nt, b.inputs["Roughness"], cc.inputs[1])
            cc.inputs[2].default_value = paint
            nt.links.new(cc.outputs[0], col)
        elif kind == "emit":
            vm = self.node("ShaderNodeVectorMath")
            vm.operation = "SCALE"
            feed(nt, b.inputs["Emission Color"], vm.inputs[0])
            feed(nt, b.inputs["Emission Strength"], vm.inputs["Scale"])
            nt.links.new(vm.outputs[0], col)
        nt.links.new(em.outputs[0], self.out.inputs["Surface"])

    def node(self, t):
        n = self.mat.node_tree.nodes.new(t)
        self.new.append(n)
        return n

    def restore(self):
        nt = self.mat.node_tree
        if self.orig is not None:
            nt.links.new(self.orig, self.out.inputs["Surface"])
        for n in self.new:
            nt.nodes.remove(n)


def has_normal_detail(mats):
    for m in mats:
        _, b = principled(m)
        if b is not None and b.inputs["Normal"].links:
            return True
    return False


def has_alpha(mats):
    for m in mats:
        _, b = principled(m)
        if b is not None and (b.inputs["Alpha"].links or b.inputs["Alpha"].default_value < 0.999):
            return True
    return False


# ================================================================================================= baking
def setup_cycles(o, ao_dist):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    if not o["cpu"]:
        try:
            prefs = bpy.context.preferences.addons["cycles"].preferences
            for dev in ("OPTIX", "CUDA", "HIP", "ONEAPI"):
                try:
                    prefs.compute_device_type = dev
                except TypeError:
                    continue
                prefs.get_devices()
                gpus = [d for d in prefs.devices if d.type == dev]
                if gpus:
                    for d in prefs.devices:
                        d.use = d.type == dev
                    sc.cycles.device = "GPU"
                    log("bake device", dev, gpus[0].name)
                    break
        except Exception as e:  # noqa: BLE001
            log("GPU unavailable:", e)
    sc.cycles.use_denoising = False
    sc.render.bake.use_selected_to_active = False
    if not sc.world:
        sc.world = bpy.data.worlds.new("BakeWorld")
    sc.world.light_settings.distance = ao_dist


def new_image(name, size):
    img = bpy.data.images.new(name, size, size, alpha=True, float_buffer=True)
    img.colorspace_settings.name = "Non-Color"
    return img


def bake_pass(objs, mats, img, kind, samples=1, paint_mats=()):
    sc = bpy.context.scene
    sc.cycles.samples = samples
    nodes = []
    for m in mats:
        n = m.node_tree.nodes.new("ShaderNodeTexImage")
        n.image = img
        m.node_tree.nodes.active = n
        nodes.append((m, n))
    rw = []
    if kind in ("base", "props", "alpha", "emit"):
        rw = [Rewire(m, kind, 1.0 if m.name in paint_mats else 0.0) for m in mats]
    select(objs, objs[0])
    margin = max(4, img.size[0] // 128)
    t0 = time.time()
    bpy.ops.object.bake(type={"base": "EMIT", "props": "EMIT", "alpha": "EMIT", "emit": "EMIT"}.get(kind, kind),
                        margin=margin, margin_type="EXTEND", use_clear=True, target="IMAGE_TEXTURES",
                        normal_space="TANGENT")
    for r in rw:
        r.restore()
    for m, n in nodes:
        m.node_tree.nodes.remove(n)
    px = np.empty(img.size[0] * img.size[1] * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    log("  bake %-6s %dpx %.1fs" % (kind, img.size[0], time.time() - t0))
    return px.reshape(img.size[1], img.size[0], 4)


def to_srgb(x):
    x = np.clip(x, 0.0, 1.0)
    return np.where(x <= 0.0031308, x * 12.92, 1.055 * np.power(x, 1 / 2.4) - 0.055)


def blur3(a):
    p = np.pad(a, 1, mode="edge")
    return sum(p[1 + dy:p.shape[0] - 1 + dy, 1 + dx:p.shape[1] - 1 + dx] for dy in (-1, 0, 1) for dx in (-1, 0, 1)) / 9.0


def save_png(path, rgba):
    h, w = rgba.shape[:2]
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=True, float_buffer=False)
    img.colorspace_settings.name = "Non-Color"           # values are written as given (already encoded)
    img.pixels.foreach_set(np.ascontiguousarray(np.clip(rgba, 0, 1), dtype=np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def paint_mask(base, pid):
    """Where the main body paint shows (not rusted, dusted or chipped): paint-material texels whose colour stays close
    to the paint's own colour (chromaticity and brightness). Returns (mask, reference linear colour)."""
    sel = pid > 0.5
    if sel.sum() < 16:
        return np.zeros(pid.shape, np.float32), None
    ref = np.median(base[sel], axis=0)
    s = base.sum(-1, keepdims=True) + 1e-4
    ch = base / s
    rch = ref / (ref.sum() + 1e-4)
    dc = np.abs(ch - rch).sum(-1)
    lum = base @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    rl = float(ref @ np.array([0.2126, 0.7152, 0.0722], np.float32)) + 1e-4
    ml = np.clip(1.0 - np.abs(np.log2(np.maximum(lum, 1e-4) / rl)) / 1.4, 0, 1)
    mc = np.clip(1.0 - dc / 0.16, 0, 1)
    m = np.clip(pid, 0, 1) * np.clip(mc * 1.6, 0, 1) * np.clip(ml * 1.6, 0, 1)
    return blur3(m).astype(np.float32), ref.tolist()


def bake_atlas(name, objs, size, out_dir, o, paint_mats):
    mats = []
    for ob in objs:
        for m in ob.data.materials:
            if m and m not in mats:
                mats.append(m)
    t0 = time.time()
    base = bake_pass(objs, mats, new_image(name + "_b", size), "base")
    props = bake_pass(objs, mats, new_image(name + "_p", size), "props", paint_mats=paint_mats)
    ao = bake_pass(objs, mats, new_image(name + "_ao", size), "AO", samples=o["ao_samples"])
    alpha = bake_pass(objs, mats, new_image(name + "_a", size), "alpha") if has_alpha(mats) else None
    emit_mats = [m for m in mats if classify(m).startswith("lamp")]
    emit = bake_pass(objs, mats, new_image(name + "_e", size), "emit") if emit_mats else None
    normal = bake_pass(objs, mats, new_image(name + "_n", size), "NORMAL") if has_normal_detail(mats) else None

    ao1 = np.clip(blur3(ao[..., 0]), 0, 1)
    b = np.clip(base[..., :3], 0, 1)
    mask, ref = paint_mask(b, props[..., 2]) if paint_mats else (np.zeros(ao1.shape, np.float32), None)
    files = {}
    a = alpha[..., 0] if alpha is not None else np.ones(ao1.shape, np.float32)
    rgb = to_srgb(b * (1.0 - AO_IN_BASE + AO_IN_BASE * ao1[..., None]))
    save_png(os.path.join(out_dir, name + "_Base.png"), np.dstack([rgb, a]))
    files["base"] = name + "_Base.png"
    save_png(os.path.join(out_dir, name + "_Mask.png"),
             np.dstack([np.clip(props[..., 0], 0, 1), ao1, mask, 1.0 - np.clip(props[..., 1], 0, 1)]))
    files["mask"] = name + "_Mask.png"
    escale = 1.0
    if emit is not None:
        e = np.maximum(emit[..., :3], 0)
        escale = float(max(1.0, e.max()))
        save_png(os.path.join(out_dir, name + "_Emission.png"), np.dstack([to_srgb(e / escale), np.ones(ao1.shape)]))
        files["emission"] = name + "_Emission.png"
    if normal is not None:
        save_png(os.path.join(out_dir, name + "_Normal.png"), np.dstack([normal[..., :3], np.ones(ao1.shape)]))
        files["normal"] = name + "_Normal.png"
    for img in [i for i in bpy.data.images if i.name.startswith(name + "_")]:
        bpy.data.images.remove(img)
    log("atlas %s %d done in %.1fs" % (name, size, time.time() - t0))
    return {"name": name, "size": size, "baseMap": files.get("base", ""), "maskMap": files.get("mask", ""),
            "normalMap": files.get("normal", ""), "emissionMap": files.get("emission", ""), "emissionScale": escale,
            "paintRef": ref or [], "hasAlpha": alpha is not None, "materials": [m.name for m in mats]}, b


def vertex_colours(ob, base_lin):
    """Per-vertex average of the baked base colour (sRGB bytes; CPU code reads it for debris and chips). A = 1 (rooted:
    no grass sway)."""
    me = ob.data
    h, w = base_lin.shape[:2]
    uv = me.uv_layers[0].data
    n = len(me.loops)
    uvs = np.empty(n * 2, np.float32)
    uv.foreach_get("uv", uvs)
    uvs = uvs.reshape(-1, 2)
    vi = np.empty(n, np.int32)
    me.loops.foreach_get("vertex_index", vi)
    x = np.clip((uvs[:, 0] % 1.0) * w, 0, w - 1).astype(np.int32)
    y = np.clip((uvs[:, 1] % 1.0) * h, 0, h - 1).astype(np.int32)
    c = base_lin[y, x]
    acc = np.zeros((len(me.vertices), 3), np.float32)
    cnt = np.zeros(len(me.vertices), np.float32)
    np.add.at(acc, vi, c)
    np.add.at(cnt, vi, 1)
    col = to_srgb(acc / np.maximum(cnt, 1)[:, None])
    alpha = np.ones((len(col), 1), np.float32)
    sw = me.attributes.get("Sway")
    swayed = False
    if sw is not None and sw.domain == "POINT":
        if sw.data_type == "FLOAT":
            s = np.empty(len(me.vertices), np.float32)
            sw.data.foreach_get("value", s)
        else:                                             # colour attribute: red channel
            c4 = np.empty(len(me.vertices) * 4, np.float32)
            sw.data.foreach_get("color", c4)
            s = c4[0::4].copy()
        alpha[:, 0] = 1.0 - np.clip(s, 0, 1)             # voxel convention: 1 = rooted, 0 = free tip
        swayed = bool((s > 0.01).any())
    for a in list(me.color_attributes):
        me.color_attributes.remove(a)
    attr = me.color_attributes.new("Col", "BYTE_COLOR", "POINT")
    attr.data.foreach_set("color_srgb", np.hstack([col, alpha]).astype(np.float32).ravel())
    me.color_attributes.active_color = attr
    me.color_attributes.render_color_index = 0
    return swayed


def strip_attributes(me):
    keep = {"position", "material_index", "sharp_edge", "sharp_face", "Col"}
    for a in list(me.attributes):
        if a.name in keep or a.name.startswith(".") or a.is_internal or a.is_required:
            continue
        if a.name in me.uv_layers:
            continue
        try:
            me.attributes.remove(a)
        except RuntimeError:
            pass
    if "Wear" not in me.uv_layers:
        w = me.uv_layers.new(name="Wear")         # runtime scrape amount (VehicleBreakables), zero at import
        z = np.zeros(len(me.loops) * 2, np.float32)
        w.data.foreach_set("uv", z)
    me.uv_layers.active_index = 0


# ================================================================================================= splitting / LODs
def split_roles(ob, classes):
    """Glass and lamp faces of a mixed mesh go to child objects <Name>__Glass / __Lamp_Head ... (same pivot)."""
    me = ob.data
    roles = {}
    for i, m in enumerate(me.materials):
        c = classes.get(m.name, "opaque") if m else "opaque"
        if c == "glass" or c.startswith("lamp"):
            roles.setdefault(c, []).append(i)
    if not roles:
        return []
    mi = np.empty(len(me.polygons), np.int32)
    me.polygons.foreach_get("material_index", mi)
    present = {c: idx for c, idx in roles.items() if np.isin(mi, idx).any()}
    if not present:
        return []
    if len(present) == 1 and np.isin(mi, list(present.values())[0]).all():
        return []                                   # the whole object is glass / one lamp kind: it keeps its own name
    if ob.name.lower() == "glass":
        present.pop("glass", None)                  # the glazing object keeps its seals
        if not present:
            return []
    made = []
    if me.users > 1:
        ob.data = me = me.copy()
    for c, idx in present.items():
        role = "Glass" if c == "glass" else "Lamp_" + c[5:].capitalize()
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index not in idx], context="FACES")
        nm = bpy.data.meshes.new(ob.name + SUFFIX_SPLIT + role)
        bm.to_mesh(nm)
        bm.free()
        for m in me.materials:
            nm.materials.append(m)
        child = bpy.data.objects.new(ob.name + SUFFIX_SPLIT + role, nm)
        for col in ob.users_collection:
            col.objects.link(child)
        arm = next((m for m in ob.modifiers if m.type == "ARMATURE"), None)
        if arm is not None:                          # skinned: a sibling bound to the same rig
            child.parent = ob.parent
            child.matrix_parent_inverse = ob.matrix_parent_inverse.copy()
            child.matrix_basis = ob.matrix_basis.copy()
            for vg in ob.vertex_groups:
                child.vertex_groups.new(name=vg.name)
            cm = child.modifiers.new("Armature", "ARMATURE")
            cm.object = arm.object
        else:
            child.parent = ob
            child.matrix_parent_inverse = Matrix()
            child.matrix_basis = Matrix()
        made.append(child)
    bm = bmesh.new()
    bm.from_mesh(me)
    allidx = [i for v in present.values() for i in v]
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index in allidx], context="FACES")
    bm.to_mesh(me)
    bm.free()
    return made


def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def make_lods(ob, skinned):
    out = []
    if tri_count(ob) < LOD_MIN_TRIS:
        return out
    for lvl, ratio in enumerate(LOD_RATIOS, 1):
        lod = ob.copy()
        lod.data = ob.data.copy()
        lod.name = lod.data.name = ob.name + SUFFIX_LOD + str(lvl)
        for col in ob.users_collection:
            col.objects.link(lod)
        for m in list(lod.modifiers):
            if m.type != "ARMATURE":
                lod.modifiers.remove(m)
        dec = lod.modifiers.new("dec", "DECIMATE")
        dec.decimate_type = "COLLAPSE"
        dec.ratio = ratio
        dec.use_collapse_triangulate = True
        select([lod], lod)
        while lod.modifiers[0] != dec:
            bpy.ops.object.modifier_move_up(modifier=dec.name)
        bpy.ops.object.modifier_apply(modifier=dec.name)
        if skinned:
            lod.parent = ob.parent
            lod.matrix_parent_inverse = ob.matrix_parent_inverse.copy()
            lod.matrix_basis = ob.matrix_basis.copy()
        else:
            for c in list(lod.children):
                c.parent = None
                bpy.data.objects.remove(c, do_unlink=True)
            lod.parent = ob
            lod.parent_type = "OBJECT"
            lod.matrix_parent_inverse = Matrix()
            lod.matrix_basis = Matrix()
        out.append(lod)
    return out


# ================================================================================================= transforms
MIRROR = Matrix.Scale(-1, 4, (1, 0, 0))


def mirror_hierarchy(objs):
    """heavy/misc were authored as the mirror image of the game: conjugate every local transform by the X mirror and
    mirror the mesh data once (normals flipped back)."""
    seen = set()
    for ob in objs:
        ob.matrix_basis = MIRROR @ ob.matrix_basis @ MIRROR
        ob.matrix_parent_inverse = MIRROR @ ob.matrix_parent_inverse @ MIRROR
        if ob.type == "MESH" and ob.data.name not in seen:
            seen.add(ob.data.name)
            ob.data.transform(MIRROR)
            ob.data.flip_normals()
        if ob.type == "ARMATURE":
            raise RuntimeError("mirroring armatures is not supported")


def game_matrix(m):
    """Blender local 4x4 -> Unity/game local 4x4."""
    return C4 @ m @ C4.inverted()


def game_vec(v):
    return list(C3 @ Vector(v))


def props_of(ob):
    """Custom properties as a JsonUtility-friendly list: {k, s (text), n (number / bool), v (number array)}."""
    out = []
    for k in ob.keys():
        if k.startswith("_") or k in ("cycles",):
            continue
        v = ob[k]
        e = {"k": k, "s": "", "n": 0.0, "v": []}
        if isinstance(v, bool):
            e["n"] = 1.0 if v else 0.0
            e["s"] = "true" if v else "false"
        elif isinstance(v, (int, float)):
            e["n"] = float(v)
            e["s"] = str(v)
        elif isinstance(v, str):
            e["s"] = v
        else:
            try:
                e["v"] = [float(x) for x in v]
            except TypeError:
                e["s"] = str(v)
        out.append(e)
    return out


def round_list(v, n=5):
    return [round(float(x), n) for x in v]


def mesh_bounds_game(ob):
    """Bounds of the mesh in its own (game-local) frame: (min, max)."""
    co = np.empty(len(ob.data.vertices) * 3, np.float32)
    ob.data.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    if not len(co):
        return [0, 0, 0], [0, 0, 0]
    g = co @ np.array(C3, np.float32).T
    return round_list(g.min(0)), round_list(g.max(0))


def trs(m):
    """Decompose with a mirror kept on X (like Unity's localScale.x = -1 for left-hand parts) instead of the
    all-negative scale + 180 degree turn mathutils gives for reflections."""
    loc = m.to_translation()
    m3 = m.to_3x3()
    sx, sy, sz = m3.col[0].length, m3.col[1].length, m3.col[2].length
    if m3.determinant() < 0:
        sx = -sx
    r = Matrix((m3.col[0] / sx, m3.col[1] / sy, m3.col[2] / sz)).transposed()
    return loc, r.to_quaternion(), Vector((sx, sy, sz))


def flat16(m):
    """Row-major 4x4 -> 16 floats (Unity Matrix4x4 m00 m01 m02 m03 m10 ...)."""
    return [round(float(m[r][c]), 6) for r in range(4) for c in range(4)]


def role_of(name):
    if SUFFIX_LOD in name and name.rsplit(SUFFIX_LOD, 1)[1].isdigit():
        return "lod" + name.rsplit(SUFFIX_LOD, 1)[1]
    if SUFFIX_SPLIT in name:
        return name.rsplit(SUFFIX_SPLIT, 1)[1].lower()
    return "mesh"


def sidecar_object(ob, root, atlas_of, classes):
    m = game_matrix(ob.matrix_local if ob.parent else Matrix())
    loc, rot, sca = trs(m)
    parent = ob.parent.name if ob.parent and ob != root else ""
    if root is not None and ob.parent == root and root.type == "EMPTY":
        parent = ""                                    # the asset root itself is the prefab root
    rm = C4 @ ob.matrix_world @ C4.inverted()          # root (= vehicle / prefab) space, the root sits at the origin
    rl = rm.to_translation()
    d = {"name": ob.name, "parent": parent, "parentBone": ob.parent_bone if ob.parent_type == "BONE" else "", "type": ob.type, "role": role_of(ob.name),
         "position": round_list(loc), "rotation": round_list((rot.x, rot.y, rot.z, rot.w), 6), "scale": round_list(sca),
         "rootPosition": round_list(rl), "rootMatrix": flat16(rm), "props": props_of(ob),
         "mesh": "", "tris": 0, "verts": 0, "boundsMin": [0, 0, 0], "boundsMax": [0, 0, 0], "materials": [], "classes": [],
         "atlas": "", "skinned": False}
    if ob.type == "MESH":
        me = ob.data
        d["mesh"] = me.name
        d["tris"] = tri_count(ob)
        d["verts"] = len(me.vertices)
        d["boundsMin"], d["boundsMax"] = mesh_bounds_game(ob)
        d["materials"] = [(mm.name if mm else "") for mm in me.materials]
        d["classes"] = [(classes.get(mm.name, "opaque") if mm else "") for mm in me.materials]
        d["atlas"] = atlas_of.get(ob.name, "")
        d["skinned"] = any(mo.type == "ARMATURE" for mo in ob.modifiers)
    return d


def armature_info(arm):
    bones = []
    for b in arm.data.bones:
        hm = C4 @ (arm.matrix_world @ b.matrix_local) @ C4.inverted()
        bones.append({"name": b.name, "parent": b.parent.name if b.parent else "",
                      "head": round_list(C3 @ (arm.matrix_world @ b.head_local)),
                      "tail": round_list(C3 @ (arm.matrix_world @ b.tail_local)),
                      "matrix": flat16(hm)})
    return bones


# ================================================================================================= FBX
class _Reporter:
    def report(self, kind, msg):
        log("fbx:", kind, msg)


def write_fbx(path, bake_space=True):
    """FBX for Unity: -Z forward, Y up, transform applied ("bake space transform"), FBX unit scale, no materials or
    textures, sRGB vertex colours, custom properties. Rigs (bake_space False): the standard path, the root node keeps the
    axis rotation and Unity's "Bake Axis Conversion" removes it (the applied transform is unreliable for armatures). Calls the add-on's writer directly (the operator's properties do
    not register under some Python 3.14 builds of Blender 5)."""
    from bpy_extras.io_utils import axis_conversion
    from io_scene_fbx import export_fbx_bin
    gm = axis_conversion(to_forward="-Z", to_up="Y").to_4x4()
    ret = export_fbx_bin.save(_Reporter(), bpy.context, filepath=path, use_selection=True, global_matrix=gm,
                              object_types={"EMPTY", "MESH", "ARMATURE"}, apply_unit_scale=True, global_scale=1.0,
                              apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                              bake_space_transform=bake_space, use_mesh_modifiers=True, use_mesh_modifiers_render=True,
                              mesh_smooth_type="OFF", use_subsurf=False, use_mesh_edges=False, use_tspace=False,
                              use_triangles=False, use_custom_props=True, add_leaf_bones=False, primary_bone_axis="Y",
                              secondary_bone_axis="X", use_armature_deform_only=True, armature_nodetype="NULL",
                              bake_anim=False, path_mode="STRIP", embed_textures=False, use_metadata=True,
                              colors_type="SRGB", prioritize_active_color=True)
    if ret != {"FINISHED"}:
        raise RuntimeError("FBX export failed: %s" % ret)


# ================================================================================================= main
def atlas_size(o, cfg, ob):
    n = _atlas_size(o, cfg, ob)
    return min(n, o["max_size"]) if o.get("max_size") else n


def _atlas_size(o, cfg, ob):
    if o["size"]:
        return o["size"]
    if cfg["atlas"] == "object":                       # character: body 1024, garments 512-1024, small bits less
        n = ob.name.lower()
        if n.endswith("_body"):
            return 1024
        if "eye" in n:
            return 128
        if "brows" in n or "beard" in n:
            return 256
        area = sum(p.area for p in ob.data.polygons)
        return 512 if area > 0.06 else 256              # garments / hair: 512 keeps a full wardrobe ~30 MB per body
    return cfg["size"]


ROOT_KINDS = {"building": "prop", "prop": "prop", "animal": "animal", "part": "part", "item": "item", "vehicle": "vehicle"}
EXPORTER_VERSION = 2


def game_scale(name, kind):
    """World items are shown smaller than their real size (WorldItemModels.For): the FBX keeps real size, the game
    scale is recorded here. Tools 0.7, clothing 0.45, media / seeds / ammo 0.3, other items 0.35, the rest 1."""
    if kind != "item":
        return 1.0
    n = name.lower()
    if n.startswith("tool_"):
        return 0.7
    if n.startswith("cloth_"):
        return 0.45
    if n.startswith(("book_", "vhs_", "bp_", "seed_", "ammo_", "bait_")):
        return 0.3
    if n.startswith(("world_res", "world_kit", "kit_", "animal_")):
        return 1.0
    return 0.35


def fresh(out_dir, name, src):
    """The asset's sidecar is newer than its source file and comes from this exporter version."""
    sc = os.path.join(out_dir, name + ".hd.json")
    if not os.path.exists(sc):
        return False
    t = os.path.getmtime(sc)
    if t < os.path.getmtime(src):                      # exporter changes that need a re-export bump EXPORTER_VERSION
        return False
    try:
        return json.load(open(sc)).get("exporter", 0) >= EXPORTER_VERSION
    except (OSError, ValueError):
        return False


def export_asset(name, root, objs, o, cfg):
    t0 = time.time()
    out_dir = os.path.join(o["out"], name)
    os.makedirs(out_dir, exist_ok=True)
    isolate(objs)
    meshes = [ob for ob in objs if ob.type == "MESH"]
    apply_modifiers(meshes, drop={"MASK"} if o["group"] == "character" else ())
    for ob in meshes:
        ob.data.name = ob.name if ob.data.users == 1 else ob.data.name
    # ---- atlas groups (meshes without faces are dropped: nothing to bake or draw)
    for ob in [ob for ob in meshes if not ob.data.polygons]:
        log("dropping empty mesh", ob.name)
        for c in list(ob.children):
            c.parent = ob.parent
        meshes.remove(ob)
        objs.remove(ob)
        bpy.data.objects.remove(ob, do_unlink=True)
    if cfg["atlas"] == "object":
        groups = [(ob.name, [ob], atlas_size(o, cfg, ob)) for ob in meshes]
    else:
        groups = [(name, meshes, atlas_size(o, cfg, None))]
    if cfg["atlas"] == "object":
        for ob in meshes:
            if ob.data.users > 1:
                ob.data = ob.data.copy()             # each mesh gets its own layout
    ensure_uvs(groups, force=cfg["atlas"] == "object" or (cfg.get("unwrap", False) and len(meshes) > 1))
    # ---- material classes, the body paint (vehicles)
    classes = {}
    for ob in meshes:
        for m in ob.data.materials:
            if m:
                classes[m.name] = classify(m)
    kind = o["kind"] or ROOT_KINDS.get(str(root.get("kind", "")) if root is not None else "", None) or cfg["kind"]
    paint_mats = set()
    if kind == "character":                            # skin and hair are tinted in game (Appearance)
        paint_mats = {m for m in classes if m.startswith(("skin", "hair"))}
        for m in paint_mats:
            classes[m] = "paint"
    if kind == "vehicle":
        area = {}
        for ob in meshes:
            if ob.name.lower().startswith(("wheel", "engine", "interior", "seat")):
                continue
            mi = [p.material_index for p in ob.data.polygons]
            for p, i in zip(ob.data.polygons, mi):
                m = ob.data.materials[i] if i < len(ob.data.materials) else None
                if m and "paint" in m.name.lower() and "engine" not in m.name.lower():
                    area[m.name] = area.get(m.name, 0.0) + p.area
        if (name + "_paint") in area:
            paint_mats = {name + "_paint"}
        elif area:
            paint_mats = {max(area, key=area.get)}
        for m in paint_mats:
            classes[m] = "paint"
    setup_cycles(o, cfg["ao"])
    atlases, atlas_of = {}, {}
    for an, gobjs, size in groups:
        info, base_lin = bake_atlas(an, gobjs, size, out_dir, o, paint_mats)
        atlases[an] = info
        sway = False
        for ob in gobjs:
            atlas_of[ob.name] = an
            sway |= vertex_colours(ob, base_lin)
            strip_attributes(ob.data)
        info["swayTip"] = SWAY_TIP if sway else 0.0
    # ---- split glass / lamps into children, LODs
    for ob in list(meshes):
        for c in split_roles(ob, classes):
            atlas_of[c.name] = atlas_of[ob.name]
            objs.append(c)
            meshes.append(c)
    if o["lod"]:
        for ob in list(meshes):
            if role_of(ob.name) != "mesh":
                continue
            skinned = any(mo.type == "ARMATURE" for mo in ob.modifiers)
            for lod in make_lods(ob, skinned):
                atlas_of[lod.name] = atlas_of[ob.name]
                objs.append(lod)
    # ---- root at the origin (preview offsets / turns removed), mirror fix
    root_matrix = None
    if root is not None:
        root_matrix = root.matrix_world.copy()
        root.matrix_world = Matrix()
    if cfg["mirror"]:
        mirror_hierarchy(objs)
    bpy.context.view_layer.update()
    for ob in objs:
        if ob.type == "MESH" and ob.data.users == 1:
            ob.data.name = ob.name                     # Unity names the mesh sub-assets after these
    # ---- sidecar (JsonUtility-friendly: lists, no dictionaries, no nulls)
    arm = next((ob for ob in objs if ob.type == "ARMATURE"), None)
    side = {
        "format": 2, "asset": name, "group": o["group"], "kind": kind, "source": os.path.basename(bpy.data.filepath),
        "units": "m", "axes": "unity (+X right, +Y up, +Z forward); position/rotation/scale parent-local, rootPosition/rootMatrix in prefab space",
        "mirroredOnExport": cfg["mirror"], "bakeAxisConversion": arm is not None, "readable": kind in ("vehicle", "character", "part"),
        "rootName": root.name if root is not None else "", "rootProps": props_of(root) if root is not None else [],
        "rootBlenderMatrix": [round(float(x), 6) for r in root_matrix for x in r] if root is not None else [],
        "atlases": list(atlases.values()), "materials": [{"name": m, "cls": c} for m, c in sorted(classes.items())],
        "paintMaterials": sorted(paint_mats),
        "objects": [sidecar_object(ob, root, atlas_of, classes) for ob in objs if not (ob == root and root.type == "EMPTY")],
        "bones": armature_info(arm) if arm is not None else [],
        "lodRatios": list(LOD_RATIOS) if o["lod"] else [], "lodSuffix": SUFFIX_LOD, "splitSuffix": SUFFIX_SPLIT,
        "gameId": str(root.get("game_id", name)) if root is not None else name,
        "gameScale": game_scale(name, kind), "exporter": EXPORTER_VERSION,
    }
    lo = np.array([1e9] * 3)
    hi = -lo
    for d in side["objects"]:
        if d["type"] == "MESH" and d["role"] == "mesh":
            M = np.array(d["rootMatrix"]).reshape(4, 4)
            for x in (d["boundsMin"][0], d["boundsMax"][0]):
                for y in (d["boundsMin"][1], d["boundsMax"][1]):
                    for z in (d["boundsMin"][2], d["boundsMax"][2]):
                        p = M @ np.array([x, y, z, 1.0])
                        lo = np.minimum(lo, p[:3])
                        hi = np.maximum(hi, p[:3])
    side["boundsMin"], side["boundsMax"] = round_list(lo), round_list(hi)
    # ---- FBX: one level under the root (nested children come out wrong with the applied space transform); the sidecar
    # keeps the logical parents. Rigs keep their hierarchy (bone-parented parts, written without the applied transform).
    if root is not None and arm is None:
        for ob in objs:
            if ob is root or ob.parent is None or ob.parent is root:
                continue
            mw = ob.matrix_world.copy()
            ob.parent = root
            ob.matrix_parent_inverse = Matrix()
            ob.matrix_world = mw
        bpy.context.view_layer.update()
    fbx = os.path.join(out_dir, name + ".fbx")
    select([ob for ob in objs], objs[0])
    for ob in objs:                                    # the FBX writer packs 3-vectors as doubles: int arrays fail
        for k in list(ob.keys()):
            v = ob[k]
            if hasattr(v, "to_list") and not isinstance(v, str):
                lv = v.to_list()
                if lv and all(isinstance(x, (int, float)) for x in lv):
                    ob[k] = [float(x) for x in lv]
    write_fbx(fbx, bake_space=arm is None)
    side["fbx"] = name + ".fbx"
    side["files"] = [{"file": f, "bytes": os.path.getsize(os.path.join(out_dir, f))} for f in sorted(os.listdir(out_dir))
                     if f.endswith((".fbx", ".png"))]
    side["tris"] = [sum(d["tris"] for d in side["objects"] if d["role"] not in ("lod1", "lod2")),
                    sum(d["tris"] for d in side["objects"] if d["role"] == "lod1"),
                    sum(d["tris"] for d in side["objects"] if d["role"] == "lod2")]
    with open(os.path.join(out_dir, name + ".hd.json"), "w") as f:
        json.dump(side, f, indent=1)
    log("%s: %d objects, tris %s, %.1f MB, %.1fs" % (name, len(objs), side["tris"], sum(f["bytes"] for f in side["files"]) / 1e6, time.time() - t0))


def main():
    o = args()
    cfg = dict(GROUPS[o["group"]])
    if not o["out"]:
        raise SystemExit("--out required")
    src = bpy.data.filepath
    assets = pick_assets(o)
    if not assets:
        raise SystemExit("no assets found")
    names = [a[0] for a in assets]
    log("assets:", names)
    opened = True
    failed = []
    for i, name in enumerate(names):
        if o["skip_fresh"] and fresh(os.path.join(o["out"], name), name, src):
            log("%s: up to date" % name)
            continue
        if not opened:                               # each asset from a fresh copy of the file
            bpy.ops.wm.open_mainfile(filepath=src)
        opened = False
        found = [a for a in pick_assets(o) if a[0] == name]
        if not found:
            continue
        _, root, objs = found[0]
        try:
            export_asset(name, root, list(objs), o, cfg)
        except Exception:                            # noqa: BLE001 - the other assets of the file still go
            import traceback
            log("FAILED %s\n%s" % (name, traceback.format_exc()))
            failed.append(name)
    if failed:
        log("failed assets:", failed)
        sys.exit(1)


if __name__ == "__main__":
    main()
