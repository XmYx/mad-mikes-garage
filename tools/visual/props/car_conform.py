"""Generated car -> working HD car, v3: the design's own part meshes, conformed to the TRELLIS model and painted with it.

v2 (car_gen.py) sliced the TRELLIS shell into parts: holes where panels were cut, inside-out patches, ground skirts and
torn lids. v3 keeps every mesh of the car's committed source blend (tools/blender/hd/cars/blend/<Name>.blend) — so the
topology, panel seams, hinges (origins), sockets, glass, lamps, wheels and interior stay clean — and takes from the
generated model (tools/visual/props/<cars|heavy>/glb/<id>.glb, fitted by car_gen.fit) only:

* shape: every painted outer vertex moves up to SHAPE of the way toward the generated surface (nearest point within
  REACH whose normal agrees), the displacement field smoothed over the mesh and capped at CAP: dents and swells
  without breaking panel lines
* paint: one shared UV layout for the whole car, the generated colour baked onto it (Cycles, selected to active from
  the TRELLIS mesh) for the paint / seam / bumper-plastic materials. The design keeps its colour (most generated
  pictures are white cars): the generated light and dark (grime, dents, shading) multiply it (`_det`, luminance over
  its local mean: high-pass, so the generated model's baked light and blotches stay out), and rust / dirt (orange, saturated texels) take the generated colour (`_gen` alpha). Missed texels
  stay as designed

    blender -b -P tools/visual/props/car_conform.py -- [<id> ...] [--group=cars|heavy] [--shape=0.5] [--size=2048]
ids from cars_set.json / heavy_set.json; writes tools/blender/hd/cars_gen/<Name>.blend (heavy: heavy_gen/); run_export.py
exports cars and heavy vehicles from there when it exists (HD_CARS=design exports the hand-built ones instead)."""
import json
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import car_gen as cg  # noqa: E402  (fit helpers)

REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
HD = os.path.join(REPO, "tools", "blender", "hd")
# group -> (source blends, output folder, Prop Foundry set)
GROUPS = {"cars": (os.path.join(HD, "cars", "blend"), os.path.join(HD, "cars_gen"), "cars_set.json"),
          "heavy": (os.path.join(HD, "heavy"), os.path.join(HD, "heavy_gen"), "heavy_set.json")}
SHAPE = 0.5
REACH = 0.06
CAP = 0.025
SMOOTH = 6
PARTS = ("Body", "Door_", "DoorRear_", "Hood", "Trunk", "Tailgate", "Bumper_", "Armor_")
PAINTED = ("paint", "seam", "plastic", "body", "panel", "metal", "camo")
SKIP_MATS = ("glass", "lamp", "lens", "chrome", "seal", "rubber", "tyre", "interior", "carpet", "leather", "dash",
             "under", "wheelwell", "void", "engine", "steel_dark", "trim")
SENTINEL = (1.0, 0.0, 1.0)
DETAIL_PX = 12
DETAIL_GAIN = 0.8


def log(*a):
    print("[car_conform]", *a, flush=True)


def target_mat(m):
    n = m.name.lower() if m else ""
    return bool(m) and not any(k in n for k in SKIP_MATS) and any(k in n for k in PAINTED)


def import_fit(pid, ext, group):
    """TRELLIS mesh fitted onto the car, ground plane and faces outside the footprint dropped."""
    g = cg.import_glb(os.path.join(HERE, group, "glb", pid + ".glb"))
    cg.fit(g, ext)
    mn, mx = cg.world_bounds(ext)
    wheels = [o for o in ext if o.name.startswith("Wheel_")]
    ground = (min(cg.world_bounds([w])[0].z for w in wheels) if wheels else mn.z) + 0.03
    bm = bmesh.new(); bm.from_mesh(g.data)
    drop = []
    for f in bm.faces:
        c = f.calc_center_median()
        if c.z < ground and abs(f.normal.z) > 0.7 or not (mn.x - 0.03 < c.x < mx.x + 0.03 and mn.y - 0.03 < c.y < mx.y + 0.03):
            drop.append(f)
    bmesh.ops.delete(bm, geom=drop, context="FACES")
    bm.to_mesh(g.data); bm.free()
    return g


def conform(o, tree, k):
    """Move painted outer vertices toward the generated surface; smooth the displacement field; cap it."""
    me = o.data
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    mats = me.materials
    mw = o.matrix_world
    inv3 = mw.inverted().to_3x3()
    rot = mw.to_3x3().normalized()
    n = len(bm.verts)
    disp = np.zeros((n, 3)); w = np.zeros(n)
    for v in bm.verts:
        if not v.link_faces or not all(f.material_index < len(mats) and target_mat(mats[f.material_index]) for f in v.link_faces):
            continue
        p = mw @ v.co
        hit = tree.find_nearest(p, REACH)
        if hit[0] is None:
            continue
        if hit[1] is not None and abs((rot @ v.normal).normalized().dot(hit[1])) < 0.5:
            continue
        d = inv3 @ (hit[0] - p)
        disp[v.index] = (d.x, d.y, d.z); w[v.index] = 1.0
    nb = [[e.other_vert(v).index for e in v.link_edges] + [v.index] for v in bm.verts]
    for _ in range(SMOOTH):
        nd = disp.copy(); nw = w.copy()
        for i in range(n):
            ws = w[nb[i]]
            s = ws.sum()
            if s > 0:
                nd[i] = (disp[nb[i]] * ws[:, None]).sum(0) / s
                nw[i] = max(w[i], ws.mean())
        disp, w = nd, nw
    moved = 0
    for v in bm.verts:
        if w[v.index] <= 0:
            continue
        d = Vector(disp[v.index]) * k * min(1.0, w[v.index])
        if d.length > CAP:
            d *= CAP / d.length
        v.co += d
        moved += 1
    bm.to_mesh(me); bm.free()
    me.update()
    return moved


def unwrap(objs, size):
    for o in objs:
        me = o.data
        while me.uv_layers:
            me.uv_layers.remove(me.uv_layers[0])
        me.uv_layers.new(name="UVMap")
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=2.5 / size, area_weight=0.6, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")


def gen_material(m, img, det):
    """Copy of `m` whose base colour = design colour x 2·detail (the generated model's light and dark, 0.5 = as
    designed), then the generated colour itself where its alpha marks rust."""
    c = m.copy(); c.name = m.name + "__gen"
    nt = c.node_tree
    b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = img; tex.name = "gen_tex"
    dt = nt.nodes.new("ShaderNodeTexImage"); dt.image = det; dt.name = "gen_det"
    mul = nt.nodes.new("ShaderNodeMix"); mul.data_type = "RGBA"; mul.blend_type = "MULTIPLY"
    mul.inputs["Factor"].default_value = 1.0
    two = nt.nodes.new("ShaderNodeVectorMath"); two.operation = "SCALE"; two.inputs["Scale"].default_value = 2.0
    rust = nt.nodes.new("ShaderNodeMix"); rust.data_type = "RGBA"; rust.blend_type = "MIX"
    base = b.inputs["Base Color"]
    if base.links:
        nt.links.new(base.links[0].from_socket, mul.inputs["A"])
    else:
        mul.inputs["A"].default_value = base.default_value
    nt.links.new(dt.outputs["Color"], mul.inputs["B"])
    nt.links.new(mul.outputs["Result"], two.inputs[0])
    nt.links.new(two.outputs["Vector"], rust.inputs["A"])
    nt.links.new(tex.outputs["Color"], rust.inputs["B"])
    nt.links.new(tex.outputs["Alpha"], rust.inputs["Factor"])
    nt.links.new(rust.outputs["Result"], base)
    nt.nodes.active = tex
    return c


def box(img, r):
    """Separable box blur of a 2D array (radius r px) via cumulative sums."""
    out = img
    for ax in (0, 1):
        c = np.cumsum(np.pad(out, [(r + 1, r) if i == ax else (0, 0) for i in range(2)], mode="edge"), axis=ax)
        hi = np.take(c, np.arange(2 * r + 1, c.shape[ax]), axis=ax)
        lo = np.take(c, np.arange(0, c.shape[ax] - 2 * r - 1), axis=ax)
        out = (hi - lo) / (2 * r + 1)
    return out


def split(a, miss):
    """Baked generated colour -> (colour + rust mask in alpha, detail = luminance / median luminance x 0.5)."""
    rgb = a[..., :3]
    lum = rgb @ np.array([0.299, 0.587, 0.114], np.float32)
    hit = ~miss
    # high-pass: luminance over its local mean (masked box blur, ~DETAIL_PX), so the generated model's baked light and
    # large blotches go and only grime, dents and edges carry over
    w = hit.astype(np.float32)
    local = box(lum * w, DETAIL_PX) / np.maximum(box(w, DETAIL_PX), 1e-3)
    det = np.clip(1.0 + (lum / np.maximum(local, 1e-3) - 1.0) * DETAIL_GAIN, 0.55, 1.25) * 0.5
    det[miss] = 0.5
    mx, mn = rgb.max(-1), rgb.min(-1)
    sat = (mx - mn) / np.maximum(mx, 1e-3)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    orange = (r > g) & (g > b) & ((r - b) > 0.12)                       # rust / dirt: red > green > blue
    rust = np.clip((sat - 0.35) / 0.25, 0, 1) * orange * (mx < 0.8) * hit
    out = a.copy(); out[..., 3] = rust
    d = np.empty_like(a); d[..., 0] = d[..., 1] = d[..., 2] = det; d[..., 3] = 1.0
    return out, d


def bake(g, objs):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        prefs.compute_device_type = "CUDA"; prefs.get_devices()
        for d in prefs.devices:
            d.use = d.type != "CPU"
        sc.cycles.device = "GPU"
    except Exception as e:
        log("gpu:", e)
    sc.cycles.samples = 1
    bk = sc.render.bake
    bk.use_selected_to_active = True; bk.cage_extrusion = 0.12; bk.max_ray_distance = 0.3
    bk.margin = 3; bk.use_clear = False; bk.use_pass_direct = False; bk.use_pass_indirect = False; bk.use_pass_color = True
    dummy = bpy.data.images.new("bake_dummy", 8, 8)
    temp = []
    for m in {m for o in objs for m in o.data.materials if m}:
        if "gen_tex" in m.node_tree.nodes:
            m.node_tree.nodes.active = m.node_tree.nodes["gen_tex"]
            continue
        t = m.node_tree.nodes.new("ShaderNodeTexImage"); t.image = dummy; t.name = "bake_dummy"
        m.node_tree.nodes.active = t
        temp.append((m, t))
    for o in objs:
        bpy.ops.object.select_all(action="DESELECT")
        g.select_set(True); o.select_set(True)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"})
    for m, t in temp:
        m.node_tree.nodes.remove(t)
    bpy.data.images.remove(dummy)


def build(pid, name, k, size, group):
    src, out, _ = GROUPS[group]
    bpy.ops.wm.open_mainfile(filepath=os.path.join(src, name + ".blend"))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.name != "Ground" and "__" not in o.name]
    ext = [o for o in meshes if o.name.startswith(PARTS + ("Lights", "Glass", "Wheel_"))]
    g = import_fit(pid, ext, group)
    tree = BVHTree.FromObject(g, bpy.context.evaluated_depsgraph_get())
    targets = [o for o in meshes if o.name.startswith(PARTS)]
    for o in targets:                                                          # mirrored doors may share mesh data
        if o.data.users > 1:
            o.data = o.data.copy()
    moved = {o.name: conform(o, tree, k) for o in targets}
    unwrap(meshes, size)
    img = bpy.data.images.new(name + "_gen", size, size, alpha=True)
    det = bpy.data.images.new(name + "_det", size, size)
    det.colorspace_settings.name = "Non-Color"
    px = np.empty((size, size, 4), np.float32); px[:] = (*SENTINEL, 1.0)
    img.pixels.foreach_set(px.ravel())
    swapped = {}
    for o in targets:
        for i, m in enumerate(o.data.materials):
            if target_mat(m):
                if m.name not in swapped:
                    swapped[m.name] = gen_material(m, img, det)
                o.data.materials[i] = swapped[m.name]
    bake(g, targets)
    a = np.empty(size * size * 4, np.float32); img.pixels.foreach_get(a); a = a.reshape(size, size, 4)
    miss = (np.abs(a[..., 0] - 1) < 0.02) & (a[..., 1] < 0.02) & (np.abs(a[..., 2] - 1) < 0.02)
    col, d = split(a, miss)
    img.pixels.foreach_set(col.ravel()); img.pack()
    det.pixels.foreach_set(d.ravel()); det.pack()
    cover = 1 - miss.mean()
    bpy.data.objects.remove(g, do_unlink=True)
    os.makedirs(out, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, name + ".blend"), compress=True)
    log("%s: moved %s, materials %s, atlas cover %.2f" % (name, moved, list(swapped), cover))


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    k = next((float(a.split("=")[1]) for a in args if a.startswith("--shape=")), SHAPE)
    size = next((int(a.split("=")[1]) for a in args if a.startswith("--size=")), 2048)
    group = next((a.split("=")[1] for a in args if a.startswith("--group=")), "cars")
    ids = [a for a in args if not a.startswith("--")]
    spec = json.load(open(os.path.join(HERE, GROUPS[group][2])))
    for pid in ids or [p["id"] for p in spec["props"]]:
        p = next(p for p in spec["props"] if p["id"] == pid)
        if not os.path.exists(os.path.join(HERE, spec["set"], "glb", pid + ".glb")):
            log("no model for", pid); continue
        try:
            build(pid, p["source"], k, size, group)
        except Exception as e:                                                     # one odd rig must not stop the batch
            log("FAILED", p["source"], e)


main()
