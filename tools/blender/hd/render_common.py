"""Shared stage for the HD asset-pack preview (run inside Blender 5: `blender -b -P <script>.py -- <out_dir>`).

Every model script builds its objects, then calls `render_tile(name, objs, out_dir)`, which writes
  <out_dir>/<name>.png        detailed render (vector mode look), 512 x 512
  <out_dir>/<name>_px.png     the same view rendered at 1/4 size without anti-aliasing, nearest-upscaled
                              (what the pixel-art camera makes of the detailed asset)
Same camera (the game's isometric 3/4 view: 35 deg elevation, 45 deg azimuth, orthographic, fitted to the
bounds), same warm wasteland sun + sky fill, same neutral ground, so all tiles compose into one sheet.
Units are metres. Game axes (+X right, +Y up, +Z forward) map to Blender (x, -z, y)... i.e. build with
Blender Z up and the vehicle's front facing Blender -Y.
"""
import math
import os

import bpy
import numpy as np
from mathutils import Vector

# Game palette ramps (Assets/MadMax/Runtime/Voxel/Pal.cs), sRGB hex, dark -> light
PAL = {
    "black": ["0d0d10", "18171c", "232127", "302d33"],
    "rust": ["3f1e10", "5e2c15", "80401d", "a4582a", "c47436"],
    "chrome": ["4d4f56", "7c7f88", "aeb1b8", "e3e5e8"],
    "glass": ["10151d", "1a2330", "2c3c52", "5a7390"],
    "tire": ["121010", "1c1918", "282322", "3a3230"],
    "bronze": ["5a2c14", "7d4020", "a0582c", "c47a40"],
    "olive": ["4a2a1a", "6b3e24", "8c5634", "b07244", "cf9458"],
    "metal": ["24211f", "38332f", "4f4842", "6b625a"],
    "sand": ["8a4a24", "a65c2e", "bb6c36", "cf8044", "e09a58"],
    "wood": ["3a2414", "56361e", "74492a", "93603a", "b07a4c"],
    "cream": ["9c9888", "c4c0b0", "dcd8c8", "eeeadc", "fbf8ee"],
    "paleblue": ["4c7c96", "6ea4c0", "8cbcd6", "aad2e6", "c4e2f0"],
    "riggreen": ["141a17", "1e2824", "2a3832", "3a4c44", "4e6458"],
    "skin": ["8a5a3a", "b07a52"],
    "crimson": ["3a0c10", "5a141a", "7c1c22", "a02a2c", "c44038"],
    "navy": ["0e1630", "18244a", "243866", "34508a", "4a6aa8"],
    "moss": ["142410", "20381a", "2e5024", "40682e", "58843c"],
    "ochre": ["5a3a0c", "7c5214", "a06e1e", "c48c2a", "e0ac40"],
}


def srgb(hexstr):
    """Hex -> linear RGBA for Blender colour inputs."""
    c = [int(hexstr[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    lin = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
    return (*lin, 1.0)


def pal(name, i=-1):
    ramp = PAL[name]
    return srgb(ramp[max(0, min(len(ramp) - 1, i if i >= 0 else len(ramp) // 2))])


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = "METRIC"
    return sc


def setup_stage(sc=None):
    sc = sc or bpy.context.scene
    try:
        sc.render.engine = "BLENDER_EEVEE"
    except TypeError:
        sc.render.engine = "BLENDER_EEVEE_NEXT"
    names = [i.identifier for i in sc.view_settings.bl_rna.properties["view_transform"].enum_items]
    sc.view_settings.view_transform = "AgX" if "AgX" in names else "Standard"
    world = bpy.data.worlds.new("Wasteland")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = srgb("c9a37a")
    bg.inputs[1].default_value = 0.55
    sc.world = world
    sun = bpy.data.lights.new("Sun", "SUN")
    sun.energy = 3.4
    sun.color = srgb("ffe2b8")[:3]
    sun.angle = math.radians(3)
    so = bpy.data.objects.new("Sun", sun)
    so.rotation_euler = (math.radians(52), 0, math.radians(-38))
    sc.collection.objects.link(so)
    fill = bpy.data.lights.new("Fill", "SUN")
    fill.energy = 0.6
    fill.color = srgb("9ab4d6")[:3]
    fo = bpy.data.objects.new("Fill", fill)
    fo.rotation_euler = (math.radians(60), 0, math.radians(150))
    sc.collection.objects.link(fo)
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, 0))
    g = bpy.context.active_object
    g.name = "Ground"
    g.data.materials.append(material("ground", srgb("b49272"), rough=0.95))
    return sc


def material(name, color, rough=0.6, metal=0.0, emit=None):
    """Simple principled material (assets may use richer node trees / textures)."""
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = color
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit is not None:
        b.inputs["Emission Color"].default_value = emit
        b.inputs["Emission Strength"].default_value = 3.0
    return m


def bounds(objs):
    lo = Vector((1e9, 1e9, 1e9)); hi = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        if o.type not in {"MESH", "CURVE", "SURFACE", "META", "FONT"}:
            continue
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    return lo, hi


def frame_camera(objs, elev=35.0, azim=45.0, margin=1.12):
    sc = bpy.context.scene
    lo, hi = bounds(objs)
    centre = (lo + hi) / 2
    cam_data = bpy.data.cameras.get("IsoCam") or bpy.data.cameras.new("IsoCam")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.get("IsoCam") or bpy.data.objects.new("IsoCam", cam_data)
    if cam.name not in sc.collection.objects:
        sc.collection.objects.link(cam)
    e, a = math.radians(elev), math.radians(azim)
    d = Vector((math.cos(e) * math.sin(a), -math.cos(e) * math.cos(a), math.sin(e)))
    cam.rotation_euler = (math.radians(90 - elev), 0, a)
    cam.location = centre + d * 50
    bpy.context.view_layer.update()
    inv = cam.matrix_world.inverted()
    xs, ys = [], []
    for x in (lo.x, hi.x):
        for y in (lo.y, hi.y):
            for z in (lo.z, hi.z):
                p = inv @ Vector((x, y, z))
                xs.append(p.x); ys.append(p.y)
    cam_data.ortho_scale = max(max(xs) - min(xs), max(ys) - min(ys)) * margin
    cam.location = centre + d * 50 + cam.matrix_world.to_3x3() @ Vector(((max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2, 0))
    cam_data.clip_end = 500
    sc.camera = cam
    return cam


def _render(path, size, aa):
    sc = bpy.context.scene
    sc.render.resolution_x = sc.render.resolution_y = size
    sc.render.resolution_percentage = 100
    sc.render.filter_size = 1.5 if aa else 0.01
    try:
        sc.eevee.taa_render_samples = 32 if aa else 1
    except AttributeError:
        pass
    sc.render.image_settings.file_format = "PNG"
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def render_tile(name, objs, out_dir, size=512, pixel_div=4, elev=35.0, azim=45.0):
    """Render the detailed tile and the pixel-mode tile for `objs` (a list of objects)."""
    os.makedirs(out_dir, exist_ok=True)
    hide_all_but(objs)
    frame_camera(objs, elev, azim)
    hi = os.path.join(out_dir, name + ".png")
    _render(hi, size, True)
    lo = os.path.join(out_dir, name + "_px_small.png")
    _render(lo, size // pixel_div, False)
    img = bpy.data.images.load(lo)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    px = px.repeat(pixel_div, axis=0).repeat(pixel_div, axis=1)
    out = bpy.data.images.new(name + "_px", w * pixel_div, h * pixel_div, alpha=True)
    out.pixels[:] = px.ravel()
    out.filepath_raw = os.path.join(out_dir, name + "_px.png")
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(img)
    os.remove(lo)
    return hi


def hide_all_but(objs):
    keep = set()
    for o in objs:
        keep.add(o.name)
        keep.update(c.name for c in o.children_recursive)
    keep |= {"Ground", "Sun", "Fill", "IsoCam"}
    for o in bpy.context.scene.objects:
        o.hide_render = o.name not in keep
