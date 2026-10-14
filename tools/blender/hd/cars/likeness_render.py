"""Likeness check renders of one built road car: side / front / rear / front three-quarter on a plain grey stage.

    blender -b cars/blend/<Name>.blend -P likeness_render.py -- <out_dir> [--size 1400]
        -> <out_dir>/<Name>_side.png, _front.png, _rear.png, _q34.png (+ _top.png with --top)
    python3 likeness_render.py montage <out_dir> <Name> <out.png> [label]
        -> one sheet: side view across the top, front / rear / three-quarter below

Orthographic cameras (the side view is a true elevation, so wheelbase, overhangs, roof and belt lines can be read
against the published dimensions); a faint 0.5 m grid on the ground plane gives the scale.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
VIEWS = {  # name: (elevation, azimuth, width, height) — azimuth 90 = the car's left side (Blender +X), 0 = front (-Y)
    "side": (0.0, 90.0, 1400, 560),
    "front": (4.0, 0.0, 600, 520),
    "rear": (4.0, 180.0, 600, 520),
    "q34": (18.0, 38.0, 800, 520),
    "top": (90.0, 90.0, 1400, 600),
}


def _blender_main():
    import bpy
    from mathutils import Vector
    sys.path[:0] = [HERE, os.path.dirname(HERE)]
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = argv[0] if argv else os.path.join(HERE, "out_likeness")
    scale = 1.0
    if "--size" in argv:
        scale = float(argv[argv.index("--size") + 1]) / VIEWS["side"][2]
    views = ["side", "front", "rear", "q34"] + (["top"] if "--top" in argv else [])
    os.makedirs(out, exist_ok=True)
    sc = bpy.context.scene
    root = next(o for o in sc.objects if o.type == "EMPTY" and o.parent is None and "game_ground_y_m" in o)
    name = root.name
    objs = [o for o in root.children_recursive if o.type == "MESH" and not o.name.startswith("RefOutline") and "__L" not in o.name]
    keep = set(o.name for o in objs) | {"Ground", "Sun", "Fill", "LikeCam"}
    for o in sc.objects:
        if o.type in ("MESH", "CURVE"):
            o.hide_render = o.name not in keep
    # plain stage: grey world, grey ground with a faint grid
    w = sc.world or bpy.data.worlds.new("Plain")
    sc.world = w
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (0.42, 0.42, 0.42, 1)
    bg.inputs[1].default_value = 0.9
    g = bpy.data.objects.get("Ground")
    if g:
        m = bpy.data.materials.new("likeness_ground")
        m.use_nodes = True
        nt = m.node_tree
        b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        tc = nt.nodes.new("ShaderNodeTexCoord")
        mp = nt.nodes.new("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (2, 2, 2)
        bk = nt.nodes.new("ShaderNodeTexChecker")
        bk.inputs["Color1"].default_value = (0.36, 0.36, 0.36, 1)
        bk.inputs["Color2"].default_value = (0.40, 0.40, 0.40, 1)
        bk.inputs["Scale"].default_value = 1.0
        nt.links.new(tc.outputs["Object"], mp.inputs["Vector"])
        nt.links.new(mp.outputs["Vector"], bk.inputs["Vector"])
        nt.links.new(bk.outputs["Color"], b.inputs["Base Color"])
        b.inputs["Roughness"].default_value = 0.95
        g.data.materials.clear()
        g.data.materials.append(m)
    try:
        sc.view_settings.look = "AgX - Base Contrast"
    except TypeError:
        pass
    lo = Vector((1e9, 1e9, 1e9))
    hi = -lo
    for o in objs:
        for c in o.bound_box:
            p = o.matrix_world @ Vector(c)
            lo = Vector(map(min, lo, p))
            hi = Vector(map(max, hi, p))
    lo.z = min(lo.z, 0.0)
    centre = (lo + hi) / 2
    cd = bpy.data.cameras.new("LikeCam")
    cd.type = "ORTHO"
    cam = bpy.data.objects.new("LikeCam", cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    for v in views:
        elev, azim, rx, ry = VIEWS[v]
        rx, ry = int(rx * scale), int(ry * scale)
        e, a = math.radians(elev), math.radians(azim)
        d = Vector((math.cos(e) * math.sin(a), -math.cos(e) * math.cos(a), math.sin(e)))
        cam.rotation_euler = (math.radians(90 - elev), 0, a)
        cam.location = centre + d * 60
        bpy.context.view_layer.update()
        inv = cam.matrix_world.inverted()
        xs, ys = [], []
        for x in (lo.x, hi.x):
            for y in (lo.y, hi.y):
                for z in (lo.z, hi.z):
                    p = inv @ Vector((x, y, z))
                    xs.append(p.x)
                    ys.append(p.y)
        wx, wy = max(xs) - min(xs), max(ys) - min(ys)
        cd.ortho_scale = max(wx, wy * rx / ry) * 1.08 if rx >= ry else max(wy, wx * ry / rx) * 1.08
        cam.location = centre + d * 60 + cam.matrix_world.to_3x3() @ Vector(((max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2, 0))
        cd.clip_end = 500
        sc.render.resolution_x, sc.render.resolution_y = rx, ry
        sc.render.resolution_percentage = 100
        sc.render.filter_size = 1.5
        try:
            sc.eevee.taa_render_samples = 32
        except AttributeError:
            pass
        sc.render.image_settings.file_format = "PNG"
        sc.render.filepath = os.path.join(out, "%s_%s.png" % (name, v))
        bpy.ops.render.render(write_still=True)
    print("[likeness] %s -> %s" % (name, out), flush=True)


def montage(d, name, dst, label=None):
    from PIL import Image, ImageDraw
    ims = {v: Image.open(os.path.join(d, "%s_%s.png" % (name, v))).convert("RGB") for v in ("side", "front", "rear", "q34")}
    W = ims["side"].width
    row = [ims["front"], ims["rear"], ims["q34"]]
    hb = ims["front"].height
    tot = sum(i.width * hb / i.height for i in row)
    k = W / tot
    row = [i.resize((int(i.width * hb / i.height * k), int(hb * k))) for i in row]
    H = ims["side"].height + row[0].height + 28
    sheet = Image.new("RGB", (W, H), (30, 30, 30))
    sheet.paste(ims["side"], (0, 28))
    x = 0
    for i in row:
        sheet.paste(i, (x, 28 + ims["side"].height))
        x += i.width
    ImageDraw.Draw(sheet).text((8, 8), label or name, fill=(235, 235, 235))
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    sheet.save(dst)
    print(dst)


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "montage":
        montage(sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5] if len(sys.argv) > 5 else None)
    else:
        _blender_main()
