"""Step 4 of the prop workflow (tools/visual/WORKFLOW.md): TRELLIS GLBs -> one HD source blend the exporter takes.

    blender -b -P tools/visual/props/fixup_blend.py -- street_set.json

Per prop: import the GLB, join it into one mesh, apply transforms, scale the longest side to `size_m`, origin = bottom
centre (floor mount), turned so its front faces game +Z, decimate to `faces`, parent under an empty `Gen_<id>` with ["game_id"] = "Gen_<id>" (what
export_hd.py names the asset, HDDomain.World). Writes tools/blender/hd/world/props_gen_<set>.blend; then
`python3 tools/blender/hd/export/run_export.py world/props_gen_<set>` bakes atlases + LODs as for every world prop."""
import json
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))


def bounds(objs):
    mn = Vector((1e9, 1e9, 1e9)); mx = -mn
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
    return mn, mx


def import_prop(path, p, x_slot):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in new if o.type == "MESH"]
    if not meshes:
        return None
    for o in meshes:
        mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
    for o in [o for o in new if o.type != "MESH"]:
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.rotation_euler = (0, 0, math.pi + math.radians(p.get("turn", 0)))                # TRELLIS fronts face -Z in game: turn to +Z (furniture front); `turn` per prop (a bed is drawn from its foot)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    mn, mx = bounds([ob])
    s = p["size_m"] / max(mx.x - mn.x, mx.y - mn.y, mx.z - mn.z, 1e-6)
    ob.scale = (s, s, s)
    bpy.ops.object.transform_apply(scale=True)
    mn, mx = bounds([ob])
    ob.location = (-(mn.x + mx.x) / 2, -(mn.y + mx.y) / 2, -mn.z)                    # bottom centre at the origin
    bpy.ops.object.transform_apply(location=True)
    faces = len(ob.data.polygons)
    if faces > p.get("faces", 4000):
        m = ob.modifiers.new("dec", "DECIMATE")
        m.ratio = p.get("faces", 4000) / faces
        bpy.ops.object.modifier_apply(modifier=m.name)
    ob.name = ob.data.name = "Body"
    gid = "Gen_" + p["id"]
    root = bpy.data.objects.new(gid, None)
    bpy.context.scene.collection.objects.link(root)
    root["game_id"] = gid
    ob.parent = root
    root.location = (x_slot, 0, 0)                                                        # side by side for a preview; the exporter resets the root
    return root, faces, len(ob.data.polygons)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    spec = json.load(open(os.path.join(HERE, argv[0])))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    x = 0.0
    for p in spec["props"]:
        path = os.path.join(HERE, spec["set"], "glb", p["id"] + ".glb")
        if not os.path.exists(path):
            print("[fixup] missing", p["id"]); continue
        r = import_prop(path, p, x)
        if r:
            print("[fixup] %s faces %d -> %d" % (r[0].name, r[1], r[2]))
            x += p["size_m"] + 0.8
    out = os.path.join(REPO, "tools", "blender", "hd", "world", "props_gen_%s.blend" % spec["set"])
    bpy.ops.wm.save_as_mainfile(filepath=out, compress=True)
    print("[fixup] saved", out)


main()
