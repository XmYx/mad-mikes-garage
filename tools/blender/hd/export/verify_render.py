"""Render an exported HD asset (FBX + baked atlases + sidecar) on the asset sheet's stage, to compare with the
procedural original (hd_preview/<group>/<name>.png).

    blender -b -P tools/blender/hd/export/verify_render.py -- <Models/HD/group/Asset dir> <out dir>

Writes <out>/<Asset>_rt.png and <Asset>_rt_px.png. The materials rebuild what MadMax/HDLit reads (base, mask,
emission, normal; glass dithered). heavy/misc exports are mirrored back to game handedness, so they render as the
mirror image of their sheet tiles; props render with the preview turn removed.
"""
import json
import os
import re
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import render_common as rc  # noqa: E402
from io_scene_fbx import import_fbx  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:]
d, out = os.path.abspath(argv[0]), os.path.abspath(argv[1])
side = json.load(open(os.path.join(d, os.path.basename(d) + ".hd.json")))


class _R:
    def report(self, *a):
        print(*a)


rc.reset_scene()
rc.setup_stage()
for name in ("Sun", "Fill"):
    lt = bpy.data.lights.get(name)
    if lt:
        lt.shadow_maximum_resolution = 0.002
        lt.shadow_filter_radius = 1.5
import_fbx.load(_R(), bpy.context, filepath=os.path.join(d, side["fbx"]), use_anim=False)
bpy.context.view_layer.update()
mats = {}


def material(atlas):
    if atlas in mats:
        return mats[atlas]
    a = next(x for x in side["atlases"] if x["name"] == atlas)
    m = bpy.data.materials.new(atlas)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]

    def tex(f, cs):
        n = nt.nodes.new("ShaderNodeTexImage")
        n.image = bpy.data.images.load(os.path.join(d, f))
        n.image.colorspace_settings.name = cs
        uv = nt.nodes.new("ShaderNodeUVMap")
        uv.uv_map = "UVMap"
        nt.links.new(uv.outputs[0], n.inputs[0])
        return n

    base = tex(a["baseMap"], "sRGB")
    nt.links.new(base.outputs[0], b.inputs["Base Color"])
    if a["hasAlpha"]:
        nt.links.new(base.outputs[1], b.inputs["Alpha"])
        m.surface_render_method = "DITHERED"
    mk = tex(a["maskMap"], "Non-Color")
    sp = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(mk.outputs[0], sp.inputs[0])
    nt.links.new(sp.outputs[0], b.inputs["Metallic"])
    inv = nt.nodes.new("ShaderNodeMath")
    inv.operation = "SUBTRACT"
    inv.inputs[0].default_value = 1.0
    nt.links.new(mk.outputs[1], inv.inputs[1])
    nt.links.new(inv.outputs[0], b.inputs["Roughness"])
    if a["emissionMap"]:
        em = tex(a["emissionMap"], "sRGB")
        nt.links.new(em.outputs[0], b.inputs["Emission Color"])
        b.inputs["Emission Strength"].default_value = a["emissionScale"]
    if a["normalMap"]:
        nm = tex(a["normalMap"], "Non-Color")
        nn = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(nm.outputs[0], nn.inputs[1])
        nt.links.new(nn.outputs[0], b.inputs["Normal"])
    mats[atlas] = m
    return m


objs = []
for o in list(bpy.data.objects):
    if o.type != "MESH" or o.name == "Ground":
        continue
    if re.search(r"__L\d+$", o.name):            # LOD meshes would overlap LOD0
        bpy.data.objects.remove(o)
        continue
    so = next((x for x in side["objects"] if x["name"] == o.name), None)
    if not so or not so["atlas"]:
        continue
    m = material(so["atlas"])
    o.data.materials.clear()
    o.data.materials.append(m)
    objs.append(o)
roots = [o for o in bpy.data.objects if o.parent is None and o.type in ("EMPTY", "ARMATURE")]
rc.render_tile(side["asset"] + "_rt", objs + roots, out)
