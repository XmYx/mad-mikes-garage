"""HD character wardrobe for the game: per body shape (M rugged, M2 lean, F soft, F2 sharp) one rig in rest pose with
the full body, eyes, brows, every hair style (plus a hat-cut variant), the full beard, and every garment of
garments.GARMENTS fitted to that body, all skinned to the 16 HumanRig bones. Skin and hair use one neutral tone each
(the exporter marks them as tintable: mask B), so ClothingLibrary / Appearance can recolour them at runtime.

    blender -b -P tools/blender/hd/character/build_wardrobe.py -- [M,M2,F,F2]

Saves character/blend/wardrobe_<KEY>.blend (collection wardrobe_<KEY>, rig wardrobe_<KEY>_Rig, meshes
wardrobe_<KEY>_<piece>). Export: tools/blender/hd/export/run_export.py Character_<KEY>.
"""
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))

import bpy  # noqa: E402
import numpy as np  # noqa: E402

import render_common as RC  # noqa: E402
import hdlib as H  # noqa: E402
import body as B  # noqa: E402
import build_lib as BL  # noqa: E402
import garments as G  # noqa: E402
import materials as MAT  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
KEYS = argv[0].split(",") if argv else ["M", "M2", "F", "F2"]
SPECS = {"M": ("m", "rugged", 1.0, 1.0), "M2": ("m", "lean", 1.02, 0.95), "F": ("f", "soft", 0.96, 0.9), "F2": ("f", "sharp", 0.95, 0.88)}
SKIN = 1                       # neutral tone (tinted in game)
HAIR = 2                       # mid brown (tinted in game)
HAT_CUT = min(v for v in G.HEADWEAR.values() if v > 0)      # the lowest crown cut fits under every hat
OUT = os.environ.get("HD_WARDROBE_OUT", os.path.join(HERE, "blend"))


def mat_for(key):
    hexc, kind, opt = G.MATS[key]
    return MAT.make("m_" + key, hexc, kind, **opt)


def build(key):
    t0 = time.time()
    RC.reset_scene()
    hum = BL.Human(key, *SPECS[key])
    hum.build_body()
    name = "wardrobe_" + key
    coll = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(coll)
    arm = H.make_armature(name + "_Rig", hum.skel, coll)
    body = hum.body
    skin = MAT.make("skin_tint", B.SKIN_TONES[SKIN][0], "skin")
    ob = BL.make_object(name + "_Body", body["mesh"], arm, skin, coll, body["weights"], copy_mesh=True)
    a = ob.data.color_attributes.new("Col", "FLOAT_COLOR", "POINT")
    a.data.foreach_set("color", hum.body_paint(0).astype(np.float32).ravel())
    hc = MAT.make("hair_tint", B.HAIR_COLORS[HAIR], "hair", grime=0.1)
    heads = [BL.build_brows(hum), BL.build_beard(hum, 2)]
    for st in B.HAIR_STYLES:
        if st == "Bald":
            continue
        heads.append(BL.build_hair(hum, st))
        heads.append(BL.build_hair(hum, st, HAT_CUT))
    for pc in heads:
        if pc:
            nm = pc["name"].replace("_cut%.3f" % HAT_CUT, "_cut")
            BL.make_object(f"{name}_{nm}", pc["mesh"], arm, hc, coll, pc["weights"])
    em = MAT.make("eye_4a3420", "e8e0d4", "eye", hex2="4a3420")
    for i, (me, loc) in enumerate(BL.build_eyes(hum)):
        eo = BL.rigid_object(f"{name}_Eye{i}", me, arm, "Head", em, coll)
        eo.location = loc
    n = 0
    for gid, (fn, slot) in G.GARMENTS.items():
        for spec in fn(hum):
            lo, hi = hum.bounds(spec["parts"], 0.17, spec.get("extra"))
            kw = {k: spec[k] for k in ("res", "tris", "allowed", "smooth", "sharp", "sym", "hides", "inner") if k in spec}
            pc = hum.piece(spec["name"], spec["field"], lo, hi, **kw)
            if not pc:
                continue
            pn = spec["name"] if spec["name"].startswith(gid) else gid + "_" + spec["name"]
            go = BL.make_object(f"{name}_{pn}", pc["mesh"], arm,
                                mat_for(spec["mat"]), coll, pc["weights"], copy_mesh=True)   # pieces are cached by name: tshirt / tshirt_worn share one
            go["garment"] = gid
            go["slot"] = slot
            n += 1
    arm["body_shape"] = key
    arm["skin_tone_hex"] = B.SKIN_TONES[SKIN][0]
    arm["hair_colour_hex"] = B.HAIR_COLORS[HAIR]
    os.makedirs(OUT, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, name + ".blend"), compress=True)
    BL.log(f"{name}: {n} garment pieces, {time.time() - t0:.0f}s")


for k in KEYS:
    build(k)
