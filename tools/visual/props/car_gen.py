"""Generated car -> working HD car, halfway between the hand-built design and the TRELLIS model.

The TRELLIS mesh of a car (tools/visual/props/cars/glb/<id>.glb) is fitted onto the car's committed source blend
(tools/blender/hd/cars/blend/<Name>.blend) and sliced into its parts, so every part keeps what the game needs (object
names, origins = hinges, sockets, wheel radii, the root's ground height):

* fit: longest horizontal axis -> length (Blender Y, front -Y); front/back by matching the roof-line profile; scaled per
  axis onto the bounds of the car's exterior parts
* slice: each face goes to the nearest exterior part (Body, Door_*, DoorRear_*, Hood, Trunk, Bumper_F/R, Lights, Glass)
  within NEAR, else Body; faces nearest a glass polygon get a see-through glass material, nearest a lamp a lamp
  material (head in front, tail behind) — the exporter splits both by material class
* blend: per part, SMOOTH Laplacian passes, then every vertex is pulled BLEND of the way onto the original part's
  surface (clean panel lines and silhouette of the design, surface detail and texture of the generated model)
* wheels: faces inside a wheel's swept cylinder; moved onto the original hub, scaled to the original radius
* doors, hood and trunk get a 15 mm shell (not paper when open); Interior, seats, engine stay as they were

    blender -b -P tools/visual/props/car_gen.py -- <id> [<id> ...] [--blend=0.5]      # ids from cars_set.json
writes tools/blender/hd/cars_gen/<Name>.blend; export:
    blender -b tools/blender/hd/cars_gen/<Name>.blend -P tools/blender/hd/export/export_hd.py -- --group cars --out Assets/MadMax/Models/HD/cars_gen"""
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
SRC = os.path.join(REPO, "tools", "blender", "hd", "cars", "blend")
OUT = os.path.join(REPO, "tools", "blender", "hd", "cars_gen")
NEAR = 0.16
GLASS_NEAR = 0.05
CLEAN = 4
BLEND = 0.5
SMOOTH = 2
PANELS = ("Door_", "DoorRear_", "Hood", "Trunk", "Tailgate")
EXTERIOR = ("Body", "Door_", "DoorRear_", "Hood", "Trunk", "Tailgate", "Bumper_", "Lights", "Glass", "Wheel_")


def is_ext(o):
    return o.type == "MESH" and o.name.startswith(EXTERIOR) and "__" not in o.name and len(o.data.vertices) > 0


def world_bounds(objs):
    mn = Vector((1e9, 1e9, 1e9)); mx = -mn
    for o in objs:
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
    return mn, mx


def bounds_of(pts):
    return Vector([min(p[i] for p in pts) for i in range(3)]), Vector([max(p[i] for p in pts) for i in range(3)])


def profile(points, mn, mx, flip, bins=24):
    """Highest z per length slice (normalised)."""
    h = [0.0] * bins
    for p in points:
        t = (p.y - mn.y) / max(mx.y - mn.y, 1e-6)
        if flip:
            t = 1 - t
        i = min(bins - 1, max(0, int(t * bins)))
        h[i] = max(h[i], (p.z - mn.z) / max(mx.z - mn.z, 1e-6))
    return h


def import_glb(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in new if o.type == "MESH"]
    for o in meshes:
        mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
    for o in new:
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    g = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return g


def fit(g, ext):
    mn, mx = world_bounds(ext)
    gm = g.data
    gmn, gmx = bounds_of([v.co for v in gm.vertices])
    if (gmx.x - gmn.x) > (gmx.y - gmn.y):                                     # length along Blender Y
        gm.transform(Matrix.Rotation(math.radians(90), 4, "Z"))
        gmn, gmx = bounds_of([v.co for v in gm.vertices])
    body_pts = [o.matrix_world @ v.co for o in ext if o.name.startswith(("Body", "Glass", "Hood", "Trunk")) for v in o.data.vertices]
    ref = profile(body_pts, mn, mx, False)
    pts = [v.co.copy() for v in gm.vertices]
    a = profile(pts, gmn, gmx, False); b = profile(pts, gmn, gmx, True)
    err = lambda p: sum((p[i] - ref[i]) ** 2 for i in range(len(ref)))
    if err(b) < err(a):                                                       # front/back by the roof line
        gm.transform(Matrix.Rotation(math.radians(180), 4, "Z"))
        gmn, gmx = bounds_of([v.co for v in gm.vertices])
    S = Matrix.Diagonal(((mx.x - mn.x) / (gmx.x - gmn.x), (mx.y - mn.y) / (gmx.y - gmn.y), (mx.z - mn.z) / (gmx.z - gmn.z), 1))
    gm.transform(Matrix.Translation(-gmn)); gm.transform(S); gm.transform(Matrix.Translation(mn))
    gm.update()
    return err(a), err(b)


def bvh_of(o):
    bm = bmesh.new(); bm.from_mesh(o.data); bm.transform(o.matrix_world)
    bm.faces.ensure_lookup_table()
    t = BVHTree.FromBMesh(bm)
    mats = [o.material_slots[f.material_index].name if o.material_slots and f.material_index < len(o.material_slots) else "" for f in bm.faces]
    bm.free()
    return t, mats


def mat_copy(base, name, alpha=None, emit=None):
    m = base.copy(); m.name = name
    p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    if alpha is not None:
        for l in list(m.node_tree.links):
            if l.to_socket == p.inputs["Alpha"]:
                m.node_tree.links.remove(l)
        p.inputs["Alpha"].default_value = alpha
        p.inputs["Roughness"].default_value = 0.05
    if emit:
        p.inputs["Emission Color"].default_value = emit
        p.inputs["Emission Strength"].default_value = 2.0
    return m


def blend_to(nb, tree, world, inv, k):
    """Laplacian smoothing, then pull each vertex k of the way onto the original part's surface (world-space BVH)."""
    for _ in range(SMOOTH):
        bmesh.ops.smooth_vert(nb, verts=nb.verts, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    for v in nb.verts:
        w = world @ v.co
        hit = tree.find_nearest(w, 0.25)
        if hit[0] is not None:
            v.co = inv @ w.lerp(hit[0], k)


def build(pid, name, k):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(SRC, name + ".blend"))
    ext = [o for o in bpy.context.scene.objects if is_ext(o)]
    g = import_glb(os.path.join(HERE, "cars", "glb", pid + ".glb"))
    ea, eb = fit(g, ext)
    base = g.data.materials[0]
    base.name = "gen_" + name
    m_glass = mat_copy(base, "glass_gen", alpha=0.35)
    pg = next(n for n in m_glass.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    for l in list(m_glass.node_tree.links):
        if l.to_socket == pg.inputs["Base Color"]:
            m_glass.node_tree.links.remove(l)
    pg.inputs["Base Color"].default_value = (0.04, 0.06, 0.07, 1)
    m_head = mat_copy(base, "lamp_head_gen", emit=(1, 0.95, 0.85, 1))
    m_tail = mat_copy(base, "lamp_tail_gen", emit=(1, 0.1, 0.05, 1))

    trees = {o.name: bvh_of(o) for o in ext if not o.name.startswith("Wheel_")}
    wheels = []
    for o in ext:
        if o.name.startswith("Wheel_"):
            mn, mx = world_bounds([o])
            wheels.append((o, (mn + mx) / 2, float(o.get("radius_m", 0.3)), float(o.get("width_m", 0.22))))

    bm = bmesh.new(); bm.from_mesh(g.data); bm.faces.ensure_lookup_table()
    uv = bm.loops.layers.uv.active
    label, kind = {}, {}
    emn, emx = world_bounds(ext)
    ground = min(h.z - r for (o, h, r, w) in wheels) + 0.03 if wheels else emn.z + 0.03
    drop = set()
    for f in bm.faces:                                                         # TRELLIS bakes a ground / shadow plane in
        c = f.calc_center_median()
        if c.z < ground and abs(f.normal.z) > 0.7 or not (emn.x - 0.03 < c.x < emx.x + 0.03 and emn.y - 0.03 < c.y < emx.y + 0.03):
            drop.add(f.index)
    for f in bm.faces:
        if f.index in drop:
            label[f.index] = "_drop"; kind[f.index] = ""
            continue
        c = f.calc_center_median()
        owner, k2 = None, ""
        for (o, hub, r, w) in wheels:
            if abs(c.x - hub.x) < w * 0.75 and math.hypot(c.y - hub.y, c.z - hub.z) < r * 1.08:
                owner = o.name
                break
        if owner is None:
            best, bd, bname, bn = None, NEAR, "", None
            for n, (t, mats) in trees.items():
                hit = t.find_nearest(c, bd)
                if hit[0] is not None and hit[3] < bd:
                    best, bd, bname, bn = n, hit[3], mats[hit[2]] if hit[2] is not None and hit[2] < len(mats) else "", hit[1]
            owner = best or "Body"
            # glass / lamps only right on the design's glass or lamp surface, facing the same way
            if bd < GLASS_NEAR and bn is not None and abs(f.normal.dot(bn)) > 0.6:
                k2 = "glass" if "glass" in bname else "lamp" if "lamp" in bname else ""
        label[f.index] = owner
        kind[f.index] = k2
    # clean cut lines: every face takes the majority label of its edge neighbours (wheels stay as cut)
    for _ in range(CLEAN):
        new = {}
        for f in bm.faces:
            if label[f.index].startswith(("Wheel_", "_drop")):
                continue
            votes = {}
            for e in f.edges:
                for g2 in e.link_faces:
                    if g2 is not f and not label[g2.index].startswith(("Wheel_", "_drop")):
                        votes[label[g2.index]] = votes.get(label[g2.index], 0) + 1
            if votes:
                top = max(votes, key=votes.get)
                if votes[top] >= 2 and top != label[f.index]:
                    new[f.index] = top
            kv = {}
            for e in f.edges:
                for g2 in e.link_faces:
                    if g2 is not f:
                        kv[kind[g2.index]] = kv.get(kind[g2.index], 0) + 1
            if kv:
                topk = max(kv, key=kv.get)
                if kv[topk] >= 2:
                    kind[f.index] = topk
        label.update(new)
    groups = {}
    for f in bm.faces:
        if label[f.index] == "_drop":
            continue
        c = f.calc_center_median()
        k2 = kind[f.index]
        mat = m_glass if k2 == "glass" else (m_head if c.y < 0 else m_tail) if k2 == "lamp" else base
        groups.setdefault(label[f.index], []).append((f, mat))

    report = {}
    for n, faces in groups.items():
        o = bpy.data.objects[n]
        world, inv = o.matrix_world.copy(), o.matrix_world.inverted()
        nb = bmesh.new()
        luv = nb.loops.layers.uv.new("UVMap")
        vmap, mats = {}, []
        for (f, mat) in faces:
            vs = []
            for v in f.verts:
                if v.index not in vmap:
                    vmap[v.index] = nb.verts.new(inv @ v.co)
                vs.append(vmap[v.index])
            try:
                nf = nb.faces.new(vs)
            except ValueError:
                continue
            if mat not in mats:
                mats.append(mat)
            nf.material_index = mats.index(mat)
            for ln, lo in zip(nf.loops, f.loops):
                ln[luv].uv = lo[uv].uv
        if n.startswith("Wheel_"):
            hub_local = inv @ next(h for (w, h, r, wd) in wheels if w.name == n)
            pts = [v.co.copy() for v in nb.verts]
            if pts:
                cen = sum(pts, Vector()) / len(pts)
                rad = max(math.hypot(p.y - cen.y, p.z - cen.z) for p in pts)
                s = float(o.get("radius_m", 0.3)) / max(rad, 1e-4)
                for v in nb.verts:
                    v.co = hub_local + (v.co - cen) * s
        elif n in trees:
            blend_to(nb, trees[n][0], world, inv, k)
        me = bpy.data.meshes.new(n)
        nb.to_mesh(me); nb.free()
        for m in mats:
            me.materials.append(m)
        old = o.data
        o.data = me
        o.modifiers.clear()
        if n.startswith(PANELS):
            sol = o.modifiers.new("shell", "SOLIDIFY"); sol.thickness = 0.015; sol.offset = -1
        if old.users == 0:
            bpy.data.meshes.remove(old)
        report[n] = len(me.polygons)
    for o in ext:
        if o.name not in groups:
            report[o.name] = "kept original"
    bm.free()
    bpy.data.objects.remove(g, do_unlink=True)
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, name + ".blend"), compress=True)
    print("[car_gen] %s: fit err front %.2f back %.2f; %s" % (name, ea, eb, json.dumps(report)), flush=True)


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    k = next((float(a.split("=")[1]) for a in args if a.startswith("--blend=")), BLEND)
    ids = [a for a in args if not a.startswith("--")]
    spec = json.load(open(os.path.join(HERE, "cars_set.json")))
    for pid in ids:
        p = next(p for p in spec["props"] if p["id"] == pid)
        build(pid, p["source"], k)


if __name__ == "__main__":
    main()
