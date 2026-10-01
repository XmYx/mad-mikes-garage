"""Render the game's current voxel assets (dumps from vox_dump/dump_voxels.py) as small iso reference images, so
the HD versions can be compared with what they replace.

blender -b -P vox_ref.py -- <vox_dir> <out_dir> <glob...>     e.g. furniture/*.vox.txt world/*.vox.txt
Game axes (x right, y up, z front) map to Blender (-x, -z, y), like the HD assets."""
import glob
import math
import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import render_common as rc  # noqa: E402

FACES = [((1, 0, 0), [(1, -1, -1), (1, 1, -1), (1, 1, 1), (1, -1, 1)]), ((-1, 0, 0), [(-1, -1, 1), (-1, 1, 1), (-1, 1, -1), (-1, -1, -1)]),
         ((0, 1, 0), [(-1, 1, -1), (-1, 1, 1), (1, 1, 1), (1, 1, -1)]), ((0, -1, 0), [(-1, -1, 1), (-1, -1, -1), (1, -1, -1), (1, -1, 1)]),
         ((0, 0, 1), [(1, -1, 1), (1, 1, 1), (-1, 1, 1), (-1, -1, 1)]), ((0, 0, -1), [(-1, -1, -1), (-1, 1, -1), (1, 1, -1), (1, -1, -1)])]


def load(path):
    head = open(path).readline()
    size = float(head.split(" size ")[1].split()[0])
    vox = {}
    for line in open(path):
        if line.startswith("#") or not line.strip():
            continue
        x, y, z, c, m, l = line.split()
        vox[(int(x), int(y), int(z))] = (c, int(m), int(l))
    return size, vox


def mesh_of(name, size, vox):
    verts, faces, cols = [], [], []
    for (x, y, z), (c, m, l) in vox.items():
        rgb = rc.srgb(c)
        for n, quad in FACES:
            if (x + n[0], y + n[1], z + n[2]) in vox:
                continue
            base = len(verts)
            for (a, b, d) in quad:
                gx, gy, gz = (x + a * 0.5) * size, (y + b * 0.5) * size, (z + d * 0.5) * size
                verts.append((-gx, -gz, gy))
                cols.append(rgb)
            faces.append((base + 3, base + 2, base + 1, base))  # the axis map mirrors: flip winding
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    ca = me.color_attributes.new("Col", "FLOAT_COLOR", "POINT")
    ca.data.foreach_set("color", np.array(cols, dtype=np.float32).ravel())
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    mat = bpy.data.materials.get("VoxCol")
    if not mat:
        mat = bpy.data.materials.new("VoxCol")
        mat.use_nodes = True
        nt = mat.node_tree
        at = nt.nodes.new("ShaderNodeVertexColor")
        at.layer_name = "Col"
        nt.links.new(at.outputs["Color"], nt.nodes["Principled BSDF"].inputs["Base Color"])
        nt.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
    me.materials.append(mat)
    return ob


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    vdir, out = argv[0], argv[1]
    files = []
    for g in argv[2:]:
        files += sorted(glob.glob(os.path.join(vdir, g)))
    rc.reset_scene()
    rc.setup_stage()
    os.makedirs(out, exist_ok=True)
    for f in files:
        name = os.path.basename(f)[:-8]
        sub = os.path.basename(os.path.dirname(f))
        dst = os.path.join(out, sub + "__" + name + ".png")
        if os.path.exists(dst):
            continue
        size, vox = load(f)
        if not vox:
            continue
        ob = mesh_of(name, size, vox)
        rc.hide_all_but([ob])
        rc.frame_camera([ob])
        rc._render(dst, 256, True)
        bpy.data.objects.remove(ob)
        print("REF", dst)


main()
