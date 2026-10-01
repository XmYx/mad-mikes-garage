"""Build + render the HD road-car pack.

    blender -b -P tools/blender/hd/cars/build.py -- <out_dir> [Name ...] [--no-blend] [--extras]

Per vehicle: <out_dir>/<Name>.png + <Name>_px.png (render_common tile), cars/blend/<Name>.blend, and
<out_dir>/stats/<Name>.json (triangle counts per object, dimensions, sockets). `--extras` renders the exploded Sedan and
the weathered-vs-clean comparison. `merge` (python3 build.py merge <out_dir>) writes tiles.json from the stats.
"""
import json
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path[:0] = [HERE, os.path.dirname(HERE)]

LABELS = {
    "Interceptor": ("V8 Interceptor", "Interceptor"), "Scavenger": ("Scavenger 4x4", "Scavenger"), "Trabant": ("Trabant P601", "Trabant"),
    "Pickup": ("Pickup (round lamps)", "Pickup"), "Coupe": ("Coupe (fastback)", "Coupe"), "Sedan": ("Sedan 4-door", "Sedan"),
    "Wagon": ("Woody Wagon", "Wagon"), "TowTruck": ("Tow Truck (winch + crane)", "TowTruck"), "Wrecker": ("Wrecker crane truck", "Wrecker"),
    "DuneBuggy": ("Dune Buggy", "DuneBuggy"), "MonsterTruck": ("Monster Truck", "MonsterTruck"),
    "Fiat126p": ("Fiat 126p", "Fiat126p"), "Fiat500": ("Fiat 500", "Fiat500"), "FiatMultipla": ("Fiat Multipla", "FiatMultipla"),
    "LanciaYpsilon": ("Lancia Ypsilon", "LanciaYpsilon"), "Peugeot205": ("Peugeot 205", "Peugeot205"), "Peugeot206": ("Peugeot 206", "Peugeot206"),
    "Peugeot207CC": ("Peugeot 207 CC", "Peugeot207CC"), "Peugeot405": ("Peugeot 405", "Peugeot405"),
    "Peugeot406Break": ("Peugeot 406 Break", "Peugeot406Break"), "Renault5": ("Renault 5", "Renault5"), "CitroenBX": ("Citroen BX", "CitroenBX"),
    "CitroenXM": ("Citroen XM", "CitroenXM"), "CitroenXantia": ("Citroen Xantia", "CitroenXantia"),
    "Sedan_exploded": ("Sedan - exploded parts", "Sedan (part structure)"),
    "Pickup_wear": ("Pickup - clean vs weathered", "Pickup (wear levels)"),
}


def merge(out):
    tiles = []
    for k, (lab, rep) in LABELS.items():
        if os.path.exists(os.path.join(out, k + ".png")):
            tiles.append({"name": k, "label": lab, "replaces": rep})
        if os.path.exists(os.path.join(out, k + "_side.png")):
            tiles.append({"name": k + "_side", "label": lab + " - side vs real outline", "replaces": rep})
    json.dump(tiles, open(os.path.join(out, "tiles.json"), "w"), indent=1)
    print("tiles", len(tiles))


def stage_tweaks(sc):
    """Finer sun shadow maps: at the 128 px pixel-mode render EEVEE's virtual shadow maps are coarse and speckle."""
    import bpy
    for name in ("Sun", "Fill"):
        lt = bpy.data.lights.get(name)
        if lt:
            lt.shadow_maximum_resolution = 0.002
            lt.shadow_filter_radius = 1.5
    sc.eevee.shadow_resolution_scale = 1.0


def uv_unwrap(objs):
    """One shared UV atlas per vehicle (smart project over all parts, packed together) for texture baking."""
    import bpy
    bpy.ops.object.select_all(action="DESELECT")
    meshes = [o for o in objs if o.type == "MESH"]
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.002, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")


def main():
    import bpy
    import hdlib as H
    import render_common as rc
    import refbuild
    roster = refbuild.ROSTER

    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = argv[0] if argv else os.path.join(HERE, "out")
    flags = [a for a in argv[1:] if a.startswith("--")]
    names = [a for a in argv[1:] if not a.startswith("--")] or list(roster)
    os.makedirs(os.path.join(out, "stats"), exist_ok=True)
    os.makedirs(os.path.join(HERE, "blend"), exist_ok=True)
    for name in names:
        t0 = time.time()
        rc.reset_scene()
        sc = rc.setup_stage()
        stage_tweaks(sc)
        H.WEAR, H.TAG = 1.0, ""
        H.MATDEF.clear()
        H.std_mats()
        veh = refbuild.build(name, outline=True)
        root, objs = veh.build()
        ref = [o for o in objs if o.name.startswith("RefOutline")]
        objs = [o for o in objs if o not in ref]
        if "--no-uv" not in flags:
            uv_unwrap(objs)
        rc.render_tile(name, objs, out)
        rc.render_tile(name + "_side", objs + ref, out, elev=0.0, azim=90.0)
        for o in ref:
            bpy.data.objects.remove(o)
        st = {"name": name, "tris_total": H.tris(objs), "objects": {o.name: H.tris([o]) for o in objs}, "info": veh.info,
              "props": {o.name: {k: (v if isinstance(v, (int, float, str, bool)) else list(v)) for k, v in o.items()} for o in objs}}
        json.dump(st, open(os.path.join(out, "stats", name + ".json"), "w"), indent=1)
        if "--no-blend" not in flags:
            bpy.context.preferences.filepaths.save_version = 0
            bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "blend", name + ".blend"), compress=True)
        print("[cars] %s: %d tris, %.1fs" % (name, st["tris_total"], time.time() - t0), flush=True)
    if "--extras" in flags:
        import extras
        extras.run(out)
    merge(out)


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "merge":
        merge(sys.argv[2])
    else:
        main()
