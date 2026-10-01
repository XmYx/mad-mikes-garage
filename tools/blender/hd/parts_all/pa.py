"""Shared runner + helpers for the full HD coverage of vehicle parts (parts_all/) and hand tools / items (items/).

Asset convention (what the exporter and the integration rely on)
  * one root EMPTY per asset, named exactly like the game id (part key / tool id / item id), in its own collection;
    every asset is also written to its own .blend: parts_all/blend/<id>.blend, items/blend/<id>.blend
  * origin = the part's mount point / socket (side parts: right side, game +X out; wheels: inner face at x=0, axle
    centre; tools: at the grip; world items: resting on the origin, centred)
  * geometry is one mesh per rigid piece: "<id>_body" under the root, hinged pieces as child mesh objects named
    exactly like the game segments (mount, gun, rotor, lamp, boom, stick, bucket, bit ...) with origins at their pivots
  * metres, Blender Z up, game axes (x, y, z) -> Blender (-x, -z, y) (the mirror Unity's FBX importer undoes; same as
    cars/hdlib.py): front faces Blender -Y, game right (+X) is Blender -X
  * procedural materials from misc/kit.py (shared palette render_common.PAL; chips / rust / dust), emissive lamps
    LampWhite / LampAmber / LampRed (+ LampBlue, LampGreen); one smart-UV atlas per asset ready for baking
  * custom properties on the root: game_id, kind, family, category, socket, mass, size_class, radius, segments ...

Builders are written in the misc/kit.py frame (kit.V(x, y, z) = voxel coords -> (x, -z, y) m) and mirrored in X when
the asset is finalised, so every helper of misc/kit.py, misc/comps.py and misc/parts.py can be reused unchanged.
Sizes: after building, each rigid piece is compared with the game mesh bounds (game_parts.json, extracted from
Generated/Meshes) and fitted to them when it is off by more than 5 % / 2 cm (logged per asset in the manifest).

Run a family:  blender -b -P tools/blender/hd/parts_all/<family>.py
"""
import json
import math
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
HD = os.path.dirname(HERE)
for p in (HD, os.path.join(HD, "misc"), HERE):
    if p not in sys.path:
        sys.path.insert(0, p)

import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import kit  # noqa: E402
import render_common as rc  # noqa: E402
from kit import Part, V, bm_box, bm_cyl, bm_lathe, bm_tube, bm_loft  # noqa: E402,F401

PREVIEW = "/home/magix/PycharmProjects/MadMaxUnity/hd_preview"
GAME = json.load(open(os.path.join(HERE, "game_parts.json")))
AXES = "game(x,y,z)->blender(-x,-z,y)"
M = {}
REG = []


def G(x, y, z):
    """Game metres -> kit frame metres."""
    return Vector((x, -z, y))


# ------------------------------------------------------------------------------------------------ materials
def extra_mats(M):
    S, T, H, Pp = kit.surface, kit.textured, kit._hex, kit.P
    M["bar_steel"] = S("BarSteel", Pp("metal", 2), rust=0.35, dust=0.35, metal=0.75, rough=0.5, var=0.15, streaks=0.4)
    M["ram_steel"] = S("RamSteel", Pp("metal", 1), rust=0.28, dust=0.4, metal=0.6, rough=0.65, var=0.2, streaks=0.35, bump=0.6)
    M["hazard_bar"] = T("HazardStripes", "planks", H("d4b020"), Pp("black", 1), scale=1.0, dust=0.3, rough=0.6, bump=0.1, rot="X")
    M["fins"] = T("RadiatorFins", "corrugated", Pp("metal", 1), Pp("metal", 3), Pp("rust", 2), scale=2.0, dust=0.2, rough=0.5, bump=0.8, rot="X")
    M["fins_cu"] = T("RadiatorCore", "corrugated", Pp("bronze", 1), Pp("bronze", 3), Pp("rust", 1), scale=2.0, dust=0.2, rough=0.5, bump=0.8, rot="X")
    M["crane"] = S("CraneYellow", H("c89a22"), chips=0.45, rust=0.3, dust=0.4, rough=0.5, streaks=0.4)
    M["jerry"] = S("JerryOlive", Pp("olive", 2), chips=0.5, chip_col=Pp("metal", 2), rust=0.3, dust=0.4, rough=0.55)
    M["lamp_blue"] = kit.emissive("LampBlue", H("3a6aff"), 3.0)
    M["red_lens"] = kit.emissive("LampRed", H("e02818"), 3.0)
    M["cat"] = S("CatYellow", H("d4a020"), chips=0.45, rust=0.3, dust=0.45, rough=0.5, streaks=0.35)
    M["poly"] = S("PolyCream", Pp("cream", 1), dust=0.4, rough=0.7, var=0.1, bump=0.15)
    M["plastic"] = S("PlasticBlack", Pp("black", 1), dust=0.35, rough=0.55, var=0.08, bump=0.1)
    M["plastic_red"] = S("PlasticRed", Pp("crimson", 3), dust=0.25, rough=0.45, var=0.08, bump=0.1)
    M["plastic_blue"] = S("PlasticBlue", Pp("navy", 3), dust=0.25, rough=0.45, var=0.08, bump=0.1)
    M["plastic_green"] = S("PlasticGreen", Pp("moss", 3), dust=0.25, rough=0.5, var=0.08, bump=0.1)
    M["plastic_yellow"] = S("PlasticYellow", Pp("ochre", 4), dust=0.25, rough=0.45, var=0.08, bump=0.1)
    M["plastic_white"] = S("PlasticWhite", Pp("cream", 3), dust=0.25, rough=0.5, var=0.06, bump=0.1)
    M["blued"] = S("HeatBlued", H("3a3c58"), rust=0.15, dust=0.2, metal=0.9, rough=0.3, var=0.25)
    M["mesh"] = T("DiamondMesh", "corrugated", Pp("metal", 1), Pp("metal", 3), Pp("rust", 2), scale=6.0, dust=0.2, rough=0.6, bump=0.6, rot="Y")
    M["diamond"] = T("DiamondPlate", "tread", Pp("chrome", 1), Pp("chrome", 2), dust=0.3, rough=0.4, bump=0.6, rot="X")
    M["paper"] = S("Paper", Pp("cream", 3), dust=0.2, rough=0.9, var=0.06, bump=0.1)
    M["paper_old"] = S("PaperOld", H("c8b48a"), dust=0.3, rough=0.9, var=0.15, bump=0.15)
    M["cardboard"] = S("Cardboard", H("a07a4c"), dust=0.3, rough=0.9, var=0.12, bump=0.2)
    M["tin"] = S("Tinplate", Pp("chrome", 2), rust=0.15, dust=0.25, metal=0.9, rough=0.3, var=0.08, bump=0.1)
    M["label_red"] = S("LabelRed", Pp("crimson", 3), dust=0.25, rough=0.7, var=0.12, bump=0.05)
    M["label_green"] = S("LabelGreen", Pp("moss", 3), dust=0.25, rough=0.7, var=0.12, bump=0.05)
    M["label_blue"] = S("LabelBlue", Pp("navy", 3), dust=0.25, rough=0.7, var=0.12, bump=0.05)
    M["label_yellow"] = S("LabelYellow", Pp("ochre", 3), dust=0.25, rough=0.7, var=0.12, bump=0.05)
    M["label_cream"] = S("LabelCream", Pp("cream", 2), dust=0.25, rough=0.8, var=0.12, bump=0.05)
    M["burlap"] = T("Burlap", "canvas", Pp("sand", 2), Pp("sand", 3), dust=0.3, rough=0.95, bump=0.4)
    M["foam"] = S("Foam", H("e8c848"), dust=0.2, rough=0.95, var=0.1, bump=0.6)
    M["bone"] = S("Bone", Pp("cream", 2), dust=0.35, rough=0.7, var=0.15, bump=0.3)
    M["fur"] = T("Fur", "leaf", Pp("wood", 2), Pp("wood", 3), Pp("sand", 3), dust=0.2, rough=0.95, bump=0.8)
    M["glass_green"] = kit.glass("GlassGreen", tint=H("2a4a2a"))
    M["glass_brown"] = kit.glass("GlassBrown", tint=H("4a2a14"))
    M["glass_clear"] = kit.glass("GlassClear", tint=Pp("paleblue", 2))
    M["water"] = kit.glass("WaterLiquid", tint=H("4a6a7a"))
    M["flame"] = kit.emissive("Flame", H("ffa030"), 5.0)
    M["arc"] = kit.emissive("ArcBlue", H("9ad8ff"), 6.0)
    M["gold"] = S("Gold", Pp("ochre", 4), dust=0.1, metal=1.0, rough=0.25, var=0.08)
    M["silver"] = S("Silver", Pp("chrome", 3), dust=0.1, metal=1.0, rough=0.25, var=0.05)
    M["felt_green"] = S("FeltGreen", Pp("riggreen", 3), dust=0.25, rough=0.95, var=0.1, bump=0.3)
    M["denim"] = T("Denim", "canvas", Pp("navy", 2), Pp("navy", 3), dust=0.3, rough=0.95, bump=0.3)
    M["nylon"] = S("NylonOlive", Pp("riggreen", 3), dust=0.35, rough=0.75, var=0.12, bump=0.3)
    M["nylon_red"] = S("NylonRed", Pp("crimson", 2), dust=0.35, rough=0.75, var=0.12, bump=0.3)
    M["nylon_blue"] = S("NylonBlue", Pp("navy", 3), dust=0.35, rough=0.75, var=0.12, bump=0.3)
    M["hay"] = T("Hay", "hay", Pp("ochre", 3), Pp("sand", 4), Pp("ochre", 2), dust=0.1, rough=0.95, bump=0.8)
    M["webbing"] = T("Webbing", "canvas", Pp("black", 2), Pp("black", 3), dust=0.3, rough=0.9, bump=0.3)


def food_mat(name, hexcol, rough=0.6, bump=0.3, var=0.18):
    """Organic surface in a given colour (sRGB hex), cached by name."""
    return kit.surface(name, kit._hex(hexcol), dust=0.05, rough=rough, var=var, bump=bump)


def paint_mat(name, hexcol, chips=0.35, rust=0.2):
    return kit.surface(name, kit._hex(hexcol), chips=chips, rust=rust, dust=0.3, rough=0.5)


# ------------------------------------------------------------------------------------------------ registry
def add(aid, fn, family, kind="part", label=None, tile=None, fit=True, azim=None, fit_axes=(0, 1, 2), **props):
    """Register an asset builder. fn() returns the top mesh object (kit frame, origin at the mount point)."""
    REG.append(dict(id=aid, fn=fn, family=family, kind=kind, label=label or aid, tile=tile, fit=fit, azim=azim, fit_axes=fit_axes, props=props))


def seg(ob, name, pivot_kit=None, parent=None):
    """Mark `ob` as the game segment `name`; optionally move its origin to `pivot_kit` (kit frame, world) and parent."""
    ob["segment"] = name
    if pivot_kit is not None:
        kit.recentre(ob, pivot_kit)
    if parent is not None:
        bpy.context.view_layer.update()
        w = ob.location.copy() if ob.parent is None else ob.matrix_world.translation.copy()
        ob.parent = parent
        pw = Vector((0, 0, 0))
        q = parent
        while q is not None:
            pw += q.location
            q = q.parent
        ob.location = w - pw
    return ob


# ------------------------------------------------------------------------------------------------ finalise
def _mesh_bounds(obs, frame):
    """Bounds of mesh objects expressed in `frame` (an object's local frame)."""
    inv = frame.matrix_world.inverted()
    lo = Vector((1e9,) * 3)
    hi = Vector((-1e9,) * 3)
    for o in obs:
        if o.type != "MESH" or not len(o.data.vertices):
            continue
        mw = inv @ o.matrix_world
        for v in o.data.vertices:
            w = mw @ v.co
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
    return lo, hi


def _join(owner, others):
    if not others:
        return
    for o in others:
        o.hide_set(False) if hasattr(o, "hide_set") else None
    with bpy.context.temp_override(active_object=owner, selected_editable_objects=[owner] + others, object=owner):
        bpy.ops.object.join()


def _pieces(top):
    """Group the hierarchy into rigid pieces: [(owner, [mesh objects of that piece])]; owner = top or a segment."""
    out = []

    def walk(owner, ob):
        for c in ob.children:
            if "segment" in c:
                out.append((c, []))
                walk(c, c)
            else:
                for oo, lst in out:
                    if oo is owner:
                        lst.append(c)
                walk(owner, c)

    out.append((top, []))
    walk(top, top)
    return out


def _flatten(top):
    """Merge non-segment children into their piece owner (one mesh per rigid piece)."""
    bpy.context.view_layer.update()
    for owner, members in _pieces(top):
        meshes = [m for m in members if m.type == "MESH"]
        # re-parent segment children of members to the owner first (keep world transforms)
        for m in members:
            for c in list(m.children):
                if "segment" in c:
                    mw = c.matrix_world.copy()
                    c.parent = owner
                    c.matrix_world = mw
        for m in [m for m in members if m.type != "MESH"]:
            bpy.data.objects.remove(m)
        if owner.type == "MESH" and meshes:
            _join(owner, meshes)
    bpy.context.view_layer.update()


def game_box(aid, segname=None):
    g = GAME.get(aid)
    if not g:
        return None
    b = g["segments"].get(segname, {}).get("bounds") if segname else g.get("bounds")
    if not b:
        return None
    (x0, y0, z0), (x1, y1, z1) = b
    if x1 - x0 < 1e-4 and y1 - y0 < 1e-4:
        return None
    return Vector((x0, -z1, y0)), Vector((x1, -z0, y1))   # kit frame


def _fit(ob, box, axes=(0, 1, 2), tol=0.05, abs_tol=0.02):
    """Fit the piece's mesh bounds (own local frame) to the game box: per-axis affine when off by more than tol."""
    lo, hi = _mesh_bounds([ob], ob)
    glo, ghi = box
    dev = 0.0
    sc, off = [1.0] * 3, [0.0] * 3
    changed = False
    for a in axes:
        ge, he = ghi[a] - glo[a], hi[a] - lo[a]
        if ge < 1e-4 or he < 1e-4:
            continue
        d = max(abs(lo[a] - glo[a]), abs(hi[a] - ghi[a]))
        dev = max(dev, d / max(ge, 0.05))
        if d > max(abs_tol, tol * ge):
            sc[a] = ge / he
            off[a] = glo[a] - lo[a] * sc[a]
            changed = True
    if changed:
        mtx = Matrix.Translation(Vector(off)) @ Matrix.Diagonal((*sc, 1.0))
        ob.data.transform(mtx)
        for c in ob.children:                           # keep child pivots on the stretched piece
            c.location = mtx @ c.location
    return {"dev_before": round(dev, 3), "scale": [round(s, 3) for s in sc], "fitted": changed}


def _mirror(root):
    for ob in [root] + list(root.children_recursive):
        if ob is not root:
            ob.location.x = -ob.location.x
            e = ob.rotation_euler
            ob.rotation_euler = (e.x, -e.y, -e.z)
        if ob.type == "MESH":
            ob.data.transform(Matrix.Diagonal((-1.0, 1.0, 1.0, 1.0)))
            bm = bmesh.new()
            bm.from_mesh(ob.data)
            bmesh.ops.reverse_faces(bm, faces=bm.faces)
            bm.to_mesh(ob.data)
            bm.free()
            ob.data.update()


def _uv(objs):
    meshes = [o for o in objs if o.type == "MESH"]
    if not meshes:
        return
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.003, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")


def finalise(a, top):
    aid = a["id"]
    _flatten(top)
    pieces = _pieces(top)
    fits = {}
    if a["fit"]:
        for owner, _ in pieces:
            sname = owner.get("segment")
            box = game_box(aid, sname)
            if box and owner.type == "MESH":
                fits[sname or "body"] = _fit(owner, box, a["fit_axes"])
    if "segment" not in top:
        top.name = aid + "_body"
        top.data.name = aid + "_body"
    root = bpy.data.objects.new(aid, None)
    root.empty_display_size = 0.25
    bpy.context.scene.collection.objects.link(root)
    top.parent = root
    top.location = (0, 0, 0) if "segment" not in top else top.location
    # names: body mesh + segments (segment names may clash across assets; the per-asset .blend fixes them)
    if "segment" not in top:
        top.name = aid + "_body"
        top.data.name = aid + "_body"
    for ob in root.children_recursive:
        if "segment" in ob:
            ob.name = aid + ":" + ob["segment"]
            if ob.type == "MESH":
                ob.data.name = aid + "_" + ob["segment"]
    _mirror(root)
    objs = [root] + list(root.children_recursive)
    _uv(objs)
    g = GAME.get(aid, {})
    props = dict(a["props"])
    root["game_id"] = aid
    root["kind"] = a["kind"]
    root["family"] = a["family"]
    root["axes"] = AXES
    for k in ("category", "mass", "size"):
        if k in g and k not in props:
            props[k if k != "size" else "size_class"] = g[k]
    if "size" in props:
        props["size_class"] = props.pop("size")
    segs = {}
    for ob in root.children_recursive:
        if "segment" in ob:
            par = ob.parent["segment"] if (ob.parent is not None and "segment" in ob.parent) else aid
            gp = ob.matrix_world.translation
            segs[ob["segment"]] = {"parent": par, "pivot_game": [round(-gp.x, 4), round(gp.z, 4), round(-gp.y, 4)]}
    if segs:
        props["segments"] = json.dumps(segs)
    for k, v in props.items():
        root[k] = v
    tr = kit.tris([root])
    lo, hi = rc.bounds(objs)
    coll = bpy.data.collections.new(aid)
    bpy.context.scene.collection.children.link(coll)
    for o in objs:
        for c in list(o.users_collection):
            c.objects.unlink(o)
        coll.objects.link(o)
    return root, {"id": aid, "kind": a["kind"], "family": a["family"], "label": a["label"], "tris": tr,
                  "size_m_game_xyz": [round(hi.x - lo.x, 3), round(hi.z - lo.z, 3), round(hi.y - lo.y, 3)],
                  "fit": fits, "segments": segs, "props": {k: v for k, v in props.items() if k != "segments"}}


# ------------------------------------------------------------------------------------------------ save one asset
def save_asset(root, path):
    """Write the asset's collection (root + children, meshes, materials) as its own .blend with clean segment names."""
    coll = bpy.data.collections[root.name]
    renamed = []
    for ob in coll.objects:
        if "segment" in ob:
            want = ob["segment"]
            clash = bpy.data.objects.get(want)
            if clash is not None and clash is not ob:
                clash.name = clash.name + "~tmp"
                renamed.append(clash)
            old = ob.name
            ob.name = want
            renamed.append((ob, old))
    sc = bpy.data.scenes.new(root.name)
    sc.collection.children.link(coll)
    sc.unit_settings.system = "METRIC"
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.data.libraries.write(path, {sc}, fake_user=False, compress=True)
    bpy.data.scenes.remove(sc)
    for r in renamed:
        if isinstance(r, tuple):
            r[0].name = r[1]
    for r in renamed:
        if not isinstance(r, tuple) and r.name.endswith("~tmp"):
            r.name = r.name[:-4]


# ------------------------------------------------------------------------------------------------ run a family
def run(family, out_sub="parts_all", per_tile=6, cols=3, gap=0.3, only=None):
    """Build all registered assets of this script, render tiles (`per_tile` assets each), save per-asset .blends and
    a manifest. `only`: list of ids (from argv after --)."""
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = only or [a for a in argv if not a.startswith("-")]
    out = os.path.join(PREVIEW, out_sub)
    base = HERE if out_sub == "parts_all" else os.path.join(HD, "items")
    kit.stage(out)
    try:
        for name in ("Sun", "Fill"):
            lt = bpy.data.lights.get(name)
            lt.shadow_maximum_resolution = 0.002
            lt.shadow_filter_radius = 1.5
    except Exception:
        pass
    M.clear()
    M.update(kit.mats())
    extra_mats(M)
    t0 = time.time()
    built = []
    for a in REG:
        if only and a["id"] not in only and a["tile"] not in only:
            continue
        try:
            top = a["fn"]()
        except Exception as e:  # keep going; the manifest records the failure
            import traceback
            traceback.print_exc()
            built.append((a, None, {"id": a["id"], "error": repr(e)}))
            continue
        root, st = finalise(a, top)
        built.append((a, root, st))
        print("BUILT", a["id"], st["tris"], st["size_m_game_xyz"], "fit:", {k: v["dev_before"] for k, v in st["fit"].items()})
    # tiles: group by explicit tile name, else chunks of per_tile in registration order
    groups, order = {}, []
    auto, n_auto = [], 0
    for a, root, st in built:
        if root is None:
            continue
        if a["tile"]:
            if a["tile"] not in groups:
                groups[a["tile"]] = []
                order.append(a["tile"])
            groups[a["tile"]].append((a, root, st))
        else:
            auto.append((a, root, st))
    for i in range(0, len(auto), per_tile):
        n_auto += 1
        name = "%s_%02d" % (family, n_auto)
        groups[name] = auto[i:i + per_tile]
        order.append(name)
    tiles = []
    for name in order:
        items = groups[name]
        roots = [r for _, r, _ in items]
        for r in roots:
            r.location = (0, 0, 0)
        kit.grid(roots, min(cols, len(roots)), gap)
        for r in roots:                                   # rest everything on the stage floor
            lo, _ = rc.bounds([r] + list(r.children_recursive))
            r.location.z -= lo.z
        bpy.context.view_layer.update()
        azim = next((a["azim"] for a, _, _ in items if a["azim"] is not None), -45.0)
        objs = [o for r in roots for o in [r] + list(r.children_recursive)]
        rc.render_tile(name, objs, out, azim=azim)
        for o in bpy.context.scene.objects:
            o.hide_render = False
        for r in roots:
            r.location = (0, 0, 0)
        ids = [a["id"] for a, _, _ in items]
        tiles.append({"name": name, "label": ", ".join(a["label"] for a, _, _ in items), "replaces": ", ".join(ids)})
        for _, _, st in items:
            st["tile"] = name
    bpy.context.view_layer.update()
    for a, root, st in built:
        if root is None:
            continue
        p = os.path.join(base, "blend", a["id"].replace(":", "_") + ".blend")
        save_asset(root, p)
        st["blend"] = os.path.relpath(p, HD)
    kit.update_tiles(tiles, out)
    os.makedirs(os.path.join(base, "manifest"), exist_ok=True)
    mpath = os.path.join(base, "manifest", family + ".json")
    old = json.load(open(mpath)) if (only and os.path.exists(mpath)) else []
    ids = {st["id"] for _, _, st in built}
    json.dump([s for s in old if s["id"] not in ids] + [st for _, _, st in built], open(mpath, "w"), indent=1)
    print("FAMILY", family, len(built), "assets", len(tiles), "tiles", "%.0fs" % (time.time() - t0))


# ------------------------------------------------------------------------------------------------ modelling helpers
def bolts(p, mat, pts, r=0.008, h=0.01, axis="Z", segs=6):
    for q in pts:
        p.cyl(mat, r, h, q, axis, segs, bevel=0.0015)


def plate(p, mat, size, loc, rot=(0, 0, 0), rivets=None, rivet_mat=None, inset=0.02, bevel=0.006):
    """Box plate with a ring of rivets on its +face of the thinnest axis."""
    p.box(mat, size, loc, rot, bevel=bevel)
    if not rivets:
        return
    sx, sy, sz = size
    ax = min(range(3), key=lambda i: size[i])
    loc = Vector(loc)
    R = Matrix.Rotation(rot[2], 3, "Z") @ Matrix.Rotation(rot[1], 3, "Y") @ Matrix.Rotation(rot[0], 3, "X")
    u, v = [i for i in range(3) if i != ax]
    n = rivets
    for i in range(n):
        for side in (-1, 1):
            for w in (u, v):
                q = [0.0, 0.0, 0.0]
                q[ax] = size[ax] / 2 + 0.002
                other = v if w == u else u
                q[w] = side * (size[w] / 2 - inset)
                q[other] = -size[other] / 2 + inset + (size[other] - 2 * inset) * i / max(n - 1, 1)
                p.add(bm_cyl(0.007, 0.008, 6), rivet_mat or M["chrome"], matrix=Matrix.Translation(loc + R @ Vector(q)) @ _axis_mtx(ax, R))


def _axis_mtx(ax, R):
    base = {0: Matrix.Rotation(math.pi / 2, 3, "Y"), 1: Matrix.Rotation(-math.pi / 2, 3, "X"), 2: Matrix.Identity(3)}[ax]
    return (R @ base).to_4x4()


def hose(p, mat, pts, r=0.012, segs=8):
    p.tube(mat, pts, r, segs)


def strap(p, mat, a, b, w=0.03, t=0.006):
    """Flat strap from a to b (kit frame)."""
    a, b = Vector(a), Vector(b)
    kit.oriented(p, bm_box(w, t, (b - a).length), mat, a, b)


def rod(p, mat, a, b, r, segs=10, bevel=0.0):
    a, b = Vector(a), Vector(b)
    kit.oriented(p, bm_cyl(r, (b - a).length, segs, bevel), mat, a, b)


def ring(p, mat, loc, R, r, axis="Z", segs=24, rsegs=6):
    bm = kit.bm_torus(R, r, segs, rsegs)
    kit._orient(bm, axis)
    p.add(bm, mat, loc)


def lens(p, mat, loc, r, axis="-Y", depth=0.01, segs=20):
    d = Vector({"-Y": (0, -1, 0), "Y": (0, 1, 0), "X": (1, 0, 0), "-X": (-1, 0, 0), "Z": (0, 0, 1), "-Z": (0, 0, -1)}[axis])
    q = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
    p.add(bm_lathe([(r, 0.0), (r * 0.6, depth * 0.7), (0, depth)], segs), mat, matrix=Matrix.Translation(Vector(loc)) @ q)


def stencil_x(p, mat, centre, size, axis_n="X", t=0.003, w=0.012):
    """Embossed X on a face (normal axis_n)."""
    c = Vector(centre)
    for s in (1, -1):
        if axis_n in ("X", "-X"):
            a, b = c + Vector((0, -size / 2, -s * size / 2)), c + Vector((0, size / 2, s * size / 2))
        elif axis_n in ("Y", "-Y"):
            a, b = c + Vector((-size / 2, 0, -s * size / 2)), c + Vector((size / 2, 0, s * size / 2))
        else:
            a, b = c + Vector((-size / 2, -s * size / 2, 0)), c + Vector((size / 2, s * size / 2, 0))
        rod(p, mat, a, b, w * 0.5, 4)
